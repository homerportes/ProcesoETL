# Documentación de Arquitectura - Proceso ETL

## 1. Diagrama de Arquitectura General

```
┌─────────────────────────────────────────────────────────────────────┐
│                     PROCESO ETL - WORKER SERVICE                    │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌───────────────────────────────────────────────────────────┐    │
│  │                   APPLICATION LAYER                       │    │
│  │  ┌─────────────┐         ┌──────────────┐                │    │
│  │  │ ETLWorker   │────────>│ ETLPipeline  │                │    │
│  │  │ (Hosted     │         │ (Orchestrator)│                │    │
│  │  │  Service)   │         └──────┬───────┘                │    │
│  │  └─────────────┘                │                         │    │
│  └─────────────────────────────────┼─────────────────────────┘    │
│                                     │                              │
│  ┌─────────────────────────────────┼─────────────────────────┐    │
│  │              CORE LAYER         │                         │    │
│  │  ┌────────────────────────────┐ │                         │    │
│  │  │      Interfaces            │ │                         │    │
│  │  │  • IExtractor<T>           │ │                         │    │
│  │  │  • IDataLoader             │ │                         │    │
│  │  │  • IStagingService         │ │                         │    │
│  │  └────────────────────────────┘ │                         │    │
│  └─────────────────────────────────┼─────────────────────────┘    │
│                                     │                              │
│  ┌─────────────────────────────────┼─────────────────────────┐    │
│  │        INFRASTRUCTURE LAYER     ▼                         │    │
│  │  ┌────────────────────────────────────────────────┐       │    │
│  │  │           Extractors                           │       │    │
│  │  │  ┌───────────┐ ┌───────────┐ ┌───────────┐   │       │    │
│  │  │  │    CSV    │ │ Database  │ │    API    │   │       │    │
│  │  │  │ Extractor │ │ Extractor │ │ Extractor │   │       │    │
│  │  │  └─────┬─────┘ └─────┬─────┘ └─────┬─────┘   │       │    │
│  │  └────────┼─────────────┼─────────────┼─────────┘       │    │
│  │           │             │             │                  │    │
│  │  ┌────────▼─────────────▼─────────────▼─────────┐       │    │
│  │  │          Staging Service                     │       │    │
│  │  │  • SaveToStagingAsync                        │       │    │
│  │  │  • LoadFromStagingAsync                      │       │    │
│  │  └──────────────────┬───────────────────────────┘       │    │
│  │                     │                                    │    │
│  │  ┌──────────────────▼───────────────────────────┐       │    │
│  │  │          Data Loader                         │       │    │
│  │  │  • LoadAsync                                 │       │    │
│  │  │  • LoadWithIdentityAsync                     │       │    │
│  │  └──────────────────┬───────────────────────────┘       │    │
│  └─────────────────────┼─────────────────────────────────  │    │
│                        │                                    │    │
└────────────────────────┼────────────────────────────────────┘
                         │
        ┌────────────────┴────────────────┐
        │                                  │
   ┌────▼────┐                      ┌─────▼──────┐
   │ FUENTES │                      │  DESTINO   │
   │  DATOS  │                      │   DATOS    │
   ├─────────┤                      ├────────────┤
   │ • CSV   │                      │ Analytics  │
   │ • DB    │                      │ Database   │
   │ • API   │                      │ (SQL Srv)  │
   └─────────┘                      └────────────┘
```

## 2. Diagrama de Secuencia - Proceso ETL Completo

```
┌──────┐    ┌──────────┐    ┌───────────┐    ┌─────────┐    ┌──────────┐    ┌──────────┐
│Worker│    │Pipeline  │    │Extractors │    │Staging  │    │DataLoader│    │Analytics │
│Service│   │          │    │           │    │Service  │    │          │    │   DB     │
└──┬───┘    └────┬─────┘    └─────┬─────┘    └────┬────┘    └────┬─────┘    └────┬─────┘
   │             │                 │                │              │               │
   │ Timer Tick  │                 │                │              │               │
   ├────────────>│                 │                │              │               │
   │             │                 │                │              │               │
   │             │ EXTRACT PHASE   │                │              │               │
   │             ├────────────────>│                │              │               │
   │             │                 │                │              │               │
   │             │  CSV Extract    │                │              │               │
   │             │<────────────────┤                │              │               │
   │             │  DB Extract     │                │              │               │
   │             │<────────────────┤                │              │               │
   │             │  API Extract    │                │              │               │
   │             │<────────────────┤                │              │               │
   │             │                 │                │              │               │
   │             │ Save to Staging │                │              │               │
   │             ├────────────────────────────────> │              │               │
   │             │                                  │              │               │
   │             │ TRANSFORM PHASE                  │              │               │
   │             │ Load from Staging                │              │               │
   │             │<─────────────────────────────────┤              │               │
   │             │                                  │              │               │
   │             │ Apply Transformations            │              │               │
   │             │ (Clean, Validate, Dedupe)        │              │               │
   │             │                                  │              │               │
   │             │ Save Transformed                 │              │               │
   │             ├─────────────────────────────────>│              │               │
   │             │                                  │              │               │
   │             │ LOAD PHASE                       │              │               │
   │             │ Load Transformed Data            │              │               │
   │             │<─────────────────────────────────┤              │               │
   │             │                                  │              │               │
   │             │ Load to DB                                      │               │
   │             ├────────────────────────────────────────────────>│               │
   │             │                                                 │  Insert Data  │
   │             │                                                 ├──────────────>│
   │             │                                                 │   Success     │
   │             │                                                 │<──────────────┤
   │             │ Success                                         │               │
   │             │<────────────────────────────────────────────────┤               │
   │  Complete   │                                                                 │
   │<────────────┤                                                                 │
   │             │                                                                 │
   │  Wait for   │                                                                 │
   │  Next Tick  │                                                                 │
```

## 3. Diagrama de Componentes por Responsabilidad

```
┌─────────────────────────────────────────────────────────────────┐
│                    EXTRACTION LAYER                             │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────────────┐  ┌─────────────────┐  ┌────────────────┐ │
│  │  CsvExtractor   │  │DatabaseExtractor│  │  ApiExtractor  │ │
│  ├─────────────────┤  ├─────────────────┤  ├────────────────┤ │
│  │ • ExtractAsync()│  │ • ExtractAsync()│  │• ExtractAsync()│ │
│  │ • Validación    │  │ • Query Builder │  │• HTTP Client   │ │
│  │ • CsvHelper     │  │ • EF Core       │  │• Retry Policy  │ │
│  │ • Stopwatch     │  │ • AsNoTracking  │  │• Error Handle  │ │
│  └─────────────────┘  └─────────────────┘  └────────────────┘ │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                     STAGING LAYER                               │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌────────────────────────────────────────────────────────┐    │
│  │              StagingService                            │    │
│  ├────────────────────────────────────────────────────────┤    │
│  │ • SaveToStagingAsync<T>()                             │    │
│  │   → Serializa datos a JSON                            │    │
│  │   → Guarda en sistema de archivos                     │    │
│  │   → Timestamp en nombre de archivo                    │    │
│  │                                                        │    │
│  │ • LoadFromStagingAsync<T>()                           │    │
│  │   → Lee último archivo JSON                           │    │
│  │   → Deserializa a objetos                             │    │
│  │                                                        │    │
│  │ • ClearStagingAsync()                                 │    │
│  │   → Elimina archivos temporales                       │    │
│  └────────────────────────────────────────────────────────┘    │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                     LOADING LAYER                               │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌────────────────────────────────────────────────────────┐    │
│  │              DataLoader                                │    │
│  ├────────────────────────────────────────────────────────┤    │
│  │ • LoadAsync<T>()                                       │    │
│  │   → Inserción normal via EF Core                      │    │
│  │                                                        │    │
│  │ • LoadWithIdentityAsync<T>()                          │    │
│  │   → Detecta columnas identity                         │    │
│  │   → SET IDENTITY_INSERT ON                            │    │
│  │   → Inserción con IDs explícitos                      │    │
│  │   → SET IDENTITY_INSERT OFF                           │    │
│  │   → Manejo de transacciones                           │    │
│  │                                                        │    │
│  │ • IsColumnIdentityAsync()                             │    │
│  │   → Query sys.columns                                 │    │
│  │   → Valida si columna es identity                     │    │
│  └────────────────────────────────────────────────────────┘    │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

## 4. Garantías de Atributos de Calidad

### RENDIMIENTO

```
┌────────────────────────────────────────┐
│     Estrategias de Rendimiento         │
├────────────────────────────────────────┤
│                                        │
│  1. Procesamiento Asíncrono            │
│     ┌──────────────────────┐          │
│     │  async/await en      │          │
│     │  todas las I/O       │          │
│     └──────────────────────┘          │
│                                        │
│  2. Procesamiento Paralelo             │
│     ┌──────────────────────┐          │
│     │ Task.WhenAll() para  │          │
│     │ múltiples fuentes    │          │
│     └──────────────────────┘          │
│                                        │
│  3. Medición de Tiempos                │
│     ┌──────────────────────┐          │
│     │ Stopwatch en cada    │          │
│     │ operación crítica    │          │
│     └──────────────────────┘          │
│                                        │
│  4. Optimizaciones EF Core             │
│     ┌──────────────────────┐          │
│     │ AsNoTracking()       │          │
│     │ Batch operations     │          │
│     └──────────────────────┘          │
└────────────────────────────────────────┘
```

### ESCALABILIDAD

```
┌────────────────────────────────────────┐
│     Estrategias de Escalabilidad       │
├────────────────────────────────────────┤
│                                        │
│  1. Diseño Modular                     │
│     ┌──────────────────────┐          │
│     │ IExtractor<T>        │          │
│     │ interface permite    │          │
│     │ agregar extractores  │          │
│     └──────────────────────┘          │
│                                        │
│  2. Configuración Externa              │
│     ┌──────────────────────┐          │
│     │ appsettings.json     │          │
│     │ para todas las       │          │
│     │ fuentes de datos     │          │
│     └──────────────────────┘          │
│                                        │
│  3. Dependency Injection               │
│     ┌──────────────────────┐          │
│     │ IServiceProvider     │          │
│     │ permite registrar    │          │
│     │ nuevos servicios     │          │
│     └──────────────────────┘          │
│                                        │
│  4. Staging Flexible                   │
│     ┌──────────────────────┐          │
│     │ JSON files permiten  │          │
│     │ manejar grandes      │          │
│     │ volúmenes            │          │
│     └──────────────────────┘          │
└────────────────────────────────────────┘
```

### SEGURIDAD

```
┌────────────────────────────────────────┐
│     Estrategias de Seguridad           │
├────────────────────────────────────────┤
│                                        │
│  1. Credenciales Externas              │
│     ┌──────────────────────┐          │
│     │ appsettings.json     │          │
│     │ User Secrets         │          │
│     │ Environment Vars     │          │
│     └──────────────────────┘          │
│                                        │
│  2. Connection Strings Seguras         │
│     ┌──────────────────────┐          │
│     │ No hardcoded en      │          │
│     │ código fuente        │          │
│     │ TrustServerCert      │          │
│     └──────────────────────┘          │
│                                        │
│  3. API Key Management                 │
│     ┌──────────────────────┐          │
│     │ Headers configurados │          │
│     │ No expuestos en logs │          │
│     └──────────────────────┘          │
│                                        │
│  4. Manejo de Errores                  │
│     ┌──────────────────────┐          │
│     │ Try-catch apropiados │          │
│     │ No exponer detalles  │          │
│     │ internos             │          │
│     └──────────────────────┘          │
└────────────────────────────────────────┘
```

### MANTENIBILIDAD

```
┌────────────────────────────────────────┐
│     Estrategias de Mantenibilidad      │
├────────────────────────────────────────┤
│                                        │
│  1. Principios SOLID                   │
│     ┌──────────────────────┐          │
│     │ S: Single Resp.      │          │
│     │ O: Open/Closed       │          │
│     │ L: Liskov Subst.     │          │
│     │ I: Interface Segreg. │          │
│     │ D: Dependency Inv.   │          │
│     └──────────────────────┘          │
│                                        │
│  2. Clean Architecture                 │
│     ┌──────────────────────┐          │
│     │ Core → Infrastructure│          │
│     │ → Application        │          │
│     │ Dependencias hacia   │          │
│     │ el centro            │          │
│     └──────────────────────┘          │
│                                        │
│  3. Logging Estructurado               │
│     ┌──────────────────────┐          │
│     │ Serilog con niveles  │          │
│     │ Console + File       │          │
│     │ Contexto rico        │          │
│     └──────────────────────┘          │
│                                        │
│  4. Documentación                      │
│     ┌──────────────────────┐          │
│     │ XML comments         │          │
│     │ README completo      │          │
│     │ Diagramas            │          │
│     └──────────────────────┘          │
└────────────────────────────────────────┘
```

## 5. Decisiones Técnicas Clave

| Decisión | Justificación | Alternativas Consideradas |
|----------|---------------|---------------------------|
| **Worker Service** | Ejecución en background, ideal para tareas programadas | Console App (sin lifecycle management) |
| **Clean Architecture** | Separación de responsabilidades, testabilidad | Arquitectura en capas tradicional |
| **IExtractor<T>** | Abstracción uniforme para todas las fuentes | Clases concretas sin interfaz |
| **Staging Service** | Buffer intermedio, recuperación ante fallos | Directo a BD (sin staging) |
| **Serilog** | Logging estructurado, múltiples destinos | ILogger solo (menos flexible) |
| **EF Core** | ORM completo, migrations, tipo seguro | ADO.NET (más código boilerplate) |
| **JSON para Staging** | Legible, debuggable, portable | Binary (más rápido pero opaco) |
| **Task.WhenAll** | Paralelismo real, mejor rendimiento | Sequential processing |

