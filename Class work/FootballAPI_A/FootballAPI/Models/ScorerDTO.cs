using Newtonsoft.Json;

namespace FootballAPI.Models
{
    public class ScorerDTO
    {
        [JsonProperty("player")]
        public Player Scorer { get; set; } = new Player();

        [JsonProperty("goals")]
        public int Goals { get; set; } = 0;

        [JsonProperty("playedMatches")]
        public int PlayedMatches { get; set; } = 0;
    }
}
