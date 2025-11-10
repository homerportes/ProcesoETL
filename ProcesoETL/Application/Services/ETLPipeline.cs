using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProcesoETL.Core.Configuration;
using ProcesoETL.Core.Interfaces;
using ProcesoETL.Infrastructure.Extractors;
using Domain.Models;
using System.Diagnostics;

namespace ProcesoETL.Application.Services;

/// <summary>
/// Main ETL pipeline orchestrator
/// </summary>
public class ETLPipeline
{
    private readonly ILogger<ETLPipeline> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IStagingService _stagingService;
    private readonly IDataLoader _dataLoader;
    private readonly DataSourceSettings _dataSourceSettings;
    private readonly ETLSettings _etlSettings;

    public ETLPipeline(
        ILogger<ETLPipeline> logger,
        IServiceProvider serviceProvider,
        IStagingService stagingService,
        IDataLoader dataLoader,
        IOptions<DataSourceSettings> dataSourceSettings,
        IOptions<ETLSettings> etlSettings)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _stagingService = stagingService;
        _dataLoader = dataLoader;
        _dataSourceSettings = dataSourceSettings.Value;
        _etlSettings = etlSettings.Value;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting ETL Pipeline execution");

        try
        {
            // Phase 1: Extract data from all sources
            await ExtractPhaseAsync(cancellationToken);

            // Phase 2: Transform data (currently minimal transformation)
            await TransformPhaseAsync(cancellationToken);

            // Phase 3: Load data into analytics database
            await LoadPhaseAsync(cancellationToken);

            _logger.LogInformation("ETL Pipeline completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ETL Pipeline failed");
            throw;
        }
    }

    private async Task ExtractPhaseAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("========== EXTRACT PHASE ==========");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (_etlSettings.EnableParallelProcessing)
            {
                _logger.LogInformation("Running extraction in parallel mode");
                
                var extractionTasks = new List<Task>
                {
                    ExtractCsvDataAsync(cancellationToken),
                    ExtractDatabaseDataAsync(cancellationToken),
                    ExtractApiDataAsync(cancellationToken)
                };

                await Task.WhenAll(extractionTasks);
            }
            else
            {
                _logger.LogInformation("Running extraction in sequential mode");
                
                await ExtractCsvDataAsync(cancellationToken);
                await ExtractDatabaseDataAsync(cancellationToken);
                await ExtractApiDataAsync(cancellationToken);
            }

            stopwatch.Stop();
            _logger.LogInformation(
                "Extract phase completed in {ElapsedSeconds:F2}s",
                stopwatch.Elapsed.TotalSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in extract phase");
            throw;
        }
    }

    private async Task ExtractCsvDataAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("--- Extracting CSV Data ---");

        try
        {
            var basePath = _dataSourceSettings.CsvPath;
            var csvFiles = _dataSourceSettings.CsvFiles;

            // Extract customers
            var customersExtractor = CreateCsvExtractor<Customer>(
                Path.Combine(basePath, csvFiles.Customers),
                "CsvCustomers");
            var customers = await customersExtractor.ExtractAsync();
            await _stagingService.SaveToStagingAsync(customers, "Customers");

            // Extract products
            var productsExtractor = CreateCsvExtractor<Product>(
                Path.Combine(basePath, csvFiles.Products),
                "CsvProducts");
            var products = await productsExtractor.ExtractAsync();
            await _stagingService.SaveToStagingAsync(products, "Products");

            // Extract orders
            var ordersExtractor = CreateCsvExtractor<Order>(
                Path.Combine(basePath, csvFiles.Orders),
                "CsvOrders");
            var orders = await ordersExtractor.ExtractAsync();
            await _stagingService.SaveToStagingAsync(orders, "Orders");

            // Extract order details
            var orderDetailsExtractor = CreateCsvExtractor<OrderDetail>(
                Path.Combine(basePath, csvFiles.OrderDetails),
                "CsvOrderDetails");
            var orderDetails = await orderDetailsExtractor.ExtractAsync();
            await _stagingService.SaveToStagingAsync(orderDetails, "OrderDetails");

            _logger.LogInformation("CSV extraction completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting CSV data");
            // Don't throw - continue with other extractions
        }
    }

    private async Task ExtractDatabaseDataAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("--- Extracting Database Data ---");

        try
        {
            // Example: Extract additional data from source database
            // This is a placeholder - you would implement actual database extraction here
            // based on your specific requirements
            
            _logger.LogInformation("Database extraction completed (placeholder)");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting database data");
            // Don't throw - continue with other extractions
        }
    }

    private async Task ExtractApiDataAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("--- Extracting API Data ---");

        try
        {
            // Example: Extract data from REST API
            // This is a placeholder - you would implement actual API extraction here
            // based on your specific requirements
            
            _logger.LogInformation("API extraction completed (placeholder)");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting API data");
            // Don't throw - continue with other extractions
        }
    }

    private async Task TransformPhaseAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("========== TRANSFORM PHASE ==========");
        
        // Load data from staging
        var customers = (await _stagingService.LoadFromStagingAsync<Customer>("Customers")).ToList();
        var products = (await _stagingService.LoadFromStagingAsync<Product>("Products")).ToList();
        var orders = (await _stagingService.LoadFromStagingAsync<Order>("Orders")).ToList();
        var orderDetails = (await _stagingService.LoadFromStagingAsync<OrderDetail>("OrderDetails")).ToList();

        // Apply transformations
        var transformedCustomers = customers
            .Where(c => !string.IsNullOrWhiteSpace(c.FirstName) || !string.IsNullOrWhiteSpace(c.LastName))
            .GroupBy(c => c.CustomerID)
            .Select(g => g.First())
            .ToList();

        var transformedProducts = products
            .Where(p => p.Price >= 0)
            .GroupBy(p => p.ProductID)
            .Select(g => g.First())
            .ToList();

        var transformedOrders = orders
            .GroupBy(o => o.OrderID)
            .Select(g => g.First())
            .ToList();

        // Save transformed data back to staging
        await _stagingService.SaveToStagingAsync(transformedCustomers, "Customers_Transformed");
        await _stagingService.SaveToStagingAsync(transformedProducts, "Products_Transformed");
        await _stagingService.SaveToStagingAsync(transformedOrders, "Orders_Transformed");
        await _stagingService.SaveToStagingAsync(orderDetails, "OrderDetails_Transformed");

        _logger.LogInformation(
            "Transform phase completed: {CustomerCount} customers, {ProductCount} products, {OrderCount} orders",
            transformedCustomers.Count,
            transformedProducts.Count,
            transformedOrders.Count);
    }

    private async Task LoadPhaseAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("========== LOAD PHASE ==========");

        try
        {
            // Load transformed data from staging
            var customers = await _stagingService.LoadFromStagingAsync<Customer>("Customers_Transformed");
            var products = await _stagingService.LoadFromStagingAsync<Product>("Products_Transformed");
            var orders = await _stagingService.LoadFromStagingAsync<Order>("Orders_Transformed");
            var orderDetails = await _stagingService.LoadFromStagingAsync<OrderDetail>("OrderDetails_Transformed");

            // Load into database
            await _dataLoader.LoadWithIdentityAsync(customers, "Customers", "CustomerID");
            await _dataLoader.LoadWithIdentityAsync(products, "Products", "ProductID");
            await _dataLoader.LoadWithIdentityAsync(orders, "Orders", "OrderID");
            await _dataLoader.LoadWithIdentityAsync(orderDetails, "OrderDetails", "OrderDetailID");

            _logger.LogInformation("Load phase completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in load phase");
            throw;
        }
    }

    private CsvExtractor<T> CreateCsvExtractor<T>(string filePath, string name)
    {
        var logger = _serviceProvider.GetRequiredService<ILogger<CsvExtractor<T>>>();
        return new CsvExtractor<T>(logger, filePath, name);
    }
}
