using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Plataforma_ventas.Filters;

namespace Plataforma_ventas.Controllers
{
    /// <summary>
    /// Administrator controller for client (buyer) management:
    /// browsing, searching, detail view, and editing client records.
    /// </summary>
    [RolAutorizado("Administrador")]
    public class ClientesController : Controller
    {
        private readonly string _conn;

        /// <summary>Initializes the controller with DB connection string from configuration.</summary>
        public ClientesController(IConfiguration config)
        {
            _conn = config.GetConnectionString("DefaultConnection")!;
        }

        /// <summary>
        /// Lists clients who have at least one active sale in the current project.
        /// Supports server-side pagination to avoid loading thousands of rows at once.
        /// Results are ordered by client name ascending.
        /// Performs a COUNT query for total pages and a paginated SELECT query for the current page.
        /// </summary>
        /// <param name="page">1-based page number. Defaults to 1.</param>
        /// <param name="pageSize">Number of clients per page. Defaults to 25.</param>
        public async Task<IActionResult> Index([FromQuery] int page = 1, [FromQuery] int pageSize = 25)
        {
            ViewBag.Nombre = HttpContext.Session.GetString("Nombre") ?? "Admin";
            ViewBag.Apellido = HttpContext.Session.GetString("Apellido") ?? "";
            ViewBag.ProyectoActivo = HttpContext.Session.GetString("ProyectoNombre") ?? "Sin proyecto";
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;
            int idAdmin = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid2) ? uid2 : 0;

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 25;

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            var proyectos = new List<(int Id, string Nombre)>();
            var cmdList = new SqlCommand("SELECT IdProyectos, Nombre FROM Proyectos WHERE Activo=1 AND IdAdminCreador=@admin ORDER BY FechaCarga DESC", con);
            cmdList.Parameters.AddWithValue("@admin", idAdmin);
            using (var r = (SqlDataReader)await cmdList.ExecuteReaderAsync())
                while (await r.ReadAsync())
                    proyectos.Add(((int)r["IdProyectos"], r["Nombre"]?.ToString() ?? ""));
            ViewBag.Proyectos = proyectos;

            // COUNT for pagination
            var cmdCount = new SqlCommand(@"
                SELECT COUNT(DISTINCT c.IdCliente)
                FROM Clientes c
                INNER JOIN Ventas v ON c.IdCliente = v.IdCliente AND v.IdProyecto = @proy", con);
            cmdCount.Parameters.AddWithValue("@proy", idProy);
            int total = (int)(await cmdCount.ExecuteScalarAsync())!;
            int totalPages = (int)Math.Ceiling((double)total / pageSize);
            if (page > totalPages && totalPages > 0) page = totalPages;

            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = totalPages;
            ViewBag.Total = total;

            // Paginated query — only clients with sales in the active project
            var clientes = new List<dynamic>();
            var cmd = new SqlCommand(@"
                SELECT c.IdCliente, c.Nombre, c.Apellido, c.Documento, c.Celular, c.Correo, c.Direccion,
                       COUNT(v.IdVenta)                  AS TotalCompras,
                       ISNULL(SUM(v.PrecioVenta), 0)     AS ValorTotal,
                       MAX(u.Nombre+' '+u.Apellido)       AS UltimoAsesor
                FROM Clientes c
                INNER JOIN Ventas v  ON c.IdCliente = v.IdCliente AND v.IdProyecto = @proy
                LEFT  JOIN Usuarios u ON v.IdUsuario = u.IdUsuario
                GROUP BY c.IdCliente, c.Nombre, c.Apellido, c.Documento, c.Celular, c.Correo, c.Direccion
                ORDER BY c.Nombre
                OFFSET (@page-1)*@pageSize ROWS FETCH NEXT @pageSize ROWS ONLY", con);
            cmd.Parameters.AddWithValue("@proy", idProy);
            cmd.Parameters.AddWithValue("@page", page);
            cmd.Parameters.AddWithValue("@pageSize", pageSize);
            using (var reader = (SqlDataReader)await cmd.ExecuteReaderAsync())
                while (await reader.ReadAsync())
                    clientes.Add(new
                    {
                        Id = (int)reader["IdCliente"],
                        Nombre = reader["Nombre"]?.ToString() ?? "",
                        Apellido = reader["Apellido"]?.ToString() ?? "",
                        Documento = reader["Documento"]?.ToString() ?? "",
                        Celular = reader["Celular"]?.ToString() ?? "",
                        Correo = reader["Correo"]?.ToString() ?? "",
                        Direccion = reader["Direccion"]?.ToString() ?? "",
                        TotalCompras = (int)reader["TotalCompras"],
                        ValorTotal = (long)reader["ValorTotal"],
                        UltimoAsesor = reader["UltimoAsesor"]?.ToString() ?? "—",
                    });

            ViewBag.Clientes = clientes;
            ViewBag.TotalClientes = clientes.Count;
            return View();
        }

        /// <summary>
        /// Displays a single client's detail page including all their purchase history
        /// across all projects. Performs SELECT queries for client data and related sales.
        /// </summary>
        /// <param name="id">Client identifier.</param>
        public async Task<IActionResult> Detalle(int id)
        {
            ViewBag.Nombre = HttpContext.Session.GetString("Nombre") ?? "Admin";
            ViewBag.Apellido = HttpContext.Session.GetString("Apellido") ?? "";
            ViewBag.ProyectoActivo = HttpContext.Session.GetString("ProyectoNombre") ?? "Sin proyecto";

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            int idAdmin = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid2) ? uid2 : 0;
            var proyectos = new List<(int Id, string Nombre)>();
            var cmdList = new SqlCommand("SELECT IdProyectos, Nombre FROM Proyectos WHERE Activo=1 AND IdAdminCreador=@admin ORDER BY FechaCarga DESC", con);
            cmdList.Parameters.AddWithValue("@admin", idAdmin);
            using (var r = (SqlDataReader)await cmdList.ExecuteReaderAsync())
                while (await r.ReadAsync())
                    proyectos.Add(((int)r["IdProyectos"], r["Nombre"]?.ToString() ?? ""));
            ViewBag.Proyectos = proyectos;

            // Datos del cliente
            var cmdCli = new SqlCommand("SELECT * FROM Clientes WHERE IdCliente=@id", con);
            cmdCli.Parameters.AddWithValue("@id", id);
            using var rC = (SqlDataReader)await cmdCli.ExecuteReaderAsync();
            if (!await rC.ReadAsync()) return RedirectToAction("Index");
            ViewBag.Cliente = new
            {
                Id = (int)rC["IdCliente"],
                Nombre = rC["Nombre"]?.ToString() ?? "",
                Apellido = rC["Apellido"]?.ToString() ?? "",
                Documento = rC["Documento"]?.ToString() ?? "",
                Celular = rC["Celular"]?.ToString() ?? "",
                Correo = rC["Correo"]?.ToString() ?? "",
                Direccion = rC["Direccion"]?.ToString() ?? "",
            };
            rC.Close();

            // Compras del cliente
            var compras = new List<dynamic>();
            var cmdV = new SqlCommand(@"
                SELECT v.IdVenta, i.Apto, i.Torre, i.Tipo, i.Piso,
                       p.Nombre AS Proyecto,
                       u.Nombre+' '+u.Apellido AS Asesor,
                       v.ListaAplicada, v.PrecioVenta, v.FechaVenta, v.Estado
                FROM Ventas v
                JOIN Inmuebles i ON v.IdInmueble = i.IdInmuebles
                JOIN Proyectos p ON v.IdProyecto = p.IdProyectos
                JOIN Usuarios  u ON v.IdUsuario  = u.IdUsuario
                WHERE v.IdCliente = @id
                ORDER BY v.FechaVenta DESC", con);
            cmdV.Parameters.AddWithValue("@id", id);
            using var rV = (SqlDataReader)await cmdV.ExecuteReaderAsync();
            while (await rV.ReadAsync())
            {
                compras.Add(new
                {
                    Id = (int)rV["IdVenta"],
                    Apto = rV["Apto"]?.ToString() ?? "",
                    Torre = rV["Torre"]?.ToString() ?? "",
                    Tipo = rV["Tipo"]?.ToString() ?? "",
                    Piso = rV["Piso"]?.ToString() ?? "",
                    Proyecto = rV["Proyecto"]?.ToString() ?? "",
                    Asesor = rV["Asesor"]?.ToString() ?? "",
                    Lista = rV["ListaAplicada"]?.ToString() ?? "",
                    PrecioVenta = (long)rV["PrecioVenta"],
                    FechaVenta = Convert.ToDateTime(rV["FechaVenta"]).ToString("dd/MM/yyyy"),
                    Estado = rV["Estado"]?.ToString() ?? "",
                });
            }
            ViewBag.Compras = compras;
            ViewBag.TotalCompras = compras.Count;
            ViewBag.ValorTotal = compras.Sum(c => (long)c.PrecioVenta);

            return View();
        }

        /// <summary>
        /// Updates a client's contact and personal information.
        /// Performs an UPDATE query on the Clientes table.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(int idCliente, string nombre, string apellido,
            string documento, string celular, string correo, string direccion)
        {
            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            var cmd = new SqlCommand(@"UPDATE Clientes
                SET Nombre=@n, Apellido=@a, Documento=@d, Celular=@c, Correo=@e, Direccion=@dir
                WHERE IdCliente=@id", con);
            cmd.Parameters.AddWithValue("@n", nombre ?? "");
            cmd.Parameters.AddWithValue("@a", apellido ?? "");
            cmd.Parameters.AddWithValue("@d", documento ?? "");
            cmd.Parameters.AddWithValue("@c", celular ?? "");
            cmd.Parameters.AddWithValue("@e", correo ?? "");
            cmd.Parameters.AddWithValue("@dir", direccion ?? "");
            cmd.Parameters.AddWithValue("@id", idCliente);
            await cmd.ExecuteNonQueryAsync();

            TempData["Exito"] = "Cliente actualizado correctamente.";
            return RedirectToAction("Detalle", new { id = idCliente });
        }
    }
}
