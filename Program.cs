using System.Diagnostics;
using System.Text;
using GeneradorReportesSeguridad.Models;
using GeneradorReportesSeguridad.Services;

namespace GeneradorReportesSeguridad;

internal class Program
{
    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        string directorioBase = Directory.GetCurrentDirectory();
        string carpetaEntrada = Path.Combine(directorioBase, "EventosEntrada");
        string carpetaReportes = Path.Combine(directorioBase, "ReportesGenerados");
        string carpetaLogs = Path.Combine(directorioBase, "LogsAutomatizacion");

        var lector = new LectorEventosService(carpetaEntrada);
        var analizador = new AnalizadorSeguridadService();
        var generador = new GeneradorReportesService(carpetaReportes);
        using var automatizacion = new AutomatizacionService(carpetaEntrada, carpetaLogs, lector, analizador, generador);

        // Si la carpeta de entrada está vacía, creamos archivos de ejemplo (.csv, .json y .log)
        bool creadosIniciales = lector.AsegurarArchivosEjemploIniciales();
        if (creadosIniciales)
        {
            automatizacion.RegistrarBitacora("Se generaron archivos iniciales de prueba (.csv, .json, .log) en EventosEntrada.");
        }

        // Modo desatendido por línea de comandos (--auto) para Tareas Programadas / Automatización por scripts
        if (args.Any(a => a.Equals("--auto", StringComparison.OrdinalIgnoreCase)))
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("[MODO AUTOMÁTICO CLI] Procesando archivos de eventos y generando reportes...");
            Console.ResetColor();

            var resumenAuto = automatizacion.EjecutarFlujoCompleto("Ejecución desatendida por argumento CLI (--auto)");
            ImprimirResumenConsola(resumenAuto);
            return;
        }

        // Ejecutar análisis inicial al abrir el programa
        var resumenInicial = automatizacion.EjecutarFlujoCompleto("Inicio del sistema");

        // Modo vigilancia directa por argumento (--watch)
        if (args.Any(a => a.Equals("--watch", StringComparison.OrdinalIgnoreCase)))
        {
            EjecutarModoVigilanciaInteractivo(automatizacion, lector, carpetaEntrada);
            return;
        }

        // Menú interactivo principal
        bool salir = false;
        while (!salir)
        {
            MostrarEncabezado(carpetaEntrada, carpetaReportes, automatizacion.UltimoResumen);
            Console.WriteLine("  [1] Generar reportes organizados ahora (HTML, TXT y CSV)");
            Console.WriteLine("  [2] Iniciar Modo Automatización en Tiempo Real (Vigilancia de carpeta)");
            Console.WriteLine("  [3] Simular llegada de nuevo archivo de eventos y actualizar reporte");
            Console.WriteLine("  [4] Registrar un evento de seguridad manualmente");
            Console.WriteLine("  [5] Ver resumen detallado y alertas de correlación en consola");
            Console.WriteLine("  [6] Abrir el último reporte HTML en el navegador");
            Console.WriteLine("  [0] Salir");
            Console.Write("\nSeleccione una opción: ");

            string? opcion = Console.ReadLine()?.Trim();
            Console.WriteLine();

            switch (opcion)
            {
                case "1":
                    var resumen = automatizacion.EjecutarFlujoCompleto("Solicitud manual desde el menú");
                    MostrarConfirmacionReporte(resumen);
                    Pausa();
                    break;

                case "2":
                    EjecutarModoVigilanciaInteractivo(automatizacion, lector, carpetaEntrada);
                    break;

                case "3":
                    string nuevoArchivo = lector.SimularNuevoArchivoEventos();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✔ Se creó un nuevo archivo de eventos: {Path.GetFileName(nuevoArchivo)}");
                    Console.ResetColor();
                    var resumenSim = automatizacion.EjecutarFlujoCompleto($"Nuevo archivo simulado ({Path.GetFileName(nuevoArchivo)})");
                    MostrarConfirmacionReporte(resumenSim);
                    Pausa();
                    break;

                case "4":
                    RegistrarEventoDesdeConsola(lector, automatizacion);
                    Pausa();
                    break;

                case "5":
                    var actual = automatizacion.UltimoResumen ?? resumenInicial;
                    ImprimirResumenConsola(actual);
                    Pausa();
                    break;

                case "6":
                    AbrirReporteEnNavegador(automatizacion.UltimoResumen?.RutaHtmlUltimo);
                    Pausa();
                    break;

                case "0":
                    salir = true;
                    break;

                default:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Opción no válida. Intente nuevamente.");
                    Console.ResetColor();
                    Thread.Sleep(900);
                    break;
            }
        }
    }

    private static void MostrarEncabezado(string carpetaEntrada, string carpetaReportes, ResumenReporte? ultimo)
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║        🛡️  GENERADOR AUTOMATIZADO DE REPORTES DE SEGURIDAD (C# .NET)         ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine($"  📂 Carpeta de Entrada  : {carpetaEntrada}");
        Console.WriteLine($"  📊 Carpeta de Reportes : {carpetaReportes}");

        if (ultimo != null)
        {
            Console.Write("  📌 Último Estado       : ");
            Console.ForegroundColor = ultimo.IndiceRiesgo >= 75 ? ConsoleColor.Red :
                                      ultimo.IndiceRiesgo >= 50 ? ConsoleColor.Yellow : ConsoleColor.Green;
            Console.WriteLine($"{ultimo.TotalEventos} eventos | Riesgo {ultimo.IndiceRiesgo}/100 ({ultimo.EstadoRiesgoGlobal})");
            Console.ResetColor();
        }
        Console.WriteLine(new string('─', 80));
    }

    private static void EjecutarModoVigilanciaInteractivo(
        AutomatizacionService automatizacion,
        LectorEventosService lector,
        string carpetaEntrada)
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("════════════════════════════════════════════════════════════════════════════════");
        Console.WriteLine("   🤖 MODO AUTOMATIZACIÓN EN TIEMPO REAL ACTIVO (FileSystemWatcher)");
        Console.WriteLine("════════════════════════════════════════════════════════════════════════════════");
        Console.ResetColor();
        Console.WriteLine($"Vigilando carpeta: {carpetaEntrada}");
        Console.WriteLine("Cualquier archivo (.csv, .json, .txt, .log) que se agregue o modifique");
        Console.WriteLine("disparará automáticamente el análisis y la actualización de los reportes.\n");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Controles en vivo:");
        Console.WriteLine("  [S] Simular llegada de un nuevo archivo de eventos ahora mismo");
        Console.WriteLine("  [Q] o [ENTER] Detener vigilancia y volver al menú principal");
        Console.ResetColor();
        Console.WriteLine(new string('─', 80));

        automatizacion.IniciarVigilanciaCarpeta((resumen, motivo) =>
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"⚡ [{DateTime.Now:HH:mm:ss}] EVENTO AUTOMÁTICO: {motivo}");
            Console.ResetColor();
            Console.WriteLine($"   -> Total eventos procesados : {resumen.TotalEventos} (Críticos: {resumen.TotalCriticos}, Altos: {resumen.TotalAltos})");
            Console.WriteLine($"   -> Nivel de Riesgo Global   : {resumen.IndiceRiesgo}/100 ({resumen.EstadoRiesgoGlobal})");
            Console.WriteLine($"   -> Reporte actualizado      : {Path.GetFileName(resumen.RutaHtmlUltimo)}");
        });

        while (true)
        {
            var tecla = Console.ReadKey(intercept: true);
            if (tecla.Key == ConsoleKey.S)
            {
                string rutaSimulada = lector.SimularNuevoArchivoEventos();
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Archivo depositado en carpeta: {Path.GetFileName(rutaSimulada)}...");
                Console.ResetColor();
            }
            else if (tecla.Key is ConsoleKey.Q or ConsoleKey.Enter or ConsoleKey.Escape)
            {
                break;
            }
        }

        automatizacion.DetenerVigilanciaCarpeta();
    }

    private static void RegistrarEventoDesdeConsola(LectorEventosService lector, AutomatizacionService automatizacion)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("─── REGISTRO MANUAL DE EVENTO DE SEGURIDAD ───");
        Console.ResetColor();

        Console.Write("Tipo de Evento (ej. Login Fallido, Malware, Acceso No Autorizado): ");
        string tipo = Console.ReadLine()?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(tipo)) tipo = "Incidente Reportado Manualmente";

        Console.Write("Severidad (Baja / Media / Alta / Critica) [Alta]: ");
        string sevTexto = Console.ReadLine()?.Trim() ?? "Alta";
        NivelSeveridad severidad = EventoSeguridad.ParsearSeveridad(string.IsNullOrWhiteSpace(sevTexto) ? "Alta" : sevTexto);

        Console.Write("IP de Origen [192.168.1.100]: ");
        string ip = Console.ReadLine()?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(ip)) ip = "192.168.1.100";

        Console.Write("Usuario involucrado [admin]: ");
        string usuario = Console.ReadLine()?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(usuario)) usuario = "admin";

        Console.Write("Servidor o Activo afectado [SRV-PRINCIPAL]: ");
        string servidor = Console.ReadLine()?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(servidor)) servidor = "SRV-PRINCIPAL";

        Console.Write("Estado (Bloqueado / Alerta Activa / En Investigación / Resuelto) [Alerta Activa]: ");
        string estado = Console.ReadLine()?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(estado)) estado = "Alerta Activa";

        Console.Write("Descripción del incidente: ");
        string desc = Console.ReadLine()?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(desc)) desc = "Incidente registrado por el operador de seguridad.";

        var nuevoEvento = new EventoSeguridad
        {
            Id = $"MAN-{DateTime.Now:HHmmss}",
            FechaHora = DateTime.Now,
            TipoEvento = tipo,
            Severidad = severidad,
            OrigenIp = ip,
            Usuario = usuario,
            Servidor = servidor,
            Estado = estado,
            Descripcion = desc
        };

        string rutaGuardado = lector.RegistrarEventoManual(nuevoEvento);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n✔ Evento guardado en '{Path.GetFileName(rutaGuardado)}'. Regenerando reportes...");
        Console.ResetColor();

        var resumen = automatizacion.EjecutarFlujoCompleto($"Registro manual de evento {nuevoEvento.Id}");
        MostrarConfirmacionReporte(resumen);
    }

    private static void MostrarConfirmacionReporte(ResumenReporte r)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n✔ ¡Reportes generados y organizados automáticamente!");
        Console.ResetColor();
        Console.WriteLine($"  • Eventos procesados : {r.TotalEventos} (de {r.TotalArchivosProcesados} archivo(s))");
        Console.WriteLine($"  • Críticos: {r.TotalCriticos} | Altos: {r.TotalAltos} | Medios: {r.TotalMedios} | Bajos: {r.TotalBajos}");
        Console.WriteLine($"  • Reporte HTML       : {r.RutaHtmlUltimo}");
        Console.WriteLine($"  • Reporte TXT        : {r.RutaTxt}");
        Console.WriteLine($"  • Reporte CSV        : {r.RutaCsv}");
    }

    private static void ImprimirResumenConsola(ResumenReporte r)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("════════════════════════════════════════════════════════════════════════════════");
        Console.WriteLine($"  RESUMEN EJECUTIVO DE SEGURIDAD ({r.FechaGeneracion:dd/MM/yyyy HH:mm:ss})");
        Console.WriteLine("════════════════════════════════════════════════════════════════════════════════");
        Console.ResetColor();

        Console.WriteLine($"Archivos leídos : {r.TotalArchivosProcesados} | Total Eventos : {r.TotalEventos}");
        Console.WriteLine($"Índice Riesgo   : {r.IndiceRiesgo}/100 -> {r.EstadoRiesgoGlobal}");
        Console.WriteLine($"Severidades     : [Crítica: {r.TotalCriticos}] [Alta: {r.TotalAltos}] [Media: {r.TotalMedios}] [Baja: {r.TotalBajos}]");
        Console.WriteLine(new string('─', 80));

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"ALERTAS DE CORRELACIÓN DETECTADAS ({r.AlertasDetectadas.Count}):");
        Console.ResetColor();

        foreach (var alerta in r.AlertasDetectadas)
        {
            Console.ForegroundColor = alerta.Nivel == NivelSeveridad.Critica ? ConsoleColor.Red : ConsoleColor.DarkYellow;
            Console.WriteLine($"  • [{alerta.Nivel.ToString().ToUpperInvariant()}] {alerta.Titulo}");
            Console.ResetColor();
            Console.WriteLine($"    Detalle: {alerta.Detalle}");
            Console.WriteLine($"    Acción : {alerta.Recomendacion}");
        }

        Console.WriteLine(new string('─', 80));
        Console.WriteLine("ÚLTIMOS EVENTOS PRIORITARIOS:");
        foreach (var ev in r.EventosOrdenados.Take(8))
        {
            Console.ForegroundColor = ev.Severidad switch
            {
                NivelSeveridad.Critica => ConsoleColor.Red,
                NivelSeveridad.Alta => ConsoleColor.Yellow,
                NivelSeveridad.Media => ConsoleColor.Cyan,
                _ => ConsoleColor.Gray
            };
            Console.Write($"  [{ev.Severidad,-7}] ");
            Console.ResetColor();
            Console.WriteLine($"{ev.FechaHora:yyyy-MM-dd HH:mm} | {ev.Id,-8} | {ev.OrigenIp,-15} | {ev.Servidor,-15} | {ev.TipoEvento}");
        }
    }

    private static void AbrirReporteEnNavegador(string? rutaHtml)
    {
        if (string.IsNullOrWhiteSpace(rutaHtml) || !File.Exists(rutaHtml))
        {
            Console.WriteLine("Aún no existe un reporte HTML generado.");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = rutaHtml,
                UseShellExecute = true
            });
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✔ Abriendo '{Path.GetFileName(rutaHtml)}' en su navegador predeterminado...");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"No se pudo abrir automáticamente el navegador: {ex.Message}");
            Console.WriteLine($"Puede abrir manualmente el archivo en: {rutaHtml}");
        }
    }

    private static void Pausa()
    {
        Console.WriteLine("\nPresione cualquier tecla para continuar...");
        Console.ReadKey(intercept: true);
    }
}

