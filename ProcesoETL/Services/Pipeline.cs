using CsvHelper;
using CsvHelper.Configuration;
using Domain.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Linq;

public class Pipeline
{
    private readonly AppDbContext _context;

    public Pipeline(AppDbContext context)
    {
        _context = context;
    }
    private List<T> ReadCsv<T>(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fs);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HeaderValidated = null,
            MissingFieldFound = null,
            TrimOptions = TrimOptions.Trim
        });
        return csv.GetRecords<T>().ToList();
    }

    private void DetachAll()
    {
        var entries = _context.ChangeTracker.Entries().ToList();
        foreach (var e in entries) e.State = EntityState.Detached;
    }

    private bool IsColumnIdentity(string tableName, string columnName)
    {
        var sql = $@"
            SELECT CASE WHEN c.is_identity = 1 THEN 1 ELSE 0 END
            FROM sys.columns c
            JOIN sys.tables t ON c.object_id = t.object_id
            WHERE t.name = '{tableName}' AND c.name = '{columnName}'";
        var conn = _context.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var r = cmd.ExecuteScalar();
        return r != null && r != DBNull.Value && Convert.ToInt32(r) == 1;
    }

    private void InsertWithIdentityHandling<T>(IEnumerable<T> entities, string tableName, string pkColumn) where T : class
    {
        var list = entities?.ToList() ?? new List<T>();
        if (!list.Any()) return;

        try
        {
            _context.Set<T>().AddRange(list);
            _context.SaveChanges();
            DetachAll();
        }
        catch (DbUpdateException ex)
        {
            var inner = ex.InnerException?.Message ?? ex.Message;
            if (inner.IndexOf("identity", StringComparison.OrdinalIgnoreCase) >= 0
                || inner.IndexOf("Cannot insert explicit value for identity column", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var isIdentity = IsColumnIdentity(tableName, pkColumn);
                if (!isIdentity) throw;

                DetachAll();
                using var tx = _context.Database.BeginTransaction();
                try
                {
                    _context.Database.ExecuteSqlRaw($"SET IDENTITY_INSERT dbo.[{tableName}] ON;");
                    _context.Set<T>().AddRange(list);
                    _context.SaveChanges();
                    _context.Database.ExecuteSqlRaw($"SET IDENTITY_INSERT dbo.[{tableName}] OFF;");
                    tx.Commit();
                    DetachAll();
                }
                catch
                {
                    try { _context.Database.ExecuteSqlRaw($"SET IDENTITY_INSERT dbo.[{tableName}] OFF;"); } catch { }
                    tx.Rollback();
                    throw;
                }
            }
            else throw;
        }
    }

    public void Run()
    {
        string basePath = @"C:\Users\TUF\Downloads\Archivo CSV Análisis de Ventas-20250924\";
        var customersPath = Path.Combine(basePath, "customers.csv");
        var productsPath = Path.Combine(basePath, "products.csv");
        var ordersPath = Path.Combine(basePath, "orders.csv");
        var orderDetailsPath = Path.Combine(basePath, "order_details.csv");

        if (!File.Exists(customersPath) || !File.Exists(productsPath) || !File.Exists(ordersPath) || !File.Exists(orderDetailsPath))
        {
            Console.WriteLine("Falta uno o más CSV en la ruta indicada:");
            Console.WriteLine(customersPath);
            Console.WriteLine(productsPath);
            Console.WriteLine(ordersPath);
            Console.WriteLine(orderDetailsPath);
            return;
        }

        var customersRaw = ReadCsv<Customer>(customersPath);
        var productsRaw = ReadCsv<Product>(productsPath);
        var ordersRaw = ReadCsv<Order>(ordersPath);
        var orderDetailsRaw = ReadCsv<OrderDetail>(orderDetailsPath);

        var customers = customersRaw
            .Where(c => !string.IsNullOrWhiteSpace(c.FirstName) || !string.IsNullOrWhiteSpace(c.LastName))
            .GroupBy(c => c.CustomerID)
            .Select(g => g.First())
            .ToList();

        var products = productsRaw
            .Where(p => p.Price >= 0)
            .GroupBy(p => p.ProductID)
            .Select(g => g.First())
            .ToList();

        ordersRaw = ordersRaw
            .GroupBy(o => o.OrderID)
            .Select(g => g.First())
            .ToList();

        orderDetailsRaw = orderDetailsRaw
            .ToList();

        Console.WriteLine("[INFO] Recreating DB: EnsureDeleted + EnsureCreated");
        _context.Database.EnsureDeleted();
        _context.Database.EnsureCreated();

        bool customersIdentity = IsColumnIdentity("Customers", "CustomerID");
        bool productsIdentity = IsColumnIdentity("Products", "ProductID");
        bool ordersIdentity = IsColumnIdentity("Orders", "OrderID");
        bool orderDetailsIdentity = IsColumnIdentity("OrderDetails", "OrderDetailID");

        Console.WriteLine($"[INFO] Identity flags -> Customers: {customersIdentity}, Products: {productsIdentity}, Orders: {ordersIdentity}, OrderDetails: {orderDetailsIdentity}");


        List<Customer> customersToInsert;
        if (customersIdentity)
        {
            customersToInsert = customers.Select(c => new Customer
            {
                FirstName = c.FirstName,
                LastName = c.LastName,
                Email = c.Email,
                Phone = c.Phone,
                City = c.City,
                Country = c.Country
            }).ToList();
        }
        else
        {
            customersToInsert = customers;
        }

        InsertWithIdentityHandling(customersToInsert, "Customers", "CustomerID");

        var dbCustomers = _context.Customers.AsNoTracking().ToList();
        var customerMap = new Dictionary<int, int>();

        foreach (var orig in customers)
        {
            int mapped = 0;
            if (!string.IsNullOrWhiteSpace(orig.Email))
            {
                var found = dbCustomers.FirstOrDefault(c => string.Equals(c.Email?.Trim(), orig.Email?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (found != null) mapped = found.CustomerID;
            }
            if (mapped == 0)
            {
                var found = dbCustomers.FirstOrDefault(c =>
                    string.Equals((c.FirstName ?? "").Trim(), (orig.FirstName ?? "").Trim(), StringComparison.OrdinalIgnoreCase)
                    && string.Equals((c.LastName ?? "").Trim(), (orig.LastName ?? "").Trim(), StringComparison.OrdinalIgnoreCase)
                    && (string.IsNullOrWhiteSpace(orig.Phone) || string.Equals((c.Phone ?? "").Trim(), (orig.Phone ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
                );
                if (found != null) mapped = found.CustomerID;
            }
            if (mapped == 0 && !customersIdentity)
            {
                mapped = orig.CustomerID;
            }
            if (mapped != 0) customerMap[orig.CustomerID] = mapped;
        }

        List<Product> productsToInsert;
        if (productsIdentity)
        {
            productsToInsert = products.Select(p => new Product
            {
                ProductName = p.ProductName,
                Category = p.Category,
                Price = p.Price,
                Stock = p.Stock
            }).ToList();
        }
        else
        {
            productsToInsert = products;
        }

        InsertWithIdentityHandling(productsToInsert, "Products", "ProductID");

        var dbProducts = _context.Products.AsNoTracking().ToList();
        var productMap = new Dictionary<int, int>();
        foreach (var orig in products)
        {
            int mapped = 0;
            var found = dbProducts.FirstOrDefault(p => string.Equals((p.ProductName ?? "").Trim(), (orig.ProductName ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
            if (found != null) mapped = found.ProductID;
            if (mapped == 0 && !productsIdentity) mapped = orig.ProductID;
            if (mapped != 0) productMap[orig.ProductID] = mapped;
        }

        var ordersOrdered = ordersRaw.ToList();
        var originalOrderIdsInOrder = new List<int>();
        var ordersToInsert = new List<Order>();

        foreach (var o in ordersOrdered)
        {
            var mappedCustomerId = o.CustomerID;
            if (customerMap.TryGetValue(o.CustomerID, out var custMapped)) mappedCustomerId = custMapped;

            if (ordersIdentity)
            {
                ordersToInsert.Add(new Order
                {
                    CustomerID = mappedCustomerId,
                    OrderDate = o.OrderDate,
                    Status = o.Status,
                    Customer = null
                });
                originalOrderIdsInOrder.Add(o.OrderID);
            }
            else
            {
                ordersToInsert.Add(new Order
                {
                    OrderID = o.OrderID,
                    CustomerID = mappedCustomerId,
                    OrderDate = o.OrderDate,
                    Status = o.Status,
                    Customer = null
                });
                originalOrderIdsInOrder.Add(o.OrderID);
            }
        }

        InsertWithIdentityHandling(ordersToInsert, "Orders", "OrderID");

        var orderMap = new Dictionary<int, int>();
        if (ordersIdentity)
        {
            for (int i = 0; i < ordersToInsert.Count; i++)
            {
                var origId = originalOrderIdsInOrder[i];
                var dbId = ordersToInsert[i].OrderID;
                orderMap[origId] = dbId;
            }
        }
        else
        {
            foreach (var o in ordersOrdered)
                orderMap[o.OrderID] = o.OrderID;
        }

        var hasValidOrderDetailId = orderDetailsRaw.Any(od => od.OrderDetailID != 0);
        List<OrderDetail> orderDetailsToInsert = new();

        if (hasValidOrderDetailId)
        {
            var dedup = orderDetailsRaw.GroupBy(od => od.OrderDetailID).Select(g => g.First());
            foreach (var od in dedup)
            {
                if (!productMap.TryGetValue(od.ProductID, out var mappedProductId))
                {
                    mappedProductId = dbProducts.FirstOrDefault(p => string.Equals((p.ProductName ?? "").Trim(), (products.FirstOrDefault(x => x.ProductID == od.ProductID)?.ProductName ?? "").Trim(), StringComparison.OrdinalIgnoreCase))?.ProductID ?? 0;
                }
                if (!orderMap.TryGetValue(od.OrderID, out var mappedOrderId))
                {
                    mappedOrderId = od.OrderID;
                }

                if (mappedProductId == 0)
                {
                    Console.WriteLine($"[WARN] Skipping OrderDetail {od.OrderDetailID}: product {od.ProductID} not mapped.");
                    continue;
                }

                orderDetailsToInsert.Add(new OrderDetail
                {
                    OrderDetailID = od.OrderDetailID,
                    OrderID = mappedOrderId,
                    ProductID = mappedProductId,
                    Quantity = od.Quantity,
                    TotalPrice = od.Quantity * dbProducts.First(p => p.ProductID == mappedProductId).Price,
                    Order = null,
                    Product = null
                });
            }
        }
        else
        {
            var dedup = orderDetailsRaw.GroupBy(od => new { od.OrderID, od.ProductID }).Select(g => g.First()).ToList();
            int genId = 1;
            foreach (var od in dedup)
            {
                if (!productMap.TryGetValue(od.ProductID, out var mappedProductId))
                {
                    mappedProductId = dbProducts.FirstOrDefault(p => string.Equals((p.ProductName ?? "").Trim(), (products.FirstOrDefault(x => x.ProductID == od.ProductID)?.ProductName ?? "").Trim(), StringComparison.OrdinalIgnoreCase))?.ProductID ?? 0;
                }
                if (!orderMap.TryGetValue(od.OrderID, out var mappedOrderId))
                {
                    mappedOrderId = od.OrderID;
                }
                if (mappedProductId == 0)
                {
                    Console.WriteLine($"[WARN] Skipping OrderDetail (Order:{od.OrderID}, Product:{od.ProductID}) -> product not mapped.");
                    continue;
                }

                var odNew = new OrderDetail
                {
                    OrderDetailID = genId++,
                    OrderID = mappedOrderId,
                    ProductID = mappedProductId,
                    Quantity = od.Quantity,
                    TotalPrice = od.Quantity * dbProducts.First(p => p.ProductID == mappedProductId).Price,
                    Order = null,
                    Product = null
                };
                orderDetailsToInsert.Add(odNew);
            }
        }

        InsertWithIdentityHandling(orderDetailsToInsert, "OrderDetails", "OrderDetailID");

        Console.WriteLine("=== ETL SUMMARY ===");
        Console.WriteLine($"Customers read: {customersRaw.Count}, inserted distinct: {_context.Customers.Count()}");
        Console.WriteLine($"Products read: {productsRaw.Count}, inserted distinct: {_context.Products.Count()}");
        Console.WriteLine($"Orders read: {ordersRaw.Count}, inserted distinct: {_context.Orders.Count()}");
        Console.WriteLine($"OrderDetails read: {orderDetailsRaw.Count}, inserted distinct: {_context.OrderDetails.Count()}");
        Console.WriteLine("ETL finished.");
    }
}
