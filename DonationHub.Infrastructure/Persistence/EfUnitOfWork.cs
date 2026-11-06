using DonationHub.Application.Abstractions.Persistence;
using DonationHub.Infrastructure.Data;

namespace DonationHub.Infrastructure.Persistence;

public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public EfUnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public Task<int> SaveChangesAsync(
        CancellationToken ct = default)
    {
        return _context.SaveChangesAsync(ct);
    }
}
