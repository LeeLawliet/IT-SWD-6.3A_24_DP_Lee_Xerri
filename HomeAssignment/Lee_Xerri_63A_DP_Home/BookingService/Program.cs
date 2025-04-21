using BookingService.Services;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace BookingService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();

            // Initialize Firebase SDK
            FirebaseApp.Create(new AppOptions
            {
                Credential = GoogleCredential.FromFile("firebase-service-account.json")
            });

            // Firestore registration
            builder.Services.AddSingleton(_ =>
            {
                var json = File.ReadAllText("firebase-service-account.json");
                return new FirestoreDbBuilder
                {
                    ProjectId = builder.Configuration["Firebase:ProjectId"],
                    JsonCredentials = json
                }.Build();
            });

            // FirebaseAuth
            builder.Services.AddSingleton(FirebaseAdmin.Auth.FirebaseAuth.DefaultInstance);

            // CORS (allow frontend or Swagger origins)
            builder.Services.AddCors(opts => opts.AddPolicy("AllowAll", p =>
                p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

            // — JWT Bearer (Firebase Issuer/Audience)
            var projectId = builder.Configuration["Firebase:ProjectId"];
            var authority = $"https://securetoken.google.com/{projectId}";
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
                        ValidAudience = projectId,
                        ValidateLifetime = true
                    };
                });
            builder.Services.AddAuthorization();

            builder.Services.AddHttpClient("CustomerAPI", client =>
            {
                client.BaseAddress = new Uri(builder.Configuration["CustomerService:BaseUrl"]!);
            });

            // add Swagger with Bearer auth scheme
            builder.Services.AddSwaggerGen(c =>
            {
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    Description = "Enter: Bearer {token}"
                });
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            // register FirestoreDb
            builder.Services.AddSingleton(_ =>
            {
                var jsonPath = Path.Combine(AppContext.BaseDirectory, "firebase-service-account.json");
                var json = File.ReadAllText(jsonPath);
                var fbBuilder = new FirestoreDbBuilder
                {
                    ProjectId = "itswd63a24dpleexerri",
                    JsonCredentials = json
                };
                return fbBuilder.Build();
            });

            // register FirebaseAuth for token verification
            builder.Services.AddSingleton(FirebaseAdmin.Auth.FirebaseAuth.DefaultInstance);

            builder.Services.AddScoped<BookingService.Services.IBookingService,
                           BookingService.Services.BookingService>();

            // HttpClient for Auth REST calls
            builder.Services.AddHttpClient();

            // MVC + Swagger UI
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();

            var app = builder.Build();

            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();
            app.Run();
        }
    }
}