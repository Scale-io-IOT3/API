using Core.Interface;
using Core.Models.Entities;
using Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class UserRepository(AppDbContext context) : IRepo<User>
{
    public async Task<List<User>> GetAll()
    {
        return await context.Users
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<User?> FindByUsername(string username)
    {
        return await context.Users.FirstOrDefaultAsync(u => u.Username == username);
    }

    public async Task<User?> FindById(int id)
    {
        return await context.Users.FirstOrDefaultAsync(u => u.Id == id);
    }

    public Task<User?> Find(string entry)
    {
        return int.TryParse(entry, out var id) ? FindById(id) : FindByUsername(entry);
    }

    public async Task CreateOrUpdate(User user)
    {
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();
    }
}
