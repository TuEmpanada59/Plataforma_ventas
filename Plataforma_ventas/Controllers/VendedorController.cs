using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using Plataforma_ventas.Filters;
using Plataforma_ventas.Hubs;

namespace Plataforma_ventas.Controllers
{
    /// <summary>
    /// Handles all vendedor (sales agent) actions: dashboard, property browsing,
    /// reservations, sales registration, and profile management.
    /// </summary>
    [RolAutorizado("Vendedor")]
    public class VendedorController : Controller
    {
        private readonly string _conn;
        private readonly IHubContext<VentasHub, IVentasClient> _hub;

        /// <summary>Initializes the controller with DB connection and SignalR hub.</summary>
        public VendedorController(IConfiguration config, IHubContext<VentasHub, IVentasClient> hub)
        {
            _conn = config.GetConnectionString("DefaultConnection")!;
            _hub = hub;
        }

        private void CargarSesion()
        {
            ViewBag.Nombre = HttpContext.Session.GetString("Nombre") ?? "";
            ViewBag.Apellido = HttpContext.Session.GetString("Apellido") ?? "";
            ViewBag.ProyectoActivo = HttpContext.Session.GetString("ProyectoNombre") ?? "Sin proyecto";
        }

        /// <summary>
        /// Resuelve el proyecto asignado al vendedor: primero desde la sesión y, si no
        /// está, desde Usuarios.IdProyecto (asignado por el administrador). Si lo encuentra
        /// en BD lo guarda en sesión. Devuelve 0 si el vendedor aún no tiene proyecto.
        /// El código de acceso fue retirado: el proyecto lo asigna únicamente el admin.
        /// </summary>
        private async Task<int> ResolverProyectoAsignado()
        {
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;
            if (idProy != 0) return idProy;

            int idUsuario = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;
            if (idUsuario == 0) return 0;

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            var cmdProy = new SqlCommand("SELECT IdProyecto FROM Usuarios WHERE IdUsuario=@id", con);
            cmdProy.Parameters.AddWithValue("@id", idUsuario);
            var res = await cmdProy.ExecuteScalarAsync();
            if (res == null || res == DBNull.Value) return 0;
            idProy = Convert.ToInt32(res);
            if (idProy == 0) return 0;

            var cmdNom = new SqlCommand("SELECT Nombre FROM Proyectos WHERE IdProyectos=@id", con);
            cmdNom.Parameters.AddWithValue("@id", idProy);
            var nom = (await cmdNom.ExecuteScalarAsync())?.ToString() ?? "";
            HttpContext.Session.SetString("ProyectoId", idProy.ToString());
            HttpContext.Session.SetString("ProyectoNombre", nom);
            return idProy;
        }

        /// <summary>
        /// Bloquea la navegación del vendedor mientras no tenga un proyecto asignado.
        /// Toda acción distinta de <c>Index</c> redirige a <c>Index</c>, que muestra el
        /// aviso para que el administrador le asigne un proyecto.
        /// </summary>
        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var action = context.ActionDescriptor.RouteValues["action"];
            if (action != "Index" && await ResolverProyectoAsignado() == 0)
            {
                context.Result = RedirectToAction("Index");
                return;
            }
            await next();
        }

        /// <summary>
        /// Renders the vendedor dashboard with KPIs for the active project.
        /// Performs SELECT queries for totals and per-vendor sale counts.
        /// </summary>
        public async Task<IActionResult> Index()
        {
            CargarSesion();
            int idUsuario = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;
            int idProy = await ResolverProyectoAsignado();

            if (idProy == 0)
            {
                ViewBag.SinProyecto = true;
                return View();
            }

            ViewBag.SinProyecto = false;
            ViewBag.ProyectoActivo = HttpContext.Session.GetString("ProyectoNombre") ?? "Sin proyecto";

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            var cmdKpi = new SqlCommand(@"
                SELECT COUNT(*) AS Total,
                    SUM(CASE WHEN Estado='DISPONIBLE' THEN 1 ELSE 0 END) AS Disponibles,
                    SUM(CASE WHEN Estado='RESERVADO'  THEN 1 ELSE 0 END) AS Reservados,
                    SUM(CASE WHEN Estado='VENDIDO'    THEN 1 ELSE 0 END) AS Vendidos
                FROM Inmuebles WHERE IdProyecto = @id", con);
            cmdKpi.Parameters.AddWithValue("@id", idProy);
            using var rKpi = (SqlDataReader)await cmdKpi.ExecuteReaderAsync();
            if (await rKpi.ReadAsync())
            {
                ViewBag.Total = rKpi["Total"] == DBNull.Value ? 0 : (int)rKpi["Total"];
                ViewBag.Disponibles = rKpi["Disponibles"] == DBNull.Value ? 0 : (int)rKpi["Disponibles"];
                ViewBag.Reservados = rKpi["Reservados"] == DBNull.Value ? 0 : (int)rKpi["Reservados"];
                ViewBag.Vendidos = rKpi["Vendidos"] == DBNull.Value ? 0 : (int)rKpi["Vendidos"];
            }
            rKpi.Close();

            var cmdMisVentas = new SqlCommand(
                "SELECT COUNT(*) FROM Ventas WHERE IdUsuario=@uid AND IdProyecto=@pid", con);
            cmdMisVentas.Parameters.AddWithValue("@uid", idUsuario);
            cmdMisVentas.Parameters.AddWithValue("@pid", idProy);
            ViewBag.MisVentas = (int)(await cmdMisVentas.ExecuteScalarAsync())!;

            var cmdMisClientes = new SqlCommand(
                "SELECT COUNT(DISTINCT IdCliente) FROM Ventas WHERE IdUsuario=@uid", con);
            cmdMisClientes.Parameters.AddWithValue("@uid", idUsuario);
            ViewBag.MisClientes = (int)(await cmdMisClientes.ExecuteScalarAsync())!;

            return View();
        }

        /// <summary>
        /// Displays the property list for the vendedor's active project.
        /// Filters out other vendors' RESERVADO properties. Performs SELECT queries.
        /// </summary>
        /// <param name="area">Optional area (metros) filter to narrow displayed properties.</param>
        public async Task<IActionResult> Inmuebles([FromQuery] string area = "")
        {
            CargarSesion();
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;
            int idUsuario = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            var cmdLista = new SqlCommand(
                "SELECT ListaActual FROM Proyectos WHERE IdProyectos=@id", con);
            cmdLista.Parameters.AddWithValue("@id", idProy);
            var resLista = await cmdLista.ExecuteScalarAsync();
            int listaActual = resLista != null && resLista != DBNull.Value ? (int)resLista : 1;
            ViewBag.ListaActual = listaActual;

            var listasXArea = new Dictionary<string, int>();
            var aptsXArea = new Dictionary<string, int>();
            var cmdPAL = new SqlCommand(
                "SELECT Metros, ListaActual, AptsPorLista FROM ProyectoAreaListas WHERE IdProyecto=@id", con);
            cmdPAL.Parameters.AddWithValue("@id", idProy);
            using (var rPAL = (SqlDataReader)await cmdPAL.ExecuteReaderAsync())
                while (await rPAL.ReadAsync())
                {
                    var metrosK = rPAL["Metros"]?.ToString() ?? "";
                    listasXArea[metrosK] = rPAL["ListaActual"] == DBNull.Value ? 1 : (int)rPAL["ListaActual"];
                    aptsXArea[metrosK] = rPAL["AptsPorLista"] == DBNull.Value ? 0 : (int)rPAL["AptsPorLista"];
                }
            ViewBag.ListasXArea = listasXArea;

            var lista = new List<dynamic>();
            var cmd = new SqlCommand(@"
                SELECT IdInmuebles,Apto,Tipo,Piso,Metros,
                       Lista1,Lista2,Lista3,Lista4,Lista5,
                       Estado,Torre,IdVendedorEnProceso,IdVendedorReserva
                FROM Inmuebles
                WHERE IdProyecto=@id
                  AND (Estado != 'RESERVADO' OR IdVendedorReserva=@uid)
                ORDER BY Metros, Piso DESC, Apto", con);
            cmd.Parameters.AddWithValue("@id", idProy);
            cmd.Parameters.AddWithValue("@uid", idUsuario);
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

            var vendedores = new Dictionary<int, string>();
            var cmdVend = new SqlCommand(
                "SELECT IdUsuario, Nombre+' '+Apellido AS NombreCompleto FROM Usuarios WHERE Rol='Vendedor'", con);
            using (var rv = (SqlDataReader)await cmdVend.ExecuteReaderAsync())
                while (await rv.ReadAsync())
                    vendedores[(int)rv["IdUsuario"]] = rv["NombreCompleto"]?.ToString() ?? "";
            ViewBag.Vendedores = vendedores;

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
            ViewBag.IdUsuario = idUsuario;
            ViewBag.ProyectoId = idProy;
            ViewBag.EnProceso = lista.Count(x => x.Estado == "EN PROCESO");
            return View();
        }

        /// <summary>
        /// Atomically transitions a property from DISPONIBLE to EN PROCESO for this vendor.
        /// Uses a single UPDATE…WHERE Estado='DISPONIBLE' to eliminate the race condition
        /// that would occur with a separate SELECT then UPDATE. If rows affected == 0
        /// the property was already taken by another concurrent request and the user
        /// is redirected with an error — no double-booking can occur.
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
                return RedirectToAction("Inmuebles");
            }
            await _hub.Clients.All.InmuebleActualizado(idProy, idInmueble, "EN PROCESO");
            return RedirectToAction("RegistrarVenta", new { idInmueble });
        }

        /// <summary>
        /// Cancels the current sales process and returns the property to DISPONIBLE.
        /// Only the vendor who took the property can cancel it (WHERE clause includes IdVendedorEnProceso).
        /// Performs an UPDATE query.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelarProceso(int idInmueble)
        {
            int idUsuario = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;
            using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            var cmdMetros = new SqlCommand(
                "SELECT Metros FROM Inmuebles WHERE IdInmuebles=@id AND IdVendedorEnProceso=@uid", con);
            cmdMetros.Parameters.AddWithValue("@id", idInmueble);
            cmdMetros.Parameters.AddWithValue("@uid", idUsuario);
            string? metros = (await cmdMetros.ExecuteScalarAsync())?.ToString();
            var cmd = new SqlCommand(@"UPDATE Inmuebles
                SET Estado='DISPONIBLE', IdVendedorEnProceso=NULL, FechaEnProceso=NULL
                WHERE IdInmuebles=@id AND IdVendedorEnProceso=@uid", con);
            cmd.Parameters.AddWithValue("@id", idInmueble);
            cmd.Parameters.AddWithValue("@uid", idUsuario);
            await cmd.ExecuteNonQueryAsync();
            await _hub.Clients.All.InmuebleActualizado(idProy, idInmueble, "DISPONIBLE");
            if (!string.IsNullOrEmpty(metros))
                return RedirectToAction("Inmuebles", new { area = metros });
            return RedirectToAction("Inmuebles");
        }

        /// <summary>
        /// Atomically reserves a property for this vendor, locking in the current list price.
        /// Uses a single UPDATE…WHERE Estado='DISPONIBLE' to eliminate the race condition
        /// that would occur with a separate SELECT then UPDATE. If rows affected == 0
        /// the property was already taken by another concurrent request. The price at the
        /// moment of reservation is stored in PrecioReserva and honoured even if the list
        /// advances before the sale is confirmed.
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

            // Precio de esa lista
            var col = Listas.ColumnaLista(listaActual);
            var cmdPrecio = new SqlCommand($"SELECT {col} FROM Inmuebles WHERE IdInmuebles=@id", con);
            cmdPrecio.Parameters.AddWithValue("@id", idInmueble);
            var rawPrecio = (await cmdPrecio.ExecuteScalarAsync())?.ToString() ?? "0";
            var limpio = rawPrecio.Replace("$", "").Replace(".", "").Replace(",", "").Replace(" ", "").Trim();
            long.TryParse(limpio, out long precioReserva);

            // Atomic reserve: only succeeds if still DISPONIBLE — prevents double-booking
            var cmd = new SqlCommand(@"UPDATE Inmuebles
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
                return RedirectToAction("Inmuebles");
            }

            await _hub.Clients.All.InmuebleActualizado(idProy, idInmueble, "RESERVADO");
            TempData["Exito"] = $"Inmueble reservado. Precio bloqueado: ${string.Format("{0:N0}", precioReserva)}";
            return RedirectToAction("Inmuebles");
        }

        /// <summary>
        /// Releases the vendor's reservation on a property, returning it to DISPONIBLE.
        /// Only the owning vendor can release (WHERE IdVendedorReserva=@uid).
        /// Performs an UPDATE query.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LiberarReserva(int idInmueble)
        {
            int idUsuario = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;
            using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            var cmd = new SqlCommand(@"UPDATE Inmuebles
                SET Estado='DISPONIBLE', IdVendedorReserva=NULL,
                    PrecioReserva=NULL, FechaReserva=NULL
                WHERE IdInmuebles=@id AND IdVendedorReserva=@uid", con);
            cmd.Parameters.AddWithValue("@id", idInmueble);
            cmd.Parameters.AddWithValue("@uid", idUsuario);
            await cmd.ExecuteNonQueryAsync();
            await _hub.Clients.All.InmuebleActualizado(idProy, idInmueble, "DISPONIBLE");
            TempData["Exito"] = "Reserva liberada correctamente.";
            return RedirectToAction("MisReservas");
        }

        /// <summary>
        /// Lists all active reservations held by this vendor for the active project.
        /// Performs a SELECT query filtered by IdVendedorReserva.
        /// </summary>
        public async Task<IActionResult> MisReservas()
        {
            CargarSesion();
            int idUsuario = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            var reservas = new List<dynamic>();
            var cmd = new SqlCommand(@"
                SELECT IdInmuebles, Apto, Torre, Piso, Metros, Tipo,
                       PrecioReserva, FechaReserva
                FROM Inmuebles
                WHERE IdProyecto=@proy AND Estado='RESERVADO' AND IdVendedorReserva=@uid
                ORDER BY Apto", con);
            cmd.Parameters.AddWithValue("@proy", idProy);
            cmd.Parameters.AddWithValue("@uid", idUsuario);
            using var reader = (SqlDataReader)await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                reservas.Add(new
                {
                    Id = (int)reader["IdInmuebles"],
                    Apto = reader["Apto"]?.ToString() ?? "",
                    Torre = reader["Torre"]?.ToString() ?? "",
                    Piso = reader["Piso"]?.ToString() ?? "",
                    Metros = reader["Metros"]?.ToString() ?? "",
                    Tipo = reader["Tipo"]?.ToString() ?? "",
                    PrecioReserva = reader["PrecioReserva"] == DBNull.Value ? 0L : (long)reader["PrecioReserva"],
                    FechaReserva = reader["FechaReserva"] == DBNull.Value ? "" :
                                    ((DateTime)reader["FechaReserva"]).ToString("dd/MM/yyyy HH:mm"),
                });

            ViewBag.Reservas = reservas;
            ViewBag.TotalReservas = reservas.Count;
            return View();
        }

        /// <summary>
        /// Displays the form to continue a sale from an existing reservation.
        /// Only the vendor who owns the reservation can access it.
        /// Performs SELECT queries for the reserved property and client list.
        /// </summary>
        /// <param name="idInmueble">Reserved property to convert to a sale.</param>
        public async Task<IActionResult> ContinuarVenta(int idInmueble)
        {
            CargarSesion();
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;
            int idUsuario = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            var cmd = new SqlCommand(@"
                SELECT IdInmuebles, Apto, Metros, Tipo, Torre, Piso, PrecioReserva
                FROM Inmuebles
                WHERE IdInmuebles=@id AND Estado='RESERVADO' AND IdVendedorReserva=@uid", con);
            cmd.Parameters.AddWithValue("@id", idInmueble);
            cmd.Parameters.AddWithValue("@uid", idUsuario);
            using var r = (SqlDataReader)await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync())
            {
                TempData["Error"] = "No tienes acceso a esta reserva.";
                return RedirectToAction("MisReservas");
            }
            ViewBag.IdInmueble = (int)r["IdInmuebles"];
            ViewBag.Apto = r["Apto"]?.ToString() ?? "";
            ViewBag.Metros = r["Metros"]?.ToString() ?? "";
            ViewBag.Tipo = r["Tipo"]?.ToString() ?? "";
            ViewBag.Torre = r["Torre"]?.ToString() ?? "";
            ViewBag.PrecioReserva = r["PrecioReserva"] == DBNull.Value ? 0L : (long)r["PrecioReserva"];
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
        /// Confirms and records a sale for a previously reserved property.
        /// Uses the locked PrecioReserva so the price cannot change after reservation
        /// even if the list level advances before confirmation.
        /// Marks the property as VENDIDO and broadcasts the change via SignalR.
        /// Performs INSERT (Ventas), INSERT or SELECT (Clientes), UPDATE (Inmuebles) queries.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmarVentaReserva(int idInmueble, long precioVenta,
            int? idClienteExistente, string tipoCliente, string destino, bool sagrilaftConsultado,
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

            // Validar datos del cliente
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

            // Verificar que la reserva pertenece a este vendedor y leer el precio bloqueado.
            string metros = ""; long precioFijo = 0;
            var cmdCheck = new SqlCommand(@"
                SELECT Metros, PrecioReserva FROM Inmuebles
                WHERE IdInmuebles=@id AND Estado='RESERVADO' AND IdVendedorReserva=@uid", con, tx);
            cmdCheck.Parameters.AddWithValue("@id", idInmueble);
            cmdCheck.Parameters.AddWithValue("@uid", idUsuario);
            using (var rCheck = (SqlDataReader)await cmdCheck.ExecuteReaderAsync())
            {
                if (!await rCheck.ReadAsync())
                {
                    TempData["Error"] = "No tienes acceso a esta reserva.";
                    return RedirectToAction("MisReservas");
                }
                metros = rCheck["Metros"]?.ToString() ?? "";
                precioFijo = rCheck["PrecioReserva"] == DBNull.Value ? 0 : (long)rCheck["PrecioReserva"];
            }
            if (precioFijo <= 0)
            {
                TempData["Error"] = "La reserva no tiene un precio bloqueado válido.";
                return RedirectToAction("MisReservas");
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

            // Cliente
            int idCliente;
            if (tipoCliente == "existente" && idClienteExistente.HasValue && idClienteExistente.Value > 0)
                idCliente = idClienteExistente.Value;
            else
            {
                var cmdCli = new SqlCommand(@"INSERT INTO Clientes
                    (Nombre,Apellido,Documento,Celular,Correo,Direccion)
                    OUTPUT INSERTED.IdCliente
                    VALUES (@n,@a,@d,@c,@e,@dir)", con, tx);
                cmdCli.Parameters.AddWithValue("@n", clienteNombre ?? "");
                cmdCli.Parameters.AddWithValue("@a", clienteApellido ?? "");
                cmdCli.Parameters.AddWithValue("@d", Texto.SoloDigitos(clienteDocumento));
                cmdCli.Parameters.AddWithValue("@c", clienteCelular ?? "");
                cmdCli.Parameters.AddWithValue("@e", clienteCorreo ?? "");
                cmdCli.Parameters.AddWithValue("@dir", clienteDireccion ?? "");
                idCliente = (int)(await cmdCli.ExecuteScalarAsync())!;
            }

            // Marcar como vendido de forma ATÓMICA (verifica reserva + propietario).
            var cmdInm = new SqlCommand(@"UPDATE Inmuebles
                SET Estado='VENDIDO', IdVendedorReserva=NULL,
                    PrecioReserva=NULL, FechaReserva=NULL
                WHERE IdInmuebles=@id AND Estado='RESERVADO' AND IdVendedorReserva=@uid", con, tx);
            cmdInm.Parameters.AddWithValue("@id", idInmueble);
            cmdInm.Parameters.AddWithValue("@uid", idUsuario);
            if (await cmdInm.ExecuteNonQueryAsync() == 0)
            {
                TempData["Error"] = "Esta reserva ya no está disponible.";
                return RedirectToAction("MisReservas");
            }

            // Registrar venta con el precio bloqueado al reservar y destino validado.
            var cmdVenta = new SqlCommand(@"INSERT INTO Ventas
                (IdInmueble,IdCliente,IdUsuario,IdProyecto,ListaAplicada,PrecioVenta,Destino,Estado)
                VALUES (@inm,@cli,@usr,@proy,@lista,@precio,@destino,'ACTIVA')", con, tx);
            cmdVenta.Parameters.AddWithValue("@inm", idInmueble);
            cmdVenta.Parameters.AddWithValue("@cli", idCliente);
            cmdVenta.Parameters.AddWithValue("@usr", idUsuario);
            cmdVenta.Parameters.AddWithValue("@proy", idProy);
            cmdVenta.Parameters.AddWithValue("@lista", listaAplicada);
            cmdVenta.Parameters.AddWithValue("@precio", precioFijo);
            cmdVenta.Parameters.AddWithValue("@destino", Texto.DestinoVenta(destino));
            await cmdVenta.ExecuteNonQueryAsync();

            await tx.CommitAsync();

            await _hub.Clients.All.InmuebleActualizado(idProy, idInmueble, "VENDIDO");
            TempData["Exito"] = $"¡Venta confirmada! Precio aplicado: ${string.Format("{0:N0}", precioFijo)}";
            return RedirectToAction("MisVentas");
        }

        /// <summary>
        /// Displays the sale registration form for a property currently EN PROCESO.
        /// Only the vendor who claimed the property can access this form.
        /// Performs SELECT queries for property data, project list config, and client list.
        /// </summary>
        /// <param name="idInmueble">Property in EN PROCESO state to register a sale for.</param>
        public async Task<IActionResult> RegistrarVenta(int idInmueble)
        {
            CargarSesion();
            int idUsuario = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            var cmdInm = new SqlCommand(@"SELECT IdInmuebles,Apto,Tipo,Piso,Metros,
                Lista1,Lista2,Lista3,Lista4,Lista5,Torre,Estado,IdVendedorEnProceso
                FROM Inmuebles WHERE IdInmuebles=@id", con);
            cmdInm.Parameters.AddWithValue("@id", idInmueble);
            using var r = (SqlDataReader)await cmdInm.ExecuteReaderAsync();
            if (!await r.ReadAsync() || r["Estado"]?.ToString() != "EN PROCESO" || (int)r["IdVendedorEnProceso"] != idUsuario)
            {
                r.Close();
                TempData["Error"] = "No tienes acceso a este inmueble.";
                return RedirectToAction("Inmuebles");
            }
            ViewBag.Inmueble = new
            {
                Id = (int)r["IdInmuebles"],
                Apto = r["Apto"]?.ToString() ?? "",
                Tipo = r["Tipo"]?.ToString() ?? "",
                Piso = r["Piso"]?.ToString() ?? "",
                Metros = r["Metros"]?.ToString() ?? "",
                Lista1 = r["Lista1"]?.ToString() ?? "",
                Lista2 = r["Lista2"]?.ToString() ?? "",
                Lista3 = r["Lista3"]?.ToString() ?? "",
                Lista4 = r["Lista4"]?.ToString() ?? "",
                Lista5 = r["Lista5"]?.ToString() ?? "",
                Torre = r["Torre"]?.ToString() ?? "",
            };
            string metros = r["Metros"]?.ToString() ?? "";
            r.Close();

            // La lista activa es la del ÁREA del inmueble (ProyectoAreaListas), con
            // respaldo a la lista global del proyecto. Debe coincidir exactamente con
            // la que se muestra en la grilla de inmuebles y con la reserva, para no
            // aplicar una lista distinta al momento de vender.
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
        /// Confirms and records a sale for a property in EN PROCESO state.
        /// Marks the property as VENDIDO, checks for automatic list escalation,
        /// and broadcasts the estado change via SignalR.
        /// Performs INSERT (Ventas, Clientes), UPDATE (Inmuebles, Proyectos) queries.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmarVenta(int idInmueble, string accion,
            int? idClienteExistente, string tipoCliente, string destino, bool sagrilaftConsultado,
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
            // Transacción: el cambio de estado + el registro de la venta deben ser atómicos.
            using var tx = (SqlTransaction)await con.BeginTransactionAsync();

            // 1. Leer el inmueble y verificar estado + propietario (evita IDOR y ventas dobles).
            string metros = ""; var listasRaw = new string[5];
            var cmdSel = new SqlCommand(@"SELECT Metros, Lista1,Lista2,Lista3,Lista4,Lista5,
                Estado, IdVendedorEnProceso FROM Inmuebles WHERE IdInmuebles=@id", con, tx);
            cmdSel.Parameters.AddWithValue("@id", idInmueble);
            using (var rs = (SqlDataReader)await cmdSel.ExecuteReaderAsync())
            {
                if (!await rs.ReadAsync() || rs["Estado"]?.ToString() != "EN PROCESO"
                    || rs["IdVendedorEnProceso"] == DBNull.Value
                    || (int)rs["IdVendedorEnProceso"] != idUsuario)
                {
                    TempData["Error"] = "Este inmueble ya no está disponible para venta.";
                    return RedirectToAction("Inmuebles");
                }
                metros = rs["Metros"]?.ToString() ?? "";
                for (int i = 0; i < 5; i++) listasRaw[i] = rs[$"Lista{i + 1}"]?.ToString() ?? "";
            }

            // 2. Lista activa del ÁREA y precio derivados en el servidor (no se confía en el formulario).
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

            // 3. Cliente (dentro de la transacción).
            int idCliente;
            if (tipoCliente == "existente" && idClienteExistente.HasValue && idClienteExistente.Value > 0)
                idCliente = idClienteExistente.Value;
            else
            {
                var cmdCli = new SqlCommand(@"INSERT INTO Clientes
                    (Nombre,Apellido,Documento,Celular,Correo,Direccion)
                    OUTPUT INSERTED.IdCliente
                    VALUES (@n,@a,@d,@c,@e,@dir)", con, tx);
                cmdCli.Parameters.AddWithValue("@n", clienteNombre ?? "");
                cmdCli.Parameters.AddWithValue("@a", clienteApellido ?? "");
                cmdCli.Parameters.AddWithValue("@d", Texto.SoloDigitos(clienteDocumento));
                cmdCli.Parameters.AddWithValue("@c", clienteCelular ?? "");
                cmdCli.Parameters.AddWithValue("@e", clienteCorreo ?? "");
                cmdCli.Parameters.AddWithValue("@dir", clienteDireccion ?? "");
                idCliente = (int)(await cmdCli.ExecuteScalarAsync())!;
            }

            // 4. Marcar VENDIDO de forma ATÓMICA con verificación del estado y propietario previos.
            var cmdInm2 = new SqlCommand(@"UPDATE Inmuebles
                SET Estado='VENDIDO', IdVendedorEnProceso=NULL, FechaEnProceso=NULL
                WHERE IdInmuebles=@id AND Estado='EN PROCESO' AND IdVendedorEnProceso=@uid", con, tx);
            cmdInm2.Parameters.AddWithValue("@id", idInmueble);
            cmdInm2.Parameters.AddWithValue("@uid", idUsuario);
            if (await cmdInm2.ExecuteNonQueryAsync() == 0)
            {
                TempData["Error"] = "Este inmueble ya no está disponible para venta.";
                return RedirectToAction("Inmuebles");
            }

            // 5. Registrar la venta con precio/lista del servidor y destino validado.
            var cmdVenta = new SqlCommand(@"INSERT INTO Ventas
                (IdInmueble,IdCliente,IdUsuario,IdProyecto,ListaAplicada,PrecioVenta,Destino,Estado)
                VALUES (@inm,@cli,@usr,@proy,@lista,@precio,@destino,'ACTIVA')", con, tx);
            cmdVenta.Parameters.AddWithValue("@inm", idInmueble);
            cmdVenta.Parameters.AddWithValue("@cli", idCliente);
            cmdVenta.Parameters.AddWithValue("@usr", idUsuario);
            cmdVenta.Parameters.AddWithValue("@proy", idProy);
            cmdVenta.Parameters.AddWithValue("@lista", listaAplicada);
            cmdVenta.Parameters.AddWithValue("@precio", precioVenta);
            cmdVenta.Parameters.AddWithValue("@destino", Texto.DestinoVenta(destino));
            await cmdVenta.ExecuteNonQueryAsync();

            await tx.CommitAsync();

            await _hub.Clients.All.InmuebleActualizado(idProy, idInmueble, "VENDIDO");

            // ── Escalamiento automático de la lista, por área ──
            // Solo escala el área del inmueble vendido y solo si tiene AptsPorLista > 0
            // (las áreas fijadas manualmente quedan en 0 y nunca se mueven).
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
                int laArea = 1, aptsArea = 0;
                using (var rPAL = (SqlDataReader)await cmdPALEsc.ExecuteReaderAsync())
                    if (await rPAL.ReadAsync())
                    {
                        laArea = rPAL["ListaActual"] == DBNull.Value ? 1 : (int)rPAL["ListaActual"];
                        aptsArea = rPAL["AptsPorLista"] == DBNull.Value ? 0 : (int)rPAL["AptsPorLista"];
                    }

                if (aptsArea > 0)
                {
                    var cmdVArea = new SqlCommand(@"SELECT COUNT(*) FROM Ventas v
                        INNER JOIN Inmuebles i ON v.IdInmueble = i.IdInmuebles
                        WHERE v.IdProyecto=@proy AND i.Metros=@metros AND v.Estado='ACTIVA'", con);
                    cmdVArea.Parameters.AddWithValue("@proy", idProy);
                    cmdVArea.Parameters.AddWithValue("@metros", metrosArea);
                    int vendidosArea = (int)(await cmdVArea.ExecuteScalarAsync())!;
                    int nuevaListaArea = (vendidosArea / aptsArea) + 1;

                    if (nuevaListaArea > laArea && nuevaListaArea <= 5
                        && await ListaAreaConPrecios(con, idProy, metrosArea, nuevaListaArea))
                    {
                        var cmdUpArea = new SqlCommand(@"UPDATE ProyectoAreaListas
                            SET ListaActual=@lista WHERE IdProyecto=@proy AND Metros=@metros", con);
                        cmdUpArea.Parameters.AddWithValue("@lista", nuevaListaArea);
                        cmdUpArea.Parameters.AddWithValue("@proy", idProy);
                        cmdUpArea.Parameters.AddWithValue("@metros", metrosArea);
                        await cmdUpArea.ExecuteNonQueryAsync();
                        await _hub.Clients.All.ListaAreaActualizada(idProy, metrosArea, nuevaListaArea);
                        TempData["Exito"] = $"¡Venta registrada! ⚡ El área {metrosArea} m² subió a Lista {nuevaListaArea}.";
                        return RedirectToAction("MisVentas");
                    }
                }
            }

            TempData["Exito"] = "¡Venta registrada exitosamente!";
            return RedirectToAction("MisVentas");
        }

        /// <summary>
        /// Lists all sales made by this vendor across all projects.
        /// Performs a SELECT query joining Ventas, Inmuebles, and Clientes.
        /// </summary>
        public async Task<IActionResult> MisVentas()
        {
            CargarSesion();
            int idUsuario = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            var ventas = new List<dynamic>();
            var cmd = new SqlCommand(@"
                SELECT v.IdVenta, i.Apto, i.Torre, c.Nombre+' '+c.Apellido AS Cliente,
                       ISNULL(v.Destino,'—') AS Destino,
                       v.PrecioVenta,
                       FORMAT(v.FechaVenta,'dd/MM/yyyy HH:mm') AS FechaVenta,
                       v.Estado, v.ListaAplicada
                FROM Ventas v
                JOIN Inmuebles i ON v.IdInmueble = i.IdInmuebles
                JOIN Clientes  c ON v.IdCliente  = c.IdCliente
                WHERE v.IdUsuario = @uid
                ORDER BY v.FechaVenta DESC", con);
            cmd.Parameters.AddWithValue("@uid", idUsuario);
            using var reader = (SqlDataReader)await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                ventas.Add(new
                {
                    Id = (int)reader["IdVenta"],
                    Apto = reader["Apto"]?.ToString() ?? "",
                    Torre = reader["Torre"]?.ToString() ?? "",
                    Cliente = reader["Cliente"]?.ToString() ?? "",
                    Destino = reader["Destino"]?.ToString() ?? "—",
                    PrecioVenta = reader["PrecioVenta"]?.ToString() ?? "0",
                    FechaVenta = reader["FechaVenta"]?.ToString() ?? "",
                    Estado = reader["Estado"]?.ToString() ?? "",
                    Lista = reader["ListaAplicada"]?.ToString() ?? "",
                });
            ViewBag.Ventas = ventas;
            ViewBag.TotalVentas = ventas.Count;
            return View();
        }

        /// <summary>
        /// Displays the vendor's own profile data.
        /// Performs a SELECT query for the authenticated user's record.
        /// </summary>
        public async Task<IActionResult> Perfil()
        {
            CargarSesion();
            int idUsuario = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;
            using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            var cmd = new SqlCommand(
                "SELECT Nombre, Apellido, Usuario, Correo, Documento, Celular FROM Usuarios WHERE IdUsuario=@id", con);
            cmd.Parameters.AddWithValue("@id", idUsuario);
            using var reader = (SqlDataReader)await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                ViewBag.PerfilNombre = reader["Nombre"]?.ToString() ?? "";
                ViewBag.PerfilApellido = reader["Apellido"]?.ToString() ?? "";
                ViewBag.PerfilUsuario = reader["Usuario"]?.ToString() ?? "";
                ViewBag.PerfilCorreo = reader["Correo"]?.ToString() ?? "";
                ViewBag.PerfilDocumento = reader["Documento"]?.ToString() ?? "";
                ViewBag.PerfilCelular = reader["Celular"]?.ToString() ?? "";
            }
            return View();
        }

        /// <summary>
        /// Checks that a price list has at least one price &gt; 0 for the given area before
        /// auto-escalating to it, so the escalation never lands on an empty list.
        /// </summary>
        private static async Task<bool> ListaAreaConPrecios(SqlConnection con, int idProy, string metrosArea, int numLista)
        {
            var col = Listas.ColumnaLista(numLista);
            var cmd = new SqlCommand(
                $"SELECT {col} FROM Inmuebles WHERE IdProyecto=@proy AND Metros=@metros", con);
            cmd.Parameters.AddWithValue("@proy", idProy);
            cmd.Parameters.AddWithValue("@metros", metrosArea);
            using var r = (SqlDataReader)await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var limpio = (r[0]?.ToString() ?? "0")
                    .Replace("$", "").Replace(".", "").Replace(",", "").Replace(" ", "").Trim();
                if (long.TryParse(limpio, out long v) && v > 0) return true;
            }
            return false;
        }
    }
}
