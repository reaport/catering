using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using CateringService.Models;
using CateringService.Services;
using System.Threading.Tasks;

namespace CateringService.Controllers
{
    [ApiController]
    [Route("")]
    public class CateringController : ControllerBase
    {
        private readonly ICateringProcessService _cateringProcessService;
        private readonly IMealTypeService _mealTypeService;
        private readonly ILogger<CateringController> _logger;

        public CateringController(
            ICateringProcessService cateringProcessService,
            IMealTypeService mealTypeService,
            ILogger<CateringController> logger)
        {
            _cateringProcessService = cateringProcessService;
            _mealTypeService = mealTypeService;
            _logger = logger;
        }

        [HttpPost("request")]
        public async Task<IActionResult> RequestCatering([FromBody] CateringRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.AircraftId) || request.Meals == null || request.Meals.Count == 0)
                {
                    _logger.LogWarning("RequestCatering: Invalid input parameters.");
                    return BadRequest(new ErrorResponse { Error = "Invalid input parameters." });
                }

                var response = await _cateringProcessService.ProcessCateringRequest(request);
                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "RequestCatering: Invalid input.");
                return BadRequest(new ErrorResponse { Error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RequestCatering: Internal server error.");
                return StatusCode(500, new ErrorResponse { Error = "InternalServerError" });
            }
        }

        [HttpGet("mealtypes")]
        public IActionResult GetMealTypes()
        {
            try
            {
                var allowedMealTypes = _mealTypeService.GetMealTypes();
                var response = new MealTypesResponse { MealTypes = allowedMealTypes };
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting meal types");
                return StatusCode(500, new ErrorResponse { Error = "InternalServerError" });
            }
        }
    }
}
