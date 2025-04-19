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
            [FromBody] CreateBookingDTO dto)
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
                CabType = dto.CabType,
                Paid = false
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
                .Select(b => new BookingDTO
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
                .Select(b => new BookingDTO
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

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id,
        [FromHeader(Name = "Authorization")] string? authHeader)
        {
            var uid = await ValidateTokenAsync(authHeader);
            if (uid == null) return Unauthorized();

            var snap = await _db
              .Collection("bookings")
              .Document(id)
              .GetSnapshotAsync();
            if (!snap.Exists) return NotFound();

            var booking = snap.ConvertTo<Models.Booking>();
            if (booking.UserUid != uid) return Forbid();

            return Ok(new BookingDTO
            {
                Id = booking.Id,
                StartLocation = booking.StartLocation,
                EndLocation = booking.EndLocation,
                DateTime = booking.DateTime.ToDateTime(),
                Passengers = booking.Passengers,
                CabType = booking.CabType,
                Paid = booking.Paid
            });
        }

        [HttpPost("{id}/mark-paid")]
        public async Task<IActionResult> MarkPaid( string id,
        [FromHeader(Name = "Authorization")] string authHeader)
        {
            var uid = await ValidateTokenAsync(authHeader);
            if (uid == null) return Unauthorized();

            var docRef = _db.Collection("bookings").Document(id);
            var snap = await docRef.GetSnapshotAsync();
            if (!snap.Exists) return NotFound();
            var booking = snap.ConvertTo<Booking>();
            if (booking.Paid)
                return BadRequest("This booking has already been paid.");

            await docRef.UpdateAsync("Paid", true);
            return Ok();
        }
    }
}
