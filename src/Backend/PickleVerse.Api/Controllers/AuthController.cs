using Microsoft.AspNetCore.Mvc;
using PickleVerse.Core.Services;

namespace PickleVerse.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;

        public AuthController()
        {
            _authService = new AuthService(); // In real app, this should be injected via DI
        }

        public class RegisterRequest
        {
            public string Username { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        [HttpPost("register")]
        public IActionResult Register([FromBody] RegisterRequest request)
        {
            if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest("Invalid request");
            }

            var profile = _authService.Register(request.Username, request.Email, request.Password);
            
            // In a real app we'd save to DB here
            
            return Ok(new { profile.Id, profile.Username, profile.DuprRating });
        }

        public class LoginRequest
        {
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            // For now just return a dummy token since DB lookup isn't implemented
            return Ok(new { Token = "dummy-jwt-token" });
        }
    }
}
