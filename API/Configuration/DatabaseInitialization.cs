using Core.Models.Entities;
using Infrastructure.Persistence.Contexts;
using Infrastructure.Utils;
using Microsoft.EntityFrameworkCore;

namespace Scale.io_API.Configuration;

public static class DatabaseInitialization
{
    public static async Task InitializeDatabaseAsync(this WebApplication app, bool migrateOnly = false)
    {
        var migrate = migrateOnly || (app.Configuration.GetValue<bool?>("ApplyMigrationsOnStartup") ??
                                      app.Environment.IsDevelopment());
        var seed = !migrateOnly && app.Environment.IsDevelopment() &&
                   app.Configuration.GetValue<bool>("SeedDefaultUserOnStartup");
        if (!migrate && !seed) return;

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (migrate) await db.Database.MigrateAsync();
        if (!seed) return;

        var username = app.Configuration["DevelopmentUser:Username"];
        var password = app.Configuration["DevelopmentUser:Password"];
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("DevelopmentUser:Username and Password are required when seeding is enabled.");
        if (await db.Users.AnyAsync(u => u.Username == username)) return;

        var user = new User { Username = username, PasswordHash = "" };
        user.PasswordHash = Cryptography.Hash(password, user);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        app.Logger.LogInformation("Development user created.");
    }
}
