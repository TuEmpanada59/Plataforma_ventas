using Microsoft.Data.SqlClient;

namespace Plataforma_ventas;

/// <summary>
/// Catálogo de medios publicitarios, mantenido por el administrador desde
/// Clientes → Medios publicitarios.
/// </summary>
/// <remarks>
/// El medio se guarda como TEXTO en Clientes.MedioPublicitario, no como una
/// referencia al catálogo: así un cliente conserva el canal por el que entró
/// aunque después se elimine el medio. Borrar un medio quita la opción para los
/// registros nuevos; no reescribe la historia ni rompe los informes.
/// </remarks>
public static class MediosRepo
{
    /// <summary>
    /// Medios disponibles, en orden alfabético. Si la tabla todavía no existe
    /// (script sin ejecutar), devuelve la lista vacía en vez de fallar: el campo
    /// es opcional y una venta no se puede caer por el catálogo de medios.
    /// </summary>
    public static async Task<List<string>> ListarAsync(SqlConnection con, SqlTransaction? tx = null)
    {
        var medios = new List<string>();
        if (!await ExisteTablaAsync(con, tx)) return medios;

        var sql = "SELECT Nombre FROM MediosPublicitarios ORDER BY Nombre";
        var cmd = tx == null ? new SqlCommand(sql, con) : new SqlCommand(sql, con, tx);
        using (var r = (SqlDataReader)await cmd.ExecuteReaderAsync())
            while (await r.ReadAsync())
                medios.Add(r["Nombre"]?.ToString() ?? "");
        return medios;
    }

    /// <summary>
    /// Devuelve el medio si está en el catálogo y cadena vacía si no.
    /// Es el mismo criterio que tenía la lista blanca fija: no se guarda texto
    /// libre, porque entonces los canales no se podrían agrupar en el informe.
    /// </summary>
    public static async Task<string> ValidarAsync(SqlConnection con, SqlTransaction? tx, string? medio)
    {
        var nombre = (medio ?? "").Trim();
        if (nombre.Length == 0) return "";
        if (!await ExisteTablaAsync(con, tx)) return "";

        var sql = "SELECT COUNT(*) FROM MediosPublicitarios WHERE Nombre=@n";
        var cmd = tx == null ? new SqlCommand(sql, con) : new SqlCommand(sql, con, tx);
        cmd.Parameters.AddWithValue("@n", nombre);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0 ? nombre : "";
    }

    /// <summary>Si el script de migración todavía no creó la tabla.</summary>
    public static async Task<bool> ExisteTablaAsync(SqlConnection con, SqlTransaction? tx = null)
    {
        var sql = "SELECT OBJECT_ID('MediosPublicitarios','U')";
        var cmd = tx == null ? new SqlCommand(sql, con) : new SqlCommand(sql, con, tx);
        return (await cmd.ExecuteScalarAsync()) is not (null or DBNull);
    }
}
