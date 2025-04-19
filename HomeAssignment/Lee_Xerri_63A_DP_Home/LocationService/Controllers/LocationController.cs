using FirebaseAdmin.Auth;
using Google.Cloud.Firestore;
using LocationService.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using LocationService.Models;
using System.IdentityModel.Tokens.Jwt;
using LocationService.Services;

namespace LocationService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class LocationController : ControllerBase
    {
        private readonly FirestoreDb _db;
        private readonly FirebaseAuth _auth;
        private readonly ILocationService _locService;

        public LocationController(FirestoreDb db, FirebaseAuth auth, ILocationService locService)
        {
            _db = db;
            _auth = auth;
            _locService = locService;
        }

        private string? GetUid() => User.FindFirstValue(ClaimTypes.NameIdentifier)
                                   ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        [HttpPost]
        public async Task<IActionResult> Add([FromBody] CreateLocationDTO dto)
        {
            var uid = GetUid();
            if (uid == null) return Unauthorized();

            // Geocode address
            var (lat, lon) = await _locService.GetCoordinatesAsync(dto.Address);

            // Save to Firestore
            var loc = new Location
            {
                Id = Guid.NewGuid().ToString(),
                UserUid = uid,
                Name = dto.Name,
                Address = dto.Address,
                Latitude = lat,
                Longitude = lon,
                CreatedAt = Timestamp.GetCurrentTimestamp()
            };
            await _db.Collection("locations").Document(loc.Id).SetAsync(loc);

            return Ok(new { loc.Id, loc.Name, loc.Address, loc.Latitude, loc.Longitude });
        }

        [HttpGet]
        public async Task<IActionResult> List()
        {
            var uid = GetUid();
            if (uid == null) return Unauthorized();

            var snaps = await _db.Collection("locations")
                                 .WhereEqualTo("UserUid", uid)
                                 .OrderByDescending("CreatedAt")
                                 .GetSnapshotAsync();
            var list = snaps.Documents.Select(d => d.ConvertTo<Location>()).Select(l => new LocationDTO
            {
                Id = l.Id,
                Name = l.Name,
                Address = l.Address,
                Latitude = l.Latitude,
                Longitude = l.Longitude
            }).ToList();
            return Ok(list);
        }

        [HttpGet("{id}/weather")]
        public async Task<IActionResult> Weather(string id)
        {
            var uid = GetUid();
            if (uid == null) return Unauthorized();

            var doc = await _db.Collection("locations").Document(id).GetSnapshotAsync();
            if (!doc.Exists) return NotFound();

            var loc = doc.ConvertTo<Location>();
            if (loc.UserUid != uid) return Forbid();

            var forecast = await _locService.GetWeatherAsync(loc.Latitude, loc.Longitude);
            return Ok(forecast);
        }
    }
}
