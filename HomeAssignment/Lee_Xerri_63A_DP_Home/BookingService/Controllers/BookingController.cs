using BookingService.DTO;
using BookingService.Models;
using BookingService.Services;
using FirebaseAdmin.Auth;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace BookingService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BookingController : ControllerBase
    {
        private readonly IBookingService _svc;
        private readonly FirebaseAuth _auth;

        public BookingController(IBookingService svc, FirebaseAuth auth)
        {
            _svc = svc;
            _auth = auth;
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

            // Cab type validation
            if (!Enum.TryParse<CabType>(dto.CabType, ignoreCase: true, out var cab)
                || !CabTypeRules.MaxPassengersByCabType.ContainsKey(cab))
            {
                var valid = string.Join(", ", Enum.GetNames<CabType>());
                return BadRequest(new
                {
                    error = $"Invalid cab type '{dto.CabType}'. Valid values: {valid}."
                });
            }

            // Passenger count validation
            var max = CabTypeRules.MaxPassengersByCabType[cab];
            if (dto.Passengers < 1 || dto.Passengers > max)
                return BadRequest(new
                {
                    error = $"{cab} supports 1–{max} passengers."
                });

            // Delegate to service
            var bookingId = await _svc.CreateAsync(uid, dto);
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

        [HttpPost("{id}/mark-paid")]
        public async Task<IActionResult> MarkPaid(
            string id,
            [FromHeader(Name = "Authorization")] string authHeader)
        {
            var uid = await ValidateTokenAsync(authHeader);
            if (uid == null)
                return Unauthorized();

            var success = await _svc.MarkPaidAsync(uid, id);
            if (!success)
                return BadRequest("Booking not found or already paid.");

            return Ok();
        }
    }
}