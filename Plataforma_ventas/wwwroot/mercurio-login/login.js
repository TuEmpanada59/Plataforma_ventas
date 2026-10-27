/* Mercurio · Login "Gran Tour galáctico" — conexión entre la interfaz y la escena 3D.
 *
 * Uso:
 *   MercurioLogin.init(document.getElementById('mercurio-login'), {
 *     base: '/mercurio-login/',            // carpeta donde están texturas/ (termina en /)
 *     intro: true,                          // false = arranca directo en Mercurio
 *     introUnaVezPorSesion: true,           // la intro solo se ve la primera vez de la sesión
 *     tono: 'plata',                        // 'plata' (marca) o 'real'
 *     onSubmit: async function (datos) {    // { usuario, contrasena, recordarme }
 *       // llama a tu backend; lanza un Error con mensaje para mostrarlo en la tarjeta
 *     },
 *     onForgot: function () { ... }         // opcional
 *   });
 */
(function () {
  'use strict';

  var CAPTIONS = [
    ['Sistema Solar exterior', 'Neptuno · Urano · Saturno'],
    ['Júpiter', 'El gigante del sistema'],
    ['Cinturón de asteroides', 'Entre Marte y Júpiter'],
    ['Planetas rocosos', 'Marte · Tierra y Luna · Venus'],
    ['Mercurio', '0,39 UA del Sol · el más rápido']
  ];

  function assetsFrom(base) {
    var t = base + 'texturas/';
    return {
      mercurio: t + 'mercurio-color.jpg', mercurioN: t + 'mercurio-relieve.jpg',
      sol: t + 'sol.jpg', venus: t + 'venus.jpg',
      tierra: t + 'tierra.jpg', tierraN: t + 'tierra-relieve.jpg', tierraS: t + 'tierra-oceanos.jpg', nubes: t + 'tierra-nubes.jpg',
      luna: t + 'luna.jpg', marte: t + 'marte.jpg', marteN: t + 'marte-relieve.jpg',
      jupiter: t + 'jupiter.jpg', saturno: t + 'saturno.jpg', anillos: t + 'saturno-anillos.png',
      urano: t + 'urano.jpg', neptuno: t + 'neptuno.jpg',
      cielo: ['px', 'nx', 'py', 'ny', 'pz', 'nz'].map(function (f) { return t + 'via-lactea-' + f + '.jpg'; })
    };
  }

  function init(root, opts) {
    opts = opts || {};
    if (!root || root.__mercurio) return root && root.__mercurio;
    var base = opts.base || root.getAttribute('data-base') || './';
    var $ = function (name) { return root.querySelector('[data-ml="' + name + '"]'); };
    var caption = $('caption'), lastPhase = -2;

    function setCaption(phase) {
      if (phase === lastPhase || !caption) return;
      lastPhase = phase;
      var c = CAPTIONS[phase];
      caption.classList.remove('is-anim');
      if (!c) { caption.textContent = ''; return; }
      caption.innerHTML = '<span class="ml-caption-title"></span><span class="ml-caption-sub"></span>';
      caption.firstChild.textContent = c[0];
      caption.lastChild.textContent = c[1];
      caption.classList.toggle('is-final', phase === CAPTIONS.length - 1);
      void caption.offsetWidth;
      caption.classList.add('is-anim');
    }

    // Intro solo la primera vez por sesión del navegador (opcional)
    var intro = opts.intro !== false;
    if (intro && opts.introUnaVezPorSesion) {
      try {
        if (window.sessionStorage.getItem('mercurio-intro-vista')) intro = false;
        window.sessionStorage.setItem('mercurio-intro-vista', '1');
      } catch (err) { /* almacenamiento bloqueado: se muestra la intro */ }
    }
    opts.intro = intro;

    var eng = null;
    if (window.THREE && window.MercurioEspacio) {
      eng = window.MercurioEspacio.create(root.querySelector('.ml-canvas'), {
        escena: 'tour',
        intro: intro,
        tono: opts.tono || 'plata',
        assets: assetsFrom(base),
        onState: function (phase, landed) {
          root.classList.toggle('ml-landed', !!landed);
          setCaption(landed ? -1 : phase);
        }
      });
      if (eng.failed) root.classList.add('ml-landed');
    } else {
      root.classList.add('ml-landed');
    }
    setCaption(opts.intro === false ? -1 : 0);

    function on(name, fn) { var el = $(name); if (el) el.addEventListener('click', fn); }
    on('skip', function () { if (eng) eng.skip(); });
    on('replay', function () { if (eng) eng.replay(); });
    on('zoom-in', function () { if (eng) eng.zoomBy(-0.35); });
    on('zoom-out', function () { if (eng) eng.zoomBy(0.35); });
    on('zoom-reset', function () { if (eng) eng.resetView(); });
    on('forgot', function (e) { if (opts.onForgot) { e.preventDefault(); opts.onForgot(); } });

    var pass = root.querySelector('#ml-pass'), toggle = $('toggle-pass');
    if (toggle && pass) toggle.addEventListener('click', function () {
      var show = pass.type === 'password';
      pass.type = show ? 'text' : 'password';
      toggle.setAttribute('aria-label', show ? 'Ocultar contraseña' : 'Mostrar contraseña');
    });

    var form = $('form'), submit = $('submit'), error = $('error');
    function showError(msg) {
      if (!error) return;
      error.textContent = msg || '';
      error.classList.toggle('is-visible', !!msg);
    }
    if (form) form.addEventListener('submit', function (e) {
      // POST clásico (p. ej. ASP.NET MVC): si el form tiene action y no hay onSubmit, se envía normal
      if (!opts.onSubmit && form.getAttribute('action')) {
        if (!form.usuario.value.trim() || !form.contrasena.value) { e.preventDefault(); showError('Escribe tu usuario y tu contraseña.'); return; }
        submit.classList.add('is-loading');
        return;
      }
      e.preventDefault();
      if (submit.classList.contains('is-loading')) return;
      var datos = {
        usuario: form.usuario.value.trim(),
        contrasena: form.contrasena.value,
        recordarme: form.recordarme.checked
      };
      if (!datos.usuario || !datos.contrasena) { showError('Escribe tu usuario y tu contraseña.'); return; }
      showError('');
      submit.classList.add('is-loading');
      var done = function () { submit.classList.remove('is-loading'); };
      if (!opts.onSubmit) { setTimeout(done, 2400); return; }
      Promise.resolve()
        .then(function () { return opts.onSubmit(datos); })
        .catch(function (err) { showError((err && err.message) || 'No pudimos iniciar sesión. Intenta de nuevo.'); })
        .then(done);
    });

    var api = {
      engine: eng,
      destroy: function () { if (eng) eng.dispose(); root.__mercurio = null; }
    };
    root.__mercurio = api;
    return api;
  }

  window.MercurioLogin = { init: init };

  function auto() {
    var r = document.getElementById('mercurio-login');
    if (r && r.hasAttribute('data-autoinit')) init(r);
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', auto); else auto();
})();
