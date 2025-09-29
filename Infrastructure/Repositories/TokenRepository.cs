using Core.Interface;
using Core.Models.API;
using Infrastructure.Persistence.Contexts;
using Infrastructure.Utils;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class TokenRepository(AppDbContext context) : IRepo<Token>
{
    public async Task<List<Token>> GetAll()
    {
        return await context.Tokens.ToListAsync();
    }

    public async Task<Token?> FindById(int id)
    {
        return await context.Tokens.FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<Token?> FindByUsername(string username)
    {
        return await context.Tokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.User.Username == username);
    }

    public async Task CreateOrUpdate(Token token)
    {
        var existing = await context.Tokens
            .FirstOrDefaultAsync(t => t.Id == token.Id);

        var t = HashToken(token);

        if (existing is null)
        {
            await Create(t);
            return;
        }

        Map(t, existing);
        await Update(existing);
    }

    private static Token HashToken(Token token)
    {
        return new Token
        {
            Id = token.Id,
            Expiry = token.Expiry,
            Refresh = Cryptography.Hash(token.Refresh, token.User),
            User = token.User
        };
    }

    private static void Map(Token newToken, Token old)
    {
        old.Refresh = newToken.Refresh;
        old.Expiry = newToken.Expiry;
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