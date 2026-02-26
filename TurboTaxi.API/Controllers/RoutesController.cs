using MediatR;
using Microsoft.AspNetCore.Mvc;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Domain.Enums;
using TurboTaxi.Models.Routes;

namespace TurboTaxi.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RoutesController : ControllerBase
    {
        private readonly IRouteEstimationService _service;
        public RoutesController(IRouteEstimationService service) => _service = service;

        [HttpPost("estimate")]
        public async Task<ActionResult<RouteEstimateResponse>> Estimate([FromBody] RouteEstimateRequest request)
        {
            if (!Enum.IsDefined(typeof(VehicleType), request.VehicleType))
            {
                var validValues = string.Join(", ", Enum.GetValues<VehicleType>()
                    .Select(v => $"{(int)v} = {v}"));

                return BadRequest(new RouteEstimateResponse
                {
                    Success = false,
                    Message = $"Invalid VehicleType: '{(int)request.VehicleType}'. Valid values are: {validValues}"
                });
            }

            return Ok(await _service.EstimateAsync(request));
        }
    }
}
