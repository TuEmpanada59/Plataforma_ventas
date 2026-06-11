-- Script para crear la tabla de Cuadro de asistencia
-- Ejecutar una sola vez en la base de datos Lanzamientos

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Asistencias' AND xtype='U')
BEGIN
    CREATE TABLE Asistencias (
        IdAsistencia      INT           IDENTITY(1,1) PRIMARY KEY,
        IdProyecto        INT           NOT NULL,
        Nombre            NVARCHAR(100) NOT NULL,
        Apellido          NVARCHAR(100) NOT NULL DEFAULT '',
        Documento         NVARCHAR(20)  NOT NULL DEFAULT '',
        Celular           NVARCHAR(20)  NOT NULL DEFAULT '',
        Correo            NVARCHAR(100) NOT NULL DEFAULT '',
        MetrosInteres     NVARCHAR(20)  NOT NULL DEFAULT '',
        TipoInteres       NVARCHAR(10)  NOT NULL DEFAULT '',
        IdVendedorAtiende INT           NULL,
        FechaVisita       DATETIME      NOT NULL DEFAULT GETDATE(),
        Estado            NVARCHAR(30)  NOT NULL DEFAULT 'Visitó',
        Observaciones     NVARCHAR(500) NOT NULL DEFAULT ''
    );
    PRINT 'Tabla Asistencias creada correctamente.';
END
ELSE
BEGIN
    PRINT 'La tabla Asistencias ya existe.';
END
