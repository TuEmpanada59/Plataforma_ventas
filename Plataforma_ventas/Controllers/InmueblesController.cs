using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using Plataforma_ventas.Filters;
using Plataforma_ventas.Hubs;

namespace Plataforma_ventas.Controllers
{
    /// <summary>
    /// Administrator controller for property and project management:
    /// viewing/editing properties, reservations, sales confirmation, and list escalation.
    /// </summary>
    [RolAutorizado("Administrador")]
    public class InmueblesController : Controller
    {
        private readonly string _conn;
        private readonly IHubContext<VentasHub, IVentasClient> _hub;

        /// <summary>Initializes the controller with DB connection and strongly-typed SignalR hub.</summary>
        public InmueblesController(IConfiguration config, IHubContext<VentasHub, IVentasClient> hub)
        {
            _conn = config.GetConnectionString("DefaultConnection")!;
            _hub = hub;
        }

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
        public async Task<IActionResult> Index([FromQuery] string torre = "", [FromQuery] string area = "")
        {
            ViewBag.Nombre = HttpContext.Session.GetString("Nombre");
            ViewBag.Apellido = HttpContext.Session.GetString("Apellido");
            var proyNombre = HttpContext.Session.GetString("ProyectoNombre") ?? "Sin proyecto";
            var proyIdStr = HttpContext.Session.GetString("ProyectoId") ?? "0";
            ViewBag.ProyectoActivo = proyNombre;
            int idProy = int.TryParse(proyIdStr, out int pid) ? pid : 0;
            if (idProy == 0) return RedirectToAction("Proyectos");

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            // Todos los proyectos activos (sin filtro de admin)
            var proyectos = new List<(int Id, string Nombre)>();
            var cmdList = new SqlCommand(
                "SELECT IdProyectos, Nombre FROM Proyectos WHERE Activo=1 ORDER BY FechaCarga DESC", con);
            using (var r = (SqlDataReader)await cmdList.ExecuteReaderAsync())
                while (await r.ReadAsync())
                    proyectos.Add(((int)r["IdProyectos"], r["Nombre"]?.ToString() ?? ""));
            ViewBag.Proyectos = proyectos;

            // Proyectos hermanos
            string nombreBase = proyNombre.Trim().Split(' ')[0].ToUpper();
            var proyectosHermanos = proyectos
                .Where(p => p.Nombre.Trim().Split(' ')[0].ToUpper() == nombreBase)
                .OrderBy(p => p.Nombre)
                .ToList();
            ViewBag.ProyectosHermanos = proyectosHermanos;

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

            // Inmuebles
            var lista = new List<dynamic>();
            var cmd = new SqlCommand(@"
                SELECT IdInmuebles,Apto,Tipo,Piso,Metros,
                       Lista1,Lista2,Lista3,Lista4,Lista5,
                       Estado,Torre,IdVendedorEnProceso,IdVendedorReserva
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
                        IdVendedorEnProceso = reader["IdVendedorEnProceso"] == DBNull.Value ? 0 : (int)reader["IdVendedorEnProceso"],
                        IdVendedorReserva = reader["IdVendedorReserva"] == DBNull.Value ? 0 : (int)reader["IdVendedorReserva"],
                    });

            // Diccionario vendedores
            var vendedores = new Dictionary<int, string>();
            var cmdVend = new SqlCommand(
                "SELECT IdUsuario, Nombre+' '+Apellido AS NombreCompleto FROM Usuarios WHERE Rol='Vendedor'", con);
            using (var rv = (SqlDataReader)await cmdVend.ExecuteReaderAsync())
                while (await rv.ReadAsync())
                    vendedores[(int)rv["IdUsuario"]] = rv["NombreCompleto"]?.ToString() ?? "";
            ViewBag.Vendedores = vendedores;

            // Torres
            string torreActual = "";
            if (proyectosHermanos.Count > 1)
            {
                if (!string.IsNullOrEmpty(torre))
                {
                    var proyTorre = proyectosHermanos.FirstOrDefault(p => p.Nombre == torre);
                    if (proyTorre.Id > 0 && proyTorre.Id != idProy)
                    {
                        HttpContext.Session.SetString("ProyectoId", proyTorre.Id.ToString());
                        HttpContext.Session.SetString("ProyectoNombre", proyTorre.Nombre);
                        return RedirectToAction("Index", new { torre = proyTorre.Nombre });
                    }
                    torreActual = string.IsNullOrEmpty(torre) ? proyNombre : torre;
                }
                else
                    torreActual = "";
            }
            else
                torreActual = proyNombre;

            ViewBag.Torres = proyectosHermanos.Select(p => p.Nombre).ToList();
            ViewBag.TorreActual = torreActual;

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

            int vendidosTotal = lista.Count(x => x.Estado == "VENDIDO");
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
        public async Task<IActionResult> ReservarInmueble(int idInmueble)
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

            var colLista = listaActual switch { 1 => "Lista1", 2 => "Lista2", 3 => "Lista3", 4 => "Lista4", _ => "Lista5" };
            var cmdPrecio = new SqlCommand($"SELECT {colLista} FROM Inmuebles WHERE IdInmuebles=@id", con);
            cmdPrecio.Parameters.AddWithValue("@id", idInmueble);
            var rawPrecio = (await cmdPrecio.ExecuteScalarAsync())?.ToString() ?? "0";
            var limpio = rawPrecio.Replace("$", "").Replace(".", "").Replace(",", "").Replace(" ", "").Trim();
            long.TryParse(limpio, out long precioReserva);

            // Atomic reserve: only succeeds if still DISPONIBLE — prevents double-booking
            var cmd = new SqlCommand(@"
                UPDATE Inmuebles
                SET Estado='RESERVADO', IdVendedorReserva=@uid,
                    PrecioReserva=@precio, FechaReserva=GETDATE()
                WHERE IdInmuebles=@id AND Estado='DISPONIBLE'", con);
            cmd.Parameters.AddWithValue("@uid", idUsuario);
            cmd.Parameters.AddWithValue("@precio", precioReserva);
            cmd.Parameters.AddWithValue("@id", idInmueble);
            var affected = await cmd.ExecuteNonQueryAsync();
            if (affected == 0)
            {
                TempData["Error"] = "Este inmueble ya no está disponible.";
                return RedirectToAction("Index");
            }

            await _hub.Clients.All.InmuebleActualizado(idProy, idInmueble, "RESERVADO");
            TempData["Exito"] = $"Inmueble reservado. Precio bloqueado: ${string.Format("{0:N0}", precioReserva)}";
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

            var cmd = new SqlCommand(@"
                SELECT i.IdInmuebles, i.Apto, i.Metros, i.Tipo, i.Torre, i.Piso,
                       i.PrecioReserva, i.FechaReserva,
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
                    NombreVendedor = rr["NombreVendedor"]?.ToString() ?? "Sin asignar",
                });

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
            int? idClienteExistente, string tipoCliente, string destino,
            string clienteNombre, string clienteApellido, string clienteDocumento,
            string clienteCelular, string clienteCorreo, string clienteDireccion)
        {
            int idUsuario = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            var cmdCheck = new SqlCommand(
                "SELECT Metros, PrecioReserva FROM Inmuebles WHERE IdInmuebles=@id AND Estado='RESERVADO'", con);
            cmdCheck.Parameters.AddWithValue("@id", idInmueble);
            using var rCheck = (SqlDataReader)await cmdCheck.ExecuteReaderAsync();
            if (!await rCheck.ReadAsync())
            {
                TempData["Error"] = "Este inmueble ya no está reservado.";
                return RedirectToAction("Reservas");
            }
            var metros = rCheck["Metros"]?.ToString() ?? "";
            long precioFijo = rCheck["PrecioReserva"] == DBNull.Value ? precioVenta : (long)rCheck["PrecioReserva"];
            rCheck.Close();

            var cmdListaApl = new SqlCommand(@"
                SELECT ISNULL(pal.ListaActual, p.ListaActual) AS ListaActual
                FROM Proyectos p
                LEFT JOIN ProyectoAreaListas pal
                    ON pal.IdProyecto = p.IdProyectos AND pal.Metros = @metros
                WHERE p.IdProyectos = @proy", con);
            cmdListaApl.Parameters.AddWithValue("@metros", metros);
            cmdListaApl.Parameters.AddWithValue("@proy", idProy);
            int listaAplicada = (int)((await cmdListaApl.ExecuteScalarAsync()) ?? 1);

            int idCliente;
            if (tipoCliente == "existente" && idClienteExistente.HasValue && idClienteExistente.Value > 0)
                idCliente = idClienteExistente.Value;
            else
            {
                var cmdCli = new SqlCommand(@"INSERT INTO Clientes
                    (Nombre,Apellido,Documento,Celular,Correo,Direccion)
                    OUTPUT INSERTED.IdCliente
                    VALUES (@n,@a,@d,@c,@e,@dir)", con);
                cmdCli.Parameters.AddWithValue("@n", clienteNombre ?? "");
                cmdCli.Parameters.AddWithValue("@a", clienteApellido ?? "");
                cmdCli.Parameters.AddWithValue("@d", clienteDocumento ?? "");
                cmdCli.Parameters.AddWithValue("@c", clienteCelular ?? "");
                cmdCli.Parameters.AddWithValue("@e", clienteCorreo ?? "");
                cmdCli.Parameters.AddWithValue("@dir", clienteDireccion ?? "");
                idCliente = (int)(await cmdCli.ExecuteScalarAsync())!;
            }

            var cmdVenta = new SqlCommand(@"INSERT INTO Ventas
                (IdInmueble,IdCliente,IdUsuario,IdProyecto,ListaAplicada,PrecioVenta,Destino,Estado)
                VALUES (@inm,@cli,@usr,@proy,@lista,@precio,@destino,'ACTIVA')", con);
            cmdVenta.Parameters.AddWithValue("@inm", idInmueble);
            cmdVenta.Parameters.AddWithValue("@cli", idCliente);
            cmdVenta.Parameters.AddWithValue("@usr", idUsuario);
            cmdVenta.Parameters.AddWithValue("@proy", idProy);
            cmdVenta.Parameters.AddWithValue("@lista", listaAplicada);
            cmdVenta.Parameters.AddWithValue("@precio", precioFijo);
            cmdVenta.Parameters.AddWithValue("@destino", destino ?? "Vivienda");
            await cmdVenta.ExecuteNonQueryAsync();

            var cmdInm = new SqlCommand(@"UPDATE Inmuebles
                SET Estado='VENDIDO', IdVendedorReserva=NULL,
                    PrecioReserva=NULL, FechaReserva=NULL
                WHERE IdInmuebles=@id", con);
            cmdInm.Parameters.AddWithValue("@id", idInmueble);
            await cmdInm.ExecuteNonQueryAsync();

            await _hub.Clients.All.InmuebleActualizado(idProy, idInmueble, "VENDIDO");
            TempData["Exito"] = $"¡Venta confirmada! Precio aplicado: ${string.Format("{0:N0}", precioFijo)}";
            return RedirectToAction("Reservas");
        }

        /// <summary>
        /// Admin-side: releases a reservation unconditionally and returns the property to DISPONIBLE.
        /// Performs an UPDATE query on Inmuebles.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LiberarReserva(int idInmueble)
        {
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;
            using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            var cmd = new SqlCommand(@"UPDATE Inmuebles
                SET Estado='DISPONIBLE', IdVendedorReserva=NULL,
                    PrecioReserva=NULL, FechaReserva=NULL
                WHERE IdInmuebles=@id", con);
            cmd.Parameters.AddWithValue("@id", idInmueble);
            await cmd.ExecuteNonQueryAsync();
            await _hub.Clients.All.InmuebleActualizado(idProy, idInmueble, "DISPONIBLE");
            TempData["Exito"] = "Reserva liberada correctamente.";
            return RedirectToAction("Index");
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
            await _hub.Clients.All.InmuebleActualizado(idProy, idInmueble, "EN PROCESO");
            return RedirectToAction("RegistrarVenta", new { idInmueble });
        }

        /// <summary>
        /// Admin-side: cancels a property's EN PROCESO state and returns it to DISPONIBLE.
        /// Performs an UPDATE query on Inmuebles.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelarProceso(int idInmueble)
        {
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;
            using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            var cmd = new SqlCommand(@"UPDATE Inmuebles
                SET Estado='DISPONIBLE', IdVendedorEnProceso=NULL, FechaEnProceso=NULL
                WHERE IdInmuebles=@id", con);
            cmd.Parameters.AddWithValue("@id", idInmueble);
            await cmd.ExecuteNonQueryAsync();
            await _hub.Clients.All.InmuebleActualizado(idProy, idInmueble, "DISPONIBLE");
            return RedirectToAction("Index");
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
            r2.Close();

            var cmdProy = new SqlCommand(
                "SELECT ListaActual, ApartamentosPorLista FROM Proyectos WHERE IdProyectos=@id", con);
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
            var cmd = new SqlCommand(@"
                MERGE ProyectoAreaListas AS target
                USING (SELECT @proy AS IdProyecto, @metros AS Metros) AS source
                ON target.IdProyecto = source.IdProyecto AND target.Metros = source.Metros
                WHEN MATCHED THEN UPDATE SET ListaActual = @lista, AptsPorLista = 0
                WHEN NOT MATCHED THEN INSERT (IdProyecto, Metros, ListaActual, AptsPorLista)
                    VALUES (@proy, @metros, @lista, 0);", con);
            cmd.Parameters.AddWithValue("@proy", idProy);
            cmd.Parameters.AddWithValue("@metros", metros ?? "");
            cmd.Parameters.AddWithValue("@lista", listaActual);
            await cmd.ExecuteNonQueryAsync();
            await _hub.Clients.All.ListaAreaActualizada(idProy, metros ?? "", listaActual);
            TempData["Exito"] = $"Lista del área {metros} m² fijada en Lista {listaActual} (modo manual). El escalamiento automático quedó desactivado para esta área.";
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
            using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            var col = numLista switch { 1 => "Lista1", 2 => "Lista2", 3 => "Lista3", 4 => "Lista4", _ => "Lista5" };
            var cmd = new SqlCommand(
                $"UPDATE Inmuebles SET {col}=@precio WHERE IdProyecto=@proy AND Metros=@metros", con);
            cmd.Parameters.AddWithValue("@precio", nuevoPrecio);
            cmd.Parameters.AddWithValue("@proy", idProy);
            cmd.Parameters.AddWithValue("@metros", metros ?? "");
            await cmd.ExecuteNonQueryAsync();
            TempData["Exito"] = $"Precios de Lista {numLista} para {metros} m² actualizados.";
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
        public async Task<IActionResult> ConfirmarVenta(int idInmueble, int listaAplicada, long precioVenta,
            string accion, int? idClienteExistente, string tipoCliente, string destino,
            string clienteNombre, string clienteApellido, string clienteDocumento,
            string clienteCelular, string clienteCorreo, string clienteDireccion)
        {
            int idUsuario = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            int idCliente;
            if (tipoCliente == "existente" && idClienteExistente.HasValue && idClienteExistente.Value > 0)
                idCliente = idClienteExistente.Value;
            else
            {
                var cmdCli = new SqlCommand(@"INSERT INTO Clientes
                    (Nombre,Apellido,Documento,Celular,Correo,Direccion)
                    OUTPUT INSERTED.IdCliente
                    VALUES (@n,@a,@d,@c,@e,@dir)", con);
                cmdCli.Parameters.AddWithValue("@n", clienteNombre ?? "");
                cmdCli.Parameters.AddWithValue("@a", clienteApellido ?? "");
                cmdCli.Parameters.AddWithValue("@d", clienteDocumento ?? "");
                cmdCli.Parameters.AddWithValue("@c", clienteCelular ?? "");
                cmdCli.Parameters.AddWithValue("@e", clienteCorreo ?? "");
                cmdCli.Parameters.AddWithValue("@dir", clienteDireccion ?? "");
                idCliente = (int)(await cmdCli.ExecuteScalarAsync())!;
            }

            var cmdVenta = new SqlCommand(@"INSERT INTO Ventas
                (IdInmueble,IdCliente,IdUsuario,IdProyecto,ListaAplicada,PrecioVenta,Destino,Estado)
                VALUES (@inm,@cli,@usr,@proy,@lista,@precio,@destino,'ACTIVA')", con);
            cmdVenta.Parameters.AddWithValue("@inm", idInmueble);
            cmdVenta.Parameters.AddWithValue("@cli", idCliente);
            cmdVenta.Parameters.AddWithValue("@usr", idUsuario);
            cmdVenta.Parameters.AddWithValue("@proy", idProy);
            cmdVenta.Parameters.AddWithValue("@lista", listaAplicada);
            cmdVenta.Parameters.AddWithValue("@precio", precioVenta);
            cmdVenta.Parameters.AddWithValue("@destino", destino ?? "Vivienda");
            await cmdVenta.ExecuteNonQueryAsync();

            var cmdInm2 = new SqlCommand(@"UPDATE Inmuebles
                SET Estado='VENDIDO', IdVendedorEnProceso=NULL, FechaEnProceso=NULL
                WHERE IdInmuebles=@id", con);
            cmdInm2.Parameters.AddWithValue("@id", idInmueble);
            await cmdInm2.ExecuteNonQueryAsync();

            await _hub.Clients.All.InmuebleActualizado(idProy, idInmueble, "VENDIDO");

            // Verifica que una lista tenga al menos un precio > 0 antes de escalar hacia ella.
            // Evita que el auto-escalamiento mueva a una lista sin precios cargados.
            async Task<bool> ListaConPrecios(int numLista, string? metrosArea)
            {
                var col = numLista switch { 1 => "Lista1", 2 => "Lista2", 3 => "Lista3", 4 => "Lista4", _ => "Lista5" };
                string sql = metrosArea == null
                    ? $"SELECT {col} FROM Inmuebles WHERE IdProyecto=@proy"
                    : $"SELECT {col} FROM Inmuebles WHERE IdProyecto=@proy AND Metros=@metros";
                var cmdP = new SqlCommand(sql, con);
                cmdP.Parameters.AddWithValue("@proy", idProy);
                if (metrosArea != null) cmdP.Parameters.AddWithValue("@metros", metrosArea);
                using (var rP = (SqlDataReader)await cmdP.ExecuteReaderAsync())
                    while (await rP.ReadAsync())
                    {
                        var limpio = (rP[0]?.ToString() ?? "0").Replace("$", "").Replace(".", "").Replace(",", "").Replace(" ", "").Trim();
                        if (long.TryParse(limpio, out long v) && v > 0) return true;
                    }
                return false;
            }

            // Escalamiento global del proyecto
            var cmdConfig = new SqlCommand(
                "SELECT ListaActual, ApartamentosPorLista FROM Proyectos WHERE IdProyectos=@id", con);
            cmdConfig.Parameters.AddWithValue("@id", idProy);
            using var rC = (SqlDataReader)await cmdConfig.ExecuteReaderAsync();
            if (await rC.ReadAsync())
            {
                int listaActual = rC["ListaActual"] == DBNull.Value ? 1 : (int)rC["ListaActual"];
                int aptsPorLista = rC["ApartamentosPorLista"] == DBNull.Value ? 0 : (int)rC["ApartamentosPorLista"];
                rC.Close();
                if (aptsPorLista > 0)
                {
                    var cmdV = new SqlCommand(
                        "SELECT COUNT(*) FROM Inmuebles WHERE IdProyecto=@id AND Estado='VENDIDO'", con);
                    cmdV.Parameters.AddWithValue("@id", idProy);
                    int totalVendidos = (int)(await cmdV.ExecuteScalarAsync())!;
                    if (totalVendidos > 0 && totalVendidos % aptsPorLista == 0)
                    {
                        int nuevaLista = Math.Min(5, listaActual + 1);
                        if (nuevaLista > listaActual && await ListaConPrecios(nuevaLista, null))
                        {
                            var cmdS = new SqlCommand(
                                "UPDATE Proyectos SET ListaActual=@l WHERE IdProyectos=@id", con);
                            cmdS.Parameters.AddWithValue("@l", nuevaLista);
                            cmdS.Parameters.AddWithValue("@id", idProy);
                            await cmdS.ExecuteNonQueryAsync();
                            await _hub.Clients.All.ListaActualizada(idProy, nuevaLista);
                            TempData["Exito"] = $"¡Venta registrada! ⚡ El proyecto subió a Lista {nuevaLista}.";
                            return RedirectToAction("Index");
                        }
                    }
                }
            }
            else rC.Close();

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
                            await _hub.Clients.All.ListaAreaActualizada(idProy, metrosArea, nuevaListaArea);
                            TempData["Exito"] = $"¡Venta registrada! ⚡ El área {metrosArea} m² subió a Lista {nuevaListaArea}.";
                            return RedirectToAction("Index");
                        }
                    }
                }
            }

            TempData["Exito"] = "¡Venta registrada exitosamente!";
            return RedirectToAction("Index");
        }
    }
}
