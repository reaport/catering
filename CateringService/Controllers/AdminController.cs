using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CateringService.Models;
using CateringService.Services;
using Newtonsoft.Json;

namespace CateringService.Controllers
{
    public class AdminController : Controller
    {
        private readonly ICapacityService _capacityService;
        private readonly IMealTypeService _mealTypeService;
        private readonly ICommModeService _commModeService;
        private readonly ICateringProcessService _cateringProcessService;
        private readonly IVehicleRegistry _vehicleRegistry;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            ICapacityService capacityService,
            IMealTypeService mealTypeService,
            ICommModeService commModeService,
            ICateringProcessService cateringProcessService,
            IVehicleRegistry vehicleRegistry,
            ILogger<AdminController> logger)
        {
            _capacityService = capacityService;
            _mealTypeService = mealTypeService;
            _commModeService = commModeService;
            _cateringProcessService = cateringProcessService;
            _vehicleRegistry = vehicleRegistry;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var capacity = _capacityService.GetCapacity();
            var mealTypes = new MealTypesResponse { MealTypes = _mealTypeService.GetMealTypes() };
            var vehicles = _vehicleRegistry.GetAllVehicles();
            var model = new AdminDashboardViewModel
            {
                Capacity = capacity,
                MealTypes = mealTypes,
                Vehicles = new List<VehicleStatusInfo>(vehicles)
            };

            ViewBag.MealTypesJson = JsonConvert.SerializeObject(mealTypes.MealTypes, Formatting.Indented);
            ViewBag.UseMock = _commModeService.UseMock;
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Index(
            string actionType,
            double? newCapacity,
            string mealTypesJson,
            string aircraftId,
            string nodeId,
            string mealsJson,
            bool? useMock)
        {
            try
            {
                switch (actionType)
                {
                    case "ToggleCommMode":
                        if (useMock.HasValue)
                        {
                            _commModeService.UseMock = useMock.Value;
                            _logger.LogInformation("Admin set UseMock={UseMock}", useMock.Value);
                            TempData["Message"] = "Режим общения изменён.";
                        }
                        break;

                    case "UpdateCapacity":
                        if (!newCapacity.HasValue || newCapacity < 0)
                        {
                            TempData["Error"] = "Неверное значение вместимости.";
                        }
                        else
                        {
                            _capacityService.UpdateCapacity(new VehicleCapacity { Capacity = newCapacity.Value });
                            _logger.LogInformation("Admin updated capacity to {Capacity}", newCapacity.Value);
                            TempData["Message"] = "Вместимость успешно обновлена.";
                        }
                        break;

                    case "UpdateMealTypes":
                        try
                        {
                            var mealTypes = JsonConvert.DeserializeObject<List<string>>(mealTypesJson);
                            if (mealTypes == null)
                                throw new Exception("Ошибка десериализации типов питания.");
                            _mealTypeService.UpdateMealTypes(mealTypes);
                            _logger.LogInformation("Admin updated meal types: {MealTypes}", string.Join(", ", mealTypes));
                            TempData["Message"] = "Типы питания успешно обновлены.";
                        }
                        catch (Exception ex)
                        {
                            TempData["Error"] = "Ошибка при обновлении типов питания: " + ex.Message;
                            _logger.LogError(ex, "Error updating meal types");
                        }
                        break;

                    case "RequestCatering":
                        try
                        {
                            var mealOrders = JsonConvert.DeserializeObject<List<MealOrder>>(mealsJson);
                            var request = new CateringRequest
                            {
                                AircraftId = aircraftId,
                                NodeId = nodeId,
                                Meals = mealOrders
                            };
                            // Запускаем процесс в фоне, если AJAX-запрос
                            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                            {
                                Task.Run(() => _cateringProcessService.ProcessCateringRequest(request));
                                return Json(new { message = "Запрос на доставку питания запущен. Обновления статусов будут отображаться в реальном времени." });
                            }
                            else
                            {
                                Task.Run(() => _cateringProcessService.ProcessCateringRequest(request));
                                TempData["Message"] = "Запрос на доставку питания запущен. Обновления статусов будут отображаться в реальном времени.";
                            }
                        }
                        catch (Exception ex)
                        {
                            TempData["Error"] = "Ошибка при обработке запроса: " + ex.Message;
                            _logger.LogError(ex, "Error in RequestCatering");
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Внутренняя ошибка сервера.";
                _logger.LogError(ex, "Error in AdminController POST");
            }

            return RedirectToAction("Index");
        }
    }
}
