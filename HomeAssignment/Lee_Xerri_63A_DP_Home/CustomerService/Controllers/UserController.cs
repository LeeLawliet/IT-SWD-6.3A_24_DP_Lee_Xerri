using CustomerService.Services;
using FirebaseAdmin.Auth;
using LeeXerri_CustomerService.DTO;
using Microsoft.AspNetCore.Mvc;
using System;
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

        private async Task<string?> ValidateTokenAsync(string bearer)
        {
            if (string.IsNullOrEmpty(bearer) || !bearer.StartsWith("Bearer "))
                return null;

            var token = bearer.Substring("Bearer ".Length);
            var decoded = await _auth.VerifyIdTokenAsync(token);
            return decoded.Uid;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDTO dto)
        {
            var (uid, email, username) = await _svc.RegisterAsync(dto);
            return Ok(new { uid, email, username });
        }

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

        [HttpGet("{uid}")]
        public async Task<IActionResult> GetProfile(
            [FromHeader(Name = "Authorization")] string authHeader,
            string uid)
        {
            var me = await ValidateTokenAsync(authHeader);
            if (me == null || me != uid)
                return Forbid();

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


        [HttpPost("{uid}/notifications")]
        public async Task<IActionResult> SendNotification(
            [FromHeader(Name = "Authorization")] string authHeader,
            string uid,
            [FromBody] string message)
        {
            var me = await ValidateTokenAsync(authHeader);
            if (me == null || me != uid)
                return Forbid();

            await _svc.SendNotificationAsync(uid, message);
            return Ok();
        }

        [HttpGet("{uid}/notifications")]
        public async Task<IActionResult> GetNotifications(
            [FromHeader(Name = "Authorization")] string authHeader,
            string uid)
        {
            var me = await ValidateTokenAsync(authHeader);
            if (me == null || me != uid)
                return Forbid();

            var notes = await _svc.GetNotificationsAsync(uid);
            return Ok(notes);
        }
    }
}
