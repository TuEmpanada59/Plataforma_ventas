using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Plataforma_ventas.Filters;

namespace Plataforma_ventas.Controllers
{
    [RolAutorizado("Vendedor")]
    public class VendedorController : Controller
    {
        private readonly string _conn;

        public VendedorController(IConfiguration config)
        {
            _conn = config.GetConnectionString("DefaultConnection")!;
        }

        private void CargarSesion()
        {
            ViewBag.Nombre = HttpContext.Session.GetString("Nombre") ?? "";
            ViewBag.Apellido = HttpContext.Session.GetString("Apellido") ?? "";
            ViewBag.ProyectoActivo = HttpContext.Session.GetString("ProyectoNombre") ?? "Sin proyecto";
        }

        public IActionResult Index()
        {
            CargarSesion();
            int idUsuario = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;

            using var con = new SqlConnection(_conn);
            con.Open();

            if (idProy == 0)
            {
                var cmdProy = new SqlCommand("SELECT IdProyecto FROM Usuarios WHERE IdUsuario=@id", con);
                cmdProy.Parameters.AddWithValue("@id", idUsuario);
                var res = cmdProy.ExecuteScalar();
                if (res != null && res != DBNull.Value)
                {
                    idProy = (int)res;
                    var cmdNom = new SqlCommand("SELECT Nombre FROM Proyectos WHERE IdProyectos=@id", con);
                    cmdNom.Parameters.AddWithValue("@id", idProy);
                    var nom = cmdNom.ExecuteScalar()?.ToString() ?? "";
                    HttpContext.Session.SetString("ProyectoId", idProy.ToString());
                    HttpContext.Session.SetString("ProyectoNombre", nom);
                    ViewBag.ProyectoActivo = nom;
                }
            }

            if (idProy == 0)
            {
                ViewBag.SinProyecto = true;
                if (TempData["ErrorCodigo"] != null)
                    ViewBag.ErrorCodigo = TempData["ErrorCodigo"]?.ToString();
                return View();
            }

            ViewBag.SinProyecto = false;

            var cmdKpi = new SqlCommand(@"
                SELECT COUNT(*) AS Total,
                    SUM(CASE WHEN Estado='DISPONIBLE' THEN 1 ELSE 0 END) AS Disponibles,
                    SUM(CASE WHEN Estado='RESERVADO'  THEN 1 ELSE 0 END) AS Reservados,
                    SUM(CASE WHEN Estado='VENDIDO'    THEN 1 ELSE 0 END) AS Vendidos
                FROM Inmuebles WHERE IdProyecto = @id", con);
            cmdKpi.Parameters.AddWithValue("@id", idProy);
            using var rKpi = cmdKpi.ExecuteReader();
            if (rKpi.Read())
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
            ViewBag.MisVentas = (int)cmdMisVentas.ExecuteScalar();

            var cmdMisClientes = new SqlCommand(
                "SELECT COUNT(DISTINCT IdCliente) FROM Ventas WHERE IdUsuario=@uid", con);
            cmdMisClientes.Parameters.AddWithValue("@uid", idUsuario);
            ViewBag.MisClientes = (int)cmdMisClientes.ExecuteScalar();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AsignarProyecto(string codigo)
        {
            int idUsuario = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;
            if (string.IsNullOrWhiteSpace(codigo))
            {
                TempData["ErrorCodigo"] = "Ingresa un código de acceso.";
                return RedirectToAction("Index");
            }
            using var con = new SqlConnection(_conn);
            con.Open();
            var cmd = new SqlCommand(
                "SELECT IdProyectos, Nombre FROM Proyectos WHERE CodigoAcceso=@codigo AND Activo=1", con);
            cmd.Parameters.AddWithValue("@codigo", codigo.Trim().ToUpper());
            using var r = cmd.ExecuteReader();
            if (!r.Read())
            {
                r.Close();
                TempData["ErrorCodigo"] = "Código inválido. Verifica con tu administrador.";
                return RedirectToAction("Index");
            }
            int idProy = (int)r["IdProyectos"];
            string nomProy = r["Nombre"]?.ToString() ?? "";
            r.Close();

            var cmdUpd = new SqlCommand(
                "UPDATE Usuarios SET IdProyecto=@proy WHERE IdUsuario=@uid", con);
            cmdUpd.Parameters.AddWithValue("@proy", idProy);
            cmdUpd.Parameters.AddWithValue("@uid", idUsuario);
            cmdUpd.ExecuteNonQuery();

            HttpContext.Session.SetString("ProyectoId", idProy.ToString());
            HttpContext.Session.SetString("ProyectoNombre", nomProy);
            TempData["Exito"] = $"¡Proyecto {nomProy} asignado correctamente!";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CambiarProyecto(int idProyecto, string nombreProyecto)
        {
            return RedirectToAction("Index");
        }

        public IActionResult Inmuebles([FromQuery] string area = "")
        {
            CargarSesion();
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;
            int idUsuario = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;

            using var con = new SqlConnection(_conn);
            con.Open();

            var cmdLista = new SqlCommand(
                "SELECT ListaActual FROM Proyectos WHERE IdProyectos=@id", con);
            cmdLista.Parameters.AddWithValue("@id", idProy);
            var resLista = cmdLista.ExecuteScalar();
            int listaActual = resLista != null && resLista != DBNull.Value ? (int)resLista : 1;
            ViewBag.ListaActual = listaActual;

            var listasXArea = new Dictionary<string, int>();
            var aptsXArea = new Dictionary<string, int>();
            var cmdPAL = new SqlCommand(
                "SELECT Metros, ListaActual, AptsPorLista FROM ProyectoAreaListas WHERE IdProyecto=@id", con);
            cmdPAL.Parameters.AddWithValue("@id", idProy);
            using (var rPAL = cmdPAL.ExecuteReader())
                while (rPAL.Read())
                {
                    var metros = rPAL["Metros"]?.ToString() ?? "";
                    listasXArea[metros] = rPAL["ListaActual"] == DBNull.Value ? 1 : (int)rPAL["ListaActual"];
                    aptsXArea[metros] = rPAL["AptsPorLista"] == DBNull.Value ? 0 : (int)rPAL["AptsPorLista"];
                }
            ViewBag.ListasXArea = listasXArea;
            ViewBag.AptsXArea = aptsXArea;

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
            using (var reader = cmd.ExecuteReader())
                while (reader.Read())
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
            using (var rv = cmdVend.ExecuteReader())
                while (rv.Read())
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
            ViewBag.EnProceso = lista.Count(x => x.Estado == "EN PROCESO");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult TomarInmueble(int idInmueble)
        {
            int idUsuario = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;
            using var con = new SqlConnection(_conn);
            con.Open();
            var cmdCheck = new SqlCommand("SELECT Estado FROM Inmuebles WHERE IdInmuebles=@id", con);
            cmdCheck.Parameters.AddWithValue("@id", idInmueble);
            if (cmdCheck.ExecuteScalar()?.ToString() != "DISPONIBLE")
            {
                TempData["Error"] = "Este inmueble ya no está disponible.";
                return RedirectToAction("Inmuebles");
            }
            var cmd = new SqlCommand(@"UPDATE Inmuebles
                SET Estado='EN PROCESO', IdVendedorEnProceso=@uid, FechaEnProceso=GETDATE()
                WHERE IdInmuebles=@id", con);
            cmd.Parameters.AddWithValue("@uid", idUsuario);
            cmd.Parameters.AddWithValue("@id", idInmueble);
            cmd.ExecuteNonQuery();
            return RedirectToAction("RegistrarVenta", new { idInmueble });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CancelarProceso(int idInmueble)
        {
            int idUsuario = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;
            using var con = new SqlConnection(_conn);
            con.Open();
            var cmd = new SqlCommand(@"UPDATE Inmuebles
                SET Estado='DISPONIBLE', IdVendedorEnProceso=NULL, FechaEnProceso=NULL
                WHERE IdInmuebles=@id AND IdVendedorEnProceso=@uid", con);
            cmd.Parameters.AddWithValue("@id", idInmueble);
            cmd.Parameters.AddWithValue("@uid", idUsuario);
            cmd.ExecuteNonQuery();
            return RedirectToAction("Inmuebles");
        }

        // ── Reservar — guarda precio bloqueado ──
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ReservarInmueble(int idInmueble)
        {
            int idUsuario = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;

            using var con = new SqlConnection(_conn);
            con.Open();

            var cmdCheck = new SqlCommand(
                "SELECT Estado, Metros FROM Inmuebles WHERE IdInmuebles=@id", con);
            cmdCheck.Parameters.AddWithValue("@id", idInmueble);
            using var rCheck = cmdCheck.ExecuteReader();
            if (!rCheck.Read() || rCheck["Estado"].ToString() != "DISPONIBLE")
            {
                TempData["Error"] = "Este inmueble ya no está disponible para reservar.";
                return RedirectToAction("Inmuebles");
            }
            var metros = rCheck["Metros"]?.ToString() ?? "";
            rCheck.Close();

            // Lista activa del área
            var cmdLista = new SqlCommand(@"
                SELECT ISNULL(pal.ListaActual, p.ListaActual) AS ListaActual
                FROM Proyectos p
                LEFT JOIN ProyectoAreaListas pal
                    ON pal.IdProyecto = p.IdProyectos AND pal.Metros = @metros
                WHERE p.IdProyectos = @proy", con);
            cmdLista.Parameters.AddWithValue("@metros", metros);
            cmdLista.Parameters.AddWithValue("@proy", idProy);
            int listaActual = (int)(cmdLista.ExecuteScalar() ?? 1);

            // Precio de esa lista
            var col = listaActual switch { 1 => "Lista1", 2 => "Lista2", 3 => "Lista3", 4 => "Lista4", _ => "Lista5" };
            var cmdPrecio = new SqlCommand($"SELECT {col} FROM Inmuebles WHERE IdInmuebles=@id", con);
            cmdPrecio.Parameters.AddWithValue("@id", idInmueble);
            var rawPrecio = cmdPrecio.ExecuteScalar()?.ToString() ?? "0";
            var limpio = rawPrecio.Replace("$", "").Replace(".", "").Replace(",", "").Replace(" ", "").Trim();
            long.TryParse(limpio, out long precioReserva);

            var cmd = new SqlCommand(@"UPDATE Inmuebles
                SET Estado='RESERVADO', IdVendedorReserva=@uid,
                    PrecioReserva=@precio, FechaReserva=GETDATE()
                WHERE IdInmuebles=@id", con);
            cmd.Parameters.AddWithValue("@uid", idUsuario);
            cmd.Parameters.AddWithValue("@precio", precioReserva);
            cmd.Parameters.AddWithValue("@id", idInmueble);
            cmd.ExecuteNonQuery();

            TempData["Exito"] = $"Inmueble reservado. Precio bloqueado: ${string.Format("{0:N0}", precioReserva)}";
            return RedirectToAction("Inmuebles");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult LiberarReserva(int idInmueble)
        {
            int idUsuario = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;
            using var con = new SqlConnection(_conn);
            con.Open();
            var cmd = new SqlCommand(@"UPDATE Inmuebles
                SET Estado='DISPONIBLE', IdVendedorReserva=NULL,
                    PrecioReserva=NULL, FechaReserva=NULL
                WHERE IdInmuebles=@id AND IdVendedorReserva=@uid", con);
            cmd.Parameters.AddWithValue("@id", idInmueble);
            cmd.Parameters.AddWithValue("@uid", idUsuario);
            cmd.ExecuteNonQuery();
            TempData["Exito"] = "Reserva liberada correctamente.";
            return RedirectToAction("MisReservas");
        }

        // ── Mis Reservas — con precio bloqueado ──
        public IActionResult MisReservas()
        {
            CargarSesion();
            int idUsuario = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;

            using var con = new SqlConnection(_conn);
            con.Open();

            var reservas = new List<dynamic>();
            var cmd = new SqlCommand(@"
                SELECT IdInmuebles, Apto, Torre, Piso, Metros, Tipo,
                       PrecioReserva, FechaReserva
                FROM Inmuebles
                WHERE IdProyecto=@proy AND Estado='RESERVADO' AND IdVendedorReserva=@uid
                ORDER BY Apto", con);
            cmd.Parameters.AddWithValue("@proy", idProy);
            cmd.Parameters.AddWithValue("@uid", idUsuario);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
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

        // ── Continuar venta desde reserva (GET) ──
        public IActionResult ContinuarVenta(int idInmueble)
        {
            CargarSesion();
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;
            int idUsuario = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;

            using var con = new SqlConnection(_conn);
            con.Open();

            var cmd = new SqlCommand(@"
                SELECT IdInmuebles, Apto, Metros, Tipo, Torre, Piso, PrecioReserva
                FROM Inmuebles
                WHERE IdInmuebles=@id AND Estado='RESERVADO' AND IdVendedorReserva=@uid", con);
            cmd.Parameters.AddWithValue("@id", idInmueble);
            cmd.Parameters.AddWithValue("@uid", idUsuario);
            using var r = cmd.ExecuteReader();
            if (!r.Read())
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
            using var rC = cmdCli.ExecuteReader();
            while (rC.Read())
                clientes.Add(new
                {
                    Id = (int)rC["IdCliente"],
                    Nombre = rC["NombreCompleto"]?.ToString() ?? "",
                    Documento = rC["Documento"]?.ToString() ?? "",
                });
            ViewBag.Clientes = clientes;
            return View();
        }

        // ── Confirmar venta desde reserva (POST) ──
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ConfirmarVentaReserva(int idInmueble, long precioVenta,
            int? idClienteExistente, string tipoCliente, string destino,
            string clienteNombre, string clienteApellido, string clienteDocumento,
            string clienteCelular, string clienteCorreo, string clienteDireccion)
        {
            int idUsuario = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;

            using var con = new SqlConnection(_conn);
            con.Open();

            // Verificar que la reserva pertenece a este vendedor
            var cmdCheck = new SqlCommand(@"
                SELECT Metros, PrecioReserva FROM Inmuebles
                WHERE IdInmuebles=@id AND Estado='RESERVADO' AND IdVendedorReserva=@uid", con);
            cmdCheck.Parameters.AddWithValue("@id", idInmueble);
            cmdCheck.Parameters.AddWithValue("@uid", idUsuario);
            using var rCheck = cmdCheck.ExecuteReader();
            if (!rCheck.Read())
            {
                TempData["Error"] = "No tienes acceso a esta reserva.";
                return RedirectToAction("MisReservas");
            }
            var metros = rCheck["Metros"]?.ToString() ?? "";
            long precioFijo = rCheck["PrecioReserva"] == DBNull.Value ? precioVenta : (long)rCheck["PrecioReserva"];
            rCheck.Close();

            // Lista activa del área al momento de confirmar
            var cmdLista = new SqlCommand(@"
                SELECT ISNULL(pal.ListaActual, p.ListaActual) AS ListaActual
                FROM Proyectos p
                LEFT JOIN ProyectoAreaListas pal
                    ON pal.IdProyecto = p.IdProyectos AND pal.Metros = @metros
                WHERE p.IdProyectos = @proy", con);
            cmdLista.Parameters.AddWithValue("@metros", metros);
            cmdLista.Parameters.AddWithValue("@proy", idProy);
            int listaAplicada = (int)(cmdLista.ExecuteScalar() ?? 1);

            // Cliente
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
                idCliente = (int)cmdCli.ExecuteScalar()!;
            }

            // Registrar venta con precio bloqueado
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
            cmdVenta.ExecuteNonQuery();

            // Marcar como vendido y limpiar reserva
            var cmdInm = new SqlCommand(@"UPDATE Inmuebles
                SET Estado='VENDIDO', IdVendedorReserva=NULL,
                    PrecioReserva=NULL, FechaReserva=NULL
                WHERE IdInmuebles=@id", con);
            cmdInm.Parameters.AddWithValue("@id", idInmueble);
            cmdInm.ExecuteNonQuery();

            TempData["Exito"] = $"¡Venta confirmada! Precio aplicado: ${string.Format("{0:N0}", precioFijo)}";
            return RedirectToAction("MisVentas");
        }

        public IActionResult RegistrarVenta(int idInmueble)
        {
            CargarSesion();
            int idUsuario = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;

            using var con = new SqlConnection(_conn);
            con.Open();

            var cmdInm = new SqlCommand(@"SELECT IdInmuebles,Apto,Tipo,Piso,Metros,
                Lista1,Lista2,Lista3,Lista4,Lista5,Torre,Estado,IdVendedorEnProceso
                FROM Inmuebles WHERE IdInmuebles=@id", con);
            cmdInm.Parameters.AddWithValue("@id", idInmueble);
            using var r = cmdInm.ExecuteReader();
            if (!r.Read() || r["Estado"]?.ToString() != "EN PROCESO" || (int)r["IdVendedorEnProceso"] != idUsuario)
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
            r.Close();

            var cmdProy = new SqlCommand(
                "SELECT ListaActual, ApartamentosPorLista FROM Proyectos WHERE IdProyectos=@id", con);
            cmdProy.Parameters.AddWithValue("@id", idProy);
            using var rP = cmdProy.ExecuteReader();
            int listaActual = 1, aptsPorLista = 0;
            if (rP.Read())
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
            using var rc = cmdCli.ExecuteReader();
            while (rc.Read())
                clientes.Add(new
                {
                    Id = (int)rc["IdCliente"],
                    Nombre = rc["NombreCompleto"]?.ToString() ?? "",
                    Documento = rc["Documento"]?.ToString() ?? "",
                });
            ViewBag.Clientes = clientes;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ConfirmarVenta(int idInmueble, int listaAplicada, long precioVenta,
            string accion, int? idClienteExistente, string tipoCliente,
            string clienteNombre, string clienteApellido, string clienteDocumento,
            string clienteCelular, string clienteCorreo, string clienteDireccion)
        {
            int idUsuario = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;

            using var con = new SqlConnection(_conn);
            con.Open();

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
                idCliente = (int)cmdCli.ExecuteScalar()!;
            }

            var cmdVenta = new SqlCommand(@"INSERT INTO Ventas
                (IdInmueble,IdCliente,IdUsuario,IdProyecto,ListaAplicada,PrecioVenta,Estado)
                VALUES (@inm,@cli,@usr,@proy,@lista,@precio,'ACTIVA')", con);
            cmdVenta.Parameters.AddWithValue("@inm", idInmueble);
            cmdVenta.Parameters.AddWithValue("@cli", idCliente);
            cmdVenta.Parameters.AddWithValue("@usr", idUsuario);
            cmdVenta.Parameters.AddWithValue("@proy", idProy);
            cmdVenta.Parameters.AddWithValue("@lista", listaAplicada);
            cmdVenta.Parameters.AddWithValue("@precio", precioVenta);
            cmdVenta.ExecuteNonQuery();

            var cmdInm2 = new SqlCommand(@"UPDATE Inmuebles
                SET Estado='VENDIDO', IdVendedorEnProceso=NULL, FechaEnProceso=NULL
                WHERE IdInmuebles=@id", con);
            cmdInm2.Parameters.AddWithValue("@id", idInmueble);
            cmdInm2.ExecuteNonQuery();

            var cmdConfig = new SqlCommand(
                "SELECT ListaActual, ApartamentosPorLista FROM Proyectos WHERE IdProyectos=@id", con);
            cmdConfig.Parameters.AddWithValue("@id", idProy);
            using var rC = cmdConfig.ExecuteReader();
            if (rC.Read())
            {
                int listaActual = rC["ListaActual"] == DBNull.Value ? 1 : (int)rC["ListaActual"];
                int aptsPorLista = rC["ApartamentosPorLista"] == DBNull.Value ? 0 : (int)rC["ApartamentosPorLista"];
                rC.Close();
                if (aptsPorLista > 0)
                {
                    var cmdV = new SqlCommand(
                        "SELECT COUNT(*) FROM Inmuebles WHERE IdProyecto=@id AND Estado='VENDIDO'", con);
                    cmdV.Parameters.AddWithValue("@id", idProy);
                    int totalVendidos = (int)cmdV.ExecuteScalar()!;
                    int listaCalculada = Math.Min(5, (totalVendidos / aptsPorLista) + 1);
                    if (listaCalculada > listaActual)
                    {
                        var cmdS = new SqlCommand(
                            "UPDATE Proyectos SET ListaActual=@l WHERE IdProyectos=@id", con);
                        cmdS.Parameters.AddWithValue("@l", listaCalculada);
                        cmdS.Parameters.AddWithValue("@id", idProy);
                        cmdS.ExecuteNonQuery();
                        TempData["Exito"] = $"¡Venta registrada! ⚡ Proyecto subió a Lista {listaCalculada}.";
                        return RedirectToAction("MisVentas");
                    }
                }
            }
            else rC.Close();

            TempData["Exito"] = "¡Venta registrada exitosamente!";
            return RedirectToAction("MisVentas");
        }

        public IActionResult MisVentas()
        {
            CargarSesion();
            int idUsuario = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;

            using var con = new SqlConnection(_conn);
            con.Open();
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
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
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

        public IActionResult Perfil()
        {
            CargarSesion();
            int idUsuario = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;
            using var con = new SqlConnection(_conn);
            con.Open();
            var cmd = new SqlCommand(
                "SELECT Nombre, Apellido, Usuario, Correo, Documento, Celular FROM Usuarios WHERE IdUsuario=@id", con);
            cmd.Parameters.AddWithValue("@id", idUsuario);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
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
    }
}
