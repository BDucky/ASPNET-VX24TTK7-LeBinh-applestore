using AppleStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AppleStore.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<AttributeDefinition> Attributes => Set<AttributeDefinition>();
    public DbSet<OptionType> OptionTypes => Set<OptionType>();
    public DbSet<User> Users => Set<User>();
    public DbSet<CategoryAttribute> CategoryAttributes => Set<CategoryAttribute>();
    public DbSet<OptionValue> OptionValues => Set<OptionValue>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<UserToken> UserTokens => Set<UserToken>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CompareList> CompareLists => Set<CompareList>();
    public DbSet<ProductAttributeValue> ProductAttributeValues => Set<ProductAttributeValue>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<CompareItem> CompareItems => Set<CompareItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<VariantOption> VariantOptions => Set<VariantOption>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<ReviewMedia> ReviewMedia => Set<ReviewMedia>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
