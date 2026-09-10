using Microsoft.AspNetCore.SignalR;

namespace Plataforma_ventas.Hubs
{
    /// <summary>
    /// Defines the strongly-typed client-side methods that VentasHub can invoke.
    /// Using a typed interface prevents typo bugs in method names and provides
    /// compile-time safety for SignalR broadcasts.
    /// </summary>
    public interface IVentasClient
    {
        /// <summary>Notifies all clients that the global list level changed for a project.</summary>
        /// <param name="idProyecto">Project identifier.</param>
        /// <param name="nuevaLista">New active list number (1–5).</param>
        [HubMethodName("ListaActualizada")]
        Task ListaActualizada(int idProyecto, int nuevaLista);

        /// <summary>Notifies all clients that the per-area list level changed.</summary>
        /// <param name="idProyecto">Project identifier.</param>
        /// <param name="metros">Area size label (e.g. "60").</param>
        /// <param name="nuevaLista">New active list number (1–5).</param>
        [HubMethodName("ListaAreaActualizada")]
        Task ListaAreaActualizada(int idProyecto, string metros, int nuevaLista);

        /// <summary>Notifies all clients that a specific property's estado changed.</summary>
        /// <param name="idProyecto">Project identifier.</param>
        /// <param name="idInmueble">Property identifier.</param>
        /// <param name="nuevoEstado">New estado string (e.g. "EN PROCESO", "RESERVADO", "VENDIDO", "DISPONIBLE").</param>
        /// <param name="quien">Full name of the user who caused the change; empty when the
        /// property was released back to DISPONIBLE and nobody holds it.</param>
        [HubMethodName("InmuebleActualizado")]
        Task InmuebleActualizado(int idProyecto, int idInmueble, string nuevoEstado, string quien);

        /// <summary>Notifies all clients that a list price was edited for a specific area.</summary>
        /// <param name="idProyecto">Project identifier.</param>
        /// <param name="metros">Area size label (e.g. "60").</param>
        /// <param name="numLista">List number (1–5) whose price changed.</param>
        /// <param name="nuevoPrecio">New price value.</param>
        [HubMethodName("PrecioAreaActualizado")]
        Task PrecioAreaActualizado(int idProyecto, string metros, int numLista, long nuevoPrecio);
    }

    /// <summary>
    /// Real-time SignalR hub for broadcasting property-state and list-level changes
    /// to all connected dashboard and vendedor views. Uses a typed client interface
    /// to ensure compile-time correctness of method names sent to browsers.
    /// </summary>
    public class VentasHub : Hub<IVentasClient>
    {
        private readonly ILogger<VentasHub> _logger;

        /// <summary>Initializes the hub with a logger for connection diagnostics.</summary>
        public VentasHub(ILogger<VentasHub> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Called by the framework when a new client connects.
        /// Logs the connection for diagnostics.
        /// </summary>
        public override Task OnConnectedAsync()
        {
            _logger.LogInformation("VentasHub client connected: {ConnectionId}", Context.ConnectionId);
            return base.OnConnectedAsync();
        }

        /// <summary>
        /// Called by the framework when a client disconnects.
        /// Logs the disconnection for diagnostics.
        /// </summary>
        public override Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("VentasHub client disconnected: {ConnectionId}", Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }
    }
}
