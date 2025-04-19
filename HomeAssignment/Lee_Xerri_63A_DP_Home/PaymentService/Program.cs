using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PaymentService.Fares;

namespace PaymentService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // — Firebase SDK init
            FirebaseApp.Create(new AppOptions
            {
                Credential = GoogleCredential.FromFile("firebase-service-account.json")
            });

            // — Firestore registration
            builder.Services.AddSingleton(_ =>
            {
                var path = Path.Combine(AppContext.BaseDirectory, "firebase-service-account.json");
                var json = File.ReadAllText(path);
                return new FirestoreDbBuilder
                {
                    ProjectId = builder.Configuration["Firebase:ProjectId"],
                    JsonCredentials = json
                }.Build();
            });

            // — FirebaseAuth
            builder.Services.AddSingleton(FirebaseAdmin.Auth.FirebaseAuth.DefaultInstance);

            // — CORS for local testing (Swagger UI)
            builder.Services.AddCors(o => o.AddPolicy("AllowLocal", p =>
                p.WithOrigins("https://localhost:44373", "http://localhost:5238")
                 .AllowAnyHeader()
                 .AllowAnyMethod()
            ));

            // — JWT Bearer using Firebase tokens
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

            // — Taxi Fare Calculator client
            builder.Services.AddHttpClient<IFareService, FareService>((sp, client) =>
            {
                var cfg = sp.GetRequiredService<IConfiguration>()
                            .GetSection("RapidApi:TaxiFare");
                client.BaseAddress = new Uri($"https://{cfg["Host"]}/");
                client.DefaultRequestHeaders.Add("X-RapidAPI-Host", cfg["Host"]);
                client.DefaultRequestHeaders.Add("X-RapidAPI-Key", cfg["Key"]);
            });

            // HttpClient for BookingService
            builder.Services
             .AddHttpClient("BookingAPI", client =>
             {
                 var url = builder.Configuration["BookingService:BaseUrl"]!;
                 client.BaseAddress = new Uri(url);
             });

            // — MVC + Swagger
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
                    Description = "Enter: Bearer {your_Firebase_ID_token}"
                });
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme {
                            Reference = new OpenApiReference {
                                Type = ReferenceType.SecurityScheme,
                                Id   = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            var app = builder.Build();

            app.UseCors("AllowLocal");
            app.UseSwagger();
            app.UseSwaggerUI();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            app.Run();
        }
    }
}
