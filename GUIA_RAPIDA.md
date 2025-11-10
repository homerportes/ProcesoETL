# 🚀 Guía Rápida de Inicio - Proceso ETL

## Inicio Rápido en 5 Minutos

### 1. Prerrequisitos
- ✅ .NET 9 SDK instalado
- ✅ SQL Server (local o remoto) corriendo
- ✅ Archivos CSV disponibles (o ajustar configuración)

### 2. Configuración Mínima

Edita `ProcesoETL/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "AnalyticsDb": "Server=TU_SERVIDOR\\SQLEXPRESS;Database=AnalyticsDB;Trusted_Connection=true;TrustServerCertificate=true;"
  },
  "DataSources": {
    "CsvPath": "C:\\TU_RUTA\\csv\\"
  }
}
```

### 3. Ejecutar

```powershell
cd c:\proyectos\ProcesoETL\ProcesoETL
dotnet run
```

### 4. Ver Logs

Los logs se generan en:
- **Console**: Salida en tiempo real
- **Archivo**: `ProcesoETL/logs/etl-YYYYMMDD.log`

---

## 📁 Estructura del Proyecto Transformado

```
ProcesoETL/
│
├── 📄 README.md                    ← Documentación principal
├── 📄 ARQUITECTURA.md              ← Diagramas y decisiones técnicas
├── 📄 CAMBIOS_REALIZADOS.md        ← Este documento
├── 📄 GUIA_RAPIDA.md               ← Guía de inicio rápido
│
├── ProcesoETL/                     ← Proyecto principal
│   ├── Core/                       ← 🏛️ Capa de Dominio
│   │   ├── Interfaces/
│   │   │   ├── IExtractor.cs
│   │   │   ├── IDataLoader.cs
│   │   │   └── IStagingService.cs
│   │   └── Configuration/
│   │       └── Settings.cs
│   │
│   ├── Infrastructure/             ← 🔧 Capa de Infraestructura
│   │   ├── Extractors/
│   │   │   ├── CsvExtractor.cs
│   │   │   ├── DatabaseExtractor.cs
│   │   │   └── ApiExtractor.cs
│   │   └── Services/
│   │       ├── StagingService.cs
│   │       └── DataLoader.cs
│   │
│   ├── Application/                ← 💼 Capa de Aplicación
│   │   └── Services/
│   │       ├── ETLWorker.cs        ← Worker Service principal
│   │       └── ETLPipeline.cs      ← Orquestador ETL
│   │
│   ├── Data/                       ← 💾 Contexto de Datos
│   │   └── AppDbContext.cs
│   │
│   ├── Models/                     ← 📦 Modelos de Dominio
│   │   ├── Customer.cs
│   │   ├── Order.cs
│   │   ├── Product.cs
│   │   └── OrderDetail.cs
│   │
│   ├── Services/                   ← (Legacy - Pipeline.cs antiguo)
│   │
│   ├── 📄 Program.cs               ← Punto de entrada Worker Service
│   ├── 📄 appsettings.json         ← Configuración principal
│   ├── 📄 appsettings.Development.json
│   └── 📄 ProcesoETL.csproj        ← Definición del proyecto
│
└── Domain/                         ← (Legacy - modelos compartidos)
```

---

## 🎯 Conceptos Clave

### 1. Clean Architecture
El proyecto sigue las capas de Clean Architecture:
- **Core**: Interfaces y abstracciones (sin dependencias externas)
- **Infrastructure**: Implementaciones concretas (EF Core, HTTP, File System)
- **Application**: Lógica de negocio y orquestación

### 2. Principio de Inversión de Dependencias
```
Application → Interfaces (Core) ← Infrastructure
```
La capa de aplicación depende de interfaces, no de implementaciones concretas.

### 3. Worker Service
- Ejecuta en background continuamente
- Usa `IHostedService` de .NET
- Programa ejecuciones periódicas
- Lifecycle management automático

---

## 🔄 Flujo de Ejecución

```
1. Worker Service inicia
   ↓
2. Ejecuta ETLPipeline inmediatamente
   ↓
3. EXTRACT Phase (paralelo)
   - CSV → Staging
   - Database → Staging
   - API → Staging
   ↓
4. TRANSFORM Phase
   - Carga de Staging
   - Limpieza y validación
   - Guarda transformado
   ↓
5. LOAD Phase
   - Carga a Analytics DB
   - Manejo de identidades
   ↓
6. Espera intervalo configurado
   ↓
7. Vuelve al paso 2
```

---

## ⚙️ Configuraciones Importantes

### Intervalo de Ejecución
```json
"ETLSettings": {
  "RunIntervalMinutes": 60  // ← Cambia esto para ajustar frecuencia
}
```

### Procesamiento Paralelo
```json
"ETLSettings": {
  "EnableParallelProcessing": true  // ← false para secuencial
}
```

### Fuentes de Datos
```json
"DataSources": {
  "CsvPath": "C:\\ruta\\csv\\",
  "ApiBaseUrl": "https://api.example.com",
  "ApiKey": "tu-api-key"
}
```

---

## 🧪 Cómo Probar

### Test 1: Verificar Compilación
```powershell
dotnet build
```
**Esperado**: ✅ Compilación exitosa

### Test 2: Ejecutar una Vez
```powershell
dotnet run
```
**Esperado**: 
- Logs en consola
- Archivo de log creado
- Base de datos poblada (si hay CSVs válidos)

### Test 3: Verificar Logs
```powershell
cat logs/etl-*.log
```
**Esperado**: Ver registros de extracción, transformación y carga

### Test 4: Verificar Base de Datos
```sql
SELECT COUNT(*) FROM Customers;
SELECT COUNT(*) FROM Products;
SELECT COUNT(*) FROM Orders;
SELECT COUNT(*) FROM OrderDetails;
```

---

## 🐛 Troubleshooting Rápido

### Problema: "Cannot open database"
```powershell
# Verificar connection string
# Verificar que SQL Server esté corriendo
```

### Problema: "CSV file not found"
```json
// Verificar en appsettings.json:
"DataSources": {
  "CsvPath": "C:\\ruta\\correcta\\"  // Notar las \\ dobles
}
```

### Problema: No se crean logs
```powershell
# Verificar que existe la carpeta
mkdir logs

# Verificar permisos de escritura
```

---

## 📚 Archivos de Documentación

| Archivo | Propósito |
|---------|-----------|
| `README.md` | Documentación completa del proyecto |
| `ARQUITECTURA.md` | Diagramas y decisiones de arquitectura |
| `CAMBIOS_REALIZADOS.md` | Resumen de la transformación del proyecto |
| `GUIA_RAPIDA.md` | Este archivo - inicio rápido |

---

## 🎓 Para la Entrega Académica

### Evidencias a Incluir:

1. **Código Fuente**
   - ✅ Todo el proyecto en GitHub
   - ✅ Commits descriptivos

2. **Diagrama de Arquitectura**
   - ✅ Ver `ARQUITECTURA.md`
   - ✅ Copiar al documento Word/PDF

3. **Justificación Técnica**
   - ✅ Ver sección "Decisiones Técnicas" en `ARQUITECTURA.md`
   - ✅ Ver "Atributos de Calidad" en `CAMBIOS_REALIZADOS.md`

4. **Screenshots Sugeridos**
   - Proyecto compilando exitosamente
   - Logs en ejecución
   - Base de datos poblada
   - Estructura de carpetas

---

## 🚀 Comandos Útiles

```powershell
# Compilar
dotnet build

# Ejecutar en modo desarrollo
dotnet run

# Publicar para producción
dotnet publish -c Release -o ./publish

# Ver logs en tiempo real
Get-Content logs\etl-*.log -Wait

# Limpiar build
dotnet clean

# Restaurar paquetes
dotnet restore

# Ver información del proyecto
dotnet --info
```

---

## ✅ Checklist de Entrega

- [ ] Código compila sin errores
- [ ] README.md completo
- [ ] ARQUITECTURA.md con diagramas
- [ ] appsettings.json configurado
- [ ] Proyecto sube a GitHub
- [ ] Screenshots tomados
- [ ] Documento Word/PDF preparado
- [ ] Video demo (opcional)

---

## 💡 Tips Finales

1. **Ajusta el intervalo** en `appsettings.json` para pruebas rápidas (ej: 1 minuto)
2. **Revisa los logs** siempre - contienen información valiosa
3. **Usa User Secrets** para credenciales sensibles en producción
4. **Documenta cambios** si agregas nuevas fuentes de datos

---

## 📞 Soporte

Si encuentras problemas:
1. Revisa los logs en `logs/etl-*.log`
2. Verifica la configuración en `appsettings.json`
3. Consulta `README.md` sección Troubleshooting
4. Revisa los errores de compilación si los hay

---

**¡Éxito con tu proyecto! 🎉**
