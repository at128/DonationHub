using DonationHub.Application.Features.Categories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DonationHub.Api.Endpoints;

public static class CategoriesEndpoints
{
    public static RouteGroupBuilder MapCategoriesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/categories").WithTags("Categories");
        group.MapGet("/", async (CategoryService service, CancellationToken ct) =>
            Results.Ok(await service.GetAllAsync(ct)));
        group.MapGet("/{id:guid}", GetById);
        group.MapPost("/", Create).RequireAuthorization(p => p.RequireRole("Admin"));
        group.MapPut("/{id:guid}", Update).RequireAuthorization(p => p.RequireRole("Admin"));
        group.MapDelete("/{id:guid}", Delete).RequireAuthorization(p => p.RequireRole("Admin"));
        return group;
    }

    private static async Task<IResult> GetById(Guid id, CategoryService service, CancellationToken ct)
    {
        var category = await service.GetByIdAsync(id, ct);
        return category is null
            ? Results.NotFound(new { code = "Category.NotFound", error = "Category was not found." })
            : Results.Ok(category);
    }

    private static Task<IResult> Create(CategoryRequest request, CategoryService service, CancellationToken ct) =>
        Write(() => service.CreateAsync(request, ct), category =>
            Results.Created($"/api/v1/categories/{category!.Id}", category));

    private static Task<IResult> Update(Guid id, CategoryRequest request, CategoryService service, CancellationToken ct) =>
        Write(() => service.UpdateAsync(id, request, ct), category => Results.Ok(category));

    private static Task<IResult> Delete(Guid id, CategoryService service, CancellationToken ct) =>
        Write(() => service.DeleteAsync(id, ct), _ => Results.NoContent(), deleting: true);

    private static async Task<IResult> Write(Func<Task<CategoryWriteResult>> operation,
        Func<CategoryResponse?, IResult> success, bool deleting = false)
    {
        CategoryWriteResult result;
        try
        {
            result = await operation();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return Results.Conflict(new { code = "Category.NameAlreadyExists", error = "Category name already exists." });
        }
        catch (DbUpdateException ex) when (deleting && ex.InnerException is SqlException { Number: 547 })
        {
            return Results.Conflict(new { code = "Category.HasCampaigns", error = "Cannot delete a category with campaigns." });
        }

        if (result.ErrorCode is null) return success(result.Category);
        var error = new { code = result.ErrorCode, error = result.ErrorMessage };
        return result.ErrorCode switch
        {
            "Validation.Failed" => Results.BadRequest(error),
            "Category.NotFound" => Results.NotFound(error),
            _ => Results.Conflict(error)
        };
    }
}
