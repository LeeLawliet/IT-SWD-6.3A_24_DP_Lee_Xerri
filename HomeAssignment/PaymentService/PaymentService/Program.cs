using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Google.Cloud.Firestore.V1;
using Google.Cloud.PubSub.V1;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PaymentService.Fares;
using PaymentService.Services;


namespace LeeXerri_PaymentService
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

            // — CORS (for localhost testing)
            builder.Services.AddCors(opts => opts.AddPolicy("AllowAll", p =>
                p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

            builder.Services.AddSingleton<SubscriberServiceApiClient>(sp =>
            {
                var creds = GoogleCredential.FromFile("gcp-service-account.json");
                return new SubscriberServiceApiClientBuilder { Credential = creds }.Build();
            });

            builder.Services.AddHostedService<DiscountSubscriberService>();

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

            builder.Services.AddHttpClient<IFareService, FareService>((sp, client) =>
            {
                var cfg = sp.GetRequiredService<IConfiguration>().GetSection("RapidApi:TaxiFare");
                client.BaseAddress = new Uri($"https://{cfg["Host"]}/");
                client.DefaultRequestHeaders.Add("X-RapidAPI-Host", cfg["Host"]);
                client.DefaultRequestHeaders.Add("X-RapidAPI-Key", cfg["Key"]);
            });

            builder.Services.AddHttpClient("BookingAPI", client =>
            {
                client.BaseAddress = new Uri(builder.Configuration["BookingService:BaseUrl"]!);
            });

            builder.Services.AddHttpClient("CustomerAPI", client =>
            {
                client.BaseAddress = new Uri(builder.Configuration["CustomerService:BaseUrl"]!);
            });

            //   * LocationService (typed client)
            builder.Services.AddHttpClient("WeatherAPI", (sp, client) =>
            {
                var cfg = sp.GetRequiredService<IConfiguration>().GetSection("RapidApi:WeatherAPI");
                client.BaseAddress = new Uri($"https://{cfg["Host"]}/");
                client.DefaultRequestHeaders.Add("X-RapidAPI-Host", cfg["Host"]);
                client.DefaultRequestHeaders.Add("X-RapidAPI-Key", cfg["Key"]);
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
                    Description = "Enter: Bearer {your Firebase ID token}"
                });
                c.AddSecurityRequirement(new OpenApiSecurityRequirement {
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
                // only show PaymentController in this service’s Swagger:
                c.DocInclusionPredicate((docName, apiDesc) =>
                    apiDesc.ActionDescriptor.RouteValues["controller"]?
                        .Equals("Payment", StringComparison.OrdinalIgnoreCase) ?? false);
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