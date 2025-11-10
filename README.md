# Proceso ETL - Worker Service

## Descripción General

Sistema ETL (Extract, Transform, Load) implementado como Worker Service en .NET 9, diseñado para extraer datos desde múltiples fuentes (CSV, Base de Datos, API REST), transformarlos y cargarlos en una base de datos analítica.

## Arquitectura

### Arquitectura Clean/Onion

El proyecto sigue los principios de Clean Architecture con las siguientes capas:

```
ProcesoETL/
├── Core/                          # Capa de dominio
│   ├── Configuration/             # Configuraciones
│   └── Interfaces/                # Abstracciones
├── Infrastructure/                # Capa de infraestructura
│   ├── Extractors/                # Implementaciones de extractores
│   └── Services/                  # Servicios de infraestructura
├── Application/                   # Capa de aplicación
│   └── Services/                  # Lógica de negocio ETL
├── Data/                          # Contexto de base de datos
└── Models/                        # Modelos de dominio
```

### Componentes Principales

| Componente | Descripción | Tecnología | Responsabilidad |
|------------|-------------|------------|-----------------|
| **CsvExtractor** | Extrae datos de archivos CSV | C#, CsvHelper | Lee y valida archivos CSV |
| **DatabaseExtractor** | Extrae datos de BD relacional | C#, EF Core | Ejecuta queries definidos |
| **ApiExtractor** | Consume API REST | C#, HttpClient | Recupera datos de APIs |
| **DataLoader** | Inserta datos en BD analítica | EF Core | Inserción con manejo de identidad |
| **StagingService** | Gestiona almacenamiento temporal | JSON, Sistema de archivos | Staging de datos extraídos |
| **ETLPipeline** | Orquesta el proceso ETL | - | Coordinación de Extract-Transform-Load |
| **ETLWorker** | Worker Service background | Microsoft.Extensions.Hosting | Ejecución programada |

## Atributos de Calidad

### ✅ Rendimiento
- **Procesamiento Asíncrono**: Uso de `async/await` en todas las operaciones I/O
- **Procesamiento Paralelo**: Extracción simultánea de múltiples fuentes con `Task.WhenAll`
- **Métricas**: Logging de tiempo de ejecución con `Stopwatch`

### ✅ Escalabilidad
- **Diseño Modular**: Fácil agregar nuevos extractores implementando `IExtractor<T>`
- **Configuración Externalizada**: Todas las fuentes configurables en `appsettings.json`
- **Staging Flexible**: Sistema de staging para manejar grandes volúmenes

### ✅ Seguridad
- **User Secrets**: Soporte para almacenar credenciales sensibles
- **Connection Strings**: Configuradas externamente, no en código
- **TrustServerCertificate**: Configuración de SSL/TLS en conexiones DB

### ✅ Mantenibilidad
- **SOLID Principles**: Separación de responsabilidades clara
- **Dependency Injection**: Inversión de dependencias completa
- **Logging Estructurado**: Serilog con niveles apropiados
- **Interfaz Única**: `IExtractor<T>` para todos los extractores

## Configuración

### appsettings.json

```json
{
  "ConnectionStrings": {
    "AnalyticsDb": "Server=...;Database=AnalyticsDB;...",
    "SourceDb": "Server=...;Database=SourceDB;..."
  },
  "DataSources": {
    "CsvPath": "C:\\path\\to\\csv\\files\\",
    "CsvFiles": {
      "Customers": "customers.csv",
      "Products": "products.csv"
    },
    "ApiBaseUrl": "https://api.example.com",
    "ApiKey": "YOUR_API_KEY"
  },
  "ETLSettings": {
    "RunIntervalMinutes": 60,
    "EnableParallelProcessing": true,
    "BatchSize": 1000,
    "StagingPath": "staging"
  }
}
```

### Variables de Entorno (Opcional)

Para desarrollo local, usar User Secrets:

```bash
dotnet user-secrets set "DataSources:ApiKey" "your-secret-key"
dotnet user-secrets set "ConnectionStrings:AnalyticsDb" "your-connection-string"
```

## Instalación y Ejecución

### Requisitos Previos

- .NET 9 SDK
- SQL Server (local o remoto)
- Archivos CSV en la ruta configurada

### Instalación

1. Clonar el repositorio:
```bash
git clone https://github.com/homerportes/ProcesoETL.git
cd ProcesoETL
```

2. Restaurar paquetes:
```bash
dotnet restore
```

3. Configurar `appsettings.json` con tus rutas y conexiones

4. Crear la base de datos (se crea automáticamente al iniciar):
```bash
dotnet ef database update
```

### Ejecución

#### Modo Desarrollo
```bash
cd ProcesoETL
dotnet run
```

#### Modo Producción (Como Servicio Windows)
```bash
dotnet publish -c Release -o ./publish
sc create "ETLWorkerService" binPath="C:\path\to\publish\ProcesoETL.exe"
sc start "ETLWorkerService"
```

## Flujo ETL

### Diagrama de Flujo

```
┌─────────────────────────────────────────────────┐
│              INICIO DEL WORKER                  │
└────────────────┬────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────┐
│         FASE EXTRACT (Paralelo)                 │
├─────────────────────────────────────────────────┤
│  ┌──────────┐  ┌──────────┐  ┌──────────┐     │
│  │   CSV    │  │    DB    │  │   API    │     │
│  │Extractor │  │Extractor │  │Extractor │     │
│  └────┬─────┘  └────┬─────┘  └────┬─────┘     │
│       └─────────────┬─────────────┘            │
│                     ▼                           │
│              Staging Storage                    │
└────────────────┬────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────┐
│         FASE TRANSFORM                          │
├─────────────────────────────────────────────────┤
│  • Limpieza de datos                           │
│  • Deduplicación                               │
│  • Validaciones                                │
│  • Normalización                               │
└────────────────┬────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────┐
│         FASE LOAD                               │
├─────────────────────────────────────────────────┤
│  • Customers → Analytics DB                    │
│  • Products → Analytics DB                     │
│  • Orders → Analytics DB                       │
│  • OrderDetails → Analytics DB                 │
└────────────────┬────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────┐
│         LOGGING & MONITORING                    │
│  • Tiempos de ejecución                        │
│  • Registros procesados                        │
│  • Errores y warnings                          │
└─────────────────────────────────────────────────┘
```

## Logs

Los logs se generan en dos destinos:

1. **Console**: Para monitoreo en tiempo real
2. **Archivo**: `logs/etl-YYYYMMDD.log` (rotación diaria)

Ejemplo de log:
```
2025-11-09 10:30:45.123 -05:00 [INF] Starting ETL Pipeline execution
2025-11-09 10:30:45.456 -05:00 [INF] Successfully extracted 1250 records from customers.csv in 234ms
2025-11-09 10:30:46.789 -05:00 [INF] Transform phase completed: 1200 customers, 350 products
```

## Pruebas

### Verificación de Rendimiento

El sistema incluye medición automática de tiempos:

```csharp
// Los tiempos se registran automáticamente en los logs
[INF] Successfully extracted 1250 records from customers.csv in 234ms
[INF] Extract phase completed in 1.45s
```

### Validación de Datos

Los datos se validan durante la transformación:
- Eliminación de registros con campos críticos vacíos
- Deduplicación por ID
- Validación de precios (>= 0)

## Extensibilidad

### Agregar Nuevo Extractor

1. Crear clase que implemente `IExtractor<T>`:

```csharp
public class CustomExtractor<T> : IExtractor<T>
{
    public string Name => "CustomExtractor";
    
    public async Task<IEnumerable<T>> ExtractAsync()
    {
        // Tu lógica de extracción
    }
}
```

2. Registrar en `Program.cs`:

```csharp
builder.Services.AddScoped<IExtractor<MyType>, CustomExtractor<MyType>>();
```

3. Usar en `ETLPipeline`:

```csharp
var extractor = scope.ServiceProvider.GetRequiredService<IExtractor<MyType>>();
var data = await extractor.ExtractAsync();
```

## Dependencias

- **Microsoft.Extensions.Hosting** (9.0.0): Worker Service framework
- **Microsoft.EntityFrameworkCore.SqlServer** (9.0.9): ORM y acceso a SQL Server
- **CsvHelper** (33.1.0): Lectura/escritura de archivos CSV
- **Serilog** (8.0.0): Logging estructurado
- **Polly** (8.5.0): Políticas de resiliencia y retry

## Troubleshooting

### Problema: "Cannot open database"
**Solución**: Verificar connection string en `appsettings.json` y que SQL Server esté ejecutándose

### Problema: "CSV file not found"
**Solución**: Verificar la ruta `DataSources:CsvPath` en `appsettings.json`

### Problema: "API request failed"
**Solución**: Verificar `ApiBaseUrl` y `ApiKey`, revisar conectividad de red

### Problema: "Identity insert error"
**Solución**: El sistema maneja esto automáticamente con `SET IDENTITY_INSERT`, revisar logs para más detalles

## Licencia

Este proyecto es parte de un ejercicio académico.

## Autor

Homer Portes
Universidad: [Tu Universidad]
Curso: Arquitectura de Software
