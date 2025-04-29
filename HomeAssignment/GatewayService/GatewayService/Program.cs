using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);
builder.Services.AddOcelot();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorWebApp", builder =>
    {
        builder
            .WithOrigins("https://localhost:44331") // Blazor WebAssembly origin
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddControllers();

var app = builder.Build();

app.UseCors("AllowBlazorWebApp");

await app.UseOcelot(); // Middleware must be awaited

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
