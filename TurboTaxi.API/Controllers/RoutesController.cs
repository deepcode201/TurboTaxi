using MediatR;
using Microsoft.AspNetCore.Mvc;
using TurboTaxi.Application.Interfaces;
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
            => Ok(await _service.EstimateAsync(request));
    }
}
