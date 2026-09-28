// Pantallas en vivo.
//
// El mapa ya se actualizaba solo, pero las cifras no: quien miraba el resumen, el
// inventario, las ventas o las reservas estaba viendo una foto del momento en que abrió
// la página. En un lanzamiento eso envejece en segundos.
//
// La idea es no repetir en el navegador las cuentas que ya hace el servidor. Cuando
// llega un aviso del hub, la página vuelve a pedirse a sí misma, se toma el bloque
// #vivo de la respuesta y se cambia por el que está en pantalla. Así el número que se
// ve siempre es el que calculó el servidor, con la misma regla, y agregar una pestaña
// nueva no obliga a escribir nada aquí.
//
// Uso: en el armazón, el contenido envuelto en un contenedor con el proyecto que se
// está mirando:
//     <div id="vivo" data-proyecto="12"> ... </div>
// Opcional, un indicador en la barra superior con id="vivoChip".
//
// Dos salvedades para lo que el servidor no puede rehacer:
//   · data-vivo-conservar en un elemento con id lo deja intacto entre refrescos. Es
//     para lo que vive en el navegador y el servidor no sabe, como el feed de
//     actividad del panel, que se llena con lo que va pasando.
//   · data-vivo-valor en un campo conserva lo que la persona escribió, como el
//     buscador de un informe.
//
// Y dos avisos para quien quiera engancharse: el evento vivo:actualizado después de
// cada refresco, y vivo:conexion cuando el hub se cae o vuelve. window.vivo expone la
// conexión para no abrir una segunda.

(function () {
    var cont = document.getElementById('vivo');
    if (!cont || typeof signalR === 'undefined') return;

    var proyecto = parseInt(cont.dataset.proyecto || '0', 10);
    if (!proyecto) return;

    var chip       = document.getElementById('vivoChip');
    var refrescando = false;   // hay una petición en el aire
    var pendiente   = false;   // llegó un aviso mientras no se podía refrescar
    var timer       = null;

    // ── Cuándo NO se puede refrescar ───────────────────────────────────────────
    // Cambiar el contenido por debajo de alguien que está escribiendo o que tiene un
    // modal abierto le borra el trabajo. Dirección no tiene formularios, pero este
    // componente sirve igual para las pantallas del administrador, y ahí sí importa.
    function ocupado() {
        var act = document.activeElement;
        if (act && cont.contains(act) && /^(INPUT|TEXTAREA|SELECT)$/.test(act.tagName)) return true;

        var capas = document.querySelectorAll(
            '.modal, .modal-fondo, .modal-bg, .modal-overlay, .modal-backdrop, dialog[open]');
        for (var i = 0; i < capas.length; i++) {
            var c = capas[i];
            if (c.tagName === 'DIALOG') return true;
            // offsetParent nulo = oculto (display:none o un ancestro oculto).
            if (c.offsetParent !== null && c.offsetWidth > 0 && c.offsetHeight > 0) return true;
        }
        return false;
    }

    // ── Estado del mapa, para saber qué cambió ─────────────────────────────────
    function estados() {
        var m = {};
        var celdas = cont.querySelectorAll('.mp-celda[data-id]');
        for (var i = 0; i < celdas.length; i++) m[celdas[i].dataset.id] = celdas[i].className;
        return m;
    }

    // Con setenta unidades en pantalla, un color que cambia sin aviso pasa
    // desapercibido: se le da un destello a lo que se movió.
    function destellar(antes) {
        var celdas = cont.querySelectorAll('.mp-celda[data-id]');
        for (var i = 0; i < celdas.length; i++) {
            var c = celdas[i], prev = antes[c.dataset.id];
            if (prev && prev !== c.className) resaltar(c);
        }
    }

    function resaltar(el) {
        el.style.boxShadow = '0 0 0 3px rgba(0,118,227,0.35)';
        setTimeout(function () { el.style.boxShadow = ''; }, 1800);
    }

    function marcarChip(clase) {
        if (!chip) return;
        chip.classList.remove('vivo-late', 'vivo-caido');
        if (clase) chip.classList.add(clase);
    }

    // El estado del hub también le sirve a quien tenga su propio indicador —el panel
    // del administrador tiene uno dentro de su tarjeta de actividad—.
    function estadoConexion(ok, texto) {
        marcarChip(ok ? null : 'vivo-caido');
        document.dispatchEvent(new CustomEvent('vivo:conexion', { detail: { ok: ok, texto: texto } }));
    }

    // ── Lo que el refresco no puede tocar ──────────────────────────────────────
    // El servidor no sabe lo que solo existe en este navegador. Esas regiones se
    // sacan antes del cambio y se vuelven a poner en su lugar después.
    function apartarConservados() {
        var guardados = {};
        var nodos = cont.querySelectorAll('[data-vivo-conservar][id]');
        for (var i = 0; i < nodos.length; i++) guardados[nodos[i].id] = nodos[i];

        var valores = {};
        var campos = cont.querySelectorAll('[data-vivo-valor][id]');
        for (var j = 0; j < campos.length; j++) valores[campos[j].id] = campos[j].value;

        return { nodos: guardados, valores: valores };
    }

    function devolverConservados(g) {
        for (var id in g.nodos) {
            var nuevo = cont.querySelector('#' + CSS.escape(id));
            if (nuevo && nuevo.parentNode) nuevo.parentNode.replaceChild(g.nodos[id], nuevo);
        }
        for (var idc in g.valores) {
            var campo = cont.querySelector('#' + CSS.escape(idc));
            if (campo) campo.value = g.valores[idc];
        }
    }

    function latido() {
        marcarChip('vivo-late');
        setTimeout(function () { if (chip) chip.classList.remove('vivo-late'); }, 900);
    }

    // ── El refresco ────────────────────────────────────────────────────────────
    function refrescar() {
        if (refrescando) { pendiente = true; return; }
        if (document.hidden || ocupado()) { pendiente = true; return; }

        refrescando = true;
        // Se pide la misma dirección, con filtros y todo: refrescar no puede cambiar
        // lo que la persona estaba mirando.
        fetch(location.href, {
            credentials: 'same-origin',
            cache: 'no-store',
            headers: { 'X-Vivo': '1' }
        })
        .then(function (r) { if (!r.ok) throw new Error('http'); return r.text(); })
        .then(function (html) {
            var doc   = new DOMParser().parseFromString(html, 'text/html');
            var nuevo = doc.getElementById('vivo');
            // Sin bloque #vivo llegó otra cosa —la pantalla de ingreso, casi siempre,
            // porque venció la sesión—. Meter eso en la página sería peor que dejarla
            // quieta: se deja como está y el indicador avisa.
            if (!nuevo) { marcarChip('vivo-caido'); return; }

            var antes     = estados();
            var guardados = apartarConservados();
            cont.innerHTML = nuevo.innerHTML;
            devolverConservados(guardados);
            destellar(antes);
            latido();
            // Para que lo que se enganche a esta página pueda volver a montarse.
            document.dispatchEvent(new CustomEvent('vivo:actualizado'));
        })
        .catch(function () { /* red intermitente: la página sigue sirviendo */ })
        .then(function () {
            refrescando = false;
            if (pendiente) { pendiente = false; programar(400); }
        });
    }

    // Un aviso suelto y una ráfaga de quince deben costar lo mismo: se espera un
    // momento y se refresca una sola vez.
    function programar(ms) {
        clearTimeout(timer);
        timer = setTimeout(refrescar, ms || 700);
    }

    // ── Avisos del hub ─────────────────────────────────────────────────────────
    var conexion = new signalR.HubConnectionBuilder()
        .withUrl('/ventasHub')
        .withAutomaticReconnect()
        .build();

    function paraEsteProyecto(id) { return id === proyecto; }

    conexion.on('InmuebleActualizado', function (idProyecto) {
        if (paraEsteProyecto(idProyecto)) programar();
    });
    conexion.on('ListaActualizada', function (idProyecto) {
        if (paraEsteProyecto(idProyecto)) programar();
    });
    conexion.on('ListaAreaActualizada', function (idProyecto) {
        if (paraEsteProyecto(idProyecto)) programar();
    });
    conexion.on('PrecioAreaActualizado', function (idProyecto) {
        if (paraEsteProyecto(idProyecto)) programar();
    });

    conexion.onreconnecting(function () { estadoConexion(false, 'Reconectando…'); });
    conexion.onreconnected(function () { estadoConexion(true, 'En vivo'); programar(200); });
    conexion.onclose(function () { estadoConexion(false, 'Desconectado'); });

    // Antes de arrancar, para que quien cargue después pueda sumar sus propios avisos
    // a esta conexión en vez de abrir otra.
    window.vivo = { conexion: conexion, proyecto: proyecto, refrescar: programar };

    conexion.start()
        .then(function () { estadoConexion(true, 'En vivo'); })
        .catch(function () { estadoConexion(false, 'Sin conexión'); });

    // ── Dos redes de seguridad ─────────────────────────────────────────────────
    // Un aviso se puede perder: el navegador estaba suspendido, el hub se cayó y
    // volvió sin que nadie se enterara. Cada minuto y medio se refresca igual, y solo
    // con la pestaña a la vista para no trabajar de gratis en segundo plano.
    setInterval(function () { if (!document.hidden) programar(0); }, 90000);

    // Al volver a la pestaña, lo que se acumuló mientras estaba oculta se aplica ya.
    document.addEventListener('visibilitychange', function () {
        if (!document.hidden && pendiente) { pendiente = false; programar(100); }
    });
})();
