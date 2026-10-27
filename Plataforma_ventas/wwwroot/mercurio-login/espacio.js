/* Mercurio · motor 3D compartido para los logins espaciales (Three.js r159). */
(function () {
  'use strict';

  var B = function (id) { return '/_blob/' + id; };
  var A = {
    mercurio: B('d17999a42bc8e03d6d4fe0f8b1ca1841'), mercurioN: B('778f30456666e4c606ba2f822d68d76d'),
    sol: B('20bea8190b3ad6dbd3f3e10ce15b6620'),
    venus: B('40dc1d1bb3f32c1ed0f7db6956dc8151'),
    tierra: B('4aa14dcc1089134d1fbf31f8257c4d72'), tierraN: B('a2dfa02787674f14138db2ca4ca4fbaa'),
    tierraS: B('767b882f2221fccf2e23e6ac40f8b216'), nubes: B('fb10c56a8a9d88c91daafe0561f9c156'),
    luna: B('650a750d728f31200fc18aef17ff09a0'),
    marte: B('b4207e0188c4c1137e8c2fad6f5d2a9a'), marteN: B('0535b31ebf09ae68b4ebffce6f2994b2'),
    jupiter: B('9afd47a96f482a17905b28d43142959a'),
    saturno: B('fa32aa38ba4704a51d3c77ecc8b32319'), anillos: B('6f3b00b9ae3cca286f96657f44ae4837'),
    urano: B('f7917f177558a5f09368c4e3f6d049c5'), neptuno: B('16fc5e92621948e4821c0cb43b301b58'),
    cielo: [
      B('7895440123cbc1f9528180bc60b7bb2b'), B('4688a5b828328725029a57a88a6bc176'),
      B('bf50069daa51f2dc54c0cb4fa845ee3d'), B('dbe466092d79891d2a79dfbc7c88df34'),
      B('8337e2a893f7f29d972f25c0f653fe86'), B('38db3c6ff0585d786433ded88b329a42')
    ]
  };

  var INFO = {
    mercurio: ['MERCURIO', '0,39 UA · Ø 4.879 km'],
    venus: ['VENUS', '0,72 UA · Ø 12.104 km'],
    tierra: ['TIERRA', '1 UA · Ø 12.742 km'],
    luna: ['LUNA', 'a 384.400 km de la Tierra'],
    marte: ['MARTE', '1,52 UA · Ø 6.779 km'],
    jupiter: ['JÚPITER', '5,2 UA · Ø 139.820 km'],
    saturno: ['SATURNO', '9,5 UA · Ø 116.460 km'],
    urano: ['URANO', '19,2 UA · Ø 50.724 km'],
    neptuno: ['NEPTUNO', '30,1 UA · Ø 49.244 km']
  };
  var TILT = { mercurio: 0.0006, venus: 3.096, tierra: 0.409, luna: 0.027, marte: 0.44, jupiter: 0.055, saturno: 0.466, urano: 1.706, neptuno: 0.494 };
  var SPIN = { mercurio: 0.05, venus: -0.01, tierra: 0.08, luna: 0.02, marte: 0.08, jupiter: 0.16, saturno: 0.14, urano: 0.1, neptuno: 0.1 };
  var ATMO = {
    venus: ['#ffdcaa', 3.0, 0.9], tierra: ['#5fb4ff', 3.2, 1.7], marte: ['#ffb08a', 4.0, 0.35],
    jupiter: ['#f2dcc0', 4.0, 0.35], saturno: ['#f6e3c0', 4.0, 0.3], urano: ['#9fe8ff', 2.6, 0.7], neptuno: ['#6fa8ff', 2.6, 0.8]
  };

  var NOISE = [
    'float hash(vec2 p) { p = fract(p * vec2(123.34, 456.21)); p += dot(p, p + 45.32); return fract(p.x * p.y); }',
    'float noise(vec2 p) {',
    '  vec2 i = floor(p); vec2 f = fract(p); vec2 u = f * f * (3.0 - 2.0 * f);',
    '  return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);',
    '}',
    'float fbm(vec2 p) {',
    '  float v = 0.0; float a = 0.5;',
    '  for (int i = 0; i < 5; i++) { v += a * noise(p); p = p * 2.03 + 11.7; a *= 0.5; }',
    '  return v;',
    '}',
    'float tri(vec3 n, float s, float t) {',
    '  vec3 w = abs(n); w /= (w.x + w.y + w.z);',
    '  return fbm(n.yz * s + t) * w.x + fbm(n.zx * s - t) * w.y + fbm(n.xy * s + t * 0.7) * w.z;',
    '}'
  ].join('\n');

  function c01(x) { return Math.min(Math.max(x, 0), 1); }
  function smooth(a, b, x) { var t = c01((x - a) / (b - a)); return t * t * (3 - 2 * t); }
  function out(x, k) { return 1 - Math.pow(1 - c01(x), k); }
  function gauss() { var u = 0; while (!u) u = Math.random(); return Math.sqrt(-2 * Math.log(u)) * Math.cos(2 * Math.PI * Math.random()); }

  // ------------------------------------------------------------------
  function Engine(canvas, opts) {
    this.canvas = canvas;
    this.opts = opts || {};
    this.cfg = SCENES[this.opts.escena] || SCENES.tour;
    this.A = this.opts.assets ? Object.assign({}, A, this.opts.assets) : A;
    this.T = window.THREE;
    this.planets = [];
    this.labels = [];
    this.phase = 0;
    this.landed = false;
    this.k = 0;
    this.px = 0; this.py = 0; this.sx = 0; this.sy = 0;
    this.visible = true;
    this.labelAll = 0;
    this.swayAmp = 0.12;
    this.flareK = 0;
    this.pmats = [];
    this.lookDir = null;
    var self = this;
    this.onMove = function (e) {
      var b = self.canvas.getBoundingClientRect();
      self.px = ((e.clientX - b.left) / Math.max(b.width, 1)) * 2 - 1;
      self.py = ((e.clientY - b.top) / Math.max(b.height, 1)) * 2 - 1;
    };
    this.zoom = 0; this.zoomT = 0; this.pinch = 0;
    this.onWheel = function (e) {
      e.preventDefault();
      self.zoomBy(e.deltaY * (e.deltaMode === 1 ? 0.04 : 0.0016));
    };
    this.onTouchStart = function (e) {
      if (e.touches.length === 2) self.pinch = self.touchDist(e);
    };
    this.onTouchMove = function (e) {
      if (e.touches.length !== 2 || !self.pinch) return;
      e.preventDefault();
      var d = self.touchDist(e);
      self.zoomBy(-Math.log(d / self.pinch) * 1.4);
      self.pinch = d;
    };
    // giro 360° arrastrando (mouse o un dedo), con inercia
    this.yaw = 0; this.pitch = 0; this.yawT = 0; this.pitchT = 0; this.vYaw = 0; this.vPitch = 0;
    this.drag = null; this.ptrs = {};
    this.onDown = function (e) {
      if (e.target && e.target.closest && e.target.closest('input,button,a,label,select,textarea,[data-no-orbit]')) return;
      self.ptrs[e.pointerId] = true;
      if (Object.keys(self.ptrs).length === 1) {
        self.drag = { id: e.pointerId, x: e.clientX, y: e.clientY };
        self.vYaw = 0; self.vPitch = 0;
        try { self.host.setPointerCapture(e.pointerId); } catch (err) { /* sin captura */ }
      } else {
        self.drag = null;
      }
    };
    this.onDragMove = function (e) {
      var d = self.drag;
      if (!d || e.pointerId !== d.id) return;
      var k = 3.4 / Math.max(self.host.clientHeight || 800, 1);
      var dx = e.clientX - d.x, dy = e.clientY - d.y;
      d.x = e.clientX; d.y = e.clientY;
      self.yawT -= dx * k;
      self.pitchT = Math.min(Math.max(self.pitchT - dy * k, -1.45), 1.45);
      self.vYaw = -dx * k; self.vPitch = -dy * k;
    };
    this.onUp = function (e) {
      delete self.ptrs[e.pointerId];
      if (self.drag && e.pointerId === self.drag.id) self.drag = null;
    };
    this.frame = this.frame.bind(this);
    this.start();
  }

  Engine.prototype.resetView = function () {
    this.zoomT = 0;
    this.yawT = Math.round(this.yaw / (Math.PI * 2)) * Math.PI * 2;
    this.pitchT = 0; this.vYaw = 0; this.vPitch = 0;
  };

  Engine.prototype.touchDist = function (e) {
    var a = e.touches[0], b = e.touches[1];
    return Math.max(Math.hypot(a.clientX - b.clientX, a.clientY - b.clientY), 1);
  };
  Engine.prototype.zoomBy = function (dz) {
    var lo = this.cfg.zoomMin !== undefined ? this.cfg.zoomMin : -0.8, hi = this.cfg.zoomMax !== undefined ? this.cfg.zoomMax : 2.3;
    this.zoomT = Math.min(Math.max(this.zoomT + dz, lo), hi);
  };
  Engine.prototype.resetZoom = function () { this.resetView(); };

  Engine.prototype.V = function (x, y, z) { return new this.T.Vector3(x, y, z); };
  Engine.prototype.smooth = smooth;
  Engine.prototype.out = out;

  Engine.prototype.start = function () {
    var T = this.T, r;
    try {
      r = new T.WebGLRenderer({ canvas: this.canvas, antialias: true, powerPreference: 'high-performance' });
    } catch (err) {
      this.failed = true;
      if (this.opts.onState) this.opts.onState(9, true);
      return;
    }
    r.setPixelRatio(Math.min(window.devicePixelRatio || 1, 1.5));
    r.outputColorSpace = T.SRGBColorSpace;
    r.toneMapping = T.ACESFilmicToneMapping;
    r.toneMappingExposure = 1.0;
    r.autoClear = false;
    r.setClearColor(0x05070c, 1);
    this.r = r;
    this.loader = new T.TextureLoader();
    this.aniso = r.capabilities.getMaxAnisotropy();
    this.glowTex = this.makeGlowTex(false);
    this.ghostTex = this.makeGlowTex(true);

    this.cam = new T.PerspectiveCamera(40, 1, 0.1, 9000);
    this.skyCam = new T.PerspectiveCamera(40, 1, 0.01, 10);
    this.flareCam = new T.OrthographicCamera(-1, 1, 1, -1, 0, 10);
    this.cams = [this.cam, this.skyCam];
    this.scene = new T.Scene();
    this.sky = new T.Scene();
    this.flareScene = new T.Scene();
    this.qSky = new T.Quaternion().setFromEuler(new T.Euler(0.35, 0.2, 0.62));

    var cube = new T.CubeTextureLoader().load(this.A.cielo);
    cube.colorSpace = T.SRGBColorSpace;
    this.sky.background = cube;
    this.sky.backgroundIntensity = 1.35;
    this.sky.add(this.makeStars(5000));

    this.sunPos = new T.Vector3(0, 0, 0);
    this._v1 = new T.Vector3(); this._v2 = new T.Vector3(); this._v3 = new T.Vector3(); this._v4 = new T.Vector3();
    this._w = new T.Vector3(); this._ahead = new T.Vector3(); this._tgt = new T.Vector3();
    this._Y = new T.Vector3(0, 1, 0);
    this._right = new T.Vector3(); this._up = new T.Vector3(); this._fwd = new T.Vector3();

    this.cfg.build(this);
    if (this.cfg.exposure) r.toneMappingExposure = this.cfg.exposure;
    if (this.cfg.fill) {
      this.fill = new T.DirectionalLight(0xdbe6ff, this.cfg.fill);
      this.scene.add(this.fill);
      this.scene.add(this.fill.target);
    }
    this.makeFlare();
    this.applyTone(this.opts.tono || 'plata');
    this.resize();

    var reduce = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    this.skipOnStart = reduce || this.opts.intro === false;
    this.t0 = null;
    var self = this;
    if (window.ResizeObserver) { this.ro = new ResizeObserver(function () { self.resize(); }); this.ro.observe(this.canvas); }
    if (window.IntersectionObserver) { this.io = new IntersectionObserver(function (en) { if (en[0]) self.visible = en[0].isIntersecting; }); this.io.observe(this.canvas); }
    window.addEventListener('pointermove', this.onMove);
    this.host = this.canvas.parentElement || this.canvas;
    this.host.addEventListener('wheel', this.onWheel, { passive: false });
    this.host.addEventListener('touchstart', this.onTouchStart, { passive: true });
    this.host.addEventListener('touchmove', this.onTouchMove, { passive: false });
    this.host.addEventListener('pointerdown', this.onDown);
    this.host.addEventListener('pointermove', this.onDragMove);
    this.host.addEventListener('pointerup', this.onUp);
    this.host.addEventListener('pointercancel', this.onUp);
    this._q1 = new T.Quaternion(); this._q2 = new T.Quaternion(); this._F = new T.Vector3();
    this.raf = requestAnimationFrame(this.frame);
  };

  Engine.prototype.dispose = function () {
    cancelAnimationFrame(this.raf);
    if (this.ro) this.ro.disconnect();
    if (this.io) this.io.disconnect();
    window.removeEventListener('pointermove', this.onMove);
    if (this.host) {
      this.host.removeEventListener('wheel', this.onWheel);
      this.host.removeEventListener('touchstart', this.onTouchStart);
      this.host.removeEventListener('touchmove', this.onTouchMove);
      this.host.removeEventListener('pointerdown', this.onDown);
      this.host.removeEventListener('pointermove', this.onDragMove);
      this.host.removeEventListener('pointerup', this.onUp);
      this.host.removeEventListener('pointercancel', this.onUp);
    }
    if (this.r) { this.r.dispose(); this.r = null; }
  };

  Engine.prototype.skip = function () { if (this.t0 !== null) this.t0 = performance.now() - this.cfg.INTRO * 1000; };
  Engine.prototype.replay = function () { this.t0 = performance.now(); this.lookDir = null; this.phase = -1; this.landed = true; };

  Engine.prototype.applyTone = function (tono) {
    if (!this.merc) return;
    this.merc.mesh.material.color.set(tono === 'real' ? '#ffffff' : '#c3cfdb');
  };
  Engine.prototype.setTone = Engine.prototype.applyTone;

  Engine.prototype.setPhase = function (p) { this.phase = p; };

  // ---------- recursos ----------
  Engine.prototype.tex = function (url, srgb) {
    var tx = this.loader.load(url);
    if (srgb !== false) tx.colorSpace = this.T.SRGBColorSpace;
    tx.anisotropy = this.aniso;
    return tx;
  };

  Engine.prototype.makeGlowTex = function (ghost) {
    var T = this.T, c = document.createElement('canvas');
    c.width = 256; c.height = 256;
    var g = c.getContext('2d'), grd = g.createRadialGradient(128, 128, 0, 128, 128, 128);
    if (ghost) {
      grd.addColorStop(0, 'rgba(255,255,255,0.18)');
      grd.addColorStop(0.75, 'rgba(255,255,255,0.32)');
      grd.addColorStop(0.92, 'rgba(255,255,255,0.75)');
      grd.addColorStop(1, 'rgba(255,255,255,0)');
    } else {
      grd.addColorStop(0, 'rgba(255,255,255,1)');
      grd.addColorStop(0.08, 'rgba(255,255,255,0.85)');
      grd.addColorStop(0.25, 'rgba(255,255,255,0.22)');
      grd.addColorStop(0.6, 'rgba(255,255,255,0.05)');
      grd.addColorStop(1, 'rgba(255,255,255,0)');
    }
    g.fillStyle = grd; g.fillRect(0, 0, 256, 256);
    var t = new T.CanvasTexture(c);
    t.colorSpace = T.SRGBColorSpace;
    return t;
  };

  Engine.prototype.pointsMaterial = function (max) {
    var T = this.T;
    var m = new T.ShaderMaterial({
      uniforms: { uScale: { value: 1000 }, uMax: { value: max }, uOpacity: { value: 1 } },
      vertexShader: [
        'attribute float aSize;', 'attribute vec3 aColor;', 'uniform float uScale;', 'uniform float uMax;',
        'varying vec3 vColor;', 'varying float vBright;',
        'void main() {',
        '  vec4 mv = modelViewMatrix * vec4(position, 1.0);',
        '  float d = max(-mv.z, 0.0001);',
        '  float s = aSize * uScale / d;',
        '  gl_PointSize = clamp(s, 1.0, uMax);',
        '  vBright = min(s, 1.0) * smoothstep(0.3, 3.0, d);',
        '  vColor = aColor;',
        '  gl_Position = projectionMatrix * mv;',
        '}'
      ].join('\n'),
      fragmentShader: [
        'uniform float uOpacity;', 'varying vec3 vColor;', 'varying float vBright;',
        'void main() {',
        '  vec2 p = gl_PointCoord - 0.5;',
        '  float r2 = dot(p, p);',
        '  if (r2 > 0.25) discard;',
        '  gl_FragColor = vec4(vColor * exp(-r2 * 16.0) * vBright * uOpacity, 1.0);',
        '  #include <tonemapping_fragment>',
        '  #include <colorspace_fragment>',
        '}'
      ].join('\n'),
      transparent: true, depthWrite: false, blending: T.AdditiveBlending
    });
    m.userData.max = max;
    this.pmats.push(m);
    return m;
  };

  Engine.prototype.pointsFrom = function (pos, col, size, max) {
    var T = this.T, geo = new T.BufferGeometry();
    geo.setAttribute('position', new T.BufferAttribute(pos, 3));
    geo.setAttribute('aColor', new T.BufferAttribute(col, 3));
    geo.setAttribute('aSize', new T.BufferAttribute(size, 1));
    var p = new T.Points(geo, this.pointsMaterial(max));
    p.frustumCulled = false;
    return p;
  };

  Engine.prototype.makeStars = function (N) {
    var T = this.T, pos = new Float32Array(N * 3), col = new Float32Array(N * 3), size = new Float32Array(N);
    var cols = ['#ffffff', '#c3cfdb', '#ffe9c9', '#cdefff', '#ffd6ad'].map(function (h) { return new T.Color(h); });
    var v = new T.Vector3();
    for (var i = 0; i < N; i++) {
      v.set(gauss(), gauss(), gauss()).normalize();
      var br = 0.25 + 1.2 * Math.pow(Math.random(), 7), s = 0.0011 + 0.0032 * Math.pow(Math.random(), 8);
      var c = cols[(Math.random() * cols.length) | 0];
      pos[i * 3] = v.x; pos[i * 3 + 1] = v.y; pos[i * 3 + 2] = v.z;
      col[i * 3] = c.r * br; col[i * 3 + 1] = c.g * br; col[i * 3 + 2] = c.b * br;
      size[i] = s;
    }
    return this.pointsFrom(pos, col, size, 4);
  };

  // ---------- Sol ----------
  Engine.prototype.addSun = function (radius, bright) {
    var T = this.T;
    var mat = new T.ShaderMaterial({
      uniforms: { uMap: { value: this.tex(this.A.sol) }, uTime: { value: 0 }, uGain: { value: 3.0 } },
      vertexShader: [
        'varying vec2 vUv; varying vec3 vN; varying vec3 vO; varying vec3 vV;',
        'void main() {',
        '  vUv = uv; vO = position;',
        '  vec4 wp = modelMatrix * vec4(position, 1.0);',
        '  vN = normalize(mat3(modelMatrix) * normal);',
        '  vV = cameraPosition - wp.xyz;',
        '  gl_Position = projectionMatrix * viewMatrix * wp;',
        '}'
      ].join('\n'),
      fragmentShader: [
        'uniform sampler2D uMap; uniform float uTime; uniform float uGain;',
        'varying vec2 vUv; varying vec3 vN; varying vec3 vO; varying vec3 vV;',
        NOISE,
        'void main() {',
        '  vec3 n = normalize(vO);',
        '  float flow = tri(n, 2.5, uTime * 0.03);',
        '  vec2 uv = vUv + (flow - 0.5) * 0.008;',
        '  vec3 tex = texture2D(uMap, uv).rgb;',
        '  float gran = tri(n, 38.0, uTime * 0.08);',
        '  float mu = max(dot(normalize(vN), normalize(vV)), 0.0);',
        '  float limb = 0.32 + 0.68 * pow(mu, 0.55);',
        '  vec3 col = tex * (0.7 + 0.6 * gran) * limb;',
        '  col = mix(col, vec3(1.0, 0.5, 0.18) * limb, 0.3 * (1.0 - mu));',
        '  gl_FragColor = vec4(col * uGain, 1.0);',
        '  #include <tonemapping_fragment>',
        '  #include <colorspace_fragment>',
        '}'
      ].join('\n')
    });
    var g = new T.Group();
    g.position.copy(this.sunPos);
    var sphere = new T.Mesh(new T.SphereGeometry(radius, 96, 64), mat);
    g.add(sphere);
    var corMat = new T.ShaderMaterial({
      uniforms: { uTime: { value: 0 }, uOpacity: { value: 1 }, uCol: { value: new T.Color('#ffcf8a') } },
      vertexShader: 'varying vec2 vUv; void main() { vUv = uv; gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }',
      fragmentShader: [
        'uniform float uTime; uniform float uOpacity; uniform vec3 uCol;', 'varying vec2 vUv;', NOISE,
        'void main() {',
        '  vec2 p = (vUv - 0.5) * 2.0;',
        '  float L = length(p);',
        '  float r = L * 4.5;',
        '  if (r < 0.97) discard;',
        '  vec2 dir = p / max(L, 0.0001);',
        '  float n1 = fbm(dir * 2.2 + vec2(r * 0.35 - uTime * 0.05, uTime * 0.02));',
        '  float n2 = fbm(dir * 5.0 + vec2(-uTime * 0.03, r * 0.8));',
        '  float fall = exp(-(r - 1.0) * 2.6) * (0.2 + 1.4 * n1 * n2) + exp(-(r - 1.0) * 12.0) * 1.1;',
        '  fall *= 1.0 - smoothstep(3.0, 4.4, r);',
        '  gl_FragColor = vec4(uCol * fall * uOpacity, 1.0);',
        '  #include <tonemapping_fragment>',
        '  #include <colorspace_fragment>',
        '}'
      ].join('\n'),
      transparent: true, depthWrite: false, blending: T.AdditiveBlending
    });
    var corona = new T.Mesh(new T.PlaneGeometry(radius * 9, radius * 9), corMat);
    g.add(corona);
    var ga = new T.Sprite(new T.SpriteMaterial({ map: this.glowTex, color: 0xfff1d6, transparent: true, opacity: 0.85, depthWrite: false, blending: T.AdditiveBlending }));
    ga.scale.setScalar(radius * 5);
    var gb = new T.Sprite(new T.SpriteMaterial({ map: this.glowTex, color: 0xffd9a8, transparent: true, opacity: 0.25, depthWrite: false, blending: T.AdditiveBlending }));
    gb.scale.setScalar(radius * 20);
    g.add(ga); g.add(gb);
    this.scene.add(g);
    var light = new T.PointLight(0xfff4e8, bright ? 4.6 : 3.1, 0, 0);
    light.position.copy(this.sunPos);
    this.scene.add(light);
    this.scene.add(new T.AmbientLight(0xc3cfdb, bright ? 0.16 : 0.025));
    this.sunObj = { g: g, mat: mat, corona: corona, corMat: corMat, radius: radius, parts: [ga, gb] };
    return this.sunObj;
  };

  // ---------- planetas ----------
  Engine.prototype.atmosphere = function (radius, color, power, strength) {
    var T = this.T;
    var mat = new T.ShaderMaterial({
      uniforms: { uColor: { value: new T.Color(color) }, uSun: { value: this.sunPos }, uPow: { value: power }, uStr: { value: strength } },
      vertexShader: 'varying vec3 vN; varying vec3 vW; void main() { vN = normalize(mat3(modelMatrix) * normal); vec4 w = modelMatrix * vec4(position, 1.0); vW = w.xyz; gl_Position = projectionMatrix * viewMatrix * w; }',
      fragmentShader: [
        'uniform vec3 uColor; uniform vec3 uSun; uniform float uPow; uniform float uStr;',
        'varying vec3 vN; varying vec3 vW;',
        'void main() {',
        '  vec3 n = normalize(vN);',
        '  vec3 V = normalize(cameraPosition - vW);',
        '  vec3 L = normalize(uSun - vW);',
        '  float f = pow(1.0 - max(dot(n, V), 0.0), uPow);',
        '  float lit = smoothstep(-0.3, 0.5, dot(n, L));',
        '  gl_FragColor = vec4(uColor * f * lit * uStr, 1.0);',
        '  #include <tonemapping_fragment>',
        '  #include <colorspace_fragment>',
        '}'
      ].join('\n'),
      transparent: true, depthWrite: false, blending: T.AdditiveBlending
    });
    return new T.Mesh(new T.SphereGeometry(radius, 96, 64), mat);
  };

  Engine.prototype.label = function (t1, t2) {
    var T = this.T, c = document.createElement('canvas');
    c.width = 1024; c.height = 256;
    var g = c.getContext('2d');
    g.strokeStyle = 'rgba(0,201,255,0.9)'; g.lineWidth = 3;
    g.beginPath(); g.moveTo(8, 248); g.lineTo(52, 176); g.lineTo(960, 176); g.stroke();
    g.fillStyle = '#00d4a1'; g.beginPath(); g.arc(8, 248, 7, 0, Math.PI * 2); g.fill();
    g.fillStyle = '#ffffff'; g.font = '600 62px "JetBrains Mono", "SFMono-Regular", Menlo, Consolas, monospace';
    g.fillText(t1, 66, 152);
    g.fillStyle = '#c3cfdb'; g.font = '400 38px "JetBrains Mono", "SFMono-Regular", Menlo, Consolas, monospace';
    g.fillText(t2, 66, 228);
    var tx = new T.CanvasTexture(c);
    tx.colorSpace = T.SRGBColorSpace;
    tx.anisotropy = this.aniso;
    var s = new T.Sprite(new T.SpriteMaterial({ map: tx, transparent: true, opacity: 0, depthTest: false, depthWrite: false, sizeAttenuation: false, toneMapped: false }));
    s.center.set(0, 0);
    s.scale.set(0.34, 0.085, 1);
    s.renderOrder = 20;
    this.scene.add(s);
    return s;
  };

  Engine.prototype.planet = function (kind, radius, pos, opt) {
    var T = this.T;
    opt = opt || {};
    var g = new T.Group();
    g.position.copy(pos);
    this.scene.add(g);
    var tilt = new T.Group();
    tilt.rotation.z = opt.tilt !== undefined ? opt.tilt : (TILT[kind] || 0);
    g.add(tilt);
    var seg = kind === 'mercurio' ? 192 : radius > 20 ? 128 : 96;
    var mat;
    if (kind === 'mercurio') {
      mat = new T.MeshStandardMaterial({ map: this.tex(this.A.mercurio), normalMap: this.tex(this.A.mercurioN, false), normalScale: new T.Vector2(1.4, 1.4), roughness: 0.96, metalness: 0 });
    } else if (kind === 'tierra') {
      mat = new T.MeshPhongMaterial({ map: this.tex(this.A.tierra), normalMap: this.tex(this.A.tierraN, false), normalScale: new T.Vector2(0.8, 0.8), specularMap: this.tex(this.A.tierraS, false), specular: new T.Color(0x4a5560), shininess: 22 });
    } else if (kind === 'marte') {
      mat = new T.MeshStandardMaterial({ map: this.tex(this.A.marte), normalMap: this.tex(this.A.marteN, false), normalScale: new T.Vector2(1.2, 1.2), roughness: 1, metalness: 0 });
    } else {
      mat = new T.MeshStandardMaterial({ map: this.tex(this.A[kind]), roughness: 1, metalness: 0 });
    }
    var mesh = new T.Mesh(new T.SphereGeometry(radius, seg, Math.round(seg * 0.66)), mat);
    tilt.add(mesh);
    var clouds = null;
    if (kind === 'tierra') {
      clouds = new T.Mesh(new T.SphereGeometry(radius * 1.012, 96, 64), new T.MeshStandardMaterial({ color: 0xffffff, alphaMap: this.tex(this.A.nubes, false), transparent: true, depthWrite: false, roughness: 1, metalness: 0 }));
      tilt.add(clouds);
    }
    if (ATMO[kind]) g.add(this.atmosphere(radius * 1.035, ATMO[kind][0], ATMO[kind][1], ATMO[kind][2]));
    if (kind === 'saturno') {
      var ring = new T.Mesh(new T.PlaneGeometry(radius * 4.75, radius * 4.75), new T.MeshBasicMaterial({ map: this.tex(this.A.anillos), transparent: true, side: T.DoubleSide, depthWrite: false, color: new T.Color(0.95, 0.92, 0.88) }));
      ring.rotation.x = -Math.PI / 2;
      tilt.add(ring);
    }
    var entry = { kind: kind, g: g, tilt: tilt, mesh: mesh, clouds: clouds, radius: radius, pos: g.position, spin: SPIN[kind] || 0.05, seed: Math.random() * 6, labelOp: 0 };
    if (opt.label !== false && INFO[kind]) entry.label = this.label(INFO[kind][0], INFO[kind][1]);
    if (kind === 'mercurio') this.merc = entry;
    this.planets.push(entry);
    return entry;
  };

  // ---------- cinturón, polvo, órbitas ----------
  Engine.prototype.rockGeometry = function (detail) {
    var T = this.T, geo = new T.SphereGeometry(1, detail >= 3 ? 11 : 7, detail >= 3 ? 8 : 5), p = geo.attributes.position, v = new T.Vector3();
    // deformación suave en varias frecuencias para que parezcan rocas reales y no poliedros
    var s1 = Math.random() * 10, s2 = Math.random() * 10;
    for (var i = 0; i < p.count; i++) {
      v.fromBufferAttribute(p, i).normalize();
      var n = 0.32 * Math.sin(v.x * 2.1 + s1) * Math.cos(v.y * 1.7 - s2)
            + 0.16 * Math.sin(v.y * 4.3 + v.z * 3.1 + s2)
            + 0.07 * Math.sin(v.z * 9.7 + v.x * 7.9 + s1);
      v.multiplyScalar(1 + n);
      p.setXYZ(i, v.x, v.y * 0.82, v.z);
    }
    geo.computeVertexNormals();
    return geo;
  };

  Engine.prototype.rockMaterial = function (tint) {
    var T = this.T;
    if (!this._rockMap) this._rockMap = this.tex(this.A.luna);
    return new T.MeshStandardMaterial({ map: this._rockMap, color: new T.Color(tint || '#8a8079'), roughness: 1, metalness: 0 });
  };

  Engine.prototype.pathSamples = function () {
    if (this._samples || !this.path) return this._samples;
    this._samples = this.path.getSpacedPoints(400);
    return this._samples;
  };

  Engine.prototype.nearPath = function (x, y, z, clear) {
    var s = this.pathSamples();
    if (!s) return false;
    var c2 = clear * clear;
    for (var i = 0; i < s.length; i++) {
      var dx = s[i].x - x, dy = s[i].y - y, dz = s[i].z - z;
      if (dx * dx + dy * dy + dz * dz < c2) return true;
    }
    return false;
  };

  Engine.prototype.addBelt = function (o) {
    var T = this.T;
    var mat = this.rockMaterial('#ffffff');
    o.tint = o.tint || 1.25;
    var inst = new T.InstancedMesh(this.rockGeometry(3), mat, o.count);
    var d = new T.Object3D(), col = new T.Color(), base = [new T.Color('#8a8079'), new T.Color('#6f665e'), new T.Color('#9a9086'), new T.Color('#5e5853')];
    for (var i = 0; i < o.count; i++) {
      var x, y, z, tries = 0;
      do {
        var a = o.ang + (o.uniform ? (Math.random() * 2 - 1) * o.spread : gauss() * o.spread);
        var rr = o.rIn + (o.rOut - o.rIn) * Math.random();
        x = rr * Math.cos(a); z = rr * Math.sin(a); y = gauss() * o.thick;
        tries++;
      } while (o.clear && this.nearPath(x, y, z, o.clear) && tries < 12);
      var s = (o.minS || 0.3) + (o.maxS || 3.4) * Math.pow(Math.random(), 6);
      d.position.set(x, y, z);
      d.rotation.set(Math.random() * 6.28, Math.random() * 6.28, Math.random() * 6.28);
      d.scale.set(s * (0.7 + 0.6 * Math.random()), s * (0.7 + 0.6 * Math.random()), s * (0.7 + 0.6 * Math.random()));
      d.updateMatrix();
      inst.setMatrixAt(i, d.matrix);
      col.copy(base[(Math.random() * base.length) | 0]).multiplyScalar(o.tint || 1);
      inst.setColorAt(i, col);
    }
    inst.instanceMatrix.needsUpdate = true;
    if (inst.instanceColor) inst.instanceColor.needsUpdate = true;
    if (inst.computeBoundingSphere) inst.computeBoundingSphere(); else inst.frustumCulled = false;
    this.scene.add(inst);
    if (o.dust !== false) {
      var N = o.dustCount || 6000, pos = new Float32Array(N * 3), cl = new Float32Array(N * 3), sz = new Float32Array(N), pc = new T.Color('#c3cfdb');
      for (var j = 0; j < N; j++) {
        var a2 = Math.random() * Math.PI * 2, r2 = o.rIn - 40 + (o.rOut - o.rIn + 80) * Math.random();
        pos[j * 3] = r2 * Math.cos(a2); pos[j * 3 + 1] = gauss() * o.thick * 1.4; pos[j * 3 + 2] = r2 * Math.sin(a2);
        var b = 0.12 + 0.25 * Math.random();
        cl[j * 3] = pc.r * b; cl[j * 3 + 1] = pc.g * b; cl[j * 3 + 2] = pc.b * b;
        sz[j] = 0.6 + Math.random();
      }
      this.scene.add(this.pointsFrom(pos, cl, sz, 3));
    }
    return inst;
  };

  Engine.prototype.addDust = function (N, spread) {
    var T = this.T, s = this.pathSamples();
    if (!s) return;
    var pos = new Float32Array(N * 3), cl = new Float32Array(N * 3), sz = new Float32Array(N);
    var cols = [new T.Color('#c3cfdb'), new T.Color('#00c9ff'), new T.Color('#ffffff')];
    for (var i = 0; i < N; i++) {
      var p = s[(Math.random() * s.length) | 0];
      var ox, oy, oz;
      do { ox = gauss() * spread; oy = gauss() * spread * 0.6; oz = gauss() * spread; } while (ox * ox + oy * oy + oz * oz < 9);
      pos[i * 3] = p.x + ox; pos[i * 3 + 1] = p.y + oy; pos[i * 3 + 2] = p.z + oz;
      var c = cols[(Math.random() * cols.length) | 0], b = 0.18 + 0.4 * Math.random();
      cl[i * 3] = c.r * b; cl[i * 3 + 1] = c.g * b; cl[i * 3 + 2] = c.b * b;
      sz[i] = 0.12 + 0.35 * Math.random();
    }
    this.dust = this.pointsFrom(pos, cl, sz, 3);
    this.scene.add(this.dust);
  };

  Engine.prototype.orbitLine = function (radius, color, opacity) {
    var T = this.T, pts = [];
    for (var i = 0; i < 256; i++) { var a = i / 256 * Math.PI * 2; pts.push(new T.Vector3(radius * Math.cos(a), 0, radius * Math.sin(a))); }
    var line = new T.LineLoop(new T.BufferGeometry().setFromPoints(pts), new T.LineBasicMaterial({ color: color, transparent: true, opacity: opacity, depthWrite: false, toneMapped: false }));
    this.scene.add(line);
    return line;
  };

  // ---------- destello de lente ----------
  Engine.prototype.makeFlare = function () {
    if (!this.sunObj) return;
    var T = this.T, self = this;
    var spec = [[0.32, 0.08, '#00c9ff', 0.12], [0.55, 0.035, '#00d4a1', 0.18], [0.78, 0.16, '#0076e3', 0.07], [1.2, 0.06, '#ffd9a8', 0.12], [1.45, 0.24, '#00c9ff', 0.05]];
    this.ghosts = spec.map(function (g) {
      var s = new T.Sprite(new T.SpriteMaterial({ map: self.ghostTex, color: new T.Color(g[2]), transparent: true, opacity: 0, depthTest: false, depthWrite: false, blending: T.AdditiveBlending }));
      s.userData = { f: g[0], s: g[1], o: g[3] };
      self.flareScene.add(s);
      return s;
    });
  };

  Engine.prototype.sunOccluded = function () {
    var c = this.cam.position, d = this._v1.copy(this.sunPos).sub(c), dist = d.length();
    d.divideScalar(dist);
    for (var i = 0; i < this.planets.length; i++) {
      var p = this.planets[i], oc = this._v2.copy(p.pos).sub(c), t = oc.dot(d);
      if (t <= 0 || t > dist) continue;
      if (oc.lengthSq() - t * t < p.radius * p.radius) return true;
    }
    return false;
  };

  Engine.prototype.updateFlare = function (dt) {
    if (!this.ghosts) return;
    var v = this._v3.copy(this.sunPos).project(this.cam);
    this.cam.getWorldDirection(this._fwd);
    var front = this._v4.copy(this.sunPos).sub(this.cam.position).dot(this._fwd) > 0;
    var len = Math.sqrt(v.x * v.x + v.y * v.y);
    var target = 0;
    if (front && Math.abs(v.x) < 1.15 && Math.abs(v.y) < 1.15 && !this.sunOccluded()) target = (1 - 0.4 * Math.min(len, 1.4)) * smooth(0.04, 0.3, len);
    this.flareK += (target - this.flareK) * Math.min(dt * 6, 1);
    var asp = this.aspect || 1.6;
    for (var i = 0; i < this.ghosts.length; i++) {
      var g = this.ghosts[i], u = g.userData;
      g.position.set(v.x * (1 - 2 * u.f), v.y * (1 - 2 * u.f), 0);
      g.scale.set(u.s / asp * 2, u.s * 2, 1);
      g.material.opacity = u.o * this.flareK;
    }
  };

  // ---------- cámara ----------
  Engine.prototype.showcase = function (ahead, endT, kEnd, dt) {
    var c = this.cam;
    var fwd = this._v1.copy(ahead).sub(c.position);
    if (fwd.lengthSq() < 1e-6) this.cam.getWorldDirection(fwd); else fwd.normalize();
    var acc = this._v2.copy(fwd).multiplyScalar(this.portrait ? 0.4 : 1);
    for (var i = 0; i < this.planets.length; i++) {
      var p = this.planets[i];
      if (p.noLook) continue;
      var toP = this._v3.copy(p.pos).sub(c.position), d = toP.length();
      toP.divideScalar(d);
      var w = smooth(p.radius * 7 + 160, p.radius * 2.5 + 30, d) * smooth(-0.1, 0.45, toP.dot(fwd)) * 0.85;
      acc.addScaledVector(toP, w);
    }
    acc.normalize();
    var endDir = this._v4.copy(endT).sub(c.position).normalize();
    acc.lerp(endDir, kEnd).normalize();
    if (!this.lookDir) this.lookDir = acc.clone();
    else this.lookDir.lerp(acc, Math.min(dt * 3.5, 1)).normalize();
    this._tgt.copy(c.position).add(this.lookDir);
    c.lookAt(this._tgt);
  };

  Engine.prototype.idleOffset = function (clock, center) {
    if (this.k <= 0) return;
    var c = this.cam, M = center || this.M;
    this._w.copy(c.position).sub(M).applyAxisAngle(this._Y, this.swayAmp * Math.sin(clock * 0.2) * this.k);
    c.position.copy(M).add(this._w);
    c.position.x += -this.sx * 0.9 * this.k;
    c.position.y += -this.sy * 0.5 * this.k;
  };

  Engine.prototype.followPath = function (u, clock, dt, kStart) {
    var c = this.cam;
    this.path.getPointAt(u, c.position);
    this.path.getPointAt(Math.min(u + 0.02, 1), this._ahead);
    this.idleOffset(clock);
    this.showcase(this._ahead, (this.portrait && this.endTM) ? this.endTM : this.endT, smooth(kStart, 1, u), dt);
  };

  // ---------- bucle ----------
  Engine.prototype.resize = function () {
    var c = this.canvas, r = this.r;
    if (!c || !r) return;
    var w = Math.max(c.clientWidth, 1), h = Math.max(c.clientHeight, 1);
    r.setSize(w, h, false);
    this.aspect = w / h;
    this.vw = w; this.vh = h;
    // en celular (vertical) se abre el lente para que quepan el planeta y el login
    this.portrait = this.aspect < 0.8;
    var fov = this.portrait ? 58 : 40;
    for (var i = 0; i < this.cams.length; i++) { this.cams[i].aspect = w / h; this.cams[i].fov = fov; this.cams[i].updateProjectionMatrix(); }
    var pr = r.getPixelRatio(), scale = (h * pr) / (2 * Math.tan(fov / 2 * Math.PI / 180));
    for (var j = 0; j < this.pmats.length; j++) { this.pmats[j].uniforms.uScale.value = scale; this.pmats[j].uniforms.uMax.value = this.pmats[j].userData.max * pr; }
    var ls = this.portrait ? 0.62 : 1;
    for (var k = 0; k < this.planets.length; k++) if (this.planets[k].label) this.planets[k].label.scale.set(0.34 * ls, 0.085 * ls, 1);
  };

  Engine.prototype.frame = function (now) {
    this.raf = requestAnimationFrame(this.frame);
    var r = this.r;
    if (!r) return;
    if (this.t0 === null) { this.t0 = now - (this.skipOnStart ? this.cfg.INTRO * 1000 : 0); this.tBase = now; this.last = now; }
    var dt = Math.min((now - this.last) / 1000, 0.05);
    this.last = now;
    if (!this.visible) return;
    var cfg = this.cfg;
    var t = Math.min((now - this.t0) / 1000, cfg.INTRO);
    var clock = (now - this.tBase) / 1000;
    var f = Math.min(dt * 2.5, 1);
    this.sx += (this.px - this.sx) * f;
    this.sy += (this.py - this.sy) * f;
    this.k = smooth(cfg.LAND - 1, cfg.INTRO, t);

    var prevPhase = this.phase, prevLanded = this.landed;
    cfg.update(this, t, clock, dt);
    this.landed = t >= cfg.LAND;
    if ((this.phase !== prevPhase || this.landed !== prevLanded) && this.opts.onState) this.opts.onState(this.phase, this.landed);

    // punto de giro: Mercurio cuando estás cerca; si no, un punto 40 unidades delante de la cámara
    var focusObj = this.M || this.sunPos;
    var dM = this.cam.position.distanceTo(focusObj);
    this.cam.getWorldDirection(this._fwd);
    this._F.copy(this.cam.position).addScaledVector(this._fwd, 40).lerp(focusObj, smooth(200, 80, dM));

    // zoom libre (rueda, pellizco o botones): acerca o aleja la cámara sobre su eje de visión
    this.zoom += (this.zoomT - this.zoom) * Math.min(dt * 5, 1);
    var zEff = this.zoom + (this.portrait && cfg.zoomPortrait ? cfg.zoomPortrait * this.k : 0);
    if (Math.abs(zEff) > 0.0005) {
      var ref = Math.min(dM, 40);
      this.cam.position.addScaledVector(this._fwd, -ref * (Math.exp(zEff) - 1));
    }

    // giro 360° alrededor del punto de giro, con inercia al soltar
    if (!this.drag && (Math.abs(this.vYaw) > 1e-5 || Math.abs(this.vPitch) > 1e-5)) {
      this.yawT += this.vYaw;
      this.pitchT = Math.min(Math.max(this.pitchT + this.vPitch, -1.45), 1.45);
      var decay = Math.pow(0.06, dt);
      this.vYaw *= decay; this.vPitch *= decay;
    }
    var fo = Math.min(dt * 8, 1);
    this.yaw += (this.yawT - this.yaw) * fo;
    this.pitch += (this.pitchT - this.pitch) * fo;
    if (Math.abs(this.yaw) > 1e-5 || Math.abs(this.pitch) > 1e-5) {
      this._right.set(1, 0, 0).applyQuaternion(this.cam.quaternion);
      this._q1.setFromAxisAngle(this._Y, this.yaw);
      this._q2.setFromAxisAngle(this._right, this.pitch);
      this._q1.multiply(this._q2);
      this._w.copy(this.cam.position).sub(this._F).applyQuaternion(this._q1);
      this.cam.position.copy(this._F).add(this._w);
      this.cam.quaternion.premultiply(this._q1);
    }
    if (this.fill) {
      this.cam.getWorldDirection(this._fwd);
      this.fill.position.copy(this.cam.position);
      this.fill.target.position.copy(this.cam.position).add(this._fwd);
    }

    // rotación de planetas y nubes
    for (var i = 0; i < this.planets.length; i++) {
      var p = this.planets[i];
      p.mesh.rotation.y = p.seed + clock * p.spin;
      if (p.clouds) p.clouds.rotation.y = p.seed + clock * p.spin * 1.18;
    }
    if (this.sunObj) {
      this.sunObj.mat.uniforms.uTime.value = clock;
      this.sunObj.corMat.uniforms.uTime.value = clock;
      this.sunObj.corona.quaternion.copy(this.cam.quaternion);
    }
    if (this.dust && this.k > 0) this.dust.position.set(Math.cos(clock * 0.05) * 6 * this.k, 0, Math.sin(clock * 0.07) * 9 * this.k);

    // etiquetas
    this.cam.updateMatrixWorld();
    this._right.setFromMatrixColumn(this.cam.matrixWorld, 0);
    this._up.setFromMatrixColumn(this.cam.matrixWorld, 1);
    this.cam.getWorldDirection(this._fwd);
    for (var j = 0; j < this.planets.length; j++) {
      var q = this.planets[j];
      if (!q.label) continue;
      var to = this._v1.copy(q.pos).sub(this.cam.position), d = to.length();
      var vis = smooth(q.radius * 10 + 220, q.radius * 4 + 50, d) * (d > q.radius * 1.6 ? 1 : 0);
      vis = Math.max(vis, this.labelAll, smooth(1.4, 2.6, this.zoom));
      if (q.kind === 'luna' && this.zoom > 1.2) vis = 0;
      if (this.portrait) vis *= 1 - this.k;
      if (to.dot(this._fwd) <= 0) vis = 0;
      q.labelOp += (vis - q.labelOp) * Math.min(dt * 4, 1);
      q.label.material.opacity = q.labelOp;
      q.label.visible = q.labelOp > 0.01;
      q.label.position.copy(q.pos).addScaledVector(this._right, q.radius * 0.72).addScaledVector(this._up, q.radius * 0.72);
    }
    var shift = this.portrait && cfg.lensPortrait ? cfg.lensPortrait * this.k : 0;
    if (shift > 0.001) {
      this.cam.setViewOffset(this.vw, this.vh, 0, shift * this.vh, this.vw, this.vh);
      this.skyCam.setViewOffset(this.vw, this.vh, 0, shift * this.vh, this.vw, this.vh);
      this._shifted = true;
    } else if (this._shifted) {
      this.cam.clearViewOffset(); this.skyCam.clearViewOffset(); this._shifted = false;
    }
    this.updateFlare(dt);

    r.clear();
    this.skyCam.quaternion.copy(this.qSky).multiply(this.cam.quaternion);
    r.render(this.sky, this.skyCam);
    r.clearDepth();
    r.render(this.scene, this.cam);
    if (this.ghosts && this.flareK > 0.002) { r.clearDepth(); r.render(this.flareScene, this.flareCam); }
  };

  // ------------------------------------------------------------------
  var SCENES = {};

  // F+ · Gran Tour: de Neptuno a Mercurio mirando cada planeta al pasar.
  SCENES.tour = {
    INTRO: 11, LAND: 10, fill: 0.85, exposure: 1.15, zoomMin: -0.8, zoomMax: 3.4, zoomPortrait: 0.55, lensPortrait: 0.28,
    build: function (E) {
      var T = E.T, V = function (x, y, z) { return new T.Vector3(x, y, z); };
      E.addSun(30, true);
      E.M = V(130, 0, 0);
      E.planet('mercurio', 6, E.M);
      E.planet('venus', 13, V(290, 12, -40));
      E.planet('tierra', 14, V(470, -6, 46));
      E.planet('luna', 3.8, V(505, 2, 64));
      E.planet('marte', 8, V(690, -10, -36));
      E.planet('jupiter', 55, V(1150, -70, 125));
      E.planet('saturno', 46, V(1550, 70, -175));
      E.planet('urano', 22, V(1950, -30, 70));
      E.planet('neptuno', 21, V(2300, 20, -60));
      E.path = new T.CatmullRomCurve3([
        V(2650, 60, 10), V(2300, 30, 12), V(1960, -8, 8), V(1570, 22, -24), V(1180, -4, -8),
        V(900, 8, 2), V(700, 4, 8), V(485, 8, -6), V(300, -2, 6), V(190, 6, -14), V(130, 3, -24)
      ], false, 'centripetal');
      E.addBelt({ rIn: 830, rOut: 970, ang: 0, spread: 0.22, count: 2400, thick: 14, clear: 9 });
      E.addDust(6000, 45);
      E.endT = V(123, 0.5, 0);
      E.endTM = V(130, 0, 0);
    },
    update: function (E, t, clock, dt) {
      E.followPath(out(t / this.INTRO, 1.5), clock, dt, 0.86);
      var x = E.cam.position.x;
      E.setPhase(x > 1350 ? 0 : x > 1000 ? 1 : x > 780 ? 2 : x > 200 ? 3 : 4);
    }
  };

  // F1 · Desde el cinturón hasta Mercurio frente al Sol.
  SCENES.cinturon = {
    INTRO: 9, LAND: 8.2,
    build: function (E) {
      var T = E.T, V = function (x, y, z) { return new T.Vector3(x, y, z); };
      E.addSun(30);
      E.M = V(130, 0, 0);
      E.planet('mercurio', 6, E.M);
      E.planet('venus', 13, V(290, 12, -40));
      E.planet('tierra', 14, V(470, -6, 46));
      E.planet('luna', 3.8, V(505, 2, 64));
      E.planet('marte', 8, V(690, -10, -36));
      E.planet('jupiter', 55, V(1150, -70, 125));
      var end = V(260, 6, 30);
      E.path = new T.CatmullRomCurve3([
        V(1240, 34, -70), V(1110, 14, -30), V(990, 6, -6), V(880, 4, 4), V(760, 0, 6),
        V(700, 6, 22), V(485, 8, -6), V(330, 2, 10), end
      ], false, 'centripetal');
      E.addBelt({ rIn: 850, rOut: 1010, ang: 0, spread: 0.3, count: 3600, thick: 16, clear: 4, maxS: 6.5 });
      E.addDust(6000, 40);
      var dS = E.sunPos.clone().sub(end).normalize();
      var right = dS.clone().cross(new T.Vector3(0, 1, 0)).normalize();
      var a = 14 * Math.PI / 180;
      E.endT = end.clone().add(dS.multiplyScalar(Math.cos(a)).addScaledVector(right, Math.sin(a)).multiplyScalar(100));
      E.swayAmp = 0.03;
      E.merc.noLook = false;
    },
    update: function (E, t, clock, dt) {
      E.followPath(out(t / this.INTRO, 1.6), clock, dt, 0.8);
      var x = E.cam.position.x;
      E.setPhase(x > 780 ? 0 : x > 300 ? 1 : 2);
    }
  };

  // F2 · Alineación planetaria vista de costado.
  SCENES.alineacion = {
    INTRO: 10, LAND: 9.2, zoomPortrait: 0.55, lensPortrait: 0.28,
    build: function (E) {
      var T = E.T, V = function (x, y, z) { return new T.Vector3(x, y, z); };
      E.addSun(30);
      E.M = V(130, 0, 0);
      E.planet('mercurio', 6, E.M);
      E.planet('venus', 13, V(290, 0, 0));
      E.planet('tierra', 14, V(470, 0, 0));
      E.planet('luna', 3.8, V(500, 6, -22));
      E.planet('marte', 8, V(700, 0, 0));
      E.planet('jupiter', 55, V(1150, 0, 0));
      E.planet('saturno', 46, V(1550, 0, 0));
      E.planet('urano', 22, V(1950, 0, 0));
      E.planet('neptuno', 21, V(2300, 0, 0));
      E.path = new T.CatmullRomCurve3([
        V(2560, 25, -240), V(1900, 18, -236), V(1250, 12, -232), V(820, 10, -210), V(560, 7, -160), V(300, 4, -80), V(130, 3, -24)
      ], false, 'centripetal');
      E.addBelt({ rIn: 790, rOut: 980, ang: -0.18, spread: 0.32, count: 2600, thick: 18, clear: 10, uniform: true });
      E.addDust(6000, 50);
      E.endT = V(123, 0.5, 0);
      E.endTM = V(130, 0, 0);
    },
    update: function (E, t, clock, dt) {
      E.followPath(out(t / this.INTRO, 1.3), clock, dt, 0.88);
      var x = E.cam.position.x;
      E.setPhase(x > 1750 ? 0 : x > 1000 ? 1 : x > 760 ? 2 : x > 200 ? 3 : 4);
    }
  };

  // F3 · Rozando los anillos de Saturno y rumbo a Mercurio.
  SCENES.saturno = {
    INTRO: 9.5, LAND: 8.8,
    build: function (E) {
      var T = E.T, V = function (x, y, z) { return new T.Vector3(x, y, z); };
      E.addSun(30);
      E.M = V(130, 0, 0);
      E.planet('mercurio', 6, E.M);
      E.planet('venus', 13, V(290, 12, -40));
      E.planet('tierra', 14, V(470, -6, 46));
      E.planet('luna', 3.8, V(505, 2, 64));
      E.planet('marte', 8, V(690, -10, -36));
      E.planet('jupiter', 55, V(1100, -40, 150));
      E.planet('saturno', 46, V(1550, 0, 0), { tilt: 0 });
      E.path = new T.CatmullRomCurve3([
        V(1620, 5, 70), V(1540, 6, 100), V(1440, 10, 100), V(1340, 18, 98), V(1150, 20, 40), V(900, 8, 0),
        V(700, 4, 8), V(485, 8, -6), V(300, -2, 6), V(190, 16, -14), V(134, 16, -18)
      ], false, 'centripetal');
      // partículas de hielo del anillo A/B cerca del inicio
      var N = 3200, inst = new T.InstancedMesh(E.rockGeometry(1), E.rockMaterial('#e6e0d4'), N);
      var d = new T.Object3D();
      for (var i = 0; i < N; i++) {
        var x, y, z, tries = 0;
        do {
          var a = (30 + 85 * Math.random()) * Math.PI / 180, rr = 72 + 32 * Math.random();
          x = 1550 + rr * Math.cos(a); z = rr * Math.sin(a); y = gauss() * 0.35;
          tries++;
        } while (E.nearPath(x, y, z, 1.2) && tries < 10);
        var s = 0.04 + 0.6 * Math.pow(Math.random(), 4);
        d.position.set(x, y, z);
        d.rotation.set(Math.random() * 6.28, Math.random() * 6.28, Math.random() * 6.28);
        d.scale.setScalar(s);
        d.updateMatrix();
        inst.setMatrixAt(i, d.matrix);
      }
      inst.instanceMatrix.needsUpdate = true;
      if (inst.computeBoundingSphere) inst.computeBoundingSphere(); else inst.frustumCulled = false;
      E.scene.add(inst);
      E.addBelt({ rIn: 830, rOut: 970, ang: 0, spread: 0.22, count: 1800, thick: 14, clear: 9 });
      E.addDust(6000, 45);
      E.endT = V(124, -1, 0);
    },
    update: function (E, t, clock, dt) {
      var x = t / this.INTRO, u;
      if (x < 0.28) u = 0.06 * (x / 0.28);
      else { var y = (x - 0.28) / 0.72; u = 0.06 + 0.94 * (0.164 * y + 2.672 * y * y - 1.836 * y * y * y); }
      E.followPath(c01(u), clock, dt, 0.86);
      var cx = E.cam.position.x;
      E.setPhase(u < 0.07 ? 0 : cx > 200 ? 1 : 2);
    }
  };

  // F4 · Órbita viva: vista general y luego persecución de Mercurio en su órbita.
  SCENES.orbita = {
    INTRO: 6.5, LAND: 5.8,
    build: function (E) {
      var T = E.T;
      E.addSun(30);
      E.orbs = [
        { p: E.planet('mercurio', 6, new T.Vector3()), r: 130, per: 48, a0: 0.4 },
        { p: E.planet('venus', 11, new T.Vector3()), r: 230, per: 122.7, a0: 2.2 },
        { p: E.planet('tierra', 12, new T.Vector3()), r: 330, per: 199, a0: 4.0 },
        { p: E.planet('marte', 7, new T.Vector3()), r: 470, per: 374.7, a0: 5.3 }
      ];
      E.luna = E.planet('luna', 3.2, new T.Vector3());
      E.orbitLine(130, '#00d4a1', 0.5);
      E.orbitLine(230, '#00c9ff', 0.2);
      E.orbitLine(330, '#00c9ff', 0.2);
      E.orbitLine(470, '#00c9ff', 0.2);
      E.addBelt({ rIn: 600, rOut: 700, ang: 0, spread: Math.PI, count: 1400, thick: 10, uniform: true, maxS: 3, dustCount: 5000 });
      E.M = E.orbs[0].p.pos;
      E.planets.forEach(function (p) { p.noLook = true; });
      E.over = new T.Vector3(-120, 760, 620);
      E._rh = new T.Vector3(); E._vh = new T.Vector3(); E._chase = new T.Vector3(); E._ct = new T.Vector3(); E._lt = new T.Vector3();
    },
    update: function (E, t, clock, dt) {
      for (var i = 0; i < E.orbs.length; i++) {
        var o = E.orbs[i], a = o.a0 + clock * Math.PI * 2 / o.per;
        o.p.pos.set(o.r * Math.cos(a), 0, o.r * Math.sin(a));
        if (i === 2) {
          var ml = clock * Math.PI * 2 / 14.9;
          E.luna.pos.set(o.p.pos.x + 26 * Math.cos(ml), 2, o.p.pos.z + 26 * Math.sin(ml));
        }
      }
      var m = E.orbs[0], am = m.a0 + clock * Math.PI * 2 / m.per;
      var rh = E._rh.set(Math.cos(am), 0, Math.sin(am)), vh = E._vh.set(-Math.sin(am), 0, Math.cos(am));
      var w = out(t / this.INTRO, 2.2);
      var chase = E._chase.copy(m.p.pos).addScaledVector(vh, -24).add(E._v4.set(0, 3, 0));
      chase.addScaledVector(rh, -E.sx * 1.2 * E.k).y += -E.sy * 0.8 * E.k;
      E.cam.position.copy(E.over).lerp(chase, w);
      var ct = E._ct.copy(m.p.pos).addScaledVector(rh, -7);
      E._lt.set(0, 0, 0).lerp(ct, smooth(0.15, 1, w));
      E.cam.lookAt(E._lt);
      E.labelAll = 1 - smooth(0.25, 0.6, w);
      E.setPhase(w < 0.6 ? 0 : 1);
    }
  };

  window.MercurioEspacio = {
    create: function (canvas, opts) { return new Engine(canvas, opts); }
  };
})();
