using FirebaseAdmin.Auth;
using Google.Cloud.Firestore;
using LeeXerri_CustomerService.DTO;
using LeeXerri_CustomerService.Models;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using System.Reflection;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly FirestoreDb _db;
    private readonly FirebaseAuth _auth;
    private readonly string _apiKey;
    private readonly HttpClient _http;

    public UserController(
        FirestoreDb db,
        FirebaseAuth auth,
        IConfiguration config,
        IHttpClientFactory httpFactory)
    {
        _db = db;
        _auth = auth;
        _apiKey = config["Firebase:ApiKey"];
        _http = httpFactory.CreateClient();
    }

    // register using Firebase Auth + Firestore
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        // create in Firebase Auth
        var userRec = await _auth.CreateUserAsync(new UserRecordArgs
        {
            Email = dto.Email,
            Password = dto.Password,
            DisplayName = dto.Username
        });

        // mirror to Firestore
        var user = new User
        {
            Uid = userRec.Uid,
            Email = dto.Email,
            Username = dto.Username
        };
        await _db.Collection("users").Document(user.Uid).SetAsync(user);

        return Ok(new { user.Uid, user.Email, user.Username });
    }

    // login by calling Firebase REST API for ID token
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={_apiKey}";
        var payload = new { email = dto.Email, password = dto.Password, returnSecureToken = true };
        var resp = await _http.PostAsJsonAsync(url, payload);

        if (!resp.IsSuccessStatusCode)
        {
            return Unauthorized("Invalid credentials.");
        }

        var data = await resp.Content.ReadFromJsonAsync<SignInResponse>();
        return Ok(data);
    }

    // verify bearer token and ensure the UID matches
    private async Task<bool> ValidateTokenAsync(string bearer, string uid)
    {
        if (string.IsNullOrEmpty(bearer) || !bearer.StartsWith("Bearer "))
        {
            return false;
        }

        var token = bearer.Substring("Bearer ".Length);
        var decoded = await _auth.VerifyIdTokenAsync(token);
        return decoded.Uid == uid;
    }

    // profile + inbox
    [HttpGet("{uid}")]
    public async Task<IActionResult> GetProfile(
        string uid,
        [FromHeader(Name = "Authorization")] string authHeader)
    {
        if (!await ValidateTokenAsync(authHeader, uid))
            return Forbid();

        // fetch user doc
        var userSnap = await _db.Collection("users").Document(uid).GetSnapshotAsync();
        if (!userSnap.Exists) return NotFound("User not found.");

        // fetch notifications sub‑collection
        var notes = await _db
            .Collection("users")
            .Document(uid)
            .Collection("notifications")
            .OrderByDescending("CreatedAt")
        .GetSnapshotAsync();

        return Ok(new
        {
            user = userSnap.ConvertTo<User>(),
            inbox = notes.Documents.Select(d => d.ConvertTo<Notification>())
        });
    }

    // send notification
    [HttpPost("{uid}/notifications")]
    public async Task<IActionResult> SendNotification(
        string uid,
        [FromBody] string message,
        [FromHeader(Name = "Authorization")] string authHeader)
    {
        if (!await ValidateTokenAsync(authHeader, uid))
            return Forbid();

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

        return Ok();
    }

    // list notifications only
    [HttpGet("{uid}/notifications")]
    public async Task<IActionResult> GetNotificationsOnly(
        string uid,
        [FromHeader(Name = "Authorization")] string authHeader)
    {
        if (!await ValidateTokenAsync(authHeader, uid))
            return Forbid();

        var snaps = await _db
            .Collection("users")
            .Document(uid)
            .Collection("notifications")
            .OrderByDescending("CreatedAt")
        .GetSnapshotAsync();

        return Ok(snaps.Documents.Select(d => d.ConvertTo<Notification>()));
    }
}
