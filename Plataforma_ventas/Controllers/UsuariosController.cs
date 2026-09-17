using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using OfficeOpenXml;
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

            // Todos los usuarios del sistema con su proyecto asignado
            var usuarios = new List<dynamic>();
            var cmd = new SqlCommand(@"
                SELECT u.IdUsuario, u.Nombre, u.Apellido, u.Usuario, u.Correo,
                       u.Documento, u.Celular, u.Rol, u.IdProyecto,
                       ISNULL(p.Nombre, '—') AS NombreProyecto,
                       COUNT(v.IdVenta) AS TotalVentas
                FROM Usuarios u
                LEFT JOIN Proyectos p ON u.IdProyecto = p.IdProyectos
                LEFT JOIN Ventas    v ON u.IdUsuario  = v.IdUsuario
                GROUP BY u.IdUsuario, u.Nombre, u.Apellido, u.Usuario, u.Correo,
                         u.Documento, u.Celular, u.Rol, u.IdProyecto, p.Nombre
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
                    });

            // El Admin no ve ni puede gestionar SuperAdministradores
            string rolActual = HttpContext.Session.GetString("Rol") ?? "";
            if (rolActual != "SuperAdministrador")
                usuarios = usuarios.Where(u => u.Rol != "SuperAdministrador").ToList();

            ViewBag.Usuarios        = usuarios;
            ViewBag.TotalUsuarios   = usuarios.Count;
            ViewBag.TotalAdmins     = usuarios.Count(u => u.Rol == "Administrador" || u.Rol == "SuperAdministrador");
            ViewBag.TotalVendedores = usuarios.Count(u => u.Rol == "Vendedor");
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

            var cmd = new SqlCommand("DELETE FROM Usuarios WHERE IdUsuario=@id", con);
            cmd.Parameters.AddWithValue("@id", idUsuario);
            await cmd.ExecuteNonQueryAsync();

            TempData["Exito"] = "Usuario eliminado correctamente.";
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
