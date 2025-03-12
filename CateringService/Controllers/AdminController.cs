using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using CateringService.Models;
using CateringService.Services;
using System.Collections.Generic;

namespace CateringService.Controllers
{
    [Route("/admin")]
    public class AdminController : Controller
    {
        private readonly IAdminConfigService _adminConfigService;
        private readonly ICateringProcessService _cateringProcessService;
        private readonly IVehicleRegistry _vehicleRegistry;
        private readonly ICommModeService _commModeService;
        private readonly IMealTypeService _mealTypeService;
        private readonly ICapacityService _capacityService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            IAdminConfigService adminConfigService,
            ICateringProcessService cateringProcessService,
            IVehicleRegistry vehicleRegistry,
            ICommModeService commModeService,
            ICapacityService capacityService,
            IMealTypeService mealTypeService,
            ILogger<AdminController> logger)
        {
            _adminConfigService = adminConfigService;
            _cateringProcessService = cateringProcessService;
            _vehicleRegistry = vehicleRegistry;
            _commModeService = commModeService;
            _capacityService = capacityService;
            _mealTypeService = mealTypeService;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var model = new AdminViewModel
            {
                Config = _adminConfigService.GetConfig(),
                Vehicles = _cateringProcessService.GetVehiclesInfo(),
                Mode = _commModeService.UseMock ? "Mock" : "Real",
                MealTypesList = _mealTypeService.GetMealTypes()
            };
            return View(model);
        }

        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] UpdateConfigRequest updateRequest)
        {
            if (updateRequest == null)
            {
                _logger.LogWarning("UpdateConfig: Empty request body.");
                return BadRequest("Invalid configuration data.");
            }

            _logger.LogInformation("Received configuration update: {@UpdateRequest}", updateRequest);

            var config = new AdminConfig
            {
                ConflictRetryCount = updateRequest.ConflictRetryCount,
                MovementSpeed = updateRequest.MovementSpeed,
                NumberOfCateringVehicles = updateRequest.NumberOfCateringVehicles
            };

            _adminConfigService.UpdateConfig(config);
            _logger.LogInformation("Configuration updated: RetryCount={RetryCount}, MovementSpeed={Speed}, Vehicles={Vehicles}",
                config.ConflictRetryCount, config.MovementSpeed, config.NumberOfCateringVehicles);

            return Ok(new { message = "Configuration updated successfully." });
        }

        [HttpPost("toggleMode")]
        public IActionResult ToggleMode([FromForm] bool useMock)
        {
            _commModeService.UseMock = useMock;
            _logger.LogInformation("Admin set UseMock={UseMock}", useMock);
            TempData["Message"] = "Communication mode updated.";
            return RedirectToAction("Index");
        }

        [HttpPost("updateMealTypes")]
        public IActionResult UpdateMealTypes([FromBody] MealTypesResponse request)
        {
            if (request == null || request.MealTypes == null)
            {
                _logger.LogWarning("UpdateMealTypes: Invalid request body.");
                return BadRequest(new ErrorResponse { Error = "Invalid meal types data." });
            }

            _mealTypeService.UpdateMealTypes(request.MealTypes);
            _logger.LogInformation("Meal types updated: {MealTypes}", string.Join(", ", request.MealTypes));
            return Ok(new { message = "Meal types updated successfully." });
        }



        [HttpPost("updateCapacityAdmin")]
        public IActionResult UpdateCapacityAdmin([FromBody] VehicleCapacity capacity)
        {
            if (capacity == null || capacity.Capacity < 0)
            {
                _logger.LogWarning("Invalid capacity value provided.");
                return BadRequest(new ErrorResponse { Error = "Invalid capacity value." });
            }
            _capacityService.UpdateCapacity((int)capacity.Capacity);
            _logger.LogInformation("Capacity updated to {Capacity}", capacity.Capacity);
            return Ok(new { message = "Capacity updated successfully.", capacity = capacity.Capacity });
        }


        [HttpPost("registerVehicle")]
        public async Task<IActionResult> RegisterVehicle([FromBody] RegisterVehicleRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.Type))
            {
                _logger.LogWarning("Registration request: Empty or missing Type.");
                return BadRequest(new { error = "Type is required." });
            }

            _logger.LogInformation("Registration request received: Type = {Type}", request.Type);

            int numberOfVehicles = _adminConfigService.GetConfig().NumberOfCateringVehicles;
            _logger.LogInformation("Registering {Count} vehicles of type {Type}", numberOfVehicles, request.Type);

            int successfulRegistrations = 0;
            for (int i = 0; i < numberOfVehicles; i++)
            {
                bool success = await _cateringProcessService.RegisterVehicleAsync(request.Type);
                if (success)
                {
                    successfulRegistrations++;
                }
            }

            if (successfulRegistrations > 0)
            {
                _logger.LogInformation("{Count} vehicles of type {Type} registered.", successfulRegistrations, request.Type);
            }

            return Ok(new { registeredVehicles = successfulRegistrations, message = "Vehicles registered successfully." });
        }

        [HttpPost("reload")]
        public async Task<IActionResult> Reload()
        {
            _logger.LogInformation("Received request to reload application. All data will be reset.");
            await _cateringProcessService.ReloadAsync();
            _logger.LogInformation("Application reloaded: Vehicle registry and counters reset.");
            return RedirectToAction("Index");
        }
    }
}
