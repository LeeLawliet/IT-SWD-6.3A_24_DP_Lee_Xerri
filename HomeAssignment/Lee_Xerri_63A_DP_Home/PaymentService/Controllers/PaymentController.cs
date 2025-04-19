﻿using FirebaseAdmin.Auth;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentService.DTO;
using PaymentService.Fares;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Linq;
using BookingService.DTO;
using System.Text.Json;

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

        [HttpPost]
        public async Task<IActionResult> Pay([FromBody] CreatePaymentDTO dto)
        {
            // 0) ensure we’re authenticated
            var uid = GetUid();
            if (uid == null)
                return Unauthorized();

            // 1) grab the raw Authorization header
            var bearer = Request.Headers
                                .FirstOrDefault(h => h.Key == "Authorization")
                                .Value
                                .FirstOrDefault();
            if (string.IsNullOrEmpty(bearer) || !bearer.StartsWith("Bearer "))
                return Unauthorized("Missing or malformed Authorization header.");

            // 2) Fetch booking from BookingService
            var bookingClient = _httpFactory.CreateClient("BookingAPI");
            bookingClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", bearer.Substring(7));

            var resp = await bookingClient.GetAsync($"api/Booking/{dto.BookingId}");
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                return NotFound("Booking not found.");
            resp.EnsureSuccessStatusCode();

            var booking = await resp.Content
                                    .ReadFromJsonAsync<BookingDTO>()
                          ?? throw new InvalidOperationException("Invalid booking data.");
            
            
            if (booking.Paid)
            {
                return BadRequest("Booking already paid.");
            }

            // create connection to WeatherAPI client
            var weatherClient = _httpFactory.CreateClient("WeatherAPI");

            // retrieve Start Point Location Latitude and Longitude
            var startJson = await weatherClient.GetFromJsonAsync<JsonElement>($"forecast.json?q={Uri.EscapeDataString(booking.StartLocation)}&days=1");
            var startLoc = startJson.GetProperty("location");
            var startLat = startLoc.GetProperty("lat").GetDouble();
            var startLon = startLoc.GetProperty("lon").GetDouble();

            // retrieve End Point Location Latitude and Longitude
            var endJson = await weatherClient.GetFromJsonAsync<JsonElement>($"forecast.json?q={Uri.EscapeDataString(booking.EndLocation)}&days=1");
            var endLoc = endJson.GetProperty("location");
            var endLat = endLoc.GetProperty("lat").GetDouble();
            var endLon = endLoc.GetProperty("lon").GetDouble();

            // get the base fare using coordinates
            var baseFare = await _fareSvc.GetBaseFareAsync(
                startLat, startLon,
                endLat, endLon);

            // 4) Lookup multipliers
            if (!CabMult.TryGetValue(booking.CabType, out var cabM))
                return BadRequest($"Unknown cab type '{booking.CabType}'.");

            var hour = booking.DateTime.ToLocalTime().Hour;
            var dayM = (hour >= 0 && hour < 8) ? 1.2 : 1.0;

            double paxM = booking.Passengers <= 4 ? 1 :
                          booking.Passengers <= 8 ? 2 :
                          throw new InvalidOperationException("Too many passengers");

            // 5) Compute total
            var total = baseFare * cabM * dayM * paxM * 1;

            // 6) Record payment in Firestore
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
            await _db.Collection("payments")
                     .Document(payment.Id)
                     .SetAsync(payment);

            booking.Paid = true;

            var markResp = await bookingClient
    .PostAsync($"api/booking/{booking.Id}/mark-paid", null);
            markResp.EnsureSuccessStatusCode();

            // 7) Return slim DTO
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
