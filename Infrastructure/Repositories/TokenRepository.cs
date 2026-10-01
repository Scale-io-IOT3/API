using Core.Interface.Login;
using Core.Models.Entities;
using Infrastructure.Persistence.Contexts;
using Infrastructure.Utils;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class TokenRepository(AppDbContext context) : ITokenRepository
{
    public async Task<List<Token>> GetAll()
    {
        return await context.Tokens
            .AsNoTracking()
            .Include(t => t.User)
            .ToListAsync();
    }

    public async Task<Token?> FindById(int id)
    {
        return await context.Tokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<Token?> FindByUsername(string username)
    {
        return await context.Tokens
            .Include(t => t.User)
            .Where(t => t.User.Username == username)
            .OrderByDescending(t => t.ExpiresAt)
            .FirstOrDefaultAsync();
    }

    public async Task CreateOrUpdate(Token token)
    {
        if (token.Id == 0)
        {
            await Create(token);
            return;
        }

        var existing = await context.Tokens.FirstOrDefaultAsync(existingToken => existingToken.Id == token.Id);

        if (existing is null)
        {
            await Create(token);
            return;
        }

        Map(token, existing);
        await Update(existing);
    }

    public async Task<Token?> Find(string token)
    {
        var fingerprint = Cryptography.FingerprintToken(token);
        var stored = await context.Tokens.AsNoTracking()
            .Include(t => t.User)
            .FirstOrDefaultAsync(t =>
                t.TokenFingerprint == fingerprint &&
                t.RevokedAt == null &&
                t.ExpiresAt > DateTime.UtcNow
            );

        return stored is null ? null : Cryptography.Verify(token, stored.TokenHash) ? stored : null;
    }


    public async Task<bool> TryRotate(Token current, Token replacement)
    {
        // Compare-and-swap ensures concurrent refreshes can consume a token only once.
        var updated = await context.Tokens
            .Where(t => t.Id == current.Id && t.TokenFingerprint == current.TokenFingerprint &&
                        t.RevokedAt == null && t.ExpiresAt > DateTime.UtcNow)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.TokenHash, replacement.TokenHash)
                .SetProperty(t => t.TokenFingerprint, replacement.TokenFingerprint)
                .SetProperty(t => t.ExpiresAt, replacement.ExpiresAt));
        return updated == 1;
    }

    private static void Map(Token newToken, Token old)
    {
        old.TokenHash = newToken.TokenHash;
        old.TokenFingerprint = newToken.TokenFingerprint;
        old.ExpiresAt = newToken.ExpiresAt;
        old.RevokedAt = newToken.RevokedAt;
    }

    private async Task Update(Token token)
    {
        context.Tokens.Update(token);
        await context.SaveChangesAsync();
    }

    private async Task Create(Token token)
    {
        await context.Tokens.AddAsync(token);
        await context.SaveChangesAsync();
    }
}
