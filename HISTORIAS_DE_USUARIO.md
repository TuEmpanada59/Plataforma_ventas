# Historias de Usuario — Plataforma de Ventas (Londoño Gómez)

**Épica:** Plataforma de Lanzamientos Inmobiliarios (nombre del proyecto en Azure DevOps)
**Versión del documento:** 3.0 · **Fecha:** 09/07/2026
**Convención de estados:** ✅ Implementada · 🔨 Parcial · ⏳ Pendiente
**Convención de tasks:** `[x]` completada (crear como *Done*) · `[ ]` pendiente (crear como *To Do*)

Estructura para Azure DevOps: **Epic → Feature → User Story → Task**.
Cada Feature tiene **descripción**; cada Historia de Usuario tiene **al menos una Task**; y cada Task tiene su **título + descripción** (lista para pegar en el campo *Description* de la Task).

---

## FEATURE 1 — Autenticación y control de acceso

**Descripción:** Gestiona la identidad y el acceso a la plataforma. Cubre el inicio de sesión seguro con protección contra fuerza bruta, la recuperación de contraseña por correo, la autorización basada en roles (SuperAdministrador, Administrador, Vendedor) y el manejo endurecido de la sesión y sus cookies. Es la puerta de entrada del sistema y la base sobre la que se apoya toda la seguridad funcional.

### HU-101 · Inicio de sesión ✅
**Como** usuario registrado (administrador o vendedor), **quiero** iniciar sesión con mi usuario y contraseña, **para** acceder a las funciones que corresponden a mi rol.

**Criterios de aceptación:** credenciales válidas → panel del rol; inválidas → mensaje con intentos restantes sin revelar el campo; verificación BCrypt (12); descarte de sesión previa; auditoría con usuario e IP.

**Tasks:**
- [x] **Login con BCrypt y auditoría** — Autenticar contra el hash BCrypt (factor 12), registrar cada intento (éxito/fallo) con usuario e IP y redirigir según el rol.
- [ ] **CAPTCHA / rate-limit por IP** — Agregar un desafío o límite por IP tras N intentos fallidos para mitigar el DoS de cuenta que hoy permite el lockout por usuario.
- [ ] **Pruebas automatizadas del login** — Cubrir con tests (unitarios e integración) los casos: credenciales correctas, incorrectas, cuenta bloqueada y sesión previa descartada.

### HU-102 · Bloqueo por intentos fallidos (lockout) ✅
**Como** responsable de seguridad, **quiero** que una cuenta se bloquee temporalmente tras varios intentos fallidos, **para** impedir ataques de fuerza bruta.

**Criterios de aceptación:** 5 fallos → 15 min de bloqueo; durante el bloqueo se rechaza incluso la contraseña correcta; bloqueo/desbloqueo auditados; restablecer contraseña quita el bloqueo.

**Tasks:**
- [x] **Contador de intentos y bloqueo temporal** — Implementar el conteo por usuario en IMemoryCache y el bloqueo de 15 minutos con sus mensajes y auditoría.
- [ ] **Panel de bloqueos para SuperAdministrador** — Pantalla para ver las cuentas bloqueadas en el momento y liberarlas manualmente antes de que expire el tiempo.

### HU-103 · Recuperación de contraseña por correo ✅
**Como** usuario que olvidó su contraseña, **quiero** solicitar un enlace de restablecimiento a mi correo, **para** recuperar el acceso sin intervención del administrador.

**Criterios de aceptación:** respuesta anti-enumeración; token 256-bit hasheado (SHA-256), un solo uso, expira 15 min; rate-limit 5/15 min por IP; nueva contraseña ≥ 8 caracteres con confirmación.

**Tasks:**
- [x] **Flujo de token hasheado de un solo uso** — Generar token aleatorio, almacenar solo su hash, validar expiración/uso único y aplicar rate-limit y respuesta genérica.
- [ ] **Plantilla HTML corporativa del correo** — Diseñar el correo de recuperación con la imagen de marca, en HTML responsivo, en lugar del texto plano actual.
- [ ] **Política de complejidad configurable** — Exigir mayúscula/minúscula/número/símbolo con reglas parametrizables desde configuración.

### HU-104 · Autorización por roles ✅
**Como** propietario del sistema, **quiero** que cada pantalla y acción valide el rol del usuario en sesión, **para** que nadie acceda a funciones que no le corresponden.

**Criterios de aceptación:** roles SuperAdministrador/Administrador/Vendedor; sin sesión → login; rol ajeno → su propio panel; SuperAdmin accede a lo de Admin; antiforgery en todos los POST.

**Tasks:**
- [x] **Filtro de autorización por rol** — Implementar el atributo que valida el rol de sesión, redirige según corresponda y protege los controladores.
- [ ] **Matriz de permisos documentada** — Anexo técnico que liste, por controlador y acción, qué rol tiene acceso, como referencia de auditoría.

### HU-105 · Gestión de sesión y sincronización entre pestañas ✅
**Como** responsable de seguridad, **quiero** sesiones con expiración corta, cookies endurecidas y comportamiento consistente entre pestañas, **para** reducir la ventana de secuestro de sesión.

**Criterios de aceptación:** expiración 20 min deslizante; cookies HttpOnly/Secure/SameSite=Strict; banner de aviso a 2 min; pestaña nueva conserva la sesión; cerrar sesión en una pestaña cierra las demás; logout audita y borra cookies.

**Tasks:**
- [x] **Endurecer cookies y reducir la sesión** — Fijar Secure/HttpOnly/SameSite y bajar la expiración a 20 minutos alineada con el banner.
- [x] **Cierre de sesión sincronizado entre pestañas** — Emitir por localStorage un evento de logout para que todas las pestañas del navegador salgan a la vez.
- [ ] **Sincronizar la expiración por inactividad** — Que al expirar la sesión por inactividad en una pestaña, las demás también redirijan al login.
- [ ] **DataProtection persistente** — Almacenar las claves en Azure Blob + Key Vault para que antiforgery y sesión sobrevivan reinicios y escalado multi-instancia.

---

## FEATURE 2 — Gestión de usuarios

**Descripción:** Administración del equipo de trabajo de la plataforma. Permite a los administradores listar, crear, editar y restablecer contraseñas de usuarios, con reglas que impiden que un Administrador cree o modifique a otros administradores, y asignar a cada vendedor el proyecto en el que operará. Es el módulo que define quién existe en el sistema y con qué alcance.

### HU-201 · Listado de usuarios ✅
**Como** administrador, **quiero** ver todos los usuarios registrados con su rol y datos de contacto, **para** administrar el equipo comercial.

**Criterios de aceptación:** pantalla completa con todos los roles; botón "Nuevo usuario" que abre el formulario en un modal.

**Tasks:**
- [x] **Vista de usuarios a pantalla completa con modal** — Rediseñar el listado para ocupar toda la pantalla y abrir el registro de nuevo usuario en un formulario modal.
- [ ] **Búsqueda y filtros por rol/nombre** — Agregar un buscador y filtros para localizar usuarios rápidamente cuando crezca el equipo.
- [ ] **Paginación del listado** — Paginar con OFFSET/FETCH (25 por página) para mantener el rendimiento con muchos usuarios.

### HU-202 · Crear y editar usuarios ✅
**Como** administrador, **quiero** crear y editar usuarios (datos personales, credenciales y rol), **para** dar de alta asesores y otros usuarios autorizados.

**Criterios de aceptación:** contraseña BCrypt(12); un Administrador no puede crear/promover ni degradar a otro Administrador (solo el SuperAdministrador); reset de contraseña disponible.

**Tasks:**
- [x] **CRUD de usuarios con reglas de rol** — Crear/editar usuarios, aplicar la restricción de no gestionar administradores y permitir el reset de contraseña.
- [ ] **Unicidad de usuario/correo** — Validar que no se repitan el nombre de usuario ni el correo, con un mensaje de error claro.
- [ ] **Desactivación (soft-delete)** — Inhabilitar usuarios en vez de borrarlos físicamente, para conservar la trazabilidad de sus ventas.

### HU-203 · Asignación de proyecto a vendedores ✅
**Como** administrador, **quiero** asignar el proyecto en el que trabajará cada vendedor, **para** que solo vea y venda inventario de ese proyecto.

**Criterios de aceptación:** asignación desde administración (sin código de acceso); sin proyecto → mensaje y navegación bloqueada; al asignar recupera navegación sin re-login.

**Tasks:**
- [x] **Bloqueo de navegación sin proyecto** — Quitar el flujo de código, mostrar el aviso al vendedor sin proyecto y bloquear toda la app hasta que el admin le asigne uno.
- [ ] **Historial de asignaciones** — Registrar qué administrador asignó qué proyecto a cada vendedor y cuándo, para auditoría.

---

## FEATURE 3 — Carga de proyectos e inventario (Excel)

**Descripción:** Punto de entrada del inventario al sistema. Permite a los administradores crear un proyecto e importar masivamente sus inmuebles desde un archivo Excel (con validación de tipo y tamaño), soportando distintos tipos de producto (apartamentos, lotes, suites, salud, oficinas) y hasta cinco listas de precio por unidad. También administra los proyectos ya cargados y su retiro sin afectar el histórico de ventas.

### HU-301 · Carga masiva desde Excel ✅
**Como** administrador, **quiero** cargar un proyecto y su inventario desde un archivo Excel, **para** montar un lanzamiento en minutos sin digitación manual.

**Criterios de aceptación:** solo .xlsx ≤ 10 MB; tipos con su columna de unidad; lectura de torre/piso/tipo/metros y 5 listas; archivo vacío → rechazo sin crear nada.

**Tasks:**
- [x] **Importación desde Excel con validación de tipo/tamaño** — Leer el .xlsx con EPPlus, validar extensión y tamaño, detectar columnas por tipo de proyecto e insertar el inventario.
- [ ] **Validación estricta del archivo** — Exigir columna PROYECTO (coincidente con el nombre) y METROS, y al menos una fila válida (unidad + metros + precio > 0), envolviendo la carga en transacción para evitar "proyectos fantasma".
- [ ] **Vista previa antes de confirmar** — Mostrar una previsualización de las filas detectadas para que el admin confirme antes de insertar.
- [ ] **Reporte de filas rechazadas** — Al terminar la carga, listar las filas descartadas y el motivo de cada una.

### HU-302 · Gestión de proyectos cargados ✅
**Como** administrador, **quiero** ver los proyectos cargados con sus totales por estado y poder retirarlos, **para** mantener el catálogo de lanzamientos al día.

**Criterios de aceptación:** totales por estado por proyecto; retiro que desactiva y desvincula sin borrar el histórico de ventas.

**Tasks:**
- [x] **Listado de proyectos con totales y retiro lógico** — Mostrar cada proyecto con sus conteos por estado y permitir desactivarlo desvinculando clientes/vendedores.
- [ ] **Confirmación en dos pasos para retiro con ventas** — Pedir confirmación explícita cuando el proyecto que se retira tiene ventas activas.

---

## FEATURE 4 — Inventario y estados de inmuebles

**Descripción:** Corazón del control de inventario en tiempo real. Muestra la grilla de inmuebles del proyecto agrupada por área y tipología, y garantiza que los cambios de estado (disponible → en proceso → reservado → vendido) sean atómicos para evitar que dos personas tomen la misma unidad. Incluye el motor de listas de precio con escalamiento automático (global y por área) difundido por SignalR.

### HU-401 · Grilla de inmuebles por proyecto ✅
**Como** administrador, **quiero** ver la grilla de inmuebles agrupada por área y tipología, **para** conocer el estado del inventario de un vistazo.

**Criterios de aceptación:** estados con color; filtro por área y cambio de proyecto activo; lista activa (global y por área) con su precio.

**Tasks:**
- [x] **Grilla con agrupación, filtros y tiempo real** — Implementar la grilla por área/tipología con colores de estado, filtros y actualización en vivo por SignalR.
- [ ] **Exportar la grilla actual** — Permitir descargar la vista de inmuebles como imagen o Excel para compartirla.

### HU-402 · Cambios de estado sin condiciones de carrera ✅
**Como** propietario del negocio, **quiero** que tomar/reservar/vender un inmueble sea atómico, **para** que dos personas no puedan quedarse con la misma unidad.

**Criterios de aceptación:** `UPDATE … WHERE Estado = previo` con verificación de filas; si otro ganó → aviso sin cambios; broadcast SignalR tras cada cambio.

**Tasks:**
- [x] **Cambios de estado atómicos + broadcast** — Implementar todos los cambios con UPDATE condicional al estado previo, verificación de filas afectadas y difusión por SignalR, incluido el cierre de venta transaccional.
- [ ] **Prueba de concurrencia automatizada** — Test que simule dos ventas simultáneas del mismo inmueble y verifique que solo una prospera.

### HU-403 · Listas de precio con escalamiento automático ✅
**Como** administrador, **quiero** que la lista de precios suba automáticamente cada N unidades vendidas, **para** ejecutar la estrategia de precios sin intervención manual.

**Criterios de aceptación:** umbral configurable global y por área; sube (máx. Lista 5) solo si la lista destino tiene precios; broadcast en vivo; cambio manual disponible.

**Tasks:**
- [x] **Escalamiento automático global y por área** — Calcular y aplicar el ascenso de lista al cumplir el umbral, validando que la lista destino tenga precios, y difundirlo por SignalR.
- [ ] **Historial de cambios de lista** — Guardar cuándo cambió cada lista y qué lo disparó (venta o cambio manual), para trazabilidad de precios.

---

## FEATURE 5 — Flujo de venta del vendedor (tiempo real)

**Descripción:** Experiencia principal del asesor durante el lanzamiento. Le muestra sus indicadores, el inventario disponible de su proyecto y le permite tomar una unidad, reservarla congelando el precio de la lista vigente, y consultar sus ventas y reservas — todo actualizándose en vivo a medida que otros asesores actúan. Está diseñado para operar bajo alta concurrencia el día del evento.

### HU-501 · Panel de inicio del vendedor ✅
**Como** vendedor, **quiero** ver al entrar mis indicadores, **para** ubicarme rápido durante el lanzamiento.

**Criterios de aceptación:** KPIs de mi proyecto y mi gestión; vista responsiva que no se descuadra según resolución/zoom.

**Tasks:**
- [x] **Panel de KPIs del vendedor responsivo** — Implementar el inicio del vendedor con sus indicadores, íconos y una maquetación que no desborde en distintas resoluciones/zoom.
- [ ] **KPI de reservas activas** — Agregar al panel un indicador con el número de reservas vigentes del vendedor.

### HU-502 · Ver inmuebles disponibles y tomar unidad ✅
**Como** vendedor, **quiero** ver los inmuebles y tomar una unidad disponible, **para** iniciar una venta con un cliente en sala.

**Criterios de aceptación:** solo mi proyecto; reservas ajenas bloqueadas con nombre; "tomar" → EN PROCESO atómico; cancelar → DISPONIBLE; cambios de otros en vivo.

**Tasks:**
- [x] **Grilla del vendedor con "tomar" atómico y tiempo real** — Mostrar el inventario del proyecto, bloquear reservas ajenas, y transicionar a EN PROCESO de forma atómica con actualización en vivo.
- [ ] **Liberación automática de "EN PROCESO"** — Devolver a DISPONIBLE una unidad que lleve demasiado tiempo tomada sin concretar, con el tiempo configurable.

### HU-503 · Reservar con precio bloqueado ✅
**Como** vendedor, **quiero** reservar congelando el precio de la lista vigente, **para** garantizarle al cliente el valor pactado aunque la lista suba.

**Criterios de aceptación:** guarda el precio de la lista activa del área; al vender desde la reserva usa siempre ese precio; solo el dueño (o admin) libera/vende.

**Tasks:**
- [x] **Reserva con precio bloqueado y verificación de propietario** — Congelar el precio de la lista activa al reservar y validar la propiedad de la reserva al liberar o vender.
- [ ] **Vencimiento automático de reservas** — Liberar reservas que superen un tiempo configurado (horas/días) para no bloquear inventario indefinidamente.

### HU-504 · Mis ventas y mis reservas ✅
**Como** vendedor, **quiero** consultar mis ventas y mis reservas activas, **para** hacer seguimiento a mi gestión.

**Criterios de aceptación:** historial de ventas con precio/destino/fecha; reservas vigentes con opción de continuar la venta o liberarlas.

**Tasks:**
- [x] **Vistas "Mis ventas" y "Mis reservas"** — Implementar las dos pantallas del vendedor con su historial de ventas y sus reservas accionables.
- [ ] **Exportar "mis ventas" a Excel** — Permitir al vendedor descargar su historial de ventas en un archivo Excel.

---

## FEATURE 6 — Registro de ventas y cumplimiento

**Descripción:** Cierre formal de la operación comercial con controles de negocio y normativos. Antes de registrar el cliente exige la verificación SAGRILAFT (prevención de lavado de activos), permite seleccionar cliente existente o crear uno nuevo con documento validado, y asegura la integridad financiera derivando el precio y la lista en el servidor dentro de una transacción atómica. Aplica tanto al vendedor como al administrador.

### HU-601 · Verificación SAGRILAFT previa al registro de cliente ✅
**Como** oficial de cumplimiento, **quiero** confirmar si el cliente ya fue consultado en SAGRILAFT antes de habilitar el panel de cliente, **para** cumplir la normativa antilavado.

**Criterios de aceptación:** pregunta en los 4 flujos; "Sí" habilita, "No" bloquea con el mensaje indicado; el servidor rechaza la venta si no llega la confirmación.

**Tasks:**
- [x] **Gate SAGRILAFT en los 4 flujos** — Agregar la pregunta y el bloqueo del panel de cliente (cliente y servidor) en venta directa y desde reserva, para vendedor y administrador.
- [ ] **Auditar la confirmación en la venta** — Guardar en la venta que se confirmó SAGRILAFT, con el usuario y la fecha, como evidencia de cumplimiento.
- [ ] **Integración con listas restrictivas** — Consultar en línea al proveedor de listas (SAGRILAFT) en lugar de una confirmación manual.

### HU-602 · Registrar venta con cliente nuevo o existente ✅
**Como** vendedor o administrador, **quiero** registrar la venta con cliente existente o nuevo, **para** cerrar la operación con los datos completos del comprador.

**Criterios de aceptación:** sin cliente válido no continúa (mensaje en la misma pantalla); documento solo números; destino de lista blanca; precio/lista calculados en servidor; cierre en transacción atómica que verifica estado y propietario.

**Tasks:**
- [x] **Cierre de venta seguro (precio en servidor + transacción)** — Derivar precio y lista en el servidor, validar destino y cliente, y ejecutar el INSERT de venta + cambio a VENDIDO en una transacción con guardia de estado/propietario.
- [ ] **Detección de cliente duplicado** — Al crear un cliente nuevo, avisar si ya existe uno con el mismo documento.
- [ ] **Anulación de ventas con auditoría** — Flujo de administrador para anular una venta registrando motivo, usuario y fecha, devolviendo el inmueble a disponible.

---

## FEATURE 7 — Gestión de clientes

**Descripción:** Administración de la información de los compradores. Permite listar clientes con paginación y búsqueda, y ver el detalle de cada uno con su historial de compras y edición de datos. Incluye consideraciones de privacidad y protección de datos personales (Ley 1581) sobre qué clientes puede ver cada rol.

### HU-701 · Listado y detalle de clientes ✅
**Como** administrador, **quiero** listar los clientes con paginación y ver su detalle con historial, **para** conocer y atender a los compradores.

**Criterios de aceptación:** listado paginado (25/página) con búsqueda; detalle editable con historial de compras.

**Tasks:**
- [x] **Listado paginado y detalle con historial** — Implementar el listado de clientes con paginación y búsqueda, y la ficha de detalle con edición e historial de compras.
- [ ] **Privacidad de clientes por vendedor** — Limitar el listado que ve un vendedor a los clientes de su proyecto o a los que él creó, conforme a la Ley 1581 (habeas data).
- [ ] **Exportar clientes a Excel** — Permitir la descarga del listado de clientes en Excel para gestión comercial.

---

## FEATURE 8 — Dashboard y monitoreo en tiempo real

**Descripción:** Centro de mando del administrador durante el lanzamiento. Reúne los KPIs del proyecto y el mapa de inmuebles que se actualiza en vivo a medida que el equipo vende, reserva o toma unidades, permitiendo dirigir el evento minuto a minuto y cambiar el proyecto activo desde la barra superior.

### HU-801 · Dashboard con KPIs y mapa en vivo ✅
**Como** administrador, **quiero** un panel con KPIs y el mapa actualizándose en tiempo real, **para** dirigir el lanzamiento minuto a minuto.

**Criterios de aceptación:** KPIs de inventario/ventas/valor/hoy; actualización sin recargar (SignalR); cambio de proyecto activo desde la barra superior.

**Tasks:**
- [x] **Dashboard con KPIs y mapa en tiempo real** — Implementar el panel de indicadores y el mapa de inmuebles con actualización en vivo por SignalR y selector de proyecto.
- [ ] **Feed de actividad en vivo** — Agregar un panel con las últimas acciones (última venta, quién y hace cuánto) para seguir el pulso del evento.

---

## FEATURE 9 — Reportes y exportaciones

**Descripción:** Generación de la información oficial del lanzamiento para la operación y la gerencia. Incluye el informe del día en pantalla, exportaciones a Excel, el reporte por asesor y el PDF técnico ejecutivo de una sola hoja (rediseñado según el handoff de diseño) que consolida estado del proyecto, ventas y asistencia.

### HU-901 · Informe del día en pantalla ✅
**Como** administrador, **quiero** un informe del día con KPIs, progreso, destinos, tipologías, mapa y detalle, **para** revisar el desempeño en cualquier momento.

**Criterios de aceptación:** KPIs, progreso por estado y detalle del proyecto activo; datos vivos.

**Tasks:**
- [x] **Informe del día en pantalla** — Construir la vista de reportes con KPIs, progreso, destinos, tipologías, mapa y detalle de ventas del día.
- [ ] **Filtro por rango de fechas** — Permitir consultar el informe por un periodo y no solo por el día actual.

### HU-902 · Exportación a Excel ✅
**Como** administrador, **quiero** exportar el informe y el detalle de ventas a Excel, **para** compartirlo con gerencia y archivarlo.

**Criterios de aceptación:** se exporta detalle e indicadores a .xlsx.

**Tasks:**
- [x] **Exportación a Excel (EPPlus)** — Generar el archivo Excel con el detalle de ventas y los indicadores del proyecto.
- [ ] **Plantilla Excel corporativa** — Unificar el formato de las exportaciones con logo, encabezados y estilos de marca.

### HU-903 · PDF técnico rediseñado 🔨
**Como** gerencia, **quiero** un PDF técnico de una sola hoja con módulos (KPIs, donut, tipologías, ventas, asistencia y colapsados), **para** recibir un informe ejecutivo estandarizado.

**Criterios de aceptación:** carta con header, franja KPI de 6 columnas, estado (donut+leyenda+barras), ventas con subtotales y destinos, asistencia y módulos colapsados, y footer; datos vivos es-CO; módulos sin datos colapsados.

**Tasks:**
- [x] **Rediseño del PDF técnico con QuestPDF** — Reescribir la composición del informe según el handoff (header, franja KPI con íconos, donut, barras de tipología, tabla de ventas, asistencia y módulos colapsados).
- [ ] **Validar generación y afinar visual** — Probar la generación tras el fix de layout con datos reales y ajustar detalles (donut, barras, tablas) contra el diseño.
- [ ] **Quitar el flag de depuración** — Retirar `QuestPDF.Settings.EnableDebugging` una vez estabilizado el informe.
- [ ] **Embeber la tipografía Barlow** — Incluir la fuente Barlow del diseño en lugar del fallback Arial actual.

### HU-904 · Reporte de asesores ✅
**Como** administrador, **quiero** un reporte por asesor, **para** medir el desempeño individual del equipo.

**Criterios de aceptación:** por asesor sus ventas, unidades y valor total.

**Tasks:**
- [x] **Reporte de asesores** — Implementar el reporte que agrupa por asesor sus ventas, unidades y valores.
- [ ] **Ranking histórico multi-proyecto** — Extender el reporte para comparar el desempeño de los asesores a través de varios proyectos/lanzamientos.

---

## FEATURE 10 — Cuadro de asistencia del lanzamiento

**Descripción:** Registro y medición de la convocatoria del evento. Permite capturar por día del lanzamiento los datos de asistencia (familias, adultos, niños, mascotas, asistentes con cita, vehículos, agendados) con cálculos de porcentajes en vivo, y exportarlos a Excel e integrarlos al PDF técnico como parte del informe oficial.

### HU-1001 · Captura del cuadro de asistencia ✅
**Como** administrador, **quiero** registrar por día los datos de asistencia, **para** medir la convocatoria del evento.

**Criterios de aceptación:** agregar/quitar días, observaciones y guardar; vista previa con porcentajes en vivo.

**Tasks:**
- [x] **Captura del cuadro con cálculo en vivo** — Implementar el formulario por días (familias, adultos, niños, vehículos, citas, etc.) con vista previa que calcula porcentajes en tiempo real y persistencia.
- [ ] **Captura optimizada para móvil** — Adaptar la captura para registrar la asistencia desde el celular en la puerta del evento.

### HU-1002 · Exportación del cuadro (Excel y PDF) ✅
**Como** administrador, **quiero** exportar el cuadro a Excel y verlo en el PDF técnico, **para** anexarlo al informe oficial.

**Criterios de aceptación:** exportación a Excel con totales/porcentajes; resumen de asistencia en el PDF técnico.

**Tasks:**
- [x] **Exportación del cuadro y módulo en el PDF** — Generar el Excel del cuadro de asistencia e incluir su resumen como módulo del informe técnico.
- [ ] **Consolidado multi-lanzamiento** — Reporte comparativo de asistencia entre distintos lanzamientos/proyectos.

---

## FEATURE 11 — Seguridad técnica y cumplimiento (transversal)

**Descripción:** Conjunto de controles técnicos que protegen la aplicación y los datos de forma transversal: cabeceras de seguridad y HTTPS, manejo de secretos fuera del repositorio, consultas parametrizadas, saneamiento de entradas y auditoría continua de calidad. Sostiene el cumplimiento normativo y la postura de seguridad de cara a producción.

### HU-1101 · Endurecimiento de aplicación ✅
**Como** responsable de seguridad, **quiero** cabeceras y configuración endurecidas, **para** reducir la superficie de ataque en producción.

**Criterios de aceptación:** CSP, X-Frame-Options DENY, nosniff, Referrer-Policy, Permissions-Policy, HSTS, HTTPS-only, TLS ≥ 1.2.

**Tasks:**
- [x] **Cabeceras de seguridad, HSTS y HTTPS-only** — Configurar la CSP y las demás cabeceras, forzar HTTPS y el TLS mínimo en el pipeline de middleware.
- [ ] **Eliminar `'unsafe-inline'` de la CSP** — Migrar el JS inline a archivos con nonce/hash para poder endurecer `script-src`.
- [ ] **Fijar `AllowedHosts`** — Restringir los hosts permitidos al dominio real de producción (hoy en `*`).

### HU-1102 · Protección de datos y secretos 🔨
**Como** responsable de seguridad, **quiero** que ningún secreto viva en el repositorio y que los datos sensibles estén protegidos, **para** cumplir buenas prácticas y la normativa de datos personales.

**Criterios de aceptación:** cadena de conexión por variable de entorno/Key Vault; `appsettings.*.json` sensibles en `.gitignore`; SQL parametrizado; documento saneado.

**Tasks:**
- [x] **Sacar secretos, sanear entradas y parametrizar SQL** — Quitar la cadena con credenciales de `appsettings.json`, saneаr el documento del cliente a solo dígitos y confirmar que todo el SQL sea parametrizado.
- [ ] **Rotar la credencial expuesta y purgar historial** — Cambiar la contraseña SQL que quedó en el historial de git y limpiar el historial (git filter-repo/BFG).
- [ ] **DataProtection con Key Vault** — Persistir y proteger las claves de DataProtection con Azure Blob + Key Vault.
- [ ] **Escaneo de dependencias en CI** — Ejecutar `dotnet list package --vulnerable` en el pipeline para detectar librerías vulnerables.

### HU-1103 · Auditoría de QA continua ⏳
**Como** líder técnico, **quiero** pruebas automatizadas (xUnit) ejecutadas en CI, **para** validar cada cambio antes de QA/Producción.

**Criterios de aceptación:** proyecto de pruebas que corre en el pipeline; `dotnet test` como gate obligatorio.

**Tasks:**
- [x] **Auditoría inicial de QA/seguridad** — Realizar la revisión estática y de seguridad del proyecto y documentar los hallazgos y remediaciones (INFORME_QA_SEGURIDAD.md).
- [ ] **Crear el proyecto de pruebas** — Crear `Plataforma_ventas.Tests` (xUnit) con los casos ya diseñados (SoloDigitos, mapeo de listas, política de contraseña).
- [ ] **Pruebas de integración del flujo de venta** — Probar tomar→vender y reservar→vender contra una BD en contenedor.
- [ ] **`dotnet test` como gate del pipeline** — Hacer obligatorias las pruebas antes de cualquier despliegue a QA/Producción.

---

## FEATURE 12 — Infraestructura, despliegue y DevOps

**Descripción:** Todo lo relacionado con llevar y mantener la aplicación en la nube. Define los dos entornos idénticos y aislados (QA y Producción) en Azure, el pipeline de CI/CD por ramas con aprobación manual a Producción desde Azure DevOps, y el monitoreo y respaldo necesarios para operar con confianza durante los lanzamientos.

### HU-1201 · Entornos QA y Producción idénticos ⏳
**Como** equipo de desarrollo, **quiero** dos entornos aislados con la misma configuración, **para** probar cada versión en QA antes de liberarla.

**Criterios de aceptación:** recursos separados por entorno; configuración sensible en el entorno (variables/Key Vault); QA no comparte BD con Producción.

**Tasks:**
- [x] **Definir arquitectura y costeo** — Elegir App Service B2 (Linux) + Azure SQL Serverless por entorno y documentar el costo aproximado para aprobación de gerencia.
- [ ] **Crear los recursos en Azure** — Crear los Resource Groups, App Service y Azure SQL de QA y de Producción con las configuraciones de seguridad definidas.
- [ ] **Dominio y subdominio con certificado** — Configurar el dominio propio y el subdominio de QA con el certificado administrado por Azure.

### HU-1202 · CI/CD por ramas con aprobación a Producción 🔨
**Como** equipo de desarrollo, **quiero** que `qa` despliegue a QA y `master` a Producción con aprobación manual, **para** liberar con control y trazabilidad.

**Criterios de aceptación:** pipeline YAML único; Producción exige aprobación en el Environment; los PR no despliegan.

**Tasks:**
- [x] **Pipeline con despliegue por rama y aprobación** — Crear `azure-pipelines.yml` que compila una vez y despliega a QA o a Producción según la rama, con aprobación en el entorno de Producción.
- [ ] **Conectar repo y crear Environments** — Vincular el repositorio a Azure Pipelines y crear los Environments QA y Production con sus aprobadores.
- [ ] **Service Connections por entorno** — Crear las conexiones de servicio a Azure, una por entorno, con permisos mínimos sobre su Resource Group.
- [ ] **Completar nombres reales en el YAML** — Reemplazar los placeholders del pipeline por los nombres reales de los App Service y las service connections.

### HU-1203 · Monitoreo y respaldo en producción ⏳
**Como** responsable de operación, **quiero** monitoreo y respaldos verificados, **para** detectar incidentes durante los lanzamientos y poder recuperarnos.

**Criterios de aceptación:** alertas ante 5xx/latencia/caídas; procedimiento de restauración documentado y probado.

**Tasks:**
- [ ] **Application Insights + alertas** — Habilitar el monitoreo de errores y desempeño con alertas por correo ante fallos o degradación.
- [ ] **Verificar respaldos (PITR)** — Confirmar la retención de respaldos de Azure SQL y documentar el procedimiento de restauración punto en el tiempo.
- [ ] **Prueba de restauración** — Ejecutar una restauración de respaldo de prueba antes del primer lanzamiento en producción.

---

## Resumen para carga en Azure DevOps

| Feature | Historias | Estado |
|---|---|---|
| 1. Autenticación y control de acceso | HU-101…HU-105 | ✅ |
| 2. Gestión de usuarios | HU-201…HU-203 | ✅ |
| 3. Carga de proyectos (Excel) | HU-301…HU-302 | ✅ (validación estricta pendiente) |
| 4. Inventario y estados de inmuebles | HU-401…HU-403 | ✅ |
| 5. Flujo de venta del vendedor | HU-501…HU-504 | ✅ |
| 6. Registro de ventas y cumplimiento | HU-601…HU-602 | ✅ |
| 7. Gestión de clientes | HU-701 | ✅ (privacidad pendiente) |
| 8. Dashboard en tiempo real | HU-801 | ✅ |
| 9. Reportes y exportaciones | HU-901…HU-904 | 🔨 (PDF técnico en ajuste) |
| 10. Cuadro de asistencia | HU-1001…HU-1002 | ✅ |
| 11. Seguridad técnica | HU-1101…HU-1103 | 🔨 |
| 12. Infraestructura y DevOps | HU-1201…HU-1203 | ⏳ |

**Total: 12 Features (con descripción) · 33 Historias de Usuario (con ≥1 Task) · 90+ Tasks (cada una con título + descripción).**

> **Carga sugerida:** Feature → pegar su *Descripción*; User Story → narrativa "Como… quiero… para…" en *Description* y los criterios en *Acceptance Criteria*; Task → el **título** como nombre del work item y la **descripción** (el texto después del guion) en su campo *Description*. Las tasks `[x]` se crean en *Done*; las `[ ]` en *To Do*.
