var builder = WebApplication.CreateBuilder(args);

// Rejestracja kontrolerów
builder.Services.AddControllers();

// Swagger i dokumentacja
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Konfiguracja środowiska
builder.Configuration.AddEnvironmentVariables();

// Rejestracja serwisu (używającego OpenRouter)
builder.Services.AddHttpClient<OpenAiService>(); // możesz zmienić nazwę na OpenRouterService, jeśli chcesz

var app = builder.Build();

// Ustawienia tylko dla trybu developerskiego
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();

// Rejestracja kontrolerów (np. ChatController)
app.MapControllers();

// (opcjonalnie) Zostawiamy endpoint pogodowy, jeśli chcesz
var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild",
    "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
