using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProcesoETL.Core.Configuration;
using ProcesoETL.Core.Interfaces;
using System.Text.Json;

namespace ProcesoETL.Infrastructure.Services;

/// <summary>
/// Service for managing staging data storage
/// </summary>
public class StagingService : IStagingService
{
    private readonly ILogger<StagingService> _logger;
    private readonly string _stagingPath;

    public StagingService(
        ILogger<StagingService> logger,
        IOptions<ETLSettings> settings)
    {
        _logger = logger;
        _stagingPath = settings.Value.StagingPath;
        
        // Ensure staging directory exists
        if (!Directory.Exists(_stagingPath))
        {
            Directory.CreateDirectory(_stagingPath);
            _logger.LogInformation("Created staging directory: {StagingPath}", _stagingPath);
        }
    }

    public async Task SaveToStagingAsync<T>(IEnumerable<T> data, string sourceName)
    {
        var fileName = $"{sourceName}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
        var filePath = Path.Combine(_stagingPath, fileName);

        try
        {
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            
            await File.WriteAllTextAsync(filePath, json);
            
            _logger.LogInformation(
                "Saved staging data for {SourceName} to {FilePath}",
                sourceName,
                filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving staging data for {SourceName}", sourceName);
            throw;
        }
    }

    public async Task<IEnumerable<T>> LoadFromStagingAsync<T>(string sourceName)
    {
        try
        {
            var files = Directory.GetFiles(_stagingPath, $"{sourceName}_*.json")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTime)  // Sort by creation time, not alphabetically
                .ToList();

            if (!files.Any())
            {
                _logger.LogWarning("No staging files found for {SourceName}", sourceName);
                return Enumerable.Empty<T>();
            }

            var latestFile = files.First().FullName;
            var json = await File.ReadAllTextAsync(latestFile);
            var data = JsonSerializer.Deserialize<List<T>>(json) ?? new List<T>();

            _logger.LogInformation(
                "Loaded {RecordCount} records from staging file {FilePath}",
                data.Count,
                latestFile);

            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading staging data for {SourceName}", sourceName);
            throw;
        }
    }

    public async Task ClearStagingAsync(string sourceName)
    {
        try
        {
            var files = Directory.GetFiles(_stagingPath, $"{sourceName}_*.json");
            
            foreach (var file in files)
            {
                File.Delete(file);
            }

            _logger.LogInformation(
                "Cleared {FileCount} staging files for {SourceName}",
                files.Length,
                sourceName);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing staging data for {SourceName}", sourceName);
            throw;
        }
    }

    /// <summary>
    /// Processes and joins Orders with OrderDetails to create sales records for Data Warehouse
    /// Calculates Total = Quantity * UnitPrice from Product data
    /// </summary>
    public async Task<List<SaleRecord>> ProcessSalesDataAsync(
        List<Domain.Models.Order> orders,
        List<Domain.Models.OrderDetail> orderDetails,
        List<Domain.Models.Product> products)
    {
        try
        {
            _logger.LogInformation("Starting sales data processing (joining Orders + OrderDetails)");

            var productDict = products.ToDictionary(p => p.ProductID);
            var orderDict = orders.ToDictionary(o => o.OrderID);

            var salesRecords = new List<SaleRecord>();

            foreach (var detail in orderDetails)
            {
                if (!orderDict.TryGetValue(detail.OrderID, out var order))
                {
                    _logger.LogWarning("Order {OrderID} not found for OrderDetail", detail.OrderID);
                    continue;
                }

                if (!productDict.TryGetValue(detail.ProductID, out var product))
                {
                    _logger.LogWarning("Product {ProductID} not found for OrderDetail", detail.ProductID);
                    continue;
                }

                // Calculate Total = Quantity * UnitPrice
                var total = detail.Quantity * product.Price;

                salesRecords.Add(new SaleRecord
                {
                    OrderID = order.OrderID,
                    ProductID = product.ProductID,
                    CustomerID = order.CustomerID,
                    Quantity = detail.Quantity,
                    UnitPrice = product.Price,
                    Total = total,
                    OrderDate = order.OrderDate
                });
            }

            _logger.LogInformation("Processed {Count} sales records", salesRecords.Count);
            
            // Save to staging
            await SaveToStagingAsync(salesRecords, "SalesRecords");

            return salesRecords;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing sales data");
            throw;
        }
    }
}

/// <summary>
/// Represents a sales record ready for Data Warehouse Fact table loading
/// </summary>
public class SaleRecord
{
    public int OrderID { get; set; }
    public int ProductID { get; set; }
    public int CustomerID { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
    public DateTime OrderDate { get; set; }
}
