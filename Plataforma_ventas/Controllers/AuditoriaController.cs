using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Plataforma_ventas.Filters;

namespace Plataforma_ventas.Controllers
{
    /// <summary>
    /// Consulta del registro de auditoría. Hasta ahora los eventos solo iban al log
    /// del servidor, que en App Service rota y se pierde; esta pantalla los deja
    /// consultables desde la plataforma.
    /// </summary>
    [RolAutorizado("Administrador")]
    public class AuditoriaController : Controller
    {
        private readonly string _conn;

        public AuditoriaController(IConfiguration config)
        {
            _conn = config.GetConnectionString("DefaultConnection")!;
        }

        /// <summary>
        /// Lista los eventos auditados con filtros y paginación.
        /// </summary>
        /// <param name="accion">Filtra por tipo de acción (LOGIN, VENTA_ANULADA, …).</param>
        /// <param name="q">Búsqueda libre sobre usuario y detalle.</param>
        /// <param name="desde">Fecha inicial (yyyy-MM-dd), en hora de Colombia.</param>
        /// <param name="hasta">Fecha final (yyyy-MM-dd), inclusive.</param>
        /// <param name="page">Página 1-based.</param>
        /// <param name="pageSize">Registros por página.</param>
        public async Task<IActionResult> Index(string accion = "", string q = "",
            string desde = "", string hasta = "", int page = 1, int pageSize = 50)
        {
            ViewBag.Nombre = HttpContext.Session.GetString("Nombre") ?? "Admin";
            ViewBag.Apellido = HttpContext.Session.GetString("Apellido") ?? "";
            ViewBag.ProyectoActivo = HttpContext.Session.GetString("ProyectoNombre") ?? "Sin proyecto";

            if (page < 1) page = 1;
            if (pageSize is < 10 or > 200) pageSize = 50;

            ViewBag.Accion = accion; ViewBag.Q = q;
            ViewBag.Desde = desde;   ViewBag.Hasta = hasta;

            var eventos = new List<dynamic>();
            var acciones = new List<string>();
            bool tablaOk = true;
            int total = 0;

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            // Filtros comunes. Las fechas se comparan en hora de Colombia (UTC-5),
            // porque Auditoria.Fecha se guarda en UTC.
            const string filtro = @"
                WHERE (@accion = '' OR Accion = @accion)
                  AND (@q = '' OR Usuario LIKE '%' + @q + '%' OR Detalle LIKE '%' + @q + '%')
                  AND (@desde = '' OR CAST(DATEADD(HOUR,-5,Fecha) AS DATE) >= CAST(@desde AS DATE))
                  AND (@hasta = '' OR CAST(DATEADD(HOUR,-5,Fecha) AS DATE) <= CAST(@hasta AS DATE))";

            void Parametros(SqlCommand c)
            {
                c.Parameters.AddWithValue("@accion", accion ?? "");
                c.Parameters.AddWithValue("@q", q ?? "");
                c.Parameters.AddWithValue("@desde", desde ?? "");
                c.Parameters.AddWithValue("@hasta", hasta ?? "");
            }

            try
            {
                var cmdCount = new SqlCommand("SELECT COUNT(*) FROM Auditoria " + filtro, con);
                Parametros(cmdCount);
                total = Convert.ToInt32(await cmdCount.ExecuteScalarAsync());

                // Acciones presentes, para poblar el desplegable de filtro.
                using (var rA = (SqlDataReader)await new SqlCommand(
                    "SELECT DISTINCT Accion FROM Auditoria ORDER BY Accion", con).ExecuteReaderAsync())
                    while (await rA.ReadAsync())
                        acciones.Add(rA["Accion"]?.ToString() ?? "");

                var cmd = new SqlCommand(@"
                    SELECT IdAuditoria,
                           FORMAT(DATEADD(HOUR,-5,Fecha),'dd/MM/yyyy HH:mm:ss') AS Cuando,
                           Usuario, Rol, Accion, Entidad, IdEntidad, Detalle, Ip
                    FROM Auditoria " + filtro + @"
                    ORDER BY Fecha DESC
                    OFFSET (@page-1)*@pageSize ROWS FETCH NEXT @pageSize ROWS ONLY", con);
                Parametros(cmd);
                cmd.Parameters.AddWithValue("@page", page);
                cmd.Parameters.AddWithValue("@pageSize", pageSize);
                using (var r = (SqlDataReader)await cmd.ExecuteReaderAsync())
                    while (await r.ReadAsync())
                        eventos.Add(new
                        {
                            Cuando = r["Cuando"]?.ToString() ?? "",
                            Usuario = r["Usuario"]?.ToString() ?? "",
                            Rol = r["Rol"]?.ToString() ?? "",
                            Accion = r["Accion"]?.ToString() ?? "",
                            Entidad = r["Entidad"]?.ToString() ?? "",
                            IdEntidad = r["IdEntidad"] == DBNull.Value ? 0 : Convert.ToInt32(r["IdEntidad"]),
                            Detalle = r["Detalle"]?.ToString() ?? "",
                            Ip = r["Ip"]?.ToString() ?? "",
                        });
            }
            catch (SqlException ex) when (ex.Message.Contains("Invalid object name") || ex.Number == 208)
            {
                // El script de migración aún no se ha ejecutado.
                tablaOk = false;
            }

            // Historial de precios: últimos cambios de lista del proyecto activo.
            var historial = new List<dynamic>();
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;
            try
            {
                var cmdH = new SqlCommand(@"
                    SELECT TOP 100
                           FORMAT(DATEADD(HOUR,-5,Fecha),'dd/MM/yyyy HH:mm') AS Cuando,
                           Metros, ListaAnterior, ListaNueva, Motivo, Usuario
                    FROM HistorialListas
                    WHERE IdProyecto=@id
                    ORDER BY Fecha DESC", con);
                cmdH.Parameters.AddWithValue("@id", idProy);
                using var rH = (SqlDataReader)await cmdH.ExecuteReaderAsync();
                while (await rH.ReadAsync())
                    historial.Add(new
                    {
                        Cuando = rH["Cuando"]?.ToString() ?? "",
                        Metros = rH["Metros"]?.ToString() ?? "",
                        Anterior = Convert.ToInt32(rH["ListaAnterior"]),
                        Nueva = Convert.ToInt32(rH["ListaNueva"]),
                        Motivo = rH["Motivo"]?.ToString() ?? "",
                        Usuario = (rH["Usuario"]?.ToString() ?? "").Trim(),
                    });
            }
            catch (SqlException ex) when (ex.Message.Contains("Invalid object name") || ex.Number == 208)
            {
                // Sin tabla de historial todavía.
            }
            ViewBag.Historial = historial;

            ViewBag.TablaOk = tablaOk;
            ViewBag.Eventos = eventos;
            ViewBag.Acciones = acciones;
            ViewBag.Total = total;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = total > 0 ? (int)Math.Ceiling((double)total / pageSize) : 0;

            return View();
        }
    }
}
