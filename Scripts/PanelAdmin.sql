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
--    Por dónde se enteró del proyecto. Lista cerrada en Texto.MediosPermitidos:
--    si fuera texto libre no se podrían agrupar los canales en el informe.
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

PRINT 'Panel de administrador: migración aplicada correctamente.';
GO
