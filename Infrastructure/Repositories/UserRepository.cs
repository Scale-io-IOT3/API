using Core.Interface;
using Core.Models.Entities;
using Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class UserRepository(AppDbContext context) : IRepo<User>
{
    public async Task<List<User>> GetAll()
    {
        if (context == null) throw new InvalidOperationException("DbContext is null");
        return context.Users == null
            ? throw new InvalidOperationException("Users DbSet is null")
            : await context.Users.ToListAsync();
    }


    public async Task<User?> Get(string username)
    {
        var users = await GetAll();
        return users.FirstOrDefault(u => u.Username == username);
    }
}