using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using OfficeOpenXml;
using System.Text.Json;
using OfficeOpenXml.Style;
using Plataforma_ventas.Filters;
using DColor = System.Drawing.Color;

namespace Plataforma_ventas.Controllers
{
    /// <summary>
    /// Administrator controller for user management:
    /// listing, creating, editing, resetting passwords, and deleting user accounts.
    /// </summary>
    [RolAutorizado("Administrador", "SuperAdministrador")]
    public class UsuariosController : Controller
    {
        private readonly string _conn;
        private readonly Plataforma_ventas.Services.IBloqueoService _bloqueo;
        private readonly Plataforma_ventas.Services.IAuditoriaService _audit;

        /// <summary>Initializes the controller with DB connection string from configuration.</summary>
        public UsuariosController(IConfiguration config,
                                  Plataforma_ventas.Services.IBloqueoService bloqueo,
                                  Plataforma_ventas.Services.IAuditoriaService audit)
        {
            _conn = config.GetConnectionString("DefaultConnection")!;
            _bloqueo = bloqueo;
            _audit = audit;
        }

        /// <summary>
        /// Lists all users in the system with their assigned project and sale count.
        /// Performs SELECT queries on Usuarios, Proyectos, and Ventas.
        /// </summary>
        public async Task<IActionResult> Index()
        {
            ViewBag.Nombre = HttpContext.Session.GetString("Nombre") ?? "Admin";
            ViewBag.Apellido = HttpContext.Session.GetString("Apellido") ?? "";
            ViewBag.ProyectoActivo = HttpContext.Session.GetString("ProyectoNombre") ?? "Sin proyecto";

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            // Todos los proyectos activos — para sidebar y formulario de creación/edición
            var proyectos = new List<(int Id, string Nombre)>();
            var cmdList = new SqlCommand(
                "SELECT IdProyectos, Nombre FROM Proyectos WHERE Activo=1 ORDER BY FechaCarga DESC", con);
            using (var r = (SqlDataReader)await cmdList.ExecuteReaderAsync())
                while (await r.ReadAsync())
                    proyectos.Add(((int)r["IdProyectos"], r["Nombre"]?.ToString() ?? ""));
            ViewBag.Proyectos = proyectos;

            // La columna de estado se agregó después; sin ella todo se ve como activo.
            var cmdColAct = new SqlCommand("SELECT COL_LENGTH('Usuarios','Activo')", con);
            bool hayActivo = (await cmdColAct.ExecuteScalarAsync()) is not (null or DBNull);
            ViewBag.HayEstadoCuenta = hayActivo;

            // Todos los usuarios del sistema con su proyecto asignado
            var usuarios = new List<dynamic>();
            var cmd = new SqlCommand($@"
                SELECT u.IdUsuario, u.Nombre, u.Apellido, u.Usuario, u.Correo,
                       u.Documento, u.Celular, u.Rol, u.IdProyecto,
                       {(hayActivo ? "ISNULL(u.Activo,1)" : "CAST(1 AS BIT)")} AS Activo,
                       ISNULL(p.Nombre, '—') AS NombreProyecto,
                       COUNT(v.IdVenta) AS TotalVentas
                FROM Usuarios u
                LEFT JOIN Proyectos p ON u.IdProyecto = p.IdProyectos
                LEFT JOIN Ventas    v ON u.IdUsuario  = v.IdUsuario
                GROUP BY u.IdUsuario, u.Nombre, u.Apellido, u.Usuario, u.Correo,
                         u.Documento, u.Celular, u.Rol, u.IdProyecto, p.Nombre
                         {(hayActivo ? ", u.Activo" : "")}
                ORDER BY u.Rol DESC, p.Nombre, u.Nombre", con);

            using (var reader = (SqlDataReader)await cmd.ExecuteReaderAsync())
                while (await reader.ReadAsync())
                    usuarios.Add(new
                    {
                        Id            = (int)reader["IdUsuario"],
                        Nombre        = reader["Nombre"]?.ToString()        ?? "",
                        Apellido      = reader["Apellido"]?.ToString()      ?? "",
                        Usuario       = reader["Usuario"]?.ToString()       ?? "",
                        Correo        = reader["Correo"]?.ToString()        ?? "",
                        Documento     = reader["Documento"]?.ToString()     ?? "",
                        Celular       = reader["Celular"]?.ToString()       ?? "",
                        Rol           = reader["Rol"]?.ToString()           ?? "",
                        IdProyecto    = reader["IdProyecto"] == DBNull.Value ? 0 : (int)reader["IdProyecto"],
                        NombreProyecto= reader["NombreProyecto"]?.ToString() ?? "—",
                        TotalVentas   = (int)reader["TotalVentas"],
                        Activo        = reader["Activo"] == DBNull.Value || (bool)reader["Activo"],
                    });

            // El Admin no ve ni puede gestionar SuperAdministradores
            string rolActual = HttpContext.Session.GetString("Rol") ?? "";
            if (rolActual != "SuperAdministrador")
                usuarios = usuarios.Where(u => u.Rol != "SuperAdministrador").ToList();

            ViewBag.Usuarios        = usuarios;
            ViewBag.TotalUsuarios   = usuarios.Count;
            ViewBag.TotalAdmins     = usuarios.Count(u => u.Rol == "Administrador" || u.Rol == "SuperAdministrador");
            ViewBag.TotalVendedores = usuarios.Count(u => u.Rol == "Vendedor");
            ViewBag.TotalInactivos  = usuarios.Count(u => !u.Activo);
            ViewBag.TotalProyectos  = proyectos.Count;
            ViewBag.RolActual       = HttpContext.Session.GetString("Rol") ?? "";
            // Cuentas bloqueadas por intentos fallidos, para poder liberarlas sin
            // esperar los 15 minutos.
            ViewBag.Bloqueadas      = _bloqueo.Listar();
            return View();
        }

        /// <summary>
        /// Exports the user list to Excel. Passwords and their hashes never leave the
        /// database: the file is for the commercial area, who use it to check who has an
        /// account and on which project, not to audit credentials.
        /// Performs SELECT queries on Usuarios, Proyectos and Ventas.
        /// </summary>
        public async Task<IActionResult> Exportar()
        {
            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            var usuarios = new List<(string Nombre, string Apellido, string Usuario, string Rol,
                                     string Correo, string Documento, string Celular,
                                     string Proyecto, int Ventas)>();
            var cmd = new SqlCommand(@"
                SELECT u.Nombre, u.Apellido, u.Usuario, u.Rol,
                       ISNULL(u.Correo,'') AS Correo, ISNULL(u.Documento,'') AS Documento,
                       ISNULL(u.Celular,'') AS Celular,
                       ISNULL(p.Nombre, '') AS NombreProyecto,
                       COUNT(v.IdVenta) AS TotalVentas
                FROM Usuarios u
                LEFT JOIN Proyectos p ON u.IdProyecto = p.IdProyectos
                LEFT JOIN Ventas    v ON u.IdUsuario  = v.IdUsuario
                GROUP BY u.IdUsuario, u.Nombre, u.Apellido, u.Usuario, u.Correo,
                         u.Documento, u.Celular, u.Rol, p.Nombre
                ORDER BY u.Rol DESC, p.Nombre, u.Nombre", con);
            using (var r = (SqlDataReader)await cmd.ExecuteReaderAsync())
                while (await r.ReadAsync())
                    usuarios.Add((
                        r["Nombre"]?.ToString() ?? "",
                        r["Apellido"]?.ToString() ?? "",
                        r["Usuario"]?.ToString() ?? "",
                        r["Rol"]?.ToString() ?? "",
                        r["Correo"]?.ToString() ?? "",
                        r["Documento"]?.ToString() ?? "",
                        r["Celular"]?.ToString() ?? "",
                        r["NombreProyecto"]?.ToString() ?? "",
                        Convert.ToInt32(r["TotalVentas"])));

            // Un administrador no puede ver superadministradores en pantalla; tampoco en el archivo.
            if ((HttpContext.Session.GetString("Rol") ?? "") != "SuperAdministrador")
                usuarios = usuarios.Where(x => x.Rol != "SuperAdministrador").ToList();

            var bloqueadas = _bloqueo.Listar()
                .Select(b => b.Usuario)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            ExcelPackage.License.SetNonCommercialPersonal("Londoño Gómez");
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Usuarios");

            ws.Cells[1, 1].Value = "Usuarios de la plataforma";
            ws.Cells[1, 1].Style.Font.Bold = true;
            ws.Cells[1, 1].Style.Font.Size = 14;
            ws.Cells[1, 1].Style.Font.Color.SetColor(DColor.FromArgb(0, 58, 112));
            ws.Cells[1, 1, 1, 9].Merge = true;

            int admins = usuarios.Count(x => x.Rol is "Administrador" or "SuperAdministrador");
            int vendedores = usuarios.Count(x => x.Rol == "Vendedor");
            ws.Cells[2, 1].Value = $"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}  ·  {usuarios.Count} usuarios  ·  " +
                                   $"{admins} administradores  ·  {vendedores} asesores";
            ws.Cells[2, 1].Style.Font.Color.SetColor(DColor.Gray);
            ws.Cells[2, 1, 2, 9].Merge = true;

            var headers = new[] { "Nombre", "Apellido", "Usuario", "Rol", "Proyecto asignado",
                                  "Celular", "Correo", "Documento", "Ventas", "Estado" };
            for (int i = 0; i < headers.Length; i++)
            {
                var c = ws.Cells[4, i + 1];
                c.Value = headers[i];
                c.Style.Font.Bold = true;
                c.Style.Font.Color.SetColor(DColor.White);
                c.Style.Fill.PatternType = ExcelFillStyle.Solid;
                c.Style.Fill.BackgroundColor.SetColor(DColor.FromArgb(0, 58, 112));
                c.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            }

            int row = 5;
            foreach (var x in usuarios)
            {
                ws.Cells[row, 1].Value = x.Nombre;
                ws.Cells[row, 2].Value = x.Apellido;
                ws.Cells[row, 3].Value = x.Usuario;
                ws.Cells[row, 4].Value = x.Rol;
                ws.Cells[row, 5].Value = string.IsNullOrWhiteSpace(x.Proyecto) ? "Sin asignar" : x.Proyecto;
                if (string.IsNullOrWhiteSpace(x.Proyecto))
                    ws.Cells[row, 5].Style.Font.Color.SetColor(DColor.FromArgb(150, 150, 150));
                ws.Cells[row, 6].Value = x.Celular;
                ws.Cells[row, 7].Value = x.Correo;
                ws.Cells[row, 8].Value = x.Documento;
                ws.Cells[row, 9].Value = x.Ventas;
                ws.Cells[row, 9].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                // Una cuenta bloqueada es alguien que no va a poder entrar al lanzamiento:
                // por eso queda marcada y no escondida en un panel aparte.
                bool bloqueada = bloqueadas.Contains(x.Usuario);
                ws.Cells[row, 10].Value = bloqueada ? "BLOQUEADA" : "Activa";
                if (bloqueada)
                {
                    ws.Cells[row, 10].Style.Font.Bold = true;
                    ws.Cells[row, 10].Style.Font.Color.SetColor(DColor.FromArgb(230, 57, 70));
                }
                row++;
            }

            if (usuarios.Count > 0)
                ws.Cells[4, 1, row - 1, headers.Length].AutoFilter = true;
            for (int c = 1; c <= headers.Length; c++) ws.Column(c).AutoFit();

            var bytes = await package.GetAsByteArrayAsync();
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Usuarios_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        }

        /// <summary>
        /// Creates a new user account. Validates username uniqueness before inserting.
        /// Passwords are hashed with BCrypt (cost factor 12) before storage — never stored in plain text.
        /// Only name, surname and mobile are collected: the account is created and reset by an
        /// administrator, so document and e-mail were data nobody used.
        /// Performs SELECT (uniqueness check) and INSERT queries on Usuarios.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(string nombre, string apellido,
            string celular, string usuario, string contrasena,
            string rol, int idProyecto)
        {
            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            // Solo se valida el nombre de usuario. Comparar también el correo cuando ya
            // no se pide haría que el primer usuario sin correo bloqueara a todos los
            // demás, porque todos compartirían la cadena vacía.
            var cmdCheck = new SqlCommand("SELECT COUNT(*) FROM Usuarios WHERE Usuario=@u", con);
            cmdCheck.Parameters.AddWithValue("@u", usuario ?? "");
            if ((int)(await cmdCheck.ExecuteScalarAsync())! > 0)
            {
                TempData["Error"] = "El nombre de usuario ya está en uso.";
                return RedirectToAction("Index");
            }

            // Solo el SuperAdministrador puede crear Administradores
            string rolSesion = HttpContext.Session.GetString("Rol") ?? "";
            string rolFinal = (rolSesion == "SuperAdministrador" && rol == "Administrador")
                ? "Administrador"
                : "Vendedor";
            object proyParam = (rolFinal == "Vendedor" && idProyecto > 0)
                ? (object)idProyecto
                : DBNull.Value;

            // Documento y Correo siguen existiendo en la tabla por los usuarios ya
            // creados; los nuevos se insertan vacíos porque ya no se piden.
            var cmd = new SqlCommand(@"
                INSERT INTO Usuarios (Nombre,Apellido,Documento,Celular,Correo,Usuario,Contraseña,Rol,IdProyecto)
                VALUES (@n,@a,'',@c,'',@u,@p,@r,@proy)", con);
            cmd.Parameters.AddWithValue("@n",    nombre   ?? "");
            cmd.Parameters.AddWithValue("@a",    apellido ?? "");
            cmd.Parameters.AddWithValue("@c",    celular  ?? "");
            cmd.Parameters.AddWithValue("@u",    usuario  ?? "");
            // BCrypt genera una sal aleatoria embebida en el hash (factor de coste = 12)
            cmd.Parameters.AddWithValue("@p",    BCrypt.Net.BCrypt.HashPassword(contrasena ?? "", 12));
            cmd.Parameters.AddWithValue("@r",    rolFinal);
            cmd.Parameters.AddWithValue("@proy", proyParam);

            try
            {
                await cmd.ExecuteNonQueryAsync();
            }
            // 2601/2627: clave duplicada. Al dejar de pedir documento y correo, todos los
            // usuarios nuevos los comparten vacíos, así que una restricción UNIQUE sobre
            // esas columnas deja crear el primero y rechaza los siguientes. La sección 13
            // del script la retira; mientras tanto, esto lo explica en vez de dar un 500.
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
            {
                bool porContactoVacio = ex.Message.Contains("Correo", StringComparison.OrdinalIgnoreCase)
                                     || ex.Message.Contains("Documento", StringComparison.OrdinalIgnoreCase);
                TempData["Error"] = porContactoVacio
                    ? "La base de datos todavía exige documento o correo únicos, y esos datos ya no se piden. " +
                      "Ejecuta la sección 13 de Scripts/PanelAdmin.sql y vuelve a intentarlo."
                    : "Ya existe un usuario con esos datos.";
                return RedirectToAction("Index");
            }

            TempData["Exito"] = $"Usuario '{usuario}' ({rolFinal}) creado correctamente.";
            return RedirectToAction("Index");
        }

        // ══════════════════════ IMPORTACIÓN MASIVA DESDE EXCEL ══════════════════════
        // El área comercial llega con la lista de asesores en un Excel. Crearlos uno por
        // uno antes de un lanzamiento es media hora de digitación y un error seguro.
        // La importación es en dos pasos: primero se muestra qué entendió el sistema y
        // solo después se escribe. Nada se guarda sin que alguien lo confirme viéndolo.

        private const string SesionFilas = "ImportUsuariosFilas";
        private const string SesionCreds = "ImportUsuariosCreds";

        /// <summary>Pantalla de importación, en su primer paso: subir el archivo.</summary>
        [HttpGet]
        public IActionResult Importar()
        {
            ViewBag.Nombre = HttpContext.Session.GetString("Nombre") ?? "Admin";
            ViewBag.Apellido = HttpContext.Session.GetString("Apellido") ?? "";
            ViewBag.ProyectoActivo = HttpContext.Session.GetString("ProyectoNombre") ?? "Sin proyecto";
            ViewBag.Paso = "subir";
            return View();
        }

        /// <summary>
        /// Descarga la plantilla vacía. Que la plantilla la genere la misma aplicación
        /// evita el archivo que circula por correo con las columnas de una versión vieja.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> PlantillaUsuarios()
        {
            ExcelPackage.License.SetNonCommercialPersonal("Londoño Gómez");
            using var paquete = new ExcelPackage();
            var ws = paquete.Workbook.Worksheets.Add("Usuarios");

            var cabeceras = new[] { "NOMBRE", "APELLIDO", "USUARIO", "CORREO", "TEL", "ROL", "CONTRASEÑA" };
            for (int i = 0; i < cabeceras.Length; i++)
            {
                var c = ws.Cells[1, i + 1];
                c.Value = cabeceras[i];
                c.Style.Font.Bold = true;
                c.Style.Font.Color.SetColor(DColor.White);
                c.Style.Fill.PatternType = ExcelFillStyle.Solid;
                c.Style.Fill.BackgroundColor.SetColor(DColor.FromArgb(0, 58, 112));
                c.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            }
            ws.Cells[2, 1].Value = "Ana María";
            ws.Cells[2, 2].Value = "Castaño Peláez";
            ws.Cells[2, 3].Value = "AnaCastano";
            ws.Cells[2, 4].Value = "acastano@ejemplo.com";
            ws.Cells[2, 5].Value = "3001234567";
            ws.Cells[2, 6].Value = "Vendedor";
            ws.Cells[2, 1, 2, 7].Style.Font.Italic = true;
            ws.Cells[2, 1, 2, 7].Style.Font.Color.SetColor(DColor.Gray);
            for (int c = 1; c <= cabeceras.Length; c++) ws.Column(c).Width = c == 4 ? 34 : 20;
            ws.View.FreezePanes(2, 1);

            var ins = paquete.Workbook.Worksheets.Add("Instrucciones");
            var lineas = new[]
            {
                "Plantilla de importación de usuarios",
                "",
                "Una fila por persona. La primera fila son los títulos y no se toca.",
                "La fila de ejemplo, en gris, se puede borrar.",
                "",
                "NOMBRE y APELLIDO   Separados. Los apellidos compuestos van completos en APELLIDO.",
                "USUARIO             Con el que inicia sesión. No puede repetirse ni llevar espacios.",
                "CORREO              Opcional. Sirve para recuperar la contraseña.",
                "TEL                 Celular.",
                "ROL                 Vendedor o Administrador. Vacío se entiende como Vendedor.",
                "CONTRASEÑA          Déjala vacía: el sistema genera una distinta por persona y",
                "                    muestra la lista una sola vez al terminar.",
                "",
                "Antes de guardar nada se muestra qué entendió el sistema: cuántos se crean,",
                "cuántos ya existen y qué filas tienen problemas.",
                "",
                "Un archivo con las contraseñas escritas es una lista de credenciales:",
                "si las escribes, bórralo apenas termines de importar.",
            };
            for (int i = 0; i < lineas.Length; i++) ins.Cells[i + 1, 1].Value = lineas[i];
            ins.Cells[1, 1].Style.Font.Bold = true;
            ins.Cells[1, 1].Style.Font.Size = 13;
            ins.Column(1).Width = 95;

            var bytes = await paquete.GetAsByteArrayAsync();
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        "Plantilla_usuarios.xlsx");
        }

        /// <summary>
        /// Segundo paso: lee el archivo, clasifica cada fila y muestra la vista previa.
        /// No escribe nada en la base.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(5 * 1024 * 1024)]
        public async Task<IActionResult> Importar(IFormFile archivo)
        {
            ViewBag.Nombre = HttpContext.Session.GetString("Nombre") ?? "Admin";
            ViewBag.Apellido = HttpContext.Session.GetString("Apellido") ?? "";
            ViewBag.ProyectoActivo = HttpContext.Session.GetString("ProyectoNombre") ?? "Sin proyecto";
            ViewBag.Paso = "subir";

            if (archivo == null || archivo.Length == 0)
            {
                ViewBag.Error = "Debes seleccionar un archivo.";
                return View();
            }
            if (Path.GetExtension(archivo.FileName).ToLowerInvariant() != ".xlsx")
            {
                ViewBag.Error = "El archivo debe ser un Excel con extensión .xlsx.";
                return View();
            }

            var filas = new List<ImportacionUsuarios.Fila>();
            try
            {
                using var stream = new MemoryStream();
                await archivo.CopyToAsync(stream);
                stream.Position = 0;
                ExcelPackage.License.SetNonCommercialPersonal("Londoño Gómez");
                using var paquete = new ExcelPackage(stream);

                var hoja = paquete.Workbook.Worksheets.FirstOrDefault(h => h.Dimension != null && h.Dimension.Rows >= 2);
                if (hoja == null)
                {
                    ViewBag.Error = "El archivo no tiene datos.";
                    return View();
                }

                // Las columnas se buscan por título, no por posición: así el archivo se
                // puede reordenar y sigue funcionando.
                var mapa = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int c = 1; c <= hoja.Dimension.Columns; c++)
                {
                    var t = ImportacionUsuarios.LimpiarTexto(hoja.Cells[1, c].Text).ToUpperInvariant();
                    if (t.Length > 0 && !mapa.ContainsKey(t)) mapa[t] = c;
                }
                int Col(params string[] alias)
                {
                    foreach (var a in alias) if (mapa.TryGetValue(a, out int c)) return c;
                    return -1;
                }

                int colNombre   = Col("NOMBRE", "NOMBRES");
                int colApellido = Col("APELLIDO", "APELLIDOS");
                int colCompleto = Col("NOMBRE COMPLETO", "NOMBRECOMPLETO");
                int colUsuario  = Col("USUARIO");
                int colCorreo   = Col("CORREO", "EMAIL", "E-MAIL");
                int colTel      = Col("TEL", "TELEFONO", "TELÉFONO", "CELULAR");
                int colRol      = Col("ROL", "PERFIL");
                int colPass     = Col("CONTRASEÑA", "CONTRASENA", "PASSWORD", "CLAVE");

                if (colUsuario < 0)
                {
                    ViewBag.Error = "No se encontró la columna USUARIO. Descarga la plantilla y vuelve a intentarlo.";
                    return View();
                }
                if (colNombre < 0 && colCompleto < 0)
                {
                    ViewBag.Error = "No se encontró la columna NOMBRE (ni NOMBRE COMPLETO). " +
                                    "Descarga la plantilla y vuelve a intentarlo.";
                    return View();
                }

                // Usuarios que ya existen. La tabla es pequeña, así que se traen todos de
                // una vez en lugar de consultar por cada fila del archivo.
                var existentes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var con = new SqlConnection(_conn))
                {
                    await con.OpenAsync();
                    var cmdEx = new SqlCommand("SELECT Usuario FROM Usuarios", con);
                    using var r = (SqlDataReader)await cmdEx.ExecuteReaderAsync();
                    while (await r.ReadAsync()) existentes.Add(r["Usuario"]?.ToString() ?? "");
                }

                bool esSuper = (HttpContext.Session.GetString("Rol") ?? "") == "SuperAdministrador";
                var vistosEnArchivo = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                for (int f = 2; f <= hoja.Dimension.Rows; f++)
                {
                    string nombre, apellido;
                    if (colNombre > 0)
                    {
                        nombre   = ImportacionUsuarios.TituloNombre(hoja.Cells[f, colNombre].Text);
                        apellido = colApellido > 0
                            ? ImportacionUsuarios.TituloNombre(hoja.Cells[f, colApellido].Text) : "";
                    }
                    else
                    {
                        (nombre, apellido) = ImportacionUsuarios.PartirNombre(hoja.Cells[f, colCompleto].Text);
                    }

                    string usuario = ImportacionUsuarios.LimpiarTexto(colUsuario > 0 ? hoja.Cells[f, colUsuario].Text : "");
                    string correo  = ImportacionUsuarios.LimpiarTexto(colCorreo  > 0 ? hoja.Cells[f, colCorreo].Text  : "").ToLowerInvariant();
                    string tel     = ImportacionUsuarios.LimpiarTexto(colTel     > 0 ? hoja.Cells[f, colTel].Text     : "");
                    string rolCrudo = colRol > 0 ? hoja.Cells[f, colRol].Text : "";
                    string pass    = ImportacionUsuarios.LimpiarTexto(colPass    > 0 ? hoja.Cells[f, colPass].Text    : "");

                    // Fila completamente vacía: es el final del archivo o un hueco.
                    if (nombre.Length == 0 && usuario.Length == 0 && correo.Length == 0) continue;

                    string rol = ImportacionUsuarios.NormalizarRol(rolCrudo);
                    string estado = "CREAR", motivo = "";

                    var problema = ImportacionUsuarios.MotivoDeRechazo(nombre, usuario, rol, correo, pass);
                    if (problema != null) { estado = "ERROR"; motivo = problema; }
                    else if (rol == "Administrador" && !esSuper)
                    {
                        estado = "ERROR";
                        motivo = "Solo un SuperAdministrador puede crear administradores";
                    }
                    else if (existentes.Contains(usuario))
                    {
                        estado = "EXISTE"; motivo = "Ya hay un usuario con ese nombre";
                    }
                    else if (!vistosEnArchivo.Add(usuario))
                    {
                        estado = "ERROR"; motivo = "El usuario está repetido dentro del archivo";
                    }

                    filas.Add(new ImportacionUsuarios.Fila(f, nombre, apellido, usuario, correo, tel,
                                                           rol, pass, estado, motivo));
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = "No se pudo leer el archivo: " + ex.Message;
                return View();
            }

            if (filas.Count == 0)
            {
                ViewBag.Error = "El archivo no tiene filas con datos.";
                return View();
            }

            // Solo viajan a la sesión las filas que se van a crear: es lo único que el
            // paso de confirmación necesita, y así no se guarda de más.
            HttpContext.Session.SetString(SesionFilas,
                JsonSerializer.Serialize(filas.Where(x => x.Estado == "CREAR").ToList()));

            ViewBag.Paso = "previsualizar";
            ViewBag.Filas = filas;
            ViewBag.NombreArchivo = archivo.FileName;
            return View();
        }

        /// <summary>
        /// Tercer paso: crea las cuentas que quedaron marcadas para crear y muestra las
        /// contraseñas generadas una sola vez.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmarImportacion()
        {
            ViewBag.Nombre = HttpContext.Session.GetString("Nombre") ?? "Admin";
            ViewBag.Apellido = HttpContext.Session.GetString("Apellido") ?? "";
            ViewBag.ProyectoActivo = HttpContext.Session.GetString("ProyectoNombre") ?? "Sin proyecto";

            var json = HttpContext.Session.GetString(SesionFilas);
            var filas = string.IsNullOrEmpty(json)
                ? new List<ImportacionUsuarios.Fila>()
                : JsonSerializer.Deserialize<List<ImportacionUsuarios.Fila>>(json)
                  ?? new List<ImportacionUsuarios.Fila>();

            if (filas.Count == 0)
            {
                ViewBag.Paso = "subir";
                ViewBag.Error = "Se perdió la vista previa (la sesión expira a los 20 minutos). Vuelve a subir el archivo.";
                return View("Importar");
            }

            var creadas = new List<CredencialImportada>();
            var fallidas = new List<FilaFallida>();

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            foreach (var f in filas)
            {
                // La contraseña del archivo manda; si venía vacía se genera una distinta
                // por persona, que es lo que recomienda la plantilla.
                string clara = f.Password.Length > 0 ? f.Password : ImportacionUsuarios.GenerarPassword();

                var cmd = new SqlCommand(@"
                    INSERT INTO Usuarios (Nombre,Apellido,Documento,Celular,Correo,Usuario,Contraseña,Rol,IdProyecto)
                    VALUES (@n,@a,'',@c,@correo,@u,@p,@r,NULL)", con);
                cmd.Parameters.AddWithValue("@n", f.Nombre);
                cmd.Parameters.AddWithValue("@a", f.Apellido);
                cmd.Parameters.AddWithValue("@c", f.Telefono);
                cmd.Parameters.AddWithValue("@correo", f.Correo);
                cmd.Parameters.AddWithValue("@u", f.Usuario);
                cmd.Parameters.AddWithValue("@p", BCrypt.Net.BCrypt.HashPassword(clara, 12));
                cmd.Parameters.AddWithValue("@r", f.Rol);

                try
                {
                    await cmd.ExecuteNonQueryAsync();
                    creadas.Add(new CredencialImportada(
                        $"{f.Nombre} {f.Apellido}".Trim(), f.Usuario, f.Rol, clara));
                }
                catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
                {
                    // Alguien creó ese usuario entre la vista previa y la confirmación, o
                    // la base todavía exige correo único. No se detiene la importación por
                    // una fila: se sigue y se reporta al final.
                    fallidas.Add(new FilaFallida(f.Usuario, "Ya existe o choca con una restricción de unicidad"));
                }
                catch (SqlException ex)
                {
                    fallidas.Add(new FilaFallida(f.Usuario, ex.Message));
                }
            }

            HttpContext.Session.Remove(SesionFilas);
            HttpContext.Session.SetString(SesionCreds, JsonSerializer.Serialize(creadas));

            await _audit.RegistrarAsync("IMPORTAR_USUARIOS", "Usuarios", detalle:
                $"Importación masiva: {creadas.Count} creados, {fallidas.Count} con error.");

            ViewBag.Paso = "resultado";
            ViewBag.Creadas = creadas;
            ViewBag.Fallidas = fallidas;
            return View("Importar");
        }

        /// <summary>
        /// Descarga las credenciales recién generadas. Se borran de la sesión al
        /// descargarlas: la lista existe para repartirla una vez, no para quedarse.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> CredencialesImportadas()
        {
            var json = HttpContext.Session.GetString(SesionCreds);
            if (string.IsNullOrEmpty(json))
            {
                TempData["Error"] = "Ya no hay credenciales disponibles para descargar.";
                return RedirectToAction("Index");
            }

            var creadas = JsonSerializer.Deserialize<List<CredencialImportada>>(json) ?? new();

            ExcelPackage.License.SetNonCommercialPersonal("Londoño Gómez");
            using var paquete = new ExcelPackage();
            var ws = paquete.Workbook.Worksheets.Add("Credenciales");
            ws.Cells[1, 1].Value = "Credenciales generadas — repartir y borrar este archivo";
            ws.Cells[1, 1].Style.Font.Bold = true;
            ws.Cells[1, 1].Style.Font.Size = 13;
            ws.Cells[1, 1].Style.Font.Color.SetColor(DColor.FromArgb(0, 58, 112));
            ws.Cells[1, 1, 1, 4].Merge = true;

            var cab = new[] { "Nombre", "Usuario", "Rol", "Contraseña" };
            for (int i = 0; i < cab.Length; i++)
            {
                var c = ws.Cells[3, i + 1];
                c.Value = cab[i];
                c.Style.Font.Bold = true;
                c.Style.Font.Color.SetColor(DColor.White);
                c.Style.Fill.PatternType = ExcelFillStyle.Solid;
                c.Style.Fill.BackgroundColor.SetColor(DColor.FromArgb(0, 58, 112));
            }
            int fila = 4;
            foreach (var c in creadas)
            {
                ws.Cells[fila, 1].Value = c.Nombre;
                ws.Cells[fila, 2].Value = c.Usuario;
                ws.Cells[fila, 3].Value = c.Rol;
                ws.Cells[fila, 4].Value = c.Password;
                fila++;
            }
            for (int c = 1; c <= 4; c++) ws.Column(c).AutoFit();

            HttpContext.Session.Remove(SesionCreds);

            var bytes = await paquete.GetAsByteArrayAsync();
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"Credenciales_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        }

        /// <summary>Fila del archivo de credenciales generado tras la importación.</summary>
        public sealed record CredencialImportada(string Nombre, string Usuario, string Rol, string Password);

        /// <summary>Usuario que no se pudo crear, con el motivo, para el informe final.</summary>
        public sealed record FilaFallida(string Usuario, string Motivo);


        /// <summary>
        /// Updates an existing user's profile data (excluding password and username).
        /// Performs an UPDATE query on Usuarios.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(int idUsuario, string nombre, string apellido,
            string celular, string rol, int idProyecto)
        {
            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            // Verificar que el Admin no intente editar a un SuperAdministrador
            string rolSesion = HttpContext.Session.GetString("Rol") ?? "";
            string? rolObjetivo = null;
            if (rolSesion != "SuperAdministrador")
            {
                var cmdRolCheck = new SqlCommand("SELECT Rol FROM Usuarios WHERE IdUsuario=@id", con);
                cmdRolCheck.Parameters.AddWithValue("@id", idUsuario);
                rolObjetivo = (await cmdRolCheck.ExecuteScalarAsync())?.ToString();
                if (rolObjetivo == "SuperAdministrador")
                {
                    TempData["Error"] = "No tienes permisos para editar a un Super Administrador.";
                    return RedirectToAction("Index");
                }
            }

            object proyParam = idProyecto > 0 ? (object)idProyecto : DBNull.Value;

            // Solo el SuperAdministrador puede cambiar roles
            string rolFinalEditar = rol ?? "Vendedor";
            if (rolSesion != "SuperAdministrador")
            {
                // No puede asignar rol Administrador
                if (rolFinalEditar == "Administrador") rolFinalEditar = "Vendedor";
                // No puede degradar a un Administrador existente
                if (rolObjetivo == "Administrador") rolFinalEditar = "Administrador";
            }

            var cmd = new SqlCommand(@"
                UPDATE Usuarios
                SET Nombre=@n, Apellido=@a, Celular=@c,
                    Rol=@r, IdProyecto=@proy
                WHERE IdUsuario=@id", con);
            cmd.Parameters.AddWithValue("@n",    nombre   ?? "");
            cmd.Parameters.AddWithValue("@a",    apellido ?? "");
            cmd.Parameters.AddWithValue("@c",    celular  ?? "");
            cmd.Parameters.AddWithValue("@r",    rolFinalEditar);
            cmd.Parameters.AddWithValue("@proy", proyParam);
            cmd.Parameters.AddWithValue("@id",   idUsuario);
            await cmd.ExecuteNonQueryAsync();

            TempData["Exito"] = "Usuario actualizado correctamente.";
            return RedirectToAction("Index");
        }

        /// <summary>
        /// Resets a user's password. The new password is hashed with BCrypt (cost 12)
        /// before storage. Only admins can trigger this operation.
        /// Performs an UPDATE query on Usuarios.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(int idUsuario, string nuevaContrasena)
        {
            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            var cmd = new SqlCommand("UPDATE Usuarios SET Contraseña=@p WHERE IdUsuario=@id", con);
            // BCrypt genera una sal aleatoria embebida en el hash (factor de coste = 12)
            cmd.Parameters.AddWithValue("@p",  BCrypt.Net.BCrypt.HashPassword(nuevaContrasena ?? "", 12));
            cmd.Parameters.AddWithValue("@id", idUsuario);
            await cmd.ExecuteNonQueryAsync();

            TempData["Exito"] = "Contraseña actualizada correctamente.";
            return RedirectToAction("Index");
        }

        /// <summary>
        /// Deletes a user account permanently. Prevents self-deletion for safety.
        /// Performs a DELETE query on Usuarios.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Eliminar(int idUsuario)
        {
            int idActual = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;
            if (idUsuario == idActual)
            {
                TempData["Error"] = "No puedes eliminar tu propio usuario.";
                return RedirectToAction("Index");
            }

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            // Verificar que el Admin no intente eliminar a un SuperAdministrador
            string rolSesionElim = HttpContext.Session.GetString("Rol") ?? "";
            if (rolSesionElim != "SuperAdministrador")
            {
                var cmdRolCheck = new SqlCommand("SELECT Rol FROM Usuarios WHERE IdUsuario=@id", con);
                cmdRolCheck.Parameters.AddWithValue("@id", idUsuario);
                string? rolObjetivo = (await cmdRolCheck.ExecuteScalarAsync())?.ToString();
                if (rolObjetivo == "SuperAdministrador")
                {
                    TempData["Error"] = "No tienes permisos para eliminar a un Super Administrador.";
                    return RedirectToAction("Index");
                }
            }

            var motivo = await MotivoNoEliminableAsync(con, idUsuario);
            if (motivo != null)
            {
                TempData["Error"] = motivo;
                return RedirectToAction("Index");
            }

            var cmd = new SqlCommand("DELETE FROM Usuarios WHERE IdUsuario=@id", con);
            cmd.Parameters.AddWithValue("@id", idUsuario);
            await cmd.ExecuteNonQueryAsync();

            TempData["Exito"] = "Usuario eliminado correctamente.";
            return RedirectToAction("Index");
        }

        // ══════════════════════ ACCIONES SOBRE VARIOS USUARIOS ══════════════════════
        // Entre lanzamientos el equipo rota: entran diez asesores y salen otros tantos.
        // Hacerlo de a uno son treinta confirmaciones seguidas, y ahí es donde alguien
        // borra la fila equivocada.

        /// <summary>
        /// Dice por qué un usuario no se puede eliminar, o null si sí se puede.
        /// Eliminar a quien ya vendió dejaría el historial huérfano, y la base lo rechaza
        /// de todos modos por la llave foránea: antes eso terminaba en un error 500.
        /// </summary>
        private async Task<string?> MotivoNoEliminableAsync(SqlConnection con, int idUsuario)
        {
            var cmdVentas = new SqlCommand("SELECT COUNT(*) FROM Ventas WHERE IdUsuario=@id", con);
            cmdVentas.Parameters.AddWithValue("@id", idUsuario);
            int ventas = Convert.ToInt32(await cmdVentas.ExecuteScalarAsync());
            if (ventas > 0)
                return $"Tiene {ventas} venta{(ventas == 1 ? "" : "s")} registrada{(ventas == 1 ? "" : "s")}: " +
                       "inactívalo en vez de eliminarlo para no perder el historial";

            // Inmuebles que todavía tiene en la mano. Se miran las dos columnas porque
            // solo una de ellas tiene llave foránea declarada: la otra dejaría el
            // inmueble apuntando a un usuario que ya no existe.
            var cmdInm = new SqlCommand(
                "SELECT COUNT(*) FROM Inmuebles WHERE IdVendedorEnProceso=@id OR IdVendedorReserva=@id", con);
            cmdInm.Parameters.AddWithValue("@id", idUsuario);
            int inmuebles = Convert.ToInt32(await cmdInm.ExecuteScalarAsync());
            if (inmuebles > 0)
                return $"Tiene {inmuebles} inmueble{(inmuebles == 1 ? "" : "s")} tomado{(inmuebles == 1 ? "" : "s")} " +
                       "o reservado: libéralos primero";

            var cmdSup = new SqlCommand("SELECT COUNT(*) FROM Usuarios WHERE IdSuperior=@id", con);
            cmdSup.Parameters.AddWithValue("@id", idUsuario);
            if (Convert.ToInt32(await cmdSup.ExecuteScalarAsync()) > 0)
                return "Hay usuarios que dependen de él";

            return null;
        }

        /// <summary>
        /// Comprueba lo que no depende de los datos: permisos y candados de rol. Devuelve
        /// el motivo del rechazo o null. Vale tanto para eliminar como para inactivar,
        /// porque dejar sin cuenta al último superadministrador es igual de grave que
        /// borrarlo.
        /// </summary>
        private async Task<string?> MotivoNoPermitidoAsync(SqlConnection con, int idUsuario,
                                                           string usuarioObjetivo, string rolObjetivo)
        {
            int idActual = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;
            string rolSesion = HttpContext.Session.GetString("Rol") ?? "";

            if (idUsuario == idActual) return "Es tu propia cuenta";
            if (rolObjetivo == "SuperAdministrador" && rolSesion != "SuperAdministrador")
                return "No tienes permisos sobre un superadministrador";

            if (rolObjetivo == "SuperAdministrador")
            {
                var cmd = new SqlCommand(
                    "SELECT COUNT(*) FROM Usuarios WHERE Rol='SuperAdministrador' AND IdUsuario<>@id", con);
                cmd.Parameters.AddWithValue("@id", idUsuario);
                if (Convert.ToInt32(await cmd.ExecuteScalarAsync()) == 0)
                    return "Es el último superadministrador: nadie podría administrar la plataforma";
            }
            return null;
        }

        /// <summary>Datos mínimos de un usuario para poder decidir y para informar.</summary>
        private async Task<(string Usuario, string Rol)?> DatosUsuarioAsync(SqlConnection con, int id)
        {
            var cmd = new SqlCommand("SELECT Usuario, Rol FROM Usuarios WHERE IdUsuario=@id", con);
            cmd.Parameters.AddWithValue("@id", id);
            using var r = (SqlDataReader)await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return null;
            return (r["Usuario"]?.ToString() ?? "", r["Rol"]?.ToString() ?? "");
        }

        /// <summary>
        /// Elimina varios usuarios. Una fila que no se pueda borrar no detiene a las
        /// demás: se salta y se explica al final, para no dejar la operación a medias
        /// sin que nadie sepa qué alcanzó a pasar.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarVarios(int[] ids)
        {
            if (ids == null || ids.Length == 0)
            {
                TempData["Error"] = "No seleccionaste ningún usuario.";
                return RedirectToAction("Index");
            }

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            int borrados = 0;
            var saltados = new List<string>();

            foreach (var id in ids.Distinct())
            {
                var datos = await DatosUsuarioAsync(con, id);
                if (datos == null) continue;   // ya no existe: nada que informar

                var motivo = await MotivoNoPermitidoAsync(con, id, datos.Value.Usuario, datos.Value.Rol)
                             ?? await MotivoNoEliminableAsync(con, id);
                if (motivo != null) { saltados.Add($"{datos.Value.Usuario}: {motivo}"); continue; }

                var cmd = new SqlCommand("DELETE FROM Usuarios WHERE IdUsuario=@id", con);
                cmd.Parameters.AddWithValue("@id", id);
                try
                {
                    await cmd.ExecuteNonQueryAsync();
                    borrados++;
                }
                catch (SqlException ex) when (ex.Number == 547)
                {
                    saltados.Add($"{datos.Value.Usuario}: tiene información asociada que no se puede borrar");
                }
            }

            await _audit.RegistrarAsync("ELIMINAR_USUARIOS", "Usuarios", detalle:
                $"Eliminación múltiple: {borrados} eliminados, {saltados.Count} omitidos.");

            TempData["Exito"] = borrados > 0
                ? $"{borrados} usuario{(borrados == 1 ? "" : "s")} eliminado{(borrados == 1 ? "" : "s")}."
                : null;
            if (saltados.Count > 0)
                TempData["Error"] = $"No se eliminaron {saltados.Count}: " + string.Join(" · ", saltados);

            return RedirectToAction("Index");
        }

        /// <summary>
        /// Activa o inactiva varios usuarios. Inactivar es lo que se hace con quien ya
        /// vendió: la persona deja de entrar y sus cifras siguen en los reportes.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarEstadoVarios(int[] ids, bool activar)
        {
            if (ids == null || ids.Length == 0)
            {
                TempData["Error"] = "No seleccionaste ningún usuario.";
                return RedirectToAction("Index");
            }

            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            var cmdCol = new SqlCommand("SELECT COL_LENGTH('Usuarios','Activo')", con);
            if ((await cmdCol.ExecuteScalarAsync()) is null or DBNull)
            {
                TempData["Error"] = "La base todavía no tiene el estado de cuenta. " +
                                    "Ejecuta la sección 17 de Scripts/PanelAdmin.sql.";
                return RedirectToAction("Index");
            }

            int cambiados = 0;
            var saltados = new List<string>();

            foreach (var id in ids.Distinct())
            {
                var datos = await DatosUsuarioAsync(con, id);
                if (datos == null) continue;

                // Inactivar sí tiene candados de permiso; reactivar no se los pone,
                // porque devolver el acceso nunca deja a nadie por fuera.
                if (!activar)
                {
                    var motivo = await MotivoNoPermitidoAsync(con, id, datos.Value.Usuario, datos.Value.Rol);
                    if (motivo != null) { saltados.Add($"{datos.Value.Usuario}: {motivo}"); continue; }
                }

                var cmd = new SqlCommand("UPDATE Usuarios SET Activo=@a WHERE IdUsuario=@id", con);
                cmd.Parameters.AddWithValue("@a", activar);
                cmd.Parameters.AddWithValue("@id", id);
                cambiados += await cmd.ExecuteNonQueryAsync();
            }

            await _audit.RegistrarAsync(activar ? "ACTIVAR_USUARIOS" : "INACTIVAR_USUARIOS", "Usuarios",
                detalle: $"{cambiados} cuentas, {saltados.Count} omitidas.");

            TempData["Exito"] = cambiados > 0
                ? $"{cambiados} cuenta{(cambiados == 1 ? "" : "s")} {(activar ? "activada" : "inactivada")}{(cambiados == 1 ? "" : "s")}."
                : null;
            if (saltados.Count > 0)
                TempData["Error"] = $"No se cambiaron {saltados.Count}: " + string.Join(" · ", saltados);

            return RedirectToAction("Index");
        }

        /// <summary>
        /// Libera manualmente una cuenta bloqueada por intentos fallidos, sin esperar
        /// a que expire el bloqueo. Útil si un asesor se bloquea en pleno lanzamiento.
        /// </summary>
        /// <param name="usuario">Nombre de usuario bloqueado.</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LiberarBloqueo(string usuario)
        {
            if (_bloqueo.Liberar(usuario ?? ""))
            {
                await _audit.RegistrarAsync(Plataforma_ventas.Services.AccionAudit.Desbloqueo,
                    "Usuario", null, null, $"Cuenta '{usuario}' liberada manualmente");
                TempData["Exito"] = $"La cuenta '{usuario}' fue liberada y ya puede iniciar sesión.";
            }
            else
            {
                TempData["Error"] = $"La cuenta '{usuario}' ya no estaba bloqueada.";
            }
            return RedirectToAction("Index");
        }

    }
}
