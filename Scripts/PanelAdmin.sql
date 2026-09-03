-- ============================================================================
-- Panel de administrador — trazabilidad, anulación de ventas y control operativo
--
-- Agrega:
--   1) Columnas de anulación en Ventas
--   2) Tabla Auditoria (registro consultable de acciones)
--   3) Tabla HistorialListas (trazabilidad de precios)
--   4) HorasVigenciaReserva en Proyectos (vencimiento de reservas)
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
-- 2) Auditoría consultable
--    Hasta ahora los eventos solo iban al log del servidor (ILogger), que en
--    App Service rota y se pierde. Esta tabla los deja consultables desde la
--    plataforma, que es lo que exige una auditoría de cumplimiento.
-- ────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID('Auditoria', 'U') IS NULL
BEGIN
    CREATE TABLE Auditoria (
        IdAuditoria  BIGINT        IDENTITY(1,1) PRIMARY KEY,
        Fecha        DATETIME      NOT NULL DEFAULT GETUTCDATE(),  -- siempre UTC
        IdUsuario    INT           NULL,                            -- NULL = anónimo (p. ej. login fallido)
        Usuario      NVARCHAR(150) NOT NULL DEFAULT '',             -- nombre legible, congelado al momento
        Rol          NVARCHAR(50)  NOT NULL DEFAULT '',
        Accion       NVARCHAR(60)  NOT NULL,                        -- LOGIN, VENTA_ANULADA, LISTA_CAMBIADA...
        Entidad      NVARCHAR(60)  NOT NULL DEFAULT '',             -- Inmueble, Venta, Usuario...
        IdEntidad    INT           NULL,
        IdProyecto   INT           NULL,
        Detalle      NVARCHAR(1000) NOT NULL DEFAULT '',
        Ip           NVARCHAR(60)  NOT NULL DEFAULT ''
    );

    CREATE INDEX IX_Auditoria_Fecha     ON Auditoria (Fecha DESC);
    CREATE INDEX IX_Auditoria_Proyecto  ON Auditoria (IdProyecto, Fecha DESC);
    CREATE INDEX IX_Auditoria_Accion    ON Auditoria (Accion, Fecha DESC);
END
GO

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

PRINT 'Panel de administrador: migración aplicada correctamente.';
GO
