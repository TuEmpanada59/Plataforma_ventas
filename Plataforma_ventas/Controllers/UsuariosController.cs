using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Plataforma_ventas.Filters;

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

        /// <summary>Initializes the controller with DB connection string from configuration.</summary>
        public UsuariosController(IConfiguration config)
        {
            _conn = config.GetConnectionString("DefaultConnection")!;
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
            return View();
        }

        /// <summary>
        /// Creates a new user account. Validates username/email uniqueness before inserting.
        /// Passwords are hashed with BCrypt (cost factor 12) before storage — never stored in plain text.
        /// Performs SELECT (uniqueness check) and INSERT queries on Usuarios.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(string nombre, string apellido, string documento,
            string celular, string correo, string usuario, string contrasena,
            string rol, int idProyecto)
        {
            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            var cmdCheck = new SqlCommand("SELECT COUNT(*) FROM Usuarios WHERE Usuario=@u OR Correo=@e", con);
            cmdCheck.Parameters.AddWithValue("@u", usuario ?? "");
            cmdCheck.Parameters.AddWithValue("@e", correo  ?? "");
            if ((int)(await cmdCheck.ExecuteScalarAsync())! > 0)
            {
                TempData["Error"] = "El nombre de usuario o correo ya está en uso.";
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

            var cmd = new SqlCommand(@"
                INSERT INTO Usuarios (Nombre,Apellido,Documento,Celular,Correo,Usuario,Contraseña,Rol,IdProyecto)
                VALUES (@n,@a,@d,@c,@e,@u,@p,@r,@proy)", con);
            cmd.Parameters.AddWithValue("@n",    nombre   ?? "");
            cmd.Parameters.AddWithValue("@a",    apellido ?? "");
            cmd.Parameters.AddWithValue("@d",    documento ?? "");
            cmd.Parameters.AddWithValue("@c",    celular  ?? "");
            cmd.Parameters.AddWithValue("@e",    correo   ?? "");
            cmd.Parameters.AddWithValue("@u",    usuario  ?? "");
            // BCrypt genera una sal aleatoria embebida en el hash (factor de coste = 12)
            cmd.Parameters.AddWithValue("@p",    BCrypt.Net.BCrypt.HashPassword(contrasena ?? "", 12));
            cmd.Parameters.AddWithValue("@r",    rolFinal);
            cmd.Parameters.AddWithValue("@proy", proyParam);
            await cmd.ExecuteNonQueryAsync();

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
            string documento, string celular, string correo, string rol, int idProyecto)
        {
            using var con = new SqlConnection(_conn);
            await con.OpenAsync();

            // Verificar que el Admin no intente editar a un SuperAdministrador
            string rolSesionCheck = HttpContext.Session.GetString("Rol") ?? "";
            if (rolSesionCheck != "SuperAdministrador")
            {
                var cmdRolCheck = new SqlCommand("SELECT Rol FROM Usuarios WHERE IdUsuario=@id", con);
                cmdRolCheck.Parameters.AddWithValue("@id", idUsuario);
                string? rolObjetivo = (await cmdRolCheck.ExecuteScalarAsync())?.ToString();
                if (rolObjetivo == "SuperAdministrador")
                {
                    TempData["Error"] = "No tienes permisos para editar a un Super Administrador.";
                    return RedirectToAction("Index");
                }
            }

            object proyParam = idProyecto > 0 ? (object)idProyecto : DBNull.Value;

            // Solo el SuperAdministrador puede asignar el rol Administrador
            string rolSesionEditar = HttpContext.Session.GetString("Rol") ?? "";
            string rolFinalEditar = rol ?? "Vendedor";
            if (rolSesionEditar != "SuperAdministrador" && rolFinalEditar == "Administrador")
                rolFinalEditar = "Vendedor";

            var cmd = new SqlCommand(@"
                UPDATE Usuarios
                SET Nombre=@n, Apellido=@a, Documento=@d, Celular=@c,
                    Correo=@e, Rol=@r, IdProyecto=@proy
                WHERE IdUsuario=@id", con);
            cmd.Parameters.AddWithValue("@n",    nombre   ?? "");
            cmd.Parameters.AddWithValue("@a",    apellido ?? "");
            cmd.Parameters.AddWithValue("@d",    documento ?? "");
            cmd.Parameters.AddWithValue("@c",    celular  ?? "");
            cmd.Parameters.AddWithValue("@e",    correo   ?? "");
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
    }
}
