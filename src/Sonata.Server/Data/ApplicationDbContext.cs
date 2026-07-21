using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Sonata.Server.Identity;
using Sonata.Server.Models;

namespace Sonata.Server.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Movement> Movements => Set<Movement>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<SourceNote> SourceNotes => Set<SourceNote>();
    public DbSet<Memory> Memories => Set<Memory>();
    public DbSet<MemoryUse> MemoryUses => Set<MemoryUse>();
    public DbSet<RefreshTokenRecord> RefreshTokenRecords => Set<RefreshTokenRecord>();

      protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Conversation>()
            .HasOne(conversation => conversation.Movement)
            .WithMany(movement => movement.Conversations)
            .HasForeignKey(conversation => conversation.MovementId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Message>()
            .HasOne(message => message.Conversation)
            .WithMany(conversation => conversation.Messages)
            .HasForeignKey(message => message.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Message>().HasIndex(message => new
        {
            message.ConversationId,
            message.Sequence
        }).IsUnique();

        modelBuilder.Entity<Message>().ToTable("messages", table =>
            table.HasCheckConstraint("CK_messages_sequence_positive", "sequence > 0"));

        modelBuilder.Entity<SourceNote>()
            .HasOne(sourceNote => sourceNote.Message)
            .WithMany()
            .HasForeignKey(sourceNote => sourceNote.MessageId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Memory>()
            .HasOne(memory => memory.Movement)
            .WithMany(movement => movement.Memories)
            .HasForeignKey(memory => memory.MovementId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Memory>()
            .HasOne(memory => memory.SourceNote)
            .WithOne(sourceNote => sourceNote.Memory)
            .HasForeignKey<Memory>(memory => memory.SourceNoteId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Memory>()
            .Property(memory => memory.Type)
            .HasConversion<string>()
            .HasMaxLength(50);

        modelBuilder.Entity<Memory>()
            .Property(memory => memory.LifecycleState)
            .HasConversion<string>()
            .HasMaxLength(50);

        modelBuilder.Entity<Memory>().HasIndex(memory => new
        {
            memory.MovementId,
            memory.LifecycleState,
            memory.CreatedAt
        });

        modelBuilder.Entity<MemoryUse>()
            .HasOne(memoryUse => memoryUse.Memory)
            .WithMany(memory => memory.Uses)
            .HasForeignKey(memoryUse => memoryUse.MemoryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MemoryUse>()
            .HasOne(memoryUse => memoryUse.ResponseMessage)
            .WithMany()
            .HasForeignKey(memoryUse => memoryUse.ResponseMessageId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MemoryUse>().HasIndex(memoryUse => new
        {
            memoryUse.ResponseMessageId,
            memoryUse.MemoryId
        }).IsUnique();

        modelBuilder.Entity<MemoryUse>().ToTable("memory_uses", table =>
            table.HasCheckConstraint("CK_memory_uses_rank_positive", "rank > 0"));
        
        modelBuilder.Entity<Movement>()
            .HasIndex(movement => new
            {
                movement.UserId,
                movement.StartedAt
            });

        modelBuilder.Entity<Movement>()
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(movement => movement.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Conversation>()
            .HasIndex(conversation => new
            {
                conversation.UserId,
                conversation.CreatedAt
            });

        modelBuilder.Entity<Conversation>()
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(conversation => conversation.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Memory>()
            .HasIndex(memory => new
            {
                memory.UserId,
                memory.MovementId,
                memory.LifecycleState
            });

        modelBuilder.Entity<Memory>()
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(memory => memory.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RefreshTokenRecord>()
            .HasIndex(record => record.TokenHash)
            .IsUnique();

        modelBuilder.Entity<RefreshTokenRecord>()
            .HasIndex(record => new
            {
                record.UserId,
                record.FamilyId
            });

        modelBuilder.Entity<RefreshTokenRecord>()
            .HasOne(record => record.User)
            .WithMany()
            .HasForeignKey(record => record.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
