using CustomerService.Services;
using FirebaseAdmin.Auth;
using LeeXerri_CustomerService.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;

namespace LeeXerri_CustomerService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly ICustomerService _svc;
        private readonly FirebaseAuth _auth;

        public UserController(ICustomerService svc, FirebaseAuth auth)
        {
            _svc = svc;
            _auth = auth;
        }

        // read the UID out of the validated JWT
        private string? GetUid() =>
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDTO dto)
        {
            var (uid, email, username) = await _svc.RegisterAsync(dto);
            return Ok(new { uid, email, username });
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO dto)
        {
            try
            {
                var resp = await _svc.LoginAsync(dto);
                return Ok(resp);
            }
            catch
            {
                return Unauthorized("Invalid credentials.");
            }
        }

        [Authorize]
        [HttpGet("{uid}")]
        public async Task<IActionResult> GetProfile(string uid)
        {
            var me = GetUid();
            if (me == null || me != uid) return Forbid();

            try
            {
                var (user, inbox) = await _svc.GetProfileAsync(uid);
                return Ok(new { user, inbox });
            }
            catch (KeyNotFoundException)
            {
                return NotFound("User not found.");
            }
        }

        [Authorize]
        [HttpPost("{uid}/notifications")]
        public async Task<IActionResult> SendNotification(string uid, [FromBody] string message)
        {
            var me = GetUid();
            if (me == null || me != uid) return Forbid();

            await _svc.SendNotificationAsync(uid, message);
            return Ok();
        }

        [Authorize]
        [HttpGet("{uid}/notifications")]
        public async Task<IActionResult> GetNotifications(string uid)
        {
            var me = GetUid();
            if (me == null || me != uid) return Forbid();

            var notes = await _svc.GetNotificationsAsync(uid);
            return Ok(notes);
        }
    }
}
