using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Contexts;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
}