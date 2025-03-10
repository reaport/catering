using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CateringService.Models;
using CateringService.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace CateringService.Services
{
    public class CateringProcessService : ICateringProcessService
    {
        private readonly IGroundControlClient _groundClient;
        private readonly IVehicleRegistry _vehicleRegistry;
        private readonly ICapacityService _capacityService;
        private readonly IHubContext<VehicleStatusHub> _hubContext;
        private readonly ILogger<CateringProcessService> _logger;
        private const double VehicleSpeed = 10;

        // Словарь для контроля количества машин для каждого рейса (максимум 2)
        private static readonly Dictionary<string, int> FlightVehicleCount = new Dictionary<string, int>();
        private static readonly object FlightLock = new object();

        public CateringProcessService(
            IGroundControlClient groundClient,
            IVehicleRegistry vehicleRegistry,
            ICapacityService capacityService,
            IHubContext<VehicleStatusHub> hubContext,
            ILogger<CateringProcessService> logger)
        {
            _groundClient = groundClient;
            _vehicleRegistry = vehicleRegistry;
            _capacityService = capacityService;
            _hubContext = hubContext;
            _logger = logger;
        }

        private async Task WaitUntilFlightVehicleCountLessThan(string flightId, int limit)
        {
            while (true)
            {
                int count;
                lock (FlightLock)
                {
                    FlightVehicleCount.TryGetValue(flightId, out count);
                }
                if (count < limit)
                    break;
                _logger.LogInformation("Waiting: already {Count} vehicles working for flight {FlightId}", count, flightId);
                await Task.Delay(1000);
            }
        }

        private void IncrementFlightVehicleCount(string flightId)
        {
            lock (FlightLock)
            {
                if (FlightVehicleCount.ContainsKey(flightId))
                    FlightVehicleCount[flightId]++;
                else
                    FlightVehicleCount[flightId] = 1;
            }
        }

        private void DecrementFlightVehicleCount(string flightId)
        {
            lock (FlightLock)
            {
                if (FlightVehicleCount.ContainsKey(flightId))
                {
                    FlightVehicleCount[flightId]--;
                    if (FlightVehicleCount[flightId] < 0)
                        FlightVehicleCount[flightId] = 0;
                }
            }
        }

        public async Task<CateringResponse> ProcessCateringRequest(CateringRequest request)
        {
            try
            {
                // Проверка входных данных
                if (string.IsNullOrEmpty(request.AircraftId))
                    throw new ArgumentException("FlightId is required");
                if (request.Meals == null || request.Meals.Count == 0)
                    throw new ArgumentException("At least one meal order is required");

                _logger.LogInformation("Processing catering request for FlightId: {FlightId}", request.AircraftId);

                // Суммируем общее количество заказанных порций
                int totalMeals = request.Meals.Sum(m => m.Count);

                double capacity = _capacityService.GetCapacity().Capacity; // количество порций, которое может обработать один автомобиль
                int remainingMeals = totalMeals;

                // Обрабатываем запрос партиями
                while (remainingMeals > 0)
                {
                    // Ждем, пока для данного рейса будет менее 2 машин занято
                    await WaitUntilFlightVehicleCountLessThan(request.AircraftId, 2);

                    // Определяем, сколько машин задействовать в этой партии:
                    // Если оставшиеся заказы превышают вместимость одного автомобиля, используем 2, иначе 1.
                    int vehiclesToDispatch = remainingMeals > capacity ? 2 : 1;

                    // Глобальный лимит: не более 5 машин в системе
                    while (_vehicleRegistry.GetAllVehicles().Count() >= 5 &&
                           _vehicleRegistry.GetAllVehicles().All(v => v.Status == "Busy"))
                    {
                        _logger.LogInformation("Global vehicle limit reached. Waiting for an available vehicle...");
                        await Task.Delay(1000);
                    }

                    List<Task> batchTasks = new List<Task>();
                    for (int i = 0; i < vehiclesToDispatch; i++)
                    {
                        int mealsForVehicle = Math.Min((int)capacity, remainingMeals - i * (int)capacity);
                        await WaitUntilFlightVehicleCountLessThan(request.AircraftId, 2);
                        IncrementFlightVehicleCount(request.AircraftId);
                        batchTasks.Add(ProcessSingleCateringOperation(request, mealsForVehicle, request.AircraftId));
                    }
                    await Task.WhenAll(batchTasks);
                    remainingMeals -= vehiclesToDispatch * (int)capacity;
                    if (remainingMeals < 0)
                        remainingMeals = 0;
                }

                return new CateringResponse { Status = "success", Waiting = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing catering request for FlightId: {FlightId}", request.AircraftId);
                throw;
            }
        }

        private async Task ProcessSingleCateringOperation(CateringRequest request, int mealsForVehicle, string flightId)
        {
            string? vehicleId = null;
            string? baseNode = null;
            // Для доставки питания destination можно вычислить как NodeId (если указан) или использовать значение из реестра
            string destination = request.AircraftId; // Можно адаптировать по необходимости

            // Пытаемся получить свободное транспортное средство
            var vehicleInfo = _vehicleRegistry.AcquireAvailableVehicle(request.AircraftId);
            if (vehicleInfo.VehicleId == null)
            {
                // Если глобальный лимит не достигнут, регистрируем новое; иначе ждём освобождения
                if (_vehicleRegistry.GetAllVehicles().Count() < 5)
                {
                    var regVehicle = await _groundClient.RegisterVehicleAsync("catering");
                    if (regVehicle != null)
                    {
                        // Попытка атомарной регистрации
                        if (!_vehicleRegistry.TryAddVehicle(regVehicle.VehicleId, regVehicle.GarageNodeId, regVehicle.ServiceSpots))
                        {
                            _logger.LogWarning("Global vehicle limit reached. Waiting for an available vehicle...");
                            while (true)
                            {
                                vehicleInfo = _vehicleRegistry.AcquireAvailableVehicle(request.AircraftId);
                                if (!string.IsNullOrEmpty(vehicleInfo.VehicleId))
                                {
                                    vehicleId = vehicleInfo.VehicleId;
                                    baseNode = vehicleInfo.BaseNode;
                                    break;
                                }
                                await Task.Delay(1000);
                            }
                        }
                        else
                        {
                            vehicleId = regVehicle.VehicleId;
                            baseNode = regVehicle.GarageNodeId;
                            _logger.LogInformation("Registered new catering vehicle {VehicleId}", vehicleId);
                        }
                    }
                    else
                    {
                        _logger.LogError("Failed to register new catering vehicle for FlightId {FlightId}", request.AircraftId);
                        DecrementFlightVehicleCount(flightId);
                        return;
                    }
                }
                else
                {
                    // Если уже 5 машин, ждём освобождения
                    while (true)
                    {
                        vehicleInfo = _vehicleRegistry.AcquireAvailableVehicle(request.AircraftId);
                        if (!string.IsNullOrEmpty(vehicleInfo.VehicleId))
                        {
                            vehicleId = vehicleInfo.VehicleId;
                            baseNode = vehicleInfo.BaseNode;
                            break;
                        }
                        _logger.LogInformation("Waiting for available catering vehicle for FlightId {FlightId}", request.AircraftId);
                        await Task.Delay(1000);
                    }
                }
            }
            else
            {
                vehicleId = vehicleInfo.VehicleId;
                baseNode = vehicleInfo.BaseNode;
            }

            // Помечаем транспорт как Busy
            _vehicleRegistry.MarkAsBusy(vehicleId);
            _logger.LogInformation("Vehicle {VehicleId} marked as Busy", vehicleId);
            await _hubContext.Clients.All.SendAsync("ReceiveVehicleUpdate", _vehicleRegistry.GetAllVehicles());
            await Task.Yield();

            // Маршрут от базы до точки доставки питания
            var route = await _groundClient.FetchRouteAsync(baseNode, destination, "catering");
            if (route != null)
            {
                _logger.LogInformation("Route to catering destination: {Route}", string.Join(" -> ", route));
                for (int j = 0; j < route.Length - 1; j++)
                {
                    var dist = await _groundClient.RequestPermissionAsync(vehicleId, route[j], route[j + 1], "catering");
                    if (dist != null)
                    {
                        int delay = (int)Math.Ceiling(dist.Value / VehicleSpeed);
                        _logger.LogInformation("Segment {Index}: delay {Delay} sec", j, delay);
                        await Task.Delay(delay * 1000);
                        await _groundClient.NotifyArrivalAsync(vehicleId, route[j + 1], "catering");
                        await _hubContext.Clients.All.SendAsync("ReceiveVehicleUpdate", _vehicleRegistry.GetAllVehicles());
                        await Task.Yield();
                    }
                }
                // Эмулируем процесс доставки питания (например, загрузка порций)
                _logger.LogInformation("Performing catering operation on vehicle {VehicleId} with Meals: {Meals}", vehicleId, mealsForVehicle);
                await Task.Delay(5000);
            }

            // Обратный маршрут от точки доставки до базы
            var returnRoute = await _groundClient.FetchRouteAsync(destination, baseNode, "catering");
            if (returnRoute != null)
            {
                _logger.LogInformation("Return route for catering vehicle: {Route}", string.Join(" -> ", returnRoute));
                for (int j = 0; j < returnRoute.Length - 1; j++)
                {
                    var dist = await _groundClient.RequestPermissionAsync(vehicleId, returnRoute[j], returnRoute[j + 1], "catering");
                    if (dist != null)
                    {
                        int delay = (int)Math.Ceiling(dist.Value / VehicleSpeed);
                        await Task.Delay(delay * 1000);
                        await _groundClient.NotifyArrivalAsync(vehicleId, returnRoute[j + 1], "catering");
                        await _hubContext.Clients.All.SendAsync("ReceiveVehicleUpdate", _vehicleRegistry.GetAllVehicles());
                        await Task.Yield();
                    }
                }
            }

            // Помечаем транспорт как Available
            _vehicleRegistry.MarkAsAvailable(vehicleId, baseNode);
            _logger.LogInformation("Vehicle {VehicleId} marked as Available", vehicleId);
            await _hubContext.Clients.All.SendAsync("ReceiveVehicleUpdate", _vehicleRegistry.GetAllVehicles());
            await Task.Yield();

            DecrementFlightVehicleCount(flightId);
        }
    }
}
