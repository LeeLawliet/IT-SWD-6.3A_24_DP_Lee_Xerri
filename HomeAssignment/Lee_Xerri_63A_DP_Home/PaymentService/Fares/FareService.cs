using Google.Cloud.Firestore;
using System.Text.Json;

namespace PaymentService.Fares
{
    public interface IFareService
    {
        Task<double> GetBaseFareAsync(string bookingId);
    }

    public class FareService : IFareService
    {
        private readonly HttpClient _http;
        public FareService(HttpClient http)
        {
            _http = http;
        }

        public async Task<double> GetBaseFareAsync(string bookingId)
        {
            // Retrieves taxi fare from external API
            var resp = await _http.GetAsync($"/?bookingId={bookingId}");
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
            return json.GetProperty("fare").GetDouble();
        }
    }

}
