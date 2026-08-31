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
        private readonly IConfiguration _config;

        /// <summary>Initializes the controller with DB connection and strongly-typed SignalR hub.</summary>
        public DashboardController(IConfiguration config, IHubContext<VentasHub, IVentasClient> hub)
        {
            _conn = config.GetConnectionString("DefaultConnection")!;
            _hub = hub;
            _config = config;
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

            // ── Pulso del día ──────────────────────────────────────────────────────
            // FechaVenta se guarda en UTC; Colombia es UTC-5 fijo, así que "hoy" se
            // calcula desplazando ambos lados de la comparación.
            long valorTotal = 0, valorHoy = 0;
            int ventasHoy = 0;
            double horasJornada = 0;

            var cmdVal = new SqlCommand(
                "SELECT ISNULL(SUM(PrecioVenta),0) FROM Ventas WHERE IdProyecto=@id AND Estado='ACTIVA'", con);
            cmdVal.Parameters.AddWithValue("@id", idProy);
            valorTotal = Convert.ToInt64(await cmdVal.ExecuteScalarAsync());

            var cmdHoy = new SqlCommand(@"
                SELECT COUNT(*) AS Num, ISNULL(SUM(PrecioVenta),0) AS Valor,
                       MIN(FechaVenta) AS Primera
                FROM Ventas
                WHERE IdProyecto=@id AND Estado='ACTIVA'
                  AND CAST(DATEADD(HOUR,-5,FechaVenta) AS DATE) = CAST(DATEADD(HOUR,-5,GETUTCDATE()) AS DATE)", con);
            cmdHoy.Parameters.AddWithValue("@id", idProy);
            using (var rH = (SqlDataReader)await cmdHoy.ExecuteReaderAsync())
                if (await rH.ReadAsync())
                {
                    ventasHoy = Convert.ToInt32(rH["Num"]);
                    valorHoy = Convert.ToInt64(rH["Valor"]);
                    if (rH["Primera"] != DBNull.Value)
                        horasJornada = (DateTime.UtcNow - (DateTime)rH["Primera"]).TotalHours;
                }

            ViewBag.ValorTotal = valorTotal;
            ViewBag.VentasHoy = ventasHoy;
            ViewBag.ValorHoy = valorHoy;
            ViewBag.Ritmo = (ventasHoy > 0 && horasJornada >= 0.5)
                ? Math.Round(ventasHoy / horasJornada, 1) : 0d;

            // Última venta registrada (para "hace X minutos")
            var cmdUlt = new SqlCommand(@"
                SELECT TOP 1 v.FechaVenta, i.Apto, i.Torre, i.Metros,
                       u.Nombre + ' ' + u.Apellido AS Asesor
                FROM Ventas v
                JOIN Inmuebles i ON v.IdInmueble = i.IdInmuebles
                JOIN Usuarios  u ON v.IdUsuario  = u.IdUsuario
                WHERE v.IdProyecto=@id AND v.Estado='ACTIVA'
                ORDER BY v.FechaVenta DESC", con);
            cmdUlt.Parameters.AddWithValue("@id", idProy);
            using (var rU = (SqlDataReader)await cmdUlt.ExecuteReaderAsync())
                if (await rU.ReadAsync())
                {
                    ViewBag.UltimaMinutos = (int)Math.Max(0, (DateTime.UtcNow - (DateTime)rU["FechaVenta"]).TotalMinutes);
                    ViewBag.UltimaApto = rU["Apto"]?.ToString() ?? "";
                    ViewBag.UltimaTorre = rU["Torre"]?.ToString() ?? "";
                    ViewBag.UltimaAsesor = rU["Asesor"]?.ToString() ?? "";
                }

            // ── Actividad reciente (siembra del feed en vivo) ──────────────────────
            var actividad = new List<dynamic>();
            var cmdAct = new SqlCommand(@"
                SELECT TOP 8 FORMAT(DATEADD(HOUR,-5,v.FechaVenta),'HH:mm') AS Hora,
                       i.Apto, i.Torre, i.Metros, v.PrecioVenta,
                       u.Nombre + ' ' + u.Apellido AS Asesor
                FROM Ventas v
                JOIN Inmuebles i ON v.IdInmueble = i.IdInmuebles
                JOIN Usuarios  u ON v.IdUsuario  = u.IdUsuario
                WHERE v.IdProyecto=@id AND v.Estado='ACTIVA'
                ORDER BY v.FechaVenta DESC", con);
            cmdAct.Parameters.AddWithValue("@id", idProy);
            using (var rA = (SqlDataReader)await cmdAct.ExecuteReaderAsync())
                while (await rA.ReadAsync())
                    actividad.Add(new
                    {
                        Hora = rA["Hora"]?.ToString() ?? "",
                        Apto = rA["Apto"]?.ToString() ?? "",
                        Torre = rA["Torre"]?.ToString() ?? "",
                        Metros = rA["Metros"]?.ToString() ?? "",
                        Asesor = rA["Asesor"]?.ToString() ?? "",
                        Precio = Convert.ToInt64(rA["PrecioVenta"]),
                    });
            ViewBag.Actividad = actividad;

            // ── Listas de precio por área (solo lectura) ───────────────────────────
            // Se muestra el estado consolidado que hoy no existe en ninguna pantalla:
            // qué lista rige cada área, si está fija o escalando, y cuánto le falta.
            var listasArea = new List<dynamic>();
            var cmdLA = new SqlCommand(@"
                SELECT pal.Metros,
                       ISNULL(pal.ListaActual,1) AS ListaActual,
                       ISNULL(pal.AptsPorLista,0) AS AptsPorLista,
                       (SELECT COUNT(*) FROM Inmuebles i
                         WHERE i.IdProyecto=pal.IdProyecto AND i.Metros=pal.Metros) AS Total,
                       (SELECT COUNT(*) FROM Ventas v
                          JOIN Inmuebles i2 ON v.IdInmueble=i2.IdInmuebles
                         WHERE v.IdProyecto=pal.IdProyecto AND i2.Metros=pal.Metros
                           AND v.Estado='ACTIVA') AS Vendidos
                FROM ProyectoAreaListas pal
                WHERE pal.IdProyecto=@id
                ORDER BY TRY_CONVERT(decimal(10,2), REPLACE(pal.Metros,',','.')), pal.Metros", con);
            cmdLA.Parameters.AddWithValue("@id", idProy);
            using (var rLA = (SqlDataReader)await cmdLA.ExecuteReaderAsync())
                while (await rLA.ReadAsync())
                {
                    int apts = Convert.ToInt32(rLA["AptsPorLista"]);
                    int vend = Convert.ToInt32(rLA["Vendidos"]);
                    listasArea.Add(new
                    {
                        Metros = rLA["Metros"]?.ToString() ?? "",
                        Lista = Convert.ToInt32(rLA["ListaActual"]),
                        Apts = apts,
                        Total = Convert.ToInt32(rLA["Total"]),
                        Vendidos = vend,
                        // Cuántas ventas faltan para que esta área suba de lista (0 = fija)
                        Faltan = apts > 0 ? apts - (vend % apts) : 0,
                    });
                }
            ViewBag.ListasArea = listasArea;

            // ── Alertas accionables ────────────────────────────────────────────────
            // FechaEnProceso ya se venía guardando pero no se consultaba en ninguna
            // parte: un inmueble tomado hace rato es inventario congelado que nadie
            // puede vender, y hasta ahora era invisible.
            int minutosEstancado = _config.GetValue<int?>("Operacion:MinutosProcesoEstancado") ?? 45;
            ViewBag.MinutosEstancado = minutosEstancado;

            var estancados = new List<dynamic>();
            var cmdEst = new SqlCommand(@"
                SELECT i.IdInmuebles, i.Apto, i.Torre, i.Metros,
                       DATEDIFF(MINUTE, i.FechaEnProceso, GETDATE()) AS Minutos,
                       ISNULL(u.Nombre + ' ' + u.Apellido, '') AS Quien
                FROM Inmuebles i
                LEFT JOIN Usuarios u ON i.IdVendedorEnProceso = u.IdUsuario
                WHERE i.IdProyecto=@id AND i.Estado='EN PROCESO'
                  AND i.FechaEnProceso IS NOT NULL
                  AND DATEDIFF(MINUTE, i.FechaEnProceso, GETDATE()) >= @min
                ORDER BY i.FechaEnProceso", con);
            cmdEst.Parameters.AddWithValue("@id", idProy);
            cmdEst.Parameters.AddWithValue("@min", minutosEstancado);
            using (var rE = (SqlDataReader)await cmdEst.ExecuteReaderAsync())
                while (await rE.ReadAsync())
                    estancados.Add(new
                    {
                        Id = Convert.ToInt32(rE["IdInmuebles"]),
                        Apto = rE["Apto"]?.ToString() ?? "",
                        Torre = rE["Torre"]?.ToString() ?? "",
                        Metros = rE["Metros"]?.ToString() ?? "",
                        Minutos = Convert.ToInt32(rE["Minutos"]),
                        Quien = (rE["Quien"]?.ToString() ?? "").Trim(),
                    });
            ViewBag.Estancados = estancados;

            // Reservas vencidas según la vigencia del proyecto.
            // HorasVigenciaReserva = 0 significa que las reservas no vencen, que es
            // el comportamiento histórico y el valor por defecto.
            int horasVigencia = 0;
            var vencidas = new List<dynamic>();
            try
            {
                var cmdVig = new SqlCommand(
                    "SELECT ISNULL(HorasVigenciaReserva,0) FROM Proyectos WHERE IdProyectos=@id", con);
                cmdVig.Parameters.AddWithValue("@id", idProy);
                horasVigencia = Convert.ToInt32(await cmdVig.ExecuteScalarAsync() ?? 0);

                if (horasVigencia > 0)
                {
                    var cmdVenc = new SqlCommand(@"
                        SELECT i.IdInmuebles, i.Apto, i.Torre, i.Metros, i.PrecioReserva,
                               DATEDIFF(HOUR, i.FechaReserva, GETDATE()) AS Horas,
                               ISNULL(u.Nombre + ' ' + u.Apellido, '') AS Quien
                        FROM Inmuebles i
                        LEFT JOIN Usuarios u ON i.IdVendedorReserva = u.IdUsuario
                        WHERE i.IdProyecto=@id AND i.Estado='RESERVADO'
                          AND i.FechaReserva IS NOT NULL
                          AND DATEDIFF(HOUR, i.FechaReserva, GETDATE()) >= @horas
                        ORDER BY i.FechaReserva", con);
                    cmdVenc.Parameters.AddWithValue("@id", idProy);
                    cmdVenc.Parameters.AddWithValue("@horas", horasVigencia);
                    using var rV = (SqlDataReader)await cmdVenc.ExecuteReaderAsync();
                    while (await rV.ReadAsync())
                        vencidas.Add(new
                        {
                            Id = Convert.ToInt32(rV["IdInmuebles"]),
                            Apto = rV["Apto"]?.ToString() ?? "",
                            Torre = rV["Torre"]?.ToString() ?? "",
                            Metros = rV["Metros"]?.ToString() ?? "",
                            Horas = Convert.ToInt32(rV["Horas"]),
                            Quien = (rV["Quien"]?.ToString() ?? "").Trim(),
                        });
                }
            }
            catch (SqlException ex) when (ex.Message.Contains("Invalid column name"))
            {
                // Scripts/PanelAdmin.sql aún no se ha ejecutado: sin vencimiento de reservas.
                horasVigencia = 0;
            }
            ViewBag.HorasVigencia = horasVigencia;
            ViewBag.ReservasVencidas = vencidas;

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
    }
}
