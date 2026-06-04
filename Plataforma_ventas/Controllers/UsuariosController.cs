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

            // Todos los proyectos activos — para sidebar y formulario de creación/edición
            var proyectos = new List<(int Id, string Nombre)>();
            var cmdList = new SqlCommand(
                "SELECT IdProyectos, Nombre FROM Proyectos WHERE Activo=1 ORDER BY FechaCarga DESC", con);
            using (var r = cmdList.ExecuteReader())
                while (r.Read())
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

            using (var reader = cmd.ExecuteReader())
                while (reader.Read())
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

            ViewBag.Usuarios        = usuarios;
            ViewBag.TotalUsuarios   = usuarios.Count;
            ViewBag.TotalAdmins     = usuarios.Count(u => u.Rol == "Administrador");
            ViewBag.TotalVendedores = usuarios.Count(u => u.Rol == "Vendedor");
            ViewBag.TotalProyectos  = proyectos.Count;
            return View();
        }

        [HttpPost]
        public IActionResult Crear(string nombre, string apellido, string documento,
            string celular, string correo, string usuario, string contrasena,
            string rol, int idProyecto)
        {
            using var con = new SqlConnection(_conn);
            con.Open();

            var cmdCheck = new SqlCommand("SELECT COUNT(*) FROM Usuarios WHERE Usuario=@u OR Correo=@e", con);
            cmdCheck.Parameters.AddWithValue("@u", usuario ?? "");
            cmdCheck.Parameters.AddWithValue("@e", correo  ?? "");
            if ((int)cmdCheck.ExecuteScalar() > 0)
            {
                TempData["Error"] = "El nombre de usuario o correo ya está en uso.";
                return RedirectToAction("Index");
            }

            // Administrador no requiere proyecto asignado; Vendedor sí
            string rolFinal = rol == "Administrador" ? "Administrador" : "Vendedor";
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
            cmd.ExecuteNonQuery();

            TempData["Exito"] = $"Usuario '{usuario}' ({rolFinal}) creado correctamente.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult Editar(int idUsuario, string nombre, string apellido,
            string documento, string celular, string correo, string rol, int idProyecto)
        {
            using var con = new SqlConnection(_conn);
            con.Open();

            object proyParam = idProyecto > 0 ? (object)idProyecto : DBNull.Value;

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
            cmd.Parameters.AddWithValue("@r",    rol      ?? "Vendedor");
            cmd.Parameters.AddWithValue("@proy", proyParam);
            cmd.Parameters.AddWithValue("@id",   idUsuario);
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
            // BCrypt genera una sal aleatoria embebida en el hash (factor de coste = 12)
            cmd.Parameters.AddWithValue("@p",  BCrypt.Net.BCrypt.HashPassword(nuevaContrasena ?? "", 12));
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
