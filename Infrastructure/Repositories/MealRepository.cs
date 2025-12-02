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
            .Include(m => m.Foods)
            .ThenInclude(f => f.Macros)
            .ToListAsync();
    }


    public Task<Meal?> FindByUsername(string username)
    {
        throw new NotImplementedException();
    }

    public async Task<Meal?> FindById(int id)
    {
        var meals = await GetAll();
        return meals.FirstOrDefault(m => m.Id == id);
    }

    public Task<Meal?> Find(string entry)
    {
        throw new NotImplementedException();
    }

    public async Task CreateOrUpdate(Meal entity)
    {
        await context.Meals.AddAsync(entity);
        await context.SaveChangesAsync();
    }
}