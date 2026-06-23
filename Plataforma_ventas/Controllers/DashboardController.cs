using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using Plataforma_ventas.Filters;
using Plataforma_ventas.Hubs;

namespace Plataforma_ventas.Controllers
{
    /// <summary>
    /// Administrator dashboard controller: shows project KPIs and manages
    /// the global list configuration for the active project.
    /// </summary>
    [RolAutorizado("Administrador")]
    public class DashboardController : Controller
    {
        private readonly string _conn;
        private readonly IHubContext<VentasHub, IVentasClient> _hub;

        /// <summary>Initializes the controller with DB connection and strongly-typed SignalR hub.</summary>
        public DashboardController(IConfiguration config, IHubContext<VentasHub, IVentasClient> hub)
        {
            _conn = config.GetConnectionString("DefaultConnection")!;
            _hub = hub;
        }

        /// <summary>
        /// Displays the admin dashboard with KPIs (totals, percentages, list config)
        /// for the active project. Auto-selects the first project if none is in session.
        /// Performs multiple SELECT queries against Inmuebles and Proyectos.
        /// </summary>
        public async Task<IActionResult> Index()
        {
            ViewBag.Nombre = HttpContext.Session.GetString("Nombre") ?? "Admin";
            ViewBag.Apellido = HttpContext.Session.GetString("Apellido") ?? "";
            int idAdmin = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            var proyectos = new List<(int Id, string Nombre)>();
            var cmdList = new SqlCommand(@"SELECT IdProyectos, Nombre FROM Proyectos
                WHERE Activo=1 ORDER BY FechaCarga DESC", con);
            using (var r = (SqlDataReader)await cmdList.ExecuteReaderAsync())
                while (await r.ReadAsync())
                    proyectos.Add(((int)r["IdProyectos"], r["Nombre"]?.ToString() ?? ""));
            ViewBag.Proyectos = proyectos;

            if (idProy == 0 && proyectos.Count > 0)
            {
                idProy = proyectos[0].Id;
                HttpContext.Session.SetString("ProyectoId", idProy.ToString());
                HttpContext.Session.SetString("ProyectoNombre", proyectos[0].Nombre);
                ViewBag.ProyectoActivo = proyectos[0].Nombre;
            }
            else if (proyectos.Count == 0)
            {
                ViewBag.ProyectoActivo = "Sin proyecto";
                ViewBag.Total = 0; ViewBag.Disponibles = 0; ViewBag.Reservados = 0;
                ViewBag.Vendidos = 0; ViewBag.EnProceso = 0;
                ViewBag.PctVendidos = 0.0; ViewBag.PctDisponibles = 0.0; ViewBag.PctReservados = 0.0;
                ViewBag.ListaActual = 1; ViewBag.ApartamentosPorLista = 0;
                ViewBag.ProyectoId = 0;
                return View();
            }
            else
            {
                ViewBag.ProyectoActivo = HttpContext.Session.GetString("ProyectoNombre") ?? proyectos[0].Nombre;
            }

            ViewBag.ProyectoId = idProy;

            // KPIs
            var cmdKpi = new SqlCommand(@"
                SELECT COUNT(*) AS Total,
                    SUM(CASE WHEN Estado='DISPONIBLE' THEN 1 ELSE 0 END) AS Disponibles,
                    SUM(CASE WHEN Estado='RESERVADO'  THEN 1 ELSE 0 END) AS Reservados,
                    SUM(CASE WHEN Estado='VENDIDO'    THEN 1 ELSE 0 END) AS Vendidos,
                    SUM(CASE WHEN Estado='EN PROCESO' THEN 1 ELSE 0 END) AS EnProceso
                FROM Inmuebles WHERE IdProyecto=@id", con);
            cmdKpi.Parameters.AddWithValue("@id", idProy);
            using var rKpi = (SqlDataReader)await cmdKpi.ExecuteReaderAsync();
            int total = 0, disponibles = 0, reservados = 0, vendidos = 0, enProceso = 0;
            if (await rKpi.ReadAsync())
            {
                total = rKpi["Total"] == DBNull.Value ? 0 : (int)rKpi["Total"];
                disponibles = rKpi["Disponibles"] == DBNull.Value ? 0 : (int)rKpi["Disponibles"];
                reservados = rKpi["Reservados"] == DBNull.Value ? 0 : (int)rKpi["Reservados"];
                vendidos = rKpi["Vendidos"] == DBNull.Value ? 0 : (int)rKpi["Vendidos"];
                enProceso = rKpi["EnProceso"] == DBNull.Value ? 0 : (int)rKpi["EnProceso"];
            }
            rKpi.Close();

            ViewBag.Total = total;
            ViewBag.Disponibles = disponibles;
            ViewBag.Reservados = reservados;
            ViewBag.Vendidos = vendidos;
            ViewBag.EnProceso = enProceso;
            ViewBag.PctVendidos = total > 0 ? Math.Round((double)vendidos / total * 100, 1) : 0.0;
            ViewBag.PctDisponibles = total > 0 ? Math.Round((double)disponibles / total * 100, 1) : 0.0;
            ViewBag.PctReservados = total > 0 ? Math.Round((double)reservados / total * 100, 1) : 0.0;

            var cmdProy = new SqlCommand("SELECT ListaActual, ApartamentosPorLista, ModoLista FROM Proyectos WHERE IdProyectos=@id", con);
            cmdProy.Parameters.AddWithValue("@id", idProy);
            using var rP = (SqlDataReader)await cmdProy.ExecuteReaderAsync();
            int listaActual = 1, aptsPorLista = 0;
            string modoLista = "Manual";
            if (await rP.ReadAsync())
            {
                listaActual = rP["ListaActual"] == DBNull.Value ? 1 : (int)rP["ListaActual"];
                aptsPorLista = rP["ApartamentosPorLista"] == DBNull.Value ? 0 : (int)rP["ApartamentosPorLista"];
                modoLista = rP["ModoLista"]?.ToString() ?? "Manual";
            }
            rP.Close();
            ViewBag.ListaActual = listaActual;
            ViewBag.ApartamentosPorLista = aptsPorLista;
            ViewBag.ModoLista = modoLista;

            // Calcular cuántas listas tienen precio en este proyecto
            var cmdTotalListas = new SqlCommand(@"
                SELECT
                    MAX(CASE WHEN Lista1 > 0 THEN 1 ELSE 0 END) AS TL1,
                    MAX(CASE WHEN Lista2 > 0 THEN 1 ELSE 0 END) AS TL2,
                    MAX(CASE WHEN Lista3 > 0 THEN 1 ELSE 0 END) AS TL3,
                    MAX(CASE WHEN Lista4 > 0 THEN 1 ELSE 0 END) AS TL4,
                    MAX(CASE WHEN Lista5 > 0 THEN 1 ELSE 0 END) AS TL5
                FROM Inmuebles WHERE IdProyecto = @id", con);
            cmdTotalListas.Parameters.AddWithValue("@id", idProy);
            int totalListas = 1;
            using (var rTL = (SqlDataReader)await cmdTotalListas.ExecuteReaderAsync())
                if (await rTL.ReadAsync())
                    totalListas = (rTL["TL1"] == DBNull.Value ? 0 : (int)rTL["TL1"])
                                + (rTL["TL2"] == DBNull.Value ? 0 : (int)rTL["TL2"])
                                + (rTL["TL3"] == DBNull.Value ? 0 : (int)rTL["TL3"])
                                + (rTL["TL4"] == DBNull.Value ? 0 : (int)rTL["TL4"])
                                + (rTL["TL5"] == DBNull.Value ? 0 : (int)rTL["TL5"]);
            ViewBag.TotalListas = Math.Max(1, totalListas);

            return View();
        }

        /// <summary>
        /// Switches the active project in session without a DB write.
        /// Redirects to the dashboard with the new project context.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CambiarProyecto(int idProyecto, string nombreProyecto)
        {
            HttpContext.Session.SetString("ProyectoId", idProyecto.ToString());
            HttpContext.Session.SetString("ProyectoNombre", nombreProyecto ?? "");
            return RedirectToAction("Index");
        }

        /// <summary>
        /// Enables automatic list escalation by setting ApartamentosPorLista on the project.
        /// After updating the DB, broadcasts the current list level via SignalR so all
        /// connected clients reflect the change in real time.
        /// Performs UPDATE (Proyectos) and SELECT (Proyectos) queries.
        /// </summary>
        /// <param name="apartamentosPorLista">Number of sales required to advance to the next list.</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfigurarLista(int apartamentosPorLista)
        {
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;
            using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            var cmd = new SqlCommand("UPDATE Proyectos SET ApartamentosPorLista=@a, ModoLista='AUTO' WHERE IdProyectos=@id", con);
            cmd.Parameters.AddWithValue("@a", apartamentosPorLista);
            cmd.Parameters.AddWithValue("@id", idProy);
            await cmd.ExecuteNonQueryAsync();
            TempData["Exito"] = $"Modo automático activado: sube cada {apartamentosPorLista} vendidos.";
            var cmdLeer = new SqlCommand("SELECT ListaActual FROM Proyectos WHERE IdProyectos=@id", con);
            cmdLeer.Parameters.AddWithValue("@id", idProy);
            var listaActualVal = (int)((await cmdLeer.ExecuteScalarAsync()) ?? 1);
            await _hub.Clients.All.ListaActualizada(idProy, listaActualVal);
            return RedirectToAction("Index");
        }

        /// <summary>
        /// Manually sets the active price list for the whole project (manual mode).
        /// Switches ModoLista to 'MANUAL' and disables automatic escalation
        /// (ApartamentosPorLista = 0) so the chosen list stays fixed.
        /// Broadcasts the new list via SignalR so all clients update in real time.
        /// Performs an UPDATE (Proyectos) query.
        /// </summary>
        /// <param name="lista">List number (1–5) to activate for the project.</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarLista(int lista)
        {
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;
            if (lista < 1) lista = 1;
            if (lista > 5) lista = 5;
            using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            var cmd = new SqlCommand(
                "UPDATE Proyectos SET ListaActual=@l, ModoLista='MANUAL', ApartamentosPorLista=0 WHERE IdProyectos=@id", con);
            cmd.Parameters.AddWithValue("@l", lista);
            cmd.Parameters.AddWithValue("@id", idProy);
            await cmd.ExecuteNonQueryAsync();
            await _hub.Clients.All.ListaActualizada(idProy, lista);
            TempData["Exito"] = $"Lista {lista} activada en modo manual. El escalamiento automático quedó desactivado.";
            return RedirectToAction("Index");
        }
    }
}
