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
    public DbSet<User> Users => Set<User>();
    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<FileMetadata> FileMetadatas => Set<FileMetadata>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // --- USER ---
        modelBuilder.Entity<User>(builder =>
        {
            builder.HasKey(u => u.Id);

            // Делаем Email уникальным индексом, чтобы не было дублей
            builder.HasIndex(u => u.Email).IsUnique();

            builder.Property(u => u.FullName).HasMaxLength(200).IsRequired();
            builder.Property(u => u.Email).HasMaxLength(150).IsRequired();
            builder.Property(u => u.Phone).HasMaxLength(20).IsRequired();

            // Пароль и соль — просто строки, но можно ограничить длину
            builder.Property(u => u.Password).IsRequired();
            builder.Property(u => u.Salt).IsRequired();
            builder.Property(u => u.Role).HasMaxLength(20).IsRequired();

            // Связь: Один пользователь - Много магазинов
            builder.HasMany(u => u.Shops)
                   .WithOne(s => s.Owner)
                   .HasForeignKey(s => s.OwnerId)
                   .OnDelete(DeleteBehavior.Restrict); // Чтобы при удалении юзера случайно не стерлись все магазины (безопаснее)
        });

        // --- SHOP ---
        modelBuilder.Entity<Shop>(builder =>
        {
            builder.HasKey(s => s.Id);
            builder.HasIndex(s => s.UrlAlias).IsUnique();

            // Связь с категориями (один магазин - много категорий)
            builder.HasMany(s => s.Categories)
                   .WithOne(c => c.Shop)
                   .HasForeignKey(c => c.ShopId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(s => s.Owner)
               .WithMany(u => u.Shops)
               .HasForeignKey(s => s.OwnerId);
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
            builder.Property(p => p.Tags).HasMaxLength(2000).HasDefaultValue(string.Empty);
            builder.Property(p => p.AttributesJson).HasColumnType("jsonb").HasDefaultValue("{}");

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
            builder.HasOne(o => o.User).WithMany().HasForeignKey(o => o.UserId);

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

        // --- CART ---
        modelBuilder.Entity<Cart>(builder =>
        {
            builder.HasKey(c => c.Id);

            // Один пользователь - одна корзина (1-к-1)
            builder.HasOne(c => c.User)
                   .WithMany()
                   .HasForeignKey(c => c.UserId);

            // Связь с айтемами
            builder.HasMany(c => c.Items)
                   .WithOne(ci => ci.Cart)
                   .HasForeignKey(ci => ci.CartId)
                   .OnDelete(DeleteBehavior.Cascade);
        });

        // --- CART ITEM ---
        modelBuilder.Entity<CartItem>(builder =>
        {
            builder.HasKey(ci => ci.Id);

            builder.HasOne(ci => ci.Product)
                   .WithMany()
                   .HasForeignKey(ci => ci.ProductId);
        });

        // --- REVIEWS ---
        modelBuilder.Entity<ProductReview>(builder =>
        {
            builder.HasKey(r => r.Id);

            // Ограничение: один пользователь может оставить только один отзыв на один товар
            builder.HasIndex(r => new { r.UserId, r.ProductId }).IsUnique();

            builder.HasOne(r => r.User)
                   .WithMany(u => u.Reviews)
                   .HasForeignKey(r => r.UserId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(r => r.Product)
                   .WithMany(p => p.Reviews)
                   .HasForeignKey(r => r.ProductId)
                   .OnDelete(DeleteBehavior.Cascade);
        });

        // --- FAVORITES ---
        modelBuilder.Entity<FavoriteProduct>(builder =>
        {
            builder.HasKey(f => f.Id);

            // Товар в избранном у юзера не может дублироваться
            builder.HasIndex(f => new { f.UserId, f.ProductId }).IsUnique();

            builder.HasOne(f => f.User)
                   .WithMany(u => u.Favorites)
                   .HasForeignKey(f => f.UserId)
                   .OnDelete(DeleteBehavior.Cascade);
        });

        // --- VIEWED ---
        modelBuilder.Entity<RecentlyViewedProduct>(builder =>
        {
            builder.HasKey(rv => rv.Id);

            // Индекс для быстрой сортировки по дате просмотра
            builder.HasIndex(rv => new { rv.UserId, rv.ViewedAtUtc });

            builder.HasOne(rv => rv.User)
                   .WithMany(u => u.RecentlyViewed)
                   .HasForeignKey(rv => rv.UserId)
                   .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FileMetadata>(builder =>
        {
            builder.HasKey(f => f.Id);
            builder.Property(f => f.Bucket).HasMaxLength(100).IsRequired();
            builder.Property(f => f.ObjectName).HasMaxLength(500).IsRequired();
            builder.Property(f => f.ContentType).HasMaxLength(100).IsRequired();
            builder.Property(f => f.OriginalFileName).HasMaxLength(255).IsRequired();
            builder.Property(f => f.CreatedAt).IsRequired();

            builder.HasOne(f => f.Product)
                .WithMany()
                .HasForeignKey(f => f.ProductId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(f => f.Shop)
                .WithMany()
                .HasForeignKey(f => f.ShopId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
