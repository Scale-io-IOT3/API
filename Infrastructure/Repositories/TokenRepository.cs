using Core.Interface;
using Core.Models.Entities;
using Infrastructure.Persistence.Contexts;
using Infrastructure.Utils;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class TokenRepository(AppDbContext context) : IRepo<Token>
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
        var t = HashToken(token);
        if (token.Id == 0)
        {
            await Create(t);
            return;
        }

        var existing = await context.Tokens.FirstOrDefaultAsync(existingToken => existingToken.Id == token.Id);

        if (existing is null)
        {
            await Create(t);
            return;
        }

        Map(t, existing);
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


    private static Token HashToken(Token token)
    {
        return new Token
        {
            Id = token.Id,
            UserId = token.UserId,
            ExpiresAt = token.ExpiresAt,
            RevokedAt = token.RevokedAt,
            TokenHash = Cryptography.Hash(token.TokenHash),
            TokenFingerprint = Cryptography.FingerprintToken(token.TokenFingerprint)
        };
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
