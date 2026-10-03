using Scale.io_API.Configuration;

var migrateOnly = args.Contains("--migrate", StringComparer.Ordinal);
var builder = WebApplication.CreateBuilder([.. args.Where(arg => arg != "--migrate")]);
builder.Configure();

await using var app = builder.Build();
await app.InitializeDatabaseAsync(migrateOnly);
if (migrateOnly) return;
app.Configure();
await app.RunAsync();

public partial class Program;
