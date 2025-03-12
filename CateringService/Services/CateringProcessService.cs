﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CateringService.Models;
using CateringService.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace CateringService.Services
{
    public class CateringProcessService : ICateringProcessService
    {
        private readonly IExternalApiService _externalApiService;
        private readonly IVehicleRegistry _vehicleRegistry;
        private readonly ICapacityService _capacityService;
        private readonly IHubContext<VehicleStatusHub> _hubContext;
        private readonly ILogger<CateringProcessService> _logger;
        private readonly IAdminConfigService _adminConfigService;
        private readonly IMealTypeService _mealTypeService;
        private readonly ICommModeService _commModeService;

        private const int MaxVehicles = 5;
        private const int MaxVehiclesPerAircraft = 2;
        private static readonly Dictionary<string, int> FlightVehicleCount = new Dictionary<string, int>();
        private static readonly object FlightLock = new object();

        public CateringProcessService(
            IExternalApiService externalApiService,
            IVehicleRegistry vehicleRegistry,
            ICapacityService capacityService,
            IMealTypeService mealTypeService,
            IHubContext<VehicleStatusHub> hubContext,
            ILogger<CateringProcessService> logger,
            IAdminConfigService adminConfigService,
            ICommModeService commModeService)
        {
            _externalApiService = externalApiService;
            _vehicleRegistry = vehicleRegistry;
            _capacityService = capacityService;
            _hubContext = hubContext;
            _mealTypeService = mealTypeService;
            _logger = logger;
            _adminConfigService = adminConfigService;
            _commModeService = commModeService;
        }

        public async Task<CateringResponse> ProcessCateringRequest(CateringRequest request)
        {
            if (string.IsNullOrEmpty(request.AircraftId))
                throw new ArgumentException("AircraftId is required.");
            if (request.Meals == null || request.Meals.Count == 0)
                throw new ArgumentException("At least one meal order is required.");

            // Получаем список допустимых типов питания
            var allowedMealTypes = _mealTypeService.GetMealTypes();

            // Проверяем, что каждый заказанный тип питания присутствует в списке допустимых
            foreach (var meal in request.Meals)
            {
                if (!allowedMealTypes.Contains(meal.MealType, StringComparer.OrdinalIgnoreCase))
                {
                    throw new ArgumentException($"Invalid meal type: {meal.MealType}. Allowed types are: {string.Join(", ", allowedMealTypes)}.");
                }
            }

            _logger.LogInformation("Processing catering request for AircraftId: {AircraftId}", request.AircraftId);

            // Уведомляем оркестратора о старте доставки питания
            await _externalApiService.NotifyCateringStartAsync(request.AircraftId);

            int totalMeals = request.Meals.Sum(m => m.Count);
            int vehicleCapacity = _capacityService.GetCapacity();
            int remainingMeals = totalMeals;

            while (remainingMeals > 0)
            {
                await WaitUntilFlightVehicleCountLessThan(request.AircraftId, MaxVehiclesPerAircraft);
                int vehiclesToDispatch = remainingMeals > vehicleCapacity ? 2 : 1;

                // Если глобальный лимит достигнут – ждем освобождения транспортных средств
                while (_vehicleRegistry.GetAllVehicles().Count() >= MaxVehicles &&
                       _vehicleRegistry.GetAllVehicles().All(v => v.Status == "Busy"))
                {
                    _logger.LogInformation("Global vehicle limit reached. Waiting for an available vehicle...");
                    await Task.Delay(1000);
                }

                var batchTasks = new List<Task>();
                for (int i = 0; i < vehiclesToDispatch; i++)
                {
                    int mealsForVehicle = Math.Min(vehicleCapacity, remainingMeals - i * vehicleCapacity);
                    await WaitUntilFlightVehicleCountLessThan(request.AircraftId, MaxVehiclesPerAircraft);
                    IncrementFlightVehicleCount(request.AircraftId);
                    batchTasks.Add(ProcessSingleCateringOperation(request, mealsForVehicle, request.AircraftId));
                }
                await Task.WhenAll(batchTasks);
                remainingMeals -= vehiclesToDispatch * vehicleCapacity;
                if (remainingMeals < 0)
                    remainingMeals = 0;
            }

            await _externalApiService.NotifyCateringFinishAsync(request.AircraftId, totalMeals);
            return new CateringResponse { Waiting = true, Status = "success" };
        }


        private async Task ProcessSingleCateringOperation(CateringRequest request, int mealsForVehicle, string flightId)
        {
            string vehicleId = null;
            string baseNode = null;
            string destination = null;

            try
            {
                // Получаем доступное транспортное средство
                var vehicleInfo = _vehicleRegistry.AcquireAvailableVehicle(request.AircraftId);
                if (string.IsNullOrEmpty(vehicleInfo.VehicleId))
                {
                    if (_vehicleRegistry.GetAllVehicles().Count() < MaxVehicles)
                    {
                        var regVehicle = await _externalApiService.RegisterVehicleAsync("catering");
                        if (regVehicle != null && _vehicleRegistry.TryAddVehicle(regVehicle.VehicleId, regVehicle.GarageNodeId, regVehicle.ServiceSpots))
                        {
                            vehicleId = regVehicle.VehicleId;
                            baseNode = regVehicle.GarageNodeId;
                            _logger.LogInformation("Registered new catering vehicle {VehicleId}", vehicleId);
                        }
                        else
                        {
                            _logger.LogError("Failed to register new catering vehicle for AircraftId {AircraftId}", request.AircraftId);
                            DecrementFlightVehicleCount(flightId);
                            return;
                        }
                    }
                    else
                    {
                        while (true)
                        {
                            vehicleInfo = _vehicleRegistry.AcquireAvailableVehicle(request.AircraftId);
                            if (!string.IsNullOrEmpty(vehicleInfo.VehicleId))
                            {
                                vehicleId = vehicleInfo.VehicleId;
                                baseNode = vehicleInfo.BaseNode;
                                break;
                            }
                            _logger.LogInformation("Waiting for available catering vehicle for AircraftId {AircraftId}", request.AircraftId);
                            await Task.Delay(1000);
                        }
                    }
                }
                else
                {
                    vehicleId = vehicleInfo.VehicleId;
                    baseNode = vehicleInfo.BaseNode;
                }

                _vehicleRegistry.MarkAsBusy(vehicleId);
                _logger.LogInformation("Vehicle {VehicleId} marked as Busy", vehicleId);
                await _hubContext.Clients.All.SendAsync("ReceiveVehicleUpdate", _vehicleRegistry.GetAllVehicles());
                await Task.Yield();

                // Формируем конечный узел на основании введенного parking_id и vehicleId
                // То есть, если в запросе передан "parking_1", итоговый destination будет "parking_1_catering_1"
                destination = $"{request.NodeId}_{vehicleId}";

                // Запрашиваем маршрут от текущего узла (baseNode) до сформированного конечного узла (destination)
                List<string> route = await _externalApiService.GetRouteAsync(baseNode, destination, "catering");
                if (route == null || route.Count < 2)
                    throw new Exception($"Route not found from {baseNode} to {destination}");

                _logger.LogInformation("Received route from {BaseNode} to {Destination}: {Route}", baseNode, destination, string.Join(" -> ", route));

                double travelSpeed = _adminConfigService.GetConfig().MovementSpeed;

                // Перемещаем транспортное средство по сегментам маршрута
                for (int i = 0; i < route.Count - 1; i++)
                {
                    string fromNode = route[i];
                    string toNode = route[i + 1];

                    double distance = await _externalApiService.RequestMoveAsync(vehicleId, "catering", fromNode, toNode);
                    int delaySeconds = (int)Math.Ceiling(distance / travelSpeed);
                    _logger.LogInformation("Moving from {FromNode} to {ToNode}, delay {Delay}s", fromNode, toNode, delaySeconds);
                    await Task.Delay(delaySeconds * 1000);

                    await _externalApiService.NotifyArrivalAsync(vehicleId, "catering", toNode);
                    // Обновляем текущее местоположение транспортного средства
                    _vehicleRegistry.UpdateCurrentNode(vehicleId, toNode);
                    await _hubContext.Clients.All.SendAsync("ReceiveVehicleUpdate", _vehicleRegistry.GetAllVehicles());
                    await Task.Yield();
                }

                _logger.LogInformation("Performing catering delivery on vehicle {VehicleId} for {MealCount} meals", vehicleId, mealsForVehicle);
                await Task.Delay(5000);

                // Обратный маршрут: от точки доставки (destination) до базового узла (baseNode)
                List<string> returnRoute = await _externalApiService.GetRouteAsync(destination, baseNode, "catering");
                if (returnRoute != null && returnRoute.Count >= 2)
                {
                    _logger.LogInformation("Return route for vehicle {VehicleId}: {Route}", vehicleId, string.Join(" -> ", returnRoute));
                    for (int i = 0; i < returnRoute.Count - 1; i++)
                    {
                        string fromNode = returnRoute[i];
                        string toNode = returnRoute[i + 1];

                        double distance = await _externalApiService.RequestMoveAsync(vehicleId, "catering", fromNode, toNode);
                        int delaySeconds = (int)Math.Ceiling(distance / travelSpeed);
                        await Task.Delay(delaySeconds * 1000);
                        await _externalApiService.NotifyArrivalAsync(vehicleId, "catering", toNode);
                        _vehicleRegistry.UpdateCurrentNode(vehicleId, toNode);
                        await _hubContext.Clients.All.SendAsync("ReceiveVehicleUpdate", _vehicleRegistry.GetAllVehicles());
                        await Task.Yield();
                    }
                }

                _vehicleRegistry.MarkAsAvailable(vehicleId, baseNode);
                _logger.LogInformation("Vehicle {VehicleId} marked as Available", vehicleId);
                await _hubContext.Clients.All.SendAsync("ReceiveVehicleUpdate", _vehicleRegistry.GetAllVehicles());
                await Task.Yield();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ProcessSingleCateringOperation for flight {FlightId}", flightId);
                if (!string.IsNullOrEmpty(vehicleId) && !string.IsNullOrEmpty(baseNode))
                {
                    _vehicleRegistry.MarkAsAvailable(vehicleId, baseNode);
                    _logger.LogInformation("Vehicle {VehicleId} forcibly marked as Available due to error", vehicleId);
                    await _hubContext.Clients.All.SendAsync("ReceiveVehicleUpdate", _vehicleRegistry.GetAllVehicles());
                }
            }
            finally
            {
                DecrementFlightVehicleCount(flightId);
            }
        }


        private async Task WaitUntilFlightVehicleCountLessThan(string aircraftId, int limit)
        {
            while (true)
            {
                int count;
                lock (FlightLock)
                {
                    FlightVehicleCount.TryGetValue(aircraftId, out count);
                }
                if (count < limit)
                    break;
                _logger.LogInformation("Waiting: already {Count} vehicles working for flight {AircraftId}", count, aircraftId);
                await Task.Delay(5000);
            }
        }

        private void IncrementFlightVehicleCount(string aircraftId)
        {
            lock (FlightLock)
            {
                if (FlightVehicleCount.ContainsKey(aircraftId))
                    FlightVehicleCount[aircraftId]++;
                else
                    FlightVehicleCount[aircraftId] = 1;
            }
        }

        private void DecrementFlightVehicleCount(string aircraftId)
        {
            lock (FlightLock)
            {
                if (FlightVehicleCount.ContainsKey(aircraftId))
                {
                    FlightVehicleCount[aircraftId]--;
                    if (FlightVehicleCount[aircraftId] < 0)
                        FlightVehicleCount[aircraftId] = 0;
                }
            }
        }

        public async Task<bool> RegisterVehicleAsync(string type)
        {
            if (!_commModeService.UseMock)
            {
                if (!_vehicleRegistry.CanRegisterNewVehicle())
                {
                    _logger.LogWarning("Global vehicle limit reached. Cannot register new vehicle of type {Type}.", type);
                    return false;
                }
                var regVehicle = await _externalApiService.RegisterVehicleAsync(type);
                if (regVehicle != null && _vehicleRegistry.TryAddVehicle(regVehicle.VehicleId, regVehicle.GarageNodeId, regVehicle.ServiceSpots))
                {
                    _logger.LogInformation("Vehicle {VehicleId} registered successfully.", regVehicle.VehicleId);
                    return true;
                }
                else
                {
                    _logger.LogError("Failed to register vehicle of type {Type}.", type);
                    return false;
                }
            }
            else
            {
                _logger.LogInformation("Mock mode: registration request for type {Type} received.", type);
                return true;
            }
        }

        public async Task ReloadAsync()
        {
            _vehicleRegistry.Reset();
            lock (FlightLock)
            {
                FlightVehicleCount.Clear();
            }
            _logger.LogInformation("System reloaded: vehicle registry and flight counters reset.");
            await Task.CompletedTask;
        }

        public IEnumerable<CateringVehicleInfo> GetVehiclesInfo()
        {
            return _vehicleRegistry.GetAllVehicles().Select(v => new CateringVehicleInfo
            {
                VehicleId = v.VehicleId,
                Status = v.Status,
                CurrentNode = v.CurrentNode
            });
        }
    }
}
