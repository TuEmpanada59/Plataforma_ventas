using Microsoft.Data.SqlClient;

namespace Plataforma_ventas
{
    /// <summary>
    /// Traza los cambios de lista de precios por área, para poder responder
    /// después "¿por qué este apartamento se vendió a este precio?".
    /// </summary>
    public static class HistorialListas
    {
        public const string Automatico = "AUTOMATICO";
        public const string Manual = "MANUAL";

        /// <summary>
        /// Registra un cambio de lista. Nunca lanza: si la tabla no existe todavía
        /// (Scripts/PanelAdmin.sql sin ejecutar) el cambio de precio se aplica igual.
        /// </summary>
        public static async Task RegistrarAsync(SqlConnection con, SqlTransaction? tx,
            int idProyecto, string metros, int listaAnterior, int listaNueva,
            string motivo, int? idUsuario, string usuario)
        {
            try
            {
                var cmd = new SqlCommand(@"
                    INSERT INTO HistorialListas
                        (IdProyecto, Metros, ListaAnterior, ListaNueva, Motivo, IdUsuario, Usuario)
                    VALUES (@proy, @met, @ant, @nue, @mot, @idu, @usr)", con, tx);
                cmd.Parameters.AddWithValue("@proy", idProyecto);
                cmd.Parameters.AddWithValue("@met", metros ?? "");
                cmd.Parameters.AddWithValue("@ant", listaAnterior);
                cmd.Parameters.AddWithValue("@nue", listaNueva);
                cmd.Parameters.AddWithValue("@mot", motivo);
                cmd.Parameters.AddWithValue("@idu", (object?)idUsuario ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@usr", usuario ?? "");
                await cmd.ExecuteNonQueryAsync();
            }
            catch (SqlException ex) when (ex.Message.Contains("Invalid object name") || ex.Number == 208)
            {
                // Sin tabla de historial: el cambio de lista sigue siendo válido.
            }
        }
    }
}
