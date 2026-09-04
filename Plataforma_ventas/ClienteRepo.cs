using Microsoft.Data.SqlClient;

namespace Plataforma_ventas
{
    /// <summary>
    /// Alta de clientes desde el flujo de venta, evitando duplicados por documento.
    /// </summary>
    public static class ClienteRepo
    {
        /// <summary>
        /// Devuelve el cliente que ya tenga ese documento o, si no existe, lo crea.
        /// </summary>
        /// <remarks>
        /// Antes se insertaba siempre, así que un mismo comprador terminaba con varias
        /// fichas y los listados y reportes quedaban inflados. Se reutiliza en vez de
        /// bloquear la venta: interrumpir un cierre en pleno lanzamiento es peor que
        /// tener un dato de contacto desactualizado, y el asesor queda avisado.
        /// Los datos del cliente existente NO se sobrescriben.
        /// </remarks>
        /// <returns>
        /// El id del cliente y si se reutilizó uno existente (true) o se creó (false).
        /// </returns>
        public static async Task<(int IdCliente, bool Reutilizado)> ObtenerOCrearAsync(
            SqlConnection con, SqlTransaction tx,
            string? nombre, string? apellido, string? documento,
            string? celular, string? correo, string? direccion)
        {
            var doc = Texto.SoloDigitos(documento);

            if (!string.IsNullOrWhiteSpace(doc))
            {
                var cmdBusca = new SqlCommand(
                    "SELECT TOP 1 IdCliente FROM Clientes WHERE Documento = @d ORDER BY IdCliente", con, tx);
                cmdBusca.Parameters.AddWithValue("@d", doc);
                var existente = await cmdBusca.ExecuteScalarAsync();
                if (existente != null && existente != DBNull.Value)
                    return (Convert.ToInt32(existente), true);
            }

            var cmdCli = new SqlCommand(@"INSERT INTO Clientes
                (Nombre,Apellido,Documento,Celular,Correo,Direccion)
                OUTPUT INSERTED.IdCliente
                VALUES (@n,@a,@d,@c,@e,@dir)", con, tx);
            cmdCli.Parameters.AddWithValue("@n", nombre ?? "");
            cmdCli.Parameters.AddWithValue("@a", apellido ?? "");
            cmdCli.Parameters.AddWithValue("@d", doc);
            cmdCli.Parameters.AddWithValue("@c", celular ?? "");
            cmdCli.Parameters.AddWithValue("@e", correo ?? "");
            cmdCli.Parameters.AddWithValue("@dir", direccion ?? "");
            return (Convert.ToInt32(await cmdCli.ExecuteScalarAsync()), false);
        }
    }
}
