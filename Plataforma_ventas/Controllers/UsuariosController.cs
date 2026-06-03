using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Plataforma_ventas.Filters;

namespace Plataforma_ventas.Controllers
{
    [RolAutorizado("Administrador")]
    public class UsuariosController : Controller
    {
        private readonly string _conn;

        public UsuariosController(IConfiguration config)
        {
            _conn = config.GetConnectionString("DefaultConnection")!;
        }

        public IActionResult Index()
        {
            ViewBag.Nombre = HttpContext.Session.GetString("Nombre") ?? "Admin";
            ViewBag.Apellido = HttpContext.Session.GetString("Apellido") ?? "";
            ViewBag.ProyectoActivo = HttpContext.Session.GetString("ProyectoNombre") ?? "Sin proyecto";

            using var con = new SqlConnection(_conn);
            con.Open();

            int idAdmin = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid2) ? uid2 : 0;

            var proyectos = new List<(int Id, string Nombre)>();
            var cmdList = new SqlCommand("SELECT IdProyectos, Nombre FROM Proyectos WHERE Activo=1 AND IdAdminCreador=@admin ORDER BY FechaCarga DESC", con);
            cmdList.Parameters.AddWithValue("@admin", idAdmin);
            using (var r = cmdList.ExecuteReader())
                while (r.Read())
                    proyectos.Add(((int)r["IdProyectos"], r["Nombre"]?.ToString() ?? ""));
            ViewBag.Proyectos = proyectos;

            // Código del proyecto del admin para mostrarlo en el formulario
            string codigoProyecto = "";
            var cmdCod = new SqlCommand("SELECT ISNULL(CodigoAcceso,'') FROM Proyectos WHERE IdAdminCreador=@admin AND Activo=1", con);
            cmdCod.Parameters.AddWithValue("@admin", idAdmin);
            var resCod = cmdCod.ExecuteScalar();
            if (resCod != null && resCod != DBNull.Value) codigoProyecto = resCod.ToString() ?? "";
            ViewBag.CodigoProyecto = codigoProyecto;

            // Proyecto del admin
            int idProyAdmin = 0;
            var cmdPAdm = new SqlCommand("SELECT IdProyectos FROM Proyectos WHERE IdAdminCreador=@admin AND Activo=1", con);
            cmdPAdm.Parameters.AddWithValue("@admin", idAdmin);
            var resP = cmdPAdm.ExecuteScalar();
            if (resP != null && resP != DBNull.Value) idProyAdmin = (int)resP;

            // Solo el admin y sus vendedores
            var usuarios = new List<dynamic>();
            var cmd = new SqlCommand(@"
                SELECT u.IdUsuario, u.Nombre, u.Apellido, u.Usuario, u.Correo, u.Documento, u.Celular, u.Rol,
                       COUNT(v.IdVenta) AS TotalVentas
                FROM Usuarios u
                LEFT JOIN Ventas v ON u.IdUsuario = v.IdUsuario
                WHERE (u.Rol = 'Administrador' AND u.IdUsuario = @admin)
                   OR (u.Rol = 'Vendedor'       AND u.IdProyecto = @proy)
                GROUP BY u.IdUsuario, u.Nombre, u.Apellido, u.Usuario, u.Correo, u.Documento, u.Celular, u.Rol
                ORDER BY u.Rol, u.Nombre", con);
            cmd.Parameters.AddWithValue("@admin", idAdmin);
            cmd.Parameters.AddWithValue("@proy", idProyAdmin);
            using (var reader = cmd.ExecuteReader())
                while (reader.Read())
                    usuarios.Add(new
                    {
                        Id = (int)reader["IdUsuario"],
                        Nombre = reader["Nombre"]?.ToString() ?? "",
                        Apellido = reader["Apellido"]?.ToString() ?? "",
                        Usuario = reader["Usuario"]?.ToString() ?? "",
                        Correo = reader["Correo"]?.ToString() ?? "",
                        Documento = reader["Documento"]?.ToString() ?? "",
                        Celular = reader["Celular"]?.ToString() ?? "",
                        Rol = reader["Rol"]?.ToString() ?? "",
                        TotalVentas = (int)reader["TotalVentas"],
                    });

            ViewBag.Usuarios = usuarios;
            ViewBag.TotalUsuarios = usuarios.Count;
            ViewBag.TotalAdmins = usuarios.Count(u => u.Rol == "Administrador");
            ViewBag.TotalVendedores = usuarios.Count(u => u.Rol == "Vendedor");
            return View();
        }

        // Crear vendedor — siempre Vendedor, con código de proyecto obligatorio
        [HttpPost]
        public IActionResult Crear(string nombre, string apellido, string documento,
            string celular, string correo, string usuario, string contrasena, string codigoProyecto)
        {
            using var con = new SqlConnection(_conn);
            con.Open();

            // Verificar usuario único
            var cmdCheck = new SqlCommand("SELECT COUNT(*) FROM Usuarios WHERE Usuario=@u", con);
            cmdCheck.Parameters.AddWithValue("@u", usuario ?? "");
            if ((int)cmdCheck.ExecuteScalar() > 0)
            {
                TempData["Error"] = "El nombre de usuario ya está en uso.";
                return RedirectToAction("Index");
            }

            // Validar código de proyecto
            if (string.IsNullOrWhiteSpace(codigoProyecto))
            {
                TempData["Error"] = "El código del proyecto es obligatorio.";
                return RedirectToAction("Index");
            }

            var cmdProy = new SqlCommand("SELECT IdProyectos FROM Proyectos WHERE CodigoAcceso=@c AND Activo=1", con);
            cmdProy.Parameters.AddWithValue("@c", codigoProyecto.Trim().ToUpper());
            var resP = cmdProy.ExecuteScalar();
            if (resP == null || resP == DBNull.Value)
            {
                TempData["Error"] = "El código del proyecto no es válido.";
                return RedirectToAction("Index");
            }
            int idProyecto = (int)resP;

            var cmd = new SqlCommand(@"INSERT INTO Usuarios (Nombre,Apellido,Documento,Celular,Correo,Usuario,Contraseña,Rol,IdProyecto)
                VALUES (@n,@a,@d,@c,@e,@u,@p,'Vendedor',@proy)", con);
            cmd.Parameters.AddWithValue("@n", nombre ?? "");
            cmd.Parameters.AddWithValue("@a", apellido ?? "");
            cmd.Parameters.AddWithValue("@d", documento ?? "");
            cmd.Parameters.AddWithValue("@c", celular ?? "");
            cmd.Parameters.AddWithValue("@e", correo ?? "");
            cmd.Parameters.AddWithValue("@u", usuario ?? "");
            // BCrypt.HashPassword genera una sal aleatoria embebida en el hash (factor de coste = 12)
            cmd.Parameters.AddWithValue("@p", BCrypt.Net.BCrypt.HashPassword(contrasena ?? "", 12));
            cmd.Parameters.AddWithValue("@proy", idProyecto);
            cmd.ExecuteNonQuery();

            TempData["Exito"] = $"Vendedor '{usuario}' creado y asignado al proyecto correctamente.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult Editar(int idUsuario, string nombre, string apellido,
            string documento, string celular, string correo, string rol)
        {
            using var con = new SqlConnection(_conn);
            con.Open();

            var cmd = new SqlCommand(@"UPDATE Usuarios 
                SET Nombre=@n, Apellido=@a, Documento=@d, Celular=@c, Correo=@e, Rol=@r
                WHERE IdUsuario=@id", con);
            cmd.Parameters.AddWithValue("@n", nombre ?? "");
            cmd.Parameters.AddWithValue("@a", apellido ?? "");
            cmd.Parameters.AddWithValue("@d", documento ?? "");
            cmd.Parameters.AddWithValue("@c", celular ?? "");
            cmd.Parameters.AddWithValue("@e", correo ?? "");
            cmd.Parameters.AddWithValue("@r", rol ?? "Vendedor");
            cmd.Parameters.AddWithValue("@id", idUsuario);
            cmd.ExecuteNonQuery();

            TempData["Exito"] = "Usuario actualizado correctamente.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult ResetPassword(int idUsuario, string nuevaContrasena)
        {
            using var con = new SqlConnection(_conn);
            con.Open();

            var cmd = new SqlCommand("UPDATE Usuarios SET Contraseña=@p WHERE IdUsuario=@id", con);
            // BCrypt.HashPassword genera una sal aleatoria embebida en el hash (factor de coste = 12)
            cmd.Parameters.AddWithValue("@p", BCrypt.Net.BCrypt.HashPassword(nuevaContrasena ?? "", 12));
            cmd.Parameters.AddWithValue("@id", idUsuario);
            cmd.ExecuteNonQuery();

            TempData["Exito"] = "Contraseña actualizada correctamente.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult Eliminar(int idUsuario)
        {
            int idActual = int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int uid) ? uid : 0;
            if (idUsuario == idActual)
            {
                TempData["Error"] = "No puedes eliminar tu propio usuario.";
                return RedirectToAction("Index");
            }

            using var con = new SqlConnection(_conn);
            con.Open();

            var cmd = new SqlCommand("DELETE FROM Usuarios WHERE IdUsuario=@id", con);
            cmd.Parameters.AddWithValue("@id", idUsuario);
            cmd.ExecuteNonQuery();

            TempData["Exito"] = "Usuario eliminado correctamente.";
            return RedirectToAction("Index");
        }

    }
}