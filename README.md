# 🛡️ Generador Automatizado de Reportes de Seguridad (C# .NET)

Aplicación en **C# (.NET)** diseñada para leer archivos de eventos de seguridad (`.csv`, `.json`, `.txt`, `.log`), analizarlos y correlacionarlos automáticamente, y generar reportes ejecutivos organizados en **HTML**, **TXT** y **CSV**.

---

## 🚀 Características Principales

1. **Lectura Multiformato de Archivos (`EventosEntrada/`)**:
   - **CSV (`.csv`)**: Eventos tabulares separados por coma o punto y coma.
   - **JSON (`.json`)**: Alertas estructuradas provenientes de SIEM / EDR.
   - **Logs de Texto (`.log` / `.txt`)**: Bitácoras de servidores en formato `[Fecha] [Severidad] [IP] [Usuario] [Servidor] [Tipo] [Estado] Descripción` o separadas por `|`.
2. **Análisis y Correlación Automática**:
   - Clasificación y ordenamiento por nivel de severidad (`Critica`, `Alta`, `Media`, `Baja`) y fecha.
   - Cálculo del **Índice de Riesgo Global (0 - 100)**.
   - Detección heurística de patrones:
     - Ataques repetitivos o fuerza bruta desde una misma dirección IP.
     - Posible compromiso de cuentas de usuario con múltiples incidentes críticos/altos.
     - Incidentes críticos activos pendientes de contención.
     - Servidores con alta concentración de ataques.
3. **Automatización en Tiempo Real y Programada**:
   - **Vigilancia en vivo (`FileSystemWatcher`)**: Detecta automáticamente cuando se agrega, modifica o elimina un archivo en `EventosEntrada/` y regenera todos los reportes al instante.
   - **Modo Desatendido CLI (`--auto` y `--watch`)**: Permite integrarlo con el **Programador de Tareas de Windows (Task Scheduler)** o scripts de automatización.
   - **Bitácora de Auditoría (`LogsAutomatizacion/automatizacion.log`)**: Registra cada ejecución automática con fecha y hora.
4. **Reportes Generados (`ReportesGenerados/`)**:
   - `Reporte_Seguridad_Ultimo.html`: Dashboard interactivo con diseño oscuro tipo SOC, tarjetas KPI, gráficos de barras en CSS, buscador en tiempo real y filtros por severidad.
   - `Reporte_Seguridad_Ultimo.txt`: Reporte ejecutivo estructurado en texto plano listo para imprimir o enviar por correo.
   - `Reporte_Seguridad_Ultimo.csv`: Base consolidada y normalizada compatible con Microsoft Excel (codificación UTF-8 con BOM).

---

## ▶️ Cómo Ejecutar el Programa

### Opción 1: Menú Interactivo en Consola
Ejecuta en la terminal dentro de la carpeta del proyecto:
```powershell
dotnet run
```
*(O haz doble clic en `ejecutar_menu.bat`)*

### Opción 2: Ejecución Automática Desatendida (Sin menú)
Ideal para tareas programadas o scripts:
```powershell
dotnet run -- --auto
```
*(O haz doble clic en `ejecutar_automatico.bat`)*

### Opción 3: Iniciar Directamente en Vigilancia de Carpeta (`FileSystemWatcher`)
```powershell
dotnet run -- --watch
```

---

## 📂 Estructura del Proyecto

- `Program.cs`: Punto de entrada, menú interactivo y soporte de argumentos CLI (`--auto`, `--watch`).
- `Models/EventoSeguridad.cs`: Modelo de datos y conversión de niveles de severidad.
- `Models/ResumenReporte.cs`: Modelo de métricas, estadísticas, alertas de correlación y rutas de salida.
- `Services/LectorEventosService.cs`: Lectura resiliente de archivos `.csv`, `.json`, `.log`, `.txt`, generador de datos de ejemplo y simulador de lotes.
- `Services/AnalizadorSeguridadService.cs`: Motor de organización, estadísticas, cálculo de riesgo y reglas de correlación.
- `Services/GeneradorReportesService.cs`: Exportador automático a formatos HTML interactivo, TXT ejecutivo y CSV consolidado.
- `Services/AutomatizacionService.cs`: Vigilancia en tiempo real con `FileSystemWatcher`, control de rebotes (*debounce*) y registro de bitácora.

