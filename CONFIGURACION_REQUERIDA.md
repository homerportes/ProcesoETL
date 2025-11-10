# ✅ Lista de Verificación - Configuración Necesaria para Ejecutar el ETL

## 📋 REQUISITOS PREVIOS

### 1. Software Instalado
- ✅ .NET 9 SDK (ya lo tienes - el proyecto compila)
- ✅ SQL Server (HOMER\SQLEXPRESS está configurado)
- ⚠️ Archivos CSV en la ubicación correcta

---

## 🔧 CONFIGURACIONES OBLIGATORIAS

### 1️⃣ **Verificar/Actualizar Ruta de Archivos CSV**

📍 **Ubicación actual configurada:**
```
C:\Users\TUF\Downloads\Archivo CSV Análisis de Ventas-20250924\
```

**¿Qué hacer?**
1. Verifica que esta carpeta existe
2. Verifica que contiene estos archivos:
   - `customers.csv`
   - `products.csv`
   - `orders.csv`
   - `order_details.csv`

**Si la ruta es diferente:**
Edita `appsettings.json` línea 29:
```json
"CsvPath": "C:\\TU\\RUTA\\CORRECTA\\",
```

---

### 2️⃣ **Verificar Conexión a SQL Server**

📍 **Servidor configurado:**
```
Server: HOMER\SQLEXPRESS
Database: AnalyticsDB (se crea automáticamente)
```

**¿Qué hacer?**
Verifica que SQL Server está corriendo:

```powershell
# Verificar servicio SQL Server
Get-Service | Where-Object {$_.Name -like "*SQL*"}
```

**Si usas otro servidor/instancia:**
Edita `appsettings.json` líneas 24-25:
```json
"AnalyticsDb": "Server=TU_SERVIDOR;Database=AnalyticsDB;Trusted_Connection=true;TrustServerCertificate=true;"
```

---

### 3️⃣ **Crear Carpeta de Staging (Opcional - se crea automáticamente)**

La carpeta `staging` se crea automáticamente en:
```
C:\proyectos\ProcesoETL\ProcesoETL\staging\
```

Si quieres cambiar la ubicación, edita `appsettings.json` línea 42:
```json
"StagingPath": "C:\\tu\\ruta\\staging"
```

---

## ⚡ CONFIGURACIONES OPCIONALES

### 4️⃣ **API REST (Opcional)**

Si vas a usar el ApiExtractor, configura:

```json
"ApiBaseUrl": "https://tu-api-real.com",
"ApiKey": "tu-api-key-real"
```

**Nota:** Por defecto está configurado con placeholders. El ApiExtractor no fallará el proceso si no puede conectarse.

---

### 5️⃣ **Intervalo de Ejecución**

El Worker Service ejecuta el ETL cada **60 minutos** por defecto.

Para cambiar el intervalo, edita `appsettings.json` línea 38:
```json
"RunIntervalMinutes": 30,  // Cambia a los minutos que prefieras
```

**Nota:** La primera ejecución ocurre inmediatamente al iniciar.

---

### 6️⃣ **Procesamiento Paralelo**

Por defecto está **ACTIVADO** para mejor rendimiento.

Si tienes problemas, puedes desactivarlo:
```json
"EnableParallelProcessing": false,
```

---

## 🚀 PASOS PARA EJECUTAR

### Opción 1: Ejecución Simple (Desarrollo)

```powershell
cd c:\proyectos\ProcesoETL\ProcesoETL
dotnet run
```

### Opción 2: Ver Logs Detallados

```powershell
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run
```

### Opción 3: Ejecutar en Modo Release

```powershell
dotnet run --configuration Release
```

---

## 📊 VERIFICACIÓN POST-EJECUCIÓN

### ✅ Verificar que funcionó:

1. **Logs en Consola:**
   ```
   [INF] Starting ETL Worker Service
   [INF] Starting ETL Pipeline execution
   [INF] Successfully extracted X records from customers.csv in XXms
   ```

2. **Archivos de Log:**
   ```
   c:\proyectos\ProcesoETL\ProcesoETL\logs\etl-20251109.log
   ```

3. **Carpeta Staging:**
   ```
   c:\proyectos\ProcesoETL\ProcesoETL\staging\
   - Customers_20251109_HHMMSS.json
   - Products_20251109_HHMMSS.json
   - Orders_20251109_HHMMSS.json
   - OrderDetails_20251109_HHMMSS.json
   ```

4. **Base de Datos:**
   ```sql
   USE AnalyticsDB;
   SELECT COUNT(*) FROM Customers;
   SELECT COUNT(*) FROM Products;
   SELECT COUNT(*) FROM Orders;
   SELECT COUNT(*) FROM OrderDetails;
   ```

---

## ⚠️ SOLUCIÓN DE PROBLEMAS COMUNES

### Error: "Cannot open database 'AnalyticsDB'"

**Solución:**
```sql
-- Crear manualmente la base de datos
CREATE DATABASE AnalyticsDB;
```

O el código lo creará automáticamente en la primera ejecución.

---

### Error: "CSV file not found"

**Solución:**
1. Verifica la ruta en `appsettings.json`
2. Verifica que los archivos existen
3. Verifica permisos de lectura en la carpeta

**Verificación rápida:**
```powershell
Test-Path "C:\Users\TUF\Downloads\Archivo CSV Análisis de Ventas-20250924\customers.csv"
```

---

### Error: "Connection timeout" / "SQL Server does not exist"

**Solución:**
1. Verifica que SQL Server está corriendo
2. Verifica el nombre de la instancia (puede ser `localhost\SQLEXPRESS` o solo `localhost`)
3. Prueba la conexión:

```powershell
sqlcmd -S HOMER\SQLEXPRESS -E -Q "SELECT @@VERSION"
```

---

### Warning: "API request failed"

**Esto es NORMAL** si no tienes una API real configurada. El proceso continuará con los otros extractores (CSV y Database).

Para usar el ApiExtractor, configura una URL real en `appsettings.json`.

---

## 🎯 CONFIGURACIÓN MÍNIMA PARA EMPEZAR

**Solo necesitas configurar 2 cosas:**

### 1. Ruta de los CSV
```json
"CsvPath": "TU_RUTA_AQUI",
```

### 2. Verificar que SQL Server está corriendo
```powershell
Get-Service MSSQL$SQLEXPRESS
```

**¡Y ya está!** El resto se configura automáticamente.

---

## 📝 NOTAS IMPORTANTES

### Comportamiento del Worker Service:
- ✅ Se ejecuta automáticamente cada 60 minutos
- ✅ Primera ejecución: inmediatamente al iniciar
- ✅ Corre en background continuamente
- ✅ Para detenerlo: `Ctrl+C` en la consola

### Datos de Staging:
- ✅ Se guardan en JSON para trazabilidad
- ✅ Se mantienen históricos con timestamp
- ⚠️ Puedes eliminarlos manualmente si ocupan mucho espacio

### Base de Datos:
- ✅ Se crea automáticamente si no existe
- ✅ Se recrea en cada ejecución (EnsureDeleted + EnsureCreated)
- ⚠️ **IMPORTANTE:** Esto borra datos existentes - cambiar para producción

---

## 🔄 CAMBIAR COMPORTAMIENTO DE LA BD (Recomendado para Producción)

**Actualmente:** Borra y recrea la BD en cada ejecución.

**Para producción:** Modifica `Program.cs` línea 69:

```csharp
// ANTES (desarrollo):
await context.Database.EnsureCreatedAsync();

// DESPUÉS (producción):
await context.Database.MigrateAsync();
```

Y crea migrations:
```powershell
dotnet ef migrations add InitialCreate
dotnet ef database update
```

---

## ✅ CHECKLIST FINAL

Antes de ejecutar, verifica:

- [ ] Ruta CSV correcta en `appsettings.json`
- [ ] Archivos CSV existen en esa ruta
- [ ] SQL Server está corriendo
- [ ] Connection string correcto en `appsettings.json`
- [ ] Tienes permisos de escritura en la carpeta del proyecto (para logs y staging)

**Si todos están marcados, ejecuta:**
```powershell
dotnet run
```

---

## 🆘 ¿Necesitas Ayuda?

Si algo no funciona:

1. **Revisa los logs** en `logs/etl-YYYYMMDD.log`
2. **Busca el error específico** en la consola
3. **Verifica la configuración** en `appsettings.json`
4. **Prueba con `EnableParallelProcessing: false`** si hay errores raros

---

## 🎓 Para la Práctica Académica

**Entregables listos:**
- ✅ Código fuente funcionando
- ✅ README.md con documentación completa
- ✅ ARQUITECTURA.md con diagramas
- ✅ Worker Service implementado
- ✅ Clean Architecture
- ✅ SOLID principles

**Solo falta:**
- Configurar las rutas de tus archivos CSV
- Ejecutar y capturar screenshots de los logs
- (Opcional) Crear tus propios diagramas en Draw.io basándote en ARQUITECTURA.md
