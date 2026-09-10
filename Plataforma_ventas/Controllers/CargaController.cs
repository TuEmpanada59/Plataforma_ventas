using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using OfficeOpenXml;
using Plataforma_ventas.Filters;
using Plataforma_ventas.Hubs;

namespace Plataforma_ventas.Controllers
{
    /// <summary>
    /// Administrator controller for bulk data loading: uploading Excel files to create
    /// new projects with their property inventory, regenerating access codes,
    /// and deleting projects with all associated data.
    /// </summary>
    [RolAutorizado("Administrador", "SuperAdministrador")]
    public class CargaController : Controller
    {
        private readonly string _conn;
        private readonly IHubContext<VentasHub, IVentasClient> _hub;

        /// <summary>Initializes the controller with DB connection string and the SignalR hub.</summary>
        public CargaController(IConfiguration config, IHubContext<VentasHub, IVentasClient> hub)
        {
            _conn = config.GetConnectionString("DefaultConnection")!;
            _hub = hub;
            ExcelPackage.License.SetNonCommercialPersonal("Londoño Gómez");
        }

        /// <summary>
        /// Displays the project upload page with the list of projects owned by this admin.
        /// Performs a SELECT query on Proyectos.
        /// </summary>
        public async Task<IActionResult> Index()
        {
            ViewBag.Nombre = HttpContext.Session.GetString("Nombre");
            ViewBag.Apellido = HttpContext.Session.GetString("Apellido");
            ViewBag.ProyectoActivo = HttpContext.Session.GetString("ProyectoNombre") ?? "Sin proyecto";
            int idAdmin = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            var proyectos = new List<(int Id, string Nombre, string Codigo)>();
            var tiposProy = new Dictionary<int, string>();

            var cmdList = new SqlCommand(@"SELECT IdProyectos, Nombre, CodigoAcceso, TipProyecto
                FROM Proyectos WHERE Activo=1
                ORDER BY FechaCarga DESC", con);
            using (var r = (SqlDataReader)await cmdList.ExecuteReaderAsync())
                while (await r.ReadAsync())
                {
                    int id = (int)r["IdProyectos"];
                    proyectos.Add((id, r["Nombre"]?.ToString() ?? "", r["CodigoAcceso"]?.ToString() ?? ""));
                    tiposProy[id] = r["TipProyecto"]?.ToString() ?? "APARTAMENTOS";
                }

            ViewBag.Proyectos = proyectos;
            ViewBag.TiposProy = tiposProy;

            return View();
        }

        /// <summary>
        /// Parses an uploaded Excel file and creates a new project with all its properties.
        /// Detects up to 5 active price lists from columns LISTA1–LISTA10 (skipping empty ones).
        /// Performs INSERT queries on Proyectos, Inmuebles, and ProyectoAreaListas.
        /// </summary>
        /// <param name="archivo">The Excel (.xlsx) file to parse.</param>
        /// <param name="nombreProyecto">Display name for the new project.</param>
        /// <param name="tipoProyecto">Project type: "APARTAMENTOS" or "LOTES".</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Subir(IFormFile archivo, string nombreProyecto, string tipoProyecto)
        {
            int idAdmin = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;

            if (archivo == null || archivo.Length == 0)
            {
                TempData["Error"] = "Debes seleccionar un archivo Excel.";
                return RedirectToAction("Index");
            }
            // Validación de tipo y tamaño (defensa contra DoS por memoria / archivos no válidos).
            const long maxBytes = 10 * 1024 * 1024; // 10 MB
            if (archivo.Length > maxBytes)
            {
                TempData["Error"] = "El archivo supera el tamaño máximo permitido (10 MB).";
                return RedirectToAction("Index");
            }
            if (Path.GetExtension(archivo.FileName).ToLowerInvariant() != ".xlsx")
            {
                TempData["Error"] = "El archivo debe ser un Excel con extensión .xlsx.";
                return RedirectToAction("Index");
            }
            if (string.IsNullOrWhiteSpace(nombreProyecto))
            {
                TempData["Error"] = "Debes ingresar el nombre del proyecto.";
                return RedirectToAction("Index");
            }

            if (tipoProyecto != "APARTAMENTOS" && tipoProyecto != "LOTES" &&
                tipoProyecto != "SUITES" && tipoProyecto != "SALUD" && tipoProyecto != "OFICINAS")
                tipoProyecto = "APARTAMENTOS";

            // Nombre de la columna principal según el tipo de producto
            string colNombreUnidad = tipoProyecto switch
            {
                "SUITES"   => "SUITE",
                "SALUD"    => "CONSULTORIO",
                "OFICINAS" => "OFICINA",
                _          => "APTO"   // APARTAMENTOS y LOTES
            };

            try
            {
                using var stream = new MemoryStream();
                await archivo.CopyToAsync(stream);
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
                int colEstado = -1, colTorre = -1, colProyecto = -1, colSuite = -1;

                int[] colListas = new int[10];
                for (int i = 0; i < 10; i++) colListas[i] = -1;

                int totalCols = ws.Dimension.Columns;
                for (int c = 1; c <= totalCols; c++)
                {
                    var header = ws.Cells[1, c].Text?.Trim().ToUpper() ?? "";
                    if (header == colNombreUnidad) colApto = c;
                    if (header == "TIPO1" || header == "TIPO") colTipo = c;
                    if (header == "PISO") colPiso = c;
                    if (header == "METROS") colMetros = c;
                    if (header == "ESTADO") colEstado = c;
                    if (header == "TORRE") colTorre = c;
                    if (header == "SUITE") colSuite = c;
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

                // La columna SUITE trae el nombre comercial completo de la unidad
                // (número + torre, p. ej. "1204 T3"). Si el archivo la incluye, ese es
                // el nombre que se muestra en toda la plataforma, aunque el proyecto no
                // sea de tipo SUITES: es como el cliente y el asesor identifican la unidad.
                if (colSuite > 0) colApto = colSuite;

                // ── Validaciones de columnas obligatorias ──────────────────────────────
                if (colApto < 0)
                {
                    TempData["Error"] = $"El archivo no contiene la columna '{colNombreUnidad}' requerida para proyectos de tipo {tipoProyecto}. Verifica que el tipo de proyecto sea el correcto.";
                    return RedirectToAction("Index");
                }
                if (colMetros < 0)
                {
                    TempData["Error"] = "El archivo debe incluir la columna METROS.";
                    return RedirectToAction("Index");
                }
                if (colProyecto < 0)
                {
                    TempData["Error"] = "El archivo debe incluir la columna PROYECTO con el nombre del proyecto.";
                    return RedirectToAction("Index");
                }

                // Validar que el nombre en la columna PROYECTO coincida con el ingresado
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

                // ── Contar filas válidas ANTES de tocar la BD ──────────────────────────
                // Una fila es válida si tiene: unidad + metros + al menos un precio > 0
                int filasValidas = 0;
                for (int row = 2; row <= totalRows; row++)
                {
                    var unidad = ws.Cells[row, colApto].Text?.Trim();
                    var metros = ws.Cells[row, colMetros].Text?.Trim();
                    if (string.IsNullOrEmpty(unidad) || string.IsNullOrEmpty(metros)) continue;
                    bool tieneListaPrecio = false;
                    for (int li = 0; li < 10 && !tieneListaPrecio; li++)
                        if (colListas[li] > 0 && ParsearPrecio(ws.Cells[row, colListas[li]].Text) > 0)
                            tieneListaPrecio = true;
                    if (tieneListaPrecio) filasValidas++;
                }
                if (filasValidas == 0)
                {
                    TempData["Error"] = $"El archivo no contiene inmuebles válidos. Cada inmueble debe tener {colNombreUnidad}, METROS y al menos un precio en una columna LISTA.";
                    return RedirectToAction("Index");
                }

                using var con = new SqlConnection(_conn);
                await con.OpenAsync();

                var cmdCheck = new SqlCommand(@"SELECT COUNT(*) FROM Proyectos
                    WHERE UPPER(Nombre)=UPPER(@n) AND IdAdminCreador=@admin AND Activo=1", con);
                cmdCheck.Parameters.AddWithValue("@n", nombreProyecto.Trim());
                cmdCheck.Parameters.AddWithValue("@admin", idAdmin);
                if ((int)(await cmdCheck.ExecuteScalarAsync())! > 0)
                {
                    TempData["Error"] = $"Ya tienes un proyecto activo llamado '{nombreProyecto}'. Elimínalo primero antes de volver a cargarlo.";
                    return RedirectToAction("Index");
                }

                string codigo = GenerarCodigo(nombreProyecto);

                // ── Transacción: proyecto + inmuebles + áreas ──────────────────────────
                using var tx = con.BeginTransaction();

                var cmdProy = new SqlCommand(@"INSERT INTO Proyectos
                    (Nombre, FechaCarga, Activo, ListaActual, IdAdminCreador, CodigoAcceso, TipProyecto)
                    OUTPUT INSERTED.IdProyectos
                    VALUES (@n, GETDATE(), 1, 1, @admin, @codigo, @tipo)", con, tx);
                cmdProy.Parameters.AddWithValue("@n", nombreProyecto.Trim());
                cmdProy.Parameters.AddWithValue("@admin", idAdmin);
                cmdProy.Parameters.AddWithValue("@codigo", codigo);
                cmdProy.Parameters.AddWithValue("@tipo", tipoProyecto);
                int idProyecto = (int)(await cmdProy.ExecuteScalarAsync())!;

                int insertados = 0, reservadosExcel = 0, vendidosExcel = 0;
                int idClienteImportado = 0;   // se crea solo si el archivo trae vendidos

                for (int row = 2; row <= totalRows; row++)
                {
                    var apto = ws.Cells[row, colApto].Text?.Trim();
                    if (string.IsNullOrEmpty(apto)) continue;

                    long GetLista(int s) =>
                        mapeoListas[s] >= 0 && colListas[mapeoListas[s]] > 0
                            ? ParsearPrecio(ws.Cells[row, colListas[mapeoListas[s]]].Text)
                            : 0;

                    var estadoFila = Texto.EstadoInmueble(colEstado > 0 ? ws.Cells[row, colEstado].Text : "");

                    // Un inmueble que llega reservado o vendido se queda con el precio de la
                    // Lista 1: es el precio con el que se negoció antes del lanzamiento, y
                    // dejarlo suelto haría que al escriturar se cobrara la lista vigente.
                    long precioLista1 = GetLista(0);

                    var cmdInm = new SqlCommand(@"INSERT INTO Inmuebles
                        (IdProyecto,Apto,Tipo,Piso,Metros,Lista1,Lista2,Lista3,Lista4,Lista5,Estado,Torre,
                         PrecioReserva,FechaReserva)
                        OUTPUT INSERTED.IdInmuebles
                        VALUES (@proy,@apto,@tipo,@piso,@metros,@l1,@l2,@l3,@l4,@l5,@estado,@torre,
                                @precioRes,
                                CASE WHEN @estado='RESERVADO' THEN GETDATE() END)", con, tx);

                    cmdInm.Parameters.AddWithValue("@proy", idProyecto);
                    cmdInm.Parameters.AddWithValue("@apto", apto);
                    cmdInm.Parameters.AddWithValue("@tipo", colTipo > 0 ? ws.Cells[row, colTipo].Text?.Trim() ?? "" : "");
                    cmdInm.Parameters.AddWithValue("@piso", colPiso > 0 ? ws.Cells[row, colPiso].Text?.Trim() ?? "" : "");
                    cmdInm.Parameters.AddWithValue("@metros", ws.Cells[row, colMetros].Text?.Trim() ?? "");
                    cmdInm.Parameters.AddWithValue("@l1", precioLista1);
                    cmdInm.Parameters.AddWithValue("@l2", GetLista(1));
                    cmdInm.Parameters.AddWithValue("@l3", GetLista(2));
                    cmdInm.Parameters.AddWithValue("@l4", GetLista(3));
                    cmdInm.Parameters.AddWithValue("@l5", GetLista(4));
                    cmdInm.Parameters.AddWithValue("@estado", estadoFila);
                    // La reserva que viene del Excel no tiene asesor, pero sí precio: el de
                    // la Lista 1, que es el que el resto de la plataforma respeta al vender.
                    cmdInm.Parameters.AddWithValue("@precioRes",
                        estadoFila == "RESERVADO" && precioLista1 > 0 ? (object)precioLista1 : DBNull.Value);
                    // La torre puede venir en su propia columna o embebida en el nombre de
                    // la unidad ("1204 T3"). Se guarda normalizada para poder agrupar y
                    // filtrar por torre sin depender de cómo venga escrita en el Excel.
                    var torreExcel = colTorre > 0 ? ws.Cells[row, colTorre].Text?.Trim() ?? "" : "";
                    cmdInm.Parameters.AddWithValue("@torre", Texto.TorreNormalizada(torreExcel, apto));

                    int idInmueble = Convert.ToInt32((await cmdInm.ExecuteScalarAsync())!);
                    insertados++;
                    if (estadoFila == "RESERVADO") reservadosExcel++;

                    // Un inmueble que ya llega vendido tiene que aparecer en Ventas: si no,
                    // el inventario dice "vendido" y el informe de ventas no lo ve, y las
                    // dos cifras nunca cuadran. La venta nace incompleta (sin cliente ni
                    // asesor reales) y se completa después desde el listado de ventas.
                    if (estadoFila == "VENDIDO")
                    {
                        if (idClienteImportado == 0)
                            idClienteImportado = await ClientePorRegistrarAsync(con, tx, nombreProyecto.Trim());

                        var cmdVenta = new SqlCommand(@"INSERT INTO Ventas
                            (IdInmueble,IdCliente,IdUsuario,IdProyecto,ListaAplicada,PrecioVenta,
                             Destino,Estado,Observaciones,Origen)
                            VALUES (@inm,@cli,@usr,@proy,1,@precio,NULL,'ACTIVA',@obs,'EXCEL')", con, tx);
                        cmdVenta.Parameters.AddWithValue("@inm", idInmueble);
                        cmdVenta.Parameters.AddWithValue("@cli", idClienteImportado);
                        cmdVenta.Parameters.AddWithValue("@usr", idAdmin);
                        cmdVenta.Parameters.AddWithValue("@proy", idProyecto);
                        cmdVenta.Parameters.AddWithValue("@precio", precioLista1);
                        cmdVenta.Parameters.AddWithValue("@obs",
                            "Venta cargada desde el Excel del proyecto. Falta completar cliente y asesor.");
                        await cmdVenta.ExecuteNonQueryAsync();
                        vendidosExcel++;
                    }
                }

                // Insertar áreas en ProyectoAreaListas (una fila por Metros+Tipo)
                var cmdAreas = new SqlCommand(@"
                    INSERT INTO ProyectoAreaListas (IdProyecto, Metros, ListaActual)
                    SELECT DISTINCT @proy, Metros, 1
                    FROM Inmuebles
                    WHERE IdProyecto = @proy AND Metros IS NOT NULL AND Metros != ''", con, tx);
                cmdAreas.Parameters.AddWithValue("@proy", idProyecto);
                await cmdAreas.ExecuteNonQueryAsync();

                tx.Commit();

                HttpContext.Session.SetString("ProyectoId", idProyecto.ToString());
                HttpContext.Session.SetString("ProyectoNombre", nombreProyecto.Trim());
                HttpContext.Session.SetString("TipProyecto", tipoProyecto);

                string tipoLabel = tipoProyecto switch
                {
                    "LOTES"    => "lotes",
                    "SUITES"   => "suites",
                    "SALUD"    => "consultorios",
                    "OFICINAS" => "oficinas",
                    _          => "apartamentos"
                };
                var detalleEstados = "";
                if (reservadosExcel > 0)
                    detalleEstados += $" {reservadosExcel} llegaron reservados con el precio de la Lista 1.";
                if (vendidosExcel > 0)
                    detalleEstados += $" {vendidosExcel} llegaron vendidos: ya están en Ventas, " +
                                      "complétales el cliente y el asesor desde ahí.";

                TempData["Exito"] = $"Proyecto '{nombreProyecto}' cargado con {insertados} {tipoLabel}. " +
                                    $"Listas detectadas: {listasDetectadas}.{detalleEstados}";
                TempData["Codigo"] = codigo;
            }
            catch (Exception ex)
            {
                // using var tx hace rollback automático al hacer Dispose sin Commit previo
                TempData["Error"] = "Error al procesar el archivo: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        /// <summary>
        /// Bulk price adjustment screen: the form, the preview of what an adjustment would
        /// do, and the history of adjustments already applied (so they can be reverted).
        /// Nothing is written here — the preview runs the same query the apply step runs,
        /// so what the administrator sees is what gets written.
        /// </summary>
        /// <param name="idProyecto">Project to adjust; falls back to the active one.</param>
        /// <param name="torre">Restrict to one tower ("" = all).</param>
        /// <param name="metros">Restrict to one area ("" = all).</param>
        /// <param name="listas">Comma-separated list numbers to touch ("" = all with a price).</param>
        /// <param name="tipo">PESOS or PORCENTAJE.</param>
        /// <param name="valor">Amount or percentage; negative lowers the price.</param>
        /// <param name="previsualizar">True to compute the preview.</param>
        public async Task<IActionResult> Precios(int idProyecto = 0, string torre = "", string metros = "",
            string listas = "", string tipo = "PORCENTAJE", decimal valor = 0, bool previsualizar = false)
        {
            ViewBag.Nombre = HttpContext.Session.GetString("Nombre");
            ViewBag.Apellido = HttpContext.Session.GetString("Apellido");
            ViewBag.ProyectoActivo = HttpContext.Session.GetString("ProyectoNombre") ?? "Sin proyecto";

            if (idProyecto <= 0)
                idProyecto = int.TryParse(HttpContext.Session.GetString("ProyectoId"), out int pid) ? pid : 0;

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            var proyectos = new List<(int Id, string Nombre)>();
            var cmdProy = new SqlCommand(
                "SELECT IdProyectos, Nombre FROM Proyectos WHERE Activo=1 ORDER BY FechaCarga DESC", con);
            using (var r = (SqlDataReader)await cmdProy.ExecuteReaderAsync())
                while (await r.ReadAsync())
                    proyectos.Add(((int)r["IdProyectos"], r["Nombre"]?.ToString() ?? ""));
            ViewBag.Proyectos = proyectos;

            if (idProyecto <= 0 && proyectos.Count > 0) idProyecto = proyectos[0].Id;
            ViewBag.IdProyecto = idProyecto;
            ViewBag.NombreProyecto = proyectos.FirstOrDefault(p => p.Id == idProyecto).Nombre ?? "";

            // Torres y áreas del proyecto, para acotar el alcance sin escribirlas a mano.
            var torres = new List<string>();
            var areas = new List<string>();
            var cmdAlc = new SqlCommand(
                "SELECT DISTINCT Torre, Metros FROM Inmuebles WHERE IdProyecto=@p", con);
            cmdAlc.Parameters.AddWithValue("@p", idProyecto);
            using (var r = (SqlDataReader)await cmdAlc.ExecuteReaderAsync())
                while (await r.ReadAsync())
                {
                    var t = r["Torre"]?.ToString() ?? "";
                    var m = r["Metros"]?.ToString() ?? "";
                    if (!string.IsNullOrWhiteSpace(t) && !torres.Contains(t)) torres.Add(t);
                    if (!string.IsNullOrWhiteSpace(m) && !areas.Contains(m)) areas.Add(m);
                }
            torres.Sort(StringComparer.OrdinalIgnoreCase);
            areas.Sort(StringComparer.OrdinalIgnoreCase);
            ViewBag.Torres = torres;
            ViewBag.Areas = areas;

            ViewBag.Torre = torre ?? "";
            ViewBag.Metros = metros ?? "";
            ViewBag.Listas = listas ?? "";
            bool porcentaje = tipo != "PESOS";
            ViewBag.Tipo = porcentaje ? "PORCENTAJE" : "PESOS";
            ViewBag.Valor = valor;

            if (previsualizar && idProyecto > 0 && valor != 0)
            {
                var afectados = await CalcularAjusteAsync(con, null, idProyecto, torre ?? "", metros ?? "",
                                                          listas ?? "", porcentaje, valor);
                ViewBag.Previa = afectados;
                ViewBag.PreviaUnidades = afectados.Select(a => a.IdInmueble).Distinct().Count();
                ViewBag.HayPrevia = true;
            }

            // Historial: los ajustes ya aplicados, para poder devolverlos.
            var historial = new List<dynamic>();
            var cmdHist = new SqlCommand(@"
                SELECT TOP 20 IdAjuste, Torre, Metros, Listas, Tipo, Valor, Unidades,
                       Usuario, Fecha, Revertido, FechaReversion
                FROM AjustesPrecio WHERE IdProyecto=@p ORDER BY Fecha DESC", con);
            cmdHist.Parameters.AddWithValue("@p", idProyecto);
            using (var r = (SqlDataReader)await cmdHist.ExecuteReaderAsync())
                while (await r.ReadAsync())
                    historial.Add(new
                    {
                        Id = Convert.ToInt64(r["IdAjuste"]),
                        Torre = r["Torre"]?.ToString() ?? "",
                        Metros = r["Metros"]?.ToString() ?? "",
                        Listas = r["Listas"]?.ToString() ?? "",
                        Tipo = r["Tipo"]?.ToString() ?? "",
                        Valor = Convert.ToDecimal(r["Valor"]),
                        Unidades = Convert.ToInt32(r["Unidades"]),
                        Usuario = r["Usuario"]?.ToString() ?? "",
                        // Las fechas se guardan en UTC; Colombia es UTC-5 todo el año.
                        Fecha = Convert.ToDateTime(r["Fecha"]).AddHours(-5),
                        Revertido = Convert.ToBoolean(r["Revertido"]),
                    });
            ViewBag.Historial = historial;

            return View();
        }

        /// <summary>
        /// Computes, without writing anything, the new price of every list of every property
        /// inside the requested scope. Both the preview and the apply step call this, so they
        /// can never disagree.
        /// </summary>
        private static async Task<List<(int IdInmueble, string Apto, string Torre, string Metros,
                                        int NumLista, long Anterior, long Nuevo)>>
            CalcularAjusteAsync(SqlConnection con, SqlTransaction? tx, int idProyecto, string torre,
                                string metros, string listas, bool porcentaje, decimal valor)
        {
            var numeros = ListasSeleccionadas(listas);

            // Los vendidos quedan fuera: su precio ya no es una oferta, es un hecho
            // registrado en la venta, y moverlo falsearía los informes.
            var sql = @"SELECT IdInmuebles, Apto, Torre, Metros, Lista1,Lista2,Lista3,Lista4,Lista5
                        FROM Inmuebles
                        WHERE IdProyecto=@p AND Estado <> 'VENDIDO'";
            if (!string.IsNullOrEmpty(torre))  sql += " AND Torre=@t";
            if (!string.IsNullOrEmpty(metros)) sql += " AND Metros=@m";
            sql += " ORDER BY Metros, Torre, Apto";

            var cmd = tx == null ? new SqlCommand(sql, con) : new SqlCommand(sql, con, tx);
            cmd.Parameters.AddWithValue("@p", idProyecto);
            if (!string.IsNullOrEmpty(torre))  cmd.Parameters.AddWithValue("@t", torre);
            if (!string.IsNullOrEmpty(metros)) cmd.Parameters.AddWithValue("@m", metros);

            var resultado = new List<(int, string, string, string, int, long, long)>();
            using (var r = (SqlDataReader)await cmd.ExecuteReaderAsync())
                while (await r.ReadAsync())
                {
                    int id = (int)r["IdInmuebles"];
                    var apto = r["Apto"]?.ToString() ?? "";
                    var tor = r["Torre"]?.ToString() ?? "";
                    var met = r["Metros"]?.ToString() ?? "";
                    foreach (var n in numeros)
                    {
                        long anterior = Texto.ParsearPrecio(r[Listas.ColumnaLista(n)]?.ToString());
                        if (anterior <= 0) continue;   // lista sin usar: no se inventa un precio
                        long nuevo = Plataforma_ventas.Listas.AjustarPrecio(anterior, valor, porcentaje);
                        if (nuevo == anterior) continue;
                        resultado.Add((id, apto, tor, met, n, anterior, nuevo));
                    }
                }
            return resultado;
        }

        /// <summary>
        /// Parses the list selection. An empty selection means "every list", which is the
        /// useful default: a price increase normally applies to the whole ladder.
        /// </summary>
        private static List<int> ListasSeleccionadas(string listas)
        {
            var numeros = new List<int>();
            foreach (var parte in (listas ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(parte.Trim(), out int n) && n >= 1 && n <= 5 && !numeros.Contains(n))
                    numeros.Add(n);
            if (numeros.Count == 0) numeros.AddRange(new[] { 1, 2, 3, 4, 5 });
            numeros.Sort();
            return numeros;
        }

        /// <summary>
        /// Applies a bulk price adjustment inside a transaction, saving the previous price of
        /// every list of every property so the adjustment can be undone later.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AplicarAjuste(int idProyecto, string torre = "", string metros = "",
            string listas = "", string tipo = "PORCENTAJE", decimal valor = 0)
        {
            if (idProyecto <= 0 || valor == 0)
            {
                TempData["Error"] = "Indica un proyecto y un valor distinto de cero.";
                return RedirectToAction("Precios", new { idProyecto });
            }

            bool porcentaje = tipo != "PESOS";
            if (porcentaje && (valor <= -100 || valor > 1000))
            {
                TempData["Error"] = "El porcentaje debe estar entre -99 % y 1000 %.";
                return RedirectToAction("Precios", new { idProyecto });
            }

            int idUsuario = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;
            var usuario = ((HttpContext.Session.GetString("Nombre") ?? "") + " " +
                           (HttpContext.Session.GetString("Apellido") ?? "")).Trim();

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            using var tx = con.BeginTransaction();
            try
            {
                var cambios = await CalcularAjusteAsync(con, tx, idProyecto, torre ?? "", metros ?? "",
                                                        listas ?? "", porcentaje, valor);
                if (cambios.Count == 0)
                {
                    TempData["Error"] = "El ajuste no cambia ningún precio.";
                    return RedirectToAction("Precios", new { idProyecto });
                }

                var cmdAj = new SqlCommand(@"INSERT INTO AjustesPrecio
                    (IdProyecto,Torre,Metros,Listas,Tipo,Valor,Unidades,IdUsuario,Usuario)
                    OUTPUT INSERTED.IdAjuste
                    VALUES (@p,@t,@m,@l,@tp,@v,@u,@iu,@us)", con, tx);
                cmdAj.Parameters.AddWithValue("@p", idProyecto);
                cmdAj.Parameters.AddWithValue("@t", torre ?? "");
                cmdAj.Parameters.AddWithValue("@m", metros ?? "");
                cmdAj.Parameters.AddWithValue("@l", string.Join(",", ListasSeleccionadas(listas ?? "")));
                cmdAj.Parameters.AddWithValue("@tp", porcentaje ? "PORCENTAJE" : "PESOS");
                cmdAj.Parameters.AddWithValue("@v", valor);
                cmdAj.Parameters.AddWithValue("@u", cambios.Select(c => c.IdInmueble).Distinct().Count());
                cmdAj.Parameters.AddWithValue("@iu", idUsuario > 0 ? (object)idUsuario : DBNull.Value);
                cmdAj.Parameters.AddWithValue("@us", usuario);
                long idAjuste = Convert.ToInt64((await cmdAj.ExecuteScalarAsync())!);

                foreach (var c in cambios)
                {
                    var col = Listas.ColumnaLista(c.NumLista);
                    var cmdUpd = new SqlCommand(
                        $"UPDATE Inmuebles SET {col}=@precio WHERE IdInmuebles=@id", con, tx);
                    cmdUpd.Parameters.AddWithValue("@precio", c.Nuevo);
                    cmdUpd.Parameters.AddWithValue("@id", c.IdInmueble);
                    await cmdUpd.ExecuteNonQueryAsync();

                    var cmdDet = new SqlCommand(@"INSERT INTO AjustesPrecioDetalle
                        (IdAjuste,IdInmueble,NumLista,PrecioAnterior,PrecioNuevo)
                        VALUES (@a,@i,@n,@ant,@nue)", con, tx);
                    cmdDet.Parameters.AddWithValue("@a", idAjuste);
                    cmdDet.Parameters.AddWithValue("@i", c.IdInmueble);
                    cmdDet.Parameters.AddWithValue("@n", c.NumLista);
                    cmdDet.Parameters.AddWithValue("@ant", c.Anterior);
                    cmdDet.Parameters.AddWithValue("@nue", c.Nuevo);
                    await cmdDet.ExecuteNonQueryAsync();
                }

                tx.Commit();

                await AvisarPreciosAsync(idProyecto, cambios);

                int unidades = cambios.Select(c => c.IdInmueble).Distinct().Count();
                TempData["Exito"] = $"Ajuste aplicado a {unidades} inmuebles ({cambios.Count} precios). " +
                                    "Puedes devolverlo desde el historial.";
            }
            catch (Exception ex)
            {
                // El using hace rollback al salir sin Commit: o se aplica todo o nada.
                TempData["Error"] = "No se pudo aplicar el ajuste: " + ex.Message;
            }

            return RedirectToAction("Precios", new { idProyecto });
        }

        /// <summary>
        /// Broadcasts the new price of every (area, list) pair that ended up with a single
        /// price, so open vendor screens don't keep quoting the old one. A pair whose
        /// properties ended with different prices is skipped: there is no single number to
        /// send, and a wrong one is worse than none — those screens refresh on their own.
        /// </summary>
        private async Task AvisarPreciosAsync(int idProyecto,
            IEnumerable<(int IdInmueble, string Apto, string Torre, string Metros,
                         int NumLista, long Anterior, long Nuevo)> cambios)
        {
            foreach (var grupo in cambios.GroupBy(c => (c.Metros, c.NumLista)))
            {
                var precios = grupo.Select(c => c.Nuevo).Distinct().ToList();
                if (precios.Count != 1) continue;
                await _hub.Clients.All.PrecioAreaActualizado(
                    idProyecto, grupo.Key.Metros, grupo.Key.NumLista, precios[0]);
            }
        }

        /// <summary>
        /// Undoes a previously applied adjustment, restoring each saved previous price.
        /// A price that changed again after the adjustment is left alone and reported:
        /// restoring it would silently discard the newer, deliberate change.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RevertirAjuste(long idAjuste, int idProyecto)
        {
            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            var cmdEstado = new SqlCommand(
                "SELECT Revertido, IdProyecto FROM AjustesPrecio WHERE IdAjuste=@a", con);
            cmdEstado.Parameters.AddWithValue("@a", idAjuste);
            bool yaRevertido = false; int proyDelAjuste = 0;
            using (var r = (SqlDataReader)await cmdEstado.ExecuteReaderAsync())
                if (await r.ReadAsync())
                {
                    yaRevertido = Convert.ToBoolean(r["Revertido"]);
                    proyDelAjuste = Convert.ToInt32(r["IdProyecto"]);
                }

            if (proyDelAjuste == 0)
            {
                TempData["Error"] = "El ajuste ya no existe.";
                return RedirectToAction("Precios", new { idProyecto });
            }
            if (yaRevertido)
            {
                TempData["Error"] = "Ese ajuste ya se había devuelto.";
                return RedirectToAction("Precios", new { idProyecto = proyDelAjuste });
            }

            // Se trae el área junto al detalle para poder avisar por SignalR qué precio
            // queda vigente en cada área tras devolver el ajuste.
            var detalle = new List<(int IdInmueble, string Metros, int NumLista, long Anterior, long Nuevo)>();
            var cmdDet = new SqlCommand(@"
                SELECT d.IdInmueble, ISNULL(i.Metros,'') AS Metros, d.NumLista,
                       d.PrecioAnterior, d.PrecioNuevo
                FROM AjustesPrecioDetalle d
                LEFT JOIN Inmuebles i ON i.IdInmuebles = d.IdInmueble
                WHERE d.IdAjuste=@a", con);
            cmdDet.Parameters.AddWithValue("@a", idAjuste);
            using (var r = (SqlDataReader)await cmdDet.ExecuteReaderAsync())
                while (await r.ReadAsync())
                    detalle.Add(((int)r["IdInmueble"], r["Metros"]?.ToString() ?? "",
                                 Convert.ToInt32(r["NumLista"]),
                                 Convert.ToInt64(r["PrecioAnterior"]), Convert.ToInt64(r["PrecioNuevo"])));

            using var tx = con.BeginTransaction();
            int restaurados = 0, omitidos = 0;
            var restauradosDet = new List<(int, string, string, string, int, long, long)>();
            try
            {
                foreach (var d in detalle)
                {
                    var col = Listas.ColumnaLista(d.NumLista);
                    // El WHERE sobre el precio que dejó el ajuste es la protección: si alguien
                    // lo cambió después, esa fila no se toca.
                    var cmdUpd = new SqlCommand(
                        $"UPDATE Inmuebles SET {col}=@ant WHERE IdInmuebles=@id AND {col}=@nue", con, tx);
                    cmdUpd.Parameters.AddWithValue("@ant", d.Anterior);
                    cmdUpd.Parameters.AddWithValue("@nue", d.Nuevo);
                    cmdUpd.Parameters.AddWithValue("@id", d.IdInmueble);
                    if (await cmdUpd.ExecuteNonQueryAsync() > 0)
                    {
                        restaurados++;
                        // El precio que queda vigente es el anterior al ajuste.
                        restauradosDet.Add((d.IdInmueble, "", "", d.Metros, d.NumLista, d.Nuevo, d.Anterior));
                    }
                    else omitidos++;
                }

                var cmdMarca = new SqlCommand(
                    "UPDATE AjustesPrecio SET Revertido=1, FechaReversion=GETUTCDATE() WHERE IdAjuste=@a", con, tx);
                cmdMarca.Parameters.AddWithValue("@a", idAjuste);
                await cmdMarca.ExecuteNonQueryAsync();

                tx.Commit();

                await AvisarPreciosAsync(proyDelAjuste, restauradosDet);

                TempData["Exito"] = omitidos > 0
                    ? $"Ajuste devuelto: {restaurados} precios restaurados. {omitidos} no se tocaron porque cambiaron después."
                    : $"Ajuste devuelto: {restaurados} precios restaurados.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "No se pudo devolver el ajuste: " + ex.Message;
            }

            return RedirectToAction("Precios", new { idProyecto = proyDelAjuste });
        }

        /// <summary>
        /// Regenerates the access code for an existing project.
        /// Performs an UPDATE query on Proyectos.
        /// </summary>
        /// <param name="idProyecto">Project whose code to regenerate.</param>
        /// <param name="nombreProyecto">Project name (used as prefix in the new code).</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegenerarCodigo(int idProyecto, string nombreProyecto)
        {
            string nuevoCodigo = GenerarCodigo(nombreProyecto);
            using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            var cmd = new SqlCommand("UPDATE Proyectos SET CodigoAcceso=@c WHERE IdProyectos=@id", con);
            cmd.Parameters.AddWithValue("@c", nuevoCodigo);
            cmd.Parameters.AddWithValue("@id", idProyecto);
            await cmd.ExecuteNonQueryAsync();
            TempData["Exito"] = "Código regenerado correctamente.";
            TempData["Codigo"] = nuevoCodigo;
            return RedirectToAction("Index");
        }

        /// <summary>
        /// Permanently deletes a project and all its associated data (sales, properties,
        /// vendor assignments) in the correct FK order inside a transaction.
        /// Clears the session if the deleted project was the active one.
        /// Performs DELETE/UPDATE queries wrapped in a transaction for atomicity.
        /// </summary>
        /// <param name="idProyecto">Project identifier to delete.</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarProyecto(int idProyecto)
        {
            using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            using var tx = con.BeginTransaction();
            try
            {
                async Task Exec(string sql)
                {
                    var cmd = new SqlCommand(sql, con, tx);
                    cmd.Parameters.AddWithValue("@id", idProyecto);
                    await cmd.ExecuteNonQueryAsync();
                }

                // Orden obligatorio para respetar todas las FK:
                // 1. Ventas (FK → Proyectos, Inmuebles, Clientes, Usuarios)
                await Exec("DELETE FROM Ventas WHERE IdProyecto = @id");

                // 2. Clientes (FK → Proyectos)
                await Exec("UPDATE Clientes SET IdProyecto = NULL WHERE IdProyecto = @id");

                // 3. Inmuebles (FK → Proyectos)
                await Exec("DELETE FROM Inmuebles WHERE IdProyecto = @id");

                // 4. Usuarios — desasignar vendedores (FK → Proyectos)
                await Exec("UPDATE Usuarios SET IdProyecto = NULL WHERE IdProyecto = @id");

                // 5. Proyectos hijos si los hay (FK → Proyectos padre)
                await Exec("UPDATE Proyectos SET IdProyecto = NULL WHERE IdProyecto = @id");

                // 6. Por último eliminar el proyecto
                await Exec("DELETE FROM Proyectos WHERE IdProyectos = @id");

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

        /// <summary>
        /// Returns the placeholder client used by sales imported from the spreadsheet,
        /// creating it once per project. It is a single row on purpose: completing a sale
        /// repoints it to the real client, so the placeholder is left behind empty instead
        /// of filling the client list with one fake record per imported sale.
        /// </summary>
        private static async Task<int> ClientePorRegistrarAsync(SqlConnection con, SqlTransaction tx,
                                                                string nombreProyecto)
        {
            var documento = "IMPORTADO-" + nombreProyecto.ToUpper();

            var cmdBusca = new SqlCommand(
                "SELECT TOP 1 IdCliente FROM Clientes WHERE Documento=@d ORDER BY IdCliente", con, tx);
            cmdBusca.Parameters.AddWithValue("@d", documento);
            var existente = await cmdBusca.ExecuteScalarAsync();
            if (existente != null && existente != DBNull.Value) return Convert.ToInt32(existente);

            var cmdCrea = new SqlCommand(@"INSERT INTO Clientes
                (Nombre,Apellido,Documento,Celular,Correo,Direccion)
                OUTPUT INSERTED.IdCliente
                VALUES ('Por registrar', @proy, @d, '', '', '')", con, tx);
            cmdCrea.Parameters.AddWithValue("@proy", nombreProyecto);
            cmdCrea.Parameters.AddWithValue("@d", documento);
            return Convert.ToInt32((await cmdCrea.ExecuteScalarAsync())!);
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
