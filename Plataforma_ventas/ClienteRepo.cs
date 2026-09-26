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
            string? celular, string? correo, string? direccion, string? medio = null)
        {
            var doc = Texto.SoloDigitos(documento);

            if (!string.IsNullOrWhiteSpace(doc))
            {
                var cmdBusca = new SqlCommand(
                    "SELECT TOP 1 IdCliente FROM Clientes WHERE Documento = @d ORDER BY IdCliente", con, tx);
                cmdBusca.Parameters.AddWithValue("@d", doc);
                var existente = await cmdBusca.ExecuteScalarAsync();
                if (existente != null && existente != DBNull.Value)
                {
                    int idExistente = Convert.ToInt32(existente);

                    // Si en esta venta se eligió un medio, se guarda aunque el cliente ya
                    // existiera. Antes se devolvía la fila tal cual, así que el asesor
                    // elegía el medio, guardaba, y no pasaba nada: el dato se perdía en
                    // silencio. Pasaba siempre con las ventas importadas del Excel, que
                    // nacen apuntando a un cliente marcador que ya existe.
                    //
                    // Solo se sobrescribe cuando viene un medio válido: abrir una venta y
                    // guardarla sin tocar ese campo no debe borrar lo que ya estaba.
                    var medioValido = await MediosRepo.ValidarAsync(con, tx, medio);
                    if (!string.IsNullOrWhiteSpace(medioValido))
                    {
                        var cmdMedio = new SqlCommand(
                            "UPDATE Clientes SET MedioPublicitario=@m WHERE IdCliente=@id", con, tx);
                        cmdMedio.Parameters.AddWithValue("@m", medioValido);
                        cmdMedio.Parameters.AddWithValue("@id", idExistente);
                        await cmdMedio.ExecuteNonQueryAsync();
                    }

                    return (idExistente, true);
                }
            }

            var cmdCli = new SqlCommand(@"INSERT INTO Clientes
                (Nombre,Apellido,Documento,Celular,Correo,Direccion,MedioPublicitario)
                OUTPUT INSERTED.IdCliente
                VALUES (@n,@a,@d,@c,@e,@dir,@medio)", con, tx);
            cmdCli.Parameters.AddWithValue("@n", nombre ?? "");
            cmdCli.Parameters.AddWithValue("@a", apellido ?? "");
            cmdCli.Parameters.AddWithValue("@d", doc);
            cmdCli.Parameters.AddWithValue("@c", celular ?? "");
            cmdCli.Parameters.AddWithValue("@e", correo ?? "");
            cmdCli.Parameters.AddWithValue("@dir", direccion ?? "");
            cmdCli.Parameters.AddWithValue("@medio", await MediosRepo.ValidarAsync(con, tx, medio));
            return (Convert.ToInt32(await cmdCli.ExecuteScalarAsync()), false);
        }
    }
}
