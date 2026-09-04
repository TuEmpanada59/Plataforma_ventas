/* ═══════════════════════════════════════════
   LONDOÑO GÓMEZ — app.js
   Sidebar móvil + helpers globales
   Incluir al final de todas las vistas:
   <script src="/js/app.js"></script>
═══════════════════════════════════════════ */

(function () {

    /* ── Sidebar toggle ── */
    function toggleSidebar() {
        var sb = document.getElementById('sidebar') || document.querySelector('.sidebar');
        var ov = document.getElementById('sbOverlay');
        if (!sb) return;
        var isOpen = sb.classList.toggle('open');
        if (ov) ov.classList.toggle('open', isOpen);
        document.body.classList.toggle('sidebar-open', isOpen);
    }

    function closeSidebar() {
        var sb = document.getElementById('sidebar') || document.querySelector('.sidebar');
        var ov = document.getElementById('sbOverlay');
        if (!sb) return;
        sb.classList.remove('open');
        if (ov) ov.classList.remove('open');
        document.body.classList.remove('sidebar-open');
    }

    /* Exponer globalmente */
    window.toggleSidebar = toggleSidebar;
    window.closeSidebar = closeSidebar;

    /* Cerrar sidebar al hacer click en overlay */
    document.addEventListener('DOMContentLoaded', function () {
        var ov = document.getElementById('sbOverlay');
        if (ov) ov.addEventListener('click', closeSidebar);

        /* Cerrar sidebar al navegar en móvil */
        document.querySelectorAll('.nav-item').forEach(function (el) {
            el.addEventListener('click', function () {
                if (window.innerWidth <= 700) closeSidebar();
            });
        });

        /* Dropdown de proyectos */
        var proyBtn = document.getElementById('proyBtn');
        var proyDrop = document.getElementById('proyDrop');
        if (proyBtn && proyDrop) {
            proyBtn.addEventListener('click', function (e) {
                e.stopPropagation();
                proyDrop.classList.toggle('open');
            });
            document.addEventListener('click', function (e) {
                if (!proyBtn.contains(e.target) && !proyDrop.contains(e.target))
                    proyDrop.classList.remove('open');
            });
        }

        /* Inyectar hamburger en topbar si no existe */
        var topbar = document.querySelector('.topbar');
        if (topbar && !document.querySelector('.hamburger')) {
            var ham = document.createElement('button');
            ham.className = 'hamburger';
            ham.setAttribute('aria-label', 'Menú');
            ham.innerHTML = '<span></span><span></span><span></span>';
            ham.addEventListener('click', toggleSidebar);

            /* Envolver el primer hijo del topbar o insertarlo al inicio */
            var firstChild = topbar.firstElementChild;
            if (firstChild && firstChild.classList.contains('tb-left')) {
                firstChild.insertBefore(ham, firstChild.firstChild);
            } else if (firstChild && firstChild.classList.contains('tb-bread')) {
                var wrapper = document.createElement('div');
                wrapper.className = 'tb-left';
                topbar.insertBefore(wrapper, firstChild);
                wrapper.appendChild(ham);
                wrapper.appendChild(firstChild);
            } else {
                topbar.insertBefore(ham, topbar.firstChild);
            }
        }

        /* Inyectar overlay si no existe */
        if (!document.getElementById('sbOverlay')) {
            var ovEl = document.createElement('div');
            ovEl.className = 'sb-overlay';
            ovEl.id = 'sbOverlay';
            ovEl.addEventListener('click', closeSidebar);
            document.body.insertBefore(ovEl, document.body.firstChild);
        }

        /* Agregar id="sidebar" al sidebar si no lo tiene */
        var sb = document.querySelector('.sidebar');
        if (sb && !sb.id) sb.id = 'sidebar';
    });

})();

/* ═══════════════════════════════════════════
   Sincronización de sesión entre pestañas
   - La sesión (cookie) ya se comparte entre pestañas del mismo navegador,
     así que abrir una pestaña nueva mantiene la misma sesión iniciada.
   - Si se cierra sesión en una pestaña, todas las demás también salen.
═══════════════════════════════════════════ */
(function () {
    var CANAL = 'lg_sesion_evento';

    /* Escuchar el cierre de sesión hecho en otra pestaña */
    window.addEventListener('storage', function (e) {
        if (e.key === CANAL && e.newValue && e.newValue.indexOf('logout') === 0) {
            window.location.replace('/Account/Login');
        }
    });

    /* Avisar a las demás pestañas cuando se cierra sesión desde esta */
    document.addEventListener('click', function (e) {
        var a = e.target && e.target.closest && e.target.closest('a[href*="/Account/Logout"]');
        if (a) {
            try { localStorage.setItem(CANAL, 'logout|' + Date.now()); } catch (err) { }
        }
    });
})();