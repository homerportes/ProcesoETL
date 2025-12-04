using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProcesoETL.Core.Interfaces;
using Domain.Models;
using ProcesoETL.Infrastructure.Services;
using ProcesoETL.Data;
using ProcesoETL.Models.DataWarehouse;

namespace ProcesoETL.Infrastructure.Services;

/// <summary>
/// Service for loading data into the Data Warehouse using Entity Framework Core
/// Implements UPSERT for dimensions and INSERT for facts
/// </summary>
public class DataLoader : IDataLoader
{
    private readonly ILogger<DataLoader> _logger;
    private readonly DWDbContext _dwContext;

    public DataLoader(
        ILogger<DataLoader> logger,
        DWDbContext dwContext)
    {
        _logger = logger;
        _dwContext = dwContext;
    }

    public async Task LoadAsync<T>(IEnumerable<T> data) where T : class
    {
        var list = data.ToList();
        if (!list.Any())
        {
            _logger.LogInformation("No data to load for type {TypeName}", typeof(T).Name);
            return;
        }

        _logger.LogInformation("LoadAsync called with {Count} records of type {Type}", list.Count, typeof(T).Name);
        
        // Route to appropriate loader based on type
        if (typeof(T) == typeof(Customer))
        {
            await LoadCustomersAsync(list.Cast<Customer>());
        }
        else if (typeof(T) == typeof(Product))
        {
            await LoadProductsAsync(list.Cast<Product>());
        }
        else if (typeof(T) == typeof(SaleRecord))
        {
            await LoadFactSalesAsync(list.Cast<SaleRecord>());
        }
        else
        {
            _logger.LogWarning("Unknown type {Type} - skipping load", typeof(T).Name);
        }
    }

    public async Task LoadWithIdentityAsync<T>(IEnumerable<T> data, string tableName, string pkColumn) where T : class
    {
        await LoadAsync(data);
    }

    /// <summary>
    /// Load customers into Dimension.DimCustomers with UPSERT logic using EF Core
    /// </summary>
    private async Task LoadCustomersAsync(IEnumerable<Customer> customers)
    {
        try
        {
            // Use execution strategy to handle retries with transactions
            var strategy = _dwContext.Database.CreateExecutionStrategy();
            
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _dwContext.Database.BeginTransactionAsync();
                
                var count = 0;
                foreach (var customer in customers)
                {
                    // Check if customer already exists
                    var existing = await _dwContext.DimCustomers
                        .FirstOrDefaultAsync(c => c.CustomerID == customer.CustomerID);

                    var companyName = $"{customer.FirstName} {customer.LastName}";
                    var contactName = $"{customer.FirstName} {customer.LastName}";

                    if (existing != null)
                    {
                        // UPDATE
                        existing.CompanyName = companyName;
                        existing.ContactName = contactName;
                        existing.Country = customer.Country ?? "Unknown";
                        _dwContext.DimCustomers.Update(existing);
                    }
                    else
                    {
                        // INSERT
                        var newCustomer = new DimCustomer
                        {
                            CustomerID = customer.CustomerID,
                            CompanyName = companyName,
                            ContactName = contactName,
                            Country = customer.Country ?? "Unknown"
                        };
                        await _dwContext.DimCustomers.AddAsync(newCustomer);
                    }
                    count++;
                }

                await _dwContext.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Successfully loaded/updated {Count} customers into Dimension.DimCustomers", count);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading customers");
            throw;
        }
    }

    /// <summary>
    /// Load products into Dimension.DimProducts with UPSERT logic using EF Core
    /// </summary>
    private async Task LoadProductsAsync(IEnumerable<Product> products)
    {
        try
        {
            // Use execution strategy to handle retries with transactions
            var strategy = _dwContext.Database.CreateExecutionStrategy();
            
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _dwContext.Database.BeginTransactionAsync();
                
                var count = 0;
                foreach (var product in products)
                {
                    // Check if product already exists
                    var existing = await _dwContext.DimProducts
                        .FirstOrDefaultAsync(p => p.ProductID == product.ProductID);

                    if (existing != null)
                    {
                        // UPDATE
                        existing.ProductName = product.ProductName ?? "Unknown";
                        existing.UnitPrice = product.Price;
                        existing.Category = product.Category ?? "General";
                        _dwContext.DimProducts.Update(existing);
                    }
                    else
                    {
                        // INSERT
                        var newProduct = new DimProduct
                        {
                            ProductID = product.ProductID,
                            ProductName = product.ProductName ?? "Unknown",
                            UnitPrice = product.Price,
                            Category = product.Category ?? "General"
                        };
                        await _dwContext.DimProducts.AddAsync(newProduct);
                    }
                    count++;
                }

                await _dwContext.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Successfully loaded/updated {Count} products into Dimension.DimProducts", count);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading products");
            throw;
        }
    }

    /// <summary>
    /// Load sales facts into Fact.FactSales (ALWAYS INSERT, no upsert) using EF Core
    /// </summary>
    private async Task LoadFactSalesAsync(IEnumerable<SaleRecord> salesRecords)
    {
        try
        {
            // Use execution strategy to handle retries with transactions
            var strategy = _dwContext.Database.CreateExecutionStrategy();
            
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _dwContext.Database.BeginTransactionAsync();

                var factSales = salesRecords.Select(sr => new FactSale
                {
                    OrderID = sr.OrderID,
                    ProductID = sr.ProductID,
                    CustomerID = sr.CustomerID,
                    Quantity = sr.Quantity,
                    UnitPrice = sr.UnitPrice,
                    Total = sr.Total,
                    OrderDate = sr.OrderDate
                }).ToList();

                await _dwContext.FactSales.AddRangeAsync(factSales);
                await _dwContext.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Successfully inserted {Count} records into Fact.FactSales", factSales.Count);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading fact sales");
            throw;
        }
    }

    /// <summary>
    /// Insert metadata record into Metadata.FuenteDatos using EF Core
    /// </summary>
    public async Task InsertMetadataAsync(string sourceName)
    {
        try
        {
            var metadata = new FuenteDato
            {
                NombreFuente = sourceName,
                FechaCarga = DateTime.Now
            };

            await _dwContext.FuenteDatos.AddAsync(metadata);
            await _dwContext.SaveChangesAsync();

            _logger.LogInformation("Inserted metadata for source: {SourceName}", sourceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inserting metadata");
            throw;
        }
    }
}
