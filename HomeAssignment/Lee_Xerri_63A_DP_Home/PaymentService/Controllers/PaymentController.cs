using FirebaseAdmin.Auth;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentService.DTO;
using PaymentService.Fares;
using LocationService.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Linq;
using BookingService.DTO;
using System.Text.Json;
using BookingService.Services;
using BookingService.Models;

namespace PaymentService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PaymentController : ControllerBase
    {
        private readonly ILocationService _locSvc;
        private readonly FirestoreDb _db;
        private readonly FirebaseAuth _auth;
        private readonly IFareService _fareSvc;
        private readonly IBookingService _bookSvc;

        private static readonly IReadOnlyDictionary<string, double> CabMult = new Dictionary<string, double>
        {
            ["Economic"] = 1.0,
            ["Premium"] = 1.2,
            ["Executive"] = 1.4
        };

        public PaymentController(
            ILocationService locSvc,
            FirestoreDb db,
            FirebaseAuth auth,
            IFareService fareSvc,
            IBookingService bookSvc)
        {
            _locSvc = locSvc;
            _db = db;
            _auth = auth;
            _fareSvc = fareSvc;
            _bookSvc = bookSvc;
        }

        private string? GetUid() =>
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        [HttpPost]
        public async Task<IActionResult> Pay([FromBody] CreatePaymentDTO dto)
        {
            var uid = GetUid();
            if (uid == null) return Unauthorized();

            // fetch booking via IBookingService
            var booking = await _bookSvc.GetByIdAsync(uid, dto.BookingId);
            if (booking == null)
            {
                return NotFound("Booking not found.");
            }

            if (booking.Paid)
            {
                return BadRequest("Booking already paid.");
            }

            var (startLat, startLon) = await _locSvc.GetCoordinatesAsync(booking.StartLocation);
            var (endLat, endLon) = await _locSvc.GetCoordinatesAsync(booking.EndLocation);

            // fare lookup
            var baseFare = await _fareSvc.GetBaseFareAsync(startLat, startLon, endLat, endLon);

            // multipliers
            if (!CabMult.TryGetValue(booking.CabType, out var cabM))
                return BadRequest($"Unknown cab type '{booking.CabType}'.");
            var hour = booking.DateTime.ToLocalTime().Hour;
            var dayM = (hour >= 0 && hour < 8) ? 1.2 : 1.0;
            double paxM = booking.Passengers <= 4 ? 1 :
                          booking.Passengers <= 8 ? 2 :
                          throw new InvalidOperationException("Too many passengers");

            // total
            var total = baseFare * cabM * dayM * paxM * 1 /* discount */;

            // record payment
            var payment = new Models.Payment
            {
                Id = Guid.NewGuid().ToString(),
                BookingId = booking.Id,
                UserUid = uid,
                CabFare = baseFare,
                CabMultiplier = cabM,
                DaytimeMultiplier = dayM,
                PassengersMultiplier = paxM,
                DiscountMultiplier = 1,
                TotalPrice = total,
                CreatedAt = Timestamp.GetCurrentTimestamp()
            };
            await _db.Collection("payments").Document(payment.Id).SetAsync(payment);

            // mark booking paid
            var marked = await _bookSvc.MarkPaidAsync(uid, booking.Id);
            if (!marked)
                return StatusCode(500, "Failed to mark booking paid.");

            // return DTO
            return Ok(new PaymentDTO
            {
                Id = payment.Id,
                BookingId = payment.BookingId,
                TotalPrice = payment.TotalPrice,
                CreatedAt = payment.CreatedAt.ToDateTime()
            });
        }

        [HttpGet("{userUid}")]
        public async Task<IActionResult> GetPayments(string userUid)
        {
            var me = GetUid();
            if (me == null || me != userUid)
                return Forbid();

            var snaps = await _db
                .Collection("payments")
                .WhereEqualTo("UserUid", userUid)
                .OrderByDescending("CreatedAt")
                .GetSnapshotAsync();

            var list = snaps.Documents
                .Select(d => d.ConvertTo<Models.Payment>())
                .Select(p => new PaymentDTO
                {
                    Id = p.Id,
                    BookingId = p.BookingId,
                    TotalPrice = p.TotalPrice,
                    CreatedAt = p.CreatedAt.ToDateTime()
                })
                .ToList();

            return Ok(list);
        }
    }
}
