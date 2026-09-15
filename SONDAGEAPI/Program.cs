using Microsoft.EntityFrameworkCore;
using SONDAGEAPI.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddSondageOpenApi();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddApiKeyAuthentication(builder.Configuration);

var app = builder.Build();

app.UseHttpsRedirection();
app.UseSondageOpenApi();
app.UseApiKeyAuthentication();

app.MapGet("/api/products/{id:int}", async (int id, ApplicationDbContext db) =>
{
    var product = await db.Products.FindAsync(id);
    return product is not null
        ? Results.Ok(product)
        : Results.NotFound($"Aucun produit avec l'id {id}");
})
.WithName("GetProduct");

app.MapGet("/ping", () =>
{
    var ping = new PingResponse("pong", DateTime.UtcNow);
    return ping;
}).WithoutApiKey();

app.Run();

record PingResponse(string Status, DateTime Timestamp);