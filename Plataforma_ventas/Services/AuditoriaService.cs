using Microsoft.Data.SqlClient;

namespace Plataforma_ventas.Services
{
    /// <summary>
    /// Acciones auditables. Se usan constantes en vez de texto suelto para que
    /// los filtros de la pantalla de auditoría y los registros no se desalineen.
    /// </summary>
    public static class AccionAudit
    {
        public const string Login          = "LOGIN";
        public const string LoginFallido   = "LOGIN_FALLIDO";
        public const string Bloqueo        = "CUENTA_BLOQUEADA";
        public const string Desbloqueo     = "CUENTA_LIBERADA";
        public const string Logout         = "LOGOUT";
        public const string RecuperarClave = "RECUPERAR_CLAVE";
        public const string VentaRegistrada = "VENTA_REGISTRADA";
        public const string VentaAnulada   = "VENTA_ANULADA";
        public const string ReservaCreada  = "RESERVA_CREADA";
        public const string ReservaLiberada = "RESERVA_LIBERADA";
        public const string ProcesoTomado  = "PROCESO_TOMADO";
        public const string ProcesoCancelado = "PROCESO_CANCELADO";
        public const string ListaCambiada  = "LISTA_CAMBIADA";
        public const string UsuarioCreado  = "USUARIO_CREADO";
        public const string UsuarioEditado = "USUARIO_EDITADO";
        public const string UsuarioEliminado = "USUARIO_ELIMINADO";
        public const string ClaveReiniciada = "CLAVE_REINICIADA";
        public const string ProyectoCargado = "PROYECTO_CARGADO";
        public const string ProyectoEliminado = "PROYECTO_ELIMINADO";
    }

    /// <summary>Registra acciones en la tabla Auditoria para consulta posterior.</summary>
    public interface IAuditoriaService
    {
        /// <summary>
        /// Registra un evento. Nunca lanza: la auditoría no debe tumbar una operación
        /// de negocio. Si la tabla no existe todavía, el evento se descarta en silencio
        /// (queda igualmente en el log de la aplicación).
        /// </summary>
        Task RegistrarAsync(string accion, string entidad = "", int? idEntidad = null,
                            int? idProyecto = null, string detalle = "");
    }

    /// <inheritdoc cref="IAuditoriaService"/>
    public class AuditoriaService : IAuditoriaService
    {
        private readonly string _conn;
        private readonly IHttpContextAccessor _http;
        private readonly ILogger<AuditoriaService> _logger;

        // Si el script PanelAdmin.sql no se ha ejecutado, se deja de intentar
        // en cada llamada para no castigar el rendimiento con errores repetidos.
        private static bool _tablaAusente;

        public AuditoriaService(IConfiguration config, IHttpContextAccessor http,
                                ILogger<AuditoriaService> logger)
        {
            _conn = config.GetConnectionString("DefaultConnection")!;
            _http = http;
            _logger = logger;
        }

        public async Task RegistrarAsync(string accion, string entidad = "", int? idEntidad = null,
                                         int? idProyecto = null, string detalle = "")
        {
            var ctx = _http.HttpContext;
            var idUsuario = int.TryParse(ctx?.Session.GetString("UsuarioId"), out int uid) ? uid : (int?)null;
            var usuario = $"{ctx?.Session.GetString("Nombre")} {ctx?.Session.GetString("Apellido")}".Trim();
            var rol = ctx?.Session.GetString("Rol") ?? "";
            var ip = ctx?.Connection.RemoteIpAddress?.ToString() ?? "";

            // El log de la aplicación se conserva: la tabla lo complementa, no lo reemplaza.
            _logger.LogInformation("Auditoría {Accion} · {Usuario} · {Entidad}#{IdEntidad} · {Detalle}",
                accion, string.IsNullOrWhiteSpace(usuario) ? "(anónimo)" : usuario, entidad, idEntidad, detalle);

            if (_tablaAusente) return;

            try
            {
                using var con = new SqlConnection(_conn);
                await con.OpenAsync();
                var cmd = new SqlCommand(@"
                    INSERT INTO Auditoria
                        (IdUsuario, Usuario, Rol, Accion, Entidad, IdEntidad, IdProyecto, Detalle, Ip)
                    VALUES
                        (@idu, @usr, @rol, @acc, @ent, @ide, @proy, @det, @ip)", con);
                cmd.Parameters.AddWithValue("@idu", (object?)idUsuario ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@usr", Recortar(usuario, 150));
                cmd.Parameters.AddWithValue("@rol", Recortar(rol, 50));
                cmd.Parameters.AddWithValue("@acc", Recortar(accion, 60));
                cmd.Parameters.AddWithValue("@ent", Recortar(entidad, 60));
                cmd.Parameters.AddWithValue("@ide", (object?)idEntidad ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@proy", (object?)idProyecto ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@det", Recortar(detalle, 1000));
                cmd.Parameters.AddWithValue("@ip", Recortar(ip, 60));
                await cmd.ExecuteNonQueryAsync();
            }
            catch (SqlException ex) when (ex.Message.Contains("Invalid object name") || ex.Number == 208)
            {
                _tablaAusente = true;
                _logger.LogWarning("La tabla Auditoria no existe. Ejecute Scripts/PanelAdmin.sql para habilitar la auditoría consultable.");
            }
            catch (Exception ex)
            {
                // Auditar nunca debe hacer fallar la operación que se está auditando.
                _logger.LogError(ex, "No se pudo registrar el evento de auditoría {Accion}", accion);
            }
        }

        private static string Recortar(string? s, int max)
        {
            s ??= "";
            return s.Length <= max ? s : s[..max];
        }
    }
}
