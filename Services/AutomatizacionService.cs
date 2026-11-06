using System.Text;
using GeneradorReportesSeguridad.Models;

namespace GeneradorReportesSeguridad.Services;

/// <summary>
/// Servicio de automatización que vigila la carpeta de entrada en tiempo real (FileSystemWatcher),
/// ejecuta generaciones programadas y mantiene una bitácora de auditoría.
/// </summary>
public class AutomatizacionService : IDisposable
{
    private readonly string _directorioEntrada;
    private readonly string _directorioLogs;
    private readonly LectorEventosService _lector;
    private readonly AnalizadorSeguridadService _analizador;
    private readonly GeneradorReportesService _generador;

    private FileSystemWatcher? _watcher;
    private Timer? _debounceTimer;
    private readonly Lock _bloqueoEjecucion = new();
    private string _ultimoArchivoDetectado = string.Empty;

    public ResumenReporte? UltimoResumen { get; private set; }

    public AutomatizacionService(
        string directorioEntrada,
        string directorioLogs,
        LectorEventosService lector,
        AnalizadorSeguridadService analizador,
        GeneradorReportesService generador)
    {
        _directorioEntrada = directorioEntrada;
        _directorioLogs = directorioLogs;
        _lector = lector;
        _analizador = analizador;
        _generador = generador;

        Directory.CreateDirectory(_directorioEntrada);
        Directory.CreateDirectory(_directorioLogs);
    }

    /// <summary>
    /// Ejecuta el flujo completo de lectura, análisis y generación de reportes.
    /// </summary>
    public ResumenReporte EjecutarFlujoCompleto(string motivoDisparo)
    {
        lock (_bloqueoEjecucion)
        {
            RegistrarBitacora($"Iniciando procesamiento automático. Motivo: {motivoDisparo}");

            var (eventos, archivosProcesados) = _lector.LeerTodosLosEventos();
            var resumen = _analizador.AnalizarEventos(eventos, archivosProcesados);
            _generador.GenerarTodosLosReportes(resumen);

            UltimoResumen = resumen;

            RegistrarBitacora(
                $"Reporte generado exitosamente | Eventos: {resumen.TotalEventos} (Críticos: {resumen.TotalCriticos}, Altos: {resumen.TotalAltos}) | " +
                $"Riesgo: {resumen.IndiceRiesgo}/100 | HTML: {Path.GetFileName(resumen.RutaHtml)}");

            return resumen;
        }
    }

    /// <summary>
    /// Inicia el monitoreo en tiempo real de la carpeta de entrada usando FileSystemWatcher.
    /// </summary>
    public void IniciarVigilanciaCarpeta(Action<ResumenReporte, string>? alGenerarReporte = null)
    {
        DetenerVigilanciaCarpeta();

        _watcher = new FileSystemWatcher(_directorioEntrada)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size,
            Filter = "*.*",
            IncludeSubdirectories = false,
            EnableRaisingEvents = true
        };

        void ManejarCambioArchivo(object sender, FileSystemEventArgs e)
        {
            string ext = Path.GetExtension(e.FullPath).ToLowerInvariant();
            if (ext is not (".csv" or ".json" or ".txt" or ".log"))
                return;

            _ultimoArchivoDetectado = Path.GetFileName(e.FullPath);

            // Debounce de 600 ms para evitar múltiples disparos mientras el archivo termina de escribirse
            if (_debounceTimer == null)
            {
                _debounceTimer = new Timer(_ =>
                {
                    try
                    {
                        string motivo = $"Cambio detectado en archivo '{_ultimoArchivoDetectado}'";
                        var resumen = EjecutarFlujoCompleto(motivo);
                        alGenerarReporte?.Invoke(resumen, motivo);
                    }
                    catch (Exception ex)
                    {
                        RegistrarBitacora($"ERROR en ejecución automática: {ex.Message}");
                    }
                }, null, 600, Timeout.Infinite);
            }
            else
            {
                _debounceTimer.Change(600, Timeout.Infinite);
            }
        }

        _watcher.Created += ManejarCambioArchivo;
        _watcher.Changed += ManejarCambioArchivo;
        _watcher.Deleted += ManejarCambioArchivo;
        _watcher.Renamed += (s, e) => ManejarCambioArchivo(s, e);

        RegistrarBitacora($"Vigilancia activa en tiempo real iniciada sobre: {_directorioEntrada}");
    }

    /// <summary>
    /// Detiene el FileSystemWatcher activo.
    /// </summary>
    public void DetenerVigilanciaCarpeta()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
            RegistrarBitacora("Vigilancia de carpeta detenida.");
        }

        _debounceTimer?.Dispose();
        _debounceTimer = null;
    }

    /// <summary>
    /// Escribe una entrada con fecha y hora en el archivo de bitácora de automatización.
    /// </summary>
    public void RegistrarBitacora(string mensaje)
    {
        try
        {
            Directory.CreateDirectory(_directorioLogs);
            string rutaLog = Path.Combine(_directorioLogs, "automatizacion.log");
            string linea = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {mensaje}{Environment.NewLine}";
            File.AppendAllText(rutaLog, linea, Encoding.UTF8);
        }
        catch
        {
            // Ignorar errores secundarios de escritura en bitácora
        }
    }

    public void Dispose()
    {
        DetenerVigilanciaCarpeta();
        GC.SuppressFinalize(this);
    }
}

