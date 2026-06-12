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

            double pctV = total > 0 ? Math.Round((double)vendidos / total * 100, 1) : 0;
            double pctD = total > 0 ? Math.Round((double)disponibles / total * 100, 1) : 0;
            double pctR = total > 0 ? Math.Round((double)reservados / total * 100, 1) : 0;
            double pctP = total > 0 ? Math.Round((double)enProceso / total * 100, 1) : 0;

            var pdfBytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(28);
                    page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(9));

                    // ── HEADER ──
                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("LONDOÑO GÓMEZ").FontSize(18).Bold().FontColor(QColor.FromHex("#003A70"));
                                c.Item().Text("Sistema de Lanzamientos Inmobiliarios").FontSize(9).FontColor(QColor.FromHex("#666666"));
                            });
                            row.ConstantItem(140).AlignRight().Column(c =>
                            {
                                c.Item().Text("INFORME TÉCNICO DE VENTAS").FontSize(9).Bold().FontColor(QColor.FromHex("#003A70"));
                                c.Item().Text($"Proyecto: {proyNombre}").FontSize(8).FontColor(QColor.FromHex("#555555"));
                                c.Item().Text($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(7.5f).FontColor(QColor.FromHex("#999999"));
                            });
                        });
                        col.Item().PaddingTop(5).LineHorizontal(2f).LineColor(QColor.FromHex("#003A70"));
                        col.Item().PaddingTop(2).LineHorizontal(0.5f).LineColor(QColor.FromHex("#0077C8"));
                    });

                    page.Content().PaddingTop(14).Column(col =>
                    {
                        // ── RESUMEN DEL DÍA ──
                        col.Item().Background(QColor.FromHex("#003A70")).Padding(8).Row(row =>
                        {
                            row.RelativeItem().Text($"📅  RESUMEN DEL DÍA — {DateTime.Now:dddd, dd/MM/yyyy}".ToUpper())
                                .FontSize(8.5f).Bold().FontColor(QColors.White);
                        });
                        col.Item().Background(QColor.FromHex("#EEF4FA")).Border(0.5f).BorderColor(QColor.FromHex("#BBCCDD"))
                            .Padding(10).Row(row =>
                            {
                                var stats = new (string Val, string Lbl)[]
                                {
                                ($"{ventasHoy}", "Ventas hoy"),
                                ($"${valorHoy:N0}", "Valor hoy"),
                                ($"{vendidos}", "Total vendidos"),
                                ($"{disponibles}", "Disponibles"),
                                ($"{pctV:0.0}%", "% Avance"),
                                };
                                foreach (var (val, lbl) in stats)
                                {
                                    row.RelativeItem().AlignCenter().Column(c =>
                                    {
                                        c.Item().AlignCenter().Text(val).FontSize(18).Bold().FontColor(QColor.FromHex("#003A70"));
                                        c.Item().AlignCenter().Text(lbl).FontSize(7.5f).FontColor(QColor.FromHex("#666666"));
                                    });
                                }
                            });

                        col.Item().PaddingTop(12);

                        // ── KPIs ──
                        col.Item().Text("INDICADORES GENERALES DEL PROYECTO")
                            .FontSize(8.5f).Bold().FontColor(QColor.FromHex("#003A70"));
                        col.Item().PaddingTop(4).Table(tbl =>
                        {
                            tbl.ColumnsDefinition(c => { for (int i = 0; i < 5; i++) c.RelativeColumn(); });
                            tbl.Header(h => {
                                foreach (var hdr in new[] { "Total inmuebles", "Disponibles", "Vendidos", "Reservados", "Valor total vendido" })
                                    h.Cell().Background(QColor.FromHex("#0055A5")).Padding(6).AlignCenter()
                                        .Text(hdr).FontSize(7.5f).Bold().FontColor(QColors.White);
                            });

                            var cells = new (string Val, string Color)[]
                            {
                                (total.ToString(), "#003A70"),
                                ($"{disponibles} ({pctD:0.0}%)", "#1EA851"),
                                ($"{vendidos} ({pctV:0.0}%)", "#E63946"),
                                ($"{reservados} ({pctR:0.0}%)", "#CC7700"),
                                ($"${valorTotal:N0}", "#003A70"),
                            };
                            foreach (var (val, color) in cells)
                                tbl.Cell().Border(0.5f).BorderColor(QColor.FromHex("#DDDDDD"))
                                    .Background(QColor.FromHex("#F8FAFD")).Padding(7).AlignCenter()
                                    .Text(val).FontSize(11).Bold().FontColor(QColor.FromHex(color));
                        });

                        col.Item().PaddingTop(14);

                        // ── TIPOLOGÍAS ──
                        col.Item().Text("ANÁLISIS POR TIPOLOGÍA")
                            .FontSize(8.5f).Bold().FontColor(QColor.FromHex("#003A70"));
                        col.Item().PaddingTop(4).Table(tbl =>
                        {
                            tbl.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(2); c.RelativeColumn(); c.RelativeColumn();
                                c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(1.5f);
                            });
                            tbl.Header(h => {
                                foreach (var hdr in new[] { "Tipología", "Total", "Vendidos", "Disponibles", "Reservados", "% Vendido" })
                                    h.Cell().Background(QColor.FromHex("#0055A5")).Padding(5).AlignCenter()
                                        .Text(hdr).FontSize(7.5f).Bold().FontColor(QColors.White);
                            });

                            bool alt = false;
                            foreach (var t in tips)
                            {
                                var bg = alt ? QColor.FromHex("#F5F8FC") : QColors.White;
                                double pt = t.Tot > 0 ? Math.Round((double)t.Vend / t.Tot * 100, 1) : 0;
                                tbl.Cell().Background(bg).Border(0.3f).BorderColor(QColor.FromHex("#EEEEEE")).Padding(5)
                                    .Text(t.Tipo).FontSize(8).Bold();
                                tbl.Cell().Background(bg).Border(0.3f).BorderColor(QColor.FromHex("#EEEEEE")).Padding(5).AlignCenter()
                                    .Text(t.Tot.ToString()).FontSize(8);
                                tbl.Cell().Background(bg).Border(0.3f).BorderColor(QColor.FromHex("#EEEEEE")).Padding(5).AlignCenter()
                                    .Text(t.Vend.ToString()).FontSize(8).Bold().FontColor(QColor.FromHex("#E63946"));
                                tbl.Cell().Background(bg).Border(0.3f).BorderColor(QColor.FromHex("#EEEEEE")).Padding(5).AlignCenter()
                                    .Text(t.Disp.ToString()).FontSize(8).FontColor(QColor.FromHex("#1EA851"));
                                tbl.Cell().Background(bg).Border(0.3f).BorderColor(QColor.FromHex("#EEEEEE")).Padding(5).AlignCenter()
                                    .Text(t.Res.ToString()).FontSize(8).FontColor(QColor.FromHex("#CC7700"));
                                tbl.Cell().Background(bg).Border(0.3f).BorderColor(QColor.FromHex("#EEEEEE")).Padding(5).AlignCenter()
                                    .Text($"{pt}%").FontSize(8).Bold().FontColor(QColor.FromHex("#E63946"));
                                alt = !alt;
                            }
                        });

                        col.Item().PaddingTop(14);

                        // ── DESTINOS ──
                        col.Item().Text("DESTINO DE LAS VENTAS")
                            .FontSize(8.5f).Bold().FontColor(QColor.FromHex("#003A70"));
                        col.Item().PaddingTop(4).Table(tbl =>
                        {
                            tbl.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(); c.RelativeColumn(); });
                            tbl.Header(h => {
                                foreach (var hdr in new[] { "Destino", "Unidades", "% del total" })
                                    h.Cell().Background(QColor.FromHex("#0055A5")).Padding(5)
                                        .Text(hdr).FontSize(7.5f).Bold().FontColor(QColors.White);
                            });

                            int totalDest = dests.Sum(d => d.Tot);
                            bool alt = false;
                            foreach (var d in dests)
                            {
                                var bg = alt ? QColor.FromHex("#F5F8FC") : QColors.White;
                                double pctDest = totalDest > 0 ? Math.Round((double)d.Tot / totalDest * 100, 1) : 0;
                                tbl.Cell().Background(bg).Border(0.3f).BorderColor(QColor.FromHex("#EEEEEE")).Padding(5)
                                    .Text(d.Dest).FontSize(8).Bold();
                                tbl.Cell().Background(bg).Border(0.3f).BorderColor(QColor.FromHex("#EEEEEE")).Padding(5).AlignCenter()
                                    .Text(d.Tot.ToString()).FontSize(8);
                                tbl.Cell().Background(bg).Border(0.3f).BorderColor(QColor.FromHex("#EEEEEE")).Padding(5).AlignCenter()
                                    .Text($"{pctDest}%").FontSize(8);
                                alt = !alt;
                            }
                        });

                        col.Item().PaddingTop(14);

                        // ── DETALLE DE VENTAS ──
                        col.Item().Text("DETALLE COMPLETO DE VENTAS")
                            .FontSize(8.5f).Bold().FontColor(QColor.FromHex("#003A70"));
                        col.Item().PaddingTop(4).Table(tbl =>
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
                    });

                    // ── FOOTER ──
                    page.Footer().PaddingTop(8).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().LineHorizontal(0.5f).LineColor(QColor.FromHex("#CCCCCC"));
                            c.Item().PaddingTop(3).Text($"Londoño Gómez  ·  {proyNombre}  ·  Informe técnico de ventas  ·  {DateTime.Now:dd/MM/yyyy HH:mm}")
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
            }).GeneratePdf();

            return File(pdfBytes, "application/pdf",
                $"Informe_Tecnico_{proyNombre.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
        }

        /// <summary>
        /// Generates a colour-coded Excel map of all properties with area column.
        /// Performs a SELECT query for all properties in the active project.
        /// </summary>
        public async Task<IActionResult> GenerarMapa()
        {
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;
            var proyNombre = HttpContext.Session.GetString("ProyectoNombre") ?? "Proyecto";

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            var inmuebles = new List<dynamic>();
            var cmd = new SqlCommand("SELECT Apto,Piso,Torre,Tipo,Metros,Estado FROM Inmuebles WHERE IdProyecto=@id ORDER BY Torre,Piso DESC,Apto", con);
            cmd.Parameters.AddWithValue("@id", idProy);
            using (var reader = (SqlDataReader)await cmd.ExecuteReaderAsync())
                while (await reader.ReadAsync())
                    inmuebles.Add(new { Apto = reader["Apto"]?.ToString() ?? "", Piso = reader["Piso"]?.ToString() ?? "", Torre = reader["Torre"]?.ToString() ?? "", Tipo = reader["Tipo"]?.ToString() ?? "", Metros = reader["Metros"]?.ToString() ?? "", Estado = reader["Estado"]?.ToString() ?? "" });

            ExcelPackage.License.SetNonCommercialPersonal("Londoño Gómez");
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Mapa de ventas");

            var torres = inmuebles.Select(i => (string)i.Torre).Distinct().OrderBy(t => t).ToList();
            int startRow = 1;

            foreach (var torre in torres)
            {
                var inmsT = inmuebles.Where(i => i.Torre == torre).ToList();
                var tipos = inmsT.Select(i => (string)i.Tipo).Distinct().OrderBy(t => t).ToList();
                var pisos = inmsT.Select(i => i.Piso).Distinct()
                    .OrderByDescending(p => { int.TryParse(p?.ToString(), out int n); return n; }).ToList();

                ws.Cells[startRow, 1].Value = $"{proyNombre} — Torre {torre}";
                ws.Cells[startRow, 1].Style.Font.Bold = true;
                ws.Cells[startRow, 1].Style.Font.Size = 13;
                ws.Cells[startRow, 1].Style.Font.Color.SetColor(DColor.FromArgb(0, 58, 112));
                ws.Cells[startRow, 1, startRow, tipos.Count + 2].Merge = true;
                startRow++;

                ws.Cells[startRow, 1].Value = "Piso"; StyleHeader(ws.Cells[startRow, 1]);
                for (int t = 0; t < tipos.Count; t++) { ws.Cells[startRow, t + 2].Value = tipos[t]; StyleHeader(ws.Cells[startRow, t + 2]); }
                ws.Cells[startRow, tipos.Count + 2].Value = "Área m²"; StyleHeader(ws.Cells[startRow, tipos.Count + 2]);
                startRow++;

                foreach (var piso in pisos)
                {
                    ws.Cells[startRow, 1].Value = piso;
                    ws.Cells[startRow, 1].Style.Font.Bold = true;
                    ws.Cells[startRow, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    ws.Cells[startRow, 1].Style.Border.BorderAround(ExcelBorderStyle.Thin, DColor.FromArgb(200, 200, 200));

                    var inmPiso = inmsT.FirstOrDefault(i => i.Piso == piso);
                    ws.Cells[startRow, tipos.Count + 2].Value = inmPiso?.Metros ?? "";
                    ws.Cells[startRow, tipos.Count + 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    ws.Cells[startRow, tipos.Count + 2].Style.Border.BorderAround(ExcelBorderStyle.Thin, DColor.FromArgb(200, 200, 200));

                    for (int t = 0; t < tipos.Count; t++)
                    {
                        var inm = inmsT.FirstOrDefault(i => i.Piso == piso && i.Tipo == tipos[t]);
                        var cell = ws.Cells[startRow, t + 2];
                        if (inm != null)
                        {
                            string estado = inm.Estado;
                            cell.Value = estado;
                            cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                            cell.Style.Font.Bold = true;
                            cell.Style.Font.Color.SetColor(DColor.White);
                            cell.Style.Fill.BackgroundColor.SetColor(estado switch { "VENDIDO" => DColor.FromArgb(230, 57, 70), "RESERVADO" => DColor.FromArgb(255, 149, 0), "EN PROCESO" => DColor.FromArgb(90, 90, 200), _ => DColor.FromArgb(52, 199, 89) });
                        }
                        else { cell.Value = "—"; cell.Style.Font.Color.SetColor(DColor.LightGray); }
                        cell.Style.Border.BorderAround(ExcelBorderStyle.Thin, DColor.FromArgb(200, 200, 200));
                    }
                    startRow++;
                }

                startRow++;
                ws.Cells[startRow, 1].Value = "RESUMEN"; ws.Cells[startRow, 1].Style.Font.Bold = true;
                ws.Cells[startRow, 2].Value = $"Disponibles: {inmsT.Count(i => i.Estado == "DISPONIBLE")}";
                ws.Cells[startRow, 3].Value = $"Vendidos: {inmsT.Count(i => i.Estado == "VENDIDO")}";
                ws.Cells[startRow, 4].Value = $"Reservados: {inmsT.Count(i => i.Estado == "RESERVADO")}";
                ws.Cells[startRow, 5].Value = $"En proceso: {inmsT.Count(i => i.Estado == "EN PROCESO")}";
                startRow += 3;
            }

            ws.Cells[startRow, 1].Value = "LEYENDA"; ws.Cells[startRow, 1].Style.Font.Bold = true; startRow++;
            foreach (var (lbl, color) in new[] { ("DISPONIBLE", DColor.FromArgb(52, 199, 89)), ("VENDIDO", DColor.FromArgb(230, 57, 70)), ("RESERVADO", DColor.FromArgb(255, 149, 0)), ("EN PROCESO", DColor.FromArgb(90, 90, 200)) })
            { var c = ws.Cells[startRow, 1]; c.Value = lbl; c.Style.Fill.PatternType = ExcelFillStyle.Solid; c.Style.Fill.BackgroundColor.SetColor(color); c.Style.Font.Color.SetColor(DColor.White); c.Style.Font.Bold = true; c.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center; startRow++; }

            for (int col = 1; col <= ws.Dimension.End.Column; col++) ws.Column(col).AutoFit();
            ws.Column(1).Width = 10;

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

        /// <summary>
        /// Displays the attendance sheet (cuadro de asistencia) for the active project.
        /// Shows all registered visitors with their interest, assigned vendor, and outcome.
        /// Gracefully handles the case where the Asistencias table has not been created yet.
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

            // Load available metros for dropdown
            var metros = new List<string>();
            var cmdMetros = new SqlCommand("SELECT DISTINCT Metros FROM ProyectoAreaListas WHERE IdProyecto=@id ORDER BY Metros", con);
            cmdMetros.Parameters.AddWithValue("@id", idProy);
            using (var rm = (SqlDataReader)await cmdMetros.ExecuteReaderAsync())
                while (await rm.ReadAsync())
                    metros.Add(rm["Metros"]?.ToString() ?? "");
            ViewBag.MetrosDisponibles = metros;

            // Load vendors for dropdown
            var vendedores = new List<dynamic>();
            var cmdVend = new SqlCommand("SELECT IdUsuario, Nombre+' '+Apellido AS NombreCompleto FROM Usuarios WHERE Rol='Vendedor' ORDER BY Nombre", con);
            using (var rv = (SqlDataReader)await cmdVend.ExecuteReaderAsync())
                while (await rv.ReadAsync())
                    vendedores.Add(new { Id = (int)rv["IdUsuario"], Nombre = rv["NombreCompleto"]?.ToString() ?? "" });
            ViewBag.Vendedores = vendedores;

            // Load attendance records — catch gracefully if table doesn't exist yet
            var asistencias = new List<dynamic>();
            try
            {
                var cmdAs = new SqlCommand(@"
                    SELECT a.IdAsistencia, a.Nombre, a.Apellido, a.Documento, a.Celular, a.Correo,
                           a.MetrosInteres, a.TipoInteres,
                           ISNULL(u.Nombre+' '+u.Apellido,'—') AS Vendedor,
                           FORMAT(a.FechaVisita,'dd/MM/yyyy HH:mm') AS FechaVisita,
                           a.Estado, a.Observaciones
                    FROM Asistencias a
                    LEFT JOIN Usuarios u ON u.IdUsuario = a.IdVendedorAtiende
                    WHERE a.IdProyecto = @id
                    ORDER BY a.FechaVisita DESC", con);
                cmdAs.Parameters.AddWithValue("@id", idProy);
                using (var ra = (SqlDataReader)await cmdAs.ExecuteReaderAsync())
                    while (await ra.ReadAsync())
                        asistencias.Add(new
                        {
                            Id = (int)ra["IdAsistencia"],
                            Nombre = ra["Nombre"]?.ToString() ?? "",
                            Apellido = ra["Apellido"]?.ToString() ?? "",
                            Documento = ra["Documento"]?.ToString() ?? "",
                            Celular = ra["Celular"]?.ToString() ?? "",
                            Correo = ra["Correo"]?.ToString() ?? "",
                            MetrosInteres = ra["MetrosInteres"]?.ToString() ?? "",
                            TipoInteres = ra["TipoInteres"]?.ToString() ?? "",
                            Vendedor = ra["Vendedor"]?.ToString() ?? "—",
                            FechaVisita = ra["FechaVisita"]?.ToString() ?? "",
                            Estado = ra["Estado"]?.ToString() ?? "",
                            Observaciones = ra["Observaciones"]?.ToString() ?? "",
                        });
                ViewBag.TablaCreadaOk = true;
            }
            catch (SqlException ex) when (ex.Message.Contains("Invalid object name") || ex.Number == 208)
            {
                ViewBag.TablaCreadaOk = false;
            }

            ViewBag.Asistencias = asistencias;
            return View();
        }

        /// <summary>
        /// Registers a new attendee in the attendance sheet.
        /// Inserts a row into the Asistencias table for the active project.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistrarAsistente(
            string nombre, string apellido, string documento,
            string celular, string correo, string metrosInteres, string tipoInteres,
            int? idVendedorAtiende, string estado, string observaciones)
        {
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;
            using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            var cmd = new SqlCommand(@"INSERT INTO Asistencias
                (IdProyecto,Nombre,Apellido,Documento,Celular,Correo,
                 MetrosInteres,TipoInteres,IdVendedorAtiende,Estado,Observaciones,FechaVisita)
                VALUES (@proy,@nom,@ape,@doc,@cel,@cor,@met,@tip,@vend,@est,@obs,GETDATE())", con);
            cmd.Parameters.AddWithValue("@proy", idProy);
            cmd.Parameters.AddWithValue("@nom", nombre ?? "");
            cmd.Parameters.AddWithValue("@ape", apellido ?? "");
            cmd.Parameters.AddWithValue("@doc", documento ?? "");
            cmd.Parameters.AddWithValue("@cel", celular ?? "");
            cmd.Parameters.AddWithValue("@cor", correo ?? "");
            cmd.Parameters.AddWithValue("@met", metrosInteres ?? "");
            cmd.Parameters.AddWithValue("@tip", tipoInteres ?? "");
            cmd.Parameters.AddWithValue("@vend", idVendedorAtiende.HasValue ? (object)idVendedorAtiende.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@est", estado ?? "Visitó");
            cmd.Parameters.AddWithValue("@obs", observaciones ?? "");
            await cmd.ExecuteNonQueryAsync();
            TempData["AsistExito"] = "Asistente registrado correctamente.";
            return RedirectToAction("Asistencia");
        }

        /// <summary>
        /// Exports the attendance sheet to Excel.
        /// Generates a colour-coded workbook with one row per attendee.
        /// </summary>
        public async Task<IActionResult> ExportarAsistencia()
        {
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;
            var proyNombre = HttpContext.Session.GetString("ProyectoNombre") ?? "Proyecto";

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            var asistencias = new List<dynamic>();
            var cmd = new SqlCommand(@"
                SELECT a.Nombre, a.Apellido, a.Documento, a.Celular, a.Correo,
                       a.MetrosInteres, a.TipoInteres,
                       ISNULL(u.Nombre+' '+u.Apellido,'—') AS Vendedor,
                       FORMAT(a.FechaVisita,'dd/MM/yyyy HH:mm') AS FechaVisita,
                       a.Estado, a.Observaciones
                FROM Asistencias a
                LEFT JOIN Usuarios u ON u.IdUsuario = a.IdVendedorAtiende
                WHERE a.IdProyecto = @id
                ORDER BY a.FechaVisita DESC", con);
            cmd.Parameters.AddWithValue("@id", idProy);
            using (var r = (SqlDataReader)await cmd.ExecuteReaderAsync())
                while (await r.ReadAsync())
                    asistencias.Add(new
                    {
                        Nombre = r["Nombre"]?.ToString() ?? "",
                        Apellido = r["Apellido"]?.ToString() ?? "",
                        Documento = r["Documento"]?.ToString() ?? "",
                        Celular = r["Celular"]?.ToString() ?? "",
                        Correo = r["Correo"]?.ToString() ?? "",
                        MetrosInteres = r["MetrosInteres"]?.ToString() ?? "",
                        TipoInteres = r["TipoInteres"]?.ToString() ?? "",
                        Vendedor = r["Vendedor"]?.ToString() ?? "—",
                        FechaVisita = r["FechaVisita"]?.ToString() ?? "",
                        Estado = r["Estado"]?.ToString() ?? "",
                        Observaciones = r["Observaciones"]?.ToString() ?? "",
                    });

            ExcelPackage.License.SetNonCommercialPersonal("Londoño Gómez");
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Cuadro de asistencia");

            ws.Cells[1, 1].Value = $"Cuadro de asistencia — {proyNombre}";
            ws.Cells[1, 1].Style.Font.Bold = true;
            ws.Cells[1, 1].Style.Font.Size = 14;
            ws.Cells[1, 1].Style.Font.Color.SetColor(DColor.FromArgb(0, 58, 112));
            ws.Cells[1, 1, 1, 11].Merge = true;
            ws.Cells[2, 1].Value = $"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}";
            ws.Cells[2, 1].Style.Font.Color.SetColor(DColor.Gray);

            var headers = new[] { "Nombre", "Apellido", "Documento", "Celular", "Correo", "Área m²", "Tipo", "Asesor", "Fecha visita", "Estado", "Observaciones" };
            for (int i = 0; i < headers.Length; i++) { ws.Cells[4, i + 1].Value = headers[i]; StyleHeader(ws.Cells[4, i + 1]); }

            int row = 5;
            foreach (var a in asistencias)
            {
                ws.Cells[row, 1].Value = a.Nombre;
                ws.Cells[row, 2].Value = a.Apellido;
                ws.Cells[row, 3].Value = a.Documento;
                ws.Cells[row, 4].Value = a.Celular;
                ws.Cells[row, 5].Value = a.Correo;
                ws.Cells[row, 6].Value = a.MetrosInteres;
                ws.Cells[row, 7].Value = a.TipoInteres;
                ws.Cells[row, 8].Value = a.Vendedor;
                ws.Cells[row, 9].Value = a.FechaVisita;
                ws.Cells[row, 10].Value = a.Estado;
                ws.Cells[row, 11].Value = a.Observaciones;

                // Colour-code by Estado
                var bgColor = a.Estado == "Compró" ? DColor.FromArgb(52, 199, 89) :
                              a.Estado == "Apartó" ? DColor.FromArgb(255, 149, 0) :
                              a.Estado == "No interesado" ? DColor.FromArgb(230, 57, 70) :
                              DColor.FromArgb(230, 244, 255);
                ws.Cells[row, 10].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[row, 10].Style.Fill.BackgroundColor.SetColor(bgColor);
                ws.Cells[row, 10].Style.Font.Bold = true;
                if (row % 2 == 0)
                {
                    ws.Cells[row, 1, row, 9].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    ws.Cells[row, 1, row, 9].Style.Fill.BackgroundColor.SetColor(DColor.FromArgb(248, 250, 253));
                    ws.Cells[row, 11].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    ws.Cells[row, 11].Style.Fill.BackgroundColor.SetColor(DColor.FromArgb(248, 250, 253));
                }
                row++;
            }

            for (int col = 1; col <= 11; col++) ws.Column(col).AutoFit();
            return File(package.GetAsByteArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Asistencia_{proyNombre.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.xlsx");
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
