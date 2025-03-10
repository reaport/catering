using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using CateringService.Models;
using CateringService.Services;

namespace CateringService.Controllers
{
    [ApiController]
    [Route("")]
    public class CateringController : ControllerBase
    {
        private readonly ICateringProcessService _cateringProcessService;
        private readonly ILogger<CateringController> _logger;

        public CateringController(ICateringProcessService cateringProcessService, ILogger<CateringController> logger)
        {
            _cateringProcessService = cateringProcessService;
            _logger = logger;
        }

        /// <summary>
        /// Публичный эндпоинт для запроса доставки питания.
        /// Если входные данные некорректны, возвращается 400; при внутренней ошибке – 500.
        /// </summary>
        [HttpPost("request")]
        public async Task<IActionResult> RequestCatering([FromBody] CateringRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.AircraftId) || request.Meals == null || request.Meals.Count == 0)
                {
                    _logger.LogWarning("RequestCatering: Некорректные входные параметры.");
                    return BadRequest(new ErrorResponse { Error = "Некорректные входные параметры." });
                }
                var response = await _cateringProcessService.ProcessCateringRequest(request);
                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "RequestCatering: Invalid meal order.");
                return BadRequest(new ErrorResponse { Error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RequestCatering: Внутренняя ошибка сервера.");
                return StatusCode(500, new ErrorResponse { Error = "InternalServerError" });
            }
        }

        /// <summary>
        /// Возвращает список типов питания.
        /// </summary>
        [HttpGet("mealtypes")]
        public IActionResult GetMealTypes()
        {
            try
            {
                var mealTypes = new MealTypesResponse
                {
                    MealTypes = new System.Collections.Generic.List<string> { "Standard", "Vegetarian", "Vegan", "Gluten-Free" }
                };
                return Ok(mealTypes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetMealTypes: Внутренняя ошибка сервера.");
                return StatusCode(500, new ErrorResponse { Error = "InternalServerError" });
            }
        }
    }
}
