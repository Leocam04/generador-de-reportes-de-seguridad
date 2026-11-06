using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using GeneradorReportesSeguridad.Models;

namespace GeneradorReportesSeguridad.Services;

/// <summary>
/// Servicio encargado de leer, parsear y crear archivos de eventos de seguridad (.csv, .json, .txt, .log).
/// </summary>
public class LectorEventosService
{
    private readonly string _directorioEntrada;

    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public LectorEventosService(string directorioEntrada)
    {
        _directorioEntrada = directorioEntrada;
        Directory.CreateDirectory(_directorioEntrada);
    }

    /// <summary>
    /// Lee todos los archivos compatibles (.csv, .json, .txt, .log) dentro de la carpeta de entrada.
    /// </summary>
    public (List<EventoSeguridad> Eventos, int ArchivosProcesados) LeerTodosLosEventos()
    {
        Directory.CreateDirectory(_directorioEntrada);

        string[] extensionesPermitidas = [".csv", ".json", ".txt", ".log"];
        var archivos = Directory.GetFiles(_directorioEntrada)
            .Where(f => extensionesPermitidas.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => f)
            .ToList();

        var todosLosEventos = new List<EventoSeguridad>();
        int archivosLeidos = 0;

        foreach (string rutaArchivo in archivos)
        {
            try
            {
                var eventosArchivo = LeerArchivo(rutaArchivo);
                todosLosEventos.AddRange(eventosArchivo);
                archivosLeidos++;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[AVISO] No se pudo procesar '{Path.GetFileName(rutaArchivo)}': {ex.Message}");
                Console.ResetColor();
            }
        }

        return (todosLosEventos, archivosLeidos);
    }

    /// <summary>
    /// Lee un archivo individual según su extensión, con reintentos ante bloqueos temporales de E/S.
    /// </summary>
    public List<EventoSeguridad> LeerArchivo(string rutaArchivo)
    {
        string contenido = LeerTextoConReintento(rutaArchivo);
        string extension = Path.GetExtension(rutaArchivo).ToLowerInvariant();
        string nombreArchivo = Path.GetFileName(rutaArchivo);

        return extension switch
        {
            ".csv" => ParsearCsv(contenido, nombreArchivo),
            ".json" => ParsearJson(contenido, nombreArchivo),
            ".txt" or ".log" => ParsearLogTexto(contenido, nombreArchivo),
            _ => []
        };
    }

    /// <summary>
    /// Lee el contenido de un archivo esperando brevemente si otro proceso aún lo está escribiendo.
    /// </summary>
    private static string LeerTextoConReintento(string rutaArchivo, int maxIntentos = 5)
    {
        for (int intento = 1; intento <= maxIntentos; intento++)
        {
            try
            {
                using var fs = new FileStream(rutaArchivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs, Encoding.UTF8);
                return sr.ReadToEnd();
            }
            catch (IOException) when (intento < maxIntentos)
            {
                Thread.Sleep(150 * intento);
            }
        }

        return File.ReadAllText(rutaArchivo, Encoding.UTF8);
    }

    /// <summary>
    /// Parsea un archivo CSV con formato:
    /// Id,FechaHora,TipoEvento,Severidad,OrigenIp,Usuario,Servidor,Estado,Descripcion
    /// </summary>
    private static List<EventoSeguridad> ParsearCsv(string contenido, string nombreArchivo)
    {
        var lista = new List<EventoSeguridad>();
        var lineas = contenido.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        int contador = 1;
        foreach (string lineaCruda in lineas)
        {
            string linea = lineaCruda.Trim();
            if (string.IsNullOrWhiteSpace(linea) || linea.StartsWith('#'))
                continue;

            // Omitir cabecera si existe
            if (contador == 1 && (linea.StartsWith("Id,", StringComparison.OrdinalIgnoreCase) ||
                                  linea.StartsWith("Id;", StringComparison.OrdinalIgnoreCase) ||
                                  linea.Contains("FechaHora", StringComparison.OrdinalIgnoreCase)))
            {
                contador++;
                continue;
            }

            char separador = linea.Contains(';') && !linea.Contains(',') ? ';' : ',';
            string[] partes = DividirLineaCsv(linea, separador);

            if (partes.Length >= 8)
            {
                DateTime fecha = ParsearFecha(partes[1]);
                lista.Add(new EventoSeguridad
                {
                    Id = string.IsNullOrWhiteSpace(partes[0]) ? $"CSV-{contador:000}" : partes[0].Trim(),
                    FechaHora = fecha,
                    TipoEvento = partes[2].Trim(),
                    Severidad = EventoSeguridad.ParsearSeveridad(partes[3]),
                    OrigenIp = partes[4].Trim(),
                    Usuario = partes[5].Trim(),
                    Servidor = partes[6].Trim(),
                    Estado = partes[7].Trim(),
                    Descripcion = partes.Length >= 9 ? partes[8].Trim() : "Sin descripción adicional",
                    ArchivoOrigen = nombreArchivo
                });
            }
            contador++;
        }

        return lista;
    }

    /// <summary>
    /// Divide una línea CSV respetando comillas dobles.
    /// </summary>
    private static string[] DividirLineaCsv(string linea, char separador)
    {
        var resultado = new List<string>();
        var actual = new StringBuilder();
        bool enComillas = false;

        foreach (char c in linea)
        {
            if (c == '"')
            {
                enComillas = !enComillas;
            }
            else if (c == separador && !enComillas)
            {
                resultado.Add(actual.ToString());
                actual.Clear();
            }
            else
            {
                actual.Append(c);
            }
        }
        resultado.Add(actual.ToString());
        return resultado.ToArray();
    }

    /// <summary>
    /// Parsea un archivo JSON que contiene un arreglo de eventos de seguridad.
    /// </summary>
    private static List<EventoSeguridad> ParsearJson(string contenido, string nombreArchivo)
    {
        if (string.IsNullOrWhiteSpace(contenido))
            return [];

        var eventos = JsonSerializer.Deserialize<List<EventoSeguridad>>(contenido, OpcionesJson) ?? [];
        int indice = 1;
        foreach (var ev in eventos)
        {
            if (string.IsNullOrWhiteSpace(ev.Id))
                ev.Id = $"JSON-{indice:000}";
            if (ev.FechaHora == default)
                ev.FechaHora = DateTime.Now;
            ev.ArchivoOrigen = nombreArchivo;
            indice++;
        }

        return eventos;
    }

    /// <summary>
    /// Parsea archivos de log de texto (.txt / .log).
    /// Soporta formato con corchetes:
    /// [2026-11-06 08:15:22] [CRITICA] [192.168.1.105] [admin] [SRV-DB01] [Login Fallido] [Bloqueado] Descripción del evento
    /// o líneas separadas por tuberías (|).
    /// </summary>
    private static List<EventoSeguridad> ParsearLogTexto(string contenido, string nombreArchivo)
    {
        var lista = new List<EventoSeguridad>();
        var lineas = contenido.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var regexCorchetes = new Regex(@"^\[(?<fecha>[^\]]+)\]\s*\[(?<sev>[^\]]+)\]\s*\[(?<ip>[^\]]+)\]\s*\[(?<usr>[^\]]+)\]\s*\[(?<srv>[^\]]+)\]\s*\[(?<tipo>[^\]]+)\]\s*\[(?<est>[^\]]+)\]\s*(?<desc>.*)$");

        int numLinea = 1;
        foreach (string lineaCruda in lineas)
        {
            string linea = lineaCruda.Trim();
            if (string.IsNullOrWhiteSpace(linea) || linea.StartsWith('#') || linea.StartsWith("//"))
                continue;

            var match = regexCorchetes.Match(linea);
            if (match.Success)
            {
                lista.Add(new EventoSeguridad
                {
                    Id = $"LOG-{numLinea:000}",
                    FechaHora = ParsearFecha(match.Groups["fecha"].Value),
                    Severidad = EventoSeguridad.ParsearSeveridad(match.Groups["sev"].Value),
                    OrigenIp = match.Groups["ip"].Value.Trim(),
                    Usuario = match.Groups["usr"].Value.Trim(),
                    Servidor = match.Groups["srv"].Value.Trim(),
                    TipoEvento = match.Groups["tipo"].Value.Trim(),
                    Estado = match.Groups["est"].Value.Trim(),
                    Descripcion = match.Groups["desc"].Value.Trim(),
                    ArchivoOrigen = nombreArchivo
                });
            }
            else if (linea.Contains('|'))
            {
                // Formato alternativo separado por '|': Fecha|Severidad|IP|Usuario|Servidor|Tipo|Estado|Descripcion
                string[] partes = linea.Split('|');
                if (partes.Length >= 7)
                {
                    lista.Add(new EventoSeguridad
                    {
                        Id = $"LOG-{numLinea:000}",
                        FechaHora = ParsearFecha(partes[0]),
                        Severidad = EventoSeguridad.ParsearSeveridad(partes[1]),
                        OrigenIp = partes[2].Trim(),
                        Usuario = partes[3].Trim(),
                        Servidor = partes[4].Trim(),
                        TipoEvento = partes[5].Trim(),
                        Estado = partes[6].Trim(),
                        Descripcion = partes.Length >= 8 ? partes[7].Trim() : "Evento de log",
                        ArchivoOrigen = nombreArchivo
                    });
                }
            }

            numLinea++;
        }

        return lista;
    }

    private static DateTime ParsearFecha(string textoFecha)
    {
        if (DateTime.TryParse(textoFecha.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime fecha))
            return fecha;
        if (DateTime.TryParse(textoFecha.Trim(), new CultureInfo("es-ES"), DateTimeStyles.None, out fecha))
            return fecha;
        return DateTime.Now;
    }

    /// <summary>
    /// Crea archivos iniciales de demostración (.csv, .json y .log) si la carpeta de entrada está vacía.
    /// </summary>
    public bool AsegurarArchivosEjemploIniciales()
    {
        Directory.CreateDirectory(_directorioEntrada);
        var existentes = Directory.GetFiles(_directorioEntrada);
        if (existentes.Length > 0)
            return false;

        DateTime baseTime = DateTime.Now.AddHours(-6);

        // 1. Archivo CSV: Eventos de Firewall y Accesos de Red
        string rutaCsv = Path.Combine(_directorioEntrada, "01_eventos_firewall.csv");
        var sbCsv = new StringBuilder();
        sbCsv.AppendLine("Id,FechaHora,TipoEvento,Severidad,OrigenIp,Usuario,Servidor,Estado,Descripcion");
        sbCsv.AppendLine($"FW-101,{baseTime.AddMinutes(5):yyyy-MM-dd HH:mm:ss},Escaneo de Puertos,Media,185.220.101.45,anonimo,SRV-GATEWAY-01,Bloqueado,\"Escaneo SYN detectado en puertos 22, 80, 443 y 3389\"");
        sbCsv.AppendLine($"FW-102,{baseTime.AddMinutes(12):yyyy-MM-dd HH:mm:ss},Login Fallido,Alta,185.220.101.45,root,SRV-LINUX-PROD,Bloqueado,\"Intento de fuerza bruta por SSH (intento 1/5)\"");
        sbCsv.AppendLine($"FW-103,{baseTime.AddMinutes(13):yyyy-MM-dd HH:mm:ss},Login Fallido,Alta,185.220.101.45,root,SRV-LINUX-PROD,Bloqueado,\"Intento de fuerza bruta por SSH (intento 2/5)\"");
        sbCsv.AppendLine($"FW-104,{baseTime.AddMinutes(14):yyyy-MM-dd HH:mm:ss},Login Fallido,Critica,185.220.101.45,admin,SRV-LINUX-PROD,Bloqueado,\"Múltiples intentos fallidos consecutivos por SSH desde IP externa\"");
        sbCsv.AppendLine($"FW-105,{baseTime.AddMinutes(35):yyyy-MM-dd HH:mm:ss}, Regla Firewall Modificada,Baja,10.0.10.15,carlos.mendez,FW-PERIMETRAL,Resuelto,\"Actualización programada de lista blanca de oficina central\"");
        sbCsv.AppendLine($"FW-106,{baseTime.AddMinutes(50):yyyy-MM-dd HH:mm:ss},Intento SQL Injection,Critica,45.155.205.99,web_guest,SRV-WEB-PORTAL,Alerta Activa,\"Payload SQLi detectado en parámetro 'id_usuario' del endpoint /api/login\"");
        File.WriteAllText(rutaCsv, sbCsv.ToString(), Encoding.UTF8);

        // 2. Archivo JSON: Alertas del SIEM y Antivirus EDR
        string rutaJson = Path.Combine(_directorioEntrada, "02_alertas_siem.json");
        var eventosJson = new List<EventoSeguridad>
        {
            new()
            {
                Id = "SIEM-201",
                FechaHora = baseTime.AddHours(1).AddMinutes(10),
                TipoEvento = "Malware Detectado",
                Severidad = NivelSeveridad.Critica,
                OrigenIp = "10.0.20.44",
                Usuario = "maria.lopez",
                Servidor = "WKS-FINANZAS-04",
                Estado = "En Investigación",
                Descripcion = "Ejecución sospechosa de script PowerShell ofuscado (TrojanDownloader.Agent)"
            },
            new()
            {
                Id = "SIEM-202",
                FechaHora = baseTime.AddHours(1).AddMinutes(45),
                TipoEvento = "Escalación de Privilegios",
                Severidad = NivelSeveridad.Alta,
                OrigenIp = "10.0.20.44",
                Usuario = "maria.lopez",
                Servidor = "SRV-ARCHIVOS-01",
                Estado = "Alerta Activa",
                Descripcion = "Solicitud inusual de permisos de Administrador de Dominio fuera de horario habitual"
            },
            new()
            {
                Id = "SIEM-203",
                FechaHora = baseTime.AddHours(2).AddMinutes(15),
                TipoEvento = "Exfiltración de Datos Sospechosa",
                Severidad = NivelSeveridad.Critica,
                OrigenIp = "10.0.20.44",
                Usuario = "maria.lopez",
                Servidor = "SRV-DB-CLIENTES",
                Estado = "Alerta Activa",
                Descripcion = "Transferencia saliente inusual de 4.8 GB hacia almacenamiento externo no autorizado"
            },
            new()
            {
                Id = "SIEM-204",
                FechaHora = baseTime.AddHours(2).AddMinutes(40),
                TipoEvento = "Certificado SSL por Expirar",
                Severidad = NivelSeveridad.Baja,
                OrigenIp = "10.0.5.10",
                Usuario = "sistema",
                Servidor = "SRV-WEB-PORTAL",
                Estado = "En Investigación",
                Descripcion = "El certificado TLS del dominio principal expira en 14 días"
            }
        };
        File.WriteAllText(rutaJson, JsonSerializer.Serialize(eventosJson, OpcionesJson), Encoding.UTF8);

        // 3. Archivo LOG (.log): Bitácora de Autenticación y Auditoría del Servidor
        string rutaLog = Path.Combine(_directorioEntrada, "03_auditoria_servidor.log");
        var sbLog = new StringBuilder();
        sbLog.AppendLine("# Bitácora de Auditoría de Seguridad - Formato: [Fecha] [Severidad] [IP] [Usuario] [Servidor] [Tipo] [Estado] Descripción");
        sbLog.AppendLine($"[{baseTime.AddHours(3).AddMinutes(5):yyyy-MM-dd HH:mm:ss}] [MEDIA] [192.168.50.12] [jorge.ruiz] [SRV-AD-01] [Cuenta Bloqueada] [Bloqueado] Cuenta bloqueada temporalmente tras 3 contraseñas erróneas");
        sbLog.AppendLine($"[{baseTime.AddHours(3).AddMinutes(30):yyyy-MM-dd HH:mm:ss}] [ALTA] [203.0.113.88] [backup_svc] [SRV-BACKUP-01] [Acceso No Autorizado] [Bloqueado] Intento de conexión RDP desde ubicación geográfica no permitida");
        sbLog.AppendLine($"[{baseTime.AddHours(4).AddMinutes(00):yyyy-MM-dd HH:mm:ss}] [BAJA] [10.0.10.22] [ana.torres] [SRV-AD-01] [Cambio de Permisos] [Resuelto] Asignación de rol lectura en carpeta de auditoría trimestral");
        sbLog.AppendLine($"[{baseTime.AddHours(4).AddMinutes(25):yyyy-MM-dd HH:mm:ss}] [CRITICA] [45.155.205.99] [web_guest] [SRV-WEB-PORTAL] [Intento RCE / WebShell] [Bloqueado] WAF bloqueó intento de subida de archivo ejecutable .aspx en directorio público");
        File.WriteAllText(rutaLog, sbLog.ToString(), Encoding.UTF8);

        return true;
    }

    /// <summary>
    /// Genera un archivo nuevo de eventos simulados en la carpeta de entrada para probar la automatización.
    /// </summary>
    public string SimularNuevoArchivoEventos()
    {
        var random = Random.Shared;
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string nombreArchivo = $"lote_automatico_{timestamp}.csv";
        string rutaCompleta = Path.Combine(_directorioEntrada, nombreArchivo);

        string[] tipos =
        [
            "Login Fallido", "Acceso No Autorizado", "Malware Detectado",
            "Escaneo de Puertos", "Intento SQL Injection", " Tráfico DDoS Detectado",
            "Escalación de Privilegios"
        ];
        string[] severidades = ["Baja", "Media", "Alta", "Critica"];
        string[] ips = ["185.220.101.45", "91.240.118.172", "10.0.20.44", "45.155.205.99", "192.168.1.250"];
        string[] usuarios = ["admin", "root", "svc_deploy", "maria.lopez", "invitado_api"];
        string[] servidores = ["SRV-DB-CLIENTES", "SRV-WEB-PORTAL", "SRV-LINUX-PROD", "FW-PERIMETRAL", "SRV-AD-01"];
        string[] estados = ["Bloqueado", "Alerta Activa", "En Investigación", "Resuelto"];

        var sb = new StringBuilder();
        sb.AppendLine("Id,FechaHora,TipoEvento,Severidad,OrigenIp,Usuario,Servidor,Estado,Descripcion");

        int cantidadEventos = random.Next(3, 6);
        for (int i = 1; i <= cantidadEventos; i++)
        {
            string tipo = tipos[random.Next(tipos.Length)].Trim();
            string sev = severidades[random.Next(severidades.Length)];
            string ip = ips[random.Next(ips.Length)];
            string usr = usuarios[random.Next(usuarios.Length)];
            string srv = servidores[random.Next(servidores.Length)];
            string est = estados[random.Next(estados.Length)];
            string id = $"AUTO-{DateTime.Now:HHmmss}-{i}";
            string fecha = DateTime.Now.AddSeconds(-random.Next(5, 180)).ToString("yyyy-MM-dd HH:mm:ss");
            string desc = $"Evento automático detectado por sensor de red ({tipo}) en {srv} desde {ip}";

            sb.AppendLine($"{id},{fecha},{tipo},{sev},{ip},{usr},{srv},{est},\"{desc}\"");
        }

        File.WriteAllText(rutaCompleta, sb.ToString(), Encoding.UTF8);
        return rutaCompleta;
    }

    /// <summary>
    /// Guarda un evento registrado manualmente por el usuario en el archivo de eventos manuales.
    /// </summary>
    public string RegistrarEventoManual(EventoSeguridad evento)
    {
        string rutaManual = Path.Combine(_directorioEntrada, "99_eventos_manuales.csv");
        bool existe = File.Exists(rutaManual);

        var sb = new StringBuilder();
        if (!existe)
        {
            sb.AppendLine("Id,FechaHora,TipoEvento,Severidad,OrigenIp,Usuario,Servidor,Estado,Descripcion");
        }

        string descripcionLimpia = evento.Descripcion.Replace("\"", "'");
        sb.AppendLine($"{evento.Id},{evento.FechaHora:yyyy-MM-dd HH:mm:ss},{evento.TipoEvento},{evento.Severidad},{evento.OrigenIp},{evento.Usuario},{evento.Servidor},{evento.Estado},\"{descripcionLimpia}\"");

        File.AppendAllText(rutaManual, sb.ToString(), Encoding.UTF8);
        return rutaManual;
    }
}

