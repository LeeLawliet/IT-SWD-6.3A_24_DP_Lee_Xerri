using LocationService.DTO;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace LocationService.Services
{
    public interface ILocationService
    {
        Task<(double Lat, double Lon)> GetCoordinatesAsync(string address);
        Task<WeatherForecastDTO> GetWeatherAsync(double lat, double lon);
    }

    public class LocationService : ILocationService
    {
        private readonly HttpClient _http;

        public LocationService(HttpClient http)
        {
            _http = http;
        }

        public async Task<(double Lat, double Lon)> GetCoordinatesAsync(string address)
        {
            var url = $"/v1/search.json?q={Uri.EscapeDataString(address)}";
            var resp = await _http.GetAsync(url);
            resp.EnsureSuccessStatusCode();

            var arr = await resp.Content.ReadFromJsonAsync<JsonElement>();
            var first = arr[0];
            return (first.GetProperty("lat").GetDouble(),
                    first.GetProperty("lon").GetDouble());
        }

        public async Task<WeatherForecastDTO> GetWeatherAsync(double lat, double lon)
        {
            var url = $"/v1/forecast.json?lat={lat}&lon={lon}&days=1";
            var resp = await _http.GetAsync(url);
            resp.EnsureSuccessStatusCode();

            return await resp.Content.ReadFromJsonAsync<WeatherForecastDTO>()
                   ?? throw new InvalidOperationException("Invalid weather data.");
        }
    }
}
