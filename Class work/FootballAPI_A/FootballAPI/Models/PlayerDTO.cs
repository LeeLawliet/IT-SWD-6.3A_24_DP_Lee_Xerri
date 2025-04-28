using Newtonsoft.Json;

namespace FootballAPI.Models
{
    public class PlayerDTO
    {
        [JsonProperty("firstName")]
        public string FirstName { get; set; } = string.Empty;

        [JsonProperty("lastName")]
        public string LastName { get; set; } = string.Empty;

        [JsonProperty("lastUpdated")]
        public string LastUpdated { get; set; } = string.Empty;

        [JsonProperty("nationality")]
        public string Nationality { get; set; } = string.Empty;

        [JsonProperty("position")]
        public string Position { get; set; } = string.Empty;
    }
}
