using FirebaseAdmin.Auth;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentService.DTO;
using PaymentService.Fares;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Linq;
using System.Text.Json;
using System.Net;

namespace PaymentService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PaymentController : ControllerBase
    {
        private readonly FirestoreDb _db;
        private readonly FirebaseAuth _auth;
        private readonly IFareService _fareSvc;
        private readonly IHttpClientFactory _httpFactory;
        

        private static readonly IReadOnlyDictionary<string, double> CabMult = new Dictionary<string, double>
        {
            ["Economic"] = 1.0,
            ["Premium"] = 1.2,
            ["Executive"] = 1.4
        };

        public PaymentController(
            FirestoreDb db,
            FirebaseAuth auth,
            IFareService fareSvc,
            IHttpClientFactory httpFactory)
        {
            
            _db = db;
            _auth = auth;
            _fareSvc = fareSvc;
            _httpFactory = httpFactory;
        }

        private string? GetUid() =>
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        [HttpPost("pay")]
        public async Task<IActionResult> Pay([FromBody] CreatePaymentDTO dto)
        {
            var uid = GetUid();
            if (uid == null)
            {
                return Unauthorized();
            }

            var bookClient = _httpFactory.CreateClient("BookingAPI");
            var userClient = _httpFactory.CreateClient("CustomerAPI");

            var bearer = Request.Headers["Authorization"].FirstOrDefault()?.Substring("Bearer ".Length);
            if (string.IsNullOrEmpty(bearer))
            {
                return Unauthorized("Missing token");
            }

            bookClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            userClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);

            // fetch booking via booking service
            var bookingResp = await bookClient.GetAsync($"/api/Booking/{dto.BookingId}");
            if (bookingResp.StatusCode == HttpStatusCode.NotFound)
            {
                return NotFound("Booking not found.");
            }

            bookingResp.EnsureSuccessStatusCode();
            var booking = await bookingResp.Content.ReadFromJsonAsync<BookingHttpDTO>()
                          ?? throw new InvalidOperationException("Invalid booking data.");

            if (booking.Paid)
            {
                return BadRequest("Booking already paid.");
            }

            var (startLat, startLon) = await _fareSvc.GetCoordinatesAsync(booking.StartLocation);
            var (endLat, endLon) = await _fareSvc.GetCoordinatesAsync(booking.EndLocation);

            // fare lookup
            //var baseFare = await _fareSvc.GetBaseFareAsync(startLat, startLon, endLat, endLon);

            // multipliers
            if (!CabMult.TryGetValue(booking.CabType, out var cabM))
                return BadRequest($"Unknown cab type '{booking.CabType}'.");

            var hour = booking.DateTime.ToLocalTime().Hour;
            var dayM = (hour >= 0 && hour < 8) ? 1.2 : 1.0;

            double paxM = booking.Passengers <= 4 ? 1 :
                          booking.Passengers <= 8 ? 2 :
                          throw new InvalidOperationException("Too many passengers");

            var notesResp = await userClient.GetAsync($"/api/User/{uid}/notifications");
            notesResp.EnsureSuccessStatusCode();

            var inbox = await notesResp.Content.ReadFromJsonAsync<List<NotificationDTO>>();
            var discountNote = inbox?.FirstOrDefault(n => n.Id == "discount");

            double discount = 1.0;
            if (discountNote != null)
            {
                discount = 0.3; // apply 70% discount
                var deleteResp = await userClient.DeleteAsync($"/api/User/{uid}/notifications/discount");
                deleteResp.EnsureSuccessStatusCode();
            }

            var baseFare = 20.0; // TODO: replace with actual fare lookup
            var total = baseFare * cabM * dayM * paxM * discount;

            var payment = new Models.Payment
            {
                Id = Guid.NewGuid().ToString(),
                BookingId = booking.Id,
                UserUid = uid,
                CabFare = baseFare,
                CabMultiplier = cabM,
                DaytimeMultiplier = dayM,
                PassengersMultiplier = paxM,
                DiscountMultiplier = discount,
                TotalPrice = total,
                CreatedAt = Timestamp.GetCurrentTimestamp()
            };
            await _db.Collection("payments").Document(payment.Id).SetAsync(payment);

            var req = new HttpRequestMessage(HttpMethod.Put, $"/api/Booking/{booking.Id}/mark-paid");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);

            var markPaidResp = await bookClient.SendAsync(req);
            if (!markPaidResp.IsSuccessStatusCode)
            {
                var body = await markPaidResp.Content.ReadAsStringAsync();
                return StatusCode(
                    (int)markPaidResp.StatusCode,
                    $"BookingService failed ({markPaidResp.StatusCode}): {body}"
                );
            }

            return Ok(new PaymentDTO
            {
                Id = payment.Id,
                BookingId = payment.BookingId,
                TotalPrice = Math.Round(payment.TotalPrice, 2),
                CreatedAt = payment.CreatedAt.ToDateTime()
            });
        }

        [HttpGet("{userUid}")]
        public async Task<IActionResult> GetPayments(string userUid)
        {
            var me = GetUid();
            if (me == null || me != userUid)
            {
                return Forbid();
            }

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