using FirebaseAdmin.Auth;
using Google.Cloud.Firestore;
using LocationService.DTO;
using LocationService.Models;
using Microsoft.AspNetCore.Http.HttpResults;
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
        Task<JsonElement> GetWeatherAsync(string name);
        JsonElement GetJsonElement(JsonElement root, string jsonPath);
        Task<(double Lat, double Lon)> GetCoordinatesAsync(string name);
    }

    public class LocationService : ILocationService
    {
        private readonly FirestoreDb _db;
        private readonly FirebaseAuth _auth;
        private readonly HttpClient _weather;

        public LocationService(FirestoreDb db, FirebaseAuth auth, HttpClient http)
        {
            _db = db;
            _auth = auth;
            _weather = http;
        }

        public async Task<LocationDTO[]> GetAllAsync(string userUid)
        {
            var snaps = await _db.Collection("favorites")
                                 .WhereEqualTo("UserUid", userUid)
                                 .OrderByDescending("CreatedAt")
                                 .GetSnapshotAsync();
            return snaps.Documents
                        .Select(d => d.ConvertTo<Location>())
                        .Select(l => new LocationDTO { Id = l.Id, Name = l.Name, Latitude = l.Latitude, Longitude = l.Longitude})
                        .ToArray();
        }

        public async Task<LocationDTO> GetByIdAsync(string userUid, string id)
        {
            var doc = await _db.Collection("favorites").Document(id).GetSnapshotAsync();
            if (!doc.Exists)
            {
                throw new KeyNotFoundException();
            }
            var loc = doc.ConvertTo<Location>();
            if (loc.UserUid != userUid)
            {
                throw new UnauthorizedAccessException();
            }

            return new LocationDTO {Id = loc.Id, Name = loc.Name, Latitude = loc.Latitude, Longitude = loc.Longitude };
        }

        public async Task<LocationDTO> CreateAsync(string userUid, CreateLocationDTO dto)
        {
            // dont allow duplicate favorites
            var existing = await _db.Collection("favorites")
                .WhereEqualTo("UserUid", userUid)
                .WhereEqualTo("Name", dto.Name)
                .Limit(1)
                .GetSnapshotAsync();

            if (existing.Count > 0)
            {
                throw new Exception("You already saved this location.");
            }

            var (lat, lon) = await GetCoordinatesAsync(dto.Name);

            var loc = new Location
            {
                Id = Guid.NewGuid().ToString(),
                UserUid = userUid,
                Name = dto.Name,
                Latitude = lat,
                Longitude = lon,
                CreatedAt = Timestamp.GetCurrentTimestamp()
            };
            await _db.Collection("favorites").Document(loc.Id).SetAsync(loc);
            return new LocationDTO { Id = loc.Id, Name = loc.Name, Latitude = loc.Latitude, Longitude = loc.Longitude};
        }

        public async Task UpdateAsync(string userUid, string id, CreateLocationDTO dto)
        {
            var doc = _db.Collection("favorites").Document(id);
            var snap = await doc.GetSnapshotAsync();

            if (!snap.Exists)
            {
                throw new KeyNotFoundException();
            }

            var loc = snap.ConvertTo<Location>();
            if (loc.UserUid != userUid)
            {
                throw new UnauthorizedAccessException();
            }

            var (newLat, newLon) = await GetCoordinatesAsync(dto.Name);

            await doc.UpdateAsync(new Dictionary<string, object>
            {
                ["Name"] = dto.Name,
                ["Latitude"] = newLat,
                ["Longitude"] = newLon
            });
        }

        public async Task DeleteAsync(string userUid, string id)
        {
            var doc = _db.Collection("favorites").Document(id);
            var snap = await doc.GetSnapshotAsync();
            if (!snap.Exists)
            {
                throw new KeyNotFoundException();
            }
            var loc = snap.ConvertTo<Location>();
            if (loc.UserUid != userUid) throw new UnauthorizedAccessException();
            await doc.DeleteAsync();
        }

        public async Task<JsonElement> GetWeatherAsync(string name)
        {
            var resp = await _weather.GetAsync($"forecast.json?q={Uri.EscapeDataString(name)}");
            resp.EnsureSuccessStatusCode();

            var root = await resp.Content.ReadFromJsonAsync<JsonElement>();
            var forecast = root.GetProperty("forecast")
                               .GetProperty("forecastday")[0];

            var weatherDesc = GetJsonElement(forecast, "day/condition/text");
            var avgTemp_C = GetJsonElement(forecast, "day/avgtemp_c");
            var avgHumidity = GetJsonElement(forecast, "day/avghumidity");

            var jsonResult = new
            {
                weatherDesc = weatherDesc.GetString(),
                avgTemp_C = avgTemp_C.GetDouble(),
                avgHumidity = avgHumidity.GetDouble()
            };

            var jsonString = JsonSerializer.Serialize(jsonResult);
            return JsonSerializer.Deserialize<JsonElement>(jsonString);
        }

        public JsonElement GetJsonElement(JsonElement root, string jsonPath)
        {
            string[] path = jsonPath.Split('/');
            var text = root;
            
            foreach (var p in path)
            {
                text = text.GetProperty(p);
            }
            
            return text;
        }

        public async Task<(double Lat, double Lon)> GetCoordinatesAsync(string name)
        {
            var root = await _weather.GetFromJsonAsync<JsonElement>(
                $"forecast.json?q={Uri.EscapeDataString(name)}");
            var loc = root.GetProperty("location");
            return (loc.GetProperty("lat").GetDouble(),
                    loc.GetProperty("lon").GetDouble());
        }
    }
}
