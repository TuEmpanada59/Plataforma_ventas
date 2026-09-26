using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Plataforma_ventas.Filters;

namespace Plataforma_ventas.Controllers
{
    /// <summary>
    /// Área de Dirección comercial: un espacio propio, no las pantallas de gestión con
    /// los botones escondidos.
    ///
    /// La diferencia importa. Una pantalla de gestión a la que se le quitan las acciones
    /// queda con columnas vacías, tarjetas a medias y sitios donde uno espera un botón y
    /// no hay nada. Aquí cada pantalla se diseñó para leer: números grandes arriba,
    /// tablas sin columna de acciones y ningún control que no sea navegar entre pestañas.
    ///
    /// Los candados siguen en RolAutorizadoAttribute: nada que no sea lectura, y ninguna
    /// respuesta que sea un archivo. Este controlador no tiene una sola acción que
    /// escriba, pero el candado se queda como red de seguridad.
    /// </summary>
    [RolAutorizado(Roles.Direccion)]
    public class DireccionController : Controller
    {
        private readonly string _conn;

        public DireccionController(IConfiguration config)
            => _conn = config.GetConnectionString("DefaultConnection")!;

        /// <summary>Datos comunes a todas las pestañas.</summary>
        private void Cabecera(string nav, string bread)
        {
            ViewBag.Nombre = HttpContext.Session.GetString("Nombre") ?? "";
            ViewBag.Apellido = HttpContext.Session.GetString("Apellido") ?? "";
            ViewBag.ProyectoActivo = HttpContext.Session.GetString("ProyectoNombre") ?? "Sin proyecto";
            ViewBag.ActiveNav = nav;
            ViewBag.TbBread = bread;
        }

        private int ProyectoActual()
            => int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int p) ? p : 0;

        // ══════════════════════════ RESUMEN ══════════════════════════

        /// <summary>
        /// La pantalla de entrada: cómo va el lanzamiento en cifras. Es lo que dirección
        /// mira primero y muchas veces lo único que necesita.
        /// </summary>
        public async Task<IActionResult> Index()
        {
            Cabecera("resumen", "Resumen");
            int idProy = ProyectoActual();

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            var cmdCols = new SqlCommand(
                "SELECT COL_LENGTH('Inmuebles','EnLanzamiento'), COL_LENGTH('Proyectos','Actividad')", con);
            bool hayLanz = false, hayActividad = false;
            using (var rc = (SqlDataReader)await cmdCols.ExecuteReaderAsync())
                if (await rc.ReadAsync())
                {
                    hayLanz = rc[0] is not (null or DBNull);
                    hayActividad = rc[1] is not (null or DBNull);
                }

            // Un inmueble "en proceso" no está comprometido: cuenta como disponible,
            // igual que en el mapa y en el informe del administrador.
            var cmdKpi = new SqlCommand($@"
                SELECT COUNT(*) AS Total,
                    SUM(CASE WHEN Estado IN ('DISPONIBLE','EN PROCESO') THEN 1 ELSE 0 END) AS Disponibles,
                    SUM(CASE WHEN Estado='VENDIDO'   THEN 1 ELSE 0 END) AS Vendidos,
                    SUM(CASE WHEN Estado='RESERVADO' THEN 1 ELSE 0 END) AS Reservados,
                    {(hayLanz ? "SUM(CASE WHEN EnLanzamiento=1 THEN 1 ELSE 0 END)" : "COUNT(*)")} AS TotalLanzamiento
                FROM Inmuebles WHERE IdProyecto=@id", con);
            cmdKpi.Parameters.AddWithValue("@id", idProy);
            int total = 0, disponibles = 0, vendidos = 0, reservados = 0, totalLanz = 0;
            using (var rk = (SqlDataReader)await cmdKpi.ExecuteReaderAsync())
                if (await rk.ReadAsync())
                {
                    total = rk["Total"] == DBNull.Value ? 0 : Convert.ToInt32(rk["Total"]);
                    disponibles = rk["Disponibles"] == DBNull.Value ? 0 : Convert.ToInt32(rk["Disponibles"]);
                    vendidos = rk["Vendidos"] == DBNull.Value ? 0 : Convert.ToInt32(rk["Vendidos"]);
                    reservados = rk["Reservados"] == DBNull.Value ? 0 : Convert.ToInt32(rk["Reservados"]);
                    totalLanz = rk["TotalLanzamiento"] == DBNull.Value ? total : Convert.ToInt32(rk["TotalLanzamiento"]);
                }
            ViewBag.Total = total;
            ViewBag.Disponibles = disponibles;
            ViewBag.Vendidos = vendidos;
            ViewBag.Reservados = reservados;
            ViewBag.TotalLanzamiento = totalLanz;
            ViewBag.Historico = Math.Max(0, total - totalLanz);

            var cmdValor = new SqlCommand(
                "SELECT ISNULL(SUM(PrecioVenta),0) FROM Ventas WHERE IdProyecto=@id AND Estado='ACTIVA'", con);
            cmdValor.Parameters.AddWithValue("@id", idProy);
            ViewBag.ValorVendido = Convert.ToInt64(await cmdValor.ExecuteScalarAsync());

            var cmdReserv = new SqlCommand(
                "SELECT ISNULL(SUM(PrecioReserva),0) FROM Inmuebles WHERE IdProyecto=@id AND Estado='RESERVADO'", con);
            cmdReserv.Parameters.AddWithValue("@id", idProy);
            ViewBag.ValorReservado = Convert.ToInt64(await cmdReserv.ExecuteScalarAsync());

            // FechaVenta se guarda en UTC y Colombia es UTC-5 fijo.
            var cmdHoy = new SqlCommand(@"
                SELECT COUNT(*) AS Num, ISNULL(SUM(PrecioVenta),0) AS Valor
                FROM Ventas WHERE IdProyecto=@id AND Estado='ACTIVA'
                  AND CAST(DATEADD(HOUR,-5,FechaVenta) AS DATE) = CAST(DATEADD(HOUR,-5,GETDATE()) AS DATE)", con);
            cmdHoy.Parameters.AddWithValue("@id", idProy);
            using (var rh = (SqlDataReader)await cmdHoy.ExecuteReaderAsync())
                if (await rh.ReadAsync())
                {
                    ViewBag.VentasHoy = Convert.ToInt32(rh["Num"]);
                    ViewBag.ValorHoy = Convert.ToInt64(rh["Valor"]);
                }

            if (hayActividad)
            {
                var cmdAct = new SqlCommand(
                    "SELECT ISNULL(Actividad,'') AS A, ISNULL(EtapaLanzamiento,'') AS E FROM Proyectos WHERE IdProyectos=@id", con);
                cmdAct.Parameters.AddWithValue("@id", idProy);
                using var ra = (SqlDataReader)await cmdAct.ExecuteReaderAsync();
                if (await ra.ReadAsync())
                {
                    ViewBag.ActividadTitulo = Actividades.Titulo(ra["A"]?.ToString());
                    ViewBag.EtapaLanzada = ra["E"]?.ToString() ?? "";
                }
            }

            // Desempeño por asesor. Solo quien vendió en este proyecto: una lista con
            // quince asesores en cero no dice nada y esconde a los que sí movieron.
            var asesores = new List<dynamic>();
            var cmdAs = new SqlCommand(@"
                SELECT u.Nombre+' '+u.Apellido AS Nombre,
                       COUNT(v.IdVenta) AS Ventas,
                       ISNULL(SUM(v.PrecioVenta),0) AS Valor
                FROM Ventas v
                JOIN Usuarios u ON u.IdUsuario = v.IdUsuario
                WHERE v.IdProyecto=@id AND v.Estado='ACTIVA'
                GROUP BY u.IdUsuario, u.Nombre, u.Apellido
                ORDER BY COUNT(v.IdVenta) DESC, SUM(v.PrecioVenta) DESC", con);
            cmdAs.Parameters.AddWithValue("@id", idProy);
            using (var r = (SqlDataReader)await cmdAs.ExecuteReaderAsync())
                while (await r.ReadAsync())
                    asesores.Add(new
                    {
                        Nombre = r["Nombre"]?.ToString() ?? "",
                        Ventas = Convert.ToInt32(r["Ventas"]),
                        Valor = Convert.ToInt64(r["Valor"]),
                    });
            ViewBag.Asesores = asesores;

            var tipologias = new List<dynamic>();
            var cmdTipo = new SqlCommand(@"
                SELECT Metros,
                       COUNT(*) AS Total,
                       SUM(CASE WHEN Estado='VENDIDO'   THEN 1 ELSE 0 END) AS Vendidos,
                       SUM(CASE WHEN Estado='RESERVADO' THEN 1 ELSE 0 END) AS Reservados
                FROM Inmuebles
                WHERE IdProyecto=@id AND Metros IS NOT NULL AND Metros<>''
                GROUP BY Metros ORDER BY COUNT(*) DESC", con);
            cmdTipo.Parameters.AddWithValue("@id", idProy);
            using (var r = (SqlDataReader)await cmdTipo.ExecuteReaderAsync())
                while (await r.ReadAsync())
                    tipologias.Add(new
                    {
                        Metros = r["Metros"]?.ToString() ?? "",
                        Total = Convert.ToInt32(r["Total"]),
                        Vendidos = Convert.ToInt32(r["Vendidos"]),
                        Reservados = Convert.ToInt32(r["Reservados"]),
                    });
            ViewBag.Tipologias = tipologias;

            return View();
        }

        // ══════════════════════════ MAPA ══════════════════════════

        /// <summary>El mapa por piso y línea, el mismo que ven el administrador y el asesor.</summary>
        public async Task<IActionResult> Mapa()
        {
            Cabecera("mapa", "Mapa de ventas");
            int idProy = ProyectoActual();

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            var cmdCol = new SqlCommand("SELECT COL_LENGTH('Inmuebles','Etapa')", con);
            bool hayEtapa = (await cmdCol.ExecuteScalarAsync()) is not (null or DBNull);

            var mapa = new List<dynamic>();
            var cmd = new SqlCommand($@"
                SELECT i.IdInmuebles, i.Apto, i.Torre, i.Piso, i.Tipo, i.Metros, i.Estado,
                       {(hayEtapa ? "ISNULL(i.Etapa,'')" : "''")} AS Etapa,
                       ISNULL(up.Nombre + ' ' + up.Apellido, '') AS EnProcesoPor,
                       ISNULL(ur.Nombre + ' ' + ur.Apellido, '') AS ReservadoPor
                FROM Inmuebles i
                LEFT JOIN Usuarios up ON i.IdVendedorEnProceso = up.IdUsuario
                LEFT JOIN Usuarios ur ON i.IdVendedorReserva   = ur.IdUsuario
                WHERE i.IdProyecto=@id
                ORDER BY i.Torre, TRY_CONVERT(int, i.Piso) DESC, i.Apto", con);
            cmd.Parameters.AddWithValue("@id", idProy);
            using (var r = (SqlDataReader)await cmd.ExecuteReaderAsync())
                while (await r.ReadAsync())
                    mapa.Add(new
                    {
                        Id = Convert.ToInt32(r["IdInmuebles"]),
                        Apto = r["Apto"]?.ToString() ?? "",
                        Torre = r["Torre"]?.ToString() ?? "",
                        Piso = r["Piso"]?.ToString() ?? "",
                        Tipo = r["Tipo"]?.ToString() ?? "",
                        Metros = r["Metros"]?.ToString() ?? "",
                        Etapa = r["Etapa"]?.ToString() ?? "",
                        Estado = r["Estado"]?.ToString() ?? "",
                        EnProcesoPor = (r["EnProcesoPor"]?.ToString() ?? "").Trim(),
                        ReservadoPor = (r["ReservadoPor"]?.ToString() ?? "").Trim(),
                    });

            ViewBag.MapaPisos = MapaPisos.Construir(mapa);
            ViewBag.ProyectoIdMapa = idProy;
            ViewBag.TotalUnidades = mapa.Count;
            return View();
        }

        // ══════════════════════════ INVENTARIO ══════════════════════════

        /// <summary>
        /// Todas las unidades con su estado y su precio. Los filtros son enlaces, no
        /// formularios: navegar no es modificar.
        /// </summary>
        public async Task<IActionResult> Inventario(string torre = "", string estado = "")
        {
            Cabecera("inventario", "Inventario");
            int idProy = ProyectoActual();

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            var cmdCol = new SqlCommand("SELECT COL_LENGTH('Inmuebles','Etapa')", con);
            bool hayEtapa = (await cmdCol.ExecuteScalarAsync()) is not (null or DBNull);

            // Lista vigente por área: es la que define el precio que hoy se le ofrece al
            // cliente, y cambia sola a medida que se vende.
            var listaPorArea = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var cmdAreas = new SqlCommand(
                "SELECT Metros, ISNULL(ListaActual,1) AS L FROM ProyectoAreaListas WHERE IdProyecto=@id", con);
            cmdAreas.Parameters.AddWithValue("@id", idProy);
            using (var r = (SqlDataReader)await cmdAreas.ExecuteReaderAsync())
                while (await r.ReadAsync())
                    listaPorArea[r["Metros"]?.ToString() ?? ""] = Convert.ToInt32(r["L"]);

            var inmuebles = new List<dynamic>();
            var cmd = new SqlCommand($@"
                SELECT i.Apto, i.Torre, i.Piso, i.Tipo, i.Metros, i.Estado,
                       {(hayEtapa ? "ISNULL(i.Etapa,'')" : "''")} AS Etapa,
                       i.Lista1, i.Lista2, i.Lista3, i.Lista4, i.Lista5,
                       ISNULL(i.PrecioReserva,0) AS PrecioReserva,
                       ISNULL(v.PrecioVenta,0) AS PrecioVenta
                FROM Inmuebles i
                LEFT JOIN Ventas v ON v.IdInmueble = i.IdInmuebles AND v.Estado='ACTIVA'
                WHERE i.IdProyecto=@id
                ORDER BY i.Torre, TRY_CONVERT(int, i.Piso), i.Apto", con);
            cmd.Parameters.AddWithValue("@id", idProy);
            using (var r = (SqlDataReader)await cmd.ExecuteReaderAsync())
                while (await r.ReadAsync())
                {
                    var metros = r["Metros"]?.ToString() ?? "";
                    int lista = listaPorArea.TryGetValue(metros, out int l) ? l : 1;
                    var est = r["Estado"]?.ToString() ?? "";
                    long precioLista = Texto.ParsearPrecio(r[Listas.ColumnaLista(lista)]?.ToString());

                    // El precio que se muestra depende del estado: lo vendido vale lo que
                    // se pagó, lo reservado lo que se bloqueó, y lo libre la lista vigente.
                    long precio = est == "VENDIDO" ? Convert.ToInt64(r["PrecioVenta"])
                                : est == "RESERVADO" ? Convert.ToInt64(r["PrecioReserva"])
                                : precioLista;
                    if (precio <= 0) precio = precioLista;

                    inmuebles.Add(new
                    {
                        Apto = r["Apto"]?.ToString() ?? "",
                        Torre = r["Torre"]?.ToString() ?? "",
                        Piso = r["Piso"]?.ToString() ?? "",
                        Tipo = r["Tipo"]?.ToString() ?? "",
                        Metros = metros,
                        Etapa = r["Etapa"]?.ToString() ?? "",
                        Estado = est,
                        Lista = lista,
                        Precio = precio,
                    });
                }

            // Los filtros se aplican en memoria: son pocas unidades y así la lista de
            // torres del filtro sale del inventario completo, no del ya filtrado.
            var torres = inmuebles.Select(i => (string)i.Torre)
                                  .Where(t => !string.IsNullOrWhiteSpace(t))
                                  .Distinct().OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList();
            ViewBag.Torres = torres;

            var filtrados = inmuebles.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(torre))
                filtrados = filtrados.Where(i => string.Equals((string)i.Torre, torre, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(estado))
                filtrados = filtrados.Where(i => EstadoVisible((string)i.Estado) == estado.ToUpperInvariant());

            var resultado = filtrados.ToList();
            ViewBag.Inmuebles = resultado;
            ViewBag.TotalSinFiltro = inmuebles.Count;
            ViewBag.FiltroTorre = torre ?? "";
            ViewBag.FiltroEstado = (estado ?? "").ToUpperInvariant();

            // Una columna que está vacía en todas las filas es ruido. Los proyectos de
            // una sola torre no informan torre, y los de una sola etapa tampoco.
            ViewBag.HayTorre = inmuebles.Any(i => !string.IsNullOrWhiteSpace((string)i.Torre));
            ViewBag.HayEtapa = inmuebles.Any(i => !string.IsNullOrWhiteSpace((string)i.Etapa));
            ViewBag.HayPiso  = inmuebles.Any(i => !string.IsNullOrWhiteSpace((string)i.Piso));
            ViewBag.HayTipo  = inmuebles.Any(i => !string.IsNullOrWhiteSpace((string)i.Tipo));

            // El valor de lo que se está mirando: con un filtro puesto responde
            // "cuánto hay en disponible" sin sacar una calculadora.
            ViewBag.ValorFiltrado = resultado.Sum(i => (long)i.Precio);

            // Agrupado por área, que es como el área comercial piensa el inventario:
            // no "cuántas unidades quedan" sino "cuántas de 98 metros quedan". Una lista
            // corrida de setenta unidades obliga a contar a ojo para responder eso.
            var porArea = resultado
                .GroupBy(i => (string)i.Metros)
                .Select(g => new
                {
                    Metros = g.Key,
                    Tipo = g.Select(x => (string)x.Tipo).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)) ?? "",
                    Unidades = g.ToList(),
                    Total = g.Count(),
                    Vendidas = g.Count(x => (string)x.Estado == "VENDIDO"),
                    Reservadas = g.Count(x => (string)x.Estado == "RESERVADO"),
                    Disponibles = g.Count(x => EstadoVisible((string)x.Estado) == "DISPONIBLE"),
                    Valor = g.Sum(x => (long)x.Precio),
                    // La lista vigente es la misma para todas las unidades del área.
                    Lista = g.Select(x => (int)x.Lista).FirstOrDefault(),
                })
                .OrderBy(a => AreaNumerica(a.Metros))
                .ToList<dynamic>();
            ViewBag.PorArea = porArea;
            return View();
        }

        // ══════════════════════════ VENTAS ══════════════════════════

        /// <summary>
        /// Qué se vendió, con qué lista y a qué precio. Sin datos del comprador: no se
        /// consultan, así que no llegan al navegador.
        /// </summary>
        public async Task<IActionResult> Ventas()
        {
            Cabecera("ventas", "Ventas");
            int idProy = ProyectoActual();

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            var cmdCol = new SqlCommand("SELECT COL_LENGTH('Ventas','Origen')", con);
            bool hayOrigen = (await cmdCol.ExecuteScalarAsync()) is not (null or DBNull);

            var ventas = new List<dynamic>();
            var cmd = new SqlCommand($@"
                SELECT i.Apto, i.Torre, i.Tipo, i.Metros,
                       u.Nombre+' '+u.Apellido AS Asesor,
                       ISNULL(v.Destino,'—') AS Destino,
                       ISNULL(v.ListaAplicada,1) AS Lista,
                       ISNULL(v.PrecioVenta,0) AS PrecioVenta,
                       FORMAT(DATEADD(HOUR,-5,v.FechaVenta),'dd/MM/yyyy HH:mm') AS Fecha,
                       {(hayOrigen ? "ISNULL(v.Origen,'PLATAFORMA')" : "'PLATAFORMA'")} AS Origen
                FROM Ventas v
                JOIN Inmuebles i ON i.IdInmuebles = v.IdInmueble
                JOIN Usuarios  u ON u.IdUsuario   = v.IdUsuario
                WHERE v.IdProyecto=@id AND v.Estado='ACTIVA'
                ORDER BY v.FechaVenta DESC", con);
            cmd.Parameters.AddWithValue("@id", idProy);
            using (var r = (SqlDataReader)await cmd.ExecuteReaderAsync())
                while (await r.ReadAsync())
                    ventas.Add(new
                    {
                        Apto = r["Apto"]?.ToString() ?? "",
                        Torre = r["Torre"]?.ToString() ?? "",
                        Tipo = r["Tipo"]?.ToString() ?? "",
                        Metros = r["Metros"]?.ToString() ?? "",
                        Asesor = r["Asesor"]?.ToString() ?? "",
                        Destino = r["Destino"]?.ToString() ?? "—",
                        Lista = Convert.ToInt32(r["Lista"]),
                        Precio = Convert.ToInt64(r["PrecioVenta"]),
                        Fecha = r["Fecha"]?.ToString() ?? "",
                        DelExcel = (r["Origen"]?.ToString() ?? "") == "EXCEL",
                    });
            ViewBag.Ventas = ventas;
            return View();
        }

        // ══════════════════════════ RESERVAS ══════════════════════════

        /// <summary>
        /// Las reservas, presentadas por antigüedad y no como la tabla operativa del
        /// administrador. A dirección no le sirve saber a quién reasignar: le sirve ver
        /// cuánto valor está comprometido y cuáles llevan demasiado tiempo quietas.
        /// </summary>
        public async Task<IActionResult> Reservas()
        {
            Cabecera("reservas", "Reservas");
            int idProy = ProyectoActual();

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            var cmdCols = new SqlCommand(@"SELECT COL_LENGTH('Inmuebles','ObservacionReserva'),
                                                  COL_LENGTH('Inmuebles','Etapa'),
                                                  COL_LENGTH('Inmuebles','EnLanzamiento')", con);
            bool hayObs = false, hayEtapa = false, hayLanz = false;
            using (var rc = (SqlDataReader)await cmdCols.ExecuteReaderAsync())
                if (await rc.ReadAsync())
                {
                    hayObs = rc[0] is not (null or DBNull);
                    hayEtapa = rc[1] is not (null or DBNull);
                    hayLanz = rc[2] is not (null or DBNull);
                }

            var reservas = new List<dynamic>();
            var cmd = new SqlCommand($@"
                SELECT i.Apto, i.Torre, i.Tipo, i.Metros,
                       {(hayEtapa ? "ISNULL(i.Etapa,'')" : "''")} AS Etapa,
                       {(hayObs ? "ISNULL(i.ObservacionReserva,'')" : "''")} AS Observacion,
                       {(hayLanz ? "i.EnLanzamiento" : "CAST(1 AS BIT)")} AS EnLanzamiento,
                       ISNULL(i.PrecioReserva,0) AS Precio,
                       ISNULL(us.Nombre + ' ' + us.Apellido,'') AS Asesor,
                       i.FechaReserva,
                       DATEDIFF(HOUR, i.FechaReserva, GETDATE()) AS Horas
                FROM Inmuebles i
                LEFT JOIN Usuarios us ON us.IdUsuario = i.IdVendedorReserva
                WHERE i.IdProyecto=@id AND i.Estado='RESERVADO'
                ORDER BY i.FechaReserva", con);
            cmd.Parameters.AddWithValue("@id", idProy);
            using (var r = (SqlDataReader)await cmd.ExecuteReaderAsync())
                while (await r.ReadAsync())
                    reservas.Add(new
                    {
                        Apto = r["Apto"]?.ToString() ?? "",
                        Torre = r["Torre"]?.ToString() ?? "",
                        Tipo = r["Tipo"]?.ToString() ?? "",
                        Metros = r["Metros"]?.ToString() ?? "",
                        Etapa = r["Etapa"]?.ToString() ?? "",
                        Observacion = r["Observacion"]?.ToString() ?? "",
                        Precio = Convert.ToInt64(r["Precio"]),
                        Asesor = (r["Asesor"]?.ToString() ?? "").Trim(),
                        Horas = r["Horas"] == DBNull.Value ? 0 : Convert.ToInt32(r["Horas"]),
                        // Una unidad que salió disponible a este evento y hoy está
                        // reservada, se reservó durante el lanzamiento. La que llegó
                        // reservada en el Excel ya venía comprometida de antes.
                        DelLanzamiento = r["EnLanzamiento"] == DBNull.Value || (bool)r["EnLanzamiento"],
                        Fecha = r["FechaReserva"] == DBNull.Value
                                ? "" : ((DateTime)r["FechaReserva"]).AddHours(-5).ToString("dd/MM/yyyy HH:mm"),
                    });
            ViewBag.Reservas = reservas;
            ViewBag.ValorReservado = reservas.Sum(x => (long)x.Precio);

            // Las dos clases que importan: lo que este equipo reservó en el evento y lo
            // que ya venía comprometido. Mezcladas, el número de reservas del lanzamiento
            // se infla con trabajo de otra jornada.
            var delLanzamiento = reservas.Where(x => (bool)x.DelLanzamiento).ToList();
            var previas = reservas.Where(x => !(bool)x.DelLanzamiento).ToList();
            ViewBag.DelLanzamiento = delLanzamiento;
            ViewBag.Previas = previas;
            ViewBag.ValorLanzamiento = delLanzamiento.Sum(x => (long)x.Precio);
            ViewBag.ValorPrevias = previas.Sum(x => (long)x.Precio);
            ViewBag.HayClasificacion = hayLanz;
            return View();
        }

        /// <summary>
        /// Ordena las áreas por su valor y no por texto: alfabéticamente "140.96" va
        /// antes que "98.17", que no es como nadie lee un cuadro de áreas.
        /// </summary>
        private static double AreaNumerica(string? metros)
        {
            double.TryParse((metros ?? "").Replace(",", "."),
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out double d);
            return d;
        }

        /// <summary>
        /// El estado tal como lo ve dirección: lo que está en proceso todavía no
        /// compromete la unidad, así que se presenta como disponible.
        /// </summary>
        public static string EstadoVisible(string? estado)
            => estado == "VENDIDO" || estado == "RESERVADO" ? estado! : "DISPONIBLE";
    }
}
