using Microsoft.AspNetCore.Mvc;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Models.PromoCodes;

namespace TurboTaxi.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PromoCodesController : ControllerBase
    {
        private readonly IPromoCodeService _promoCodeService;
        private readonly ILogger<PromoCodesController> _logger;

        public PromoCodesController(IPromoCodeService promoCodeService, ILogger<PromoCodesController> logger)
        {
            _promoCodeService = promoCodeService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<ActionResult<CreatePromoCodeResponse>> Create([FromBody] CreatePromoCodeRequest request, CancellationToken ct)
        {
            try
            {
                var result = await _promoCodeService.CreateAsync(request, ct);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create promo code for user {UserId}", request.UserId);
                return StatusCode(500, new { error = "Failed to create promo code", details = ex.Message });
            }
        }

        [HttpPost("apply")]
        public async Task<ActionResult<ApplyPromoCodeResponse>> Apply([FromBody] ApplyPromoCodeRequest request, CancellationToken ct)
        {
            try
            {
                var result = await _promoCodeService.ApplyAsync(request, ct);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply promo code {PromoCodeId} for user {UserId}", request.PromoCodeId, request.UserId);
                return StatusCode(500, new { error = "Failed to apply promo code", details = ex.Message });
            }
        }

        [HttpDelete("{promoCodeId}")]
        public async Task<IActionResult> Delete(int promoCodeId, CancellationToken ct)
        {
            try
            {
                await _promoCodeService.DeleteAsync(promoCodeId, ct);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete promo code {PromoCodeId}", promoCodeId);
                return StatusCode(500, new { error = "Failed to delete promo code", details = ex.Message });
            }
        }
    }
}
