using DonationHub.Application.Abstractions.Persistence;
using DonationHub.Domain.Entities;

namespace DonationHub.Application.Features.Categories;

public sealed record CategoryRequest(string NameAr, string NameEn, string DescriptionAr, string DescriptionEn);
public sealed record CategoryResponse(Guid Id, string NameAr, string NameEn, string DescriptionAr, string DescriptionEn);
public sealed record CategoryWriteResult(CategoryResponse? Category = null, string? ErrorCode = null, string? ErrorMessage = null);

public interface ICategoryQueries
{
    Task<IReadOnlyList<CategoryResponse>> GetAllAsync(CancellationToken ct);
    Task<CategoryResponse?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<bool> NameExistsAsync(string nameAr, string nameEn, Guid? exceptId, CancellationToken ct);
    Task<bool> HasCampaignsAsync(Guid categoryId, CancellationToken ct);
}

public sealed class CategoryService(
    IGenericRepository<Category> categories, ICategoryQueries queries, IUnitOfWork unitOfWork)
{
    public Task<IReadOnlyList<CategoryResponse>> GetAllAsync(CancellationToken ct) => queries.GetAllAsync(ct);
    public Task<CategoryResponse?> GetByIdAsync(Guid id, CancellationToken ct) => queries.GetByIdAsync(id, ct);

    public async Task<CategoryWriteResult> CreateAsync(CategoryRequest request, CancellationToken ct)
    {
        Category category;
        try
        {
            category = Category.Create(request.NameAr, request.NameEn, request.DescriptionAr, request.DescriptionEn);
        }
        catch (ArgumentException ex)
        {
            return new(ErrorCode: "Validation.Failed", ErrorMessage: ex.Message);
        }

        if (await queries.NameExistsAsync(category.NameAr, category.NameEn, null, ct)) return DuplicateName();
        await categories.AddAsync(category, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return new(ToResponse(category));
    }

    public async Task<CategoryWriteResult> UpdateAsync(Guid id, CategoryRequest request, CancellationToken ct)
    {
        var category = await categories.GetByIdAsync(id, ct);
        if (category is null) return NotFound();
        try
        {
            category.Update(request.NameAr, request.NameEn, request.DescriptionAr, request.DescriptionEn);
        }
        catch (ArgumentException ex)
        {
            return new(ErrorCode: "Validation.Failed", ErrorMessage: ex.Message);
        }

        if (await queries.NameExistsAsync(category.NameAr, category.NameEn, id, ct)) return DuplicateName();
        await unitOfWork.SaveChangesAsync(ct);
        return new(ToResponse(category));
    }

    public async Task<CategoryWriteResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        var category = await categories.GetByIdAsync(id, ct);
        if (category is null) return NotFound();
        try
        {
            category.EnsureCanBeDeleted(await queries.HasCampaignsAsync(id, ct));
        }
        catch (InvalidOperationException ex)
        {
            return new(ErrorCode: "Category.HasCampaigns", ErrorMessage: ex.Message);
        }

        categories.Remove(category);
        await unitOfWork.SaveChangesAsync(ct);
        return new();
    }

    private static CategoryWriteResult NotFound() => new(ErrorCode: "Category.NotFound", ErrorMessage: "Category was not found.");
    private static CategoryWriteResult DuplicateName() => new(ErrorCode: "Category.NameAlreadyExists", ErrorMessage: "Category name already exists.");
    private static CategoryResponse ToResponse(Category category) => new(category.Id, category.NameAr,
        category.NameEn, category.DescriptionAr, category.DescriptionEn);
}
