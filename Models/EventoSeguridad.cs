using System.Text.Json.Serialization;

namespace GeneradorReportesSeguridad.Models;

/// <summary>
/// Niveles de severidad para clasificar los eventos de seguridad.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NivelSeveridad
{
    Baja = 1,
    Media = 2,
    Alta = 3,
    Critica = 4
}

/// <summary>
/// Representa un evento individual de seguridad capturado desde archivos de log, CSV o JSON.
/// </summary>
public class EventoSeguridad
{
    public string Id { get; set; } = string.Empty;
    public DateTime FechaHora { get; set; }
    public string TipoEvento { get; set; } = string.Empty;
    public NivelSeveridad Severidad { get; set; } = NivelSeveridad.Baja;
    public string OrigenIp { get; set; } = string.Empty;
    public string Usuario { get; set; } = string.Empty;
    public string Servidor { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string ArchivoOrigen { get; set; } = string.Empty;

    /// <summary>
    /// Convierte un texto libre de severidad al enum correspondiente.
    /// </summary>
    public static NivelSeveridad ParsearSeveridad(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return NivelSeveridad.Baja;

        string limpio = valor.Trim().ToUpperInvariant()
            .Replace("Í", "I")
            .Replace("Á", "A");

        return limpio switch
        {
            "CRITICA" or "CRITICO" or "CRITICAL" or "4" => NivelSeveridad.Critica,
            "ALTA" or "ALTO" or "HIGH" or "3" => NivelSeveridad.Alta,
            "MEDIA" or "MEDIO" or "MEDIUM" or "WARNING" or "2" => NivelSeveridad.Media,
            _ => NivelSeveridad.Baja
        };
    }
}

