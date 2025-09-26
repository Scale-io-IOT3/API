using Core.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Contexts;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public required DbSet<User> Users;
}