
using DonationHub.Application.Abstractions.Persistence;
using DonationHub.Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DonationHub.Api.Endpoints;

public static class CategoriesEndpoints
{
    public static RouteGroupBuilder MapCategoriesEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/categories")
            .WithTags("Categories");

        group.MapGet("/", GetAll);
        group.MapGet("/{id:guid}", GetById);

        group.MapPost("/", Create)
            .RequireAuthorization(p => p.RequireRole("Admin"));

        group.MapPut("/{id:guid}", Update)
            .RequireAuthorization(p => p.RequireRole("Admin"));

        group.MapDelete("/{id:guid}", Delete)
            .RequireAuthorization(p => p.RequireRole("Admin"));

        return group;
    }

    private static async Task<IResult> GetAll(
        IRepository<Category> repository,
        CancellationToken ct)
    {
        var categories = await repository.Query()
            .OrderBy(c => c.NameEn)
            .Select(c => new CategoryResponse(
                c.Id, c.NameAr, c.NameEn,
                c.DescriptionAr, c.DescriptionEn))
            .ToListAsync(ct);

        return Results.Ok(categories);
    }

    private static async Task<IResult> GetById(
        Guid id,
        IRepository<Category> repository,
        CancellationToken ct)
    {
        var category = await repository.GetByIdAsync(id, ct);

        return category is null
            ? Results.NotFound()
            : Results.Ok(ToResponse(category));
    }

    private static async Task<IResult> Create(
        CategoryRequest request,
        IRepository<Category> repository,
        IUnitOfWork unitOfWork,
        CancellationToken ct)
    {
        Category category;

        try
        {
            category = Category.Create(
                request.NameAr,
                request.NameEn,
                request.DescriptionAr,
                request.DescriptionEn);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }

        var duplicate = await repository.Query().AnyAsync(
            c => c.NameAr == category.NameAr ||
                 c.NameEn == category.NameEn, ct);

        if (duplicate)
            return Results.Conflict(
                new { error = "Category name already exists." });

        await repository.AddAsync(category, ct);

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return Results.Conflict(
                new { error = "Category name already exists." });
        }

        return Results.Created(
            $"/api/v1/categories/{category.Id}",
            ToResponse(category));
    }

    private static async Task<IResult> Update(
        Guid id,
        CategoryRequest request,
        IRepository<Category> repository,
        IUnitOfWork unitOfWork,
        CancellationToken ct)
    {
        var category = await repository.GetByIdAsync(id, ct);

        if (category is null)
            return Results.NotFound();

        try
        {
            category.Update(
                request.NameAr,
                request.NameEn,
                request.DescriptionAr,
                request.DescriptionEn);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }

        var duplicate = await repository.Query().AnyAsync(
            c => c.Id != id &&
                 (c.NameAr == category.NameAr ||
                  c.NameEn == category.NameEn), ct);

        if (duplicate)
            return Results.Conflict(
                new { error = "Category name already exists." });

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return Results.Conflict(
                new { error = "Category name already exists." });
        }

        return Results.Ok(ToResponse(category));
    }

    private static async Task<IResult> Delete(
        Guid id,
        IRepository<Category> categories,
        IRepository<Campaign> campaigns,
        IUnitOfWork unitOfWork,
        CancellationToken ct)
    {
        var category = await categories.GetByIdAsync(id, ct);

        if (category is null)
            return Results.NotFound();

        // Include soft-deleted campaigns to preserve FK integrity.
        var hasCampaigns = await campaigns.Query()
            .IgnoreQueryFilters()
            .AnyAsync(c => c.CategoryId == id, ct);

        if (hasCampaigns)
        {
            return Results.Conflict(new
            {
                error = "Cannot delete a category with campaigns."
            });
        }

        categories.Remove(category);

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException { Number: 547 })
        {
            return Results.Conflict(new
            {
                error = "Cannot delete a category with campaigns."
            });
        }

        return Results.NoContent();
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException is SqlException
        {
            Number: 2601 or 2627
        };

    private static CategoryResponse ToResponse(Category c)
        => new(
            c.Id, c.NameAr, c.NameEn,
            c.DescriptionAr, c.DescriptionEn);
}

public sealed record CategoryRequest(
    string NameAr,
    string NameEn,
    string DescriptionAr,
    string DescriptionEn);

public sealed record CategoryResponse(
    Guid Id,
    string NameAr,
    string NameEn,
    string DescriptionAr,
    string DescriptionEn);
