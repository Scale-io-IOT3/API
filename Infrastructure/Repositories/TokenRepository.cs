using Core.Interface;
using Core.Models.API;
using Infrastructure.Persistence.Contexts;
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

        if (existing is not null)
        {
            Map(token, existing);
            await Update(existing);
            return;
        }

        await Create(token);
    }

    private static void Map(Token newToken, Token existing)
    {
        existing.Refresh = newToken.Refresh;
        existing.Access = newToken.Access;
        existing.RefreshExpiry = newToken.RefreshExpiry;
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