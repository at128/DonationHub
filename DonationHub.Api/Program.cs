using DonationHub.Application;
using DonationHub.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

var app = builder.Build();


app.MapGet("/", () => "Hello World!");

app.Run();
