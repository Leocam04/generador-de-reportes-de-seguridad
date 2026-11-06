using GeneradorReportesSeguridad.Models;

namespace GeneradorReportesSeguridad.Services;

/// <summary>
/// Servicio que procesa, clasifica, correlaciona y organiza los eventos de seguridad en un ResumenReporte.
/// </summary>
public class AnalizadorSeguridadService
{
    public ResumenReporte AnalizarEventos(List<EventoSeguridad> eventos, int archivosProcesados)
    {
        var resumen = new ResumenReporte
        {
            FechaGeneracion = DateTime.Now,
            TotalEventos = eventos.Count,
            TotalArchivosProcesados = archivosProcesados
        };

        if (eventos.Count == 0)
            return resumen;

        // Ordenar eventos: primero por mayor severidad y luego por fecha más reciente
        resumen.EventosOrdenados = eventos
            .OrderByDescending(e => e.Severidad)
            .ThenByDescending(e => e.FechaHora)
            .ToList();

        resumen.FechaPrimerEvento = eventos.Min(e => e.FechaHora);
        resumen.FechaUltimoEvento = eventos.Max(e => e.FechaHora);

        // Conteos por severidad
        resumen.TotalCriticos = eventos.Count(e => e.Severidad == NivelSeveridad.Critica);
        resumen.TotalAltos = eventos.Count(e => e.Severidad == NivelSeveridad.Alta);
        resumen.TotalMedios = eventos.Count(e => e.Severidad == NivelSeveridad.Media);
        resumen.TotalBajos = eventos.Count(e => e.Severidad == NivelSeveridad.Baja);

        // Agrupación por Tipo de Evento
        resumen.EventosPorTipo = eventos
            .GroupBy(e => string.IsNullOrWhiteSpace(e.TipoEvento) ? "No Especificado" : e.TipoEvento)
            .OrderByDescending(g => g.Count())
            .ToDictionary(g => g.Key, g => g.Count());

        // Agrupación por Estado
        resumen.EventosPorEstado = eventos
            .GroupBy(e => string.IsNullOrWhiteSpace(e.Estado) ? "Desconocido" : e.Estado)
            .OrderByDescending(g => g.Count())
            .ToDictionary(g => g.Key, g => g.Count());

        // Top IPs de origen
        resumen.TopIpsOrigen = eventos
            .GroupBy(e => string.IsNullOrWhiteSpace(e.OrigenIp) ? "N/A" : e.OrigenIp)
            .Select(g => (
                Ip: g.Key,
                Cantidad: g.Count(),
                CriticosYAltos: g.Count(x => x.Severidad is NivelSeveridad.Critica or NivelSeveridad.Alta)
            ))
            .OrderByDescending(x => x.Cantidad)
            .ThenByDescending(x => x.CriticosYAltos)
            .Take(8)
            .ToList();

        // Top Usuarios afectados
        resumen.TopUsuariosAfectados = eventos
            .GroupBy(e => string.IsNullOrWhiteSpace(e.Usuario) ? "N/A" : e.Usuario)
            .Select(g => (Usuario: g.Key, Cantidad: g.Count()))
            .OrderByDescending(x => x.Cantidad)
            .Take(8)
            .ToList();

        // Top Servidores afectados
        resumen.TopServidoresAfectados = eventos
            .GroupBy(e => string.IsNullOrWhiteSpace(e.Servidor) ? "N/A" : e.Servidor)
            .Select(g => (Servidor: g.Key, Cantidad: g.Count()))
            .OrderByDescending(x => x.Cantidad)
            .Take(8)
            .ToList();

        // Calcular Índice de Riesgo Global (0 a 100)
        int puntajeBruto = (resumen.TotalCriticos * 18) +
                           (resumen.TotalAltos * 9) +
                           (resumen.TotalMedios * 3) +
                           (resumen.TotalBajos * 1);

        resumen.IndiceRiesgo = Math.Clamp(puntajeBruto, 0, 100);
        resumen.EstadoRiesgoGlobal = resumen.IndiceRiesgo switch
        {
            >= 75 => "CRÍTICO - Acción Inmediata Requerida",
            >= 50 => "ALTO - Atención Prioritaria",
            >= 25 => "MODERADO - Monitoreo Activo",
            _ => "BAJO - Operación Normal"
        };

        // Ejecutar reglas de correlación y detección automática de patrones
        DetectarAlertasCorrelacion(eventos, resumen);

        return resumen;
    }

    private static void DetectarAlertasCorrelacion(List<EventoSeguridad> eventos, ResumenReporte resumen)
    {
        // Regla 1: Múltiples incidentes desde una misma dirección IP (>= 3 eventos)
        var ipsSospechosas = eventos
            .GroupBy(e => e.OrigenIp)
            .Where(g => !string.IsNullOrWhiteSpace(g.Key) && g.Count() >= 3);

        foreach (var grupoIp in ipsSospechosas)
        {
            var tipos = string.Join(", ", grupoIp.Select(x => x.TipoEvento).Distinct());
            resumen.AlertasDetectadas.Add(new AlertaCorrelacion
            {
                Titulo = $"Actividad Hostil Repetitiva desde IP {grupoIp.Key}",
                Nivel = NivelSeveridad.Critica,
                Detalle = $"Se registraron {grupoIp.Count()} eventos desde la IP {grupoIp.Key} (Tipos: {tipos}). Posible ataque de fuerza bruta o reconocimiento activo.",
                Recomendacion = $"Bloquear permanentemente la IP {grupoIp.Key} en el Firewall perimetral y auditar accesos previos desde este origen."
            });
        }

        // Regla 2: Posible compromiso de cuenta de usuario (múltiples eventos Críticos/Altos en distintos servidores)
        var usuariosEnRiesgo = eventos
            .Where(e => e.Severidad is NivelSeveridad.Critica or NivelSeveridad.Alta)
            .GroupBy(e => e.Usuario)
            .Where(g => !string.IsNullOrWhiteSpace(g.Key) && g.Count() >= 2);

        foreach (var grupoUsr in usuariosEnRiesgo)
        {
            var servidores = string.Join(", ", grupoUsr.Select(x => x.Servidor).Distinct());
            resumen.AlertasDetectadas.Add(new AlertaCorrelacion
            {
                Titulo = $"Posible Compromiso de Credenciales: Usuario '{grupoUsr.Key}'",
                Nivel = NivelSeveridad.Critica,
                Detalle = $"La cuenta '{grupoUsr.Key}' presenta {grupoUsr.Count()} incidentes de severidad Alta/Crítica afectando a: {servidores}.",
                Recomendacion = $"Forzar restablecimiento inmediato de contraseña para '{grupoUsr.Key}', revocar sesiones activas y verificar MFA."
            });
        }

        // Regla 3: Incidentes Críticos aún con Alerta Activa o En Investigación
        var criticosPendientes = eventos
            .Where(e => e.Severidad == NivelSeveridad.Critica &&
                        !e.Estado.Equals("Bloqueado", StringComparison.OrdinalIgnoreCase) &&
                        !e.Estado.Equals("Resuelto", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (criticosPendientes.Count > 0)
        {
            var ids = string.Join(", ", criticosPendientes.Select(x => x.Id));
            resumen.AlertasDetectadas.Add(new AlertaCorrelacion
            {
                Titulo = $"{criticosPendientes.Count} Incidente(s) Crítico(s) Sin Contención Definitiva",
                Nivel = NivelSeveridad.Critica,
                Detalle = $"Los eventos [{ids}] tienen severidad CRÍTICA pero su estado actual no figura como Bloqueado ni Resuelto.",
                Recomendacion = "Aislar de la red los equipos afectados y escalar inmediatamente al equipo de Respuesta a Incidentes (CSIRT)."
            });
        }

        // Regla 4: Servidores bajo ataque concentrado (>= 3 eventos)
        var servidoresAtacados = eventos
            .GroupBy(e => e.Servidor)
            .Where(g => !string.IsNullOrWhiteSpace(g.Key) && g.Count() >= 3);

        foreach (var srv in servidoresAtacados)
        {
            resumen.AlertasDetectadas.Add(new AlertaCorrelacion
            {
                Titulo = $"Alta Concentración de Eventos en Servidor '{srv.Key}'",
                Nivel = NivelSeveridad.Alta,
                Detalle = $"El activo '{srv.Key}' acumuló {srv.Count()} eventos de seguridad en el periodo analizado.",
                Recomendacion = $"Revisar políticas de endurecimiento (hardening), parches de seguridad y reglas WAF/IDS asociadas a '{srv.Key}'."
            });
        }
    }
}

