using Microsoft.EntityFrameworkCore;
using Sonata.Server.Models;

namespace Sonata.Server.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Message> Messages => Set<Message>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Message>()
            .HasOne(message => message.Session)
            .WithMany(session => session.Messages)
            .HasForeignKey(message => message.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
        
        modelBuilder.Entity<Message>().HasIndex(message => new
        {
            message.SessionId,
            message.Sequence
        }).IsUnique();
        
        modelBuilder.Entity<Message>().ToTable("messages", table => 
            table.HasCheckConstraint("CK_messages_sequence_positive", "sequence > 0"));
    }
}
