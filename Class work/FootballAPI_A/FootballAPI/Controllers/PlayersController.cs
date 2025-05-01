using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FootballAPI.Models;
using Newtonsoft.Json;

namespace FootballAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PlayersController : ControllerBase
    {
        private readonly PlayerDbContext _context;

        private static readonly HttpClient _httpClient = new HttpClient();

        public PlayersController(PlayerDbContext context)
        {
            _context = context;
        }

        // GET: api/Players
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Player>>> GetPlayers()
        {
            return await _context.Players.ToListAsync();
        }

        // GET: api/Players/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Player>> GetPlayer(long id)
        {
            var player = await _context.Players.FindAsync(id);

            if (player == null)
            {
                //retrieve the player from the external API
                player = await RetrievePlayerFromAPI(id);

                if(player == null)
                {
                    return NotFound();
                }

                //save data in the DB to cache it locally
                _context.Players.Add(player);
                await _context.SaveChangesAsync();

                return player;
            }

            return player;
        }

        // PUT: api/Players/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutPlayer(long id, Player player)
        {
            if (id != player.Id)
            {
                return BadRequest();
            }

            _context.Entry(player).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PlayerExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/Players
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Player>> PostPlayer(Player player)
        {
            _context.Players.Add(player);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetPlayer", new { id = player.Id }, player);
        }

        // DELETE: api/Players/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePlayer(long id)
        {
            var player = await _context.Players.FindAsync(id);
            if (player == null)
            {
                return NotFound();
            }

            _context.Players.Remove(player);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool PlayerExists(long id)
        {
            return _context.Players.Any(e => e.Id == id);
        }

        private async Task<Player?> RetrievePlayerFromAPI(long id)
        {
            try
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Get, $"http://api.football-data.org/v4/persons/{id}");

                request.Headers.Add("X-Auth-Token", "b62d023166ac4e2098451b01f9bae142");

                using var response = await _httpClient.SendAsync(request);

                if(!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Error retrieving the player: {response.StatusCode}");
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();

                PlayerDTO? playerData = JsonConvert.DeserializeObject<PlayerDTO>(json);

                if(playerData == null)
                {
                    Console.WriteLine("Failed to deserialize the data");
                    return null;
                }

                return new Player
                {
                    Id = id,
                    FirstName = playerData.FirstName,
                    LastName = playerData.LastName,
                    LastUpdated = playerData.LastUpdated,
                    Nationality = playerData.Nationality,
                    Position = playerData.Position
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception occurred: {ex.Message}");
                return null;
            }
        }

        private ScorerDTO? GetPlayerStatsById(long id, ScorersDTO scorersData)
        {
            return scorersData.Scorers?.FirstOrDefault(s => s.Scorer.Id == id);
        }

        // GET: api/Scorers
        [HttpGet("{id}")]
        private async Task<Player?> RetrievePLScorer(long id)
        {
            try
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Get, $"http://api.football-data.org/v4/competitions/PL/scorers");

                request.Headers.Add("X-Auth-Token", "b62d023166ac4e2098451b01f9bae142");

                using var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Error retrieving scorers: {response.StatusCode}");
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();

                ScorersDTO? scorersData = JsonConvert.DeserializeObject<ScorersDTO>(json);

                if (scorersData == null)
                {
                    Console.WriteLine("Failed to deserialize the data");
                    return null;
                }

                var playerStats = GetPlayerStatsById(id, scorersData);

                if (playerStats != null)
                {
                    return new Player
                    {
                        Id = id,
                        FirstName = playerStats.Scorer.FirstName,
                        LastName = playerStats.Scorer.LastName,
                        LastUpdated = playerStats.Scorer.LastUpdated,
                        Nationality = playerStats.Scorer.Nationality,
                        Position = playerStats.Scorer.Position,
                        GoalCount = playerStats.Goals,
                        PlayedMatches = playerStats.PlayedMatches,
                    };
                }
                else
                {
                    Console.WriteLine("Player not found.");
                    return null;
                }
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception occurred: {ex.Message}");
                return null;
            }
        }
    }
}
