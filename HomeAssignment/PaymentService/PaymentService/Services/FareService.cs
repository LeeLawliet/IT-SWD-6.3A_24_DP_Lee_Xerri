using Google.Cloud.Firestore;
using System.Text.Json;

namespace PaymentService.Fares
{
    public interface IFareService
    {
        Task<(double Lat, double Lon)> GetCoordinatesAsync(string name);
        Task<double> GetBaseFareAsync(double depLat, double depLng, double arrLat, double arrLng);
    }

    public class FareService : IFareService
    {
        private readonly HttpClient _http;
        private readonly HttpClient _weather;
        private readonly IHttpClientFactory _httpFactory;

        public FareService(HttpClient http, IHttpClientFactory httpFactory)
        {
            _http = http;
            _httpFactory = httpFactory;
            _weather = httpFactory.CreateClient("WeatherAPI");
        }

        public async Task<(double Lat, double Lon)> GetCoordinatesAsync(string name)
        {
            var root = await _weather.GetFromJsonAsync<JsonElement>($"forecast.json?q={Uri.EscapeDataString(name)}");
            var loc = root.GetProperty("location");
            return (loc.GetProperty("lat").GetDouble(), loc.GetProperty("lon").GetDouble());
        }

        public async Task<double> GetBaseFareAsync(
            double depLat, double depLng,
            double arrLat, double arrLng)
        {
            // reduce errors from Taxi API end
            depLat = Math.Round(depLat, 1);
            depLng = Math.Round(depLng, 1);
            arrLat = Math.Round(arrLat, 1);
            arrLng = Math.Round(arrLng, 1);

            // build the query as the Taxi Fare Calculator expects:
            var qs = $"?dep_lat={depLat}"
                    + $"&dep_lng={depLng}"
                    + $"&arr_lat={arrLat}"
                    + $"&arr_lng={arrLng}";

            // call the external API (HttpClient already has Host & Key headers)
            var resp = await _http.GetAsync($"search-geo{qs}");
            resp.EnsureSuccessStatusCode();

            // retrieve the fare's price_in_cents
            var root = await resp.Content.ReadFromJsonAsync<JsonElement>();
            var journey = root.GetProperty("journey");
            var fares = journey.GetProperty("fares").EnumerateArray();

            // always pick the entry whose name == "by Day"
            var dayFare = fares
                .First(f => f.GetProperty("name").GetString() == "by Day");

            var cents = dayFare.GetProperty("price_in_cents").GetInt32();

            // convert to major unit
            return cents / 100.0;
        }
    }

}
