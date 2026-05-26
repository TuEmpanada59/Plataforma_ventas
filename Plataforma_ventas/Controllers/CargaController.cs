using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using OfficeOpenXml;
using Plataforma_ventas.Filters;

namespace Plataforma_ventas.Controllers
{
    [RolAutorizado("Administrador")]
    public class CargaController : Controller
    {
        private readonly string _conn;

        public CargaController(IConfiguration config)
        {
            _conn = config.GetConnectionString("DefaultConnection")!;
            ExcelPackage.License.SetNonCommercialPersonal("Londoño Gómez");
        }

        public IActionResult Index()
        {
            ViewBag.Nombre = HttpContext.Session.GetString("Nombre");
            ViewBag.Apellido = HttpContext.Session.GetString("Apellido");
            ViewBag.ProyectoActivo = HttpContext.Session.GetString("ProyectoNombre") ?? "Sin proyecto";
            int idAdmin = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;

            using var con = new SqlConnection(_conn);
            con.Open();

            var proyectos = new List<(int Id, string Nombre, string Codigo)>();
            var tiposProy = new Dictionary<int, string>();

            var cmdList = new SqlCommand(@"SELECT IdProyectos, Nombre, CodigoAcceso, TipProyecto
             FROM Proyectos WHERE Activo=1 AND IdAdminCreador=@uid 
             ORDER BY FechaCarga DESC", con);
            cmdList.Parameters.AddWithValue("@uid", idAdmin);
            using (var r = cmdList.ExecuteReader())
                while (r.Read())
                {
                    int id = (int)r["IdProyectos"];
                    proyectos.Add((id, r["Nombre"]?.ToString() ?? "", r["CodigoAcceso"]?.ToString() ?? ""));
                    tiposProy[id] = r["TipProyecto"]?.ToString() ?? "APARTAMENTOS";
                }

            ViewBag.Proyectos = proyectos;
            ViewBag.TiposProy = tiposProy;

            return View();
        }

        [HttpPost]
        public IActionResult Subir(IFormFile archivo, string nombreProyecto, string tipoProyecto)
        {
            int idAdmin = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;

            if (archivo == null || archivo.Length == 0)
            {
                TempData["Error"] = "Debes seleccionar un archivo Excel.";
                return RedirectToAction("Index");
            }
            if (string.IsNullOrWhiteSpace(nombreProyecto))
            {
                TempData["Error"] = "Debes ingresar el nombre del proyecto.";
                return RedirectToAction("Index");
            }

            if (tipoProyecto != "APARTAMENTOS" && tipoProyecto != "LOTES")
                tipoProyecto = "APARTAMENTOS";

            try
            {
                using var stream = new MemoryStream();
                archivo.CopyTo(stream);
                stream.Position = 0;

                using var package = new ExcelPackage(stream);
                var ws = package.Workbook.Worksheets[0];
                int totalRows = ws.Dimension?.Rows ?? 0;

                if (totalRows < 2)
                {
                    TempData["Error"] = "El archivo no tiene datos.";
                    return RedirectToAction("Index");
                }

                int colApto = -1, colTipo = -1, colPiso = -1, colMetros = -1;
                int colEstado = -1, colTorre = -1, colProyecto = -1;

                int[] colListas = new int[10];
                for (int i = 0; i < 10; i++) colListas[i] = -1;

                int totalCols = ws.Dimension.Columns;
                for (int c = 1; c <= totalCols; c++)
                {
                    var header = ws.Cells[1, c].Text?.Trim().ToUpper() ?? "";
                    if (header == "APTO") colApto = c;
                    if (header == "TIPO1" || header == "TIPO") colTipo = c;
                    if (header == "PISO") colPiso = c;
                    if (header == "METROS") colMetros = c;
                    if (header == "ESTADO") colEstado = c;
                    if (header == "TORRE") colTorre = c;
                    if (header == "PROYECTO") colProyecto = c;
                    for (int li = 1; li <= 10; li++)
                        if (header == $"LISTA{li}") colListas[li - 1] = c;
                }

                bool[] listaActiva = new bool[10];
                for (int li = 0; li < 10; li++)
                {
                    if (colListas[li] < 0) continue;
                    for (int row = 2; row <= totalRows; row++)
                    {
                        var val = ParsearPrecio(ws.Cells[row, colListas[li]].Text);
                        if (val > 0) { listaActiva[li] = true; break; }
                    }
                }

                int[] mapeoListas = new int[5];
                for (int i = 0; i < 5; i++) mapeoListas[i] = -1;
                int slot = 0;
                for (int li = 0; li < 10 && slot < 5; li++)
                    if (listaActiva[li]) mapeoListas[slot++] = li;

                int listasDetectadas = slot;

                if (colProyecto > 0)
                {
                    var nombreEnExcel = ws.Cells[2, colProyecto].Text?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(nombreEnExcel))
                    {
                        var baseIngresado = nombreProyecto.Trim().Split(' ')[0].ToUpper();
                        var baseExcel = nombreEnExcel.Split(' ')[0].ToUpper();
                        if (!baseExcel.Equals(baseIngresado, StringComparison.OrdinalIgnoreCase))
                        {
                            TempData["Error"] = $"El Excel pertenece al proyecto '{nombreEnExcel}', no coincide con '{nombreProyecto}'. Verifica el nombre ingresado.";
                            return RedirectToAction("Index");
                        }
                    }
                }

                using var con = new SqlConnection(_conn);
                con.Open();

                var cmdCheck = new SqlCommand(@"SELECT COUNT(*) FROM Proyectos 
                    WHERE UPPER(Nombre)=UPPER(@n) AND IdAdminCreador=@admin AND Activo=1", con);
                cmdCheck.Parameters.AddWithValue("@n", nombreProyecto.Trim());
                cmdCheck.Parameters.AddWithValue("@admin", idAdmin);
                if ((int)cmdCheck.ExecuteScalar() > 0)
                {
                    TempData["Error"] = $"Ya tienes un proyecto activo llamado '{nombreProyecto}'. Elimínalo primero antes de volver a cargarlo.";
                    return RedirectToAction("Index");
                }

                string codigo = GenerarCodigo(nombreProyecto);

                var cmdProy = new SqlCommand(@"INSERT INTO Proyectos 
                    (Nombre, FechaCarga, Activo, ListaActual, IdAdminCreador, CodigoAcceso, TipProyecto) 
                    OUTPUT INSERTED.IdProyectos 
                    VALUES (@n, GETDATE(), 1, 1, @admin, @codigo, @tipo)", con);
                cmdProy.Parameters.AddWithValue("@n", nombreProyecto.Trim());
                cmdProy.Parameters.AddWithValue("@admin", idAdmin);
                cmdProy.Parameters.AddWithValue("@codigo", codigo);
                cmdProy.Parameters.AddWithValue("@tipo", tipoProyecto);
                int idProyecto = (int)cmdProy.ExecuteScalar();

                int insertados = 0;
                for (int row = 2; row <= totalRows; row++)
                {
                    var apto = ws.Cells[row, colApto > 0 ? colApto : 1].Text?.Trim();
                    if (string.IsNullOrEmpty(apto)) continue;

                    long GetLista(int slot) =>
                        mapeoListas[slot] >= 0 && colListas[mapeoListas[slot]] > 0
                            ? ParsearPrecio(ws.Cells[row, colListas[mapeoListas[slot]]].Text)
                            : 0;

                    var cmdInm = new SqlCommand(@"INSERT INTO Inmuebles 
                        (IdProyecto,Apto,Tipo,Piso,Metros,Lista1,Lista2,Lista3,Lista4,Lista5,Estado,Torre)
                        VALUES (@proy,@apto,@tipo,@piso,@metros,@l1,@l2,@l3,@l4,@l5,@estado,@torre)", con);

                    cmdInm.Parameters.AddWithValue("@proy", idProyecto);
                    cmdInm.Parameters.AddWithValue("@apto", apto);
                    cmdInm.Parameters.AddWithValue("@tipo", colTipo > 0 ? ws.Cells[row, colTipo].Text?.Trim() ?? "" : "");
                    cmdInm.Parameters.AddWithValue("@piso", colPiso > 0 ? ws.Cells[row, colPiso].Text?.Trim() ?? "" : "");
                    cmdInm.Parameters.AddWithValue("@metros", colMetros > 0 ? ws.Cells[row, colMetros].Text?.Trim() ?? "" : "");
                    cmdInm.Parameters.AddWithValue("@l1", GetLista(0));
                    cmdInm.Parameters.AddWithValue("@l2", GetLista(1));
                    cmdInm.Parameters.AddWithValue("@l3", GetLista(2));
                    cmdInm.Parameters.AddWithValue("@l4", GetLista(3));
                    cmdInm.Parameters.AddWithValue("@l5", GetLista(4));
                    cmdInm.Parameters.AddWithValue("@estado", colEstado > 0
                        ? ws.Cells[row, colEstado].Text?.Trim().ToUpper() ?? "DISPONIBLE"
                        : "DISPONIBLE");
                    cmdInm.Parameters.AddWithValue("@torre", colTorre > 0 ? ws.Cells[row, colTorre].Text?.Trim() ?? "" : "");

                    cmdInm.ExecuteNonQuery();
                    insertados++;
                }

                // ── Insertar áreas en ProyectoAreaListas (una fila por Metros+Tipo) ──
                var cmdAreas = new SqlCommand(@"
                    INSERT INTO ProyectoAreaListas (IdProyecto, Metros, ListaActual)
                    SELECT DISTINCT @proy, Metros, 1
                    FROM Inmuebles
                    WHERE IdProyecto = @proy AND Metros IS NOT NULL AND Metros != ''", con);
                cmdAreas.Parameters.AddWithValue("@proy", idProyecto);
                cmdAreas.ExecuteNonQuery();

                HttpContext.Session.SetString("ProyectoId", idProyecto.ToString());
                HttpContext.Session.SetString("ProyectoNombre", nombreProyecto.Trim());
                HttpContext.Session.SetString("TipProyecto", tipoProyecto);

                string tipoLabel = tipoProyecto == "LOTES" ? "lotes" : "inmuebles";
                TempData["Exito"] = $"Proyecto '{nombreProyecto}' cargado con {insertados} {tipoLabel}. Listas detectadas: {listasDetectadas}.";
                TempData["Codigo"] = codigo;
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al procesar el archivo: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult RegenerarCodigo(int idProyecto, string nombreProyecto)
        {
            string nuevoCodigo = GenerarCodigo(nombreProyecto);
            using var con = new SqlConnection(_conn);
            con.Open();
            var cmd = new SqlCommand("UPDATE Proyectos SET CodigoAcceso=@c WHERE IdProyectos=@id", con);
            cmd.Parameters.AddWithValue("@c", nuevoCodigo);
            cmd.Parameters.AddWithValue("@id", idProyecto);
            cmd.ExecuteNonQuery();
            TempData["Exito"] = "Código regenerado correctamente.";
            TempData["Codigo"] = nuevoCodigo;
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult EliminarProyecto(int idProyecto)
        {
            using var con = new SqlConnection(_conn);
            con.Open();
            using var tx = con.BeginTransaction();
            try
            {
                void Exec(string sql)
                {
                    var cmd = new SqlCommand(sql, con, tx);
                    cmd.Parameters.AddWithValue("@id", idProyecto);
                    cmd.ExecuteNonQuery();
                }

                // Orden obligatorio para respetar todas las FK:
                // 1. Ventas (FK → Proyectos, Inmuebles, Clientes, Usuarios)
                Exec("DELETE FROM Ventas WHERE IdProyecto = @id");

                // 2. Clientes (FK → Proyectos)
                Exec("UPDATE Clientes SET IdProyecto = NULL WHERE IdProyecto = @id");

                // 3. Inmuebles (FK → Proyectos)
                Exec("DELETE FROM Inmuebles WHERE IdProyecto = @id");

                // 4. Usuarios — desasignar vendedores (FK → Proyectos)
                Exec("UPDATE Usuarios SET IdProyecto = NULL WHERE IdProyecto = @id");

                // 5. Proyectos hijos si los hay (FK → Proyectos padre)
                Exec("UPDATE Proyectos SET IdProyecto = NULL WHERE IdProyecto = @id");

                // 6. Por último eliminar el proyecto
                Exec("DELETE FROM Proyectos WHERE IdProyectos = @id");

                tx.Commit();
            }
            catch (Exception ex)
            {
                tx.Rollback();
                TempData["Error"] = "Error al eliminar: " + ex.Message;
                return RedirectToAction("Index");
            }

            // Limpiar sesión si era el proyecto activo
            if (HttpContext.Session.GetString("ProyectoId") == idProyecto.ToString())
            {
                HttpContext.Session.Remove("ProyectoId");
                HttpContext.Session.Remove("ProyectoNombre");
                HttpContext.Session.Remove("TipProyecto");
            }

            TempData["Exito"] = "Proyecto eliminado correctamente.";
            return RedirectToAction("Index");
        }

        private static string GenerarCodigo(string nombreProyecto)
        {
            string prefijo = nombreProyecto.Length >= 4
                ? nombreProyecto.Substring(0, 4).ToUpper().Replace(" ", "")
                : nombreProyecto.ToUpper().Replace(" ", "").PadRight(4, 'X');
            string sufijo = Guid.NewGuid().ToString("N").Substring(0, 4).ToUpper();
            return $"{prefijo}-{sufijo}";
        }

        private long ParsearPrecio(string valor)
        {
            if (string.IsNullOrEmpty(valor)) return 0;
            var limpio = valor.Replace("$", "").Replace(".", "").Replace(",", "").Replace(" ", "").Trim();
            return long.TryParse(limpio, out long resultado) ? resultado : 0;
        }
    }
}