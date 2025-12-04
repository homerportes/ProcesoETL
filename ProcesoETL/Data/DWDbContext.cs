using Microsoft.EntityFrameworkCore;
using ProcesoETL.Models.DataWarehouse;

namespace ProcesoETL.Data;

/// <summary>
/// DbContext for Data Warehouse (DWVentasDb)
/// </summary>
public class DWDbContext : DbContext
{
    public DWDbContext(DbContextOptions<DWDbContext> options) : base(options)
    {
    }

    // Dimension Tables
    public DbSet<DimCustomer> DimCustomers { get; set; }
    public DbSet<DimProduct> DimProducts { get; set; }

    // Fact Tables
    public DbSet<FactSale> FactSales { get; set; }

    // Metadata
    public DbSet<FuenteDato> FuenteDatos { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure DimCustomers
        modelBuilder.Entity<DimCustomer>(entity =>
        {
            entity.ToTable("DimCustomers", "Dimension");
            entity.HasKey(e => e.IdCustomerDW);
            entity.Property(e => e.IdCustomerDW).ValueGeneratedOnAdd();
            entity.HasIndex(e => e.CustomerID).IsUnique();
            entity.Property(e => e.CompanyName).HasMaxLength(200);
            entity.Property(e => e.ContactName).HasMaxLength(200);
            entity.Property(e => e.Country).HasMaxLength(100);
        });

        // Configure DimProducts
        modelBuilder.Entity<DimProduct>(entity =>
        {
            entity.ToTable("DimProducts", "Dimension");
            entity.HasKey(e => e.IdProductDW);
            entity.Property(e => e.IdProductDW).ValueGeneratedOnAdd();
            entity.HasIndex(e => e.ProductID).IsUnique();
            entity.Property(e => e.ProductName).HasMaxLength(200);
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Category).HasMaxLength(100);
        });

        // Configure FactSales
        modelBuilder.Entity<FactSale>(entity =>
        {
            entity.ToTable("FactSales", "Fact");
            entity.HasKey(e => e.IdSaleDW);
            entity.Property(e => e.IdSaleDW).ValueGeneratedOnAdd();
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Total).HasColumnType("decimal(18,2)");
        });

        // Configure FuenteDatos
        modelBuilder.Entity<FuenteDato>(entity =>
        {
            entity.ToTable("FuenteDatos", "Metadata");
            entity.HasKey(e => e.IdFuente);
            entity.Property(e => e.IdFuente).ValueGeneratedOnAdd();
            entity.Property(e => e.NombreFuente).HasMaxLength(200).IsRequired();
            entity.Property(e => e.FechaCarga).HasDefaultValueSql("GETDATE()");
        });
    }
}
