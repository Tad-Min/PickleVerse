using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Entities.DTOs;
using Services.Interfaces;

namespace API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class EconomyController : ControllerBase
    {
        private readonly IEconomyService _economyService;
        private readonly Services.Interfaces.IOnlineGameService _game;

        public EconomyController(IEconomyService economyService, Services.Interfaces.IOnlineGameService game)
        {
            _economyService = economyService;
            _game = game;
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
            return Ok(await _game.LegacyCosmetic(userId, request.CosmeticId, true, true, Request.Headers["Idempotency-Key"].FirstOrDefault() ?? Guid.NewGuid().ToString("N")));
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
            return Ok(await _game.LegacyCosmetic(userId, request.CosmeticId, false, request.Equip, Request.Headers["Idempotency-Key"].FirstOrDefault() ?? Guid.NewGuid().ToString("N")));
        }
    }
}
