using DonationHub.Api.Endpoints;
using DonationHub.Application;
using DonationHub.Infrastructure;
using DonationHub.Infrastructure.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

var app = builder.Build();

await IdentitySeeder.SeedAsync(app.Services);

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapCategoriesEndpoints();

app.MapGet("/", () => "Hello World!");

app.Run();
