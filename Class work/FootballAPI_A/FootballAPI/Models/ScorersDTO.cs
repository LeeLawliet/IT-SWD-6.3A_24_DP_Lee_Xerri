using Newtonsoft.Json;

namespace FootballAPI.Models
{
    public class ScorersDTO
    {
        [JsonProperty("scorers")]
        public List<ScorerDTO>? Scorers { get; set; } = new List<ScorerDTO>();
    }
}
