using DonationHub.Api.Endpoints;
using DonationHub.Application;
using DonationHub.Infrastructure;
using DonationHub.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration, builder.Environment.IsDevelopment());


builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "DonationHub API";
        document.Info.Version = "v1";

        document.Components ??= new OpenApiComponents();

        document.Components.SecuritySchemes =
            new Dictionary<string, IOpenApiSecurityScheme>
            {
                ["Bearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT"
                }
            };

        return Task.CompletedTask;
    });

    options.AddOperationTransformer((operation, context, _) =>
    {
        var metadata =
            context.Description.ActionDescriptor.EndpointMetadata;

        var requiresAuth =
            metadata.OfType<IAuthorizeData>().Any() ||
            metadata.OfType<AuthorizationPolicy>().Any();

        if (requiresAuth)
        {
            operation.Security ??= [];

            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(
                    "Bearer", context.Document)] = []
            });
        }

        return Task.CompletedTask;
    });
});


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.MapScalarApiReference(options =>
        options.WithTitle("DonationHub API")
               .AddPreferredSecuritySchemes("Bearer"));
}

app.Services.GetRequiredService<IStartupValidator>().Validate();
await IdentitySeeder.SeedAsync(app.Services, app.Environment.IsDevelopment());

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapCategoriesEndpoints();

app.MapGet("/", () => "Hello World!");

app.Run();

public partial class Program;
