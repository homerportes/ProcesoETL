-- Data Warehouse Schema Creation Script for DWVentasDb
-- Run this before the ETL process

USE master;
GO

-- Create database if not exists
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'DWVentasDb')
BEGIN
    CREATE DATABASE DWVentasDb;
END
GO

USE DWVentasDb;
GO

-- Create schemas
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'Dimension')
BEGIN
    EXEC('CREATE SCHEMA Dimension');
END
GO

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'Fact')
BEGIN
    EXEC('CREATE SCHEMA Fact');
END
GO

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'Metadata')
BEGIN
    EXEC('CREATE SCHEMA Metadata');
END
GO

-- Drop tables if they exist (for clean run)
IF OBJECT_ID('Fact.FactSales', 'U') IS NOT NULL DROP TABLE Fact.FactSales;
IF OBJECT_ID('Dimension.DimProducts', 'U') IS NOT NULL DROP TABLE Dimension.DimProducts;
IF OBJECT_ID('Dimension.DimCustomers', 'U') IS NOT NULL DROP TABLE Dimension.DimCustomers;
IF OBJECT_ID('Metadata.FuenteDatos', 'U') IS NOT NULL DROP TABLE Metadata.FuenteDatos;
GO

-- Create Dimension Tables
CREATE TABLE Dimension.DimCustomers (
    IdCustomerDW INT IDENTITY(1,1) PRIMARY KEY,
    CustomerID INT NOT NULL,
    CompanyName NVARCHAR(200),
    ContactName NVARCHAR(200),
    Country NVARCHAR(100),
    CONSTRAINT UQ_DimCustomers_CustomerID UNIQUE (CustomerID)
);
GO

CREATE TABLE Dimension.DimProducts (
    IdProductDW INT IDENTITY(1,1) PRIMARY KEY,
    ProductID INT NOT NULL,
    ProductName NVARCHAR(200),
    UnitPrice DECIMAL(18,2),
    Category NVARCHAR(100),
    CONSTRAINT UQ_DimProducts_ProductID UNIQUE (ProductID)
);
GO

-- Create Fact Table
CREATE TABLE Fact.FactSales (
    IdSaleDW INT IDENTITY(1,1) PRIMARY KEY,
    OrderID INT NOT NULL,
    ProductID INT NOT NULL,
    CustomerID INT NOT NULL,
    Quantity INT NOT NULL,
    UnitPrice DECIMAL(18,2) NOT NULL,
    Total DECIMAL(18,2) NOT NULL,
    OrderDate DATETIME NOT NULL
);
GO

-- Create Metadata Table
CREATE TABLE Metadata.FuenteDatos (
    IdFuente INT IDENTITY(1,1) PRIMARY KEY,
    NombreFuente NVARCHAR(200) NOT NULL,
    FechaCarga DATETIME NOT NULL DEFAULT GETDATE()
);
GO

-- Create indexes for better query performance
CREATE INDEX IX_FactSales_OrderDate ON Fact.FactSales(OrderDate);
CREATE INDEX IX_FactSales_CustomerID ON Fact.FactSales(CustomerID);
CREATE INDEX IX_FactSales_ProductID ON Fact.FactSales(ProductID);
GO

PRINT 'Data Warehouse schema created successfully!';
GO
