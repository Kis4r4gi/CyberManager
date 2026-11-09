using CyberManager.Shared;
using CyberManager.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace CyberManager.Server.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<DeviceRegistration> Devices => Set<DeviceRegistration>();

    public DbSet<Zone> Zones => Set<Zone>();
    public DbSet<Rate> Rates => Set<Rate>();
    public DbSet<TimePackage> TimePackages => Set<TimePackage>();
    public DbSet<Station> Stations => Set<Station>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Reservation> Reservations => Set<Reservation>();

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierPayment> SupplierPayments => Set<SupplierPayment>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerTransaction> CustomerTransactions => Set<CustomerTransaction>();

    public DbSet<CashSession> CashSessions => Set<CashSession>();
    public DbSet<CashMovement> CashMovements => Set<CashMovement>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();

    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    public DbSet<InvoiceSequence> InvoiceSequences => Set<InvoiceSequence>();
    public DbSet<Wallpaper> Wallpapers => Set<Wallpaper>();
    public DbSet<PromoVideo> PromoVideos => Set<PromoVideo>();
    public DbSet<WebFilterRule> WebFilterRules => Set<WebFilterRule>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<UserAccount>().HasIndex(x => x.Username).IsUnique();
        b.Entity<Setting>().HasKey(x => x.Key);
        b.Entity<Product>().HasIndex(x => x.Sku).IsUnique();
        b.Entity<Station>().HasIndex(x => x.Name).IsUnique();
        b.Entity<DeviceRegistration>().HasIndex(x => x.Token).IsUnique();
        b.Entity<Sale>().HasIndex(x => x.Number).IsUnique();
        b.Entity<Customer>().HasIndex(x => x.Name);

        b.Entity<Session>()
            .HasOne(s => s.Station)
            .WithMany(x => x.Sessions)
            .HasForeignKey(s => s.StationId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<Sale>()
            .HasOne(s => s.Session)
            .WithMany(x => x.Sales)
            .HasForeignKey(s => s.SessionId)
            .OnDelete(DeleteBehavior.SetNull);

        b.Entity<Sale>()
            .HasOne(s => s.Station)
            .WithMany()
            .HasForeignKey(s => s.StationId)
            .OnDelete(DeleteBehavior.SetNull);

        b.Entity<SaleItem>()
            .HasOne(i => i.Sale)
            .WithMany(s => s.Items)
            .HasForeignKey(i => i.SaleId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<PurchaseItem>()
            .HasOne(i => i.Purchase)
            .WithMany(p => p.Items)
            .HasForeignKey(i => i.PurchaseId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<SupplierPayment>()
            .HasOne(p => p.Supplier)
            .WithMany(s => s.Payments)
            .HasForeignKey(p => p.SupplierId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<CashMovement>()
            .HasOne(m => m.CashSession)
            .WithMany(c => c.Movements)
            .HasForeignKey(m => m.CashSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<StockMovement>().HasIndex(x => x.CreatedUtc);
        b.Entity<Sale>().HasIndex(x => x.CreatedUtc);
        b.Entity<Session>().HasIndex(x => x.StartedUtc);
        b.Entity<CashSession>().HasIndex(x => x.IsOpen);
        b.Entity<Wallpaper>().HasIndex(x => x.SortOrder);
        b.Entity<PromoVideo>().HasIndex(x => x.SortOrder);
        b.Entity<SupplierPayment>().HasIndex(x => x.DateUtc);
        b.Entity<WebFilterRule>().HasIndex(x => x.Pattern);
        b.Entity<WebFilterRule>().HasIndex(x => x.Kind);
    }
}
