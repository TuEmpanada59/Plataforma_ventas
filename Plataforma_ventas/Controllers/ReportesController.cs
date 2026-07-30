using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Plataforma_ventas.Filters;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QColor = QuestPDF.Infrastructure.Color;
using QColors = QuestPDF.Helpers.Colors;
using DColor = System.Drawing.Color;

namespace Plataforma_ventas.Controllers
{
    [RolAutorizado("Administrador")]
    public class ReportesController : Controller
    {
        private readonly string _conn;

        public ReportesController(IConfiguration config)
        {
            _conn = config.GetConnectionString("DefaultConnection")!;
        }

        /// <summary>
        /// Displays the reports dashboard with KPIs, asesor rankings, tipology breakdown,
        /// destination analysis, property map, and full sale list for the active project.
        /// Performs multiple SELECT queries against Inmuebles, Ventas, Usuarios, and Clientes.
        /// </summary>
        public async Task<IActionResult> Index()
        {
            ViewBag.Nombre = HttpContext.Session.GetString("Nombre") ?? "Admin";
            ViewBag.Apellido = HttpContext.Session.GetString("Apellido") ?? "";
            ViewBag.ProyectoActivo = HttpContext.Session.GetString("ProyectoNombre") ?? "Sin proyecto";
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;
            int idAdmin = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid2) ? uid2 : 0;

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            var proyectos = new List<(int Id, string Nombre)>();
            var cmdList = new SqlCommand("SELECT IdProyectos, Nombre FROM Proyectos WHERE Activo=1 AND IdAdminCreador=@admin ORDER BY FechaCarga DESC", con);
            cmdList.Parameters.AddWithValue("@admin", idAdmin);
            using (var r = (SqlDataReader)await cmdList.ExecuteReaderAsync())
                while (await r.ReadAsync())
                    proyectos.Add(((int)r["IdProyectos"], r["Nombre"]?.ToString() ?? ""));
            ViewBag.Proyectos = proyectos;

            var cmdKpi = new SqlCommand(@"
                SELECT COUNT(*) AS Total,
                    SUM(CASE WHEN Estado='DISPONIBLE' THEN 1 ELSE 0 END) AS Disponibles,
                    SUM(CASE WHEN Estado='VENDIDO'    THEN 1 ELSE 0 END) AS Vendidos,
                    SUM(CASE WHEN Estado='RESERVADO'  THEN 1 ELSE 0 END) AS Reservados,
                    SUM(CASE WHEN Estado='EN PROCESO' THEN 1 ELSE 0 END) AS EnProceso
                FROM Inmuebles WHERE IdProyecto=@id", con);
            cmdKpi.Parameters.AddWithValue("@id", idProy);
            using (var rk = (SqlDataReader)await cmdKpi.ExecuteReaderAsync())
                if (await rk.ReadAsync())
                {
                    ViewBag.Total = rk["Total"] == DBNull.Value ? 0 : (int)rk["Total"];
                    ViewBag.Disponibles = rk["Disponibles"] == DBNull.Value ? 0 : (int)rk["Disponibles"];
                    ViewBag.Vendidos = rk["Vendidos"] == DBNull.Value ? 0 : (int)rk["Vendidos"];
                    ViewBag.Reservados = rk["Reservados"] == DBNull.Value ? 0 : (int)rk["Reservados"];
                    ViewBag.EnProceso = rk["EnProceso"] == DBNull.Value ? 0 : (int)rk["EnProceso"];
                }

            var cmdValor = new SqlCommand("SELECT ISNULL(SUM(PrecioVenta),0) FROM Ventas WHERE IdProyecto=@id AND Estado='ACTIVA'", con);
            cmdValor.Parameters.AddWithValue("@id", idProy);
            ViewBag.ValorTotal = (long)(await cmdValor.ExecuteScalarAsync())!;

            var cmdHoy = new SqlCommand(@"
                SELECT COUNT(*) AS VentasHoy, ISNULL(SUM(PrecioVenta),0) AS ValorHoy
                FROM Ventas WHERE IdProyecto=@id AND Estado='ACTIVA'
                  AND CAST(FechaVenta AS DATE) = CAST(GETDATE() AS DATE)", con);
            cmdHoy.Parameters.AddWithValue("@id", idProy);
            using (var rh = (SqlDataReader)await cmdHoy.ExecuteReaderAsync())
                if (await rh.ReadAsync())
                {
                    ViewBag.VentasHoy = rh["VentasHoy"] == DBNull.Value ? 0 : (int)rh["VentasHoy"];
                    ViewBag.ValorHoy = rh["ValorHoy"] == DBNull.Value ? 0L : (long)rh["ValorHoy"];
                }

            var asesores = new List<dynamic>();
            var cmdAs = new SqlCommand(@"
                SELECT u.Nombre+' '+u.Apellido AS Nombre,
                       COUNT(v.IdVenta) AS TotalVentas,
                       ISNULL(SUM(v.PrecioVenta),0) AS ValorTotal
                FROM Usuarios u
                LEFT JOIN Ventas v ON u.IdUsuario=v.IdUsuario AND v.IdProyecto=@id AND v.Estado='ACTIVA'
                WHERE u.Rol='Vendedor'
                GROUP BY u.IdUsuario, u.Nombre, u.Apellido
                ORDER BY TotalVentas DESC", con);
            cmdAs.Parameters.AddWithValue("@id", idProy);
            using (var ra = (SqlDataReader)await cmdAs.ExecuteReaderAsync())
                while (await ra.ReadAsync())
                    asesores.Add(new { Nombre = ra["Nombre"]?.ToString() ?? "", TotalVentas = (int)ra["TotalVentas"], ValorTotal = (long)ra["ValorTotal"] });
            ViewBag.Asesores = asesores;

            var tipologias = new List<dynamic>();
            var cmdTipo = new SqlCommand(@"
                SELECT Tipo,
                    COUNT(*) AS Total,
                    SUM(CASE WHEN Estado='VENDIDO'    THEN 1 ELSE 0 END) AS Vendidos,
                    SUM(CASE WHEN Estado='DISPONIBLE' THEN 1 ELSE 0 END) AS Disponibles,
                    SUM(CASE WHEN Estado='RESERVADO'  THEN 1 ELSE 0 END) AS Reservados
                FROM Inmuebles WHERE IdProyecto=@id AND Tipo IS NOT NULL AND Tipo != ''
                GROUP BY Tipo ORDER BY Vendidos DESC", con);
            cmdTipo.Parameters.AddWithValue("@id", idProy);
            using (var rt = (SqlDataReader)await cmdTipo.ExecuteReaderAsync())
                while (await rt.ReadAsync())
                    tipologias.Add(new { Tipo = rt["Tipo"]?.ToString() ?? "", Total = (int)rt["Total"], Vendidos = (int)rt["Vendidos"], Disponibles = (int)rt["Disponibles"], Reservados = (int)rt["Reservados"] });
            ViewBag.Tipologias = tipologias;

            var destinos = new List<dynamic>();
            var cmdDest = new SqlCommand(@"
                SELECT ISNULL(Destino,'Sin especificar') AS Destino, COUNT(*) AS Total
                FROM Ventas WHERE IdProyecto=@id AND Estado='ACTIVA'
                GROUP BY Destino ORDER BY Total DESC", con);
            cmdDest.Parameters.AddWithValue("@id", idProy);
            using (var rd = (SqlDataReader)await cmdDest.ExecuteReaderAsync())
                while (await rd.ReadAsync())
                    destinos.Add(new { Destino = rd["Destino"]?.ToString() ?? "Sin especificar", Total = (int)rd["Total"] });
            ViewBag.Destinos = destinos;

            var mapa = new List<dynamic>();
            var cmdMapa = new SqlCommand("SELECT Apto, Tipo, Metros, Torre, Estado FROM Inmuebles WHERE IdProyecto=@id ORDER BY Torre, Apto", con);
            cmdMapa.Parameters.AddWithValue("@id", idProy);
            using (var rm = (SqlDataReader)await cmdMapa.ExecuteReaderAsync())
                while (await rm.ReadAsync())
                    mapa.Add(new { Apto = rm["Apto"]?.ToString() ?? "", Tipo = rm["Tipo"]?.ToString() ?? "", Metros = rm["Metros"]?.ToString() ?? "", Torre = rm["Torre"]?.ToString() ?? "", Estado = rm["Estado"]?.ToString() ?? "" });
            ViewBag.Mapa = mapa;

            var ventas = new List<dynamic>();
            var cmdVentas = new SqlCommand(@"
                SELECT u.Nombre+' '+u.Apellido AS Asesor,
                       i.Apto, i.Torre, i.Tipo, i.Metros,
                       c.Nombre+' '+c.Apellido AS Cliente,
                       ISNULL(v.Destino,'—') AS Destino,
                       v.ListaAplicada, v.PrecioVenta,
                       FORMAT(v.FechaVenta,'dd/MM/yyyy HH:mm') AS FechaVenta
                FROM Ventas v
                JOIN Inmuebles i ON v.IdInmueble=i.IdInmuebles
                JOIN Clientes  c ON v.IdCliente=c.IdCliente
                JOIN Usuarios  u ON v.IdUsuario=u.IdUsuario
                WHERE v.IdProyecto=@id AND v.Estado='ACTIVA'
                ORDER BY u.Nombre, v.FechaVenta DESC", con);
            cmdVentas.Parameters.AddWithValue("@id", idProy);
            using (var rvd = (SqlDataReader)await cmdVentas.ExecuteReaderAsync())
                while (await rvd.ReadAsync())
                    ventas.Add(new
                    {
                        Asesor = rvd["Asesor"]?.ToString() ?? "",
                        Apto = rvd["Apto"]?.ToString() ?? "",
                        Torre = rvd["Torre"]?.ToString() ?? "",
                        Tipo = rvd["Tipo"]?.ToString() ?? "",
                        Metros = rvd["Metros"]?.ToString() ?? "",
                        Cliente = rvd["Cliente"]?.ToString() ?? "",
                        Destino = rvd["Destino"]?.ToString() ?? "—",
                        Lista = rvd["ListaAplicada"]?.ToString() ?? "",
                        PrecioVenta = (long)rvd["PrecioVenta"],
                        FechaVenta = rvd["FechaVenta"]?.ToString() ?? "",
                    });
            ViewBag.Ventas = ventas;

            // ── Ventas por hora del día y por área (m²) para la gráfica de horas pico ──
            // La fecha se guarda en UTC (GETDATE en Azure SQL); Colombia es UTC-5 fijo.
            var vha = new List<dynamic>();
            var cmdVHA = new SqlCommand(@"
                SELECT DATEPART(HOUR, DATEADD(HOUR, -5, v.FechaVenta)) AS Hora,
                       ISNULL(NULLIF(i.Metros,''), 'N/D') AS Area,
                       COUNT(*) AS NumVentas,
                       ISNULL(SUM(v.PrecioVenta),0) AS Valor
                FROM Ventas v
                JOIN Inmuebles i ON v.IdInmueble = i.IdInmuebles
                WHERE v.IdProyecto=@id AND v.Estado='ACTIVA'
                GROUP BY DATEPART(HOUR, DATEADD(HOUR, -5, v.FechaVenta)), ISNULL(NULLIF(i.Metros,''), 'N/D')
                ORDER BY Hora", con);
            cmdVHA.Parameters.AddWithValue("@id", idProy);
            using (var rvha = (SqlDataReader)await cmdVHA.ExecuteReaderAsync())
                while (await rvha.ReadAsync())
                    vha.Add(new
                    {
                        Hora = Convert.ToInt32(rvha["Hora"]),
                        Area = rvha["Area"]?.ToString() ?? "N/D",
                        Num = Convert.ToInt32(rvha["NumVentas"]),
                        Valor = Convert.ToInt64(rvha["Valor"]),
                    });
            ViewBag.VentasHoraArea = vha;

            // Indicadores destacados (hora pico, área líder, combinación top).
            if (vha.Count > 0)
            {
                var porHora = vha.GroupBy(x => (int)x.Hora)
                    .Select(g => new { Hora = g.Key, Num = g.Sum(x => (int)x.Num), Valor = g.Sum(x => (long)x.Valor) })
                    .OrderByDescending(x => x.Num).ThenByDescending(x => x.Valor).ToList();
                var porArea = vha.GroupBy(x => (string)x.Area)
                    .Select(g => new { Area = g.Key, Num = g.Sum(x => (int)x.Num), Valor = g.Sum(x => (long)x.Valor) })
                    .OrderByDescending(x => x.Num).ToList();
                var combo = vha.OrderByDescending(x => (int)x.Num).ThenByDescending(x => (long)x.Valor).First();

                ViewBag.HoraPico = porHora[0].Hora;
                ViewBag.HoraPicoNum = porHora[0].Num;
                ViewBag.HoraPicoValor = porHora[0].Valor;
                ViewBag.AreaTop = porArea[0].Area;
                ViewBag.AreaTopNum = porArea[0].Num;
                ViewBag.ComboArea = (string)combo.Area;
                ViewBag.ComboHora = (int)combo.Hora;
                ViewBag.ComboNum = (int)combo.Num;
            }

            return View();
        }

        /// <summary>
        /// Generates a PDF technical sales report with KPIs, tipology breakdown,
        /// destination analysis, and full per-asesor sale detail table.
        /// Performs multiple SELECT queries then renders with QuestPDF.
        /// </summary>
        public async Task<IActionResult> ReportePDF()
        {
            QuestPDF.Settings.License = LicenseType.Community;
            QuestPDF.Settings.EnableDebugging = true;   // temporal: enriquece el error de layout con la ubicación exacta

            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;
            var proyNombre = HttpContext.Session.GetString("ProyectoNombre") ?? "Proyecto";

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            int total = 0, disponibles = 0, vendidos = 0, reservados = 0, enProceso = 0;
            long valorTotal = 0, valorHoy = 0;
            int ventasHoy = 0;

            var cmdKpi = new SqlCommand(@"
                SELECT COUNT(*) AS Total,
                    SUM(CASE WHEN Estado='DISPONIBLE' THEN 1 ELSE 0 END) AS Disponibles,
                    SUM(CASE WHEN Estado='VENDIDO'    THEN 1 ELSE 0 END) AS Vendidos,
                    SUM(CASE WHEN Estado='RESERVADO'  THEN 1 ELSE 0 END) AS Reservados,
                    SUM(CASE WHEN Estado='EN PROCESO' THEN 1 ELSE 0 END) AS EnProceso
                FROM Inmuebles WHERE IdProyecto=@id", con);
            cmdKpi.Parameters.AddWithValue("@id", idProy);
            using (var rk = (SqlDataReader)await cmdKpi.ExecuteReaderAsync())
                if (await rk.ReadAsync())
                {
                    total = rk["Total"] == DBNull.Value ? 0 : (int)rk["Total"];
                    disponibles = rk["Disponibles"] == DBNull.Value ? 0 : (int)rk["Disponibles"];
                    vendidos = rk["Vendidos"] == DBNull.Value ? 0 : (int)rk["Vendidos"];
                    reservados = rk["Reservados"] == DBNull.Value ? 0 : (int)rk["Reservados"];
                    enProceso = rk["EnProceso"] == DBNull.Value ? 0 : (int)rk["EnProceso"];
                }

            var cmdV = new SqlCommand("SELECT ISNULL(SUM(PrecioVenta),0) FROM Ventas WHERE IdProyecto=@id AND Estado='ACTIVA'", con);
            cmdV.Parameters.AddWithValue("@id", idProy);
            valorTotal = (long)(await cmdV.ExecuteScalarAsync())!;

            var cmdH = new SqlCommand(@"SELECT COUNT(*) AS VH, ISNULL(SUM(PrecioVenta),0) AS VLH
                FROM Ventas WHERE IdProyecto=@id AND Estado='ACTIVA'
                AND CAST(FechaVenta AS DATE)=CAST(GETDATE() AS DATE)", con);
            cmdH.Parameters.AddWithValue("@id", idProy);
            using (var rh = (SqlDataReader)await cmdH.ExecuteReaderAsync())
                if (await rh.ReadAsync()) { ventasHoy = (int)rh["VH"]; valorHoy = (long)rh["VLH"]; }

            var ventas = new List<(string Asesor, string Apto, string Torre, string Tipo, string Metros, string Cliente, string Destino, string Lista, long Precio, string Fecha)>();
            var cmdVentas = new SqlCommand(@"
                SELECT u.Nombre+' '+u.Apellido AS Asesor,
                       i.Apto, i.Torre, i.Tipo, i.Metros,
                       c.Nombre+' '+c.Apellido AS Cliente,
                       ISNULL(v.Destino,'—') AS Destino,
                       v.ListaAplicada, v.PrecioVenta,
                       FORMAT(v.FechaVenta,'dd/MM/yyyy HH:mm') AS FechaVenta
                FROM Ventas v
                JOIN Inmuebles i ON v.IdInmueble=i.IdInmuebles
                JOIN Clientes  c ON v.IdCliente=c.IdCliente
                JOIN Usuarios  u ON v.IdUsuario=u.IdUsuario
                WHERE v.IdProyecto=@id AND v.Estado='ACTIVA'
                ORDER BY u.Nombre, v.FechaVenta DESC", con);
            cmdVentas.Parameters.AddWithValue("@id", idProy);
            using (var rv2 = (SqlDataReader)await cmdVentas.ExecuteReaderAsync())
                while (await rv2.ReadAsync())
                    ventas.Add((rv2["Asesor"]?.ToString() ?? "", rv2["Apto"]?.ToString() ?? "", rv2["Torre"]?.ToString() ?? "",
                        rv2["Tipo"]?.ToString() ?? "", rv2["Metros"]?.ToString() ?? "", rv2["Cliente"]?.ToString() ?? "",
                        rv2["Destino"]?.ToString() ?? "—", rv2["ListaAplicada"]?.ToString() ?? "",
                        (long)rv2["PrecioVenta"], rv2["FechaVenta"]?.ToString() ?? ""));

            var tips = new List<(string Tipo, int Tot, int Vend, int Disp, int Res)>();
            var cmdT = new SqlCommand(@"
                SELECT Tipo, COUNT(*) AS Total,
                    SUM(CASE WHEN Estado='VENDIDO'    THEN 1 ELSE 0 END) AS Vendidos,
                    SUM(CASE WHEN Estado='DISPONIBLE' THEN 1 ELSE 0 END) AS Disponibles,
                    SUM(CASE WHEN Estado='RESERVADO'  THEN 1 ELSE 0 END) AS Reservados
                FROM Inmuebles WHERE IdProyecto=@id AND Tipo IS NOT NULL AND Tipo!=''
                GROUP BY Tipo ORDER BY Vendidos DESC", con);
            cmdT.Parameters.AddWithValue("@id", idProy);
            using (var rt = (SqlDataReader)await cmdT.ExecuteReaderAsync())
                while (await rt.ReadAsync())
                    tips.Add((rt["Tipo"]?.ToString() ?? "", (int)rt["Total"], (int)rt["Vendidos"], (int)rt["Disponibles"], (int)rt["Reservados"]));

            var dests = new List<(string Dest, int Tot)>();
            var cmdD = new SqlCommand(@"
                SELECT ISNULL(Destino,'Sin especificar') AS Destino, COUNT(*) AS Total
                FROM Ventas WHERE IdProyecto=@id AND Estado='ACTIVA'
                GROUP BY Destino ORDER BY Total DESC", con);
            cmdD.Parameters.AddWithValue("@id", idProy);
            using (var rd = (SqlDataReader)await cmdD.ExecuteReaderAsync())
                while (await rd.ReadAsync())
                    dests.Add((rd["Destino"]?.ToString() ?? "", (int)rd["Total"]));

            // ── Ventas por hora del día y por área (m²) — para el módulo de horas pico ──
            var vhaPdf = new List<(int Hora, string Area, int Num, long Valor)>();
            var cmdVhaP = new SqlCommand(@"
                SELECT DATEPART(HOUR, DATEADD(HOUR, -5, v.FechaVenta)) AS Hora,
                       ISNULL(NULLIF(i.Metros,''), 'N/D') AS Area,
                       COUNT(*) AS NumVentas,
                       ISNULL(SUM(v.PrecioVenta),0) AS Valor
                FROM Ventas v
                JOIN Inmuebles i ON v.IdInmueble = i.IdInmuebles
                WHERE v.IdProyecto=@id AND v.Estado='ACTIVA'
                GROUP BY DATEPART(HOUR, DATEADD(HOUR, -5, v.FechaVenta)), ISNULL(NULLIF(i.Metros,''), 'N/D')
                ORDER BY Hora", con);
            cmdVhaP.Parameters.AddWithValue("@id", idProy);
            using (var rvp = (SqlDataReader)await cmdVhaP.ExecuteReaderAsync())
                while (await rvp.ReadAsync())
                    vhaPdf.Add((Convert.ToInt32(rvp["Hora"]), rvp["Area"]?.ToString() ?? "N/D",
                               Convert.ToInt32(rvp["NumVentas"]), Convert.ToInt64(rvp["Valor"])));

            double pctV = total > 0 ? Math.Round((double)vendidos / total * 100, 1) : 0;
            double pctD = total > 0 ? Math.Round((double)disponibles / total * 100, 1) : 0;
            double pctR = total > 0 ? Math.Round((double)reservados / total * 100, 1) : 0;
            double pctP = total > 0 ? Math.Round((double)enProceso / total * 100, 1) : 0;

            var ahoraCol = AhoraColombia();
            var esCo = new System.Globalization.CultureInfo("es-CO");

            var asist = await CargarEventoAsync(con, idProy);

            // ── Helpers de diseño (informe técnico rediseñado, handoff PRIMAVELA) ──
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            string Money(long v) => "$" + v.ToString("N0", esCo);
            string MoneyM(long v) => v >= 1_000_000
                ? "$" + (v / 1_000_000d).ToString("#,##0.0", esCo) + "M"
                : Money(v);
            // Ícono Lucide inline (stroke 2, currentColor via color fijo)
            string Lucide(string inner, string color) =>
                $"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='none' stroke='{color}' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'>{inner}</svg>";
            var LIco = new Dictionary<string, string>
            {
                ["home"]       = "<path d='M3 9.5 12 3l9 6.5V21a1 1 0 0 1-1 1h-5v-7H9v7H4a1 1 0 0 1-1-1z'/>",
                ["check"]      = "<circle cx='12' cy='12' r='9'/><path d='m8 12 2.5 2.5L16 9'/>",
                ["key"]        = "<circle cx='7.5' cy='15.5' r='4.5'/><path d='m10.5 12.5 8-8M17 5l2 2M15 7l2 2'/>",
                ["lock"]       = "<rect x='5' y='11' width='14' height='10' rx='2'/><path d='M8 11V7a4 4 0 0 1 8 0v4'/>",
                ["calendar"]   = "<rect x='4' y='5' width='16' height='16' rx='2'/><path d='M4 9h16M8 3v4M16 3v4'/>",
                ["dollar"]     = "<line x1='12' y1='2' x2='12' y2='22'/><path d='M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6'/>",
                ["users"]      = "<path d='M16 20v-1a4 4 0 0 0-4-4H7a4 4 0 0 0-4 4v1'/><circle cx='9.5' cy='8' r='3.5'/><path d='M17 15a4 4 0 0 1 4 4v1'/><circle cx='17.5' cy='8' r='3'/>",
                ["user"]       = "<circle cx='12' cy='8' r='3.6'/><path d='M5.5 20a6.5 6.5 0 0 1 13 0'/>",
                ["baby"]       = "<path d='M9 12h.01M15 12h.01M10 16c.5.3 1.2.5 2 .5s1.5-.2 2-.5'/><circle cx='12' cy='12' r='9'/>",
                ["calcheck"]   = "<rect x='4' y='5' width='16' height='16' rx='2'/><path d='M4 9h16M8 3v4M16 3v4M9 15l2 2 4-4'/>",
                ["car"]        = "<path d='M5 13l1.5-4.5A2 2 0 0 1 8.4 7h7.2a2 2 0 0 1 1.9 1.5L19 13v5h-2v-2H7v2H5z'/><circle cx='8' cy='16' r='1'/><circle cx='16' cy='16' r='1'/>",
                ["bike"]       = "<circle cx='6' cy='17' r='3'/><circle cx='18' cy='17' r='3'/><path d='M6 17 10 8h4l2 4M9 8h4'/>",
                ["pause"]      = "<rect x='7' y='5' width='3.5' height='14' rx='1'/><rect x='13.5' y='5' width='3.5' height='14' rx='1'/>",
                ["clock"]      = "<circle cx='12' cy='12' r='9'/><path d='M12 7v5l3.5 2'/>",
                ["tag"]        = "<path d='M3 12V4a1 1 0 0 1 1-1h8l9 9-9 9z'/><circle cx='7.5' cy='7.5' r='1.4'/>",
                ["chart"]      = "<path d='M4 4v16h16'/><rect x='7' y='11' width='3' height='6'/><rect x='12' y='7' width='3' height='10'/><rect x='17' y='13' width='3' height='4'/>",
            };
            string DonutSvg(double pctVend)
            {
                double sold = Math.Max(pctVend, 0.6);           // mínimo visible
                double rest = Math.Max(100 - sold, 0);
                return "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 42 42'>" +
                       "<circle cx='21' cy='21' r='15.9155' fill='none' stroke='#9BE3B0' stroke-width='5'/>" +
                       $"<circle cx='21' cy='21' r='15.9155' fill='none' stroke='#E63946' stroke-width='5' " +
                       $"stroke-dasharray='{sold.ToString(inv)} {rest.ToString(inv)}' transform='rotate(-90 21 21)'/>" +
                       "</svg>";
            }
            // Formato de hora en 12h español (ej. "2:00 p. m.")
            string FmtHora(int h) => $"{(h % 12 == 0 ? 12 : h % 12)}:00 {(h < 12 ? "a. m." : "p. m.")}";
            // Etiqueta de módulo (01 · TÍTULO) con nota a la derecha
            string docId = $"IT-{new string(proyNombre.Where(char.IsLetter).Take(3).ToArray()).ToUpper()}-{ahoraCol:ddMM}";

            var pdfBytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.Letter);
                    page.Margin(34);
                    page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(9).FontColor(QColor.FromHex("#1A1A1A")));

                    // ── HEADER ──
                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text($"INFORME TÉCNICO DE VENTAS Y ASISTENCIA — {proyNombre.ToUpper()}")
                                    .FontSize(13).Bold().FontColor(QColor.FromHex("#1A1A1A"));
                                c.Item().PaddingTop(2).Text("SISTEMA DE LANZAMIENTOS INMOBILIARIOS")
                                    .FontSize(8).SemiBold().LetterSpacing(0.12f).FontColor(QColor.FromHex("#8A8A8E"));
                            });
                            row.ConstantItem(160).AlignRight().Column(c =>
                            {
                                c.Item().AlignRight().Text($"GEN. {ahoraCol:dd/MM/yyyy HH:mm}").FontSize(8).FontColor(QColor.FromHex("#8A8A8E"));
                                c.Item().AlignRight().PaddingTop(2).Text($"DOC. {docId} · 6 MÓDULOS").FontSize(8).FontColor(QColor.FromHex("#8A8A8E"));
                            });
                        });
                        col.Item().PaddingTop(10).LineHorizontal(2f).LineColor(QColor.FromHex("#1A1A1A"));
                    });

                    page.Content().PaddingTop(14).Column(col =>
                    {
                        // ── FRANJA KPI (6 columnas) ──
                        var kpis = new (string Ico, string Lbl, string Val, string Col, float Fs)[]
                        {
                            ("home",     "TOTAL",         total.ToString(),                    "#003A70", 19f),
                            ("check",    "DISPONIBLES",   disponibles.ToString(),              "#1A7A35", 19f),
                            ("key",      "VENDIDOS",      vendidos.ToString(),                 "#E63946", 19f),
                            ("lock",     "RESERVADOS",    reservados.ToString(),               "#CC7700", 19f),
                            ("calendar", "VENTAS HOY",    $"{ventasHoy} · {MoneyM(valorHoy)}", "#0055A5", 10f),
                            ("dollar",   "VALOR VENDIDO", MoneyM(valorTotal),                  "#003A70", 12f),
                        };
                        col.Item().BorderBottom(1).BorderColor(QColor.FromHex("#EDEDEF")).Row(row =>
                        {
                            for (int i = 0; i < kpis.Length; i++)
                            {
                                var k = kpis[i];
                                var cell = row.RelativeItem();
                                if (i < kpis.Length - 1) cell = cell.BorderRight(1).BorderColor(QColor.FromHex("#F2F2F4"));
                                cell.PaddingVertical(11).PaddingHorizontal(7).Column(c =>
                                {
                                    c.Item().Row(rr =>
                                    {
                                        rr.ConstantItem(11).Height(11).Svg(Lucide(LIco[k.Ico], k.Col));
                                        rr.RelativeItem().PaddingLeft(4).Text(k.Lbl)
                                            .FontSize(7.5f).SemiBold().LetterSpacing(0.05f).FontColor(QColor.FromHex("#8A8A8E"));
                                    });
                                    c.Item().PaddingTop(5).Text(k.Val).FontSize(k.Fs).Light().FontColor(QColor.FromHex(k.Col));
                                });
                            }
                        });

                        col.Item().PaddingTop(14);

                        // ── MÓDULO 01 · ESTADO DEL PROYECTO ──
                        col.Item().Row(mr =>
                        {
                            mr.RelativeItem().Text("01 · ESTADO DEL PROYECTO")
                                .FontSize(9).Bold().LetterSpacing(0.15f).FontColor(QColor.FromHex("#0077C8"));
                            mr.AutoItem().AlignRight().Text($"{total} unidades · {pctV.ToString("0.0", esCo)}% avance")
                                .FontSize(8.5f).FontColor(QColor.FromHex("#B0B0B4"));
                        });
                        col.Item().PaddingTop(10).Row(row =>
                        {
                            // Donut con % centrado
                            row.ConstantItem(150).Height(150).Layers(layers =>
                            {
                                layers.PrimaryLayer().Svg(DonutSvg(pctV));
                                layers.Layer().AlignMiddle().AlignCenter().Column(cc =>
                                {
                                    cc.Item().AlignCenter().Text($"{pctV.ToString("0.0", esCo)}%")
                                        .FontSize(34).Light().FontColor(QColor.FromHex("#003A70"));
                                    cc.Item().AlignCenter().Text("VENDIDO")
                                        .FontSize(7).SemiBold().LetterSpacing(0.15f).FontColor(QColor.FromHex("#8A8A8E"));
                                });
                            });
                            // Leyenda + barras por tipología
                            row.RelativeItem().PaddingLeft(24).Column(c =>
                            {
                                void Leg(string dot, string nombre, int val, double pct)
                                {
                                    c.Item().PaddingVertical(2).Row(lr =>
                                    {
                                        lr.ConstantItem(14).AlignMiddle().Text("●").FontSize(11).FontColor(QColor.FromHex(dot));
                                        lr.RelativeItem().AlignMiddle().Text(nombre).FontSize(11).FontColor(QColor.FromHex("#1A1A1A"));
                                        lr.AutoItem().AlignMiddle().PaddingRight(8).Text(val.ToString()).FontSize(18).Light().FontColor(QColor.FromHex("#1A1A1A"));
                                        lr.ConstantItem(46).AlignMiddle().AlignRight().Text($"{pct.ToString("0.0", esCo)}%").FontSize(10).FontColor(QColor.FromHex("#8A8A8E"));
                                    });
                                }
                                Leg("#34C759", "Disponibles", disponibles, pctD);
                                Leg("#E63946", "Vendidos", vendidos, pctV);
                                Leg("#FF9500", "Reservados", reservados, pctR);

                                c.Item().PaddingTop(8);
                                foreach (var t in tips.Take(6))
                                {
                                    double p = t.Tot > 0 ? (double)t.Vend / t.Tot * 100 : 0;
                                    int fillW = (int)Math.Max(Math.Round(p), p > 0 ? 4 : 1);
                                    int restW = Math.Max(100 - fillW, 0);
                                    c.Item().PaddingVertical(3).Row(br =>
                                    {
                                        br.ConstantItem(52).Text(t.Tipo).FontSize(8.5f).SemiBold().FontColor(QColor.FromHex("#3A3A3C"));
                                        br.RelativeItem().Height(12).Background(QColor.FromHex("#EEEFF1")).Row(bar =>
                                        {
                                            bar.RelativeItem(fillW).Background(QColor.FromHex("#0077C8"));
                                            if (restW > 0) bar.RelativeItem(restW);
                                        });
                                        br.ConstantItem(78).PaddingLeft(8).Text($"{t.Vend}/{t.Tot} · {p.ToString("0.0", esCo)}%").FontSize(8.5f).FontColor(QColor.FromHex("#8A8A8E"));
                                    });
                                }
                            });
                        });
                        col.Item().PaddingTop(12).LineHorizontal(1f).LineColor(QColor.FromHex("#EDEDEF"));
                        col.Item().PaddingTop(12);

                        // ── MÓDULO 02 · DETALLE DE VENTAS ──
                        col.Item().Row(mr =>
                        {
                            mr.RelativeItem().Text("02 · DETALLE DE VENTAS")
                                .FontSize(9).Bold().LetterSpacing(0.15f).FontColor(QColor.FromHex("#0077C8"));
                            mr.AutoItem().AlignRight().Text($"{ventas.Count} venta{(ventas.Count != 1 ? "s" : "")}")
                                .FontSize(8.5f).FontColor(QColor.FromHex("#B0B0B4"));
                        });
                        col.Item().PaddingTop(6).Table(tbl =>
                        {
                            tbl.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(1.8f); // Asesor
                                c.RelativeColumn(0.8f); // Apto
                                c.RelativeColumn(1.2f); // Torre
                                c.RelativeColumn(0.7f); // Tipo
                                c.RelativeColumn(0.7f); // m²
                                c.RelativeColumn(2f);   // Cliente
                                c.RelativeColumn(1.8f); // Destino
                                c.RelativeColumn(0.5f); // Lista
                                c.RelativeColumn(1.5f); // Precio
                                c.RelativeColumn(1.5f); // Fecha
                            });
                            tbl.Header(h => {
                                foreach (var hdr in new[] { "Asesor", "Apto", "Torre", "Tipo", "m²", "Cliente", "Destino", "Lista", "Precio", "Fecha y hora" })
                                    h.Cell().Background(QColor.FromHex("#003A70")).Padding(4).AlignCenter()
                                        .Text(hdr).FontSize(7).Bold().FontColor(QColors.White);
                            });

                            bool alt = false;
                            string asesorActual = "";
                            long subTotal = 0;
                            int subCont = 0;

                            foreach (var v in ventas)
                            {
                                // Subtotal por asesor
                                if (v.Asesor != asesorActual)
                                {
                                    if (asesorActual != "" && subCont > 0)
                                    {
                                        tbl.Cell().ColumnSpan(8).Background(QColor.FromHex("#E8F0FA"))
                                            .Border(0.3f).BorderColor(QColor.FromHex("#BBCCDD"))
                                            .Padding(3).AlignRight()
                                            .Text($"Subtotal {asesorActual} ({subCont} venta{(subCont > 1 ? "s": "")})").FontSize(7).Bold().FontColor(QColor.FromHex("#003A70"));
                                        tbl.Cell().ColumnSpan(2).Background(QColor.FromHex("#E8F0FA"))
                                            .Border(0.3f).BorderColor(QColor.FromHex("#BBCCDD"))
                                            .Padding(3).AlignCenter()
                                            .Text($"${subTotal:N0}").FontSize(7).Bold().FontColor(QColor.FromHex("#003A70"));
                                        alt = false;
                                    }
                                    asesorActual = v.Asesor;
                                    subTotal = 0;
                                    subCont = 0;
                                }

                                var bg = alt ? QColor.FromHex("#F5F8FC") : QColors.White;
                                tbl.Cell().Background(bg).Border(0.3f).BorderColor(QColor.FromHex("#EEEEEE")).Padding(3)
                                    .Text(v.Asesor).FontSize(7).Bold().FontColor(QColor.FromHex("#0077C8"));
                                tbl.Cell().Background(bg).Border(0.3f).BorderColor(QColor.FromHex("#EEEEEE")).Padding(3)
                                    .Text(v.Apto).FontSize(7).Bold();
                                tbl.Cell().Background(bg).Border(0.3f).BorderColor(QColor.FromHex("#EEEEEE")).Padding(3)
                                    .Text(v.Torre).FontSize(7);
                                tbl.Cell().Background(bg).Border(0.3f).BorderColor(QColor.FromHex("#EEEEEE")).Padding(3)
                                    .Text(v.Tipo).FontSize(7);
                                tbl.Cell().Background(bg).Border(0.3f).BorderColor(QColor.FromHex("#EEEEEE")).Padding(3)
                                    .Text(v.Metros).FontSize(7);
                                tbl.Cell().Background(bg).Border(0.3f).BorderColor(QColor.FromHex("#EEEEEE")).Padding(3)
                                    .Text(v.Cliente).FontSize(7);
                                tbl.Cell().Background(bg).Border(0.3f).BorderColor(QColor.FromHex("#EEEEEE")).Padding(3)
                                    .Text(v.Destino).FontSize(7).FontColor(QColor.FromHex("#555555"));
                                tbl.Cell().Background(bg).Border(0.3f).BorderColor(QColor.FromHex("#EEEEEE")).Padding(3).AlignCenter()
                                    .Text(v.Lista).FontSize(7).FontColor(QColor.FromHex("#0055A5"));
                                tbl.Cell().Background(bg).Border(0.3f).BorderColor(QColor.FromHex("#EEEEEE")).Padding(3).AlignRight()
                                    .Text($"${v.Precio:N0}").FontSize(7).Bold().FontColor(QColor.FromHex("#003A70"));
                                tbl.Cell().Background(bg).Border(0.3f).BorderColor(QColor.FromHex("#EEEEEE")).Padding(3)
                                    .Text(v.Fecha).FontSize(6.5f).FontColor(QColor.FromHex("#666666"));

                                subTotal += v.Precio;
                                subCont++;
                                alt = !alt;
                            }

                            // Último subtotal
                            if (asesorActual != "" && subCont > 0)
                            {
                                tbl.Cell().ColumnSpan(8).Background(QColor.FromHex("#E8F0FA"))
                                    .Border(0.3f).BorderColor(QColor.FromHex("#BBCCDD"))
                                    .Padding(3).AlignRight()
                                    .Text($"Subtotal {asesorActual} ({subCont} venta{(subCont > 1 ? "s": "")})").FontSize(7).Bold().FontColor(QColor.FromHex("#003A70"));
                                tbl.Cell().ColumnSpan(2).Background(QColor.FromHex("#E8F0FA"))
                                    .Border(0.3f).BorderColor(QColor.FromHex("#BBCCDD"))
                                    .Padding(3).AlignCenter()
                                    .Text($"${subTotal:N0}").FontSize(7).Bold().FontColor(QColor.FromHex("#003A70"));
                            }

                            // Gran total
                            long gran = ventas.Sum(v => v.Precio);
                            tbl.Cell().ColumnSpan(8).Background(QColor.FromHex("#003A70"))
                                .Border(0.5f).BorderColor(QColor.FromHex("#001F40"))
                                .Padding(5).AlignRight()
                                .Text($"TOTAL GENERAL — {ventas.Count} venta{(ventas.Count != 1 ? "s": "")}").FontSize(8).Bold().FontColor(QColors.White);
                            tbl.Cell().ColumnSpan(2).Background(QColor.FromHex("#003A70"))
                                .Border(0.5f).BorderColor(QColor.FromHex("#001F40"))
                                .Padding(5).AlignCenter()
                                .Text($"${gran:N0}").FontSize(8).Bold().FontColor(QColors.White);
                        });

                        // Línea de destinos
                        if (dests.Count > 0)
                        {
                            int totalDest = dests.Sum(d => d.Tot);
                            var destLinea = string.Join("   ·   ", dests.Select(d =>
                                $"{d.Dest} {(totalDest > 0 ? Math.Round((double)d.Tot / totalDest * 100, 1) : 0).ToString("0.0", esCo)}%"));
                            col.Item().PaddingTop(6).BorderBottom(1).BorderColor(QColor.FromHex("#EDEDEF")).PaddingBottom(8)
                                .Text($"Destino:   {destLinea}").FontSize(9).FontColor(QColor.FromHex("#8A8A8E"));
                        }
                        col.Item().PaddingTop(12);

                        // ── MÓDULO 03 · ASISTENCIA DEL DÍA ──
                        bool hayAsist = asist.TablaOk && asist.Dias.Count > 0;
                        int aFam = 0, aAdu = 0, aNin = 0, aCita = 0, aCar = 0, aMot = 0;
                        if (hayAsist)
                            foreach (var d in asist.Dias)
                            {
                                aFam += (int)d.Familias; aAdu += (int)d.Adultos; aNin += (int)d.Ninos;
                                aCita += (int)d.AsisteCita; aCar += (int)d.Carros; aMot += (int)d.Motos;
                            }
                        double conCita = aFam > 0 ? (double)aCita / aFam * 100 : 0;

                        col.Item().Text("03 · ASISTENCIA DEL DÍA")
                            .FontSize(9).Bold().LetterSpacing(0.15f).FontColor(QColor.FromHex("#0077C8"));
                        if (hayAsist)
                        {
                            col.Item().PaddingTop(8).Row(row =>
                            {
                                void Card(string ico, string val, string lbl, bool tint = false)
                                {
                                    row.RelativeItem().PaddingHorizontal(3).Background(QColor.FromHex(tint ? "#EAF3FB" : "#F1F1F2"))
                                        .PaddingVertical(10).Column(c =>
                                        {
                                            c.Item().AlignCenter().Width(15).Height(15).Svg(Lucide(LIco[ico], tint ? "#0055A5" : "#8A8A8E"));
                                            c.Item().AlignCenter().PaddingTop(4).Text(val).FontSize(18).Light().FontColor(QColor.FromHex(tint ? "#0055A5" : "#1A1A1A"));
                                            c.Item().AlignCenter().Text(lbl).FontSize(8).SemiBold().LetterSpacing(0.06f).FontColor(QColor.FromHex("#8A8A8E"));
                                        });
                                }
                                Card("users", aFam.ToString(), "FAMILIAS");
                                Card("user", aAdu.ToString(), "ADULTOS");
                                Card("baby", aNin.ToString(), "NIÑOS");
                                Card("calcheck", $"{conCita.ToString("0", esCo)}%", "CON CITA", true);
                                Card("car", aCar.ToString(), "CARROS");
                                Card("bike", aMot.ToString(), "MOTOS");
                            });
                        }
                        else
                        {
                            col.Item().PaddingTop(6).Border(1).BorderColor(QColor.FromHex("#E5E5E7")).Padding(9).Row(cr =>
                            {
                                cr.ConstantItem(12).Height(12).Svg(Lucide(LIco["pause"], "#B0B0B4"));
                                cr.RelativeItem().PaddingLeft(6).AlignMiddle().Text("Asistencia del día — sin cuadro cargado.").FontSize(10).FontColor(QColor.FromHex("#B0B0B4"));
                                cr.AutoItem().AlignMiddle().Text("COLAPSADO").FontSize(8).SemiBold().FontColor(QColor.FromHex("#B0B0B4"));
                            });
                        }
                        col.Item().PaddingTop(12);

                        // ── MÓDULO 04 · VENTAS POR HORA Y ÁREA ──
                        col.Item().Row(mr =>
                        {
                            mr.RelativeItem().Text("04 · VENTAS POR HORA Y ÁREA")
                                .FontSize(9).Bold().LetterSpacing(0.15f).FontColor(QColor.FromHex("#0077C8"));
                            mr.AutoItem().AlignRight().Text("Hora de Colombia")
                                .FontSize(8.5f).FontColor(QColor.FromHex("#B0B0B4"));
                        });
                        if (vhaPdf.Count > 0)
                        {
                            var porHoraP = vhaPdf.GroupBy(x => x.Hora)
                                .Select(g => new { Hora = g.Key, Num = g.Sum(x => x.Num), Valor = g.Sum(x => x.Valor),
                                                   AreaLider = g.OrderByDescending(x => x.Num).ThenByDescending(x => x.Valor).First().Area })
                                .OrderByDescending(x => x.Num).ThenByDescending(x => x.Valor).ToList();
                            var porAreaP = vhaPdf.GroupBy(x => x.Area)
                                .Select(g => new { Area = g.Key, Num = g.Sum(x => x.Num), Valor = g.Sum(x => x.Valor) })
                                .OrderByDescending(x => x.Num).ToList();
                            var comboP = vhaPdf.OrderByDescending(x => x.Num).ThenByDescending(x => x.Valor).First();
                            int maxHoraNum = porHoraP.Max(x => x.Num);

                            // Tarjetas de indicadores
                            col.Item().PaddingTop(8).Row(row =>
                            {
                                void Ind(string ico, string val, string lbl, string sub, bool tint = false)
                                {
                                    row.RelativeItem().PaddingHorizontal(3).Background(QColor.FromHex(tint ? "#EAF3FB" : "#F1F1F2"))
                                        .PaddingVertical(10).PaddingHorizontal(10).Column(c =>
                                        {
                                            c.Item().Row(rr =>
                                            {
                                                rr.ConstantItem(14).Height(14).Svg(Lucide(LIco[ico], tint ? "#0055A5" : "#8A8A8E"));
                                                rr.RelativeItem().PaddingLeft(5).AlignMiddle().Text(lbl)
                                                    .FontSize(7.5f).SemiBold().LetterSpacing(0.05f).FontColor(QColor.FromHex("#8A8A8E"));
                                            });
                                            c.Item().PaddingTop(5).Text(val).FontSize(15).Light().FontColor(QColor.FromHex(tint ? "#0055A5" : "#1A1A1A"));
                                            c.Item().PaddingTop(2).Text(sub).FontSize(8).FontColor(QColor.FromHex("#8A8A8E"));
                                        });
                                }
                                var hp = porHoraP.OrderByDescending(x => x.Num).ThenByDescending(x => x.Valor).First();
                                Ind("clock", FmtHora(hp.Hora), "HORA PICO", $"{hp.Num} venta{(hp.Num != 1 ? "s" : "")} · {MoneyM(hp.Valor)}", true);
                                Ind("tag", $"{porAreaP[0].Area} m²", "ÁREA LÍDER", $"{porAreaP[0].Num} venta{(porAreaP[0].Num != 1 ? "s" : "")} en total");
                                Ind("chart", $"{comboP.Area} m² · {FmtHora(comboP.Hora)}", "MEJOR COMBINACIÓN", $"{comboP.Num} venta{(comboP.Num != 1 ? "s" : "")} en esa franja");
                            });

                            // Tabla por hora (barra proporcional + área líder)
                            col.Item().PaddingTop(10).Table(tbl =>
                            {
                                tbl.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn(1.4f); // Hora
                                    c.RelativeColumn(0.8f); // N.º
                                    c.RelativeColumn(3.2f); // Barra
                                    c.RelativeColumn(1.6f); // Valor
                                    c.RelativeColumn(1.2f); // Área líder
                                });
                                tbl.Header(h =>
                                {
                                    foreach (var hdr in new[] { "Hora", "Ventas", "Distribución", "Valor", "Área líder" })
                                        h.Cell().Background(QColor.FromHex("#003A70")).Padding(4)
                                            .Text(hdr).FontSize(7).Bold().FontColor(QColors.White);
                                });
                                bool altH = false;
                                foreach (var ph in porHoraP.OrderBy(x => x.Hora))
                                {
                                    var bg = altH ? QColor.FromHex("#F5F8FC") : QColors.White;
                                    altH = !altH;
                                    int fillW = (int)Math.Max(Math.Round((double)ph.Num / maxHoraNum * 100), 4);
                                    int restW = Math.Max(100 - fillW, 0);
                                    tbl.Cell().Background(bg).Border(0.3f).BorderColor(QColor.FromHex("#EEEEEE")).Padding(3)
                                        .Text(FmtHora(ph.Hora)).FontSize(7.5f).SemiBold().FontColor(QColor.FromHex("#1A1A1A"));
                                    tbl.Cell().Background(bg).Border(0.3f).BorderColor(QColor.FromHex("#EEEEEE")).Padding(3).AlignCenter()
                                        .Text(ph.Num.ToString()).FontSize(7.5f).Bold().FontColor(QColor.FromHex("#0077C8"));
                                    tbl.Cell().Background(bg).Border(0.3f).BorderColor(QColor.FromHex("#EEEEEE")).Padding(3)
                                        .Height(12).Background(QColor.FromHex("#EEEFF1")).Row(bar =>
                                        {
                                            bar.RelativeItem(fillW).Background(QColor.FromHex("#0077C8"));
                                            if (restW > 0) bar.RelativeItem(restW);
                                        });
                                    tbl.Cell().Background(bg).Border(0.3f).BorderColor(QColor.FromHex("#EEEEEE")).Padding(3).AlignRight()
                                        .Text(Money(ph.Valor)).FontSize(7).FontColor(QColor.FromHex("#003A70"));
                                    tbl.Cell().Background(bg).Border(0.3f).BorderColor(QColor.FromHex("#EEEEEE")).Padding(3).AlignCenter()
                                        .Text($"{ph.AreaLider} m²").FontSize(7).FontColor(QColor.FromHex("#555555"));
                                }
                            });
                        }
                        else
                        {
                            col.Item().PaddingTop(6).Border(1).BorderColor(QColor.FromHex("#E5E5E7")).Padding(9).Row(cr =>
                            {
                                cr.ConstantItem(12).Height(12).Svg(Lucide(LIco["pause"], "#B0B0B4"));
                                cr.RelativeItem().PaddingLeft(6).AlignMiddle().Text("Ventas por hora — sin ventas registradas.").FontSize(10).FontColor(QColor.FromHex("#B0B0B4"));
                                cr.AutoItem().AlignMiddle().Text("SIN DATOS").FontSize(8).SemiBold().FontColor(QColor.FromHex("#B0B0B4"));
                            });
                        }
                        col.Item().PaddingTop(12);

                        // ── MÓDULOS 05–06 · COLAPSADOS ──
                        void Colapsado(string titulo)
                        {
                            col.Item().PaddingBottom(6).Border(1).BorderColor(QColor.FromHex("#E5E5E7")).Padding(9).Row(cr =>
                            {
                                cr.ConstantItem(12).Height(12).Svg(Lucide(LIco["pause"], "#B0B0B4"));
                                cr.RelativeItem().PaddingLeft(6).AlignMiddle().Text(titulo).FontSize(10).FontColor(QColor.FromHex("#B0B0B4"));
                                cr.AutoItem().AlignMiddle().Text("COLAPSADO").FontSize(8).SemiBold().FontColor(QColor.FromHex("#B0B0B4"));
                            });
                        }
                        Colapsado("05 · Preventas — sin registros para el periodo.");
                        if (enProceso == 0)
                            Colapsado("06 · Opciones en proceso — sin registros.");
                        else
                            col.Item().PaddingBottom(6).Text($"06 · OPCIONES EN PROCESO — {enProceso} unidad{(enProceso != 1 ? "es" : "")}")
                                .FontSize(9).Bold().LetterSpacing(0.15f).FontColor(QColor.FromHex("#0077C8"));
                    });

                    // ── FOOTER ──
                    page.Footer().Column(fc =>
                    {
                        fc.Item().PaddingTop(6).LineHorizontal(1f).LineColor(QColor.FromHex("#EDEDEF"));
                        fc.Item().PaddingTop(5).Row(row =>
                        {
                            row.RelativeItem().Text("Londoño Gómez · Sistema de Lanzamientos · Los módulos sin datos se resumen en una línea")
                                .FontSize(8.5f).FontColor(QColor.FromHex("#B0B0B4"));
                            row.AutoItem().AlignRight().Text(x =>
                            {
                                x.Span($"{proyNombre} · {ahoraCol:dd/MM/yyyy HH:mm} · Pág. ").FontSize(8.5f).FontColor(QColor.FromHex("#B0B0B4"));
                                x.CurrentPageNumber().FontSize(8.5f).FontColor(QColor.FromHex("#8A8A8E"));
                                x.Span("/").FontSize(8.5f).FontColor(QColor.FromHex("#B0B0B4"));
                                x.TotalPages().FontSize(8.5f).FontColor(QColor.FromHex("#8A8A8E"));
                            });
                        });
                    });
                });

                // ── PÁGINA ASISTENCIA (si el cuadro fue guardado) ──
                if (asist.TablaOk && asist.Dias.Count > 0)
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4.Landscape());
                        page.Margin(28);
                        page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(8));

                        page.Header().Column(col =>
                        {
                            col.Item().Row(row =>
                            {
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().Text("LONDOÑO GÓMEZ").FontSize(14).Bold().FontColor(QColor.FromHex("#003A70"));
                                    c.Item().Text("Sistema de Lanzamientos Inmobiliarios").FontSize(8).FontColor(QColor.FromHex("#666666"));
                                });
                                row.ConstantItem(160).AlignRight().Column(c =>
                                {
                                    c.Item().Text("CUADRO DE ASISTENCIA").FontSize(9).Bold().FontColor(QColor.FromHex("#003A70"));
                                    c.Item().Text($"Proyecto: {proyNombre}").FontSize(7.5f).FontColor(QColor.FromHex("#555555"));
                                    c.Item().Text($"Generado: {ahoraCol:dd/MM/yyyy HH:mm}").FontSize(7).FontColor(QColor.FromHex("#999999"));
                                });
                            });
                            col.Item().PaddingTop(4).LineHorizontal(2f).LineColor(QColor.FromHex("#003A70"));
                            col.Item().PaddingTop(2).LineHorizontal(0.5f).LineColor(QColor.FromHex("#0077C8"));
                        });

                        page.Content().PaddingTop(12).Column(col =>
                        {
                            if (!string.IsNullOrWhiteSpace(asist.Titulo))
                                col.Item().PaddingBottom(6).Text(asist.Titulo).FontSize(10).Bold().FontColor(QColor.FromHex("#003A70"));

                            var dias = asist.Dias;
                            int nDias = dias.Count;
                            int totalCols = 2 + nDias; // label + N días + TOTAL

                            col.Item().Table(tbl =>
                            {
                                tbl.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn(3f); // label
                                    for (int d = 0; d < nDias; d++) c.RelativeColumn(1.2f);
                                    c.RelativeColumn(1.4f); // TOTAL
                                });

                                // Header
                                tbl.Header(h =>
                                {
                                    h.Cell().Background(QColor.FromHex("#003A70")).Padding(4).Text("").FontColor(QColors.White);
                                    foreach (var dia in dias)
                                        h.Cell().Background(QColor.FromHex("#003A70")).Padding(4).AlignCenter()
                                            .Text((string)dia.NombreDia).FontSize(7.5f).Bold().FontColor(QColors.White);
                                    h.Cell().Background(QColor.FromHex("#003A70")).Padding(4).AlignCenter()
                                        .Text("TOTAL").FontSize(7.5f).Bold().FontColor(QColors.White);
                                });

                                bool alt = false;
                                QColor Bg() { alt = !alt; return alt ? QColor.FromHex("#F5F8FC") : QColors.White; }

                                void FilaInt(string label, Func<dynamic, int> sel)
                                {
                                    var bg = Bg();
                                    tbl.Cell().Background(bg).Border(0.3f).BorderColor(QColor.FromHex("#EEEEEE")).Padding(3)
                                        .Text(label).FontSize(7.5f).Bold().FontColor(QColor.FromHex("#003A70"));
                                    int tot = 0;
                                    foreach (var dia in dias)
                                    {
                                        int v = sel(dia); tot += v;
                                        tbl.Cell().Background(bg).Border(0.3f).BorderColor(QColor.FromHex("#EEEEEE")).Padding(3)
                                            .AlignCenter().Text(v.ToString()).FontSize(7.5f);
                                    }
                                    tbl.Cell().Background(QColor.FromHex("#E8F0FA")).Border(0.3f).BorderColor(QColor.FromHex("#BBCCDD"))
                                        .Padding(3).AlignCenter().Text(tot.ToString()).FontSize(7.5f).Bold().FontColor(QColor.FromHex("#003A70"));
                                }

                                void FilaPct(string label, Func<dynamic, double> sel, double totPct)
                                {
                                    var bg = Bg();
                                    tbl.Cell().Background(bg).Border(0.3f).BorderColor(QColor.FromHex("#EEEEEE")).Padding(3)
                                        .Text(label).FontSize(7.5f).Bold().FontColor(QColor.FromHex("#003A70"));
                                    foreach (var dia in dias)
                                        tbl.Cell().Background(bg).Border(0.3f).BorderColor(QColor.FromHex("#EEEEEE")).Padding(3)
                                            .AlignCenter().Text($"{sel(dia):P0}").FontSize(7.5f);
                                    tbl.Cell().Background(QColor.FromHex("#E8F0FA")).Border(0.3f).BorderColor(QColor.FromHex("#BBCCDD"))
                                        .Padding(3).AlignCenter().Text($"{totPct:P0}").FontSize(7.5f).Bold().FontColor(QColor.FromHex("#003A70"));
                                }

                                void FilaTorreInt(string label, Func<dynamic, long> sel)
                                {
                                    var bg = Bg();
                                    tbl.Cell().Background(bg).Border(0.3f).BorderColor(QColor.FromHex("#EEEEEE")).Padding(3)
                                        .Text(label).FontSize(7.5f).Bold().FontColor(QColor.FromHex("#003A70"));
                                    long tot = 0;
                                    foreach (var dia in dias)
                                    {
                                        long v = 0; foreach (var t in (List<dynamic>)dia.Torres) v += sel(t);
                                        tot += v;
                                        tbl.Cell().Background(bg).Border(0.3f).BorderColor(QColor.FromHex("#EEEEEE")).Padding(3)
                                            .AlignCenter().Text(v.ToString()).FontSize(7.5f);
                                    }
                                    tbl.Cell().Background(QColor.FromHex("#E8F0FA")).Border(0.3f).BorderColor(QColor.FromHex("#BBCCDD"))
                                        .Padding(3).AlignCenter().Text(tot.ToString()).FontSize(7.5f).Bold().FontColor(QColor.FromHex("#003A70"));
                                }

                                void FilaTorreMoney(string label, Func<dynamic, long> sel)
                                {
                                    var bg = Bg();
                                    tbl.Cell().Background(bg).Border(0.3f).BorderColor(QColor.FromHex("#EEEEEE")).Padding(3)
                                        .Text(label).FontSize(7.5f).Bold().FontColor(QColor.FromHex("#003A70"));
                                    long tot = 0;
                                    foreach (var dia in dias)
                                    {
                                        long v = 0; foreach (var t in (List<dynamic>)dia.Torres) v += sel(t);
                                        tot += v;
                                        tbl.Cell().Background(bg).Border(0.3f).BorderColor(QColor.FromHex("#EEEEEE")).Padding(3)
                                            .AlignRight().Text($"${v:N0}").FontSize(7.5f);
                                    }
                                    tbl.Cell().Background(QColor.FromHex("#E8F0FA")).Border(0.3f).BorderColor(QColor.FromHex("#BBCCDD"))
                                        .Padding(3).AlignRight().Text($"${tot:N0}").FontSize(7.5f).Bold().FontColor(QColor.FromHex("#003A70"));
                                }

                                // Sección tráfico
                                int sumFam = dias.Sum(d => (int)d.Familias);
                                int sumAsisteCita = dias.Sum(d => (int)d.AsisteCita);
                                int sumAgendLucia = dias.Sum(d => (int)d.AgendadosLucia);
                                int sumAsisteLucia = dias.Sum(d => (int)d.AsisteCitaLucia);
                                int sumAgendEquipo = dias.Sum(d => (int)d.AgendadosEquipo);

                                // Sección header tráfico
                                tbl.Cell().ColumnSpan((uint)totalCols).Background(QColor.FromHex("#EEF4FA"))
                                    .Border(0.5f).BorderColor(QColor.FromHex("#BBCCDD")).Padding(4)
                                    .Text("TRÁFICO").FontSize(8).Bold().FontColor(QColor.FromHex("#003A70"));

                                FilaInt("Familias", d => (int)d.Familias);
                                FilaInt("Adultos", d => (int)d.Adultos);
                                FilaInt("Niños", d => (int)d.Ninos);
                                FilaInt("Mascotas", d => (int)d.Mascotas);
                                FilaInt("Asiste con cita", d => (int)d.AsisteCita);
                                FilaPct("% asiste con cita", d => (int)d.Familias > 0 ? (double)(int)d.AsisteCita / (int)d.Familias : 0,
                                    sumFam > 0 ? (double)sumAsisteCita / sumFam : 0);
                                FilaInt("Carros", d => (int)d.Carros);
                                FilaInt("Motos", d => (int)d.Motos);
                                FilaInt("Caminando", d => (int)d.Caminando);

                                // Sección torres
                                tbl.Cell().ColumnSpan((uint)totalCols).Background(QColor.FromHex("#EEF4FA"))
                                    .Border(0.5f).BorderColor(QColor.FromHex("#BBCCDD")).Padding(4)
                                    .Text("TORRES / ETAPAS").FontSize(8).Bold().FontColor(QColor.FromHex("#003A70"));

                                FilaTorreInt("Preventas", t => (long)(int)t.Preventas);
                                FilaTorreMoney("Valor preventa", t => (long)t.ValorPreventa);
                                FilaTorreInt("Ventas", t => (long)(int)t.Ventas);
                                FilaTorreMoney("Valor de venta", t => (long)t.ValorVenta);
                                FilaTorreInt("Opciones (En proceso)", t => (long)(int)t.Opciones);
                                FilaTorreMoney("Opciones (En pesos)", t => (long)t.ValorOpciones);
                                FilaTorreInt("Ventas totales unidades", t => (long)((int)t.Preventas + (int)t.Ventas));
                                FilaTorreMoney("Ventas totales pesos", t => (long)t.ValorPreventa + (long)t.ValorVenta);
                                FilaTorreInt("Opciones + ventas", t => (long)((int)t.Preventas + (int)t.Ventas + (int)t.Opciones));
                                FilaTorreMoney("Opciones + ventas (pesos)", t => (long)t.ValorPreventa + (long)t.ValorVenta + (long)t.ValorOpciones);

                                // Sección citas
                                tbl.Cell().ColumnSpan((uint)totalCols).Background(QColor.FromHex("#EEF4FA"))
                                    .Border(0.5f).BorderColor(QColor.FromHex("#BBCCDD")).Padding(4)
                                    .Text("CITAS").FontSize(8).Bold().FontColor(QColor.FromHex("#003A70"));

                                FilaInt("Agendados equipo comercial", d => (int)d.AgendadosEquipo);
                                FilaInt("Agendados por Lucía", d => (int)d.AgendadosLucia);
                                FilaInt("Asiste con cita Lucía", d => (int)d.AsisteCitaLucia);
                                FilaPct("% asistencia Lucía",
                                    d => (int)d.AgendadosLucia > 0 ? (double)(int)d.AsisteCitaLucia / (int)d.AgendadosLucia : 0,
                                    sumAgendLucia > 0 ? (double)sumAsisteLucia / sumAgendLucia : 0);
                                FilaInt("Total agendados", d => (int)d.AgendadosEquipo + (int)d.AgendadosLucia);
                                FilaPct("% cumplimiento cita",
                                    d => ((int)d.AgendadosEquipo + (int)d.AgendadosLucia) > 0 ? (double)(int)d.AsisteCitaLucia / ((int)d.AgendadosEquipo + (int)d.AgendadosLucia) : 0,
                                    (sumAgendEquipo + sumAgendLucia) > 0 ? (double)sumAsisteLucia / (sumAgendEquipo + sumAgendLucia) : 0);
                                FilaPct("Ventas vs familias",
                                    d => (int)d.Familias > 0 ? (double)((List<dynamic>)d.Torres).Sum(t => (int)t.Ventas) / (int)d.Familias : 0,
                                    sumFam > 0 ? (double)dias.SelectMany(d => (List<dynamic>)d.Torres).Sum(t => (int)t.Ventas) / sumFam : 0);
                            });

                            if (!string.IsNullOrWhiteSpace(asist.Observaciones))
                            {
                                col.Item().PaddingTop(10).Text("Observaciones").FontSize(8).Bold().FontColor(QColor.FromHex("#003A70"));
                                col.Item().PaddingTop(4).Background(QColor.FromHex("#F8FAFD"))
                                    .Border(0.5f).BorderColor(QColor.FromHex("#DDDDDD"))
                                    .Padding(8).Text(asist.Observaciones).FontSize(7.5f).FontColor(QColor.FromHex("#333333"));
                            }
                        });

                        page.Footer().PaddingTop(8).Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().LineHorizontal(0.5f).LineColor(QColor.FromHex("#CCCCCC"));
                                c.Item().PaddingTop(3).Text($"Londoño Gómez  ·  {proyNombre}  ·  Cuadro de asistencia  ·  {ahoraCol:dd/MM/yyyy HH:mm}")
                                    .FontSize(7).FontColor(QColor.FromHex("#999999"));
                            });
                            row.ConstantItem(60).AlignRight().Column(c =>
                            {
                                c.Item().LineHorizontal(0.5f).LineColor(QColor.FromHex("#CCCCCC"));
                                c.Item().PaddingTop(3).AlignRight().Text(x =>
                                {
                                    x.Span("Pág. ").FontSize(7).FontColor(QColor.FromHex("#999999"));
                                    x.CurrentPageNumber().FontSize(7).Bold().FontColor(QColor.FromHex("#003A70"));
                                    x.Span(" / ").FontSize(7).FontColor(QColor.FromHex("#999999"));
                                    x.TotalPages().FontSize(7).Bold().FontColor(QColor.FromHex("#003A70"));
                                });
                            });
                        });
                    });
                }

            }).GeneratePdf();

            return File(pdfBytes, "application/pdf",
                $"Informe_Tecnico_{proyNombre.Replace(" ", "_")}_{ahoraCol:yyyyMMdd_HHmm}.pdf");
        }

        /// <summary>
        /// Generates a colour-coded Excel workbook of the property map. The first sheet
        /// ("Mapa general") is a classified report of every area together (global summary
        /// plus a per-area breakdown by estado). Then one sheet per loaded area (Metros)
        /// shows ALL of its apartments laid out by torre/piso, colour-coded by estado.
        /// Performs a SELECT query for all properties in the active project.
        /// </summary>
        public async Task<IActionResult> GenerarMapa()
        {
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;
            var proyNombre = HttpContext.Session.GetString("ProyectoNombre") ?? "Proyecto";

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            var inmuebles = new List<dynamic>();
            var cmd = new SqlCommand("SELECT Apto,Piso,Torre,Tipo,Metros,Estado FROM Inmuebles WHERE IdProyecto=@id ORDER BY Metros,Torre,Piso DESC,Apto", con);
            cmd.Parameters.AddWithValue("@id", idProy);
            using (var reader = (SqlDataReader)await cmd.ExecuteReaderAsync())
                while (await reader.ReadAsync())
                    inmuebles.Add(new { Apto = reader["Apto"]?.ToString() ?? "", Piso = reader["Piso"]?.ToString() ?? "", Torre = reader["Torre"]?.ToString() ?? "", Tipo = reader["Tipo"]?.ToString() ?? "", Metros = reader["Metros"]?.ToString() ?? "", Estado = reader["Estado"]?.ToString() ?? "" });

            ExcelPackage.License.SetNonCommercialPersonal("Londoño Gómez");
            using var package = new ExcelPackage();

            // ── Helpers locales ──
            DColor EstadoColor(string e) => e switch
            {
                "VENDIDO" => DColor.FromArgb(230, 57, 70),
                "RESERVADO" => DColor.FromArgb(255, 149, 0),
                "EN PROCESO" => DColor.FromArgb(90, 90, 200),
                _ => DColor.FromArgb(52, 199, 89)
            };
            DColor EstadoTinte(string e) => e switch
            {
                "VENDIDO" => DColor.FromArgb(250, 224, 227),
                "RESERVADO" => DColor.FromArgb(255, 238, 214),
                "EN PROCESO" => DColor.FromArgb(228, 228, 247),
                _ => DColor.FromArgb(223, 246, 230)
            };
            int PisoNum(string p) { int.TryParse(p, out int n); return n; }
            double AreaNum(string m)
            {
                double.TryParse((m ?? "").Replace(",", "."), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double d);
                return d;
            }
            var bordeGris = DColor.FromArgb(200, 200, 200);
            void Borde(ExcelRange c) => c.Style.Border.BorderAround(ExcelBorderStyle.Thin, bordeGris);

            var usados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string NombreHoja(string baseName)
            {
                var invalid = new[] { '\\', '/', '?', '*', '[', ']', ':' };
                var clean = new string((baseName ?? "").Select(c => invalid.Contains(c) ? ' ' : c).ToArray()).Trim();
                if (clean.Length > 31) clean = clean.Substring(0, 31);
                if (string.IsNullOrWhiteSpace(clean)) clean = "Hoja";
                var final = clean; int k = 1;
                while (usados.Contains(final))
                {
                    var suf = " (" + (++k) + ")";
                    final = clean.Substring(0, Math.Min(clean.Length, 31 - suf.Length)) + suf;
                }
                usados.Add(final);
                return final;
            }

            string[] estados = { "DISPONIBLE", "VENDIDO", "RESERVADO", "EN PROCESO" };
            var areas = inmuebles.Select(i => (string)i.Metros).Distinct().OrderBy(AreaNum).ToList();

            // ════════════════════ HOJA 1 · MAPA GENERAL ════════════════════
            var wsMain = package.Workbook.Worksheets.Add(NombreHoja("Mapa general"));
            int r = 1;
            wsMain.Cells[r, 1].Value = $"{proyNombre} — Mapa general de inmuebles";
            wsMain.Cells[r, 1].Style.Font.Bold = true;
            wsMain.Cells[r, 1].Style.Font.Size = 15;
            wsMain.Cells[r, 1].Style.Font.Color.SetColor(DColor.FromArgb(0, 58, 112));
            wsMain.Cells[r, 1, r, 8].Merge = true;
            r++;
            wsMain.Cells[r, 1].Value = $"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}  ·  {inmuebles.Count} inmuebles  ·  {areas.Count} áreas";
            wsMain.Cells[r, 1].Style.Font.Color.SetColor(DColor.FromArgb(110, 110, 110));
            wsMain.Cells[r, 1, r, 8].Merge = true;
            r += 2;

            // Resumen global por estado (cajas de color)
            for (int e = 0; e < estados.Length; e++)
            {
                int cnt = inmuebles.Count(i => i.Estado == estados[e]);
                var box = wsMain.Cells[r, 1 + e * 2, r, 2 + e * 2];
                box.Merge = true;
                box.Value = $"{estados[e]}: {cnt}";
                box.Style.Fill.PatternType = ExcelFillStyle.Solid;
                box.Style.Fill.BackgroundColor.SetColor(EstadoColor(estados[e]));
                box.Style.Font.Color.SetColor(DColor.White);
                box.Style.Font.Bold = true;
                box.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            }
            r += 2;

            // Tabla clasificada por área
            var headersMain = new[] { "Área m²", "Tipos", "Total", "Disponibles", "Vendidos", "Reservados", "En proceso", "% Vendido" };
            for (int i = 0; i < headersMain.Length; i++) { wsMain.Cells[r, i + 1].Value = headersMain[i]; StyleHeader(wsMain.Cells[r, i + 1]); }
            // Tintar encabezados de estado con su color
            wsMain.Cells[r, 4].Style.Fill.BackgroundColor.SetColor(EstadoColor("DISPONIBLE"));
            wsMain.Cells[r, 5].Style.Fill.BackgroundColor.SetColor(EstadoColor("VENDIDO"));
            wsMain.Cells[r, 6].Style.Fill.BackgroundColor.SetColor(EstadoColor("RESERVADO"));
            wsMain.Cells[r, 7].Style.Fill.BackgroundColor.SetColor(EstadoColor("EN PROCESO"));
            r++;

            foreach (var metros in areas)
            {
                var inmsA = inmuebles.Where(i => i.Metros == metros).ToList();
                var tipos = string.Join(", ", inmsA.Select(i => (string)i.Tipo).Where(t => !string.IsNullOrEmpty(t)).Distinct().OrderBy(t => t));
                int tot = inmsA.Count;
                int disp = inmsA.Count(i => i.Estado == "DISPONIBLE");
                int vend = inmsA.Count(i => i.Estado == "VENDIDO");
                int res = inmsA.Count(i => i.Estado == "RESERVADO");
                int proc = inmsA.Count(i => i.Estado == "EN PROCESO");
                int pctV = tot > 0 ? (int)Math.Round((double)vend / tot * 100) : 0;

                wsMain.Cells[r, 1].Value = metros; wsMain.Cells[r, 1].Style.Font.Bold = true; Borde(wsMain.Cells[r, 1]);
                wsMain.Cells[r, 2].Value = tipos; Borde(wsMain.Cells[r, 2]);
                wsMain.Cells[r, 3].Value = tot; wsMain.Cells[r, 3].Style.Font.Bold = true; Borde(wsMain.Cells[r, 3]);
                var celdas = new[] { (4, disp, "DISPONIBLE"), (5, vend, "VENDIDO"), (6, res, "RESERVADO"), (7, proc, "EN PROCESO") };
                foreach (var (colE, val, estE) in celdas)
                {
                    var c = wsMain.Cells[r, colE];
                    c.Value = val;
                    c.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    c.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    c.Style.Fill.BackgroundColor.SetColor(EstadoTinte(estE));
                    Borde(c);
                }
                wsMain.Cells[r, 8].Value = $"{pctV}%"; wsMain.Cells[r, 8].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center; Borde(wsMain.Cells[r, 8]);
                r++;
            }
            // Fila de totales
            wsMain.Cells[r, 1].Value = "TOTAL"; wsMain.Cells[r, 1].Style.Font.Bold = true;
            wsMain.Cells[r, 2].Value = "";
            wsMain.Cells[r, 3].Value = inmuebles.Count; wsMain.Cells[r, 3].Style.Font.Bold = true;
            wsMain.Cells[r, 4].Value = inmuebles.Count(i => i.Estado == "DISPONIBLE");
            wsMain.Cells[r, 5].Value = inmuebles.Count(i => i.Estado == "VENDIDO");
            wsMain.Cells[r, 6].Value = inmuebles.Count(i => i.Estado == "RESERVADO");
            wsMain.Cells[r, 7].Value = inmuebles.Count(i => i.Estado == "EN PROCESO");
            int pctVTot = inmuebles.Count > 0 ? (int)Math.Round((double)inmuebles.Count(i => i.Estado == "VENDIDO") / inmuebles.Count * 100) : 0;
            wsMain.Cells[r, 8].Value = $"{pctVTot}%";
            for (int c = 1; c <= 8; c++)
            {
                wsMain.Cells[r, c].Style.Font.Bold = true;
                wsMain.Cells[r, c].Style.Fill.PatternType = ExcelFillStyle.Solid;
                wsMain.Cells[r, c].Style.Fill.BackgroundColor.SetColor(DColor.FromArgb(232, 240, 248));
                wsMain.Cells[r, c].Style.HorizontalAlignment = c >= 3 ? ExcelHorizontalAlignment.Center : ExcelHorizontalAlignment.Left;
                Borde(wsMain.Cells[r, c]);
            }
            r += 2;

            // Leyenda
            wsMain.Cells[r, 1].Value = "LEYENDA"; wsMain.Cells[r, 1].Style.Font.Bold = true; r++;
            foreach (var est in estados)
            {
                var c = wsMain.Cells[r, 1];
                c.Value = est;
                c.Style.Fill.PatternType = ExcelFillStyle.Solid;
                c.Style.Fill.BackgroundColor.SetColor(EstadoColor(est));
                c.Style.Font.Color.SetColor(DColor.White);
                c.Style.Font.Bold = true;
                c.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                r++;
            }
            for (int col = 1; col <= 8; col++) wsMain.Column(col).AutoFit();
            wsMain.Column(2).Width = Math.Max(wsMain.Column(2).Width, 16);

            // ════════════════════ UNA HOJA POR ÁREA ════════════════════
            foreach (var metros in areas)
            {
                var inmsA = inmuebles.Where(i => i.Metros == metros).ToList();
                var ws = package.Workbook.Worksheets.Add(NombreHoja($"Área {metros}"));
                int ar = 1;

                ws.Cells[ar, 1].Value = $"{proyNombre} — Área {metros} m²";
                ws.Cells[ar, 1].Style.Font.Bold = true;
                ws.Cells[ar, 1].Style.Font.Size = 14;
                ws.Cells[ar, 1].Style.Font.Color.SetColor(DColor.FromArgb(0, 58, 112));
                ws.Cells[ar, 1, ar, 8].Merge = true;
                ar++;
                ws.Cells[ar, 1].Value = $"{inmsA.Count} inmuebles  ·  Disponibles: {inmsA.Count(i => i.Estado == "DISPONIBLE")}  ·  Vendidos: {inmsA.Count(i => i.Estado == "VENDIDO")}  ·  Reservados: {inmsA.Count(i => i.Estado == "RESERVADO")}  ·  En proceso: {inmsA.Count(i => i.Estado == "EN PROCESO")}";
                ws.Cells[ar, 1].Style.Font.Color.SetColor(DColor.FromArgb(110, 110, 110));
                ws.Cells[ar, 1, ar, 8].Merge = true;
                ar += 2;

                var torres = inmsA.Select(i => (string)i.Torre).Distinct().OrderBy(t => t).ToList();
                foreach (var torre in torres)
                {
                    var inmsT = inmsA.Where(i => i.Torre == torre).ToList();
                    if (inmsT.Count == 0) continue;
                    var pisos = inmsT.Select(i => (string)i.Piso).Distinct().OrderByDescending(PisoNum).ToList();
                    int maxU = pisos.Max(p => inmsT.Count(i => (string)i.Piso == p));
                    int anchoBloque = 1 + maxU;

                    ws.Cells[ar, 1].Value = string.IsNullOrEmpty(torre) ? "Torre única" : $"Torre {torre}";
                    ws.Cells[ar, 1].Style.Font.Bold = true;
                    ws.Cells[ar, 1].Style.Font.Size = 12;
                    ws.Cells[ar, 1].Style.Font.Color.SetColor(DColor.FromArgb(0, 58, 112));
                    ws.Cells[ar, 1, ar, anchoBloque].Merge = true;
                    ar++;

                    ws.Cells[ar, 1].Value = "Piso"; StyleHeader(ws.Cells[ar, 1]);
                    var hUnid = ws.Cells[ar, 2, ar, anchoBloque];
                    hUnid.Merge = true; hUnid.Value = "Unidades"; StyleHeader(hUnid);
                    ar++;

                    foreach (var piso in pisos)
                    {
                        ws.Cells[ar, 1].Value = piso;
                        ws.Cells[ar, 1].Style.Font.Bold = true;
                        ws.Cells[ar, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        Borde(ws.Cells[ar, 1]);

                        var unidades = inmsT.Where(i => (string)i.Piso == piso).OrderBy(i => (string)i.Apto).ToList();
                        for (int u = 0; u < maxU; u++)
                        {
                            var cell = ws.Cells[ar, 2 + u];
                            if (u < unidades.Count)
                            {
                                string estado = unidades[u].Estado;
                                cell.Value = (string)unidades[u].Apto;
                                cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                                cell.Style.Fill.BackgroundColor.SetColor(EstadoColor(estado));
                                cell.Style.Font.Bold = true;
                                cell.Style.Font.Color.SetColor(DColor.White);
                            }
                            else
                            {
                                cell.Value = "—";
                                cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                                cell.Style.Font.Color.SetColor(DColor.LightGray);
                            }
                            Borde(cell);
                        }
                        ar++;
                    }
                    ar++; // espacio entre torres
                }

                // Leyenda por hoja de área
                ws.Cells[ar, 1].Value = "LEYENDA"; ws.Cells[ar, 1].Style.Font.Bold = true; ar++;
                foreach (var est in estados)
                {
                    var c = ws.Cells[ar, 1];
                    c.Value = est;
                    c.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    c.Style.Fill.BackgroundColor.SetColor(EstadoColor(est));
                    c.Style.Font.Color.SetColor(DColor.White);
                    c.Style.Font.Bold = true;
                    c.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    ar++;
                }

                if (ws.Dimension != null)
                    for (int col = 1; col <= ws.Dimension.End.Column; col++) ws.Column(col).AutoFit();
                ws.Column(1).Width = 8;
            }

            return File(package.GetAsByteArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Mapa_Ventas_{proyNombre.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        /// <summary>
        /// Generates an Excel workbook with a per-asesor sale breakdown including
        /// subtotals per asesor and a grand total row.
        /// Performs a SELECT query joining Ventas, Inmuebles, Clientes, and Usuarios.
        /// </summary>
        public async Task<IActionResult> ReporteAsesores()
        {
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;
            var proyNombre = HttpContext.Session.GetString("ProyectoNombre") ?? "Proyecto";

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            var ventas = new List<dynamic>();
            var cmd = new SqlCommand(@"
                SELECT u.Nombre+' '+u.Apellido AS Asesor,
                       i.Apto, i.Torre, i.Tipo, i.Piso, i.Metros,
                       c.Nombre+' '+c.Apellido AS Cliente, c.Documento, c.Celular,
                       ISNULL(v.Destino,'—') AS Destino,
                       v.ListaAplicada, v.PrecioVenta,
                       FORMAT(v.FechaVenta,'dd/MM/yyyy HH:mm') AS FechaVenta
                FROM Ventas v
                JOIN Inmuebles i ON v.IdInmueble=i.IdInmuebles
                JOIN Clientes  c ON v.IdCliente=c.IdCliente
                JOIN Usuarios  u ON v.IdUsuario=u.IdUsuario
                WHERE v.IdProyecto=@id AND v.Estado='ACTIVA'
                ORDER BY u.Nombre, v.FechaVenta DESC", con);
            cmd.Parameters.AddWithValue("@id", idProy);
            using (var r = (SqlDataReader)await cmd.ExecuteReaderAsync())
                while (await r.ReadAsync())
                    ventas.Add(new { Asesor = r["Asesor"]?.ToString() ?? "", Apto = r["Apto"]?.ToString() ?? "", Torre = r["Torre"]?.ToString() ?? "", Tipo = r["Tipo"]?.ToString() ?? "", Piso = r["Piso"]?.ToString() ?? "", Metros = r["Metros"]?.ToString() ?? "", Cliente = r["Cliente"]?.ToString() ?? "", Documento = r["Documento"]?.ToString() ?? "", Celular = r["Celular"]?.ToString() ?? "", Destino = r["Destino"]?.ToString() ?? "—", Lista = r["ListaAplicada"]?.ToString() ?? "", PrecioVenta = (long)r["PrecioVenta"], FechaVenta = r["FechaVenta"]?.ToString() ?? "" });

            ExcelPackage.License.SetNonCommercialPersonal("Londoño Gómez");
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Ventas por asesor");

            ws.Cells[1, 1].Value = $"Reporte de ventas — {proyNombre}"; ws.Cells[1, 1].Style.Font.Bold = true; ws.Cells[1, 1].Style.Font.Size = 14; ws.Cells[1, 1].Style.Font.Color.SetColor(DColor.FromArgb(0, 58, 112)); ws.Cells[1, 1, 1, 13].Merge = true;
            ws.Cells[2, 1].Value = $"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}"; ws.Cells[2, 1].Style.Font.Color.SetColor(DColor.Gray);

            var headers = new[] { "Asesor", "Apto", "Torre", "Piso", "Tipo", "Área m²", "Cliente", "Documento", "Celular", "Destino", "Lista", "Precio venta", "Fecha y hora" };
            for (int i = 0; i < headers.Length; i++) { ws.Cells[4, i + 1].Value = headers[i]; StyleHeader(ws.Cells[4, i + 1]); }

            int row = 5; string asesorActual = ""; long totalAsesor = 0; int inicioAsesor = 5;
            foreach (var v in ventas)
            {
                if (v.Asesor != asesorActual)
                {
                    if (asesorActual != "" && row > inicioAsesor)
                    {
                        ws.Cells[row, 1].Value = $"Total {asesorActual}"; ws.Cells[row, 12].Value = totalAsesor;
                        ws.Cells[row, 1, row, 13].Style.Fill.PatternType = ExcelFillStyle.Solid; ws.Cells[row, 1, row, 13].Style.Fill.BackgroundColor.SetColor(DColor.FromArgb(235, 245, 255));
                        ws.Cells[row, 1].Style.Font.Bold = true; ws.Cells[row, 12].Style.Font.Bold = true; ws.Cells[row, 12].Style.Numberformat.Format = "#,##0"; row += 2;
                    }
                    asesorActual = v.Asesor; totalAsesor = 0; inicioAsesor = row;
                }
                ws.Cells[row, 1].Value = v.Asesor; ws.Cells[row, 2].Value = v.Apto; ws.Cells[row, 3].Value = v.Torre; ws.Cells[row, 4].Value = v.Piso; ws.Cells[row, 5].Value = v.Tipo; ws.Cells[row, 6].Value = v.Metros; ws.Cells[row, 7].Value = v.Cliente; ws.Cells[row, 8].Value = v.Documento; ws.Cells[row, 9].Value = v.Celular; ws.Cells[row, 10].Value = v.Destino; ws.Cells[row, 11].Value = v.Lista; ws.Cells[row, 12].Value = v.PrecioVenta; ws.Cells[row, 12].Style.Numberformat.Format = "#,##0"; ws.Cells[row, 13].Value = v.FechaVenta;
                totalAsesor += v.PrecioVenta;
                if (row % 2 == 0) { ws.Cells[row, 1, row, 13].Style.Fill.PatternType = ExcelFillStyle.Solid; ws.Cells[row, 1, row, 13].Style.Fill.BackgroundColor.SetColor(DColor.FromArgb(248, 250, 253)); }
                row++;
            }
            if (asesorActual != "" && row > inicioAsesor)
            { ws.Cells[row, 1].Value = $"Total {asesorActual}"; ws.Cells[row, 12].Value = totalAsesor; ws.Cells[row, 1, row, 13].Style.Fill.PatternType = ExcelFillStyle.Solid; ws.Cells[row, 1, row, 13].Style.Fill.BackgroundColor.SetColor(DColor.FromArgb(235, 245, 255)); ws.Cells[row, 1].Style.Font.Bold = true; ws.Cells[row, 12].Style.Font.Bold = true; ws.Cells[row, 12].Style.Numberformat.Format = "#,##0"; }

            for (int col = 1; col <= 13; col++) ws.Column(col).AutoFit();

            return File(package.GetAsByteArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Ventas_Asesores_{proyNombre.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        // ====================================================================
        //  CUADRO DE ASISTENCIA — resumen diario del lanzamiento
        // ====================================================================

        /// <summary>
        /// Loads the launch attendance summary (evento + días + torres) for the
        /// active project and renders the entry form / summary table.
        /// Gracefully handles the case where the tables don't exist yet.
        /// </summary>
        public async Task<IActionResult> Asistencia()
        {
            ViewBag.Nombre = HttpContext.Session.GetString("Nombre") ?? "Admin";
            ViewBag.Apellido = HttpContext.Session.GetString("Apellido") ?? "";
            ViewBag.ProyectoActivo = HttpContext.Session.GetString("ProyectoNombre") ?? "Sin proyecto";
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;
            int idAdmin = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid2) ? uid2 : 0;

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            var proyectos = new List<(int Id, string Nombre)>();
            var cmdList = new SqlCommand("SELECT IdProyectos, Nombre FROM Proyectos WHERE Activo=1 AND IdAdminCreador=@admin ORDER BY FechaCarga DESC", con);
            cmdList.Parameters.AddWithValue("@admin", idAdmin);
            using (var r = (SqlDataReader)await cmdList.ExecuteReaderAsync())
                while (await r.ReadAsync())
                    proyectos.Add(((int)r["IdProyectos"], r["Nombre"]?.ToString() ?? ""));
            ViewBag.Proyectos = proyectos;

            var evento = await CargarEventoAsync(con, idProy);
            ViewBag.TablaCreadaOk = evento.TablaOk;
            ViewBag.EventoJson = System.Text.Json.JsonSerializer.Serialize(evento.Data);

            return View();
        }

        /// <summary>
        /// Persists the full launch summary sent as a JSON payload, replacing any
        /// previous summary for the active project. Runs inside a transaction so a
        /// partial save can never leave orphaned días/torres.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarAsistencia(string payload)
        {
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;
            if (idProy == 0)
            {
                TempData["AsistError"] = "No hay un proyecto activo.";
                return RedirectToAction("Asistencia");
            }

            using var doc = System.Text.Json.JsonDocument.Parse(payload ?? "{}");
            var root = doc.RootElement;
            string titulo = root.TryGetProperty("titulo", out var tEl) ? (tEl.GetString() ?? "") : "";
            string observaciones = root.TryGetProperty("observaciones", out var oEl) ? (oEl.GetString() ?? "") : "";

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            using var tx = (SqlTransaction)await con.BeginTransactionAsync();
            try
            {
                // Replace: remove the previous summary for this project (cascade clears días/torres)
                var cmdDel = new SqlCommand("DELETE FROM AsistenciaEvento WHERE IdProyecto=@p", con, tx);
                cmdDel.Parameters.AddWithValue("@p", idProy);
                await cmdDel.ExecuteNonQueryAsync();

                var cmdEv = new SqlCommand(@"INSERT INTO AsistenciaEvento (IdProyecto,Titulo,Observaciones)
                    OUTPUT INSERTED.IdEvento VALUES (@p,@t,@o)", con, tx);
                cmdEv.Parameters.AddWithValue("@p", idProy);
                cmdEv.Parameters.AddWithValue("@t", titulo);
                cmdEv.Parameters.AddWithValue("@o", observaciones);
                int idEvento = (int)(await cmdEv.ExecuteScalarAsync())!;

                int ordenDia = 0;
                if (root.TryGetProperty("dias", out var diasEl) && diasEl.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var dia in diasEl.EnumerateArray())
                    {
                        string fechaStr = dia.TryGetProperty("fecha", out var fEl) ? (fEl.GetString() ?? "") : "";
                        object fechaParam = DateTime.TryParse(fechaStr, out var fdt) ? fdt.Date : (object)DBNull.Value;
                        string nombreDia = dia.TryGetProperty("nombreDia", out var ndEl) ? (ndEl.GetString() ?? "") : "";

                        var cmdDia = new SqlCommand(@"INSERT INTO AsistenciaDia
                            (IdEvento,Fecha,NombreDia,Orden,Familias,Adultos,Ninos,Mascotas,
                             AsisteCita,Carros,Motos,Caminando,AgendadosEquipo,AgendadosLucia,AsisteCitaLucia)
                            OUTPUT INSERTED.IdDia
                            VALUES (@ev,@fe,@nd,@or,@fa,@ad,@ni,@ma,@ac,@ca,@mo,@cm,@ae,@al,@acl)", con, tx);
                        cmdDia.Parameters.AddWithValue("@ev", idEvento);
                        cmdDia.Parameters.AddWithValue("@fe", fechaParam);
                        cmdDia.Parameters.AddWithValue("@nd", nombreDia);
                        cmdDia.Parameters.AddWithValue("@or", ordenDia++);
                        cmdDia.Parameters.AddWithValue("@fa", JInt(dia, "familias"));
                        cmdDia.Parameters.AddWithValue("@ad", JInt(dia, "adultos"));
                        cmdDia.Parameters.AddWithValue("@ni", JInt(dia, "ninos"));
                        cmdDia.Parameters.AddWithValue("@ma", JInt(dia, "mascotas"));
                        cmdDia.Parameters.AddWithValue("@ac", JInt(dia, "asisteCita"));
                        cmdDia.Parameters.AddWithValue("@ca", JInt(dia, "carros"));
                        cmdDia.Parameters.AddWithValue("@mo", JInt(dia, "motos"));
                        cmdDia.Parameters.AddWithValue("@cm", JInt(dia, "caminando"));
                        cmdDia.Parameters.AddWithValue("@ae", JInt(dia, "agendadosEquipo"));
                        cmdDia.Parameters.AddWithValue("@al", JInt(dia, "agendadosLucia"));
                        cmdDia.Parameters.AddWithValue("@acl", JInt(dia, "asisteCitaLucia"));
                        int idDia = (int)(await cmdDia.ExecuteScalarAsync())!;

                        int ordenTorre = 0;
                        if (dia.TryGetProperty("torres", out var torresEl) && torresEl.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var t in torresEl.EnumerateArray())
                            {
                                var cmdT = new SqlCommand(@"INSERT INTO AsistenciaTorre
                                    (IdDia,Torre,Orden,Preventas,ValorPreventa,Ventas,ValorVenta,Opciones,ValorOpciones)
                                    VALUES (@d,@to,@or,@pv,@vpv,@ve,@vve,@op,@vop)", con, tx);
                                cmdT.Parameters.AddWithValue("@d", idDia);
                                cmdT.Parameters.AddWithValue("@to", t.TryGetProperty("torre", out var toEl) ? (toEl.GetString() ?? "") : "");
                                cmdT.Parameters.AddWithValue("@or", ordenTorre++);
                                cmdT.Parameters.AddWithValue("@pv", JInt(t, "preventas"));
                                cmdT.Parameters.AddWithValue("@vpv", JLong(t, "valorPreventa"));
                                cmdT.Parameters.AddWithValue("@ve", JInt(t, "ventas"));
                                cmdT.Parameters.AddWithValue("@vve", JLong(t, "valorVenta"));
                                cmdT.Parameters.AddWithValue("@op", JInt(t, "opciones"));
                                cmdT.Parameters.AddWithValue("@vop", JLong(t, "valorOpciones"));
                                await cmdT.ExecuteNonQueryAsync();
                            }
                        }
                    }
                }

                await tx.CommitAsync();
                TempData["AsistExito"] = "Cuadro de asistencia guardado correctamente.";
            }
            catch (SqlException ex) when (ex.Message.Contains("Invalid object name") || ex.Number == 208)
            {
                await tx.RollbackAsync();
                TempData["AsistError"] = "Las tablas de asistencia no existen aún. Ejecuta Scripts/Asistencias.sql.";
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }

            return RedirectToAction("Asistencia");
        }

        /// <summary>
        /// Exports the launch attendance summary to Excel, reproducing the
        /// "Cuadro de asistencia" layout: metric rows, one column per day,
        /// a TOTAL column, the per-torre breakdown, and the observations block.
        /// </summary>
        public async Task<IActionResult> ExportarAsistencia()
        {
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;
            var proyNombre = HttpContext.Session.GetString("ProyectoNombre") ?? "Proyecto";

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            var ev = await CargarEventoAsync(con, idProy);
            if (!ev.TablaOk)
            {
                TempData["AsistError"] = "Las tablas de asistencia no existen aún.";
                return RedirectToAction("Asistencia");
            }
            var dias = ev.Dias;

            ExcelPackage.License.SetNonCommercialPersonal("Londoño Gómez");
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Cuadro de asistencia");

            // Column layout: A = label, then one column per día, last column = TOTAL
            int nDias = dias.Count;
            int colTotal = 2 + nDias;

            // Title
            ws.Cells[1, 1].Value = string.IsNullOrWhiteSpace(ev.Titulo) ? $"Cuadro de asistencia — {proyNombre}" : ev.Titulo;
            ws.Cells[1, 1].Style.Font.Bold = true;
            ws.Cells[1, 1].Style.Font.Size = 13;
            ws.Cells[1, 1].Style.Font.Color.SetColor(DColor.FromArgb(0, 58, 112));
            ws.Cells[1, 1, 1, colTotal].Merge = true;
            ws.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            int row = 3;
            // Header row with day names
            ws.Cells[row, 1].Value = "";
            for (int d = 0; d < nDias; d++) { ws.Cells[row, 2 + d].Value = dias[d].NombreDia; StyleHeader(ws.Cells[row, 2 + d]); }
            ws.Cells[row, colTotal].Value = "TOTAL"; StyleHeader(ws.Cells[row, colTotal]);
            row++;

            // Helper to write an integer metric row with TOTAL = sum
            void FilaInt(string label, Func<dynamic, int> sel, bool bold = false)
            {
                ws.Cells[row, 1].Value = label;
                ws.Cells[row, 1].Style.Font.Bold = true;
                ws.Cells[row, 1].Style.Font.Color.SetColor(DColor.FromArgb(0, 58, 112));
                int total = 0;
                for (int d = 0; d < nDias; d++)
                {
                    int v = sel(dias[d]); total += v;
                    var c = ws.Cells[row, 2 + d]; c.Value = v;
                    c.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    if (bold) c.Style.Font.Bold = true;
                }
                var ct = ws.Cells[row, colTotal]; ct.Value = total;
                ct.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center; ct.Style.Font.Bold = true;
                row++;
            }
            // Helper for percentage row (calculated per day, no naive sum)
            void FilaPct(string label, Func<dynamic, double> sel, double totalPct)
            {
                ws.Cells[row, 1].Value = label;
                ws.Cells[row, 1].Style.Font.Bold = true;
                ws.Cells[row, 1].Style.Font.Color.SetColor(DColor.FromArgb(0, 58, 112));
                for (int d = 0; d < nDias; d++)
                {
                    var c = ws.Cells[row, 2 + d]; c.Value = sel(dias[d]);
                    c.Style.Numberformat.Format = "0%"; c.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                }
                var ct = ws.Cells[row, colTotal]; ct.Value = totalPct;
                ct.Style.Numberformat.Format = "0%"; ct.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center; ct.Style.Font.Bold = true;
                row++;
            }

            int SumFam = dias.Sum(d => (int)d.Familias);
            int SumAsisteCita = dias.Sum(d => (int)d.AsisteCita);
            int SumAgendLucia = dias.Sum(d => (int)d.AgendadosLucia);
            int SumAsisteLucia = dias.Sum(d => (int)d.AsisteCitaLucia);
            int SumAgendEquipo = dias.Sum(d => (int)d.AgendadosEquipo);

            FilaInt("Familias", d => (int)d.Familias, true);
            FilaInt("Adultos", d => (int)d.Adultos, true);
            FilaInt("Niños", d => (int)d.Ninos);
            FilaInt("Mascotas", d => (int)d.Mascotas);
            FilaInt("Asiste con cita", d => (int)d.AsisteCita, true);
            FilaPct("% asiste con cita", d => (int)d.Familias > 0 ? (double)(int)d.AsisteCita / (int)d.Familias : 0,
                    SumFam > 0 ? (double)SumAsisteCita / SumFam : 0);
            FilaInt("Carros", d => (int)d.Carros);
            FilaInt("Motos", d => (int)d.Motos);
            FilaInt("Caminando", d => (int)d.Caminando);

            // ── Bloque por torre ──
            var torresNombres = dias.SelectMany(d => ((List<dynamic>)d.Torres).Select(t => (string)t.Torre))
                                     .Distinct().ToList();
            // Header torres
            ws.Cells[row, 1].Value = "TORRES /ETAPAS";
            ws.Cells[row, 1].Style.Font.Bold = true;
            row++;

            long GVal(dynamic dia, string torre, Func<dynamic, long> sel)
            {
                foreach (var t in (List<dynamic>)dia.Torres) if ((string)t.Torre == torre) return sel(t);
                return 0;
            }

            void FilaTorreInt(string label, Func<dynamic, long> sel)
            {
                ws.Cells[row, 1].Value = label; ws.Cells[row, 1].Style.Font.Bold = true;
                long total = 0;
                for (int d = 0; d < nDias; d++)
                {
                    long v = 0; foreach (var t in (List<dynamic>)dias[d].Torres) v += sel(t);
                    total += v; ws.Cells[row, 2 + d].Value = v;
                    ws.Cells[row, 2 + d].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                }
                ws.Cells[row, colTotal].Value = total; ws.Cells[row, colTotal].Style.Font.Bold = true;
                ws.Cells[row, colTotal].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                row++;
            }
            void FilaTorreMoney(string label, Func<dynamic, long> sel)
            {
                ws.Cells[row, 1].Value = label; ws.Cells[row, 1].Style.Font.Bold = true;
                long total = 0;
                for (int d = 0; d < nDias; d++)
                {
                    long v = 0; foreach (var t in (List<dynamic>)dias[d].Torres) v += sel(t);
                    total += v; var c = ws.Cells[row, 2 + d]; c.Value = v; c.Style.Numberformat.Format = "$ #,##0";
                }
                var ct = ws.Cells[row, colTotal]; ct.Value = total; ct.Style.Numberformat.Format = "$ #,##0"; ct.Style.Font.Bold = true;
                row++;
            }

            FilaTorreInt("Preventas", t => (long)(int)t.Preventas);
            FilaTorreMoney("Valor preventa", t => (long)t.ValorPreventa);
            FilaTorreInt("Ventas", t => (long)(int)t.Ventas);
            FilaTorreMoney("Valor de venta", t => (long)t.ValorVenta);
            FilaTorreInt("Opciones (En proceso)", t => (long)(int)t.Opciones);
            FilaTorreMoney("Opciones (En pesos)", t => (long)t.ValorOpciones);
            FilaTorreInt("Ventas totales unidades", t => (long)((int)t.Preventas + (int)t.Ventas));
            FilaTorreMoney("Ventas totales pesos", t => (long)t.ValorPreventa + (long)t.ValorVenta);
            FilaTorreInt("Opciones + ventas", t => (long)((int)t.Preventas + (int)t.Ventas + (int)t.Opciones));
            FilaTorreMoney("Opciones + ventas (En pesos)", t => (long)t.ValorPreventa + (long)t.ValorVenta + (long)t.ValorOpciones);

            // ── Bloque citas ──
            FilaInt("Agendados Equipo comercial", d => (int)d.AgendadosEquipo);
            FilaInt("Agendados por Lucia", d => (int)d.AgendadosLucia);
            FilaInt("Asiste con cita Lucía", d => (int)d.AsisteCitaLucia);
            FilaPct("% asistencia Lucía", d => (int)d.AgendadosLucia > 0 ? (double)(int)d.AsisteCitaLucia / (int)d.AgendadosLucia : 0,
                    SumAgendLucia > 0 ? (double)SumAsisteLucia / SumAgendLucia : 0);
            FilaInt("Total agendados", d => (int)d.AgendadosEquipo + (int)d.AgendadosLucia);
            FilaPct("% cumplimiento cita",
                    d => ((int)d.AgendadosEquipo + (int)d.AgendadosLucia) > 0 ? (double)(int)d.AsisteCitaLucia / ((int)d.AgendadosEquipo + (int)d.AgendadosLucia) : 0,
                    (SumAgendEquipo + SumAgendLucia) > 0 ? (double)SumAsisteLucia / (SumAgendEquipo + SumAgendLucia) : 0);
            FilaPct("Ventas Vs familias",
                    d => (int)d.Familias > 0 ? (double)((List<dynamic>)d.Torres).Sum(t => (int)t.Ventas) / (int)d.Familias : 0,
                    SumFam > 0 ? (double)dias.SelectMany(d => (List<dynamic>)d.Torres).Sum(t => (int)t.Ventas) / SumFam : 0);

            // ── Observaciones ──
            row++;
            ws.Cells[row, 1].Value = "Observaciones";
            ws.Cells[row, 1].Style.Font.Bold = true;
            ws.Cells[row, 1].Style.Font.Color.SetColor(DColor.FromArgb(0, 58, 112));
            var obsCell = ws.Cells[row, 2, row, colTotal];
            obsCell.Merge = true;
            obsCell.Value = ev.Observaciones;
            obsCell.Style.WrapText = true;
            obsCell.Style.VerticalAlignment = ExcelVerticalAlignment.Top;
            ws.Row(row).Height = 200;

            ws.Column(1).Width = 28;
            for (int c = 2; c <= colTotal; c++) ws.Column(c).Width = 20;

            return File(package.GetAsByteArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Asistencia_{proyNombre.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        // ── Helpers ──

        /// <summary>
        /// Returns the current date/time in Colombia (UTC-5). Resolves the timezone by trying
        /// the IANA id first ("America/Bogota", Linux/macOS) and falling back to the Windows id
        /// ("SA Pacific Standard Time"); if neither is available, applies a fixed -5h offset so
        /// the call never throws regardless of the host OS or ICU configuration.
        /// </summary>
        private static DateTime AhoraColombia()
        {
            foreach (var id in new[] { "America/Bogota", "SA Pacific Standard Time" })
            {
                try { return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(id)); }
                catch (TimeZoneNotFoundException) { }
                catch (InvalidTimeZoneException) { }
            }
            return DateTime.UtcNow.AddHours(-5);
        }

        private static int JInt(System.Text.Json.JsonElement el, string prop)
        {
            if (el.TryGetProperty(prop, out var p))
            {
                if (p.ValueKind == System.Text.Json.JsonValueKind.Number && p.TryGetInt32(out int v)) return v;
                if (p.ValueKind == System.Text.Json.JsonValueKind.String && int.TryParse(p.GetString(), out int sv)) return sv;
            }
            return 0;
        }
        private static long JLong(System.Text.Json.JsonElement el, string prop)
        {
            if (el.TryGetProperty(prop, out var p))
            {
                if (p.ValueKind == System.Text.Json.JsonValueKind.Number && p.TryGetInt64(out long v)) return v;
                if (p.ValueKind == System.Text.Json.JsonValueKind.String && long.TryParse(p.GetString(), out long sv)) return sv;
            }
            return 0;
        }

        /// <summary>
        /// Loads the full attendance summary (evento + días + torres) for a project.
        /// Returns TablaOk=false (without throwing) if the schema isn't installed yet.
        /// </summary>
        private async Task<(bool TablaOk, int IdEvento, string Titulo, string Observaciones, List<dynamic> Dias, object Data)>
            CargarEventoAsync(SqlConnection con, int idProy)
        {
            var dias = new List<dynamic>();
            try
            {
                var cmdEv = new SqlCommand("SELECT TOP 1 IdEvento,Titulo,Observaciones FROM AsistenciaEvento WHERE IdProyecto=@p ORDER BY IdEvento DESC", con);
                cmdEv.Parameters.AddWithValue("@p", idProy);
                int idEvento = 0; string titulo = "", obs = "";
                using (var re = (SqlDataReader)await cmdEv.ExecuteReaderAsync())
                    if (await re.ReadAsync())
                    {
                        idEvento = (int)re["IdEvento"];
                        titulo = re["Titulo"]?.ToString() ?? "";
                        obs = re["Observaciones"]?.ToString() ?? "";
                    }

                if (idEvento > 0)
                {
                    var cmdD = new SqlCommand("SELECT * FROM AsistenciaDia WHERE IdEvento=@e ORDER BY Orden", con);
                    cmdD.Parameters.AddWithValue("@e", idEvento);
                    var diasRaw = new List<dynamic>();
                    using (var rd = (SqlDataReader)await cmdD.ExecuteReaderAsync())
                        while (await rd.ReadAsync())
                            diasRaw.Add(new
                            {
                                IdDia = (int)rd["IdDia"],
                                Fecha = rd["Fecha"] == DBNull.Value ? "" : ((DateTime)rd["Fecha"]).ToString("yyyy-MM-dd"),
                                NombreDia = rd["NombreDia"]?.ToString() ?? "",
                                Familias = (int)rd["Familias"], Adultos = (int)rd["Adultos"], Ninos = (int)rd["Ninos"],
                                Mascotas = (int)rd["Mascotas"], AsisteCita = (int)rd["AsisteCita"], Carros = (int)rd["Carros"],
                                Motos = (int)rd["Motos"], Caminando = (int)rd["Caminando"],
                                AgendadosEquipo = (int)rd["AgendadosEquipo"], AgendadosLucia = (int)rd["AgendadosLucia"],
                                AsisteCitaLucia = (int)rd["AsisteCitaLucia"],
                            });

                    foreach (var d in diasRaw)
                    {
                        var torres = new List<dynamic>();
                        var cmdT = new SqlCommand("SELECT * FROM AsistenciaTorre WHERE IdDia=@d ORDER BY Orden", con);
                        cmdT.Parameters.AddWithValue("@d", (int)d.IdDia);
                        using (var rt = (SqlDataReader)await cmdT.ExecuteReaderAsync())
                            while (await rt.ReadAsync())
                                torres.Add(new
                                {
                                    Torre = rt["Torre"]?.ToString() ?? "",
                                    Preventas = (int)rt["Preventas"], ValorPreventa = (long)rt["ValorPreventa"],
                                    Ventas = (int)rt["Ventas"], ValorVenta = (long)rt["ValorVenta"],
                                    Opciones = (int)rt["Opciones"], ValorOpciones = (long)rt["ValorOpciones"],
                                });
                        dias.Add(new
                        {
                            d.IdDia, d.Fecha, d.NombreDia, d.Familias, d.Adultos, d.Ninos, d.Mascotas,
                            d.AsisteCita, d.Carros, d.Motos, d.Caminando, d.AgendadosEquipo, d.AgendadosLucia,
                            d.AsisteCitaLucia, Torres = torres,
                        });
                    }
                }

                var data = new { titulo, observaciones = obs, dias };
                return (true, idEvento, titulo, obs, dias, data);
            }
            catch (SqlException ex) when (ex.Message.Contains("Invalid object name") || ex.Number == 208)
            {
                return (false, 0, "", "", dias, new { titulo = "", observaciones = "", dias = new List<dynamic>() });
            }
        }


        private static void StyleHeader(ExcelRange cell)
        {
            cell.Style.Font.Bold = true;
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(DColor.FromArgb(0, 85, 165));
            cell.Style.Font.Color.SetColor(DColor.White);
            cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            cell.Style.Border.BorderAround(ExcelBorderStyle.Thin, DColor.FromArgb(0, 55, 130));
        }
    }
}
