using Microsoft.Data.SqlClient;

namespace Plataforma_ventas;

/// <summary>
/// Datos del proyecto activo que hacen falta para redactar los mensajes.
/// </summary>
public static class Proyecto
{
    /// <summary>
    /// Cómo se nombra una unidad del proyecto activo ("suite", "apartamento"…).
    ///
    /// El tipo se guarda en la sesión junto al id del proyecto al que pertenece. Antes
    /// se guardaba suelto al cargar un Excel y no se actualizaba al cambiar de proyecto,
    /// así que un proyecto de suites podía terminar mostrando "apartamento". Guardar
    /// también el id permite detectar que la sesión quedó desactualizada y volver a
    /// consultarlo: una consulta al cambiar de proyecto, ninguna el resto del tiempo.
    /// </summary>
    public static async Task<Texto.Unidad> UnidadAsync(HttpContext ctx, SqlConnection con, int idProyecto)
    {
        if (idProyecto <= 0) return Texto.UnidadDe(null);

        if (ctx.Session.GetString("TipProyectoId") == idProyecto.ToString())
            return Texto.UnidadDe(ctx.Session.GetString("TipProyecto"));

        var cmd = new SqlCommand("SELECT TipProyecto FROM Proyectos WHERE IdProyectos=@id", con);
        cmd.Parameters.AddWithValue("@id", idProyecto);
        var tipo = (await cmd.ExecuteScalarAsync())?.ToString() ?? "APARTAMENTOS";

        ctx.Session.SetString("TipProyecto", tipo);
        ctx.Session.SetString("TipProyectoId", idProyecto.ToString());
        return Texto.UnidadDe(tipo);
    }
}
