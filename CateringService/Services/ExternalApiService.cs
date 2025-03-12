using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using CateringService.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace CateringService.Services
{
    public class ExternalApiService : IExternalApiService
    {
        private readonly HttpClient _externalApiClient;
        private readonly HttpClient _orchestratorClient;
        private readonly ILogger<ExternalApiService> _logger;
        private readonly ICommModeService _commModeService;
        private const int MaxRetries = 30;

        public ExternalApiService(IHttpClientFactory httpClientFactory, ILogger<ExternalApiService> logger, ICommModeService commModeService)
        {
            _externalApiClient = httpClientFactory.CreateClient("ExternalApi");
            _orchestratorClient = httpClientFactory.CreateClient("Orchestrator");
            _logger = logger;
            _commModeService = commModeService;
        }

        public async Task<RegisterVehicleResponse> RegisterVehicleAsync(string type)
        {
            if (_commModeService.UseMock)
            {
                _logger.LogInformation("MOCK: Registering catering vehicle of type {Type}", type);
                await Task.Delay(200);
                return new RegisterVehicleResponse
                {
                    VehicleId = $"catering_{Guid.NewGuid().ToString().Substring(0, 8)}",
                    GarageNodeId = "garrage_catering_1",
                    ServiceSpots = new Dictionary<string, string>
                    {
                        { "parking_1", "parking_1_catering_1" },
                        { "parking_2", "parking_2_catering_1" }
                    }
                };
            }

            _logger.LogInformation("Отправляем запрос на регистрацию транспортного средства типа: {Type}", type);
            var response = await _externalApiClient.PostAsJsonAsync<object>($"/register-vehicle/{type}", null);
            _logger.LogInformation("Получен ответ на регистрацию, статус: {StatusCode}", response.StatusCode);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<RegisterVehicleResponse>();
            string responseBody = JsonSerializer.Serialize(result);
            _logger.LogInformation("Ответ на регистрацию: {ResponseBody}", responseBody);
            return result;
        }

        public async Task<List<string>> GetRouteAsync(string from, string to, string vehicleType)
        {
            if (_commModeService.UseMock)
            {
                _logger.LogInformation("MOCK: Возвращаем фиктивный маршрут от {From} до {To}", from, to);
                await Task.Delay(100);
                return new List<string> { from, "MockIntermediate", to };
            }

            var payload = new { from, to, type = vehicleType };
            string requestBody = JsonConvert.SerializeObject(payload, Formatting.Indented);
            _logger.LogInformation("Отправляем запрос маршрута:\n{RequestBody}", requestBody);

            var response = await _externalApiClient.PostAsJsonAsync<object>("/route", payload);
            _logger.LogInformation("Получен ответ на запрос маршрута, статус: {StatusCode}", response.StatusCode);
            response.EnsureSuccessStatusCode();
            var nodes = await response.Content.ReadFromJsonAsync<List<string>>();
            if (nodes == null || nodes.Count < 2)
                throw new Exception("Неверный формат маршрута или недостаточное количество узлов.");
            _logger.LogInformation("Полученный маршрут от {From} до {To}: {Route}",
                from, to, string.Join(" -> ", nodes));
            return nodes;
        }

        public async Task<double> RequestMoveAsync(string vehicleId, string vehicleType, string from, string to)
        {
            if (_commModeService.UseMock)
            {
                _logger.LogInformation("MOCK: Возвращаем фиктивное расстояние для транспортного средства {VehicleId}", vehicleId);
                await Task.Delay(100);
                return 50.0;
            }

            var payload = new { vehicleId, vehicleType, from, to };
            string requestBody = JsonSerializer.Serialize(payload);
            _logger.LogInformation("Отправляем запрос на перемещение:\n{RequestBody}", requestBody);

            HttpResponseMessage response = null;
            int retryCount = 0;
            while (true)
            {
                response = await _externalApiClient.PostAsJsonAsync<object>("/move", payload);
                _logger.LogInformation("Получен ответ на запрос перемещения, статус: {StatusCode}", response.StatusCode);

                if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    _logger.LogWarning("Conflict при запросе перемещения от {From} до {To} для ТС {VehicleId}. Ожидание 2 секунды и повторная попытка.", from, to, vehicleId);
                    await Task.Delay(TimeSpan.FromSeconds(2));
                    retryCount++;
                    if (retryCount >= MaxRetries)
                    {
                        _logger.LogError("Превышено число попыток ({MaxRetries}) для запроса перемещения от {From} до {To} для ТС {VehicleId}.", MaxRetries, from, to, vehicleId);
                        break;
                    }
                    continue;
                }
                break;
            }

            response.EnsureSuccessStatusCode();
            var moveResponse = await response.Content.ReadFromJsonAsync<MoveResponse>();
            string responseBody = JsonSerializer.Serialize(moveResponse);
            _logger.LogInformation("Ответ на перемещение: {ResponseBody}", responseBody);
            return moveResponse.Distance;
        }

        public async Task NotifyArrivalAsync(string vehicleId, string vehicleType, string nodeId)
        {
            if (_commModeService.UseMock)
            {
                _logger.LogInformation("MOCK: Уведомляем о прибытии ТС {VehicleId} в узел {NodeId}", vehicleId, nodeId);
                await Task.Delay(50);
                return;
            }

            var payload = new { vehicleId, vehicleType, nodeId };
            string requestBody = JsonSerializer.Serialize(payload);
            _logger.LogInformation("Отправляем уведомление о прибытии:\n{RequestBody}", requestBody);

            var response = await _externalApiClient.PostAsJsonAsync<object>("/arrived", payload);
            _logger.LogInformation("Получен ответ на уведомление о прибытии, статус: {StatusCode}", response.StatusCode);
            response.EnsureSuccessStatusCode();
        }

        public async Task NotifyCateringStartAsync(string flightId)
        {
            if (_commModeService.UseMock)
            {
                _logger.LogInformation("MOCK: Уведомляем о старте доставки питания для рейса {FlightId}", flightId);
                await Task.Delay(100);
                return;
            }

            var payload = new { aircraft_id = flightId };
            string requestBody = JsonSerializer.Serialize(payload);
            _logger.LogInformation("Отправляем уведомление о старте доставки питания:\n{RequestBody}", requestBody);

            var response = await _orchestratorClient.PostAsJsonAsync<object>("/catering/start", payload);
            _logger.LogInformation("Получен ответ на уведомление о старте, статус: {StatusCode}", response.StatusCode);
            response.EnsureSuccessStatusCode();
        }

        public async Task NotifyCateringFinishAsync(string flightId, int totalMeals)
        {
            if (_commModeService.UseMock)
            {
                _logger.LogInformation("MOCK: Уведомляем о завершении доставки питания для рейса {FlightId} с количеством порций: {TotalMeals}", flightId, totalMeals);
                await Task.Delay(100);
                return;
            }

            var payload = new { aircraft_id = flightId, meal_count = totalMeals };
            string requestBody = JsonSerializer.Serialize(payload);
            _logger.LogInformation("Отправляем уведомление о завершении доставки питания:\n{RequestBody}", requestBody);

            var response = await _orchestratorClient.PostAsJsonAsync<object>("/catering/finish", payload);
            _logger.LogInformation("Получен ответ на уведомление о завершении, статус: {StatusCode}", response.StatusCode);
            response.EnsureSuccessStatusCode();
        }
    }
}
