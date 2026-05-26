using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Plataforma_ventas.Filters;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

namespace Plataforma_ventas.Controllers
{
    [RolAutorizado("Administrador")]
    public class VentasController : Controller
    {
        private readonly string _conn;

        public VentasController(IConfiguration config)
        {
            _conn = config.GetConnectionString("DefaultConnection")!;
        }

        public IActionResult Index()
        {
            ViewBag.Nombre = HttpContext.Session.GetString("Nombre") ?? "Admin";
            ViewBag.Apellido = HttpContext.Session.GetString("Apellido") ?? "";
            var proyNombre = HttpContext.Session.GetString("ProyectoNombre") ?? "Sin proyecto";
            var proyIdStr = HttpContext.Session.GetString("ProyectoId") ?? "0";
            ViewBag.ProyectoActivo = proyNombre;
            int idProy = int.TryParse(proyIdStr, out int pid) ? pid : 0;
            int idAdmin = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;

            using var con = new SqlConnection(_conn);
            con.Open();

            // Solo proyectos de este admin
            var proyectos = new List<(int Id, string Nombre)>();
            var cmdList = new SqlCommand("SELECT IdProyectos, Nombre FROM Proyectos WHERE Activo=1 AND IdAdminCreador=@admin ORDER BY FechaCarga DESC", con);
            cmdList.Parameters.AddWithValue("@admin", idAdmin);
            using (var r = cmdList.ExecuteReader())
                while (r.Read())
                    proyectos.Add(((int)r["IdProyectos"], r["Nombre"]?.ToString() ?? ""));
            ViewBag.Proyectos = proyectos;

            // Todas las ventas del proyecto
            var ventas = new List<dynamic>();
            var cmd = new SqlCommand(@"
                SELECT v.IdVenta,
                       i.Apto, i.Torre, i.Tipo, i.Piso,
                       c.Nombre+' '+c.Apellido AS Cliente,
                       c.Documento, c.Celular,
                       u.Nombre+' '+u.Apellido AS Asesor,
                       ISNULL(v.Destino,'—') AS Destino,
                       v.PrecioVenta,
                       FORMAT(v.FechaVenta,'dd/MM/yyyy HH:mm') AS FechaVenta,
                       v.Estado, v.ListaAplicada
                FROM Ventas v
                JOIN Inmuebles i ON v.IdInmueble = i.IdInmuebles
                JOIN Clientes  c ON v.IdCliente  = c.IdCliente
                JOIN Usuarios  u ON v.IdUsuario  = u.IdUsuario
                WHERE v.IdProyecto = @proy
                ORDER BY v.FechaVenta DESC", con);
            cmd.Parameters.AddWithValue("@proy", idProy);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                ventas.Add(new
                {
                    Id = (int)reader["IdVenta"],
                    Apto = reader["Apto"]?.ToString() ?? "",
                    Torre = reader["Torre"]?.ToString() ?? "",
                    Tipo = reader["Tipo"]?.ToString() ?? "",
                    Piso = reader["Piso"]?.ToString() ?? "",
                    Cliente = reader["Cliente"]?.ToString() ?? "",
                    Documento = reader["Documento"]?.ToString() ?? "",
                    Celular = reader["Celular"]?.ToString() ?? "",
                    Asesor = reader["Asesor"]?.ToString() ?? "",
                    Destino = reader["Destino"]?.ToString() ?? "—",
                    PrecioVenta = reader["PrecioVenta"]?.ToString() ?? "0",
                    FechaVenta = reader["FechaVenta"]?.ToString() ?? "",
                    Estado = reader["Estado"]?.ToString() ?? "",
                    Lista = reader["ListaAplicada"]?.ToString() ?? ""
                });
            }

            ViewBag.Ventas = ventas;
            ViewBag.TotalVentas = ventas.Count;
            ViewBag.TotalValor = ventas.Sum(v => long.TryParse(v.PrecioVenta, out long p) ? p : 0);

            return View();
        }

        // ── Generar mapa Excel ──
        public IActionResult GenerarMapa()
        {
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;
            var proyNombre = HttpContext.Session.GetString("ProyectoNombre") ?? "Proyecto";

            using var con = new SqlConnection(_conn);
            con.Open();

            var inmuebles = new List<dynamic>();
            var cmd = new SqlCommand(@"SELECT Apto, Piso, Torre, Tipo, Estado FROM Inmuebles 
                WHERE IdProyecto=@id ORDER BY Torre, Piso DESC, Apto", con);
            cmd.Parameters.AddWithValue("@id", idProy);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                inmuebles.Add(new { Apto = reader["Apto"]?.ToString() ?? "", Piso = reader["Piso"]?.ToString() ?? "", Torre = reader["Torre"]?.ToString() ?? "", Tipo = reader["Tipo"]?.ToString() ?? "", Estado = reader["Estado"]?.ToString() ?? "" });
            reader.Close();

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

                var titleCell = ws.Cells[startRow, 1];
                titleCell.Value = $"{proyNombre} — Torre {torre}";
                titleCell.Style.Font.Bold = true;
                titleCell.Style.Font.Size = 14;
                titleCell.Style.Font.Color.SetColor(Color.FromArgb(0, 58, 112));
                ws.Cells[startRow, 1, startRow, tipos.Count + 2].Merge = true;
                startRow++;

                ws.Cells[startRow, 1].Value = "Piso";
                ws.Cells[startRow, 1].Style.Font.Bold = true;
                ws.Cells[startRow, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[startRow, 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(0, 85, 165));
                ws.Cells[startRow, 1].Style.Font.Color.SetColor(Color.White);

                for (int t = 0; t < tipos.Count; t++)
                {
                    var cell = ws.Cells[startRow, t + 2];
                    cell.Value = tipos[t];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(0, 85, 165));
                    cell.Style.Font.Color.SetColor(Color.White);
                    cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                }
                startRow++;

                foreach (var piso in pisos)
                {
                    ws.Cells[startRow, 1].Value = piso;
                    ws.Cells[startRow, 1].Style.Font.Bold = true;
                    ws.Cells[startRow, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

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
                            cell.Style.Font.Color.SetColor(Color.White);
                            cell.Style.Fill.BackgroundColor.SetColor(estado switch
                            {
                                "VENDIDO" => Color.FromArgb(230, 57, 70),
                                "RESERVADO" => Color.FromArgb(255, 149, 0),
                                "EN PROCESO" => Color.FromArgb(90, 90, 200),
                                _ => Color.FromArgb(52, 199, 89)
                            });
                        }
                        else
                        {
                            cell.Value = "—";
                            cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                            cell.Style.Font.Color.SetColor(Color.LightGray);
                        }
                        cell.Style.Border.BorderAround(ExcelBorderStyle.Thin, Color.FromArgb(200, 200, 200));
                    }
                    ws.Cells[startRow, 1].Style.Border.BorderAround(ExcelBorderStyle.Thin, Color.FromArgb(200, 200, 200));
                    startRow++;
                }

                startRow++;
                ws.Cells[startRow, 1].Value = "RESUMEN"; ws.Cells[startRow, 1].Style.Font.Bold = true;
                ws.Cells[startRow, 2].Value = $"✅ Disponibles: {inmsT.Count(i => i.Estado == "DISPONIBLE")}";
                ws.Cells[startRow, 3].Value = $"🔴 Vendidos: {inmsT.Count(i => i.Estado == "VENDIDO")}";
                ws.Cells[startRow, 4].Value = $"🟠 Reservados: {inmsT.Count(i => i.Estado == "RESERVADO")}";
                ws.Cells[startRow, 5].Value = $"🟣 En proceso: {inmsT.Count(i => i.Estado == "EN PROCESO")}";
                startRow += 3;
            }

            ws.Cells[startRow, 1].Value = "LEYENDA"; ws.Cells[startRow, 1].Style.Font.Bold = true; startRow++;
            foreach (var (lbl, color) in new[] { ("DISPONIBLE", Color.FromArgb(52, 199, 89)), ("VENDIDO", Color.FromArgb(230, 57, 70)), ("RESERVADO", Color.FromArgb(255, 149, 0)), ("EN PROCESO", Color.FromArgb(90, 90, 200)) })
            {
                var c = ws.Cells[startRow, 1];
                c.Value = lbl;
                c.Style.Fill.PatternType = ExcelFillStyle.Solid;
                c.Style.Fill.BackgroundColor.SetColor(color);
                c.Style.Font.Color.SetColor(Color.White);
                c.Style.Font.Bold = true;
                c.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                startRow++;
            }

            for (int col = 1; col <= ws.Dimension.End.Column; col++) ws.Column(col).AutoFit();
            ws.Column(1).Width = 10;

            return File(package.GetAsByteArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Mapa_Ventas_{proyNombre.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.xlsx");
        }
    }
}
