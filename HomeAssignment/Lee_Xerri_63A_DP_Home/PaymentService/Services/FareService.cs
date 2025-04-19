using Google.Cloud.Firestore;
using System.Text.Json;

namespace PaymentService.Fares
{
    public interface IFareService
    {
        Task<double> GetBaseFareAsync(double depLat, double depLng, double arrLat, double arrLng);
    }

    public class FareService : IFareService
    {
        private readonly HttpClient _http;
        public FareService(HttpClient http)
        {
            _http = http;
        }

        public async Task<double> GetBaseFareAsync(
            double depLat, double depLng,
            double arrLat, double arrLng)
        {
            // Build the query exactly as the Taxi Fare Calculator expects:
            var qs = $"?dep_lat={depLat}"
                   + $"&dep_lng={depLng}"
                   + $"&arr_lat={arrLat}"
                   + $"&arr_lng={arrLng}";

            // Call the external API (HttpClient already has Host & Key headers)
            var resp = await _http.GetAsync($"search-geo{qs}");
            resp.EnsureSuccessStatusCode();

            // Parse the JSON, pull out the first fare's price_in_cents
            var root = await resp.Content.ReadFromJsonAsync<JsonElement>();
            var journey = root.GetProperty("journey");
            var fares = journey.GetProperty("fares").EnumerateArray();

            // always pick the entry whose name == "by Day"
            var dayFare = fares
                .First(f => f.GetProperty("name").GetString() == "by Day");

            var cents = dayFare.GetProperty("price_in_cents").GetInt32();

            // Convert to major units (e.g. dollars/euros)
            return cents / 100.0;
        }
    }

}
