namespace GeneradorReportesSeguridad.Models;

/// <summary>
/// Representa un hallazgo o alerta automática detectada por correlación de eventos.
/// </summary>
public class AlertaCorrelacion
{
    public string Titulo { get; set; } = string.Empty;
    public NivelSeveridad Nivel { get; set; }
    public string Detalle { get; set; } = string.Empty;
    public string Recomendacion { get; set; } = string.Empty;
}

/// <summary>
/// Contiene toda la información organizada y las métricas calculadas para el reporte de seguridad.
/// </summary>
public class ResumenReporte
{
    public DateTime FechaGeneracion { get; set; } = DateTime.Now;
    public int TotalEventos { get; set; }
    public int TotalArchivosProcesados { get; set; }
    public DateTime? FechaPrimerEvento { get; set; }
    public DateTime? FechaUltimoEvento { get; set; }

    // Conteo por nivel de severidad
    public int TotalCriticos { get; set; }
    public int TotalAltos { get; set; }
    public int TotalMedios { get; set; }
    public int TotalBajos { get; set; }

    // Índice de riesgo general (0 - 100)
    public int IndiceRiesgo { get; set; }
    public string EstadoRiesgoGlobal { get; set; } = "Bajo";

    // Estadísticas agrupadas
    public Dictionary<string, int> EventosPorTipo { get; set; } = new();
    public Dictionary<string, int> EventosPorEstado { get; set; } = new();
    public List<(string Ip, int Cantidad, int CriticosYAltos)> TopIpsOrigen { get; set; } = new();
    public List<(string Usuario, int Cantidad)> TopUsuariosAfectados { get; set; } = new();
    public List<(string Servidor, int Cantidad)> TopServidoresAfectados { get; set; } = new();

    // Alertas automáticas detectadas por reglas heurísticas
    public List<AlertaCorrelacion> AlertasDetectadas { get; set; } = new();

    // Lista completa ordenada (primero por severidad descendente, luego por fecha descendente)
    public List<EventoSeguridad> EventosOrdenados { get; set; } = new();

    // Rutas de los archivos generados
    public string RutaHtml { get; set; } = string.Empty;
    public string RutaHtmlUltimo { get; set; } = string.Empty;
    public string RutaTxt { get; set; } = string.Empty;
    public string RutaCsv { get; set; } = string.Empty;
}

