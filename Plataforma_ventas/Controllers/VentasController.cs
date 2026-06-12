using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Plataforma_ventas.Filters;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

namespace Plataforma_ventas.Controllers
{
    /// <summary>
    /// Administrator controller for viewing and exporting sales records
    /// for the active project.
    /// </summary>
    [RolAutorizado("Administrador")]
    public class VentasController : Controller
    {
        private readonly string _conn;

        /// <summary>Initializes the controller with DB connection string from configuration.</summary>
        public VentasController(IConfiguration config)
        {
            _conn = config.GetConnectionString("DefaultConnection")!;
        }

        /// <summary>
        /// Lists all sales for the active project with server-side pagination.
        /// Results are ordered by FechaVenta descending (newest first).
        /// Performs a COUNT query for total pages and a paginated SELECT query for the current page.
        /// </summary>
        /// <param name="page">1-based page number. Defaults to 1.</param>
        /// <param name="pageSize">Number of sales per page. Defaults to 25.</param>
        public async Task<IActionResult> Index([FromQuery] int page = 1, [FromQuery] int pageSize = 25)
        {
            ViewBag.Nombre = HttpContext.Session.GetString("Nombre") ?? "Admin";
            ViewBag.Apellido = HttpContext.Session.GetString("Apellido") ?? "";
            var proyNombre = HttpContext.Session.GetString("ProyectoNombre") ?? "Sin proyecto";
            var proyIdStr = HttpContext.Session.GetString("ProyectoId") ?? "0";
            ViewBag.ProyectoActivo = proyNombre;
            int idProy = int.TryParse(proyIdStr, out int pid) ? pid : 0;
            int idAdmin = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 25;

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            // Solo proyectos de este admin
            var proyectos = new List<(int Id, string Nombre)>();
            var cmdList = new SqlCommand("SELECT IdProyectos, Nombre FROM Proyectos WHERE Activo=1 AND IdAdminCreador=@admin ORDER BY FechaCarga DESC", con);
            cmdList.Parameters.AddWithValue("@admin", idAdmin);
            using (var r = (SqlDataReader)await cmdList.ExecuteReaderAsync())
                while (await r.ReadAsync())
                    proyectos.Add(((int)r["IdProyectos"], r["Nombre"]?.ToString() ?? ""));
            ViewBag.Proyectos = proyectos;

            // COUNT for pagination
            var cmdCount = new SqlCommand(
                "SELECT COUNT(*) FROM Ventas WHERE IdProyecto = @proy", con);
            cmdCount.Parameters.AddWithValue("@proy", idProy);
            int total = (int)(await cmdCount.ExecuteScalarAsync())!;
            int totalPages = (int)Math.Ceiling((double)total / pageSize);
            if (page > totalPages && totalPages > 0) page = totalPages;

            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = totalPages;
            ViewBag.Total = total;

            // Sum the full project total (all pages). NOTE: this scalar query MUST run
            // before the paginated reader below is opened — a `using var` reader stays
            // open until the end of the method, and executing another command on the
            // same connection while a DataReader is open throws InvalidOperationException.
            var cmdSum = new SqlCommand(
                "SELECT ISNULL(SUM(PrecioVenta),0) FROM Ventas WHERE IdProyecto=@proy", con);
            cmdSum.Parameters.AddWithValue("@proy", idProy);
            ViewBag.TotalValor = Convert.ToInt64(await cmdSum.ExecuteScalarAsync());

            // Paginated query — sales ordered newest first
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
                ORDER BY v.FechaVenta DESC
                OFFSET (@page-1)*@pageSize ROWS FETCH NEXT @pageSize ROWS ONLY", con);
            cmd.Parameters.AddWithValue("@proy", idProy);
            cmd.Parameters.AddWithValue("@page", page);
            cmd.Parameters.AddWithValue("@pageSize", pageSize);
            using var reader = (SqlDataReader)await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
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

            return View();
        }

        /// <summary>
        /// Generates an Excel workbook with a colour-coded property map
        /// showing the current estado of every unit in the active project.
        /// Performs a SELECT query for all properties ordered by Torre/Piso/Apto.
        /// </summary>
        public async Task<IActionResult> GenerarMapa()
        {
            int idProy = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;
            var proyNombre = HttpContext.Session.GetString("ProyectoNombre") ?? "Proyecto";

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            var inmuebles = new List<dynamic>();
            var cmd = new SqlCommand(@"SELECT Apto, Piso, Torre, Tipo, Estado FROM Inmuebles
                WHERE IdProyecto=@id ORDER BY Torre, Piso DESC, Apto", con);
            cmd.Parameters.AddWithValue("@id", idProy);
            using var reader = (SqlDataReader)await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
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
