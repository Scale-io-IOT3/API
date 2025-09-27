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


    public async Task<User?> Get(string username)
    {
        var users = await GetAll();
        return users.FirstOrDefault(u => u.Username == username);
    }
}