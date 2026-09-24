using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Plataforma_ventas;

/// <summary>
/// Reglas de la importación masiva de usuarios desde Excel. Vive aparte del
/// controlador para poder probarla: es donde están las decisiones que, mal tomadas,
/// crean cuentas con las que nadie puede entrar.
/// </summary>
public static class ImportacionUsuarios
{
    /// <summary>Una fila del archivo, ya leída y clasificada.</summary>
    /// <param name="NumeroFila">Fila del Excel, para poder señalarla en pantalla.</param>
    /// <param name="Estado">CREAR, EXISTE o ERROR.</param>
    /// <param name="Motivo">Por qué no se va a crear, cuando aplica.</param>
    public sealed record Fila(
        int NumeroFila, string Nombre, string Apellido, string Usuario,
        string Correo, string Telefono, string Rol, string Password,
        string Estado = "CREAR", string Motivo = "");

    /// <summary>Roles que la importación puede asignar.</summary>
    /// <remarks>
    /// SuperAdministrador queda fuera a propósito: es la cuenta que lo ve todo y que
    /// puede gestionar a los demás administradores. Crear una desde un Excel, donde
    /// una celda mal escrita pasa inadvertida, es demasiado fácil.
    /// </remarks>
    public static readonly string[] RolesPermitidos = { "Vendedor", "Administrador" };

    /// <summary>
    /// Limpia un valor del Excel. Quita el espacio duro (U+00A0) que dejan los pegados
    /// desde Word o desde una página web: es invisible en la celda, pero un usuario que
    /// lo lleve dentro no se puede escribir en la pantalla de ingreso y nadie entiende
    /// por qué esa persona no puede entrar.
    /// </summary>
    public static string LimpiarTexto(string? valor)
    {
        if (string.IsNullOrEmpty(valor)) return "";
        var s = valor.Replace(' ', ' ').Replace('​', ' ').Trim();
        // Colapsa espacios repetidos en uno solo.
        var sb = new StringBuilder(s.Length);
        bool espacio = false;
        foreach (var c in s)
        {
            if (char.IsWhiteSpace(c)) { if (!espacio && sb.Length > 0) sb.Append(' '); espacio = true; }
            else { sb.Append(c); espacio = false; }
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Pasa un nombre a mayúscula inicial. Los archivos del área comercial vienen en
    /// mayúscula sostenida y así quedarían en toda la plataforma y en los reportes.
    /// </summary>
    public static string TituloNombre(string? valor)
    {
        var s = LimpiarTexto(valor);
        if (s.Length == 0) return "";
        return CultureInfo.GetCultureInfo("es-CO").TextInfo.ToTitleCase(s.ToLowerInvariant());
    }

    /// <summary>
    /// Separa un nombre completo en nombre y apellidos siguiendo la convención
    /// colombiana: dos apellidos al final. Con cuatro palabras o más, las dos primeras
    /// son el nombre; con tres, solo la primera. No es infalible (un apellido compuesto
    /// la rompe), por eso la plantilla trae las dos columnas separadas y esto es solo el
    /// respaldo para los archivos que traen una sola.
    /// </summary>
    public static (string Nombre, string Apellido) PartirNombre(string? completo)
    {
        var t = LimpiarTexto(completo).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (t.Length == 0) return ("", "");
        if (t.Length == 1) return (TituloNombre(t[0]), "");
        int corte = t.Length >= 4 ? 2 : 1;
        return (TituloNombre(string.Join(' ', t.Take(corte))),
                TituloNombre(string.Join(' ', t.Skip(corte))));
    }

    /// <summary>
    /// Valida el rol contra la lista blanca, sin distinguir mayúsculas ni tildes de más.
    /// Vacío significa Vendedor, que es el caso de la mayoría de las filas. Devuelve
    /// cadena vacía si el valor no se reconoce, para que la fila se marque con error en
    /// vez de crear una cuenta con un rol que la plataforma no entiende.
    /// </summary>
    public static string NormalizarRol(string? rol)
    {
        var r = LimpiarTexto(rol);
        if (r.Length == 0) return "Vendedor";
        foreach (var p in RolesPermitidos)
            if (string.Equals(p, r, StringComparison.OrdinalIgnoreCase)) return p;
        // Formas sueltas que escribe la gente y que significan lo mismo.
        var bajo = r.ToLowerInvariant();
        if (bajo is "asesor" or "asesora" or "vendedora") return "Vendedor";
        if (bajo is "admin" or "administradora") return "Administrador";
        return "";
    }

    /// <summary>
    /// Revisa una fila y devuelve el motivo por el que no se puede crear, o null si
    /// está bien. Solo mira el contenido del archivo; los choques contra la base de
    /// datos los resuelve el controlador, que es quien la puede consultar.
    /// </summary>
    public static string? MotivoDeRechazo(string nombre, string usuario, string rol,
                                          string correo, string password)
    {
        if (nombre.Length == 0)   return "Falta el nombre";
        if (usuario.Length == 0)  return "Falta el usuario";
        if (usuario.Contains(' ')) return "El usuario no puede llevar espacios";
        if (usuario.Length > 100) return "El usuario supera los 100 caracteres";
        if (rol.Length == 0)      return "El rol debe ser Vendedor o Administrador";
        if (correo.Length > 0 && (!correo.Contains('@') || correo.Contains(' ')))
            return "El correo no tiene un formato válido";
        if (password.Length > 0 && password.Length < 8)
            return "La contraseña escrita es muy corta (mínimo 8)";
        return null;
    }

    // Sin caracteres que se confundan al dictarlos por teléfono: ni O ni 0, ni l ni 1.
    private const string Alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";

    /// <summary>
    /// Genera una contraseña temporal distinta por persona. Se usa cuando la columna
    /// viene vacía, que es lo recomendado: un archivo con las contraseñas escritas es
    /// una lista de credenciales que circula por correo.
    /// </summary>
    public static string GenerarPassword(int largo = 10)
    {
        var sb = new StringBuilder(largo + 2);
        for (int i = 0; i < largo; i++)
            sb.Append(Alfabeto[RandomNumberGenerator.GetInt32(Alfabeto.Length)]);
        // Un símbolo y un dígito al final: así cumple cualquier política razonable
        // sin depender de la suerte del generador.
        sb.Append('+');
        sb.Append((char)('2' + RandomNumberGenerator.GetInt32(8)));
        return sb.ToString();
    }
}
