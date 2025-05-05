using BookingService.DTO;
using BookingService.Models;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BookingService.Services
{
    public interface IBookingService
    {
        Task<string> CreateAsync(string userUid, CreateBookingDTO dto);
        Task<IEnumerable<BookingDTO>> GetCurrentAsync(string userUid);
        Task<IEnumerable<BookingDTO>> GetPastAsync(string userUid);
        Task<BookingDTO?> GetByIdAsync(string userUid, string id);
    }

    public class BookingService : IBookingService
    {
        private readonly FirestoreDb _db;

        public BookingService(FirestoreDb db)
        {
            _db = db;
        }

        public async Task<string> CreateAsync(string userUid, CreateBookingDTO dto)
        {
            var booking = new Booking
            {
                Id = Guid.NewGuid().ToString(),
                UserUid = userUid,
                StartLocation = dto.StartLocation,
                EndLocation = dto.EndLocation,
                DateTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(10)), // add minutes to simulate a present booking
                Passengers = dto.Passengers,
                CabType = dto.CabType,
                Paid = false
            };
            await _db.Collection("bookings").Document(booking.Id).SetAsync(booking);
            return booking.Id;
        }

        public async Task<IEnumerable<BookingDTO>> GetCurrentAsync(string userUid)
        {
            var now = Timestamp.FromDateTime(DateTime.UtcNow);
            var snaps = await _db
                .Collection("bookings")
                .WhereEqualTo("UserUid", userUid)
                .WhereGreaterThanOrEqualTo("DateTime", now)
                .OrderBy("DateTime")
                .GetSnapshotAsync();

            return snaps.Documents
                .Select(d => d.ConvertTo<Booking>())
                .Select(b => new BookingDTO
                {
                    Id = b.Id,
                    StartLocation = b.StartLocation,
                    EndLocation = b.EndLocation,
                    DateTime = b.DateTime.ToDateTime(),
                    Passengers = b.Passengers,
                    CabType = b.CabType,
                    Paid = b.Paid
                })
                .ToList();
        }

        public async Task<IEnumerable<BookingDTO>> GetPastAsync(string userUid)
        {
            var now = Timestamp.FromDateTime(DateTime.UtcNow);
            var snaps = await _db
                .Collection("bookings")
                .WhereEqualTo("UserUid", userUid)
                .WhereLessThan("DateTime", now)
                .OrderByDescending("DateTime")
                .GetSnapshotAsync();

            return snaps.Documents
                .Select(d => d.ConvertTo<Booking>())
                .Select(b => new BookingDTO
                {
                    Id = b.Id,
                    StartLocation = b.StartLocation,
                    EndLocation = b.EndLocation,
                    DateTime = b.DateTime.ToDateTime(),
                    Passengers = b.Passengers,
                    CabType = b.CabType,
                    Paid = b.Paid
                })
                .ToList();
        }

        public async Task<BookingDTO?> GetByIdAsync(string userUid, string id)
        {
            var doc = await _db.Collection("bookings").Document(id).GetSnapshotAsync();
            if (!doc.Exists) return null;

            var b = doc.ConvertTo<Booking>();
            if (b.UserUid != userUid) return null;

            return new BookingDTO
            {
                Id = b.Id,
                StartLocation = b.StartLocation,
                EndLocation = b.EndLocation,
                DateTime = b.DateTime.ToDateTime(),
                Passengers = b.Passengers,
                CabType = b.CabType,
                Paid = b.Paid
            };
        }

        [Authorize]
        [HttpPut("{id}/mark-paid")]
        public async Task<IActionResult> MarkBookingAsPaid(string id)
        {
            var snap = await _db.Collection("bookings").Document(id).GetSnapshotAsync();

            if (!snap.Exists)
                return NotFound("Booking not found.");

            await snap.Reference.UpdateAsync("Paid", true);
            return NoContent();
        }

    }
}