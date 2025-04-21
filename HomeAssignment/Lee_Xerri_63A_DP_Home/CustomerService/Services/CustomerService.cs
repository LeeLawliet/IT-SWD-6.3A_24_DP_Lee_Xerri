using FirebaseAdmin.Auth;
using Google.Cloud.Firestore;
using LeeXerri_CustomerService.DTO;
using LeeXerri_CustomerService.Models;
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
        Task<SignInResponse> LoginAsync(LoginDTO dto);
        Task<(User User, IEnumerable<Notification> Inbox)> GetProfileAsync(string uid);
        Task SendNotificationAsync(string uid, string message);
        Task<IEnumerable<Notification>> GetNotificationsAsync(string uid);
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
                Username = dto.Username,
                DiscountAvailable = false
            };
            await _db.Collection("users")
                     .Document(user.Uid)
                     .SetAsync(user);

            return (user.Uid, user.Email, user.Username);
        }

        public async Task<SignInResponse> LoginAsync(LoginDTO dto)
        {
            var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={_apiKey}";
            var payload = new { email = dto.Email, password = dto.Password, returnSecureToken = true };
            var resp = await _http.PostAsJsonAsync(url, payload);
            if (!resp.IsSuccessStatusCode)
                throw new UnauthorizedAccessException("Invalid credentials.");
            return await resp.Content.ReadFromJsonAsync<SignInResponse>()!;
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
                .OrderByDescending("CreatedAt")
                .GetSnapshotAsync();

            var inbox = notesSnap.Documents
                                .Select(d => d.ConvertTo<Notification>());

            return (user, inbox);
        }

        public async Task SendNotificationAsync(string uid, string message)
        {
            var note = new Notification
            {
                Id = Guid.NewGuid().ToString(),
                Message = message,
                CreatedAt = Timestamp.GetCurrentTimestamp(),
                IsRead = false
            };
            await _db
                .Collection("users")
                .Document(uid)
                .Collection("notifications")
                .Document(note.Id)
                .SetAsync(note);
        }

        public async Task<IEnumerable<Notification>> GetNotificationsAsync(string uid)
        {
            var snaps = await _db
                .Collection("users")
                .Document(uid)
                .Collection("notifications")
                .OrderByDescending("CreatedAt")
                .GetSnapshotAsync();

            return snaps.Documents
                        .Select(d => d.ConvertTo<Notification>());
        }
    }
}
