using FirebaseAdmin.Auth;
using Google.Cloud.Firestore;
using LocationService.DTO;
using LocationService.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace LocationService.Services
{
    public interface ILocationService
    {
        Task<LocationDTO[]> GetAllAsync(string userUid);
        Task<LocationDTO> GetByIdAsync(string userUid, string id);
        Task<LocationDTO> CreateAsync(string userUid, CreateLocationDTO dto);
        Task UpdateAsync(string userUid, string id, CreateLocationDTO dto);
        Task DeleteAsync(string userUid, string id);
        Task GetWeatherAsync(string address);
    }

    public class LocationService : ILocationService
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly FirestoreDb _db;
        private readonly FirebaseAuth _auth;
        private readonly HttpClient _http;

        public LocationService(FirestoreDb db, FirebaseAuth auth, HttpClient http)
        {
            _db = db;
            _auth = auth;
            _http = http;
        }

        public async Task<LocationDTO[]> GetAllAsync(string userUid)
        {
            var snaps = await _db.Collection("locations")
                                 .WhereEqualTo("UserUid", userUid)
                                 .OrderByDescending("CreatedAt")
                                 .GetSnapshotAsync();
            return snaps.Documents
                        .Select(d => d.ConvertTo<Location>())
                        .Select(l => new LocationDTO { Id = l.Id, Name = l.Name })
                        .ToArray();
        }

        public async Task<LocationDTO> GetByIdAsync(string userUid, string id)
        {
            var doc = await _db.Collection("locations").Document(id).GetSnapshotAsync();
            if (!doc.Exists) throw new KeyNotFoundException();
            var loc = doc.ConvertTo<Location>();
            if (loc.UserUid != userUid) throw new UnauthorizedAccessException();
            return new LocationDTO { Id = loc.Id, Name = loc.Name };
        }

        public async Task<LocationDTO> CreateAsync(string userUid, CreateLocationDTO dto)
        {
            var loc = new Location
            {
                Id = Guid.NewGuid().ToString(),
                UserUid = userUid,
                Name = dto.Name,
                CreatedAt = Timestamp.GetCurrentTimestamp()
            };
            await _db.Collection("locations").Document(loc.Id).SetAsync(loc);
            return new LocationDTO { Id = loc.Id, Name = loc.Name};
        }

        public async Task UpdateAsync(string userUid, string id, CreateLocationDTO dto)
        {
            var doc = _db.Collection("locations").Document(id);
            var snap = await doc.GetSnapshotAsync();
            if (!snap.Exists) throw new KeyNotFoundException();
            var loc = snap.ConvertTo<Location>();
            if (loc.UserUid != userUid) throw new UnauthorizedAccessException();
            await doc.UpdateAsync(new Dictionary<string, object>
            {
                ["Name"] = dto.Name
            });
        }

        public async Task DeleteAsync(string userUid, string id)
        {
            var doc = _db.Collection("locations").Document(id);
            var snap = await doc.GetSnapshotAsync();
            if (!snap.Exists) throw new KeyNotFoundException();
            var loc = snap.ConvertTo<Location>();
            if (loc.UserUid != userUid) throw new UnauthorizedAccessException();
            await doc.DeleteAsync();
        }

        public async Task GetWeatherAsync(string address)
        {
            var client = _httpFactory.CreateClient("WeatherAPI");
            var resp = await client.GetAsync($"forecast.json?q={Uri.EscapeDataString(address)}");
            resp.EnsureSuccessStatusCode();

            var root = await resp.Content.ReadFromJsonAsync<JsonElement>();
            var forecast = root.GetProperty("forecast")
                               .GetProperty("forecastday")[0];
            var day = forecast.GetProperty("day");

            var condition = day.GetProperty("condition");

            // TODO: Work on retrieving weather and displaying as JSON
        }
    }
}
