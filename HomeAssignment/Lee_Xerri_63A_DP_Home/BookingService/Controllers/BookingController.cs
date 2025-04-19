using BookingService.DTO;
using BookingService.Models;
using FirebaseAdmin.Auth;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using System.Collections;
namespace BookingService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BookingController : ControllerBase
    {
        public static class CabTypeRules
        {
            // maps each CabType to its max allowed passengers
            public static readonly IReadOnlyDictionary<CabType, int> MaxPassengersByCabType =
                new Dictionary<CabType, int>
                {
            { CabType.Economic, 8 },
            { CabType.Premium, 8 },
            { CabType.Executive, 8 }
                };
        }

        private readonly FirestoreDb _db;
        private readonly FirebaseAuth _auth;
        

        public BookingController(FirestoreDb db, FirebaseAuth auth)
        {
            _db = db;
            _auth = auth;
        }

        // returns the UID if valid, otherwise null
        private async Task<string?> ValidateTokenAsync(string bearer)
        {
            if (string.IsNullOrEmpty(bearer) || !bearer.StartsWith("Bearer "))
                return null;

            var token = bearer.Substring("Bearer ".Length);
            var decoded = await _auth.VerifyIdTokenAsync(token);
            return decoded.Uid;
        }

        // create a new booking
        [HttpPost]
        public async Task<IActionResult> Create(
            [FromHeader(Name = "Authorization")] string authHeader,
            [FromBody] CreateBookingDto dto)
        {
            var uid = await ValidateTokenAsync(authHeader);
            if (uid == null)
                return Unauthorized();

            // validate cabType
            // if cabtype does not exist in the enum, or if it is not in the dictionary
            if (!Enum.TryParse<CabType>(dto.CabType, ignoreCase: true, out var cabTypeEnum) || !CabTypeRules.MaxPassengersByCabType.ContainsKey(cabTypeEnum))
            {
                var validTypes = string.Join(", ", Enum.GetNames<CabType>());
                return BadRequest(new
                {
                    error = $"Invalid cab type '{dto.CabType}'. Valid values are: {validTypes}."
                });
            }

            // validate passenger amount
            
            if (dto.Passengers < 1 || dto.Passengers > 8) // if less than 1 or more than max
            {
                return BadRequest(new
                {
                    error = $"Cabs only support 1–8 passengers."
                });
            }

            var booking = new Models.Booking
            {
                Id = Guid.NewGuid().ToString(),
                UserUid = uid,
                StartLocation = dto.StartLocation,
                EndLocation = dto.EndLocation,
                DateTime = Timestamp.FromDateTime(dto.DateTime.ToUniversalTime()),
                Passengers = dto.Passengers,
                CabType = dto.CabType
            };

            await _db
              .Collection("bookings")
              .Document(booking.Id)
              .SetAsync(booking);

            return Ok(new { booking.Id });
        }

        // view current (future) bookings for this user
        [HttpGet("current")]
        public async Task<IActionResult> GetCurrent(
            [FromHeader(Name = "Authorization")] string authHeader)
        {
            var uid = await ValidateTokenAsync(authHeader);
            if (uid == null)
                return Unauthorized();

            var now = Timestamp.FromDateTime(DateTime.UtcNow);
            var query = _db
              .Collection("bookings")
              .WhereEqualTo("UserUid", uid)
              .WhereGreaterThanOrEqualTo("DateTime", now)
              .OrderBy("DateTime");

            var snaps = await query.GetSnapshotAsync();
            var list = snaps.Documents
                .Select(d => d.ConvertTo<Models.Booking>())
                .Select(b => new BookingDto
                {
                    Id = b.Id,
                    StartLocation = b.StartLocation,
                    EndLocation = b.EndLocation,
                    DateTime = b.DateTime.ToDateTime(),
                    Passengers = b.Passengers,
                    CabType = b.CabType
                })
                .ToList();

            return Ok(list);
        }

        // view past bookings for this user
        [HttpGet("past")]
        public async Task<IActionResult> GetPast(
            [FromHeader(Name = "Authorization")] string authHeader)
        {
            var uid = await ValidateTokenAsync(authHeader);
            if (uid == null)
                return Unauthorized();

            var now = Timestamp.FromDateTime(DateTime.UtcNow);
            var query = _db
              .Collection("bookings")
              .WhereEqualTo("UserUid", uid)
              .WhereLessThan("DateTime", now)
              .OrderByDescending("DateTime");

            var snaps = await query.GetSnapshotAsync();
            var list = snaps.Documents
                .Select(d => d.ConvertTo<Models.Booking>())
                .Select(b => new BookingDto
                {
                    Id = b.Id,
                    StartLocation = b.StartLocation,
                    EndLocation = b.EndLocation,
                    DateTime = b.DateTime.ToDateTime(),
                    Passengers = b.Passengers,
                    CabType = b.CabType
                })
                .ToList();

            return Ok(list);
        }
    }
}
