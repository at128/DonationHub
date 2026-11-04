using DonationHub.Application;
using DonationHub.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices();

var app = builder.Build();


app.MapGet("/", () => "Hello World!");

app.Run();
