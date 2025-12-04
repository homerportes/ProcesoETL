using System;

namespace ProcesoETL.Models.DataWarehouse;

/// <summary>
/// Dimension table for Customers
/// </summary>
public class DimCustomer
{
    public int IdCustomerDW { get; set; }
    public int CustomerID { get; set; }
    public string? CompanyName { get; set; }
    public string? ContactName { get; set; }
    public string? Country { get; set; }
}

/// <summary>
/// Dimension table for Products
/// </summary>
public class DimProduct
{
    public int IdProductDW { get; set; }
    public int ProductID { get; set; }
    public string? ProductName { get; set; }
    public decimal UnitPrice { get; set; }
    public string? Category { get; set; }
}

/// <summary>
/// Fact table for Sales
/// </summary>
public class FactSale
{
    public int IdSaleDW { get; set; }
    public int OrderID { get; set; }
    public int ProductID { get; set; }
    public int CustomerID { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
    public DateTime OrderDate { get; set; }
}

/// <summary>
/// Metadata table for tracking ETL loads
/// </summary>
public class FuenteDato
{
    public int IdFuente { get; set; }
    public string NombreFuente { get; set; } = string.Empty;
    public DateTime FechaCarga { get; set; }
}
