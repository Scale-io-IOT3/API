using Core.DTO.Foods;
using Core.Interface.Foods;
using Core.Models.Entities;
using Infrastructure.Persistence.Contexts;
using Infrastructure.Utils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Tests;

public sealed class ApiFactory(bool enableApiDocs = false) : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    public const string Password = "test-password";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "test-api",
            ["Jwt:Audience"] = "test-mobile",
            ["Jwt:Key"] = Convert.ToBase64String(new byte[64]),
            ["Jwt:TokenValidityMins"] = "60",
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=unused;Username=test",
            ["ApplyMigrationsOnStartup"] = "false",
            ["EnableApiDocs"] = enableApiDocs.ToString(),
            ["SeedDefaultUserOnStartup"] = "true"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
            services.RemoveAll<IFreshFoodsService>();
            services.RemoveAll<IBarcodeService>();
            services.AddSingleton<IFreshFoodsService, TestFoods>();
            services.AddSingleton<IBarcodeService, TestFoods>();
        });
    }

    public void Initialize()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        var user = new User { Username = "tester", PasswordHash = "" };
        user.PasswordHash = Cryptography.Hash(Password, user);
        db.Users.Add(user);
        db.SaveChanges();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }

    private sealed class TestFoods : IFreshFoodsService, IBarcodeService
    {
        public Task<FoodResponse?> FetchAsync(string input, double? grams = null)
        {
            if (input == "throw") throw new InvalidOperationException("private upstream details");
            if (input == "missing") return Task.FromResult<FoodResponse?>(new() { Foods = [] });
            var food = new FoodDto
            {
                HiddenName = "Test food", Brands = "Test", HiddenMacrosDto = MacrosDto.From(10, 2, 3, 70)
            };
            food.Scale(grams ?? 100);
            return Task.FromResult<FoodResponse?>(new() { Foods = [food] });
        }
    }
}
