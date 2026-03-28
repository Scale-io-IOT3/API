using Core.Interface;
using Core.Models.Entities;
using Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class MealRepository(AppDbContext context) : IRepo<Meal>
{
    public async Task<List<Meal>> GetAll()
    {
        return await context.Meals
            .AsNoTracking()
            .Include(m => m.Foods)
            .ToListAsync();
    }

    public async Task<List<Meal>> GetByUsername(string username)
    {
        return await context.Meals
            .AsNoTracking()
            .Include(m => m.Foods)
            .Where(m => m.User.Username == username)
            .ToListAsync();
    }

    public async Task<List<Meal>> GetByUserId(int userId)
    {
        return await context.Meals
            .AsNoTracking()
            .Include(m => m.Foods)
            .Where(m => m.UserId == userId)
            .ToListAsync();
    }

    public Task<Meal?> FindByUsername(string username)
    {
        return context.Meals
            .AsNoTracking()
            .Include(m => m.Foods)
            .Where(m => m.User.Username == username)
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<Meal?> FindById(int id)
    {
        return await context.Meals
            .AsNoTracking()
            .Include(m => m.Foods)
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public Task<Meal?> Find(string entry)
    {
        return int.TryParse(entry, out var id) ? FindById(id) : FindByUsername(entry);
    }

    public async Task CreateOrUpdate(Meal entity)
    {
        await context.Meals.AddAsync(entity);
        await context.SaveChangesAsync();
    }
}
