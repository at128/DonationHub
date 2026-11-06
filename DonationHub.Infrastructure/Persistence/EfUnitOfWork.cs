using DonationHub.Application.Abstractions.Persistence;
using DonationHub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DonationHub.Infrastructure.Persistence;

public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public EfUnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync(
        CancellationToken ct = default)
    {
        try { return await _context.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException ex) { throw new PersistenceConcurrencyException(ex); }
    }
}
