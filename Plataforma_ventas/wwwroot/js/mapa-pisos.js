// Mapa de ventas en vivo.
//
// Durante un lanzamiento el mapa es lo que todos miran: el administrador desde el
// informe y el asesor desde su perfil. Si hay que recargar para ver un cambio, dos
// personas pueden estar ofreciendo la misma unidad al mismo tiempo.
//
// Uso: en la página, un contenedor con el identificador del proyecto:
//     <div id="mapaPisos" data-proyecto="12"> ... </div>
// Las celdas son .mp-celda con data-id del inmueble.

(function () {
    var cont = document.getElementById('mapaPisos');
    if (!cont) return;

    var proyecto = parseInt(cont.dataset.proyecto || '0', 10);
    // Sin proyecto activo no hay nada que escuchar. Y si la librería no cargó (red
    // caída, bloqueo del navegador), la página se queda como una foto: sigue
    // sirviendo al recargar, que es mejor que romperse.
    if (!proyecto || typeof signalR === 'undefined') return;

    var conexion = new signalR.HubConnectionBuilder()
        .withUrl('/ventasHub')
        .withAutomaticReconnect()
        .build();

    conexion.on('InmuebleActualizado', function (idProyecto, idInmueble, estado, quien) {
        if (idProyecto !== proyecto) return;

        var celda = cont.querySelector('.mp-celda[data-id="' + idInmueble + '"]');
        if (!celda) return;

        // Tres estados, los mismos que pinta el servidor: lo que está en proceso
        // todavía no compromete la unidad y se sigue viendo disponible.
        var est = estado === 'VENDIDO' ? 'VENDIDO'
                : estado === 'RESERVADO' ? 'RESERVADO' : 'DISPONIBLE';
        var cls = est === 'VENDIDO' ? 'V' : est === 'RESERVADO' ? 'R' : 'D';

        celda.className = 'mp-celda e-' + cls;
        var etiqueta = celda.querySelector('.mp-estado');
        if (etiqueta) etiqueta.textContent = est;
        celda.title = (est === 'RESERVADO' && quien) ? est + ' · ' + quien : est;

        // Un destello breve: con setenta unidades en pantalla, un cambio de color sin
        // aviso pasa desapercibido.
        celda.style.boxShadow = '0 0 0 3px rgba(0,118,227,0.35)';
        setTimeout(function () { celda.style.boxShadow = ''; }, 1500);
    });

    conexion.start().catch(function () { /* sin tiempo real: la página sigue sirviendo */ });
})();
