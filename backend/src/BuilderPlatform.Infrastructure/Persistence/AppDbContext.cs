using BuilderPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BuilderPlatform.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product>        Products         => Set<Product>();
    public DbSet<ChatMessage>    ChatMessages     => Set<ChatMessage>();
    public DbSet<ActivityEvent>  ActivityEvents   => Set<ActivityEvent>();
    public DbSet<Approval>       Approvals        => Set<Approval>();
    public DbSet<ProductMemory>  ProductMemories  => Set<ProductMemory>();
    public DbSet<Artifact>       Artifacts        => Set<Artifact>();
    public DbSet<ScaffoldEntry>  ScaffoldEntries  => Set<ScaffoldEntry>();
    public DbSet<ScaffoldChange> ScaffoldChanges  => Set<ScaffoldChange>();
    public DbSet<ProductModule>  ProductModules   => Set<ProductModule>();
    public DbSet<FileRevision>   FileRevisions    => Set<FileRevision>();
    public DbSet<ValidationRun>  ValidationRuns   => Set<ValidationRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Name).IsRequired().HasMaxLength(200);
            e.Property(p => p.Prompt).IsRequired();
            e.Property(p => p.Status).HasConversion<string>();
            e.Property(p => p.RuntimePhase).HasMaxLength(50).HasDefaultValue("idle");
            e.Property(p => p.ScaffoldStatus).HasMaxLength(20).HasDefaultValue("none");
            e.Property(p => p.ProjectPath).HasMaxLength(500);
            e.Property(p => p.PreviewStatus).HasMaxLength(20).HasDefaultValue("stopped");
            e.Property(p => p.PreviewUrl).HasMaxLength(200);
            e.Property(p => p.PreviewError).HasMaxLength(500);
            e.Property(p => p.RuntimeHealth).HasMaxLength(20).HasDefaultValue("healthy");
            e.HasMany(p => p.Messages).WithOne(m => m.Product).HasForeignKey(m => m.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.ActivityEvents).WithOne(a => a.Product).HasForeignKey(a => a.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Approvals).WithOne(a => a.Product).HasForeignKey(a => a.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Memory).WithOne(m => m.Product).HasForeignKey(m => m.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Artifacts).WithOne(a => a.Product).HasForeignKey(a => a.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.ScaffoldEntries).WithOne(s => s.Product).HasForeignKey(s => s.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Modules).WithOne(m => m.Product).HasForeignKey(m => m.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatMessage>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.Content).IsRequired();
            e.Property(m => m.Role).HasConversion<string>();
        });

        modelBuilder.Entity<ActivityEvent>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Title).IsRequired().HasMaxLength(300);
            e.Property(a => a.EventType).HasConversion<string>();
            e.HasOne(a => a.Artifact).WithMany().HasForeignKey(a => a.ArtifactId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Approval>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Title).IsRequired().HasMaxLength(300);
            e.Property(a => a.Status).HasConversion<string>();
            e.HasOne(a => a.Artifact).WithMany().HasForeignKey(a => a.ArtifactId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ProductMemory>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.Key).IsRequired().HasMaxLength(100);
            e.HasIndex(m => new { m.ProductId, m.Key });
        });

        modelBuilder.Entity<Artifact>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Type).IsRequired().HasMaxLength(50);
            e.Property(a => a.Title).IsRequired().HasMaxLength(300);
            e.Property(a => a.Status).HasConversion<string>();
            e.HasIndex(a => new { a.ProductId, a.Type, a.Version });
        });

        modelBuilder.Entity<ScaffoldEntry>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.RelativePath).IsRequired().HasMaxLength(500);
            e.Property(s => s.EntryType).HasMaxLength(10).HasDefaultValue("file");
            e.Property(s => s.Language).HasMaxLength(30);
            e.HasIndex(s => s.ProductId);
        });

        modelBuilder.Entity<ScaffoldChange>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.ChangeType).IsRequired().HasMaxLength(20).HasDefaultValue("created");
            e.Property(s => s.TargetPath).IsRequired().HasMaxLength(600);
            e.Property(s => s.ModuleLabel).IsRequired().HasMaxLength(100);
            e.Property(s => s.Layer).HasMaxLength(20).HasDefaultValue("backend");
            e.HasOne(s => s.Product).WithMany(p => p.ScaffoldChanges).HasForeignKey(s => s.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(s => s.ProductId);
        });

        modelBuilder.Entity<ProductModule>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.ModuleName).IsRequired().HasMaxLength(100);
            e.Property(m => m.EntityName).HasMaxLength(100);
            e.Property(m => m.RoutePath).HasMaxLength(200);
            e.Property(m => m.ControllerName).HasMaxLength(100);
            e.Property(m => m.Layer).HasMaxLength(20).HasDefaultValue("full-stack");
            e.Property(m => m.Source).HasMaxLength(20).HasDefaultValue("scaffold");
            e.HasIndex(m => m.ProductId);
        });

        modelBuilder.Entity<FileRevision>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.RelativePath).IsRequired().HasMaxLength(600);
            e.Property(r => r.PatchType).IsRequired().HasMaxLength(50);
            e.Property(r => r.Reason).IsRequired().HasMaxLength(300);
            e.HasOne(r => r.Product).WithMany(p => p.FileRevisions).HasForeignKey(r => r.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(r => r.ProductId);
        });

        modelBuilder.Entity<ValidationRun>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Status).IsRequired().HasMaxLength(20).HasDefaultValue("running");
            e.Property(r => r.Logs).HasMaxLength(3000);
            e.Property(r => r.Errors).HasMaxLength(2000);
            e.HasOne(r => r.Product).WithMany(p => p.ValidationRuns).HasForeignKey(r => r.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(r => r.ProductId);
        });
    }
}
