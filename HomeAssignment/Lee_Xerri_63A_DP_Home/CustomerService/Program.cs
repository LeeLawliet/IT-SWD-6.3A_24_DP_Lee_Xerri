using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;

namespace LeeXerri_CustomerService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // initialize Firebase SDK from service account JSON
            FirebaseApp.Create(new AppOptions
            {
                Credential = GoogleCredential.FromFile("firebase-service-account.json")
            });

            // register FirestoreDb
            builder.Services.AddSingleton(_ =>
            {
                // load the JSON
                var jsonPath = Path.Combine(AppContext.BaseDirectory, "firebase-service-account.json");
                var json = File.ReadAllText(jsonPath);

                // build a FirestoreDb with those credentials
                var fbBuilder = new FirestoreDbBuilder
                {
                    ProjectId = "itswd63a24dpleexerri",
                    // JsonCredentials takes the entire JSON text
                    JsonCredentials = json
                };
                return fbBuilder.Build();
            });

            // register FirebaseAuth for token verification
            builder.Services.AddSingleton(FirebaseAdmin.Auth.FirebaseAuth.DefaultInstance);

            // HttpClient for Auth REST calls login
            builder.Services.AddHttpClient();

            // MVC + Swagger
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();
            app.UseSwagger();
            app.UseSwaggerUI();
            app.MapControllers();
            app.Run();
        }
    }
}
