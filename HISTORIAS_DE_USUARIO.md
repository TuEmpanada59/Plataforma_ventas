# Historias de Usuario — Plataforma de Ventas (Londoño Gómez)

**Épica:** Plataforma de Lanzamientos Inmobiliarios (nombre del proyecto en Azure DevOps)
**Versión del documento:** 5.0 · **Fecha:** 13/08/2026
**Convención de estados:** ✅ Implementada · 🔨 Parcial · ⏳ Pendiente
**Convención de tasks:** `[x]` completada (crear como *Done*) · `[ ]` pendiente (crear como *To Do*)

Estructura para Azure DevOps: **Epic → Feature → User Story → Task**.
Cada Feature tiene **descripción**; cada Historia de Usuario tiene **al menos una Task**; y cada Task tiene su **título + descripción** (lista para pegar en el campo *Description* de la Task).

**Novedades v5.0:** entran **3 historias nuevas** (HU-802 panel de inicio accionable, HU-907 horas pico y área, HU-908 navegación por pestañas de Reportes) y se cierran tasks pendientes de v4.0 (PDF técnico, feed de actividad en vivo, unificación de la fórmula de escalamiento). Además se documentan **dos defectos encontrados y corregidos durante el desarrollo**: el escalamiento de listas no se aplicaba en el flujo del vendedor (HU-403) y el nombre de quien tenía el inmueble no se mostraba para administradores ni sobrevivía a la actualización en vivo (HU-405).

**Nota sobre la v4.0:** se auditó el código controlador por controlador contra el documento y aparecieron 4 historias ya construidas pero no documentadas (HU-404, HU-905, HU-906, HU-1104).

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

**Criterios de aceptación:** cada área se configura de forma independiente — `AptsPorLista = 0` la deja **fija** en su lista pase lo que pase, `> 0` la hace **automática** y sube cada N ventas de esa área; sube (máx. Lista 5) solo si la lista destino tiene precios; broadcast en vivo; cambio manual disponible; el comportamiento es idéntico venda un vendedor o un administrador.

**Tasks:**
- [x] **Escalamiento automático por área** — Calcular y aplicar el ascenso de lista al cumplir el umbral del área, validando que la lista destino tenga precios, y difundirlo por SignalR.
- [x] **Extraer el mapeo lista→columna a un helper testeable** — Unificar el `switch` duplicado en 4 lugares de los controladores en `Listas.ColumnaLista(int)` y cubrirlo con pruebas unitarias.
- [x] **Corregir el escalamiento en el flujo del vendedor** — El camino de venta del vendedor solo tenía el escalamiento global, por lo que las áreas configuradas como automáticas nunca subían de lista cuando vendía un asesor (únicamente si la venta la registraba un administrador). Portar el escalamiento por área a `VendedorController` con la misma validación de precios.
- [x] **Eliminar el escalamiento global del proyecto** — Como la carga crea una fila por área en `ProyectoAreaListas`, el `ISNULL(pal.ListaActual, p.ListaActual)` siempre resolvía por área: el escalamiento global no cambiaba ningún precio, pero cortaba el flujo con un `return` anticipado que impedía el escalamiento del área y mostraba un mensaje de éxito engañoso. Retirarlo y dejar un solo mecanismo.
- [ ] **Historial de cambios de lista** — Guardar cuándo cambió cada lista y qué lo disparó (venta o cambio manual), para trazabilidad de precios.
- [ ] **Pruebas unitarias del escalamiento** — Cubrir los casos borde ahora que la fórmula es única: umbral exacto, tope en Lista 5, área fija (`AptsPorLista = 0`) y lista destino sin precios.

### HU-404 · Vista de reservas activas (administración) ✅
**Como** administrador, **quiero** ver todas las reservas activas del proyecto (no solo las mías), **para** supervisar el trabajo de todo el equipo de vendedores y poder liberar o continuar cualquier reserva si es necesario.

**Criterios de aceptación:**
1. Listado de todas las unidades en estado RESERVADO del proyecto activo, con el vendedor que la reservó, el precio bloqueado y la fecha de reserva.
2. El administrador puede liberar cualquier reserva o continuar la venta desde ella, sin estar limitado al vendedor que la creó.
3. La lista se actualiza en vivo cuando cambia el estado de una reserva (SignalR).

**Tasks:**
- [x] **Vista de reservas del administrador** — Implementar la acción `Reservas` con el listado de todas las reservas activas del proyecto, vendedor, precio bloqueado y acciones de liberar/continuar.
- [ ] **Filtro por vendedor o por antigüedad de la reserva** — Permitir filtrar el listado para encontrar rápido reservas próximas a vencer o de un vendedor específico.


### HU-405 · Visibilidad de quién tiene el inmueble ✅
**Como** administrador, **quiero** ver el nombre de la persona que tiene un inmueble EN PROCESO o RESERVADO, **para** saber a quién dirigirme cuando haya que liberar, apurar o consultar por una unidad durante el lanzamiento.

**Criterios de aceptación:**
1. En la grilla de inmuebles, bajo el estado EN PROCESO o RESERVADO aparece el nombre completo de quien lo tiene.
2. Funciona igual si quien lo tomó fue un vendedor o un administrador.
3. El nombre se mantiene cuando la fila se actualiza en vivo por SignalR, sin necesidad de recargar.
4. El mapa de inmuebles del informe del día también indica quién tiene cada unidad no disponible.

**Tasks:**
- [x] **Mostrar el asesor en la grilla y el mapa** — Renderizar el nombre bajo el estado en la grilla de inmuebles (proceso y reserva) y agregarlo al mapa de Reportes con LEFT JOIN a Usuarios por `IdVendedorEnProceso` / `IdVendedorReserva`.
- [x] **Corregir el diccionario de usuarios** — El diccionario de nombres se cargaba con `WHERE Rol='Vendedor'`, por lo que un inmueble tomado por un administrador mostraba la palabra genérica "Vendedor" en lugar de su nombre. Incluir a todos los usuarios.
- [x] **Propagar el actor en el evento en vivo** — `InmuebleActualizado` no llevaba quién causaba el cambio, así que la actualización por SignalR borraba el nombre hasta recargar. Agregar el parámetro `quien` al contrato del hub y actualizar las 12 llamadas y los 3 manejadores de JavaScript.
- [ ] **Antigüedad del proceso** — Mostrar hace cuánto está tomada la unidad y señalar los procesos estancados, aprovechando `FechaEnProceso`.

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

**Criterios de aceptación:** pregunta en los 4 flujos; el panel de cliente permanece **oculto** hasta confirmar la consulta y entonces se despliega; "No" mantiene el bloqueo con el mensaje indicado; se puede deshacer la confirmación; el servidor rechaza la venta si no llega la confirmación.

**Tasks:**
- [x] **Gate SAGRILAFT en los 4 flujos** — Agregar la pregunta y el bloqueo del panel de cliente (cliente y servidor) en venta directa y desde reserva, para vendedor y administrador.
- [x] **Desplegar el panel de cliente al confirmar** — El panel estaba siempre visible en opacidad 45%, lo que hacía ver un formulario apagado en vez de un paso pendiente. Ocultarlo por completo y desplegarlo al confirmar la consulta, con mensaje de confirmación, desplazamiento hacia el bloque y opción de deshacer.
- [ ] **Auditar la confirmación en la venta** — Guardar en la venta que se confirmó SAGRILAFT, con el usuario, la fecha y el número o soporte de la consulta, como evidencia auditable. Hoy la confirmación se valida pero no queda registrada.
- [ ] **Consulta de listas restrictivas desde el sistema** — Reemplazar la confirmación declarativa por una verificación real. Alternativas evaluadas: (a) cargar periódicamente las listas públicas gratuitas (OFAC, ONU, Unión Europea) y contrastar por nombre y documento con coincidencia aproximada; (b) integrar un proveedor comercial con API que consolide listas, PEP y antecedentes. Requiere definición del área de cumplimiento.

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
- [x] **Feed de actividad en vivo** — Agregar un panel con las últimas acciones (última venta, quién y hace cuánto) para seguir el pulso del evento.

### HU-802 · Página de inicio accionable ✅
**Como** administrador, **quiero** que la página de inicio muestre el pulso del lanzamiento en vez de repetir los mismos indicadores, **para** saber de un vistazo cómo va el día, qué acaba de pasar y dónde están los precios.

**Criterios de aceptación:**
1. No se repite el mismo dato en varios bloques: los tres que mostraban disponibles/reservados/vendidos se consolidan en uno.
2. Se muestran ventas y valor de hoy, ritmo de ventas por hora y cuánto hace que se registró la última venta.
3. La actividad en vivo se ve en pantalla, con indicador del estado de la conexión (en vivo, reconectando, desconectado).
4. Se ve el estado de las listas de precio de todas las áreas, en solo lectura, con enlace a Inmuebles para gestionarlas.
5. La iconografía es consistente con el resto de la plataforma (SVG, no emojis).

**Tasks:**
- [x] **Eliminar la redundancia visual** — Los mismos tres números aparecían en cuatro bloques distintos (KPIs, progreso, resumen y dónut). Consolidar dejando el dónut y liberar el espacio.
- [x] **Pulso del día** — Agregar ventas y valor de hoy, ritmo por hora y tiempo transcurrido desde la última venta, con la unidad y el asesor.
- [x] **Feed de actividad en pantalla** — Renderizar los eventos SignalR que antes solo iban a la consola del navegador, sembrado con las últimas ventas e indicador de conexión.
- [x] **Listas de precio por área en solo lectura** — Tabla con la lista activa de cada área, si está fija o escalando automáticamente y cuántas ventas faltan para el siguiente escalón. La vista de conjunto no existía en ninguna pantalla desde que la gestión de listas pasó a ser por área.
- [x] **KPI de valor vendido** — El panel no mostraba dinero por ningún lado; agregarlo a la franja de indicadores.
- [x] **Unificar iconografía y limpiar código muerto** — Reemplazar los emojis de los KPIs por los SVG de `Iconos.cs` y retirar la función `cambiarLista()`, que referenciaba un formulario inexistente desde que la gestión de listas se movió a Inmuebles.
- [ ] **Alertas accionables** — Señalar inmuebles en proceso estancados y reservas próximas a vencer, para que el panel diga qué requiere atención.
- [ ] **Detalle de la unidad en el feed** — El evento en vivo identifica el inmueble por su ID interno; enriquecer el mensaje de SignalR para mostrar torre y apartamento.

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

### HU-903 · PDF técnico rediseñado ✅
**Como** gerencia, **quiero** un informe técnico de una sola pieza con la identidad de la empresa, **para** recibir un documento ejecutivo estandarizado sin anexos sueltos.

**Criterios de aceptación:** logo corporativo en el encabezado; documento continuo sin páginas anexas; módulos numerados 01–06 (estado, ventas, horas pico, asistencia, preventas por torre, en proceso); módulos sin datos colapsados en una línea; datos vivos es-CO; footer con paginación.

**Tasks:**
- [x] **Rediseño del PDF técnico con QuestPDF** — Reescribir la composición del informe según el handoff (header, franja KPI con íconos, donut, barras de tipología, tabla de ventas, asistencia y módulos colapsados).
- [x] **Logo corporativo en el encabezado** — Incorporar el logo de Londoño Gómez, degradando con elegancia al encabezado de solo texto si el archivo no está disponible en el servidor.
- [x] **Informe en una sola pieza** — Eliminar la página apaisada del cuadro de asistencia trasladando su información al flujo principal, sin perder datos.
- [x] **Reorganizar los módulos** — Nuevo orden 01–06 con el análisis de horas pico junto al detalle de ventas, y la asistencia y las torres integradas al documento.
- [x] **Quitar el flag de depuración** — Retirado `QuestPDF.Settings.EnableDebugging`, que era temporal para diagnosticar el error de layout.
- [ ] **Validar con datos reales** — Generar el informe con un lanzamiento completo y afinar detalles visuales.
- [ ] **Embeber la tipografía Barlow** — Incluir la fuente Barlow del diseño en lugar del fallback Arial actual.

### HU-904 · Reporte de asesores ✅
**Como** administrador, **quiero** un reporte por asesor, **para** medir el desempeño individual del equipo.

**Criterios de aceptación:** por asesor sus ventas, unidades y valor total.

**Tasks:**
- [x] **Reporte de asesores** — Implementar el reporte que agrupa por asesor sus ventas, unidades y valores.
- [ ] **Ranking histórico multi-proyecto** — Extender el reporte para comparar el desempeño de los asesores a través de varios proyectos/lanzamientos.

### HU-905 · Mapa de ventas exportable a Excel ✅
**Como** administrador, **quiero** exportar un mapa de inmuebles coloreado por estado (agrupado por torre y piso), **para** compartir con el equipo directivo una vista visual e imprimible del avance del lanzamiento.

**Criterios de aceptación:**
1. El Excel generado agrupa por torre, muestra los pisos y aptos con el color correspondiente a su estado (disponible/reservado/vendido).
2. Se genera con los datos del proyecto activo en el momento de la exportación.

**Tasks:**
- [x] **Generación del mapa en Excel (EPPlus)** — Implementar la exportación con formato y colores por estado, agrupado por torre y piso.
- [ ] **Unificar la generación del mapa** — Hoy existen dos implementaciones equivalentes (`VentasController.GenerarMapa` y `ReportesController.GenerarMapa`); consolidarlas en un solo servicio compartido para evitar mantenimiento duplicado.

### HU-906 · Listado global de ventas con paginación ✅
**Como** administrador, **quiero** ver todas las ventas del proyecto activo (de todos los vendedores) en un listado paginado, **para** auditar y consultar el histórico completo de ventas sin depender del informe del día.

**Criterios de aceptación:**
1. Listado paginado (25 por página) de todas las ventas del proyecto, ordenado por fecha descendente.
2. Incluye vendedor, cliente, inmueble, precio y fecha de cada venta.

**Tasks:**
- [x] **Listado de ventas con paginación server-side** — Implementar la consulta paginada (COUNT + OFFSET/FETCH) y la vista de todas las ventas del proyecto activo.
- [ ] **Filtros de búsqueda** — Agregar filtros por vendedor, rango de fechas o cliente al listado global de ventas.


### HU-907 · Análisis de horas pico y área de venta ✅
**Como** administrador, **quiero** ver a qué horas se vende más y qué áreas se venden en cada franja, **para** planear la operación del lanzamiento y la asignación del equipo comercial.

**Criterios de aceptación:**
1. Gráfica de líneas sobre plano cartesiano: eje X la hora del día (continuo, incluye las horas sin ventas para no distorsionar la jornada), eje Y el número de ventas y una línea por área (m²).
2. Indicadores destacados de hora pico, área líder y mejor combinación área+hora.
3. Las horas se calculan en zona horaria de Colombia (la fecha se almacena en UTC).
4. Existe una tabla de detalle por hora, accesible sin depender del color.
5. El análisis también aparece en el PDF técnico.

**Tasks:**
- [x] **Consulta de ventas por hora y área** — Agrupar las ventas activas por hora local (`DATEADD(HOUR,-5, FechaVenta)`) y por metraje, con conteo y valor.
- [x] **Gráfica de líneas con Chart.js** — Implementar el plano cartesiano con línea guía vertical, tooltip por hora con el valor vendido y total de la franja, y leyenda.
- [x] **Paleta validada para daltonismo** — La paleta inicial fallaba la separación de color para protanopía (naranja↔verde) y el contraste mínimo; reemplazarla por una verificada en luminosidad, croma, separación CVD y contraste.
- [x] **Límite de series con agrupación** — Como el metraje es texto libre y puede producir decenas de series ilegibles, graficar las 8 áreas con más ventas y agrupar el resto en "Otras áreas".
- [x] **Tabla de detalle por hora** — Vista de datos con total y valor por franja, resaltando la hora pico.
- [x] **Módulo en el PDF técnico** — Incluir hora pico, área líder y tabla por hora con barra proporcional en el informe.
- [ ] **Comparar contra lanzamientos anteriores** — Contrastar la curva del día con eventos previos del mismo proyecto para anticipar los picos.

### HU-908 · Navegación por pestañas en Reportes ✅
**Como** administrador, **quiero** moverme entre las vistas de Reportes sin perder pestañas ni recargar de más, **para** navegar el módulo con coherencia.

**Criterios de aceptación:** cada vista tiene URL propia y compartible; las tres pestañas (Dashboard, Horas pico y área, Cuadro de asistencia) se ven desde cualquiera de ellas; la pestaña activa se resalta.

**Tasks:**
- [x] **Extraer la barra de pestañas a un partial** — Centralizar `_Tabs.cshtml` y llevar sus estilos a `platform.css` para que las tres vistas compartan la misma barra y no se pierdan pestañas al navegar.
- [x] **Página propia para Horas pico** — Crear la acción y la vista `/Reportes/HorasPico` con URL propia en lugar de una pestaña interna.
- [x] **Corregir el espaciado del dashboard** — Retirar el contenedor que anulaba el `gap` del layout y dejaba banner, KPIs y tarjetas pegados entre sí.

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
- [x] **Crear el proyecto de pruebas** — Crear `PruebasLanzamientos` (xUnit) con casos reales de `Texto` (SoloDigitos, ParsearPrecio, DestinoVenta) y `Listas.ColumnaLista`.
- [ ] **Ampliar cobertura unitaria** — Cubrir la fórmula de escalamiento de lista (una vez unificada) y la política de contraseña con pruebas de los casos borde.
- [ ] **Pruebas de integración del flujo de venta** — Probar tomar→vender y reservar→vender (atomicidad, precio derivado en servidor, gate SAGRILAFT) con `WebApplicationFactory` contra una BD de pruebas.
- [ ] **`dotnet test` como gate del pipeline** — Hacer obligatorias las pruebas antes de cualquier despliegue a QA/Producción.

### HU-1104 · Manejo de errores y páginas de estado personalizadas ✅
**Como** usuario de la plataforma, **quiero** ver una página clara y de marca cuando ocurre un error o una página no existe, **para** no encontrarme con una pantalla técnica en blanco o un error crudo de servidor.

**Criterios de aceptación:**
1. Los errores 404 y demás códigos de estado se re-ejecutan hacia una página amigable (`/Error/{codigo}`).
2. En producción no se exponen stack traces ni detalles técnicos; en desarrollo sí, para depurar.

**Tasks:**
- [x] **Handler global de errores y códigos de estado** — Configurar `UseStatusCodePagesWithReExecute` y `UseExceptionHandler` en `Program.cs` con la acción `Error` de `HomeController`.
- [ ] **Página de error con soporte de marca y acción de retorno** — Mejorar la vista de error para que oriente al usuario (volver al inicio según su rol) en vez de un mensaje genérico.

---

## FEATURE 12 — Infraestructura, despliegue y DevOps

**Descripción:** Todo lo relacionado con llevar y mantener la aplicación en la nube. Define los dos entornos idénticos y aislados (QA y Producción) en Azure, el pipeline de CI/CD por ramas con aprobación manual a Producción desde Azure DevOps, y el monitoreo y respaldo necesarios para operar con confianza durante los lanzamientos.

### HU-1201 · Entornos QA y Producción idénticos 🔨
**Como** equipo de desarrollo, **quiero** dos entornos aislados con la misma configuración, **para** probar cada versión en QA antes de liberarla.

**Criterios de aceptación:** recursos separados por entorno; configuración sensible en el entorno (variables/Key Vault); QA no comparte BD con Producción.

**Tasks:**
- [x] **Definir arquitectura y costeo** — Elegir App Service B2 (Linux) + Azure SQL Serverless por entorno y documentar el costo aproximado para aprobación de gerencia.
- [x] **Crear el entorno de QA** — App Service y Azure SQL Serverless de QA creados y desplegados en Azure.
- [ ] **Ajustar el timeout de conexión a la base serverless** — Configurar `Connection Timeout=90;ConnectRetryCount=3;ConnectRetryInterval=10` en la cadena de conexión de QA (y de Producción) para tolerar el arranque en frío de la base al auto-pausarse.
- [ ] **Crear el entorno de Producción** — Crear el Resource Group, App Service y Azure SQL de Producción con las mismas configuraciones de seguridad que QA.
- [ ] **Dominio y subdominio con certificado** — Configurar el dominio propio y el subdominio de QA con el certificado administrado por Azure.

### HU-1202 · CI/CD por ramas con aprobación a Producción 🔨
**Como** equipo de desarrollo, **quiero** que `DEVELOP`/`QA` desplieguen a QA y la rama de Producción despliegue con aprobación manual, **para** liberar con control y trazabilidad desde Azure DevOps.

**Criterios de aceptación:** pipeline YAML único; Producción exige aprobación en el Environment; los PR no despliegan.

**Tasks:**
- [x] **Pipeline con despliegue por rama y aprobación** — Crear `azure-pipelines.yml` que compila una vez y despliega a QA o a Producción según la rama, con aprobación en el entorno de Producción.
- [x] **Conectar el repositorio a Azure DevOps** — Repo `Lanzamientos` creado en Azure Repos, remoto configurado desde el equipo local, y ramas `DEVELOP`/`QA` sincronizadas con el flujo GitHub → local → Azure DevOps.
- [x] **Flujo de promoción DEVELOP → QA por Pull Request** — Establecer el proceso de Pull Request dentro de Azure DevOps para llevar los cambios de `DEVELOP` a `QA` sin merges locales.
- [ ] **Ajustar el YAML a los nombres reales de rama** — Cambiar el trigger de `qa`/`master` a los nombres reales de las ramas del repo (`QA` y la rama de Producción que se defina).
- [ ] **Crear Environments y Service Connections** — Crear los Environments `QA`/`Production` en Pipelines, con su aprobador en Production, y las Service Connections por entorno con permisos mínimos.
- [ ] **Completar nombres reales en el YAML** — Reemplazar los placeholders del pipeline por los nombres reales de los App Service y las service connections, y ejecutar el pipeline por primera vez.

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
| 4. Inventario y estados de inmuebles | HU-401…HU-405 | ✅ |
| 5. Flujo de venta del vendedor | HU-501…HU-504 | ✅ |
| 6. Registro de ventas y cumplimiento | HU-601…HU-602 | ✅ |
| 7. Gestión de clientes | HU-701 | ✅ (privacidad pendiente) |
| 8. Dashboard en tiempo real | HU-801…HU-802 | ✅ |
| 9. Reportes y exportaciones | HU-901…HU-908 | ✅ |
| 10. Cuadro de asistencia | HU-1001…HU-1002 | ✅ |
| 11. Seguridad técnica | HU-1101…HU-1104 | 🔨 |
| 12. Infraestructura y DevOps | HU-1201…HU-1203 | 🔨 |

**Total: 12 Features (con descripción) · 41 Historias de Usuario (con ≥1 Task) · 125+ Tasks (cada una con título + descripción).**

> **Carga sugerida:** Feature → pegar su *Descripción*; User Story → narrativa "Como… quiero… para…" en *Description* y los criterios en *Acceptance Criteria*; Task → el **título** como nombre del work item y la **descripción** (el texto después del guion) en su campo *Description*. Las tasks `[x]` se crean en *Done*; las `[ ]` en *To Do*.
- [x] **Integrar la asistencia al informe principal** — Trasladar tarjetas resumen, tabla por día, línea de citas y observaciones al flujo del PDF técnico, eliminando la página apaisada anexa.
- [x] **Módulo de preventas por torre** — Rescatar los datos de torres/etapas (preventas, ventas, opciones y sus valores) que solo existían en la página eliminada y publicarlos como módulo 05 del informe.
