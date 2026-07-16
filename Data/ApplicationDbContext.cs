using Microsoft.EntityFrameworkCore;

namespace sonata_api.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Models.Session> Sessions => Set<Models.Session>();
    public DbSet<Models.Message> Messages => Set<Models.Message>();
}