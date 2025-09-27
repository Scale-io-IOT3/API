using Core.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Persistence.Contexts;

public class AppDbContext(IConfiguration configuration) : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Meal> Meals => Set<Meal>();
    public DbSet<Food> Foods => Set<Food>();
    public DbSet<Macros> Macros => Set<Macros>();
    public DbSet<MacrosType> MacrosTypes => Set<MacrosType>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MacrosType>().HasData(
            new MacrosType { Id = 1, Name = "Proteins" },
            new MacrosType { Id = 2, Name = "Fat" },
            new MacrosType { Id = 3, Name = "Carbs" }
        );

        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = 1,
                Username = "Monke",
                PasswordHash = "AQAAAAIAAYagAAAAEKS93xVFcKpk6IDRUvMILGvB7fP3fis8q6vpEoZkO/9GAgHzJvqNzPB1iji9hTe8Eg=="
            }
        );
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var src = configuration["SOURCE"];
        optionsBuilder.UseSqlite(src);
    }
}