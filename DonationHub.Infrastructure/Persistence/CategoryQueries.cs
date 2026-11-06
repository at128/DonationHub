using DonationHub.Application.Features.Categories;
using DonationHub.Domain.Entities;
using DonationHub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace DonationHub.Infrastructure.Persistence;

public sealed class CategoryQueries(AppDbContext db) : ICategoryQueries
{
    private static readonly Expression<Func<Category, CategoryResponse>> Projection = c =>
        new CategoryResponse(c.Id, c.NameAr, c.NameEn, c.DescriptionAr, c.DescriptionEn);

    public async Task<IReadOnlyList<CategoryResponse>> GetAllAsync(CancellationToken ct) =>
        await db.Categories.AsNoTracking().OrderBy(c => c.NameEn).Select(Projection).ToListAsync(ct);

    public Task<CategoryResponse?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Categories.AsNoTracking().Where(c => c.Id == id).Select(Projection).SingleOrDefaultAsync(ct);

    public Task<bool> NameExistsAsync(string nameAr, string nameEn, Guid? exceptId, CancellationToken ct) =>
        db.Categories.AsNoTracking().AnyAsync(c => (!exceptId.HasValue || c.Id != exceptId.Value) &&
            (c.NameAr == nameAr || c.NameEn == nameEn), ct);

    // Deleted campaigns still retain a foreign key to their category.
    public Task<bool> HasCampaignsAsync(Guid categoryId, CancellationToken ct) =>
        db.Campaigns.IgnoreQueryFilters().AsNoTracking().AnyAsync(c => c.CategoryId == categoryId, ct);
}
