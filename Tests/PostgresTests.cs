using Core.Models.Entities;
using Infrastructure.Persistence.Contexts;
using Infrastructure.Repositories;
using Infrastructure.Utils;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Tests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TEST_DATABASE_URL")))
            Skip = "Set TEST_DATABASE_URL to a dedicated PostgreSQL test database.";
    }
}

public sealed class PostgresTests
{
    [PostgresFact]
    public async Task MigrationsAndConcurrentRefreshWorkOnPostgres()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("TEST_DATABASE_URL")).Options;
        await using var setup = new AppDbContext(options);
        await setup.Database.MigrateAsync();
        Assert.Empty(await setup.Database.GetPendingMigrationsAsync());
        var user = new User { Username = $"test-{Guid.NewGuid():N}", PasswordHash = Cryptography.Hash("test") };
        setup.Users.Add(user);
        await setup.SaveChangesAsync();
        try
        {
            var plaintext = Cryptography.GenerateToken();
            await new TokenRepository(setup).CreateOrUpdate(new Token
            {
                UserId = user.Id, TokenHash = Cryptography.Hash(plaintext),
                TokenFingerprint = Cryptography.FingerprintToken(plaintext)
            });
            await using var firstDb = new AppDbContext(options);
            await using var secondDb = new AppDbContext(options);
            var firstRepo = new TokenRepository(firstDb);
            var secondRepo = new TokenRepository(secondDb);
            var firstSnapshot = (await firstRepo.Find(plaintext))!;
            var secondSnapshot = (await secondRepo.Find(plaintext))!;
            var outcomes = await Task.WhenAll(firstRepo.TryRotate(firstSnapshot, Replacement()),
                secondRepo.TryRotate(secondSnapshot, Replacement()));
            Assert.Single(outcomes, success => success);
            Assert.Null(await firstRepo.Find(plaintext));
        }
        finally
        {
            setup.Users.Remove(user);
            await setup.SaveChangesAsync();
        }
    }

    private static Token Replacement()
    {
        var plaintext = Cryptography.GenerateToken();
        return new Token { TokenHash = Cryptography.Hash(plaintext), TokenFingerprint = Cryptography.FingerprintToken(plaintext) };
    }
}
