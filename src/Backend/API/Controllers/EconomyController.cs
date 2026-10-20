using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.DTOs;
using Services.Interfaces;

namespace API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class EconomyController : ControllerBase
    {
        private readonly IEconomyService _economyService;

        public EconomyController(IEconomyService economyService)
        {
            _economyService = economyService;
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }

        [HttpGet("currencies")]
        public async Task<IActionResult> GetCurrencies()
        {
            var userId = GetCurrentUserId();
            var balances = await _economyService.GetCurrenciesBalanceAsync(userId);
            return Ok(balances);
        }

        [HttpGet("shop")]
        public async Task<IActionResult> GetShopItems([FromQuery] string? type)
        {
            var userId = GetCurrentUserId();
            var items = await _economyService.GetShopItemsAsync(userId, type);
            return Ok(items);
        }

        [HttpPost("buy")]
        public async Task<IActionResult> BuyCosmetic([FromBody] BuyCosmeticRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _economyService.BuyCosmeticAsync(userId, request.CosmeticId);
            if (!result.Success) return BadRequest(new { message = result.Message });
            return Ok(new { message = result.Message });
        }

        [HttpGet("my-inventory")]
        public async Task<IActionResult> GetMyCosmetics()
        {
            var userId = GetCurrentUserId();
            var list = await _economyService.GetMyCosmeticsAsync(userId);
            return Ok(list);
        }

        [HttpPost("equip")]
        public async Task<IActionResult> EquipCosmetic([FromBody] EquipCosmeticRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _economyService.EquipCosmeticAsync(userId, request.CosmeticId, request.Equip);
            if (!result.Success) return BadRequest(new { message = result.Message });
            return Ok(new { message = result.Message });
        }
    }
}
