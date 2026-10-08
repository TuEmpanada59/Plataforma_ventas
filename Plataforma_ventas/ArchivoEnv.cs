namespace Plataforma_ventas;

/// <summary>
/// Lector del archivo .env.
///
/// .NET no lee .env de serie: su configuración viene de appsettings.json, de las
/// variables de entorno y de los secretos de usuario. Esto añade el .env como una
/// fuente más, para que la plataforma se configure igual que Andrómeda y el equipo
/// no tenga que recordar dos formas distintas de hacer lo mismo.
///
/// Los nombres usan la convención de .NET: dos guiones bajos separan los niveles.
///     ConnectionStrings__DefaultConnection=...
///     Graph__TenantId=...
/// Esa es la ventaja de hacerlo así y no con nombres propios: son EXACTAMENTE los
/// mismos nombres que se escriben en Azure (App Service > Configuration), así que lo
/// que se prueba en local y lo que se configura en producción se llaman igual.
///
/// Va escrito a mano y sin dependencias nuevas: son treinta líneas, y un paquete de
/// terceros que lee credenciales es un paquete más que auditar.
/// </summary>
public static class ArchivoEnv
{
    /// <summary>
    /// Lee el archivo y devuelve sus pares clave/valor. Si no existe, devuelve vacío:
    /// el .env es opcional y la aplicación tiene que arrancar sin él —en Azure no hay
    /// ninguno, la configuración vive en el App Service—.
    /// </summary>
    public static Dictionary<string, string?> Leer(string ruta)
    {
        var valores = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(ruta)) return valores;

        foreach (var linea in File.ReadAllLines(ruta))
        {
            var texto = linea.Trim();
            if (texto.Length == 0 || texto.StartsWith('#')) continue;

            // Solo la primera igualdad: una cadena de conexión lleva varias dentro.
            int corte = texto.IndexOf('=');
            if (corte <= 0) continue;

            var clave = texto[..corte].Trim();
            var valor = texto[(corte + 1)..].Trim();

            // Comillas opcionales alrededor del valor, como en cualquier .env. Se
            // quitan solo si envuelven todo: una contraseña puede empezar por comilla.
            if (valor.Length >= 2 &&
                ((valor[0] == '"' && valor[^1] == '"') || (valor[0] == '\'' && valor[^1] == '\'')))
                valor = valor[1..^1];

            if (clave.Length > 0) valores[clave] = valor;
        }

        return valores;
    }
}
