using FirebaseAdmin.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LocationService.DTO;
using LocationService.Services;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace LocationService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class LocationController : ControllerBase
    {
        private readonly ILocationService _locSvc;

        public LocationController(ILocationService locSvc)
        {
            _locSvc = locSvc;
        }

        private string? GetUid() =>
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        [HttpPost]
        public async Task<IActionResult> Add([FromBody] CreateLocationDTO dto)
        {
            var uid = GetUid(); if (uid == null) return Unauthorized();
            var result = await _locSvc.CreateAsync(uid, dto);
            return Ok(result);
        }

        [HttpGet]
        public async Task<IActionResult> List()
        {
            var uid = GetUid();
            if (uid == null)
            {
                return Unauthorized();
            }
            var list = await _locSvc.GetAllAsync(uid);
            return Ok(list);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(string id)
        {
            var uid = GetUid(); if (uid == null) return Unauthorized();
            var loc = await _locSvc.GetByIdAsync(uid, id);
            return Ok(loc);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] CreateLocationDTO dto)
        {
            var uid = GetUid();
            if (uid == null)
            {
                return Unauthorized();
            }
            await _locSvc.UpdateAsync(uid, id, dto);
            return Ok($"ID : {id}\nResult: Successfully changed to \"{dto.Name}\".");
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var uid = GetUid(); if (uid == null) return Unauthorized();
            await _locSvc.DeleteAsync(uid, id);
            return Ok($"ID : {id}\nResult: Successfully Deleted.");
        }

        [HttpGet("{id}/weather")]
        public async Task<IActionResult> Weather(string id)
        {
            var uid = GetUid();
            if (uid == null)
            {
                return Unauthorized();
            }
            var locDto = await _locSvc.GetByIdAsync(uid, id);
            var forecast = await _locSvc.GetWeatherAsync(locDto.Name);
            return Ok(forecast);
        }
    }
}