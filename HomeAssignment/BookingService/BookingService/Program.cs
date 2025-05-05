using BookingService.Models;
using BookingService.Services;
using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Google.Cloud.PubSub.V1;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
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

            // Load Firebase and PubSub credentials from separate files
            var firebaseCreds = GoogleCredential.FromFile("firebase-service-account.json");
            var pubsubCreds = GoogleCredential.FromFile("gcp-service-account.json");

            // Initialize Firestore manually (not registered yet)
            var db = new FirestoreDbBuilder
            {
                ProjectId = "itswd63a24dpleexerri",
                Credential = firebaseCreds
            }.Build();

            // Create the Pub/Sub publisher synchronously
            var pubsubClient = new PublisherServiceApiClientBuilder
            {
                Credential = pubsubCreds
            }.Build();

            // FirebaseAuth (Firebase SDK requires Create)
            FirebaseApp.Create(new AppOptions { Credential = firebaseCreds });

            // Register services
            builder.Services.AddSingleton(db);
            builder.Services.AddSingleton(pubsubClient);
            builder.Services.AddSingleton(sp =>
            {
                var config = builder.Configuration.GetSection("PubSub");
                return new PubSubTopics
                {
                    DiscountTopic = TopicName.FromProjectTopic(config["ProjectId"], config["DiscountTopicId"]),
                    BookingTopic = TopicName.FromProjectTopic(config["ProjectId"], config["BookingTopicId"])
                };
            });

            builder.Services.AddSingleton(FirebaseAuth.DefaultInstance);
            builder.Services.AddScoped<IBookingService, BookingService.Services.BookingService>();

            // JWT Authentication
            builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opts =>
            {
                var projectId = builder.Configuration["Firebase:ProjectId"];
                var authority = "https://securetoken.google.com/itswd63a24dpleexerri";
                opts.Authority = authority;
                opts.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = authority,
                    ValidateAudience = true,
                    ValidAudience = "itswd63a24dpleexerri",
                    ValidateLifetime = true
                };
            });

            builder.Services.AddAuthorization(options =>
            {
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
            });

            // HttpClients
            builder.Services.AddHttpClient("CustomerAPI", client =>
            {
                client.BaseAddress = new Uri(builder.Configuration["CustomerService:BaseUrl"]!);
            });
            builder.Services.AddHttpClient();

            // Swagger
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

            builder.Services.AddControllers();

            // Build and run
            var app = builder.Build();
            app.UseSwagger();
            app.UseSwaggerUI();
            app.Use(async (context, next) =>
            {
                Console.WriteLine($"Path: {context.Request.Path}");
                Console.WriteLine($"IsAuthenticated: {context.User.Identity?.IsAuthenticated}");
                Console.WriteLine($"AuthType: {context.User.Identity?.AuthenticationType}");
                Console.WriteLine($"Claims: {string.Join(", ", context.User.Claims.Select(c => $"{c.Type}={c.Value}"))}");
                await next();
            });
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            app.Run();
        }
    }
}