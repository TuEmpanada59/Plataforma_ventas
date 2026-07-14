-- ============================================================================
-- Cuadro de asistencia / resumen del lanzamiento
-- Reemplaza la antigua tabla "Asistencias" (registro por visitante) por un
-- modelo de RESUMEN DIARIO del evento, con métricas por día y por torre.
--
-- Ejecutar una sola vez en la base de datos Lanzamientos.
-- ============================================================================

-- 1) Eliminar el modelo viejo si existía
IF OBJECT_ID('Asistencias', 'U') IS NOT NULL DROP TABLE Asistencias;

-- 2) Eliminar el modelo nuevo si se está reinstalando (respetar orden por FKs)
IF OBJECT_ID('AsistenciaTorre',  'U') IS NOT NULL DROP TABLE AsistenciaTorre;
IF OBJECT_ID('AsistenciaDia',    'U') IS NOT NULL DROP TABLE AsistenciaDia;
IF OBJECT_ID('AsistenciaEvento', 'U') IS NOT NULL DROP TABLE AsistenciaEvento;

-- 3) Evento de lanzamiento (un resumen por proyecto)
CREATE TABLE AsistenciaEvento (
    IdEvento       INT            IDENTITY(1,1) PRIMARY KEY,
    IdProyecto     INT            NOT NULL,
    Titulo         NVARCHAR(200)  NOT NULL DEFAULT '',
    Observaciones  NVARCHAR(MAX)  NOT NULL DEFAULT '',
    FechaCreacion  DATETIME       NOT NULL DEFAULT GETDATE()
);

-- 4) Un día del lanzamiento (métricas de tráfico y de citas)
CREATE TABLE AsistenciaDia (
    IdDia            INT          IDENTITY(1,1) PRIMARY KEY,
    IdEvento         INT          NOT NULL,
    Fecha            DATE         NULL,
    NombreDia        NVARCHAR(50) NOT NULL DEFAULT '',
    Orden            INT          NOT NULL DEFAULT 0,
    -- Tráfico
    Familias         INT          NOT NULL DEFAULT 0,
    Adultos          INT          NOT NULL DEFAULT 0,
    Ninos            INT          NOT NULL DEFAULT 0,
    Mascotas         INT          NOT NULL DEFAULT 0,
    AsisteCita       INT          NOT NULL DEFAULT 0,
    Carros           INT          NOT NULL DEFAULT 0,
    Motos            INT          NOT NULL DEFAULT 0,
    Caminando        INT          NOT NULL DEFAULT 0,
    -- Citas / agendamiento
    AgendadosEquipo  INT          NOT NULL DEFAULT 0,
    AgendadosLucia   INT          NOT NULL DEFAULT 0,
    AsisteCitaLucia  INT          NOT NULL DEFAULT 0,
    CONSTRAINT FK_AsistDia_Evento FOREIGN KEY (IdEvento)
        REFERENCES AsistenciaEvento(IdEvento) ON DELETE CASCADE
);

-- 5) Una torre/etapa dentro de un día (preventas, ventas y opciones)
CREATE TABLE AsistenciaTorre (
    IdTorre        INT          IDENTITY(1,1) PRIMARY KEY,
    IdDia          INT          NOT NULL,
    Torre          NVARCHAR(50) NOT NULL DEFAULT '',
    Orden          INT          NOT NULL DEFAULT 0,
    Preventas      INT          NOT NULL DEFAULT 0,
    ValorPreventa  BIGINT       NOT NULL DEFAULT 0,
    Ventas         INT          NOT NULL DEFAULT 0,
    ValorVenta     BIGINT       NOT NULL DEFAULT 0,
    Opciones       INT          NOT NULL DEFAULT 0,
    ValorOpciones  BIGINT       NOT NULL DEFAULT 0,
    CONSTRAINT FK_AsistTorre_Dia FOREIGN KEY (IdDia)
        REFERENCES AsistenciaDia(IdDia) ON DELETE CASCADE
);

PRINT 'Tablas AsistenciaEvento / AsistenciaDia / AsistenciaTorre creadas correctamente.';
