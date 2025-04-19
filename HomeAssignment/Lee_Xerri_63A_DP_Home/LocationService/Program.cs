using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using LocationService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace LocationService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Load Firebase settings
            var firebaseProjectId = builder.Configuration["Firebase:ProjectId"];
            var firebaseCredentialPath = Path.Combine(AppContext.BaseDirectory, "firebase-service-account.json");

            // Initialize Firebase SDK
            FirebaseApp.Create(new AppOptions
            {
                Credential = GoogleCredential.FromFile(firebaseCredentialPath)
            });

            // Firestore registration
            builder.Services.AddSingleton(_ =>
            {
                var json = File.ReadAllText(firebaseCredentialPath);
                return new FirestoreDbBuilder
                {
                    ProjectId = firebaseProjectId,
                    JsonCredentials = json
                }.Build();
            });

            // FirebaseAuth
            builder.Services.AddSingleton(FirebaseAdmin.Auth.FirebaseAuth.DefaultInstance);

            // CORS (allow frontend or Swagger origins)
            builder.Services.AddCors(opts => opts.AddPolicy("AllowAll", p =>
                p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

            // JWT Authentication using Firebase
            var authority = $"https://securetoken.google.com/{firebaseProjectId}";
            builder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(opts =>
                {
                    opts.Authority = authority;
                    opts.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = authority,
                        ValidateAudience = true,
                        ValidAudience = firebaseProjectId,
                        ValidateLifetime = true
                    };
                });
            builder.Services.AddAuthorization();

            // WeatherAPI.com client for forecast
            builder.Services.AddHttpClient("WeatherAPI", client =>
            {
                var cfg = builder.Configuration.GetSection("RapidApi:WeatherApi");
                client.BaseAddress = new Uri($"https://{cfg["Host"]}/");
                client.DefaultRequestHeaders.Add("X-RapidAPI-Host", cfg["Host"]);
                client.DefaultRequestHeaders.Add("X-RapidAPI-Key", cfg["Key"]);
            });

            // LocationService DI
            builder.Services.AddScoped<ILocationService, LocationService.Services.LocationService>();

            // Controllers and Swagger
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    Description = "Enter 'Bearer <ID token>'"
                });
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
            });

            var app = builder.Build();
            app.UseCors("AllowAll");
            app.UseSwagger();
            app.UseSwaggerUI();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            app.Run();
        }
    }
}
