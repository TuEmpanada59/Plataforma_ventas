-- ============================================================================
-- Panel de administrador — trazabilidad, anulación de ventas y control operativo
--
-- Agrega:
--   1) Columnas de anulación en Ventas
--   3) Tabla HistorialListas (trazabilidad de precios)
--   4) HorasVigenciaReserva en Proyectos (vencimiento de reservas)
--   5) Migración del destino "Vivienda" a "Uso propio"
--   6) Observaciones en reservas y en ventas
--   7) Medio publicitario del cliente
--   8) Tabla AsistenciaFranja (familias por franja horaria)
--   9) Relleno de Inmuebles.Torre desde el nombre de la unidad
--  10) Tablas AjustesPrecio y AjustesPrecioDetalle (ajuste masivo con reversión)
--  11) Ventas.Origen (ventas que llegan en el Excel)
--  12) Inmuebles.Etapa (una hoja del Excel por etapa)
--  13) Quitar la unicidad de Documento y Correo en Usuarios
--  14) Tabla MediosPublicitarios con su carga inicial
--
-- Es IDEMPOTENTE: se puede ejecutar varias veces sin romper nada ni perder datos.
-- Ejecutar en la base de datos Lanzamientos.
-- ============================================================================

-- ────────────────────────────────────────────────────────────────────────────
-- 1) Anulación de ventas
--    La venta NO se borra: se marca como ANULADA conservando el registro
--    completo, con el motivo, quién la anuló y cuándo.
-- ────────────────────────────────────────────────────────────────────────────
IF COL_LENGTH('Ventas', 'MotivoAnulacion') IS NULL
    ALTER TABLE Ventas ADD MotivoAnulacion NVARCHAR(500) NULL;
GO

IF COL_LENGTH('Ventas', 'FechaAnulacion') IS NULL
    ALTER TABLE Ventas ADD FechaAnulacion DATETIME NULL;
GO

IF COL_LENGTH('Ventas', 'IdUsuarioAnula') IS NULL
    ALTER TABLE Ventas ADD IdUsuarioAnula INT NULL;
GO

-- ────────────────────────────────────────────────────────────────────────────
-- 2) (Sin uso) Tabla Auditoria
--    Se retiró: la pantalla de auditoría ya no forma parte de la plataforma.
--    Los eventos (login, anulaciones, cambios de lista) siguen quedando en el
--    log de la aplicación. El servicio que los registraba detecta que la tabla
--    no existe y se desactiva solo, así que no hay nada que crear aquí.
--    Los números de las secciones siguientes se conservan a propósito.
-- ────────────────────────────────────────────────────────────────────────────

-- ────────────────────────────────────────────────────────────────────────────
-- 3) Historial de cambios de lista de precios
--    Permite responder "¿por qué este apartamento se vendió a este precio?".
-- ────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID('HistorialListas', 'U') IS NULL
BEGIN
    CREATE TABLE HistorialListas (
        IdHistorial  BIGINT        IDENTITY(1,1) PRIMARY KEY,
        IdProyecto   INT           NOT NULL,
        Metros       NVARCHAR(50)  NOT NULL DEFAULT '',
        ListaAnterior INT          NOT NULL,
        ListaNueva   INT           NOT NULL,
        Motivo       NVARCHAR(30)  NOT NULL,   -- AUTOMATICO | MANUAL
        IdUsuario    INT           NULL,
        Usuario      NVARCHAR(150) NOT NULL DEFAULT '',
        Fecha        DATETIME      NOT NULL DEFAULT GETUTCDATE()
    );

    CREATE INDEX IX_HistorialListas_Proy ON HistorialListas (IdProyecto, Fecha DESC);
END
GO

-- ────────────────────────────────────────────────────────────────────────────
-- 4) Vigencia de las reservas
--    0 = la reserva no vence (comportamiento actual, es el valor por defecto
--    para no cambiar el funcionamiento de los proyectos ya cargados).
--    El vencimiento NUNCA libera la reserva de forma automática: solo la marca
--    como vencida para que el administrador decida.
-- ────────────────────────────────────────────────────────────────────────────
IF COL_LENGTH('Proyectos', 'HorasVigenciaReserva') IS NULL
    ALTER TABLE Proyectos ADD HorasVigenciaReserva INT NOT NULL DEFAULT 0;
GO


-- ────────────────────────────────────────────────────────────────────────────
-- 5) Renombrar el destino "Vivienda" a "Uso propio"
--    Las ventas ya registradas se migran para que los reportes no queden
--    partidos entre el nombre viejo y el nuevo.
-- ────────────────────────────────────────────────────────────────────────────
UPDATE Ventas SET Destino = 'Uso propio' WHERE Destino = 'Vivienda';
GO

-- ────────────────────────────────────────────────────────────────────────────
-- 6) Observaciones en reservas y en ventas
--    La observación de la reserva vive en el inmueble (junto al resto del
--    estado de la reserva) y se limpia cuando la reserva se libera o se vende.
--    La de la venta queda en el registro de la venta, de forma permanente.
-- ────────────────────────────────────────────────────────────────────────────
IF COL_LENGTH('Inmuebles', 'ObservacionReserva') IS NULL
    ALTER TABLE Inmuebles ADD ObservacionReserva NVARCHAR(500) NULL;
GO

IF COL_LENGTH('Ventas', 'Observaciones') IS NULL
    ALTER TABLE Ventas ADD Observaciones NVARCHAR(1000) NULL;
GO

-- ────────────────────────────────────────────────────────────────────────────
-- 7) Medio publicitario del cliente
--    Por dónde se enteró del proyecto. Se guarda como texto, validado contra el
--    catálogo de la sección 14: si fuera texto libre no se podrían agrupar los
--    canales en el informe.
-- ────────────────────────────────────────────────────────────────────────────
IF COL_LENGTH('Clientes', 'MedioPublicitario') IS NULL
    ALTER TABLE Clientes ADD MedioPublicitario NVARCHAR(80) NULL;
GO

-- ────────────────────────────────────────────────────────────────────────────
-- 8) Asistencia por franja horaria (opcional)
--    Las ventas tienen hora exacta, pero la asistencia se captura como total
--    del día, así que no se podían cruzar para ver picos dentro de la jornada.
--    Esta tabla guarda SOLO el conteo de familias por franja: es el dato que
--    dibuja la curva, y pedir las nueve métricas por franja haría inviable el
--    conteo durante el evento.
--    Es OPCIONAL: si un día no tiene franjas, el informe compara por día.
-- ────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID('AsistenciaFranja', 'U') IS NULL
BEGIN
    CREATE TABLE AsistenciaFranja (
        IdFranja  INT           IDENTITY(1,1) PRIMARY KEY,
        IdDia     INT           NOT NULL,
        Orden     INT           NOT NULL DEFAULT 0,
        HoraDesde INT           NOT NULL,          -- hora de inicio (0-23)
        HoraHasta INT           NOT NULL,          -- hora de fin, excluyente
        Familias  INT           NOT NULL DEFAULT 0,
        CONSTRAINT FK_AsistFranja_Dia FOREIGN KEY (IdDia)
            REFERENCES AsistenciaDia (IdDia) ON DELETE CASCADE
    );

    CREATE INDEX IX_AsistFranja_Dia ON AsistenciaFranja (IdDia, Orden);
END
GO

-- ────────────────────────────────────────────────────────────────────────────
-- 9) Torre a partir del nombre de la unidad
--    Los archivos traen la torre dentro del nombre comercial ("1204 T3"), no
--    siempre en una columna TORRE. Las cargas nuevas ya la extraen al subir el
--    Excel; esto completa los proyectos que se cargaron antes, para que el
--    filtro por torre los incluya. Solo toca filas sin torre: no pisa un valor
--    cargado a mano.
-- ────────────────────────────────────────────────────────────────────────────
UPDATE Inmuebles
SET Torre = 'T' + SUBSTRING(Apto, PATINDEX('%T[0-9]%', Apto) + 1, 1)
WHERE (Torre IS NULL OR LTRIM(RTRIM(Torre)) = '')
  AND Apto IS NOT NULL
  AND PATINDEX('%T[0-9]%', Apto) > 0;
GO

-- ────────────────────────────────────────────────────────────────────────────
-- 10) Ajuste masivo de precios con reversión
--     Subir un 3 % todas las listas de un proyecto se hacía a mano, inmueble
--     por inmueble. Estas dos tablas guardan cada ajuste con el precio ANTERIOR
--     de cada inmueble y cada lista, que es lo único que permite deshacerlo:
--     sin el detalle, un ajuste mal hecho no tiene vuelta atrás.
-- ────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID('AjustesPrecio', 'U') IS NULL
BEGIN
    CREATE TABLE AjustesPrecio (
        IdAjuste       BIGINT        IDENTITY(1,1) PRIMARY KEY,
        IdProyecto     INT           NOT NULL,
        Torre          NVARCHAR(50)  NOT NULL DEFAULT '',   -- '' = todas
        Metros         NVARCHAR(50)  NOT NULL DEFAULT '',   -- '' = todas las áreas
        Listas         NVARCHAR(20)  NOT NULL DEFAULT '',   -- '1,2,3'
        Tipo           NVARCHAR(12)  NOT NULL,              -- PESOS | PORCENTAJE
        Valor          DECIMAL(18,4) NOT NULL,              -- puede ser negativo (bajada)
        Unidades       INT           NOT NULL DEFAULT 0,
        IdUsuario      INT           NULL,
        Usuario        NVARCHAR(150) NOT NULL DEFAULT '',
        Fecha          DATETIME      NOT NULL DEFAULT GETUTCDATE(),
        Revertido      BIT           NOT NULL DEFAULT 0,
        FechaReversion DATETIME      NULL
    );

    CREATE INDEX IX_AjustesPrecio_Proy ON AjustesPrecio (IdProyecto, Fecha DESC);
END
GO

IF OBJECT_ID('AjustesPrecioDetalle', 'U') IS NULL
BEGIN
    CREATE TABLE AjustesPrecioDetalle (
        IdDetalle      BIGINT IDENTITY(1,1) PRIMARY KEY,
        IdAjuste       BIGINT NOT NULL,
        IdInmueble     INT    NOT NULL,
        NumLista       INT    NOT NULL,
        PrecioAnterior BIGINT NOT NULL,
        PrecioNuevo    BIGINT NOT NULL,
        CONSTRAINT FK_AjusteDet_Ajuste FOREIGN KEY (IdAjuste)
            REFERENCES AjustesPrecio (IdAjuste) ON DELETE CASCADE
    );

    CREATE INDEX IX_AjusteDet_Ajuste ON AjustesPrecioDetalle (IdAjuste);
END
GO

-- ────────────────────────────────────────────────────────────────────────────
-- 11) Origen de la venta
--     Los inmuebles que llegan VENDIDO en el Excel ahora generan su venta al
--     cargar el proyecto, para que aparezcan en el listado y en los informes.
--     Esas ventas nacen incompletas (sin cliente ni asesor reales), así que hay
--     que poder distinguirlas de las que se registraron en la plataforma.
-- ────────────────────────────────────────────────────────────────────────────
IF COL_LENGTH('Ventas', 'Origen') IS NULL
    ALTER TABLE Ventas ADD Origen NVARCHAR(20) NOT NULL DEFAULT 'PLATAFORMA';
GO

-- ────────────────────────────────────────────────────────────────────────────
-- 12) Etapa del inmueble
--     Los archivos de suites traen "Etapa 1" y "Etapa 2" en hojas distintas del
--     mismo libro, pero es un solo lanzamiento: un proyecto, un código de acceso
--     y unas cifras que suman el total. La etapa es el nombre de la hoja.
--     Vacía en los proyectos de una sola hoja, que son la mayoría.
-- ────────────────────────────────────────────────────────────────────────────
IF COL_LENGTH('Inmuebles', 'Etapa') IS NULL
    ALTER TABLE Inmuebles ADD Etapa NVARCHAR(60) NOT NULL DEFAULT '';
GO

-- ────────────────────────────────────────────────────────────────────────────
-- 13) Quitar la unicidad de Documento y Correo en Usuarios
--     Al crear una cuenta ya no se piden: todos los usuarios nuevos los comparten
--     vacíos. Con una restricción UNIQUE encima, el primero entra y el segundo es
--     rechazado, así que la restricción ya no describe una regla del negocio.
--     El nombre de usuario SÍ sigue siendo único; eso se valida en la aplicación.
--
--     Se arma dinámicamente porque el nombre de la restricción lo puso SQL Server
--     al crear la tabla y no es el mismo en todas las bases.
-- ────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID('Usuarios', 'U') IS NOT NULL
BEGIN
    DECLARE @sqlUq NVARCHAR(MAX) = N'';

    -- Restricciones UNIQUE declaradas sobre esas columnas
    SELECT @sqlUq = @sqlUq + N'ALTER TABLE Usuarios DROP CONSTRAINT ' + QUOTENAME(kc.name) + N';'
    FROM sys.key_constraints kc
    JOIN sys.index_columns ic ON ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id
    JOIN sys.columns c        ON c.object_id  = ic.object_id        AND c.column_id = ic.column_id
    WHERE kc.parent_object_id = OBJECT_ID('Usuarios')
      AND kc.type = 'UQ'
      AND c.name IN ('Documento', 'Correo');

    -- Índices únicos sueltos (sin restricción asociada) sobre esas columnas
    SELECT @sqlUq = @sqlUq + N'DROP INDEX ' + QUOTENAME(i.name) + N' ON Usuarios;'
    FROM sys.indexes i
    JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
    JOIN sys.columns c        ON c.object_id  = ic.object_id AND c.column_id = ic.column_id
    WHERE i.object_id = OBJECT_ID('Usuarios')
      AND i.is_unique = 1
      AND i.is_primary_key = 0
      AND i.is_unique_constraint = 0
      AND c.name IN ('Documento', 'Correo');

    IF LEN(@sqlUq) > 0
    BEGIN
        EXEC sp_executesql @sqlUq;
        PRINT 'Usuarios: se retiró la unicidad de Documento y Correo.';
    END
END
GO

-- ────────────────────────────────────────────────────────────────────────────
-- 14) Catálogo de medios publicitarios
--     Antes era una lista fija en el código: agregar un medio obligaba a tocar
--     el código y volver a desplegar. Ahora es una tabla que el administrador
--     mantiene desde Clientes → Medios publicitarios.
--
--     El medio se sigue guardando como TEXTO en Clientes.MedioPublicitario: así
--     un cliente conserva el canal por el que entró aunque el medio se elimine
--     después del catálogo. Borrar un medio quita la opción para los nuevos
--     registros, no reescribe la historia.
-- ────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID('MediosPublicitarios', 'U') IS NULL
BEGIN
    CREATE TABLE MediosPublicitarios (
        IdMedio INT           IDENTITY(1,1) PRIMARY KEY,
        Nombre  NVARCHAR(120) NOT NULL
    );

    CREATE UNIQUE INDEX UX_Medios_Nombre ON MediosPublicitarios (Nombre);
END
GO

-- Carga inicial. Solo inserta los que falten, así que se puede repetir sin
-- duplicar y sin pisar los que el administrador haya agregado o renombrado.
INSERT INTO MediosPublicitarios (Nombre)
SELECT v.Nombre
FROM (VALUES
        (N'Activaciones'),
        (N'Aeropuerto'),
        (N'Alejandro Falla'),
        (N'Aliado'),
        (N'Aliado David Beleño'),
        (N'Aliado Informe Inmobiliario'),
        (N'Analfe'),
        (N'Analitik'),
        (N'Argos'),
        (N'Arquitectura y Concreto'),
        (N'Asohost'),
        (N'Banderas'),
        (N'BD EL SITIO'),
        (N'BD migrada arrendatarios'),
        (N'Bici-Valla'),
        (N'BRIKSS'),
        (N'Buses'),
        (N'Café Le gris'),
        (N'Canchas'),
        (N'Carro Valla'),
        (N'Cauca Viejo'),
        (N'Cavipetrol'),
        (N'Centros Comerciales'),
        (N'Cerramiento'),
        (N'Ciencuadras'),
        (N'Claro media'),
        (N'Club El Nogal'),
        (N'Club Intelecto'),
        (N'Colraices'),
        (N'Comercial de Televisión'),
        (N'Compensar'),
        (N'Corbeta'),
        (N'Cotizador'),
        (N'El Bellanita'),
        (N'El Heraldo'),
        (N'El sitio inmobiliario'),
        (N'Email base de datos UMBRAL'),
        (N'Empleado Arquitectura y Concreto'),
        (N'Empleado Bemsa'),
        (N'Empleado CASA'),
        (N'Empleado Conexo'),
        (N'Empleado Crystal'),
        (N'Empleado Muros y Techos'),
        (N'Empleado Umbral'),
        (N'Empleados FIC'),
        (N'Escuelas de Equitación'),
        (N'Eucoles'),
        (N'Expoinmobiliaria-Camacol'),
        (N'FEC Bancolombia'),
        (N'FEPEP'),
        (N'Feria Barcelona'),
        (N'Feria Colombianos en el exterior'),
        (N'Feria Compensar'),
        (N'Feria de Oriente'),
        (N'Feria Expocolombia'),
        (N'Feria Finca Raíz Open Day'),
        (N'Feria Fiscalía'),
        (N'Feria itinerante'),
        (N'Feria Jardines de Llanogrande'),
        (N'Feria Londoño Gómez'),
        (N'Feria Marco Fidel Suárez'),
        (N'Feria Smurfit Kappa'),
        (N'Feria tu norte'),
        (N'Feria Virtual FR'),
        (N'Finca Clic'),
        (N'FNA'),
        (N'Fondo Presente'),
        (N'Fundación Ellen Riegner de Casas'),
        (N'Gojom'),
        (N'Grupo Inversionistas Juan Londoño'),
        (N'GSI'),
        (N'Hablador'),
        (N'HOUM'),
        (N'Hugo Zapata'),
        (N'Juan Londoño'),
        (N'Local Parque Fabricato'),
        (N'Mega Feria AyC'),
        (N'Mi Oriente'),
        (N'Minuto 30'),
        (N'Multi Homes'),
        (N'Nota económica'),
        (N'Pantallas'),
        (N'Pauta Digital CE'),
        (N'Pedro Fernández'),
        (N'Periódico Mi Casa en Colombia'),
        (N'Plan Inmobiliario'),
        (N'Programa TV Tu Espacio'),
        (N'Proppit'),
        (N'QHubo'),
        (N'Radio'),
        (N'Redes Sociales Organico'),
        (N'Restaurante'),
        (N'Revista Columbus'),
        (N'Revista Destino Inmobiliario'),
        (N'Revista El Pulso'),
        (N'Revista Fondo Presente'),
        (N'Revista Lazoos'),
        (N'Revista Tu Espacio'),
        (N'Rompetráfico'),
        (N'Saldos Industriales'),
        (N'Serenity Seniors Club'),
        (N'Smartfit'),
        (N'SMS Base de datos LG'),
        (N'SMS Base de datos UMBRAL'),
        (N'Stand Copacabana'),
        (N'Stand San Nicolás'),
        (N'Sucasaya'),
        (N'Te acerca Vivienda'),
        (N'Teleantioquia'),
        (N'Telemedellín'),
        (N'Torre Almagrán'),
        (N'Treasure'),
        (N'Tu propiedad Colombia'),
        (N'Umbral'),
        (N'Unión Andina'),
        (N'Visita empresa'),
        (N'Viventa'),
        (N'Vivir en el Poblado'),
        (N'Webinar Colombianos en el exterior'),
        (N'Whatsapp base de datos externa'),
        (N'Whatsapp.'),
        (N'360 Inmobiliario'),
        (N'Alianza Bancolombia'),
        (N'Ascensores'),
        (N'Call Center'),
        (N'Cliente LG'),
        (N'Club Unión'),
        (N'Colombia Raíz'),
        (N'Construcaribe'),
        (N'Convenio Cosmovisión'),
        (N'Convenio Empresas'),
        (N'Correo Directo'),
        (N'El Colombiano'),
        (N'Email base de datos externa'),
        (N'Email base de datos LG'),
        (N'Email Fincaraiz'),
        (N'Empleado Bancolombia'),
        (N'Empleado Londoño Gómez'),
        (N'Estrenar Vivienda'),
        (N'Feria Camacol'),
        (N'Feria Davivienda y Fiscalia'),
        (N'Feria La Lonja'),
        (N'Feria Mega Sale de Scotiabank Colpatria'),
        (N'Feria Salón Del Inmueble'),
        (N'Feria VIMO'),
        (N'Finca Raíz'),
        (N'Gestión Base de datos LG'),
        (N'Hatoviejo'),
        (N'La Haus'),
        (N'Landing Page'),
        (N'Mailing'),
        (N'Malla de servicios'),
        (N'Medio Impreso'),
        (N'Pasacalle'),
        (N'Pauta Digital Lg'),
        (N'Pendón'),
        (N'Properati'),
        (N'Propietario Proyecto'),
        (N'Página Web'),
        (N'Recorrido Sector'),
        (N'Referido'),
        (N'Referido A&C'),
        (N'Referido Colega'),
        (N'Referido Empleado'),
        (N'Referido Gerencia'),
        (N'Referido Propietario'),
        (N'Referido Sala De Ventas'),
        (N'Referido Socio'),
        (N'Revista Informe Inmobiliario'),
        (N'Revista Oriente'),
        (N'Revista Propiedades'),
        (N'Sala de Ventas Virtual'),
        (N'Socios del Proyecto'),
        (N'Stand'),
        (N'Valla'),
        (N'Vecindario'),
        (N'Vivendo'),
        (N'Viviendas Universales'),
        (N'Volante'),
        (N'Voz a Voz'),
        (N'Waze'),
        (N'Whatsapp El Colombiano'),
        (N'Zoho forms')
     ) AS v(Nombre)
WHERE NOT EXISTS (SELECT 1 FROM MediosPublicitarios m WHERE m.Nombre = v.Nombre);
GO

-- ============================================================================
-- 15) ACTIVIDAD COMERCIAL DEL PROYECTO
--     Un proyecto puede salir a vender de tres formas distintas, y de eso depende
--     qué cuenta como resultado del evento:
--       PROYECTO_NUEVO  estreno, todo el inventario sale por primera vez
--       ACTIVACION      se vuelve a salir con un inventario que ya tenía ventas
--       NUEVA_ETAPA     se lanza una etapa de un proyecto con historia
--     La regla de conteo es una sola: entra al lanzamiento lo que llega DISPONIBLE
--     en el Excel. Lo que llega vendido o reservado estaba comprometido antes.
-- ============================================================================
IF COL_LENGTH('Proyectos', 'Actividad') IS NULL
BEGIN
    ALTER TABLE Proyectos ADD Actividad NVARCHAR(30) NOT NULL
        CONSTRAINT DF_Proyectos_Actividad DEFAULT 'PROYECTO_NUEVO';
    PRINT 'Columna Proyectos.Actividad creada.';
END
GO

IF COL_LENGTH('Proyectos', 'EtapaLanzamiento') IS NULL
BEGIN
    ALTER TABLE Proyectos ADD EtapaLanzamiento NVARCHAR(100) NOT NULL
        CONSTRAINT DF_Proyectos_EtapaLanz DEFAULT '';
    PRINT 'Columna Proyectos.EtapaLanzamiento creada.';
END
GO

-- Marca por unidad. Se calcula UNA vez, en el momento de la carga, y no se vuelve
-- a tocar: si se recalculara con el estado actual, el total del lanzamiento bajaría
-- solo a medida que se venden las unidades y el porcentaje de avance no cuadraría.
IF COL_LENGTH('Inmuebles', 'EnLanzamiento') IS NULL
BEGIN
    ALTER TABLE Inmuebles ADD EnLanzamiento BIT NOT NULL
        CONSTRAINT DF_Inmuebles_EnLanz DEFAULT 1;
    PRINT 'Columna Inmuebles.EnLanzamiento creada.';

    -- Reconstrucción para los proyectos ya cargados: queda fuera del lanzamiento lo
    -- que se importó vendido desde el Excel y lo que llegó reservado sin asesor.
    IF COL_LENGTH('Ventas', 'Origen') IS NOT NULL
        EXEC sp_executesql N'
            UPDATE i SET i.EnLanzamiento = 0
            FROM Inmuebles i
            JOIN Ventas v ON v.IdInmueble = i.IdInmuebles AND v.Origen = ''EXCEL''';

    IF COL_LENGTH('Inmuebles', 'IdVendedorReserva') IS NOT NULL
        EXEC sp_executesql N'
            UPDATE Inmuebles SET EnLanzamiento = 0
            WHERE Estado = ''RESERVADO'' AND IdVendedorReserva IS NULL';

    PRINT 'Inmuebles.EnLanzamiento reconstruido para los proyectos existentes.';
END
GO

-- Historial de actividades: una fila por evento comercial del proyecto, con la foto
-- de las cifras al momento de arrancar. Guardar la foto permite comparar lanzamientos
-- del mismo proyecto meses después, cuando el inventario ya se movió.
IF OBJECT_ID('ProyectoActividades', 'U') IS NULL
BEGIN
    CREATE TABLE ProyectoActividades (
        IdActividad         INT           IDENTITY(1,1) PRIMARY KEY,
        IdProyecto          INT           NOT NULL,
        Tipo                NVARCHAR(30)  NOT NULL DEFAULT 'PROYECTO_NUEVO',
        Etapa               NVARCHAR(100) NOT NULL DEFAULT '',
        FechaInicio         DATETIME      NOT NULL DEFAULT GETDATE(),
        TotalProyecto       INT           NOT NULL DEFAULT 0,
        TotalLanzamiento    INT           NOT NULL DEFAULT 0,
        HistoricoVendidas   INT           NOT NULL DEFAULT 0,
        HistoricoReservadas INT           NOT NULL DEFAULT 0
    );
    CREATE INDEX IX_ProyActividades_Proy ON ProyectoActividades (IdProyecto, FechaInicio DESC);
    PRINT 'Tabla ProyectoActividades creada.';
END
GO

-- ============================================================================
-- 16) ÍNDICES
--     Las secciones anteriores crean sus índices junto con la tabla, dentro del
--     mismo IF. Eso deja un hueco: si la base se montó a partir de un script de
--     esquema generado desde otra base, las tablas ya existen, el IF no entra y
--     los índices nunca se crean. El asistente "Generar scripts" de SSMS además
--     omite los índices no agrupados salvo que se le pida expresamente.
--     Esta sección los crea aparte, comprobando uno por uno.
-- ============================================================================
IF OBJECT_ID('HistorialListas','U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_HistorialListas_Proy'
                                               AND object_id = OBJECT_ID('HistorialListas'))
BEGIN
    CREATE INDEX IX_HistorialListas_Proy ON HistorialListas (IdProyecto, Fecha DESC);
    PRINT 'Índice IX_HistorialListas_Proy creado.';
END
GO

IF OBJECT_ID('AsistenciaFranja','U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AsistFranja_Dia'
                                               AND object_id = OBJECT_ID('AsistenciaFranja'))
BEGIN
    CREATE INDEX IX_AsistFranja_Dia ON AsistenciaFranja (IdDia, Orden);
    PRINT 'Índice IX_AsistFranja_Dia creado.';
END
GO

IF OBJECT_ID('AjustesPrecio','U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AjustesPrecio_Proy'
                                               AND object_id = OBJECT_ID('AjustesPrecio'))
BEGIN
    CREATE INDEX IX_AjustesPrecio_Proy ON AjustesPrecio (IdProyecto, Fecha DESC);
    PRINT 'Índice IX_AjustesPrecio_Proy creado.';
END
GO

IF OBJECT_ID('AjustesPrecioDetalle','U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AjusteDet_Ajuste'
                                               AND object_id = OBJECT_ID('AjustesPrecioDetalle'))
BEGIN
    CREATE INDEX IX_AjusteDet_Ajuste ON AjustesPrecioDetalle (IdAjuste);
    PRINT 'Índice IX_AjusteDet_Ajuste creado.';
END
GO

IF OBJECT_ID('ProyectoActividades','U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProyActividades_Proy'
                                               AND object_id = OBJECT_ID('ProyectoActividades'))
BEGIN
    CREATE INDEX IX_ProyActividades_Proy ON ProyectoActividades (IdProyecto, FechaInicio DESC);
    PRINT 'Índice IX_ProyActividades_Proy creado.';
END
GO

-- Este es único: es lo que impide que queden dos medios publicitarios con el mismo
-- nombre, que después aparecen repetidos en el formulario de venta y parten en dos
-- las cifras del reporte de "cómo se enteraron".
IF OBJECT_ID('MediosPublicitarios','U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Medios_Nombre'
                                               AND object_id = OBJECT_ID('MediosPublicitarios'))
BEGIN
    IF EXISTS (SELECT Nombre FROM MediosPublicitarios GROUP BY Nombre HAVING COUNT(*) > 1)
        PRINT 'ATENCIÓN: hay medios publicitarios repetidos. Depúralos y vuelve a ejecutar esta sección.';
    ELSE
    BEGIN
        CREATE UNIQUE INDEX UX_Medios_Nombre ON MediosPublicitarios (Nombre);
        PRINT 'Índice UX_Medios_Nombre creado.';
    END
END
GO

-- ============================================================================
-- 17) USUARIOS ACTIVOS E INACTIVOS
--     Un asesor que sale del equipo no se puede borrar: sus ventas quedarían
--     huérfanas y la base lo rechaza por la llave foránea de Ventas. Antes eso
--     terminaba en un error 500 sin explicación.
--     Con esta columna la cuenta se inactiva: la persona no puede entrar, pero
--     su historial y sus cifras siguen intactos en los reportes.
-- ============================================================================
IF COL_LENGTH('Usuarios', 'Activo') IS NULL
BEGIN
    ALTER TABLE Usuarios ADD Activo BIT NOT NULL
        CONSTRAINT DF_Usuarios_Activo DEFAULT 1;
    PRINT 'Columna Usuarios.Activo creada. Todas las cuentas existentes quedan activas.';
END
GO

-- ============================================================================
-- 18) ENLACES DEL PROYECTO
--     El material del lanzamiento (presentación, brochure, render, formulario)
--     hoy se reparte por WhatsApp y cada asesor termina con una versión
--     distinta. Aquí el administrador publica el enlace una vez y todos los
--     asesores del proyecto ven el mismo.
--
--     Los enlaces son por proyecto: el material de un lanzamiento no tiene por
--     qué aparecer en el siguiente.
-- ============================================================================
IF OBJECT_ID('ProyectoEnlaces', 'U') IS NULL
BEGIN
    CREATE TABLE ProyectoEnlaces (
        IdEnlace      INT            IDENTITY(1,1) PRIMARY KEY,
        IdProyecto    INT            NOT NULL,
        Titulo        NVARCHAR(150)  NOT NULL,
        Url           NVARCHAR(1000) NOT NULL,
        Descripcion   NVARCHAR(400)  NOT NULL DEFAULT '',
        Orden         INT            NOT NULL DEFAULT 0,
        Visible       BIT            NOT NULL DEFAULT 1,
        FechaCreacion DATETIME       NOT NULL DEFAULT GETDATE(),
        IdUsuario     INT            NULL,
        CONSTRAINT FK_ProyEnlaces_Proyecto FOREIGN KEY (IdProyecto)
            REFERENCES Proyectos(IdProyectos) ON DELETE CASCADE
    );
    CREATE INDEX IX_ProyEnlaces_Proy ON ProyectoEnlaces (IdProyecto, Orden);
    PRINT 'Tabla ProyectoEnlaces creada.';
END
GO

PRINT 'Panel de administrador: migración aplicada correctamente.';
GO
