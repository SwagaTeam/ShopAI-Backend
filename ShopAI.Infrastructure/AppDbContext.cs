using Domain.Entities;
using Domain.ValueObjects;

namespace ShopAI.Infrastructure;

using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Shop> Shops => Set<Shop>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // --- SHOP ---
    modelBuilder.Entity<Shop>(builder =>
    {
        builder.HasKey(s => s.Id);
        builder.HasIndex(s => s.UrlAlias).IsUnique();
        builder.OwnsOne(s => s.Theme);

        // Связь с категориями (один магазин - много категорий)
        builder.HasMany(s => s.Categories)
               .WithOne(c => c.Shop)
               .HasForeignKey(c => c.ShopId)
               .OnDelete(DeleteBehavior.Cascade);
    });

    // --- CATEGORY ---
    modelBuilder.Entity<Category>(builder =>
    {
        builder.HasKey(c => c.Id);
        
        // Связь с товарами
        builder.HasMany(c => c.Products)
               .WithOne(p => p.Category)
               .HasForeignKey(p => p.CategoryId);
    });

    // --- PRODUCT ---
    modelBuilder.Entity<Product>(builder =>
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Price).HasPrecision(18, 2);

        // Связь с магазином
        builder.HasOne(p => p.Shop)
               .WithMany(s => s.Products)
               .HasForeignKey(p => p.ShopId);

        // Связь с брендом (опционально)
        builder.HasOne(p => p.Brand)
               .WithMany() // У бренда может не быть списка товаров, если нам это не нужно
               .HasForeignKey(p => p.BrandId)
               .OnDelete(DeleteBehavior.SetNull);
    });

    // --- ORDER ---
    modelBuilder.Entity<Order>(builder =>
    {
        builder.HasKey(o => o.Id);
        builder.OwnsOne(o => o.Customer);

        // Настраиваем OrderItems как коллекцию сущностей
        builder.HasMany(o => o.Items)
               .WithOne() // У OrderItem может не быть свойства Order, если мы работаем через корень
               .HasForeignKey("OrderId") // Теневой (shadow) ключ в БД
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(o => o.Shop)
               .WithMany()
               .HasForeignKey(o => o.ShopId);
    });

    // --- ORDER ITEM ---
    modelBuilder.Entity<OrderItem>(builder =>
    {
        builder.HasKey(oi => oi.Id);
        builder.Property(oi => oi.PriceAtPurchase).HasPrecision(18, 2);

        // Связь с продуктом, чтобы знать, что купили
        builder.HasOne(oi => oi.Product)
               .WithMany()
               .HasForeignKey(oi => oi.ProductId);
    });
}
}