using Microsoft.AspNetCore.Components;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Text.Json;

namespace WebApp.Services
{
    public class AuthService
    {
        private readonly HttpClient _http;
        private readonly NavigationManager _nav;
        private const string TokenKey = "idToken";

        public string? IdToken { get; private set; }
        public string? Email { get; private set; }
        public string? Username { get; private set; }
        public bool IsAuthenticated => !string.IsNullOrEmpty(IdToken);
        public event Action? OnAuthStateChanged;

        public AuthService(HttpClient http, NavigationManager nav)
        {
            _http = http;
            _nav = nav;
            IdToken = null;
        }

        public async Task<bool> LoginAsync(string email, string password)
        {
            var loginDto = new { Email = email, Password = password };
            var resp = await _http.PostAsJsonAsync("customer/login", loginDto);

            if (!resp.IsSuccessStatusCode) return false;

            // Read back the SignInResponseDTO JSON:
            var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
            IdToken = json.GetProperty("idToken").GetString();
            Username = json.GetProperty("displayName").GetString();

            OnAuthStateChanged?.Invoke();
            return true;
        }

        public void Logout()
        {
            IdToken = null;
            OnAuthStateChanged?.Invoke();
            _nav.NavigateTo("login");
        }

        public void AttachToken(HttpRequestMessage request)
        {
            if (!string.IsNullOrEmpty(IdToken))
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", IdToken);
        }

        public string? GetUidFromToken()
        {
            var token = IdToken; // however you store the current token
            if (string.IsNullOrEmpty(token)) return null;

            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);

            return jwt.Claims.FirstOrDefault(c => c.Type == "user_id" || c.Type == "sub")?.Value;
        }
    }
}
