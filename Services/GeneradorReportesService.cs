using System.Net;
using System.Text;
using GeneradorReportesSeguridad.Models;

namespace GeneradorReportesSeguridad.Services;

/// <summary>
/// Servicio que exporta el ResumenReporte en múltiples formatos organizados: HTML, TXT y CSV.
/// </summary>
public class GeneradorReportesService
{
    private readonly string _directorioSalida;

    public GeneradorReportesService(string directorioSalida)
    {
        _directorioSalida = directorioSalida;
        Directory.CreateDirectory(_directorioSalida);
    }

    /// <summary>
    /// Genera automáticamente los tres formatos de reporte (HTML, TXT y CSV) y actualiza los archivos "_Ultimo".
    /// </summary>
    public void GenerarTodosLosReportes(ResumenReporte resumen)
    {
        Directory.CreateDirectory(_directorioSalida);
        string marcaTiempo = resumen.FechaGeneracion.ToString("yyyyMMdd_HHmmss");

        // 1. Generar HTML
        string contenidoHtml = ConstruirHtml(resumen);
        resumen.RutaHtml = Path.Combine(_directorioSalida, $"Reporte_Seguridad_{marcaTiempo}.html");
        resumen.RutaHtmlUltimo = Path.Combine(_directorioSalida, "Reporte_Seguridad_Ultimo.html");
        File.WriteAllText(resumen.RutaHtml, contenidoHtml, Encoding.UTF8);
        File.WriteAllText(resumen.RutaHtmlUltimo, contenidoHtml, Encoding.UTF8);

        // 2. Generar TXT
        string contenidoTxt = ConstruirTxt(resumen);
        resumen.RutaTxt = Path.Combine(_directorioSalida, $"Reporte_Seguridad_{marcaTiempo}.txt");
        string rutaTxtUltimo = Path.Combine(_directorioSalida, "Reporte_Seguridad_Ultimo.txt");
        File.WriteAllText(resumen.RutaTxt, contenidoTxt, Encoding.UTF8);
        File.WriteAllText(rutaTxtUltimo, contenidoTxt, Encoding.UTF8);

        // 3. Generar CSV consolidado (UTF-8 con BOM para compatibilidad directa con Excel)
        string contenidoCsv = ConstruirCsv(resumen);
        var utf8ConBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        resumen.RutaCsv = Path.Combine(_directorioSalida, $"Reporte_Seguridad_{marcaTiempo}.csv");
        string rutaCsvUltimo = Path.Combine(_directorioSalida, "Reporte_Seguridad_Ultimo.csv");
        File.WriteAllText(resumen.RutaCsv, contenidoCsv, utf8ConBom);
        File.WriteAllText(rutaCsvUltimo, contenidoCsv, utf8ConBom);
    }

    private static string ConstruirHtml(ResumenReporte r)
    {
        var sb = new StringBuilder();
        string colorRiesgo = r.IndiceRiesgo switch
        {
            >= 75 => "#ef4444",
            >= 50 => "#f97316",
            >= 25 => "#eab308",
            _ => "#22c55e"
        };

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"es\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\" />");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />");
        sb.AppendLine($"  <title>Reporte de Seguridad SOC - {r.FechaGeneracion:yyyy-MM-dd HH:mm:ss}</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine("""
            :root {
              --bg: #0b1120;
              --panel: #151f32;
              --panel-alt: #1e293b;
              --border: #334155;
              --text: #f8fafc;
              --muted: #94a3b8;
              --critica: #ef4444;
              --alta: #f97316;
              --media: #eab308;
              --baja: #22c55e;
              --accent: #38bdf8;
            }
            * { box-sizing: border-box; margin: 0; padding: 0; }
            body {
              font-family: 'Segoe UI', system-ui, -apple-system, sans-serif;
              background-color: var(--bg);
              color: var(--text);
              line-height: 1.5;
              padding: 28px;
            }
            .container { max-width: 1360px; margin: 0 auto; }
            header {
              display: flex;
              flex-wrap: wrap;
              justify-content: space-between;
              align-items: center;
              background: linear-gradient(135deg, #151f32 0%, #0f172a 100%);
              border: 1px solid var(--border);
              border-left: 5px solid var(--accent);
              border-radius: 12px;
              padding: 22px 26px;
              margin-bottom: 24px;
              gap: 16px;
            }
            header h1 { font-size: 1.55rem; letter-spacing: 0.3px; }
            header p { color: var(--muted); font-size: 0.92rem; margin-top: 4px; }
            .risk-box {
              text-align: right;
              padding: 12px 18px;
              border-radius: 10px;
              background: rgba(15, 23, 42, 0.75);
              border: 1px solid var(--border);
            }
            .risk-score { font-size: 1.8rem; font-weight: 800; }
            .kpi-grid {
              display: grid;
              grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
              gap: 16px;
              margin-bottom: 24px;
            }
            .kpi-card {
              background: var(--panel);
              border: 1px solid var(--border);
              border-radius: 10px;
              padding: 18px 20px;
              position: relative;
              overflow: hidden;
            }
            .kpi-card .label { color: var(--muted); font-size: 0.85rem; text-transform: uppercase; letter-spacing: 0.6px; }
            .kpi-card .value { font-size: 2rem; font-weight: 700; margin-top: 6px; }
            .kpi-card.critica { border-top: 4px solid var(--critica); }
            .kpi-card.alta { border-top: 4px solid var(--alta); }
            .kpi-card.media { border-top: 4px solid var(--media); }
            .kpi-card.baja { border-top: 4px solid var(--baja); }
            .kpi-card.total { border-top: 4px solid var(--accent); }
            .section {
              background: var(--panel);
              border: 1px solid var(--border);
              border-radius: 12px;
              padding: 22px;
              margin-bottom: 24px;
            }
            .section h2 {
              font-size: 1.15rem;
              margin-bottom: 16px;
              padding-bottom: 10px;
              border-bottom: 1px solid var(--border);
              color: #e2e8f0;
            }
            .grid-2 {
              display: grid;
              grid-template-columns: repeat(auto-fit, minmax(380px, 1fr));
              gap: 20px;
              margin-bottom: 24px;
            }
            .alert-item {
              background: rgba(239, 68, 68, 0.08);
              border: 1px solid rgba(239, 68, 68, 0.35);
              border-left: 4px solid var(--critica);
              border-radius: 8px;
              padding: 14px 16px;
              margin-bottom: 12px;
            }
            .alert-item.alta {
              background: rgba(249, 115, 22, 0.08);
              border-color: rgba(249, 115, 22, 0.35);
              border-left-color: var(--alta);
            }
            .alert-title { font-weight: 700; font-size: 0.98rem; margin-bottom: 4px; }
            .alert-detail { color: #cbd5e1; font-size: 0.9rem; margin-bottom: 6px; }
            .alert-rec { color: var(--accent); font-size: 0.86rem; font-weight: 500; }
            .bar-row { margin-bottom: 12px; }
            .bar-header { display: flex; justify-content: space-between; font-size: 0.88rem; margin-bottom: 4px; }
            .bar-track { background: var(--panel-alt); height: 10px; border-radius: 6px; overflow: hidden; }
            .bar-fill { background: linear-gradient(90deg, #0ea5e9, #38bdf8); height: 100%; border-radius: 6px; }
            .controls {
              display: flex;
              flex-wrap: wrap;
              gap: 12px;
              margin-bottom: 16px;
              align-items: center;
              justify-content: space-between;
            }
            .search-input {
              flex: 1;
              min-width: 260px;
              background: var(--bg);
              border: 1px solid var(--border);
              color: var(--text);
              padding: 9px 14px;
              border-radius: 8px;
              font-size: 0.92rem;
            }
            .filter-btns { display: flex; gap: 8px; flex-wrap: wrap; }
            .filter-btn {
              background: var(--panel-alt);
              border: 1px solid var(--border);
              color: var(--text);
              padding: 7px 14px;
              border-radius: 8px;
              cursor: pointer;
              font-size: 0.85rem;
              transition: 0.15s;
            }
            .filter-btn.active, .filter-btn:hover {
              background: var(--accent);
              color: #0f172a;
              border-color: var(--accent);
              font-weight: 600;
            }
            .table-wrapper { overflow-x: auto; }
            table { width: 100%; border-collapse: collapse; font-size: 0.88rem; }
            th, td {
              padding: 11px 12px;
              text-align: left;
              border-bottom: 1px solid var(--border);
            }
            th {
              background: var(--panel-alt);
              color: #cbd5e1;
              font-weight: 600;
              text-transform: uppercase;
              font-size: 0.78rem;
              letter-spacing: 0.5px;
            }
            tr:hover td { background: rgba(255, 255, 255, 0.03); }
            .badge {
              display: inline-block;
              padding: 3px 9px;
              border-radius: 999px;
              font-size: 0.75rem;
              font-weight: 700;
              text-transform: uppercase;
            }
            .badge.Critica { background: rgba(239, 68, 68, 0.2); color: #fca5a5; border: 1px solid #ef4444; }
            .badge.Alta { background: rgba(249, 115, 22, 0.2); color: #fdba74; border: 1px solid #f97316; }
            .badge.Media { background: rgba(234, 179, 8, 0.2); color: #fde047; border: 1px solid #eab308; }
            .badge.Baja { background: rgba(34, 197, 94, 0.2); color: #86efac; border: 1px solid #22c55e; }
            .mono { font-family: 'Consolas', 'Courier New', monospace; color: #bae6fd; }
            footer { text-align: center; color: var(--muted); font-size: 0.82rem; margin-top: 20px; }
            """);
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <div class=\"container\">");

        // Encabezado
        sb.AppendLine("    <header>");
        sb.AppendLine("      <div>");
        sb.AppendLine("        <h1>🛡️ Reporte Automatizado de Incidentes y Eventos de Seguridad</h1>");
        sb.AppendLine($"        <p>Generado automáticamente el <strong>{r.FechaGeneracion:dd/MM/yyyy HH:mm:ss}</strong> | Archivos analizados: <strong>{r.TotalArchivosProcesados}</strong> | Rango: <strong>{(r.FechaPrimerEvento?.ToString("dd/MM/yyyy HH:mm") ?? "N/A")}</strong> a <strong>{(r.FechaUltimoEvento?.ToString("dd/MM/yyyy HH:mm") ?? "N/A")}</strong></p>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"risk-box\">");
        sb.AppendLine($"        <div class=\"risk-score\" style=\"color: {colorRiesgo};\">{r.IndiceRiesgo}/100</div>");
        sb.AppendLine($"        <div style=\"font-size: 0.82rem; color: {colorRiesgo}; font-weight: 600;\">{Escapar(r.EstadoRiesgoGlobal)}</div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </header>");

        // Tarjetas KPI
        sb.AppendLine("    <div class=\"kpi-grid\">");
        sb.AppendLine($"      <div class=\"kpi-card total\"><div class=\"label\">Total Eventos</div><div class=\"value\">{r.TotalEventos}</div></div>");
        sb.AppendLine($"      <div class=\"kpi-card critica\"><div class=\"label\">Severidad Crítica</div><div class=\"value\" style=\"color:var(--critica)\">{r.TotalCriticos}</div></div>");
        sb.AppendLine($"      <div class=\"kpi-card alta\"><div class=\"label\">Severidad Alta</div><div class=\"value\" style=\"color:var(--alta)\">{r.TotalAltos}</div></div>");
        sb.AppendLine($"      <div class=\"kpi-card media\"><div class=\"label\">Severidad Media</div><div class=\"value\" style=\"color:var(--media)\">{r.TotalMedios}</div></div>");
        sb.AppendLine($"      <div class=\"kpi-card baja\"><div class=\"label\">Severidad Baja</div><div class=\"value\" style=\"color:var(--baja)\">{r.TotalBajos}</div></div>");
        sb.AppendLine("    </div>");

        // Alertas de Correlación Automática
        sb.AppendLine("    <div class=\"section\">");
        sb.AppendLine("      <h2>🚨 Hallazgos de Correlación y Recomendaciones Automáticas</h2>");
        if (r.AlertasDetectadas.Count == 0)
        {
            sb.AppendLine("      <p style=\"color: var(--baja);\">No se detectaron patrones críticos de ataque correlacionados en este lote de eventos.</p>");
        }
        else
        {
            foreach (var alerta in r.AlertasDetectadas)
            {
                string claseNivel = alerta.Nivel == NivelSeveridad.Critica ? "critica" : "alta";
                sb.AppendLine($"      <div class=\"alert-item {claseNivel}\">");
                sb.AppendLine($"        <div class=\"alert-title\">[{alerta.Nivel.ToString().ToUpperInvariant()}] {Escapar(alerta.Titulo)}</div>");
                sb.AppendLine($"        <div class=\"alert-detail\">{Escapar(alerta.Detalle)}</div>");
                sb.AppendLine($"        <div class=\"alert-rec\">💡 Recomendación: {Escapar(alerta.Recomendacion)}</div>");
                sb.AppendLine("      </div>");
            }
        }
        sb.AppendLine("    </div>");

        // Gráficos de distribución en 2 columnas
        sb.AppendLine("    <div class=\"grid-2\">");

        // Columna 1: Eventos por Tipo
        sb.AppendLine("      <div class=\"section\" style=\"margin-bottom:0;\">");
        sb.AppendLine("        <h2>📊 Distribución por Tipo de Evento</h2>");
        int maxTipo = r.EventosPorTipo.Values.DefaultIfEmpty(1).Max();
        foreach (var kv in r.EventosPorTipo)
        {
            int porcentaje = (int)Math.Round((kv.Value * 100.0) / Math.Max(1, maxTipo));
            sb.AppendLine("        <div class=\"bar-row\">");
            sb.AppendLine($"          <div class=\"bar-header\"><span>{Escapar(kv.Key)}</span><strong>{kv.Value} evento(s)</strong></div>");
            sb.AppendLine($"          <div class=\"bar-track\"><div class=\"bar-fill\" style=\"width:{porcentaje}%;\"></div></div>");
            sb.AppendLine("        </div>");
        }
        sb.AppendLine("      </div>");

        // Columna 2: Top IPs de Origen y Servidores Afectados
        sb.AppendLine("      <div class=\"section\" style=\"margin-bottom:0;\">");
        sb.AppendLine("        <h2>🌐 Principales IPs de Origen y Activos Afectados</h2>");
        sb.AppendLine("        <table>");
        sb.AppendLine("          <thead><tr><th>IP Origen</th><th>Total Eventos</th><th>Críticos / Altos</th></tr></thead>");
        sb.AppendLine("          <tbody>");
        foreach (var ip in r.TopIpsOrigen.Take(5))
        {
            sb.AppendLine($"            <tr><td class=\"mono\">{Escapar(ip.Ip)}</td><td>{ip.Cantidad}</td><td style=\"color:#fca5a5;font-weight:600;\">{ip.CriticosYAltos}</td></tr>");
        }
        sb.AppendLine("          </tbody>");
        sb.AppendLine("        </table>");
        sb.AppendLine("      </div>");

        sb.AppendLine("    </div>");

        // Tabla interactiva de todos los eventos organizados
        sb.AppendLine("    <div class=\"section\">");
        sb.AppendLine("      <h2>📋 Registro Detallado y Organizado de Eventos de Seguridad</h2>");
        sb.AppendLine("      <div class=\"controls\">");
        sb.AppendLine("        <input type=\"text\" id=\"buscador\" class=\"search-input\" placeholder=\"🔍 Filtrar por IP, usuario, servidor, tipo o descripción...\" onkeyup=\"filtrarTabla()\" />");
        sb.AppendLine("        <div class=\"filter-btns\">");
        sb.AppendLine("          <button class=\"filter-btn active\" onclick=\"filtrarSeveridad('TODOS', this)\">Todos</button>");
        sb.AppendLine("          <button class=\"filter-btn\" onclick=\"filtrarSeveridad('Critica', this)\">Crítica</button>");
        sb.AppendLine("          <button class=\"filter-btn\" onclick=\"filtrarSeveridad('Alta', this)\">Alta</button>");
        sb.AppendLine("          <button class=\"filter-btn\" onclick=\"filtrarSeveridad('Media', this)\">Media</button>");
        sb.AppendLine("          <button class=\"filter-btn\" onclick=\"filtrarSeveridad('Baja', this)\">Baja</button>");
        sb.AppendLine("        </div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"table-wrapper\">");
        sb.AppendLine("        <table id=\"tablaEventos\">");
        sb.AppendLine("          <thead>");
        sb.AppendLine("            <tr>");
        sb.AppendLine("              <th>ID</th>");
        sb.AppendLine("              <th>Fecha y Hora</th>");
        sb.AppendLine("              <th>Severidad</th>");
        sb.AppendLine("              <th>Tipo de Evento</th>");
        sb.AppendLine("              <th>IP Origen</th>");
        sb.AppendLine("              <th>Usuario</th>");
        sb.AppendLine("              <th>Servidor / Activo</th>");
        sb.AppendLine("              <th>Estado</th>");
        sb.AppendLine("              <th>Descripción</th>");
        sb.AppendLine("              <th>Fuente</th>");
        sb.AppendLine("            </tr>");
        sb.AppendLine("          </thead>");
        sb.AppendLine("          <tbody>");

        foreach (var ev in r.EventosOrdenados)
        {
            sb.AppendLine($"            <tr data-severidad=\"{ev.Severidad}\">");
            sb.AppendLine($"              <td class=\"mono\">{Escapar(ev.Id)}</td>");
            sb.AppendLine($"              <td>{ev.FechaHora:yyyy-MM-dd HH:mm:ss}</td>");
            sb.AppendLine($"              <td><span class=\"badge {ev.Severidad}\">{ev.Severidad}</span></td>");
            sb.AppendLine($"              <td><strong>{Escapar(ev.TipoEvento)}</strong></td>");
            sb.AppendLine($"              <td class=\"mono\">{Escapar(ev.OrigenIp)}</td>");
            sb.AppendLine($"              <td>{Escapar(ev.Usuario)}</td>");
            sb.AppendLine($"              <td class=\"mono\">{Escapar(ev.Servidor)}</td>");
            sb.AppendLine($"              <td>{Escapar(ev.Estado)}</td>");
            sb.AppendLine($"              <td>{Escapar(ev.Descripcion)}</td>");
            sb.AppendLine($"              <td style=\"color:var(--muted);font-size:0.8rem;\">{Escapar(ev.ArchivoOrigen)}</td>");
            sb.AppendLine("            </tr>");
        }

        sb.AppendLine("          </tbody>");
        sb.AppendLine("        </table>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");

        sb.AppendLine("    <footer>Generador Automatizado de Reportes de Seguridad en C# (.NET)</footer>");
        sb.AppendLine("  </div>");

        // Script para filtrado interactivo
        sb.AppendLine("""
          <script>
            let severidadActual = 'TODOS';
            function filtrarSeveridad(sev, btn) {
              severidadActual = sev;
              document.querySelectorAll('.filter-btn').forEach(b => b.classList.remove('active'));
              btn.classList.add('active');
              filtrarTabla();
            }
            function filtrarTabla() {
              const texto = document.getElementById('buscador').value.toLowerCase();
              const filas = document.querySelectorAll('#tablaEventos tbody tr');
              filas.forEach(fila => {
                const sevFila = fila.getAttribute('data-severidad');
                const coincideSev = (severidadActual === 'TODOS' || sevFila === severidadActual);
                const coincideTexto = fila.innerText.toLowerCase().includes(texto);
                fila.style.display = (coincideSev && coincideTexto) ? '' : 'none';
              });
            }
          </script>
        """);

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static string ConstruirTxt(ResumenReporte r)
    {
        var sb = new StringBuilder();
        string separadorDoble = new('=', 95);
        string separadorSimple = new('-', 95);

        sb.AppendLine(separadorDoble);
        sb.AppendLine("                REPORTE EJECUTIVO AUTOMATIZADO DE SEGURIDAD INFORMÁTICA");
        sb.AppendLine(separadorDoble);
        sb.AppendLine($"Fecha de Generación      : {r.FechaGeneracion:dd/MM/yyyy HH:mm:ss}");
        sb.AppendLine($"Archivos Procesados      : {r.TotalArchivosProcesados}");
        sb.AppendLine($"Total de Eventos         : {r.TotalEventos}");
        sb.AppendLine($"Periodo Analizado        : {(r.FechaPrimerEvento?.ToString("dd/MM/yyyy HH:mm:ss") ?? "N/A")} -> {(r.FechaUltimoEvento?.ToString("dd/MM/yyyy HH:mm:ss") ?? "N/A")}");
        sb.AppendLine($"Índice de Riesgo Global  : {r.IndiceRiesgo}/100 ({r.EstadoRiesgoGlobal})");
        sb.AppendLine(separadorSimple);

        sb.AppendLine("1. RESUMEN POR NIVEL DE SEVERIDAD");
        sb.AppendLine(separadorSimple);
        sb.AppendLine($"  * CRÍTICA : {r.TotalCriticos,4} evento(s)");
        sb.AppendLine($"  * ALTA    : {r.TotalAltos,4} evento(s)");
        sb.AppendLine($"  * MEDIA   : {r.TotalMedios,4} evento(s)");
        sb.AppendLine($"  * BAJA    : {r.TotalBajos,4} evento(s)");
        sb.AppendLine(separadorSimple);

        sb.AppendLine("2. ALERTAS DE CORRELACIÓN Y RECOMENDACIONES AUTOMÁTICAS");
        sb.AppendLine(separadorSimple);
        if (r.AlertasDetectadas.Count == 0)
        {
            sb.AppendLine("  No se detectaron alertas críticas de correlación en este corte.");
        }
        else
        {
            int idx = 1;
            foreach (var alerta in r.AlertasDetectadas)
            {
                sb.AppendLine($"  [{idx}] [{alerta.Nivel.ToString().ToUpperInvariant()}] {alerta.Titulo}");
                sb.AppendLine($"      Detalle       : {alerta.Detalle}");
                sb.AppendLine($"      Recomendación : {alerta.Recomendacion}");
                sb.AppendLine();
                idx++;
            }
        }
        sb.AppendLine(separadorSimple);

        sb.AppendLine("3. ESTADÍSTICAS POR TIPO DE EVENTO Y ORIGEN");
        sb.AppendLine(separadorSimple);
        sb.AppendLine("  Eventos por Categoría:");
        foreach (var kv in r.EventosPorTipo)
        {
            sb.AppendLine($"    - {kv.Key,-35} : {kv.Value} evento(s)");
        }
        sb.AppendLine();
        sb.AppendLine("  Top Direcciones IP de Origen:");
        foreach (var ip in r.TopIpsOrigen)
        {
            sb.AppendLine($"    - {ip.Ip,-20} : {ip.Cantidad} evento(s) ({ip.CriticosYAltos} Críticos/Altos)");
        }
        sb.AppendLine(separadorSimple);

        sb.AppendLine("4. DETALLE ORGANIZADO DE EVENTOS (ORDENADO POR PRIORIDAD Y FECHA)");
        sb.AppendLine(separadorSimple);
        sb.AppendLine($"{"ID",-12} | {"FECHA Y HORA",-19} | {"SEVERIDAD",-9} | {"IP ORIGEN",-15} | {"USUARIO",-14} | {"SERVIDOR",-16} | {"TIPO DE EVENTO"}");
        sb.AppendLine(separadorSimple);

        foreach (var ev in r.EventosOrdenados)
        {
            sb.AppendLine($"{Truncar(ev.Id, 12),-12} | {ev.FechaHora:yyyy-MM-dd HH:mm:ss} | {ev.Severidad,-9} | {Truncar(ev.OrigenIp, 15),-15} | {Truncar(ev.Usuario, 14),-14} | {Truncar(ev.Servidor, 16),-16} | {ev.TipoEvento}");
            sb.AppendLine($"   -> Estado: [{ev.Estado}] | Fuente: {ev.ArchivoOrigen} | Detalle: {ev.Descripcion}");
        }

        sb.AppendLine(separadorDoble);
        sb.AppendLine("Fin del Reporte Automatizado.");
        return sb.ToString();
    }

    private static string ConstruirCsv(ResumenReporte r)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Id,FechaHora,Severidad,TipoEvento,OrigenIp,Usuario,Servidor,Estado,ArchivoOrigen,Descripcion");

        foreach (var ev in r.EventosOrdenados)
        {
            string descEscapada = ev.Descripcion.Replace("\"", "\"\"");
            string tipoEscapado = ev.TipoEvento.Replace("\"", "\"\"");
            sb.AppendLine($"{ev.Id},{ev.FechaHora:yyyy-MM-dd HH:mm:ss},{ev.Severidad},\"{tipoEscapado}\",{ev.OrigenIp},{ev.Usuario},{ev.Servidor},{ev.Estado},{ev.ArchivoOrigen},\"{descEscapada}\"");
        }

        return sb.ToString();
    }

    private static string Escapar(string? texto) => WebUtility.HtmlEncode(texto ?? string.Empty);

    private static string Truncar(string texto, int max) =>
        string.IsNullOrEmpty(texto) ? "" : (texto.Length <= max ? texto : texto[..(max - 2)] + "..");
}

