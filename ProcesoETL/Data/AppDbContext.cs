using Domain.Models;
using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderDetail> OrderDetails { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer("Server=HOMER\\SQLEXPRESS;Database=CustomerOrdersDB;Trusted_Connection=true;TrustServerCertificate=true;");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>()
     .Property(c => c.CustomerID)
     .ValueGeneratedNever();

        modelBuilder.Entity<Product>()
            .Property(p => p.ProductID)
            .ValueGeneratedNever();

        modelBuilder.Entity<Order>()
            .Property(o => o.OrderID)
            .ValueGeneratedNever();

        modelBuilder.Entity<OrderDetail>()
            .Property(od => od.OrderDetailID)
            .ValueGeneratedNever();

    }
}
