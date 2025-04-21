using CustomerService.Services;
using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace LeeXerri_CustomerService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // — Firebase SDK + Firestore
            FirebaseApp.Create(new AppOptions
            {
                Credential = GoogleCredential.FromFile("firebase-service-account.json")
            });
            builder.Services.AddSingleton(FirebaseAuth.DefaultInstance);
            builder.Services.AddSingleton(sp => {
                var json = File.ReadAllText("firebase-service-account.json");
                return new FirestoreDbBuilder
                {
                    ProjectId = builder.Configuration["Firebase:ProjectId"]!,
                    JsonCredentials = json
                }.Build();
            });

            // — JWT Bearer (Firebase tokens)
            builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opts =>
            {
                var projectId = builder.Configuration["Firebase:ProjectId"];
                var authority = $"https://securetoken.google.com/{projectId}";
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

            // — CORS
            builder.Services.AddCors(opts =>
                opts.AddPolicy("AllowAll", p => p
                    .AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                )
            );

            builder.Services.AddHttpClient();
            builder.Services.AddScoped<ICustomerService, CustomerService.Services.CustomerService>();

            // — MVC & Swagger
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c => {
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
