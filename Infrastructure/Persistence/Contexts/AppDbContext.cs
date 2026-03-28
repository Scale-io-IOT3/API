using Core.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Contexts;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Token> Tokens => Set<Token>();
    public DbSet<Meal> Meals => Set<Meal>();
    public DbSet<Food> Foods => Set<Food>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.Property(u => u.Username).HasMaxLength(64);
            entity.Property(u => u.PasswordHash).HasMaxLength(512);
            entity.HasIndex(u => u.Username).IsUnique();
        });

        modelBuilder.Entity<Meal>(entity =>
        {
            entity.ToTable("Meals");
            entity.HasIndex(m => m.UserId);
            entity.HasOne(m => m.User)
                .WithMany(u => u.Meals)
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Food>(entity =>
        {
            entity.ToTable("MealItems");
            entity.HasIndex(f => f.MealId);
            entity.Property(f => f.Name).HasMaxLength(255);
            entity.Property(f => f.Brands).HasMaxLength(255);
            entity.HasOne(f => f.Meal)
                .WithMany(m => m.Foods)
                .HasForeignKey(f => f.MealId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Token>(entity =>
        {
            entity.ToTable("RefreshTokens");
            entity.Property(t => t.TokenHash).HasMaxLength(512);
            entity.Property(t => t.TokenFingerprint).HasMaxLength(64);
            entity.HasIndex(t => t.UserId);
            entity.HasIndex(t => t.TokenFingerprint).IsUnique();
            entity.HasOne(t => t.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

    }
}
