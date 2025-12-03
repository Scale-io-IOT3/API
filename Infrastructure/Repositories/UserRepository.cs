using Core.Interface;
using Core.Models.Entities;
using Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class UserRepository(AppDbContext context) : IRepo<User>
{
    public async Task<List<User>> GetAll()
    {
        return await context.Users.ToListAsync();
    }

    public async Task<User?> FindByUsername(string username)
    {
        var users = await GetAll();
        return users.FirstOrDefault(u => u.Username == username);
    }

    public async Task<User?> FindById(int id)
    {
        var users = await GetAll();
        return users.FirstOrDefault(u => u.Id == id);
    }

    public Task<User?> Find(string entry)
    {
        throw new NotImplementedException();
    }

    public async Task CreateOrUpdate(User user)
    {
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();
    }
}