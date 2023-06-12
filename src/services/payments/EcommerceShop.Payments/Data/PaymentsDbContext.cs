using Microsoft.EntityFrameworkCore;
using EcommerceShop.BuildingBlocks.Messaging;
using EcommerceShop.Payments.Domain;

namespace EcommerceShop.Payments.Data;

public sealed class PaymentsDbContext : DbContext, IOutboxDbContext, IInboxDbContext
{
    public PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : base(options) { }

    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<CancelledOrder> CancelledOrders => Set<CancelledOrder>();
    public DbSet<ReconciliationRun> ReconciliationRuns => Set<ReconciliationRun>();
    public DbSet<ReconciliationIssue> ReconciliationIssues => Set<ReconciliationIssue>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>(b =>
        {
            b.ToTable("payments");
            b.HasKey(p => p.Id);
            b.Property(p => p.Id).ValueGeneratedNever();
            b.HasIndex(p => p.OrderId).IsUnique();
            b.HasIndex(p => p.IdempotencyKey).IsUnique();
            b.Property(p => p.Email).HasMaxLength(256);
            b.Property(p => p.Amount).HasPrecision(12, 2);
            b.Property(p => p.Currency).HasMaxLength(3);
            b.Property(p => p.Provider).HasMaxLength(20);
            b.Property(p => p.ProviderReference).HasMaxLength(100);
            b.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(p => p.FailureReason).HasMaxLength(500);
            b.Property(p => p.IdempotencyKey).HasMaxLength(100);
            b.Property(p => p.RefundReference).HasMaxLength(100);
            b.Property(p => p.RefundReason).HasMaxLength(500);
            b.HasIndex(p => p.CreatedAt);
        });

        modelBuilder.Entity<CancelledOrder>(b =>
        {
            b.ToTable("cancelled_orders");
            b.HasKey(c => c.OrderId);
            b.Property(c => c.OrderId).ValueGeneratedNever();
            b.Property(c => c.Reason).HasMaxLength(500);
        });

        modelBuilder.Entity<ReconciliationRun>(b =>
        {
            b.ToTable("reconciliation_runs");
            b.HasKey(r => r.Id);
            b.Property(r => r.Id).ValueGeneratedNever();
            b.Property(r => r.Trigger).HasMaxLength(20);
            b.HasIndex(r => r.StartedAt);
            b.HasMany(r => r.Issues).WithOne().HasForeignKey(i => i.RunId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(r => r.Issues).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<ReconciliationIssue>(b =>
        {
            b.ToTable("reconciliation_issues");
            b.HasKey(i => i.Id);
            b.Property(i => i.Id).ValueGeneratedNever();
            b.Property(i => i.Kind).HasConversion<string>().HasMaxLength(40);
            b.Property(i => i.Resolution).HasConversion<string>().HasMaxLength(20);
            b.Property(i => i.Detail).HasMaxLength(500);
            b.HasIndex(i => i.OrderId);
        });

        modelBuilder.ConfigureOutbox().ConfigureInbox();
    }
}

public sealed class DesignTimeDbContextFactory : Microsoft.EntityFrameworkCore.Design.IDesignTimeDbContextFactory<PaymentsDbContext>
{
    public PaymentsDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseNpgsql("Host=localhost;Database=payments_db;Username=ecommerceshop;Password=ecommerceshop")
            .Options);
}
