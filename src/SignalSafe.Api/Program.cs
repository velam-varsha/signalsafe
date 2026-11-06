using System.Text.Json.Serialization;

using SignalSafe.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
    o.MultipartBodyLengthLimit = CheckEndpoints.MaxLayoutBytes + CheckEndpoints.MaxTablesBytes);

// The web app runs on another address in development (Vite) and may in production (Vercel).
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(allowedOrigins).AllowAnyHeader().WithMethods("GET", "POST")));

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();

app.MapOpenApi();
app.UseSwaggerUI(o =>
{
    o.SwaggerEndpoint("/openapi/v1.json", "SignalSafe API v1");
    o.DocumentTitle = "SignalSafe API";
});

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapGet("/health", () => new { status = "ok" }).WithTags("Health");
app.MapCheckEndpoints();

app.Run();

/// <summary>Exposed so the API tests can start the app in memory.</summary>
public partial class Program;
