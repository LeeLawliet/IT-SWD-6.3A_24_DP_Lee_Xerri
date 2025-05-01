using BookingService.DTO;
using BookingService.Models;
using BookingService.Services;
using FirebaseAdmin.Auth;
using Google.Cloud.Firestore;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;

namespace BookingService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BookingController : ControllerBase
    {
        private readonly IBookingService _svc;
        private readonly FirebaseAuth _auth;
        private readonly FirestoreDb _db;
        private readonly IHttpClientFactory _httpFactory;
        private readonly ILogger<BookingController> _logger;
        private readonly PublisherServiceApiClient _publisher;
        private readonly TopicName _discountTopic;
        private readonly TopicName _bookingTopic;

        public BookingController(IBookingService svc,
            FirebaseAuth auth,
            IHttpClientFactory httpFactory,
            FirestoreDb db,
            ILogger<BookingController> logger,
            PublisherServiceApiClient publisher,
            PubSubTopics topics)
        {
            _svc = svc;
            _auth = auth;
            _httpFactory = httpFactory;
            _db = db;
            _logger = logger;
            _publisher = publisher;
            _discountTopic = topics.DiscountTopic;
            _bookingTopic = topics.BookingTopic;
        }

        private static class CabTypeRules
        {
            public static readonly IReadOnlyDictionary<CabType, int> MaxPassengersByCabType =
                new Dictionary<CabType, int>
                {
                    { CabType.Economic,  8 },
                    { CabType.Premium,   8 },
                    { CabType.Executive, 8 }
                };
        }

        private async Task<string?> ValidateTokenAsync(string bearer)
        {
            if (string.IsNullOrEmpty(bearer) || !bearer.StartsWith("Bearer "))
                return null;

            var token = bearer.Substring("Bearer ".Length);
            var decoded = await _auth.VerifyIdTokenAsync(token);
            return decoded.Uid;
        }

        [HttpPost]
        public async Task<IActionResult> Create(
            [FromHeader(Name = "Authorization")] string authHeader,
            [FromBody] CreateBookingDTO dto)
        {
            var uid = await ValidateTokenAsync(authHeader);
            if (uid == null)
                return Unauthorized();

            // cab type validation
            if (!Enum.TryParse<CabType>(dto.CabType, ignoreCase: true, out var cab)
                || !CabTypeRules.MaxPassengersByCabType.ContainsKey(cab))
            {
                var valid = string.Join(", ", Enum.GetNames<CabType>());
                return BadRequest(new
                {
                    error = $"Invalid cab type '{dto.CabType}'. Valid values: {valid}."
                });
            }

            // passenger count validation
            var max = CabTypeRules.MaxPassengersByCabType[cab];
            if (dto.Passengers < 1 || dto.Passengers > max)
                return BadRequest(new
                {
                    error = $"{cab} supports 1–{max} passengers."
                });

            // delegate to service
            var bookingId = await _svc.CreateAsync(uid, dto);

            // DISCOUNT CHECK
            // retrieve user total bookings
            var snap = await _db
                .Collection("bookings")
                .WhereEqualTo("UserUid", uid)
                .GetSnapshotAsync();

            if (snap.Count == 3) // third booking just created
            {
                var payload = new
                {
                    Uid = uid,
                    BookingId = bookingId,
                    Message = "User has made their 3rd booking."
                };

                string json = JsonSerializer.Serialize(payload);
                var pubsubMessage = new PubsubMessage
                {
                    Data = ByteString.CopyFromUtf8(json),
                    Attributes = { { "discount", "discount" } }
                };

                await _publisher.PublishAsync(_discountTopic, new[] { pubsubMessage });
                _logger.LogInformation("Published third-booking event for UID: {uid}", uid);
            }

            // CAB ARRIVAL
            // run arrival notification task asynchronously
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(3));

                    var message = new
                    {
                        Uid = uid,
                        BookingId = bookingId,
                        Message = $"Your cab is ready for pickup.\nBooking ID: {bookingId}\nFrom: {dto.StartLocation}\nTo: {dto.EndLocation}"
                    };

                    var json = JsonSerializer.Serialize(message);
                    var pubsubMessage = new PubsubMessage
                    {
                        Data = ByteString.CopyFromUtf8(json),
                        Attributes = { { "event", "cab_ready" } }
                    };

                    await _publisher.PublishAsync(_bookingTopic, new[] { pubsubMessage });
                    _logger.LogInformation($"Published cab ready message to booking-topic for UID: {uid}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to publish cab ready message.");
                }
            });

            return Ok(new { id = bookingId });
        }

        [HttpGet("current")]
        public async Task<IActionResult> GetCurrent(
            [FromHeader(Name = "Authorization")] string authHeader)
        {
            var uid = await ValidateTokenAsync(authHeader);
            if (uid == null)
                return Unauthorized();

            var list = await _svc.GetCurrentAsync(uid);
            return Ok(list);
        }

        [HttpGet("past")]
        public async Task<IActionResult> GetPast(
            [FromHeader(Name = "Authorization")] string authHeader)
        {
            var uid = await ValidateTokenAsync(authHeader);
            if (uid == null)
                return Unauthorized();

            var list = await _svc.GetPastAsync(uid);
            return Ok(list);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(
            string id,
            [FromHeader(Name = "Authorization")] string authHeader)
        {
            var uid = await ValidateTokenAsync(authHeader);
            if (uid == null)
                return Unauthorized();

            var booking = await _svc.GetByIdAsync(uid, id);
            if (booking == null)
                return NotFound();

            return Ok(booking);
        }
    }
}