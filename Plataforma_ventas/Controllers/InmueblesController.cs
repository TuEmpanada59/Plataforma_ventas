using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using Plataforma_ventas.Filters;
using Plataforma_ventas.Hubs;
using DColor = System.Drawing.Color;

namespace Plataforma_ventas.Controllers
{
    /// <summary>
    /// Administrator controller for property and project management:
    /// viewing/editing properties, reservations, sales confirmation, and list escalation.
    /// </summary>
    [RolAutorizado("Administrador", "Direccion")]
    public class InmueblesController : Controller
    {
        /// <summary>
        /// Nombre completo del usuario en sesión, para informar en los avisos en vivo
        /// quién tomó, reservó o vendió el inmueble.
        /// </summary>
        private string QuienSoy()
        {
            var n = HttpContext.Session.GetString("Nombre") ?? "";
            var a = HttpContext.Session.GetString("Apellido") ?? "";
            return $"{n} {a}".Trim();
        }

        private readonly string _conn;
        private readonly IHubContext<VentasHub, IVentasClient> _hub;

        /// <summary>Initializes the controller with DB connection and strongly-typed SignalR hub.</summary>
        public InmueblesController(IConfiguration config, IHubContext<VentasHub, IVentasClient> hub,
                                   Plataforma_ventas.Services.IAuditoriaService audit)
        {
            _conn = config.GetConnectionString("DefaultConnection")!;
            _hub = hub;
            _audit = audit;
        }

        private readonly Plataforma_ventas.Services.IAuditoriaService _audit;

        /// <summary>
        /// Lists all active projects with their property counts and estados.
        /// Performs a SELECT with aggregation over Proyectos and Inmuebles.
        /// </summary>
        public async Task<IActionResult> Proyectos()
        {
            ViewBag.Nombre = HttpContext.Session.GetString("Nombre") ?? "Admin";
            ViewBag.Apellido = HttpContext.Session.GetString("Apellido") ?? "";
            ViewBag.ProyectoActivo = HttpContext.Session.GetString("ProyectoNombre") ?? "Sin proyecto";

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            var proyectosGrid = new List<dynamic>();
            var cmdGrid = new SqlCommand(@"
                SELECT p.IdProyectos, p.Nombre, p.CodigoAcceso, p.TipProyecto, p.FechaCarga,
                       ISNULL(COUNT(i.IdInmuebles), 0) AS Total,
                       ISNULL(SUM(CASE WHEN i.Estado='DISPONIBLE' THEN 1 ELSE 0 END), 0) AS Disponibles,
                       ISNULL(SUM(CASE WHEN i.Estado='VENDIDO'    THEN 1 ELSE 0 END), 0) AS Vendidos,
                       ISNULL(SUM(CASE WHEN i.Estado='RESERVADO'  THEN 1 ELSE 0 END), 0) AS Reservados,
                       ISNULL(SUM(CASE WHEN i.Estado='EN PROCESO' THEN 1 ELSE 0 END), 0) AS EnProceso,
                       u.Nombre + ' ' + u.Apellido AS NombreAdmin
                FROM Proyectos p
                LEFT JOIN Inmuebles i ON i.IdProyecto = p.IdProyectos
                LEFT JOIN Usuarios u ON u.IdUsuario = p.IdAdminCreador
                WHERE p.Activo = 1
                GROUP BY p.IdProyectos, p.Nombre, p.CodigoAcceso, p.TipProyecto, p.FechaCarga, u.Nombre, u.Apellido
                ORDER BY p.FechaCarga DESC", con);
            using (var rg = (SqlDataReader)await cmdGrid.ExecuteReaderAsync())
                while (await rg.ReadAsync())
                    proyectosGrid.Add(new
                    {
                        Id = (int)rg["IdProyectos"],
                        Nombre = rg["Nombre"]?.ToString() ?? "",
                        Codigo = rg["CodigoAcceso"]?.ToString() ?? "",
                        Tipo = rg["TipProyecto"]?.ToString() ?? "APARTAMENTOS",
                        Total = rg["Total"] == DBNull.Value ? 0 : (int)rg["Total"],
                        Disponibles = rg["Disponibles"] == DBNull.Value ? 0 : (int)rg["Disponibles"],
                        Vendidos = rg["Vendidos"] == DBNull.Value ? 0 : (int)rg["Vendidos"],
                        Reservados = rg["Reservados"] == DBNull.Value ? 0 : (int)rg["Reservados"],
                        EnProceso = rg["EnProceso"] == DBNull.Value ? 0 : (int)rg["EnProceso"],
                        Admin = rg["NombreAdmin"]?.ToString() ?? "",
                    });

            ViewBag.ProyectosGrid = proyectosGrid;
            ViewBag.Proyectos = proyectosGrid.Select(p => ((int)p.Id, (string)p.Nombre)).ToList();
            return View();
        }

        /// <summary>
        /// Sets the active project in session and redirects to Index.
        /// Performs no DB queries — session only.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SeleccionarProyecto(int idProyecto, string nombreProyecto)
        {
            HttpContext.Session.SetString("ProyectoId", idProyecto.ToString());
            HttpContext.Session.SetString("ProyectoNombre", nombreProyecto ?? "");
            return RedirectToAction("Index");
        }

        /// <summary>
        /// Displays all properties for the active project with grouping by area and type.
        /// Supports optional torre and area filters. Performs SELECT queries for
        /// projects, project config, area lists, properties, and vendor names.
        /// </summary>
        /// <param name="torre">Optional project (torre) name to switch to.</param>
        /// <param name="area">Optional area (metros) filter.</param>
        public async Task<IActionResult> Index([FromQuery] string torre = "", [FromQuery] string area = "",
                                               [FromQuery] string etapa = "")
        {
            ViewBag.Nombre = HttpContext.Session.GetString("Nombre");
            ViewBag.Apellido = HttpContext.Session.GetString("Apellido");
            var proyNombre = HttpContext.Session.GetString("ProyectoNombre") ?? "Sin proyecto";
            var proyIdStr = HttpContext.Session.GetString("ProyectoId") ?? "0";
            ViewBag.ProyectoActivo = proyNombre;
            int idProy = int.TryParse(proyIdStr, out int pid) ? pid : 0;
            ViewBag.ProyectoId = idProy;
            if (idProy == 0) return RedirectToAction("Proyectos");

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            ViewBag.Unidad = await Proyecto.UnidadAsync(HttpContext, con, idProy);

            // Esta pantalla trabaja siempre sobre el proyecto activo, así que el topbar
            // muestra su nombre sin desplegable: listar todos los proyectos aquí solo
            // invitaba a cambiar de proyecto sin querer. Para cambiarlo está el selector
            // de las demás pantallas.
            ViewBag.OcultarSelectorProyecto = true;

            // Las etapas son una columna nueva: si el script de migración todavía no se
            // ejecutó, la pantalla sigue funcionando sin ellas en vez de caerse con un 500.
            var cmdColEtapa = new SqlCommand("SELECT COL_LENGTH('Inmuebles','Etapa')", con);
            bool hayColumnaEtapa = (await cmdColEtapa.ExecuteScalarAsync()) is not (null or DBNull);

            // Config del proyecto
            var cmdLista = new SqlCommand(
                "SELECT ListaActual, ApartamentosPorLista FROM Proyectos WHERE IdProyectos=@id", con);
            cmdLista.Parameters.AddWithValue("@id", idProy);
            int listaActual = 1, aptsPorLista = 0;
            using (var rL = (SqlDataReader)await cmdLista.ExecuteReaderAsync())
                if (await rL.ReadAsync())
                {
                    listaActual = rL["ListaActual"] == DBNull.Value ? 1 : (int)rL["ListaActual"];
                    aptsPorLista = rL["ApartamentosPorLista"] == DBNull.Value ? 0 : (int)rL["ApartamentosPorLista"];
                }
            ViewBag.ListaActual = listaActual;
            ViewBag.AptsPorLista = aptsPorLista;

            // Listas por área
            var listasXArea = new Dictionary<string, int>();
            var aptsXArea = new Dictionary<string, int>();
            var cmdPAL = new SqlCommand(
                "SELECT Metros, ListaActual, AptsPorLista FROM ProyectoAreaListas WHERE IdProyecto=@id", con);
            cmdPAL.Parameters.AddWithValue("@id", idProy);
            using (var rPAL = (SqlDataReader)await cmdPAL.ExecuteReaderAsync())
                while (await rPAL.ReadAsync())
                {
                    var metros = rPAL["Metros"]?.ToString() ?? "";
                    listasXArea[metros] = rPAL["ListaActual"] == DBNull.Value ? 1 : (int)rPAL["ListaActual"];
                    aptsXArea[metros] = rPAL["AptsPorLista"] == DBNull.Value ? 0 : (int)rPAL["AptsPorLista"];
                }
            ViewBag.ListasXArea = listasXArea;
            ViewBag.AptsXArea = aptsXArea;

            // Último cambio manual de precio por área que todavía se puede devolver.
            // Se muestra como un botón "Deshacer" en la tarjeta del área: es donde el
            // administrador acaba de equivocarse, y mandarlo a otra pantalla a buscarlo
            // en un historial no sirve cuando el lanzamiento está en curso.
            var deshacerXArea = new Dictionary<string, (long Id, string Listas)>();
            var cmdTablaAj = new SqlCommand("SELECT OBJECT_ID('AjustesPrecio','U')", con);
            if ((await cmdTablaAj.ExecuteScalarAsync()) is not (null or DBNull))
            {
                var cmdDesh = new SqlCommand(@"
                    SELECT IdAjuste, Metros, Listas FROM AjustesPrecio
                    WHERE IdProyecto=@p AND Tipo='MANUAL' AND Revertido=0
                    ORDER BY IdAjuste DESC", con);
                cmdDesh.Parameters.AddWithValue("@p", idProy);
                using (var rd = (SqlDataReader)await cmdDesh.ExecuteReaderAsync())
                    while (await rd.ReadAsync())
                    {
                        var m = rd["Metros"]?.ToString() ?? "";
                        // Solo el más reciente de cada área: los anteriores siguen en el
                        // historial de Cargar Excel.
                        if (!deshacerXArea.ContainsKey(m))
                            deshacerXArea[m] = (Convert.ToInt64(rd["IdAjuste"]), rd["Listas"]?.ToString() ?? "");
                    }
            }
            ViewBag.DeshacerXArea = deshacerXArea;

            // Inmuebles
            var lista = new List<dynamic>();
            var cmd = new SqlCommand($@"
                SELECT IdInmuebles,Apto,Tipo,Piso,Metros,
                       Lista1,Lista2,Lista3,Lista4,Lista5,
                       Estado,Torre,{(hayColumnaEtapa ? "ISNULL(Etapa,'')" : "''")} AS Etapa,
                       IdVendedorEnProceso,IdVendedorReserva
                FROM Inmuebles WHERE IdProyecto=@id ORDER BY Metros, Piso DESC, Apto", con);
            cmd.Parameters.AddWithValue("@id", idProy);
            using (var reader = (SqlDataReader)await cmd.ExecuteReaderAsync())
                while (await reader.ReadAsync())
                    lista.Add(new
                    {
                        Id = (int)reader["IdInmuebles"],
                        Apto = reader["Apto"]?.ToString() ?? "",
                        Tipo = reader["Tipo"]?.ToString() ?? "",
                        Piso = reader["Piso"]?.ToString() ?? "",
                        Metros = reader["Metros"]?.ToString() ?? "",
                        Lista1 = reader["Lista1"]?.ToString() ?? "",
                        Lista2 = reader["Lista2"]?.ToString() ?? "",
                        Lista3 = reader["Lista3"]?.ToString() ?? "",
                        Lista4 = reader["Lista4"]?.ToString() ?? "",
                        Lista5 = reader["Lista5"]?.ToString() ?? "",
                        Estado = reader["Estado"]?.ToString() ?? "",
                        Torre = reader["Torre"]?.ToString() ?? "",
                        Etapa = reader["Etapa"]?.ToString() ?? "",
                        IdVendedorEnProceso = reader["IdVendedorEnProceso"] == DBNull.Value ? 0 : (int)reader["IdVendedorEnProceso"],
                        IdVendedorReserva = reader["IdVendedorReserva"] == DBNull.Value ? 0 : (int)reader["IdVendedorReserva"],
                    });

            // Diccionario vendedores
            var vendedores = new Dictionary<int, string>();
            var cmdVend = new SqlCommand(
                @"SELECT IdUsuario, Nombre+' '+Apellido AS NombreCompleto, Rol FROM Usuarios", con);
            using (var rv = (SqlDataReader)await cmdVend.ExecuteReaderAsync())
                while (await rv.ReadAsync())
                    vendedores[(int)rv["IdUsuario"]] = rv["NombreCompleto"]?.ToString() ?? "";
            ViewBag.Vendedores = vendedores;

            // Torres: son las del propio proyecto (columna Torre de los inmuebles, que sale
            // de la columna TORRE del Excel o del nombre de la unidad, "1204 T3"). Antes se
            // deducían de otros proyectos con el mismo prefijo de nombre, lo que obligaba a
            // cambiar de proyecto para ver "otra torre" y confundía el inventario.
            var torres = lista
                .Select(i => (string)i.Torre)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct()
                .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Un filtro por una torre que este proyecto no tiene se ignora en vez de
            // devolver una pantalla vacía sin explicación.
            string torreActual = torres.Contains(torre, StringComparer.OrdinalIgnoreCase)
                ? torres.First(t => string.Equals(t, torre, StringComparison.OrdinalIgnoreCase))
                : "";

            ViewBag.Torres = torres;
            ViewBag.TorreActual = torreActual;

            // Etapas: cada hoja del Excel del proyecto. La mayoría de proyectos vienen en
            // una sola hoja y no tienen etapa, así que el filtro solo aparece si hay varias.
            var etapas = lista
                .Select(i => (string)i.Etapa)
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct()
                .OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
                .ToList();

            string etapaActual = etapas.Contains(etapa, StringComparer.OrdinalIgnoreCase)
                ? etapas.First(e => string.Equals(e, etapa, StringComparison.OrdinalIgnoreCase))
                : "";

            ViewBag.Etapas = etapas;
            ViewBag.EtapaActual = etapaActual;

            // El resto de la pantalla (áreas, tabla y KPIs) se calcula sobre la torre y la
            // etapa seleccionadas; sin filtro, sobre todo el proyecto.
            var listaProyecto = lista;
            if (!string.IsNullOrEmpty(torreActual))
                lista = lista.Where(i => string.Equals((string)i.Torre, torreActual,
                                                       StringComparison.OrdinalIgnoreCase)).ToList();
            if (!string.IsNullOrEmpty(etapaActual))
                lista = lista.Where(i => string.Equals((string)i.Etapa, etapaActual,
                                                       StringComparison.OrdinalIgnoreCase)).ToList();

            // Grupos de áreas
            long PrecioLista(dynamic inm, int n)
            {
                var raw = n == 1 ? inm.Lista1 : n == 2 ? inm.Lista2 : n == 3 ? inm.Lista3 : n == 4 ? inm.Lista4 : inm.Lista5;
                var limpio = (raw?.ToString() ?? "0").Replace("$", "").Replace(".", "").Replace(",", "").Replace(" ", "").Trim();
                return long.TryParse(limpio, out long v) ? v : 0;
            }
            var grupos = lista
                .GroupBy(i => new { Metros = (string)i.Metros, Tipo = (string)i.Tipo })
                .Select(g => new {
                    Metros = g.Key.Metros,
                    Tipo = g.Key.Tipo,
                    Total = g.Count(),
                    Disponibles = g.Count(x => x.Estado == "DISPONIBLE"),
                    Vendidos = g.Count(x => x.Estado == "VENDIDO"),
                    EnProceso = g.Count(x => x.Estado == "EN PROCESO"),
                    Reservados = g.Count(x => x.Estado == "RESERVADO"),
                    // Torres en las que existe esta área, para verlo sin abrir la tabla.
                    Torres = g.Select(x => (string)x.Torre)
                              .Where(t => !string.IsNullOrWhiteSpace(t))
                              .Distinct()
                              .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
                              .ToList(),
                    PrecioL1 = g.Select(x => PrecioLista(x, 1)).Where(p => p > 0).DefaultIfEmpty(0).Min(),
                    PrecioL2 = g.Select(x => PrecioLista(x, 2)).Where(p => p > 0).DefaultIfEmpty(0).Min(),
                    PrecioL3 = g.Select(x => PrecioLista(x, 3)).Where(p => p > 0).DefaultIfEmpty(0).Min(),
                    PrecioL4 = g.Select(x => PrecioLista(x, 4)).Where(p => p > 0).DefaultIfEmpty(0).Min(),
                    PrecioL5 = g.Select(x => PrecioLista(x, 5)).Where(p => p > 0).DefaultIfEmpty(0).Min(),
                    PrecioActivo = g.Select(x => PrecioLista(x, listaActual)).Where(p => p > 0).DefaultIfEmpty(0).Min(),
                })
                .OrderBy(g => {
                    if (double.TryParse(g.Metros.Replace(",", "."),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out double m)) return m;
                    return 0;
                })
                .ToList<dynamic>();
            ViewBag.Grupos = grupos;

            ViewBag.AreaActual = area;
            var listaFiltrada = string.IsNullOrEmpty(area)
                ? lista
                : lista.Where(x => (string)x.Metros == area).ToList();
            ViewBag.Inmuebles = listaFiltrada;
            ViewBag.Total = lista.Count;
            ViewBag.Disponibles = lista.Count(x => x.Estado == "DISPONIBLE");
            ViewBag.Reservados = lista.Count(x => x.Estado == "RESERVADO");
            ViewBag.Vendidos = lista.Count(x => x.Estado == "VENDIDO");
            ViewBag.EnProceso = lista.Count(x => x.Estado == "EN PROCESO");

            // La subida automática cuenta las ventas de todo el proyecto, no las de la
            // torre que se esté mirando.
            int vendidosTotal = listaProyecto.Count(x => x.Estado == "VENDIDO");
            ViewBag.ProximaLista = aptsPorLista > 0
                ? aptsPorLista - (vendidosTotal % aptsPorLista)
                : 0;

            return View();
        }

        /// <summary>
        /// Atomically reserves a property for the current admin user.
        /// Uses a single UPDATE…WHERE Estado='DISPONIBLE' to eliminate the race condition
        /// that would occur with a separate SELECT then UPDATE. If rows affected == 0
        /// the property was already taken by another concurrent request — no double-booking.
        /// Locks in the current list price at reservation time.
        /// </summary>
        /// <param name="idInmueble">Property to reserve.</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReservarInmueble(int idInmueble, string observacion = "")
        {
            int idUsuario = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            var cmdMetros = new SqlCommand("SELECT Metros FROM Inmuebles WHERE IdInmuebles=@id", con);
            cmdMetros.Parameters.AddWithValue("@id", idInmueble);
            var metros = (await cmdMetros.ExecuteScalarAsync())?.ToString() ?? "";

            var cmdLista = new SqlCommand(@"
                SELECT ISNULL(pal.ListaActual, p.ListaActual) AS ListaActual
                FROM Proyectos p
                LEFT JOIN ProyectoAreaListas pal
                    ON pal.IdProyecto = p.IdProyectos AND pal.Metros = @metros
                WHERE p.IdProyectos = @proy", con);
            cmdLista.Parameters.AddWithValue("@metros", metros);
            cmdLista.Parameters.AddWithValue("@proy", idProy);
            int listaActual = (int)((await cmdLista.ExecuteScalarAsync()) ?? 1);

            var colLista = Listas.ColumnaLista(listaActual);
            var cmdPrecio = new SqlCommand($"SELECT {colLista} FROM Inmuebles WHERE IdInmuebles=@id", con);
            cmdPrecio.Parameters.AddWithValue("@id", idInmueble);
            var rawPrecio = (await cmdPrecio.ExecuteScalarAsync())?.ToString() ?? "0";
            var limpio = rawPrecio.Replace("$", "").Replace(".", "").Replace(",", "").Replace(" ", "").Trim();
            long.TryParse(limpio, out long precioReserva);

            // Atomic reserve: only succeeds if still DISPONIBLE — prevents double-booking
            var cmd = new SqlCommand(@"
                UPDATE Inmuebles
                SET Estado='RESERVADO', IdVendedorReserva=@uid,
                    PrecioReserva=@precio, FechaReserva=GETDATE(),
                    ObservacionReserva=@obs
                WHERE IdInmuebles=@id AND Estado='DISPONIBLE'", con);
            cmd.Parameters.AddWithValue("@uid", idUsuario);
            cmd.Parameters.AddWithValue("@precio", precioReserva);
            cmd.Parameters.AddWithValue("@obs", (observacion ?? "").Trim());
            cmd.Parameters.AddWithValue("@id", idInmueble);
            var affected = await cmd.ExecuteNonQueryAsync();
            if (affected == 0)
            {
                var uOcupado = await Proyecto.UnidadAsync(HttpContext, con, idProy);
                TempData["Error"] = $"{uOcupado.Demostrativo} {uOcupado.Singular} ya no está disponible.";
                return RedirectToAction("Index");
            }

            await _hub.Clients.All.InmuebleActualizado(idProy, idInmueble, "RESERVADO", QuienSoy());
            var uRes = await Proyecto.UnidadAsync(HttpContext, con, idProy);
            TempData["Exito"] = $"{uRes.Titulo} reservad{uRes.Fin}. Precio bloqueado: ${string.Format("{0:N0}", precioReserva)}";
            return RedirectToAction("Index");
        }

        /// <summary>
        /// Lists all currently RESERVADO properties for the active project.
        /// Performs a SELECT query joining Inmuebles and Usuarios.
        /// </summary>
        public async Task<IActionResult> Reservas()
        {
            ViewBag.Nombre = HttpContext.Session.GetString("Nombre") ?? "";
            ViewBag.Apellido = HttpContext.Session.GetString("Apellido") ?? "";
            ViewBag.ProyectoActivo = HttpContext.Session.GetString("ProyectoNombre") ?? "";
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            var proyectos = new List<(int Id, string Nombre)>();
            var cmdList = new SqlCommand(
                "SELECT IdProyectos, Nombre FROM Proyectos WHERE Activo=1 ORDER BY FechaCarga DESC", con);
            using (var rp = (SqlDataReader)await cmdList.ExecuteReaderAsync())
                while (await rp.ReadAsync())
                    proyectos.Add(((int)rp["IdProyectos"], rp["Nombre"]?.ToString() ?? ""));
            ViewBag.Proyectos = proyectos;

            // La observación es una columna agregada después: si el script no se ejecutó,
            // la pantalla sigue funcionando sin ella.
            var cmdColObs = new SqlCommand("SELECT COL_LENGTH('Inmuebles','ObservacionReserva')", con);
            bool hayColumnaObs = (await cmdColObs.ExecuteScalarAsync()) is not (null or DBNull);
            ViewBag.HayObservacion = hayColumnaObs;

            var cmd = new SqlCommand($@"
                SELECT i.IdInmuebles, i.Apto, i.Metros, i.Tipo, i.Torre, i.Piso,
                       i.PrecioReserva, i.FechaReserva, i.IdVendedorReserva,
                       {(hayColumnaObs ? "ISNULL(i.ObservacionReserva,'')" : "''")} AS Observacion,
                       u.Nombre + ' ' + u.Apellido AS NombreVendedor
                FROM Inmuebles i
                LEFT JOIN Usuarios u ON u.IdUsuario = i.IdVendedorReserva
                WHERE i.IdProyecto = @proy AND i.Estado = 'RESERVADO'
                ORDER BY i.FechaReserva DESC", con);
            cmd.Parameters.AddWithValue("@proy", idProy);

            var lista = new List<dynamic>();
            using var rr = (SqlDataReader)await cmd.ExecuteReaderAsync();
            while (await rr.ReadAsync())
                lista.Add(new
                {
                    Id = (int)rr["IdInmuebles"],
                    Apto = rr["Apto"]?.ToString() ?? "",
                    Metros = rr["Metros"]?.ToString() ?? "",
                    Tipo = rr["Tipo"]?.ToString() ?? "",
                    Torre = rr["Torre"]?.ToString() ?? "",
                    Piso = rr["Piso"]?.ToString() ?? "",
                    PrecioReserva = rr["PrecioReserva"] == DBNull.Value ? 0L : (long)rr["PrecioReserva"],
                    FechaReserva = rr["FechaReserva"] == DBNull.Value ? "" :
                                     ((DateTime)rr["FechaReserva"]).ToString("dd/MM/yyyy HH:mm"),
                    NombreVendedor = rr["NombreVendedor"]?.ToString() ?? "",
                    IdVendedorReserva = rr["IdVendedorReserva"] == DBNull.Value ? 0 : (int)rr["IdVendedorReserva"],
                    Observacion = rr["Observacion"]?.ToString() ?? "",
                });
            rr.Close();

            // Asesores a los que se puede asignar una reserva. Las que vienen del Excel
            // entran sin dueño, y sin poder asignarlas la comisión no queda registrada.
            var asesores = new List<(int Id, string Nombre)>();
            var cmdAses = new SqlCommand(@"
                SELECT IdUsuario, Nombre+' '+Apellido AS NombreCompleto
                FROM Usuarios WHERE Rol='Vendedor' ORDER BY Nombre, Apellido", con);
            using (var ra = (SqlDataReader)await cmdAses.ExecuteReaderAsync())
                while (await ra.ReadAsync())
                    asesores.Add((Convert.ToInt32(ra["IdUsuario"]), ra["NombreCompleto"]?.ToString() ?? ""));
            ViewBag.Asesores = asesores;

            ViewBag.Reservas = lista;
            return View();
        }

        /// <summary>
        /// Displays the form to continue a sale from an existing reservation.
        /// Performs SELECT queries for the reserved property details and the client list.
        /// </summary>
        /// <param name="idInmueble">Reserved property identifier.</param>
        public async Task<IActionResult> ContinuarVenta(int idInmueble)
        {
            ViewBag.Nombre = HttpContext.Session.GetString("Nombre") ?? "";
            ViewBag.Apellido = HttpContext.Session.GetString("Apellido") ?? "";
            ViewBag.ProyectoActivo = HttpContext.Session.GetString("ProyectoNombre") ?? "";
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            ViewBag.Medios = await MediosRepo.ListarAsync(con);

            var proyectos = new List<(int Id, string Nombre)>();
            var cmdList = new SqlCommand(
                "SELECT IdProyectos, Nombre FROM Proyectos WHERE Activo=1 ORDER BY FechaCarga DESC", con);
            using (var rp = (SqlDataReader)await cmdList.ExecuteReaderAsync())
                while (await rp.ReadAsync())
                    proyectos.Add(((int)rp["IdProyectos"], rp["Nombre"]?.ToString() ?? ""));
            ViewBag.Proyectos = proyectos;

            var cmd = new SqlCommand(@"
                SELECT i.IdInmuebles, i.Apto, i.Metros, i.Tipo, i.Torre, i.Piso,
                       i.PrecioReserva,
                       u.Nombre + ' ' + u.Apellido AS NombreVendedor
                FROM Inmuebles i
                LEFT JOIN Usuarios u ON u.IdUsuario = i.IdVendedorReserva
                WHERE i.IdInmuebles = @id AND i.Estado = 'RESERVADO'", con);
            cmd.Parameters.AddWithValue("@id", idInmueble);
            using var r = (SqlDataReader)await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync())
            {
                TempData["Error"] = "Este inmueble ya no está reservado.";
                return RedirectToAction("Reservas");
            }
            ViewBag.IdInmueble = (int)r["IdInmuebles"];
            ViewBag.Apto = r["Apto"]?.ToString() ?? "";
            ViewBag.Metros = r["Metros"]?.ToString() ?? "";
            ViewBag.Tipo = r["Tipo"]?.ToString() ?? "";
            ViewBag.Torre = r["Torre"]?.ToString() ?? "";
            ViewBag.PrecioReserva = r["PrecioReserva"] == DBNull.Value ? 0L : (long)r["PrecioReserva"];
            ViewBag.Vendedor = r["NombreVendedor"]?.ToString() ?? "";
            r.Close();

            var cmdCli = new SqlCommand(
                "SELECT IdCliente, Nombre+' '+Apellido AS NombreCompleto, Documento FROM Clientes ORDER BY Nombre", con);
            var clientes = new List<dynamic>();
            using var rC = (SqlDataReader)await cmdCli.ExecuteReaderAsync();
            while (await rC.ReadAsync())
                clientes.Add(new
                {
                    Id = (int)rC["IdCliente"],
                    Nombre = rC["NombreCompleto"]?.ToString() ?? "",
                    Documento = rC["Documento"]?.ToString() ?? "",
                });
            ViewBag.Clientes = clientes;

            return View();
        }

        /// <summary>
        /// Confirms and records a sale for an admin-managed reservation.
        /// Uses the locked PrecioReserva price. Marks the property as VENDIDO
        /// and broadcasts the change via SignalR.
        /// Performs INSERT (Ventas, Clientes), UPDATE (Inmuebles) queries.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmarVentaReserva(int idInmueble, long precioVenta,
            int? idClienteExistente, string tipoCliente, string destino, bool sagrilaftConsultado,
            string observaciones, string clienteMedio,
            string clienteNombre, string clienteApellido, string clienteDocumento,
            string clienteCelular, string clienteCorreo, string clienteDireccion)
        {
            int idUsuario = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;

            // Cumplimiento SAGRILAFT: no se puede registrar la venta sin confirmar la consulta previa.
            if (!sagrilaftConsultado)
            {
                TempData["Error"] = "Por favor, consulte el cliente y recargue la página para realizar el nuevo registro.";
                return RedirectToAction("ContinuarVenta", new { idInmueble });
            }

            if (tipoCliente == "existente" && (!idClienteExistente.HasValue || idClienteExistente.Value <= 0))
            {
                TempData["Error"] = "Por favor ingrese los datos del cliente para continuar con la venta.";
                return RedirectToAction("ContinuarVenta", new { idInmueble });
            }
            if (tipoCliente != "existente" && (string.IsNullOrWhiteSpace(clienteNombre) || string.IsNullOrWhiteSpace(clienteDocumento)))
            {
                TempData["Error"] = "Por favor ingrese los datos del cliente para continuar con la venta.";
                return RedirectToAction("ContinuarVenta", new { idInmueble });
            }

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            using var tx = (SqlTransaction)await con.BeginTransactionAsync();

            // Verificar reserva dentro del proyecto activo y leer el precio bloqueado.
            string metros = ""; long precioFijo = 0;
            var cmdCheck = new SqlCommand(
                "SELECT Metros, PrecioReserva FROM Inmuebles WHERE IdInmuebles=@id AND Estado='RESERVADO' AND IdProyecto=@proy", con, tx);
            cmdCheck.Parameters.AddWithValue("@id", idInmueble);
            cmdCheck.Parameters.AddWithValue("@proy", idProy);
            using (var rCheck = (SqlDataReader)await cmdCheck.ExecuteReaderAsync())
            {
                if (!await rCheck.ReadAsync())
                {
                    TempData["Error"] = "Este inmueble ya no está reservado.";
                    return RedirectToAction("Reservas");
                }
                metros = rCheck["Metros"]?.ToString() ?? "";
                precioFijo = rCheck["PrecioReserva"] == DBNull.Value ? 0 : (long)rCheck["PrecioReserva"];
            }
            if (precioFijo <= 0)
            {
                TempData["Error"] = "La reserva no tiene un precio bloqueado válido.";
                return RedirectToAction("Reservas");
            }

            var cmdListaApl = new SqlCommand(@"
                SELECT ISNULL(pal.ListaActual, p.ListaActual) AS ListaActual
                FROM Proyectos p
                LEFT JOIN ProyectoAreaListas pal
                    ON pal.IdProyecto = p.IdProyectos AND pal.Metros = @metros
                WHERE p.IdProyectos = @proy", con, tx);
            cmdListaApl.Parameters.AddWithValue("@metros", metros);
            cmdListaApl.Parameters.AddWithValue("@proy", idProy);
            int listaAplicada = Convert.ToInt32((await cmdListaApl.ExecuteScalarAsync()) ?? 1);

            int idCliente;
            bool clienteReutilizado = false;
            if (tipoCliente == "existente" && idClienteExistente.HasValue && idClienteExistente.Value > 0)
                idCliente = idClienteExistente.Value;
            else
            {
                // Reutiliza el cliente si ya existe uno con ese documento, en vez de duplicarlo.
                var altaCliente = await ClienteRepo.ObtenerOCrearAsync(con, tx,
                    clienteNombre, clienteApellido, clienteDocumento,
                    clienteCelular, clienteCorreo, clienteDireccion, clienteMedio);
                idCliente = altaCliente.IdCliente;
                clienteReutilizado = altaCliente.Reutilizado;
            }

            // Marcar vendido de forma ATÓMICA (verifica reserva + proyecto).
            var cmdInm = new SqlCommand(@"UPDATE Inmuebles
                SET Estado='VENDIDO', IdVendedorReserva=NULL,
                    PrecioReserva=NULL, FechaReserva=NULL, ObservacionReserva=NULL
                WHERE IdInmuebles=@id AND Estado='RESERVADO' AND IdProyecto=@proy", con, tx);
            cmdInm.Parameters.AddWithValue("@id", idInmueble);
            cmdInm.Parameters.AddWithValue("@proy", idProy);
            if (await cmdInm.ExecuteNonQueryAsync() == 0)
            {
                TempData["Error"] = "Esta reserva ya no está disponible.";
                return RedirectToAction("Reservas");
            }

            var cmdVenta = new SqlCommand(@"INSERT INTO Ventas
                (IdInmueble,IdCliente,IdUsuario,IdProyecto,ListaAplicada,PrecioVenta,Destino,Estado,Observaciones)
                VALUES (@inm,@cli,@usr,@proy,@lista,@precio,@destino,'ACTIVA',@obsVenta)", con, tx);
            cmdVenta.Parameters.AddWithValue("@inm", idInmueble);
            cmdVenta.Parameters.AddWithValue("@cli", idCliente);
            cmdVenta.Parameters.AddWithValue("@usr", idUsuario);
            cmdVenta.Parameters.AddWithValue("@proy", idProy);
            cmdVenta.Parameters.AddWithValue("@lista", listaAplicada);
            cmdVenta.Parameters.AddWithValue("@precio", precioFijo);
            cmdVenta.Parameters.AddWithValue("@destino", Texto.DestinoVenta(destino));
            cmdVenta.Parameters.AddWithValue("@obsVenta", (observaciones ?? "").Trim());
            await cmdVenta.ExecuteNonQueryAsync();

            await tx.CommitAsync();

            await _hub.Clients.All.InmuebleActualizado(idProy, idInmueble, "VENDIDO", QuienSoy());
            TempData["Exito"] = $"¡Venta confirmada! Precio aplicado: ${string.Format("{0:N0}", precioFijo)}";
            return RedirectToAction("Reservas");
        }

        /// <summary>
        /// Assigns (or reassigns) the adviser that owns a reservation. Reservations that come
        /// from the project spreadsheet arrive with no adviser, and without this the sale
        /// would end up credited to nobody.
        /// </summary>
        /// <param name="idInmueble">Reserved property.</param>
        /// <param name="idVendedor">Adviser to credit the reservation to; 0 leaves it unassigned.</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AsignarReserva(int idInmueble, int idVendedor)
        {
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            // El WHERE sobre el estado es la guardia: si la reserva se liberó o se vendió
            // mientras el administrador tenía la pantalla abierta, no se toca nada.
            var cmd = new SqlCommand(@"
                UPDATE Inmuebles SET IdVendedorReserva=@v
                WHERE IdInmuebles=@id AND IdProyecto=@proy AND Estado='RESERVADO'", con);
            cmd.Parameters.AddWithValue("@v", idVendedor > 0 ? (object)idVendedor : DBNull.Value);
            cmd.Parameters.AddWithValue("@id", idInmueble);
            cmd.Parameters.AddWithValue("@proy", idProy);

            if (await cmd.ExecuteNonQueryAsync() == 0)
            {
                TempData["Error"] = "Esa reserva ya no está activa.";
                return RedirectToAction("Reservas");
            }

            var cmdNom = new SqlCommand(
                "SELECT Nombre+' '+Apellido FROM Usuarios WHERE IdUsuario=@v", con);
            cmdNom.Parameters.AddWithValue("@v", idVendedor);
            var nombreAsesor = idVendedor > 0
                ? (await cmdNom.ExecuteScalarAsync())?.ToString() ?? "el asesor"
                : "";

            await _hub.Clients.All.InmuebleActualizado(idProy, idInmueble, "RESERVADO", nombreAsesor);

            var uAsig = await Proyecto.UnidadAsync(HttpContext, con, idProy);
            TempData["Exito"] = idVendedor > 0
                ? $"Reserva asignada a {nombreAsesor}."
                : $"La reserva de {uAsig.Articulo} {uAsig.Singular} quedó sin asesor asignado.";
            return RedirectToAction("Reservas");
        }


        /// <summary>
        /// Excel con las reservas activas del proyecto: unidad, torre, precio bloqueado,
        /// asesor, observación y fecha.
        /// </summary>
        /// <remarks>
        /// Va aparte de los informes generales a propósito. Una reserva es información
        /// operativa del día —a quién hay que perseguir para que cierre— mientras que los
        /// informes miden lo que ya se cerró. Mezclarlas obligaría a filtrar cada vez.
        ///
        /// Se entrega en Excel y no en PDF porque este listado se trabaja: se filtra, se
        /// ordena y se le agregan columnas. El PDF sirve para presentar, no para eso.
        /// </remarks>
        public async Task<IActionResult> ExportarReservas()
        {
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;
            var proyNombre = HttpContext.Session.GetString("ProyectoNombre") ?? "Proyecto";

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            var u = await Proyecto.UnidadAsync(HttpContext, con, idProy);

            // Columnas agregadas después: el informe no puede depender de que el script
            // esté al día.
            var cmdCols = new SqlCommand(@"SELECT COL_LENGTH('Inmuebles','ObservacionReserva'),
                                                  COL_LENGTH('Inmuebles','Etapa')", con);
            bool hayObs = false, hayEtapa = false;
            using (var rc = (SqlDataReader)await cmdCols.ExecuteReaderAsync())
                if (await rc.ReadAsync())
                {
                    hayObs = rc[0] is not (null or DBNull);
                    hayEtapa = rc[1] is not (null or DBNull);
                }

            var reservas = new List<(string Apto, string Torre, string Etapa, string Piso, string Tipo,
                                     string Metros, long Precio, string Asesor, string Obs, string Fecha)>();
            var cmd = new SqlCommand($@"
                SELECT i.Apto, i.Torre, i.Piso, i.Tipo, i.Metros,
                       {(hayEtapa ? "ISNULL(i.Etapa,'')" : "''")} AS Etapa,
                       {(hayObs ? "ISNULL(i.ObservacionReserva,'')" : "''")} AS Observacion,
                       ISNULL(i.PrecioReserva,0) AS PrecioReserva, i.FechaReserva,
                       ISNULL(us.Nombre + ' ' + us.Apellido, '') AS Asesor
                FROM Inmuebles i
                LEFT JOIN Usuarios us ON us.IdUsuario = i.IdVendedorReserva
                WHERE i.IdProyecto=@proy AND i.Estado='RESERVADO'
                ORDER BY i.Torre, i.FechaReserva DESC", con);
            cmd.Parameters.AddWithValue("@proy", idProy);
            using (var r = (SqlDataReader)await cmd.ExecuteReaderAsync())
                while (await r.ReadAsync())
                    reservas.Add((
                        r["Apto"]?.ToString() ?? "",
                        r["Torre"]?.ToString() ?? "",
                        r["Etapa"]?.ToString() ?? "",
                        r["Piso"]?.ToString() ?? "",
                        r["Tipo"]?.ToString() ?? "",
                        r["Metros"]?.ToString() ?? "",
                        Convert.ToInt64(r["PrecioReserva"]),
                        r["Asesor"]?.ToString() ?? "",
                        r["Observacion"]?.ToString() ?? "",
                        r["FechaReserva"] == DBNull.Value ? "" : ((DateTime)r["FechaReserva"]).ToString("dd/MM/yyyy HH:mm")));

            ExcelPackage.License.SetNonCommercialPersonal("Londoño Gómez");
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Reservas");

            ws.Cells[1, 1].Value = $"Informe de reservas — {proyNombre}";
            ws.Cells[1, 1].Style.Font.Bold = true;
            ws.Cells[1, 1].Style.Font.Size = 14;
            ws.Cells[1, 1].Style.Font.Color.SetColor(DColor.FromArgb(0, 58, 112));
            ws.Cells[1, 1, 1, 9].Merge = true;

            ws.Cells[2, 1].Value = $"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}  ·  {reservas.Count} reservas activas";
            ws.Cells[2, 1].Style.Font.Color.SetColor(DColor.Gray);
            ws.Cells[2, 1, 2, 9].Merge = true;

            var headers = new[] { u.Titulo, "Torre", "Etapa", "Piso", "Tipo", "Área m²",
                                  "Precio bloqueado", "Asesor", "Observación", "Fecha de reserva" };
            for (int i = 0; i < headers.Length; i++)
            {
                var c = ws.Cells[4, i + 1];
                c.Value = headers[i];
                c.Style.Font.Bold = true;
                c.Style.Font.Color.SetColor(DColor.White);
                c.Style.Fill.PatternType = ExcelFillStyle.Solid;
                c.Style.Fill.BackgroundColor.SetColor(DColor.FromArgb(0, 58, 112));
                c.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            }

            int row = 5;
            foreach (var r in reservas)
            {
                ws.Cells[row, 1].Value = r.Apto;
                ws.Cells[row, 2].Value = r.Torre;
                ws.Cells[row, 3].Value = r.Etapa;
                ws.Cells[row, 4].Value = r.Piso;
                ws.Cells[row, 5].Value = r.Tipo;
                ws.Cells[row, 6].Value = r.Metros;
                ws.Cells[row, 7].Value = r.Precio;
                ws.Cells[row, 7].Style.Numberformat.Format = "$#,##0";
                // Sin asesor se resalta: es una reserva que nadie tiene asignada y que hay
                // que repartir antes de que se cierre.
                if (string.IsNullOrWhiteSpace(r.Asesor))
                {
                    ws.Cells[row, 8].Value = "SIN ASIGNAR";
                    ws.Cells[row, 8].Style.Font.Color.SetColor(DColor.FromArgb(204, 119, 0));
                    ws.Cells[row, 8].Style.Font.Bold = true;
                }
                else ws.Cells[row, 8].Value = r.Asesor;
                ws.Cells[row, 9].Value = r.Obs;
                ws.Cells[row, 9].Style.WrapText = true;
                ws.Cells[row, 10].Value = r.Fecha;
                row++;
            }

            if (reservas.Count > 0)
            {
                ws.Cells[row, 1].Value = "Total bloqueado";
                ws.Cells[row, 1].Style.Font.Bold = true;
                ws.Cells[row, 7].Formula = $"SUM(G5:G{row - 1})";
                ws.Cells[row, 7].Style.Font.Bold = true;
                ws.Cells[row, 7].Style.Numberformat.Format = "$#,##0";
                ws.Cells[row, 1, row, 10].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[row, 1, row, 10].Style.Fill.BackgroundColor.SetColor(DColor.FromArgb(235, 241, 248));
            }
            else
            {
                ws.Cells[5, 1].Value = "No hay reservas activas en este proyecto.";
                ws.Cells[5, 1, 5, 10].Merge = true;
                ws.Cells[5, 1].Style.Font.Color.SetColor(DColor.Gray);
            }

            ws.Cells[4, 1, Math.Max(row, 5), 10].AutoFitColumns();
            // La observación se deja ancha y con ajuste de texto: es lo que se lee.
            ws.Column(9).Width = 45;

            var bytes = package.GetAsByteArray();
            var nombre = $"Reservas_{proyNombre.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombre);
        }

        /// <summary>
        /// Edits the note attached to a reservation. The adviser writes it in a hurry while
        /// the client is in front of them, so the administrator has to be able to complete
        /// it afterwards without having to release the reservation and make it again.
        /// </summary>
        /// <param name="idInmueble">Reserved property.</param>
        /// <param name="observacion">New note; empty clears it.</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarObservacionReserva(int idInmueble, string observacion)
        {
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            var cmdCol = new SqlCommand("SELECT COL_LENGTH('Inmuebles','ObservacionReserva')", con);
            if ((await cmdCol.ExecuteScalarAsync()) is null or DBNull)
            {
                TempData["Error"] = "La base todavía no tiene la columna de observaciones. " +
                                    "Ejecuta la sección 6 de Scripts/PanelAdmin.sql.";
                return RedirectToAction("Reservas");
            }

            // Solo sobre reservas activas: si ya se liberó o se vendió, la observación de
            // la reserva dejó de existir y escribirla sería dejar un dato huérfano.
            var cmd = new SqlCommand(@"
                UPDATE Inmuebles SET ObservacionReserva=@obs
                WHERE IdInmuebles=@id AND IdProyecto=@proy AND Estado='RESERVADO'", con);
            cmd.Parameters.AddWithValue("@obs", (observacion ?? "").Trim());
            cmd.Parameters.AddWithValue("@id", idInmueble);
            cmd.Parameters.AddWithValue("@proy", idProy);

            if (await cmd.ExecuteNonQueryAsync() == 0)
                TempData["Error"] = "Esa reserva ya no está activa.";
            else
                TempData["Exito"] = "Observación actualizada.";

            return RedirectToAction("Reservas");
        }

        /// <summary>
        /// Admin-side: releases a reservation unconditionally and returns the property to DISPONIBLE.
        /// Performs an UPDATE query on Inmuebles.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LiberarReserva(int idInmueble, string volverA = "")
        {
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;
            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            var cmdApto = new SqlCommand("SELECT Apto FROM Inmuebles WHERE IdInmuebles=@id", con);
            cmdApto.Parameters.AddWithValue("@id", idInmueble);
            var apto = (await cmdApto.ExecuteScalarAsync())?.ToString() ?? "";

            var cmd = new SqlCommand(@"UPDATE Inmuebles
                SET Estado='DISPONIBLE', IdVendedorReserva=NULL,
                    PrecioReserva=NULL, FechaReserva=NULL, ObservacionReserva=NULL
                WHERE IdInmuebles=@id", con);
            cmd.Parameters.AddWithValue("@id", idInmueble);
            await cmd.ExecuteNonQueryAsync();
            await _hub.Clients.All.InmuebleActualizado(idProy, idInmueble, "DISPONIBLE", "");
            await _audit.RegistrarAsync(Services.AccionAudit.ReservaLiberada, "Inmueble", idInmueble, idProy,
                $"Apto {apto} · reserva liberada por el administrador");
            var uLib = await Proyecto.UnidadAsync(HttpContext, con, idProy);
            TempData["Exito"] = $"{uLib.Titulo} {apto} liberad{uLib.Fin} correctamente. Vuelve a estar disponible.";
            return volverA == "dashboard" ? RedirectToAction("Index", "Dashboard") : RedirectToAction("Index");
        }

        /// <summary>
        /// Atomically transitions a property from DISPONIBLE to EN PROCESO for the admin.
        /// Uses a single UPDATE…WHERE Estado='DISPONIBLE' to eliminate the race condition
        /// that would occur with a separate SELECT then UPDATE. If rows affected == 0
        /// the property was already taken by another concurrent request.
        /// </summary>
        /// <param name="idInmueble">Property to claim for sale processing.</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TomarInmueble(int idInmueble)
        {
            int idUsuario = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;
            using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            var cmd = new SqlCommand(@"UPDATE Inmuebles
                SET Estado='EN PROCESO', IdVendedorEnProceso=@uid, FechaEnProceso=GETDATE()
                WHERE IdInmuebles=@id AND Estado='DISPONIBLE'", con);
            cmd.Parameters.AddWithValue("@uid", idUsuario);
            cmd.Parameters.AddWithValue("@id", idInmueble);
            var affected = await cmd.ExecuteNonQueryAsync();
            if (affected == 0)
            {
                TempData["Error"] = "Este inmueble ya no está disponible.";
                return RedirectToAction("Index");
            }
            await _hub.Clients.All.InmuebleActualizado(idProy, idInmueble, "EN PROCESO", QuienSoy());
            return RedirectToAction("RegistrarVenta", new { idInmueble });
        }

        /// <summary>
        /// Admin-side: cancels a property's EN PROCESO state and returns it to DISPONIBLE.
        /// Performs an UPDATE query on Inmuebles.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelarProceso(int idInmueble, string volverA = "")
        {
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;
            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            // Se toma el apto antes de limpiar, para dejarlo en la auditoría.
            var cmdApto = new SqlCommand("SELECT Apto FROM Inmuebles WHERE IdInmuebles=@id", con);
            cmdApto.Parameters.AddWithValue("@id", idInmueble);
            var apto = (await cmdApto.ExecuteScalarAsync())?.ToString() ?? "";

            var cmd = new SqlCommand(@"UPDATE Inmuebles
                SET Estado='DISPONIBLE', IdVendedorEnProceso=NULL, FechaEnProceso=NULL
                WHERE IdInmuebles=@id", con);
            cmd.Parameters.AddWithValue("@id", idInmueble);
            await cmd.ExecuteNonQueryAsync();
            await _hub.Clients.All.InmuebleActualizado(idProy, idInmueble, "DISPONIBLE", "");
            await _audit.RegistrarAsync(Services.AccionAudit.ProcesoCancelado, "Inmueble", idInmueble, idProy,
                $"Apto {apto} · proceso cancelado por el administrador");
            var uCan = await Proyecto.UnidadAsync(HttpContext, con, idProy);
            TempData["Exito"] = $"Proceso de {uCan.Articulo} {uCan.Singular} {apto} cancelado. Vuelve a estar disponible.";
            return volverA == "dashboard" ? RedirectToAction("Index", "Dashboard") : RedirectToAction("Index");
        }

        /// <summary>
        /// Displays the sale registration form for a property currently EN PROCESO.
        /// Verifies the requesting user is the one who claimed the property.
        /// Performs SELECT queries for property data, project config, and client list.
        /// </summary>
        /// <param name="idInmueble">Property in EN PROCESO state.</param>
        public async Task<IActionResult> RegistrarVenta(int idInmueble)
        {
            ViewBag.Nombre = HttpContext.Session.GetString("Nombre");
            ViewBag.Apellido = HttpContext.Session.GetString("Apellido");
            int idUsuario = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;
            ViewBag.ProyectoActivo = HttpContext.Session.GetString("ProyectoNombre") ?? "Sin proyecto";

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            ViewBag.Medios = await MediosRepo.ListarAsync(con);

            var proyectos = new List<(int Id, string Nombre)>();
            var cmdList = new SqlCommand(
                "SELECT IdProyectos, Nombre FROM Proyectos WHERE Activo=1 ORDER BY FechaCarga DESC", con);
            using (var r = (SqlDataReader)await cmdList.ExecuteReaderAsync())
                while (await r.ReadAsync())
                    proyectos.Add(((int)r["IdProyectos"], r["Nombre"]?.ToString() ?? ""));
            ViewBag.Proyectos = proyectos;

            var cmdInm = new SqlCommand(@"SELECT IdInmuebles,Apto,Tipo,Piso,Metros,
                Lista1,Lista2,Lista3,Lista4,Lista5,Torre,Estado,IdVendedorEnProceso
                FROM Inmuebles WHERE IdInmuebles=@id", con);
            cmdInm.Parameters.AddWithValue("@id", idInmueble);
            using var r2 = (SqlDataReader)await cmdInm.ExecuteReaderAsync();
            if (!await r2.ReadAsync() || r2["Estado"]?.ToString() != "EN PROCESO" || (int)r2["IdVendedorEnProceso"] != idUsuario)
            {
                r2.Close();
                TempData["Error"] = "No tienes acceso a este inmueble.";
                return RedirectToAction("Index");
            }
            ViewBag.Inmueble = new
            {
                Id = (int)r2["IdInmuebles"],
                Apto = r2["Apto"]?.ToString() ?? "",
                Tipo = r2["Tipo"]?.ToString() ?? "",
                Piso = r2["Piso"]?.ToString() ?? "",
                Metros = r2["Metros"]?.ToString() ?? "",
                Lista1 = r2["Lista1"]?.ToString() ?? "",
                Lista2 = r2["Lista2"]?.ToString() ?? "",
                Lista3 = r2["Lista3"]?.ToString() ?? "",
                Lista4 = r2["Lista4"]?.ToString() ?? "",
                Lista5 = r2["Lista5"]?.ToString() ?? "",
                Torre = r2["Torre"]?.ToString() ?? "",
            };
            string metros = r2["Metros"]?.ToString() ?? "";
            r2.Close();

            // La lista activa es la del ÁREA del inmueble (ProyectoAreaListas), con
            // respaldo a la lista global del proyecto. Debe coincidir con la grilla
            // de inmuebles y con la reserva, para no aplicar una lista distinta al vender.
            var cmdProy = new SqlCommand(@"
                SELECT ISNULL(pal.ListaActual, p.ListaActual) AS ListaActual,
                       p.ApartamentosPorLista
                FROM Proyectos p
                LEFT JOIN ProyectoAreaListas pal
                    ON pal.IdProyecto = p.IdProyectos AND pal.Metros = @metros
                WHERE p.IdProyectos = @id", con);
            cmdProy.Parameters.AddWithValue("@metros", metros);
            cmdProy.Parameters.AddWithValue("@id", idProy);
            using var rP = (SqlDataReader)await cmdProy.ExecuteReaderAsync();
            int listaActual = 1, aptsPorLista = 0;
            if (await rP.ReadAsync())
            {
                listaActual = rP["ListaActual"] == DBNull.Value ? 1 : (int)rP["ListaActual"];
                aptsPorLista = rP["ApartamentosPorLista"] == DBNull.Value ? 0 : (int)rP["ApartamentosPorLista"];
            }
            rP.Close();
            ViewBag.ListaActual = listaActual;
            ViewBag.AptsPorLista = aptsPorLista;

            var clientes = new List<dynamic>();
            var cmdCli = new SqlCommand(
                "SELECT IdCliente, Nombre+' '+Apellido AS NombreCompleto, Documento FROM Clientes ORDER BY Nombre", con);
            using var rc = (SqlDataReader)await cmdCli.ExecuteReaderAsync();
            while (await rc.ReadAsync())
                clientes.Add(new
                {
                    Id = (int)rc["IdCliente"],
                    Nombre = rc["NombreCompleto"]?.ToString() ?? "",
                    Documento = rc["Documento"]?.ToString() ?? "",
                });
            ViewBag.Clientes = clientes;
            return View();
        }

        /// <summary>
        /// Updates the active list level for a specific area size within the project.
        /// Uses MERGE to upsert the ProyectoAreaListas record, then broadcasts the
        /// change via SignalR so all connected clients update in real time.
        /// </summary>
        /// <param name="metros">Area size label (e.g. "60").</param>
        /// <param name="listaActual">New list number (1–5) to activate for this area.</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarListaArea(string metros, int listaActual)
        {
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;
            using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            // Estado vigente antes del cambio: la lista para el historial y el umbral
            // para conservarlo. Cambiar de lista y activar el escalamiento automático
            // son dos decisiones independientes; antes esta acción apagaba el
            // automático (AptsPorLista = 0) y se perdía la configuración del área.
            var cmdPrev = new SqlCommand(
                "SELECT ISNULL(ListaActual,1) AS L, ISNULL(AptsPorLista,0) AS A FROM ProyectoAreaListas WHERE IdProyecto=@p AND Metros=@m", con);
            cmdPrev.Parameters.AddWithValue("@p", idProy);
            cmdPrev.Parameters.AddWithValue("@m", metros ?? "");
            int listaAnterior = 1, aptsPrevios = 0;
            using (var rp = (SqlDataReader)await cmdPrev.ExecuteReaderAsync())
                if (await rp.ReadAsync())
                {
                    listaAnterior = Convert.ToInt32(rp["L"]);
                    aptsPrevios = Convert.ToInt32(rp["A"]);
                }

            var cmd = new SqlCommand(@"
                MERGE ProyectoAreaListas AS target
                USING (SELECT @proy AS IdProyecto, @metros AS Metros) AS source
                ON target.IdProyecto = source.IdProyecto AND target.Metros = source.Metros
                WHEN MATCHED THEN UPDATE SET ListaActual = @lista
                WHEN NOT MATCHED THEN INSERT (IdProyecto, Metros, ListaActual, AptsPorLista)
                    VALUES (@proy, @metros, @lista, 0);", con);
            cmd.Parameters.AddWithValue("@proy", idProy);
            cmd.Parameters.AddWithValue("@metros", metros ?? "");
            cmd.Parameters.AddWithValue("@lista", listaActual);
            await cmd.ExecuteNonQueryAsync();
            int idUsuarioCambio = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uidc) ? uidc : 0;
            await HistorialListas.RegistrarAsync(con, null, idProy, metros ?? "",
                listaAnterior, listaActual, HistorialListas.Manual, idUsuarioCambio, QuienSoy());
            await _audit.RegistrarAsync(Services.AccionAudit.ListaCambiada, "Area", null, idProy,
                $"Área {metros} m²: Lista {listaAnterior} → Lista {listaActual} (manual)");
            await _hub.Clients.All.ListaAreaActualizada(idProy, metros ?? "", listaActual);
            TempData["Exito"] = aptsPrevios > 0
                ? $"Área {metros} m² movida a Lista {listaActual}. Sigue subiendo sola cada {aptsPrevios} ventas."
                : $"Área {metros} m² fijada en Lista {listaActual}.";
            return RedirectToAction("Index");
        }

        /// <summary>
        /// Bulk-updates the price for a given list number across all properties of a specific area.
        /// Performs an UPDATE query on Inmuebles filtered by IdProyecto and Metros.
        /// </summary>
        /// <param name="metros">Area size label to update prices for.</param>
        /// <param name="numLista">List number (1–5) whose price column to update.</param>
        /// <param name="nuevoPrecio">New price value to apply.</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarPrecioArea(string metros, int numLista, long nuevoPrecio)
        {
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;
            metros ??= "";
            using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            var col = Listas.ColumnaLista(numLista);

            // Antes de escribir se guarda el precio que tenía cada inmueble, que es lo
            // único que permite devolver el cambio. Un precio tecleado con un cero de más
            // no tenía vuelta atrás: había que acordarse del valor anterior.
            var cmdTabla = new SqlCommand("SELECT OBJECT_ID('AjustesPrecio','U')", con);
            bool hayHistorial = (await cmdTabla.ExecuteScalarAsync()) is not (null or DBNull);

            var anteriores = new List<(int Id, long Precio)>();
            if (hayHistorial)
            {
                var cmdPrev = new SqlCommand(
                    $"SELECT IdInmuebles, {col} AS P FROM Inmuebles WHERE IdProyecto=@proy AND Metros=@metros", con);
                cmdPrev.Parameters.AddWithValue("@proy", idProy);
                cmdPrev.Parameters.AddWithValue("@metros", metros);
                using (var rp = (SqlDataReader)await cmdPrev.ExecuteReaderAsync())
                    while (await rp.ReadAsync())
                    {
                        long previo = Texto.ParsearPrecio(rp["P"]?.ToString());
                        // Solo se registran los que de verdad cambian: devolver un precio
                        // que no se tocó no tendría sentido.
                        if (previo != nuevoPrecio) anteriores.Add(((int)rp["IdInmuebles"], previo));
                    }
            }

            using var tx = (SqlTransaction)await con.BeginTransactionAsync();
            try
            {
                var cmd = new SqlCommand(
                    $"UPDATE Inmuebles SET {col}=@precio WHERE IdProyecto=@proy AND Metros=@metros", con, tx);
                cmd.Parameters.AddWithValue("@precio", nuevoPrecio);
                cmd.Parameters.AddWithValue("@proy", idProy);
                cmd.Parameters.AddWithValue("@metros", metros);
                await cmd.ExecuteNonQueryAsync();

                if (anteriores.Count > 0)
                {
                    int idUsuario = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;
                    var usuario = ((HttpContext.Session.GetString("Nombre") ?? "") + " " +
                                   (HttpContext.Session.GetString("Apellido") ?? "")).Trim();

                    // Se guarda en las mismas tablas del ajuste masivo, con Tipo MANUAL:
                    // así un solo historial muestra todo lo que le pasó a los precios.
                    var cmdAj = new SqlCommand(@"INSERT INTO AjustesPrecio
                        (IdProyecto,Torre,Metros,Listas,Tipo,Valor,Unidades,IdUsuario,Usuario)
                        OUTPUT INSERTED.IdAjuste
                        VALUES (@p,'',@m,@l,'MANUAL',@v,@u,@iu,@us)", con, tx);
                    cmdAj.Parameters.AddWithValue("@p", idProy);
                    cmdAj.Parameters.AddWithValue("@m", metros);
                    cmdAj.Parameters.AddWithValue("@l", numLista.ToString());
                    cmdAj.Parameters.AddWithValue("@v", (decimal)nuevoPrecio);
                    cmdAj.Parameters.AddWithValue("@u", anteriores.Count);
                    cmdAj.Parameters.AddWithValue("@iu", idUsuario > 0 ? (object)idUsuario : DBNull.Value);
                    cmdAj.Parameters.AddWithValue("@us", usuario);
                    long idAjuste = Convert.ToInt64((await cmdAj.ExecuteScalarAsync())!);

                    foreach (var a in anteriores)
                    {
                        var cmdDet = new SqlCommand(@"INSERT INTO AjustesPrecioDetalle
                            (IdAjuste,IdInmueble,NumLista,PrecioAnterior,PrecioNuevo)
                            VALUES (@a,@i,@n,@ant,@nue)", con, tx);
                        cmdDet.Parameters.AddWithValue("@a", idAjuste);
                        cmdDet.Parameters.AddWithValue("@i", a.Id);
                        cmdDet.Parameters.AddWithValue("@n", numLista);
                        cmdDet.Parameters.AddWithValue("@ant", a.Precio);
                        cmdDet.Parameters.AddWithValue("@nue", nuevoPrecio);
                        await cmdDet.ExecuteNonQueryAsync();
                    }
                }

                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }

            await _hub.Clients.All.PrecioAreaActualizado(idProy, metros, numLista, nuevoPrecio);
            TempData["Exito"] = anteriores.Count > 0
                ? $"Precios de Lista {numLista} para {metros} m² actualizados. Puedes devolverlos con el botón Deshacer."
                : $"Precios de Lista {numLista} para {metros} m² actualizados.";
            return RedirectToAction("Index");
        }

        /// <summary>
        /// Configures the automatic list-escalation threshold for a specific area.
        /// Uses MERGE to upsert the ProyectoAreaListas record.
        /// </summary>
        /// <param name="metros">Area size label to configure.</param>
        /// <param name="aptsPorLista">Number of sales in this area required to advance the list. Set to 0 to disable.</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfigurarAutoArea(string metros, int aptsPorLista)
        {
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;
            using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            var cmd = new SqlCommand(@"
                MERGE ProyectoAreaListas AS target
                USING (SELECT @proy AS IdProyecto, @metros AS Metros) AS source
                ON target.IdProyecto = source.IdProyecto AND target.Metros = source.Metros
                WHEN MATCHED THEN UPDATE SET AptsPorLista = @apts
                WHEN NOT MATCHED THEN INSERT (IdProyecto, Metros, ListaActual, AptsPorLista)
                    VALUES (@proy, @metros, 1, @apts);", con);
            cmd.Parameters.AddWithValue("@proy", idProy);
            cmd.Parameters.AddWithValue("@metros", metros ?? "");
            cmd.Parameters.AddWithValue("@apts", aptsPorLista);
            await cmd.ExecuteNonQueryAsync();
            TempData["Exito"] = aptsPorLista > 0
                ? $"Escalamiento activado: {metros} m² sube de lista cada {aptsPorLista} vendidos."
                : $"Escalamiento desactivado para {metros} m².";
            return RedirectToAction("Index");
        }

        /// <summary>
        /// Confirms and records a sale for a property in EN PROCESO state (admin flow).
        /// Marks the property as VENDIDO, checks global and per-area list escalation,
        /// broadcasts all changes via SignalR.
        /// Performs INSERT (Ventas, Clientes), UPDATE (Inmuebles, Proyectos, ProyectoAreaListas) queries.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmarVenta(int idInmueble, string accion,
            int? idClienteExistente, string tipoCliente, string destino, bool sagrilaftConsultado,
            string observaciones, string clienteMedio,
            string clienteNombre, string clienteApellido, string clienteDocumento,
            string clienteCelular, string clienteCorreo, string clienteDireccion)
        {
            int idUsuario = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;

            // Cumplimiento SAGRILAFT: no se puede registrar la venta sin confirmar la consulta previa.
            if (!sagrilaftConsultado)
            {
                TempData["Error"] = "Por favor, consulte el cliente y recargue la página para realizar el nuevo registro.";
                return RedirectToAction("RegistrarVenta", new { idInmueble });
            }

            if (tipoCliente == "existente" && (!idClienteExistente.HasValue || idClienteExistente.Value <= 0))
            {
                TempData["Error"] = "Por favor ingrese los datos del cliente para continuar con la venta.";
                return RedirectToAction("RegistrarVenta", new { idInmueble });
            }
            if (tipoCliente != "existente" && (string.IsNullOrWhiteSpace(clienteNombre) || string.IsNullOrWhiteSpace(clienteDocumento)))
            {
                TempData["Error"] = "Por favor ingrese los datos del cliente para continuar con la venta.";
                return RedirectToAction("RegistrarVenta", new { idInmueble });
            }

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            // Transacción: cambio de estado + registro de venta atómicos.
            using var tx = (SqlTransaction)await con.BeginTransactionAsync();

            // 1. Verificar estado + propietario (admin que tomó el inmueble) dentro del proyecto.
            string metros = ""; var listasRaw = new string[5];
            var cmdSel = new SqlCommand(@"SELECT Metros, Lista1,Lista2,Lista3,Lista4,Lista5,
                Estado, IdVendedorEnProceso FROM Inmuebles
                WHERE IdInmuebles=@id AND IdProyecto=@proy", con, tx);
            cmdSel.Parameters.AddWithValue("@id", idInmueble);
            cmdSel.Parameters.AddWithValue("@proy", idProy);
            using (var rs = (SqlDataReader)await cmdSel.ExecuteReaderAsync())
            {
                if (!await rs.ReadAsync() || rs["Estado"]?.ToString() != "EN PROCESO"
                    || rs["IdVendedorEnProceso"] == DBNull.Value
                    || (int)rs["IdVendedorEnProceso"] != idUsuario)
                {
                    TempData["Error"] = "Este inmueble ya no está disponible para venta.";
                    return RedirectToAction("Index");
                }
                metros = rs["Metros"]?.ToString() ?? "";
                for (int i = 0; i < 5; i++) listasRaw[i] = rs[$"Lista{i + 1}"]?.ToString() ?? "";
            }

            // 2. Lista activa del ÁREA y precio derivados en el servidor.
            var cmdLa = new SqlCommand(@"
                SELECT ISNULL(pal.ListaActual, p.ListaActual)
                FROM Proyectos p
                LEFT JOIN ProyectoAreaListas pal
                    ON pal.IdProyecto = p.IdProyectos AND pal.Metros = @metros
                WHERE p.IdProyectos = @proy", con, tx);
            cmdLa.Parameters.AddWithValue("@metros", metros);
            cmdLa.Parameters.AddWithValue("@proy", idProy);
            int listaAplicada = Convert.ToInt32((await cmdLa.ExecuteScalarAsync()) ?? 1);
            if (listaAplicada < 1 || listaAplicada > 5) listaAplicada = 1;
            long precioVenta = Texto.ParsearPrecio(listasRaw[listaAplicada - 1]);
            if (precioVenta <= 0)
            {
                TempData["Error"] = "El inmueble no tiene un precio válido en la lista activa.";
                return RedirectToAction("RegistrarVenta", new { idInmueble });
            }

            int idCliente;
            bool clienteReutilizado = false;
            if (tipoCliente == "existente" && idClienteExistente.HasValue && idClienteExistente.Value > 0)
                idCliente = idClienteExistente.Value;
            else
            {
                // Reutiliza el cliente si ya existe uno con ese documento, en vez de duplicarlo.
                var altaCliente = await ClienteRepo.ObtenerOCrearAsync(con, tx,
                    clienteNombre, clienteApellido, clienteDocumento,
                    clienteCelular, clienteCorreo, clienteDireccion, clienteMedio);
                idCliente = altaCliente.IdCliente;
                clienteReutilizado = altaCliente.Reutilizado;
            }

            // 3. Marcar VENDIDO de forma ATÓMICA con verificación de estado y propietario.
            var cmdInm2 = new SqlCommand(@"UPDATE Inmuebles
                SET Estado='VENDIDO', IdVendedorEnProceso=NULL, FechaEnProceso=NULL
                WHERE IdInmuebles=@id AND Estado='EN PROCESO' AND IdVendedorEnProceso=@uid", con, tx);
            cmdInm2.Parameters.AddWithValue("@id", idInmueble);
            cmdInm2.Parameters.AddWithValue("@uid", idUsuario);
            if (await cmdInm2.ExecuteNonQueryAsync() == 0)
            {
                TempData["Error"] = "Este inmueble ya no está disponible para venta.";
                return RedirectToAction("Index");
            }

            // 4. Registrar la venta con precio/lista del servidor y destino validado.
            var cmdVenta = new SqlCommand(@"INSERT INTO Ventas
                (IdInmueble,IdCliente,IdUsuario,IdProyecto,ListaAplicada,PrecioVenta,Destino,Estado,Observaciones)
                VALUES (@inm,@cli,@usr,@proy,@lista,@precio,@destino,'ACTIVA',@obsVenta)", con, tx);
            cmdVenta.Parameters.AddWithValue("@inm", idInmueble);
            cmdVenta.Parameters.AddWithValue("@cli", idCliente);
            cmdVenta.Parameters.AddWithValue("@usr", idUsuario);
            cmdVenta.Parameters.AddWithValue("@proy", idProy);
            cmdVenta.Parameters.AddWithValue("@lista", listaAplicada);
            cmdVenta.Parameters.AddWithValue("@precio", precioVenta);
            cmdVenta.Parameters.AddWithValue("@destino", Texto.DestinoVenta(destino));
            cmdVenta.Parameters.AddWithValue("@obsVenta", (observaciones ?? "").Trim());
            await cmdVenta.ExecuteNonQueryAsync();

            await tx.CommitAsync();

            await _hub.Clients.All.InmuebleActualizado(idProy, idInmueble, "VENDIDO", QuienSoy());

            // Verifica que una lista tenga al menos un precio > 0 antes de escalar hacia ella.
            // Evita que el auto-escalamiento mueva a una lista sin precios cargados.
            async Task<bool> ListaConPrecios(int numLista, string metrosArea)
            {
                var col = Listas.ColumnaLista(numLista);
                var cmdP = new SqlCommand(
                    $"SELECT {col} FROM Inmuebles WHERE IdProyecto=@proy AND Metros=@metros", con);
                cmdP.Parameters.AddWithValue("@proy", idProy);
                cmdP.Parameters.AddWithValue("@metros", metrosArea);
                using (var rP = (SqlDataReader)await cmdP.ExecuteReaderAsync())
                    while (await rP.ReadAsync())
                    {
                        var limpio = (rP[0]?.ToString() ?? "0").Replace("$", "").Replace(".", "").Replace(",", "").Replace(" ", "").Trim();
                        if (long.TryParse(limpio, out long v) && v > 0) return true;
                    }
                return false;
            }

            // Escalamiento por área
            var cmdMetrosEsc = new SqlCommand(
                "SELECT Metros FROM Inmuebles WHERE IdInmuebles=@id", con);
            cmdMetrosEsc.Parameters.AddWithValue("@id", idInmueble);
            var metrosArea = (await cmdMetrosEsc.ExecuteScalarAsync())?.ToString() ?? "";
            if (!string.IsNullOrEmpty(metrosArea))
            {
                var cmdPALEsc = new SqlCommand(@"SELECT ListaActual, AptsPorLista FROM ProyectoAreaListas
                    WHERE IdProyecto=@proy AND Metros=@metros", con);
                cmdPALEsc.Parameters.AddWithValue("@proy", idProy);
                cmdPALEsc.Parameters.AddWithValue("@metros", metrosArea);
                using var rPAL = (SqlDataReader)await cmdPALEsc.ExecuteReaderAsync();
                if (await rPAL.ReadAsync())
                {
                    int laArea = rPAL["ListaActual"] == DBNull.Value ? 1 : (int)rPAL["ListaActual"];
                    int aptsArea = rPAL["AptsPorLista"] == DBNull.Value ? 0 : (int)rPAL["AptsPorLista"];
                    rPAL.Close();
                    if (aptsArea > 0)
                    {
                        var cmdVArea = new SqlCommand(@"SELECT COUNT(*) FROM Ventas v
                            INNER JOIN Inmuebles i ON v.IdInmueble = i.IdInmuebles
                            WHERE v.IdProyecto=@proy AND i.Metros=@metros AND v.Estado='ACTIVA'", con);
                        cmdVArea.Parameters.AddWithValue("@proy", idProy);
                        cmdVArea.Parameters.AddWithValue("@metros", metrosArea);
                        int vendidosArea = (int)(await cmdVArea.ExecuteScalarAsync())!;
                        int nuevaListaArea = (vendidosArea / aptsArea) + 1;
                        if (nuevaListaArea > laArea && nuevaListaArea <= 5 && await ListaConPrecios(nuevaListaArea, metrosArea))
                        {
                            var cmdUpArea = new SqlCommand(@"UPDATE ProyectoAreaListas
                                SET ListaActual=@lista WHERE IdProyecto=@proy AND Metros=@metros", con);
                            cmdUpArea.Parameters.AddWithValue("@lista", nuevaListaArea);
                            cmdUpArea.Parameters.AddWithValue("@proy", idProy);
                            cmdUpArea.Parameters.AddWithValue("@metros", metrosArea);
                            await cmdUpArea.ExecuteNonQueryAsync();
                            await HistorialListas.RegistrarAsync(con, null, idProy, metrosArea,
                                laArea, nuevaListaArea, HistorialListas.Automatico, idUsuario, QuienSoy());
                            await _hub.Clients.All.ListaAreaActualizada(idProy, metrosArea, nuevaListaArea);
                            TempData["Exito"] = $"¡Venta registrada! ⚡ El área {metrosArea} m² subió a Lista {nuevaListaArea}.";
                            return RedirectToAction("Index");
                        }
                    }
                }
            }

            if (clienteReutilizado)
                TempData["Aviso"] = "Ya existía un cliente con ese documento: se usó su ficha en vez de crear una nueva.";
            TempData["Exito"] = "¡Venta registrada exitosamente!";
            return RedirectToAction("Index");
        }
    }
}
