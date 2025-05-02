using FirebaseAdmin.Auth;
using Google.Cloud.Firestore;
using CustomerService.DTO;
using CustomerService.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CustomerService.Services
{
    public interface ICustomerService
    {
        Task<(string Uid, string Email, string Username)> RegisterAsync(RegisterDTO dto);
        Task<SignInResponseDTO> LoginAsync(LoginDTO dto);
        Task<(User User, IEnumerable<Notification> Inbox)> GetProfileAsync(string uid);
        Task<IEnumerable<NotificationDTO>> GetNotificationsAsync(string uid);
    }

    public class CustomerService : ICustomerService
    {
        private readonly FirestoreDb _db;
        private readonly FirebaseAuth _auth;
        private readonly string _apiKey;
        private readonly HttpClient _http;

        public CustomerService(
            FirestoreDb db,
            FirebaseAuth auth,
            IConfiguration config,
            IHttpClientFactory httpFactory)
        {
            _db = db;
            _auth = auth;
            _apiKey = config["Firebase:ApiKey"]!;
            _http = httpFactory.CreateClient();
        }

        public async Task<(string Uid, string Email, string Username)> RegisterAsync(RegisterDTO dto)
        {
            var userRec = await _auth.CreateUserAsync(new UserRecordArgs
            {
                Email = dto.Email,
                Password = dto.Password,
                DisplayName = dto.Username
            });

            var user = new User
            {
                Uid = userRec.Uid,
                Email = dto.Email,
                Username = dto.Username
            };
            await _db.Collection("users")
                     .Document(user.Uid)
                     .SetAsync(user);

            return (user.Uid, user.Email, user.Username);
        }

        public async Task<SignInResponseDTO> LoginAsync(LoginDTO dto)
        {
            var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={_apiKey}";
            var payload = new { email = dto.Email, password = dto.Password, returnSecureToken = true };
            var resp = await _http.PostAsJsonAsync(url, payload);

            if (!resp.IsSuccessStatusCode)
                throw new UnauthorizedAccessException("Invalid credentials.");

            var result = await resp.Content.ReadFromJsonAsync<SignInResponseDTO>();
            if (result is null || string.IsNullOrWhiteSpace(result.idToken))
                throw new UnauthorizedAccessException("Login failed to return a token.");

            return result;
        }

        public async Task<(User User, IEnumerable<Notification> Inbox)> GetProfileAsync(string uid)
        {
            // fetch user from db
            var userSnap = await _db
                .Collection("users")
                .Document(uid)
                .GetSnapshotAsync();
            if (!userSnap.Exists)
                throw new KeyNotFoundException("User not found.");

            var user = userSnap.ConvertTo<User>();

            // fetch notifications sub‑collection from db
            var notesSnap = await _db
                .Collection("users")
                .Document(uid)
                .Collection("notifications")
                .OrderByDescending("timestamp")
                .GetSnapshotAsync();

            var inbox = notesSnap.Documents
                                .Select(d => d.ConvertTo<Notification>());

            return (user, inbox);
        }

        public async Task<IEnumerable<NotificationDTO>> GetNotificationsAsync(string uid)
        {
            var snaps = await _db
                .Collection("users")
                .Document(uid)
                .Collection("notifications")
                .OrderByDescending("timestamp")
                .GetSnapshotAsync();

            return snaps.Documents.Select(d => new NotificationDTO
            {
                // use the document‐ID as your notification ID
                Id = d.Id,
                Message = d.GetValue<string>("message"),
                // pull the Firestore Timestamp and convert to DateTime
                Timestamp = d.GetValue<Timestamp>("timestamp").ToDateTime()
            });
        }
    }
}
