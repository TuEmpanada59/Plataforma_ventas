-- ============================================================================
-- PLATAFORMA DE LANZAMIENTOS — BASE DE DATOS COMPLETA
-- Londoño Gómez S.A.S.
--
-- Un solo archivo que deja la base lista. Se ejecuta sobre una base VACÍA,
-- recién creada en Azure SQL, conectado a ella (revisa el desplegable de la
-- barra de herramientas antes de ejecutar).
--
-- Contiene, en orden:
--   PARTE 1  Esquema: las 15 tablas, claves foráneas y valores por defecto
--   PARTE 2  Migraciones: columnas agregadas después, índices y el catálogo
--            de medios publicitarios
--   PARTE 3  Verificación: no modifica nada, solo informa qué quedó pendiente
--
-- Se puede volver a ejecutar completo sobre una base ya montada: la parte 1
-- avisará que las tablas existen y la parte 2 saltará lo ya aplicado.
--
-- LO QUE ESTE ARCHIVO NO HACE:
--   · No crea el usuario administrador. La contraseña se guarda con BCrypt y el
--     hash no se puede escribir a mano: esa fila se copia desde la otra base.
--     Sin ella nadie puede iniciar sesión, porque no hay pantalla de registro.
--   · No carga proyectos ni inmuebles. Eso entra por el Excel desde la
--     plataforma.
--
-- CODIFICACIÓN: este archivo está en UTF-8 con BOM porque la columna
-- Usuarios.Contraseña lleva eñe. Si se vuelve a guardar en otra codificación,
-- esa columna queda creada con el nombre corrupto y el inicio de sesión falla
-- sin decir por qué. La parte 3 lo comprueba.
-- ============================================================================
PRINT '>>> PARTE 1 de 3: esquema base';
GO

-- ============================================================================
-- ESQUEMA BASE DE LA PLATAFORMA
--
-- Generado desde la base de pruebas con el asistente "Generar scripts" de SSMS,
-- solo esquema y sin datos. Es el punto de partida para montar una base nueva.
--
-- NO incluye los índices no agrupados: el asistente los omite. Los crea la
-- sección 16 de PanelAdmin.sql, que se ejecuta después.
--
-- Orden de montaje:  este archivo -> PanelAdmin.sql -> Asistencias.sql -> VerificarBase.sql
--
-- Ojo con la codificación: la columna Usuarios.Contraseña lleva eñe. Este archivo
-- está en UTF-8 con BOM. Si se vuelve a guardar en otra codificación, esa columna
-- puede quedar creada con el nombre corrupto y el inicio de sesión falla.
-- ============================================================================

/****** Object:  Table [dbo].[AjustesPrecio]    Script Date: 21/09/2026 11:46:21 a. m. ******/

SET ANSI_NULLS ON

GO

SET QUOTED_IDENTIFIER ON

GO

CREATE TABLE [dbo].[AjustesPrecio](

	[IdAjuste] [bigint] IDENTITY(1,1) NOT NULL,

	[IdProyecto] [int] NOT NULL,

	[Torre] [nvarchar](50) NOT NULL,

	[Metros] [nvarchar](50) NOT NULL,

	[Listas] [nvarchar](20) NOT NULL,

	[Tipo] [nvarchar](12) NOT NULL,

	[Valor] [decimal](18, 4) NOT NULL,

	[Unidades] [int] NOT NULL,

	[IdUsuario] [int] NULL,

	[Usuario] [nvarchar](150) NOT NULL,

	[Fecha] [datetime] NOT NULL,

	[Revertido] [bit] NOT NULL,

	[FechaReversion] [datetime] NULL,

PRIMARY KEY CLUSTERED 

(

	[IdAjuste] ASC

)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]

) ON [PRIMARY]

GO

/****** Object:  Table [dbo].[AjustesPrecioDetalle]    Script Date: 21/09/2026 11:46:21 a. m. ******/

SET ANSI_NULLS ON

GO

SET QUOTED_IDENTIFIER ON

GO

CREATE TABLE [dbo].[AjustesPrecioDetalle](

	[IdDetalle] [bigint] IDENTITY(1,1) NOT NULL,

	[IdAjuste] [bigint] NOT NULL,

	[IdInmueble] [int] NOT NULL,

	[NumLista] [int] NOT NULL,

	[PrecioAnterior] [bigint] NOT NULL,

	[PrecioNuevo] [bigint] NOT NULL,

PRIMARY KEY CLUSTERED 

(

	[IdDetalle] ASC

)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]

) ON [PRIMARY]

GO

/****** Object:  Table [dbo].[AsistenciaDia]    Script Date: 21/09/2026 11:46:21 a. m. ******/

SET ANSI_NULLS ON

GO

SET QUOTED_IDENTIFIER ON

GO

CREATE TABLE [dbo].[AsistenciaDia](

	[IdDia] [int] IDENTITY(1,1) NOT NULL,

	[IdEvento] [int] NOT NULL,

	[Fecha] [date] NULL,

	[NombreDia] [nvarchar](50) NOT NULL,

	[Orden] [int] NOT NULL,

	[Familias] [int] NOT NULL,

	[Adultos] [int] NOT NULL,

	[Ninos] [int] NOT NULL,

	[Mascotas] [int] NOT NULL,

	[AsisteCita] [int] NOT NULL,

	[Carros] [int] NOT NULL,

	[Motos] [int] NOT NULL,

	[Caminando] [int] NOT NULL,

	[AgendadosEquipo] [int] NOT NULL,

	[AgendadosLucia] [int] NOT NULL,

	[AsisteCitaLucia] [int] NOT NULL,

PRIMARY KEY CLUSTERED 

(

	[IdDia] ASC

)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]

) ON [PRIMARY]

GO

/****** Object:  Table [dbo].[AsistenciaEvento]    Script Date: 21/09/2026 11:46:21 a. m. ******/

SET ANSI_NULLS ON

GO

SET QUOTED_IDENTIFIER ON

GO

CREATE TABLE [dbo].[AsistenciaEvento](

	[IdEvento] [int] IDENTITY(1,1) NOT NULL,

	[IdProyecto] [int] NOT NULL,

	[Titulo] [nvarchar](200) NOT NULL,

	[Observaciones] [nvarchar](max) NOT NULL,

	[FechaCreacion] [datetime] NOT NULL,

PRIMARY KEY CLUSTERED 

(

	[IdEvento] ASC

)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]

) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO

/****** Object:  Table [dbo].[AsistenciaFranja]    Script Date: 21/09/2026 11:46:21 a. m. ******/

SET ANSI_NULLS ON

GO

SET QUOTED_IDENTIFIER ON

GO

CREATE TABLE [dbo].[AsistenciaFranja](

	[IdFranja] [int] IDENTITY(1,1) NOT NULL,

	[IdDia] [int] NOT NULL,

	[Orden] [int] NOT NULL,

	[HoraDesde] [int] NOT NULL,

	[HoraHasta] [int] NOT NULL,

	[Familias] [int] NOT NULL,

PRIMARY KEY CLUSTERED 

(

	[IdFranja] ASC

)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]

) ON [PRIMARY]

GO

/****** Object:  Table [dbo].[AsistenciaTorre]    Script Date: 21/09/2026 11:46:21 a. m. ******/

SET ANSI_NULLS ON

GO

SET QUOTED_IDENTIFIER ON

GO

CREATE TABLE [dbo].[AsistenciaTorre](

	[IdTorre] [int] IDENTITY(1,1) NOT NULL,

	[IdDia] [int] NOT NULL,

	[Torre] [nvarchar](50) NOT NULL,

	[Orden] [int] NOT NULL,

	[Preventas] [int] NOT NULL,

	[ValorPreventa] [bigint] NOT NULL,

	[Ventas] [int] NOT NULL,

	[ValorVenta] [bigint] NOT NULL,

	[Opciones] [int] NOT NULL,

	[ValorOpciones] [bigint] NOT NULL,

PRIMARY KEY CLUSTERED 

(

	[IdTorre] ASC

)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]

) ON [PRIMARY]

GO

/****** Object:  Table [dbo].[Clientes]    Script Date: 21/09/2026 11:46:21 a. m. ******/

SET ANSI_NULLS ON

GO

SET QUOTED_IDENTIFIER ON

GO

CREATE TABLE [dbo].[Clientes](

	[IdCliente] [int] IDENTITY(1,1) NOT NULL,

	[Nombre] [nvarchar](100) NOT NULL,

	[Apellido] [nvarchar](100) NOT NULL,

	[Documento] [nvarchar](20) NULL,

	[Celular] [nvarchar](20) NULL,

	[Correo] [nvarchar](150) NULL,

	[Direccion] [nvarchar](200) NULL,

	[IdProyecto] [int] NULL,

	[MedioPublicitario] [nvarchar](80) NULL,

 CONSTRAINT [PK__Clientes__D5946642159E5EF2] PRIMARY KEY CLUSTERED 

(

	[IdCliente] ASC

)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]

) ON [PRIMARY]

GO

/****** Object:  Table [dbo].[HistorialListas]    Script Date: 21/09/2026 11:46:21 a. m. ******/

SET ANSI_NULLS ON

GO

SET QUOTED_IDENTIFIER ON

GO

CREATE TABLE [dbo].[HistorialListas](

	[IdHistorial] [bigint] IDENTITY(1,1) NOT NULL,

	[IdProyecto] [int] NOT NULL,

	[Metros] [nvarchar](50) NOT NULL,

	[ListaAnterior] [int] NOT NULL,

	[ListaNueva] [int] NOT NULL,

	[Motivo] [nvarchar](30) NOT NULL,

	[IdUsuario] [int] NULL,

	[Usuario] [nvarchar](150) NOT NULL,

	[Fecha] [datetime] NOT NULL,

PRIMARY KEY CLUSTERED 

(

	[IdHistorial] ASC

)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]

) ON [PRIMARY]

GO

/****** Object:  Table [dbo].[Inmuebles]    Script Date: 21/09/2026 11:46:21 a. m. ******/

SET ANSI_NULLS ON

GO

SET QUOTED_IDENTIFIER ON

GO

CREATE TABLE [dbo].[Inmuebles](

	[IdInmuebles] [int] IDENTITY(1,1) NOT NULL,

	[IdProyecto] [int] NOT NULL,

	[Apto] [nvarchar](20) NOT NULL,

	[Tipo] [nvarchar](50) NULL,

	[Piso] [nvarchar](10) NULL,

	[Metros] [nvarchar](20) NULL,

	[Lista1] [nvarchar](30) NULL,

	[Lista2] [nvarchar](30) NULL,

	[Lista3] [nvarchar](30) NULL,

	[Lista4] [nvarchar](30) NULL,

	[Lista5] [nvarchar](30) NULL,

	[Estado] [nvarchar](20) NULL,

	[Torre] [nvarchar](50) NULL,

	[IdVendedorEnProceso] [int] NULL,

	[FechaEnProceso] [datetime] NULL,

	[IdVendedorReserva] [int] NULL,

	[PrecioReserva] [bigint] NULL,

	[FechaReserva] [datetime] NULL,

	[ObservacionReserva] [nvarchar](500) NULL,

	[Etapa] [nvarchar](60) NOT NULL,

	[EnLanzamiento] [bit] NOT NULL,

 CONSTRAINT [PK__Inmueble__6B13429C9F46F935] PRIMARY KEY CLUSTERED 

(

	[IdInmuebles] ASC

)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]

) ON [PRIMARY]

GO

/****** Object:  Table [dbo].[MediosPublicitarios]    Script Date: 21/09/2026 11:46:21 a. m. ******/

SET ANSI_NULLS ON

GO

SET QUOTED_IDENTIFIER ON

GO

CREATE TABLE [dbo].[MediosPublicitarios](

	[IdMedio] [int] IDENTITY(1,1) NOT NULL,

	[Nombre] [nvarchar](120) NOT NULL,

PRIMARY KEY CLUSTERED 

(

	[IdMedio] ASC

)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]

) ON [PRIMARY]

GO

/****** Object:  Table [dbo].[ProyectoActividades]    Script Date: 21/09/2026 11:46:21 a. m. ******/

SET ANSI_NULLS ON

GO

SET QUOTED_IDENTIFIER ON

GO

CREATE TABLE [dbo].[ProyectoActividades](

	[IdActividad] [int] IDENTITY(1,1) NOT NULL,

	[IdProyecto] [int] NOT NULL,

	[Tipo] [nvarchar](30) NOT NULL,

	[Etapa] [nvarchar](100) NOT NULL,

	[FechaInicio] [datetime] NOT NULL,

	[TotalProyecto] [int] NOT NULL,

	[TotalLanzamiento] [int] NOT NULL,

	[HistoricoVendidas] [int] NOT NULL,

	[HistoricoReservadas] [int] NOT NULL,

PRIMARY KEY CLUSTERED 

(

	[IdActividad] ASC

)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]

) ON [PRIMARY]

GO

/****** Object:  Table [dbo].[ProyectoAreaListas]    Script Date: 21/09/2026 11:46:21 a. m. ******/

SET ANSI_NULLS ON

GO

SET QUOTED_IDENTIFIER ON

GO

CREATE TABLE [dbo].[ProyectoAreaListas](

	[IdProyecto] [int] NOT NULL,

	[Metros] [varchar](20) NOT NULL,

	[ListaActual] [int] NOT NULL,

	[AptsPorLista] [int] NOT NULL,

 CONSTRAINT [PK_PAL] PRIMARY KEY CLUSTERED 

(

	[IdProyecto] ASC,

	[Metros] ASC

)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]

) ON [PRIMARY]

GO

/****** Object:  Table [dbo].[Proyectos]    Script Date: 21/09/2026 11:46:21 a. m. ******/

SET ANSI_NULLS ON

GO

SET QUOTED_IDENTIFIER ON

GO

CREATE TABLE [dbo].[Proyectos](

	[IdProyectos] [int] IDENTITY(1,1) NOT NULL,

	[Nombre] [nvarchar](150) NOT NULL,

	[ListaActual] [int] NULL,

	[ApartamentosPorLista] [int] NULL,

	[FechaCarga] [datetime] NULL,

	[Activo] [bit] NULL,

	[CodigoAcceso] [varchar](20) NULL,

	[IdAdminCreador] [int] NULL,

	[IdProyecto] [int] NULL,

	[ModoLista] [varchar](10) NULL,

	[TipProyecto] [varchar](20) NOT NULL,

	[HorasVigenciaReserva] [int] NOT NULL,

	[Actividad] [nvarchar](30) NOT NULL,

	[EtapaLanzamiento] [nvarchar](100) NOT NULL,

 CONSTRAINT [PK__Proyecto__E6EE063CD54B19B0] PRIMARY KEY CLUSTERED 

(

	[IdProyectos] ASC

)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]

) ON [PRIMARY]

GO

/****** Object:  Table [dbo].[Usuarios]    Script Date: 21/09/2026 11:46:21 a. m. ******/

SET ANSI_NULLS ON

GO

SET QUOTED_IDENTIFIER ON

GO

CREATE TABLE [dbo].[Usuarios](

	[IdUsuario] [int] IDENTITY(1,1) NOT NULL,

	[Nombre] [varchar](100) NOT NULL,

	[Apellido] [varchar](100) NOT NULL,

	[Documento] [varchar](20) NOT NULL,

	[Celular] [varchar](20) NOT NULL,

	[Correo] [varchar](150) NOT NULL,

	[Usuario] [varchar](100) NOT NULL,

	[Contraseña] [nvarchar](255) NOT NULL,

	[Rol] [varchar](50) NOT NULL,

	[IdProyecto] [int] NULL,

	[IdSuperior] [int] NULL,

 CONSTRAINT [PK__Usuarios__5B65BF97FDC3EDB6] PRIMARY KEY CLUSTERED 

(

	[IdUsuario] ASC

)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],

 CONSTRAINT [UQ__Usuarios__E3237CF79E6224E6] UNIQUE NONCLUSTERED 

(

	[Usuario] ASC

)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]

) ON [PRIMARY]

GO

/****** Object:  Table [dbo].[Ventas]    Script Date: 21/09/2026 11:46:21 a. m. ******/

SET ANSI_NULLS ON

GO

SET QUOTED_IDENTIFIER ON

GO

CREATE TABLE [dbo].[Ventas](

	[IdVenta] [int] IDENTITY(1,1) NOT NULL,

	[IdInmueble] [int] NOT NULL,

	[IdCliente] [int] NOT NULL,

	[IdUsuario] [int] NOT NULL,

	[IdProyecto] [int] NOT NULL,

	[ListaAplicada] [int] NULL,

	[PrecioVenta] [bigint] NULL,

	[FechaVenta] [datetime] NULL,

	[Estado] [nvarchar](20) NULL,

	[Destino] [varchar](50) NULL,

	[MotivoAnulacion] [nvarchar](500) NULL,

	[FechaAnulacion] [datetime] NULL,

	[IdUsuarioAnula] [int] NULL,

	[Observaciones] [nvarchar](1000) NULL,

	[Origen] [nvarchar](20) NOT NULL,

 CONSTRAINT [PK__Ventas__BC1240BDDC3B0D89] PRIMARY KEY CLUSTERED 

(

	[IdVenta] ASC

)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]

) ON [PRIMARY]

GO

ALTER TABLE [dbo].[AjustesPrecio] ADD  DEFAULT ('') FOR [Torre]

GO

ALTER TABLE [dbo].[AjustesPrecio] ADD  DEFAULT ('') FOR [Metros]

GO

ALTER TABLE [dbo].[AjustesPrecio] ADD  DEFAULT ('') FOR [Listas]

GO

ALTER TABLE [dbo].[AjustesPrecio] ADD  DEFAULT ((0)) FOR [Unidades]

GO

ALTER TABLE [dbo].[AjustesPrecio] ADD  DEFAULT ('') FOR [Usuario]

GO

ALTER TABLE [dbo].[AjustesPrecio] ADD  DEFAULT (getutcdate()) FOR [Fecha]

GO

ALTER TABLE [dbo].[AjustesPrecio] ADD  DEFAULT ((0)) FOR [Revertido]

GO

ALTER TABLE [dbo].[AsistenciaDia] ADD  DEFAULT ('') FOR [NombreDia]

GO

ALTER TABLE [dbo].[AsistenciaDia] ADD  DEFAULT ((0)) FOR [Orden]

GO

ALTER TABLE [dbo].[AsistenciaDia] ADD  DEFAULT ((0)) FOR [Familias]

GO

ALTER TABLE [dbo].[AsistenciaDia] ADD  DEFAULT ((0)) FOR [Adultos]

GO

ALTER TABLE [dbo].[AsistenciaDia] ADD  DEFAULT ((0)) FOR [Ninos]

GO

ALTER TABLE [dbo].[AsistenciaDia] ADD  DEFAULT ((0)) FOR [Mascotas]

GO

ALTER TABLE [dbo].[AsistenciaDia] ADD  DEFAULT ((0)) FOR [AsisteCita]

GO

ALTER TABLE [dbo].[AsistenciaDia] ADD  DEFAULT ((0)) FOR [Carros]

GO

ALTER TABLE [dbo].[AsistenciaDia] ADD  DEFAULT ((0)) FOR [Motos]

GO

ALTER TABLE [dbo].[AsistenciaDia] ADD  DEFAULT ((0)) FOR [Caminando]

GO

ALTER TABLE [dbo].[AsistenciaDia] ADD  DEFAULT ((0)) FOR [AgendadosEquipo]

GO

ALTER TABLE [dbo].[AsistenciaDia] ADD  DEFAULT ((0)) FOR [AgendadosLucia]

GO

ALTER TABLE [dbo].[AsistenciaDia] ADD  DEFAULT ((0)) FOR [AsisteCitaLucia]

GO

ALTER TABLE [dbo].[AsistenciaEvento] ADD  DEFAULT ('') FOR [Titulo]

GO

ALTER TABLE [dbo].[AsistenciaEvento] ADD  DEFAULT ('') FOR [Observaciones]

GO

ALTER TABLE [dbo].[AsistenciaEvento] ADD  DEFAULT (getdate()) FOR [FechaCreacion]

GO

ALTER TABLE [dbo].[AsistenciaFranja] ADD  DEFAULT ((0)) FOR [Orden]

GO

ALTER TABLE [dbo].[AsistenciaFranja] ADD  DEFAULT ((0)) FOR [Familias]

GO

ALTER TABLE [dbo].[AsistenciaTorre] ADD  DEFAULT ('') FOR [Torre]

GO

ALTER TABLE [dbo].[AsistenciaTorre] ADD  DEFAULT ((0)) FOR [Orden]

GO

ALTER TABLE [dbo].[AsistenciaTorre] ADD  DEFAULT ((0)) FOR [Preventas]

GO

ALTER TABLE [dbo].[AsistenciaTorre] ADD  DEFAULT ((0)) FOR [ValorPreventa]

GO

ALTER TABLE [dbo].[AsistenciaTorre] ADD  DEFAULT ((0)) FOR [Ventas]

GO

ALTER TABLE [dbo].[AsistenciaTorre] ADD  DEFAULT ((0)) FOR [ValorVenta]

GO

ALTER TABLE [dbo].[AsistenciaTorre] ADD  DEFAULT ((0)) FOR [Opciones]

GO

ALTER TABLE [dbo].[AsistenciaTorre] ADD  DEFAULT ((0)) FOR [ValorOpciones]

GO

ALTER TABLE [dbo].[HistorialListas] ADD  DEFAULT ('') FOR [Metros]

GO

ALTER TABLE [dbo].[HistorialListas] ADD  DEFAULT ('') FOR [Usuario]

GO

ALTER TABLE [dbo].[HistorialListas] ADD  DEFAULT (getutcdate()) FOR [Fecha]

GO

ALTER TABLE [dbo].[Inmuebles] ADD  CONSTRAINT [DF__Inmuebles__Estad__7A672E12]  DEFAULT ('DISPONIBLE') FOR [Estado]

GO

ALTER TABLE [dbo].[Inmuebles] ADD  DEFAULT ('') FOR [Etapa]

GO

ALTER TABLE [dbo].[Inmuebles] ADD  CONSTRAINT [DF_Inmuebles_EnLanz]  DEFAULT ((1)) FOR [EnLanzamiento]

GO

ALTER TABLE [dbo].[ProyectoActividades] ADD  DEFAULT ('PROYECTO_NUEVO') FOR [Tipo]

GO

ALTER TABLE [dbo].[ProyectoActividades] ADD  DEFAULT ('') FOR [Etapa]

GO

ALTER TABLE [dbo].[ProyectoActividades] ADD  DEFAULT (getdate()) FOR [FechaInicio]

GO

ALTER TABLE [dbo].[ProyectoActividades] ADD  DEFAULT ((0)) FOR [TotalProyecto]

GO

ALTER TABLE [dbo].[ProyectoActividades] ADD  DEFAULT ((0)) FOR [TotalLanzamiento]

GO

ALTER TABLE [dbo].[ProyectoActividades] ADD  DEFAULT ((0)) FOR [HistoricoVendidas]

GO

ALTER TABLE [dbo].[ProyectoActividades] ADD  DEFAULT ((0)) FOR [HistoricoReservadas]

GO

ALTER TABLE [dbo].[ProyectoAreaListas] ADD  CONSTRAINT [DF__ProyectoA__Lista__756D6ECB]  DEFAULT ((1)) FOR [ListaActual]

GO

ALTER TABLE [dbo].[ProyectoAreaListas] ADD  CONSTRAINT [DF__ProyectoA__AptsP__0697FACD]  DEFAULT ((0)) FOR [AptsPorLista]

GO

ALTER TABLE [dbo].[Proyectos] ADD  CONSTRAINT [DF__Proyectos__Lista__6FE99F9F]  DEFAULT ((1)) FOR [ListaActual]

GO

ALTER TABLE [dbo].[Proyectos] ADD  CONSTRAINT [DF__Proyectos__Apart__70DDC3D8]  DEFAULT ((1)) FOR [ApartamentosPorLista]

GO

ALTER TABLE [dbo].[Proyectos] ADD  CONSTRAINT [DF__Proyectos__Fecha__71D1E811]  DEFAULT (getdate()) FOR [FechaCarga]

GO

ALTER TABLE [dbo].[Proyectos] ADD  CONSTRAINT [DF__Proyectos__Activ__72C60C4A]  DEFAULT ((1)) FOR [Activo]

GO

ALTER TABLE [dbo].[Proyectos] ADD  CONSTRAINT [DF__Proyectos__ModoL__4D5F7D71]  DEFAULT ('MANUAL') FOR [ModoLista]

GO

ALTER TABLE [dbo].[Proyectos] ADD  CONSTRAINT [DF__Proyectos__TipPr__607251E5]  DEFAULT ('APARTAMENTOS') FOR [TipProyecto]

GO

ALTER TABLE [dbo].[Proyectos] ADD  DEFAULT ((0)) FOR [HorasVigenciaReserva]

GO

ALTER TABLE [dbo].[Proyectos] ADD  CONSTRAINT [DF_Proyectos_Actividad]  DEFAULT ('PROYECTO_NUEVO') FOR [Actividad]

GO

ALTER TABLE [dbo].[Proyectos] ADD  CONSTRAINT [DF_Proyectos_EtapaLanz]  DEFAULT ('') FOR [EtapaLanzamiento]

GO

ALTER TABLE [dbo].[Usuarios] ADD  CONSTRAINT [DF__Usuarios__Rol__5EBF139D]  DEFAULT ('Vendedor') FOR [Rol]

GO

ALTER TABLE [dbo].[Ventas] ADD  CONSTRAINT [DF__Ventas__FechaVen__0A9D95DB]  DEFAULT (getdate()) FOR [FechaVenta]

GO

ALTER TABLE [dbo].[Ventas] ADD  CONSTRAINT [DF__Ventas__Estado__0B91BA14]  DEFAULT ('ACTIVA') FOR [Estado]

GO

ALTER TABLE [dbo].[Ventas] ADD  DEFAULT ('PLATAFORMA') FOR [Origen]

GO

ALTER TABLE [dbo].[AjustesPrecioDetalle]  WITH CHECK ADD  CONSTRAINT [FK_AjusteDet_Ajuste] FOREIGN KEY([IdAjuste])

REFERENCES [dbo].[AjustesPrecio] ([IdAjuste])

ON DELETE CASCADE

GO

ALTER TABLE [dbo].[AjustesPrecioDetalle] CHECK CONSTRAINT [FK_AjusteDet_Ajuste]

GO

ALTER TABLE [dbo].[AsistenciaDia]  WITH CHECK ADD  CONSTRAINT [FK_AsistDia_Evento] FOREIGN KEY([IdEvento])

REFERENCES [dbo].[AsistenciaEvento] ([IdEvento])

ON DELETE CASCADE

GO

ALTER TABLE [dbo].[AsistenciaDia] CHECK CONSTRAINT [FK_AsistDia_Evento]

GO

ALTER TABLE [dbo].[AsistenciaFranja]  WITH CHECK ADD  CONSTRAINT [FK_AsistFranja_Dia] FOREIGN KEY([IdDia])

REFERENCES [dbo].[AsistenciaDia] ([IdDia])

ON DELETE CASCADE

GO

ALTER TABLE [dbo].[AsistenciaFranja] CHECK CONSTRAINT [FK_AsistFranja_Dia]

GO

ALTER TABLE [dbo].[AsistenciaTorre]  WITH CHECK ADD  CONSTRAINT [FK_AsistTorre_Dia] FOREIGN KEY([IdDia])

REFERENCES [dbo].[AsistenciaDia] ([IdDia])

ON DELETE CASCADE

GO

ALTER TABLE [dbo].[AsistenciaTorre] CHECK CONSTRAINT [FK_AsistTorre_Dia]

GO

ALTER TABLE [dbo].[Clientes]  WITH CHECK ADD  CONSTRAINT [FK_Clientes] FOREIGN KEY([IdProyecto])

REFERENCES [dbo].[Proyectos] ([IdProyectos])

GO

ALTER TABLE [dbo].[Clientes] CHECK CONSTRAINT [FK_Clientes]

GO

ALTER TABLE [dbo].[Inmuebles]  WITH CHECK ADD  CONSTRAINT [FK__Inmuebles__IdPro__797309D9] FOREIGN KEY([IdProyecto])

REFERENCES [dbo].[Proyectos] ([IdProyectos])

GO

ALTER TABLE [dbo].[Inmuebles] CHECK CONSTRAINT [FK__Inmuebles__IdPro__797309D9]

GO

ALTER TABLE [dbo].[Inmuebles]  WITH CHECK ADD  CONSTRAINT [FK__Inmuebles__IdVen__7B5B524B] FOREIGN KEY([IdVendedorEnProceso])

REFERENCES [dbo].[Usuarios] ([IdUsuario])

GO

ALTER TABLE [dbo].[Inmuebles] CHECK CONSTRAINT [FK__Inmuebles__IdVen__7B5B524B]

GO

ALTER TABLE [dbo].[Proyectos]  WITH CHECK ADD  CONSTRAINT [FK__Proyectos__IdPro__208CD6FA] FOREIGN KEY([IdProyecto])

REFERENCES [dbo].[Proyectos] ([IdProyectos])

GO

ALTER TABLE [dbo].[Proyectos] CHECK CONSTRAINT [FK__Proyectos__IdPro__208CD6FA]

GO

ALTER TABLE [dbo].[Usuarios]  WITH CHECK ADD  CONSTRAINT [FK__Usuarios__IdProy__2739D489] FOREIGN KEY([IdProyecto])

REFERENCES [dbo].[Proyectos] ([IdProyectos])

GO

ALTER TABLE [dbo].[Usuarios] CHECK CONSTRAINT [FK__Usuarios__IdProy__2739D489]

GO

ALTER TABLE [dbo].[Usuarios]  WITH CHECK ADD  CONSTRAINT [FK_ADMIN] FOREIGN KEY([IdSuperior])

REFERENCES [dbo].[Usuarios] ([IdUsuario])

GO

ALTER TABLE [dbo].[Usuarios] CHECK CONSTRAINT [FK_ADMIN]

GO

ALTER TABLE [dbo].[Ventas]  WITH CHECK ADD  CONSTRAINT [FK__Ventas__IdClient__07C12930] FOREIGN KEY([IdCliente])

REFERENCES [dbo].[Clientes] ([IdCliente])

GO

ALTER TABLE [dbo].[Ventas] CHECK CONSTRAINT [FK__Ventas__IdClient__07C12930]

GO

ALTER TABLE [dbo].[Ventas]  WITH CHECK ADD  CONSTRAINT [FK__Ventas__IdInmueb__06CD04F7] FOREIGN KEY([IdInmueble])

REFERENCES [dbo].[Inmuebles] ([IdInmuebles])

GO

ALTER TABLE [dbo].[Ventas] CHECK CONSTRAINT [FK__Ventas__IdInmueb__06CD04F7]

GO

ALTER TABLE [dbo].[Ventas]  WITH CHECK ADD  CONSTRAINT [FK__Ventas__IdProyec__09A971A2] FOREIGN KEY([IdProyecto])

REFERENCES [dbo].[Proyectos] ([IdProyectos])

GO

ALTER TABLE [dbo].[Ventas] CHECK CONSTRAINT [FK__Ventas__IdProyec__09A971A2]

GO

ALTER TABLE [dbo].[Ventas]  WITH CHECK ADD  CONSTRAINT [FK__Ventas__IdUsuari__08B54D69] FOREIGN KEY([IdUsuario])

REFERENCES [dbo].[Usuarios] ([IdUsuario])

GO

ALTER TABLE [dbo].[Ventas] CHECK CONSTRAINT [FK__Ventas__IdUsuari__08B54D69]

GO


PRINT '>>> PARTE 2 de 3: migraciones, índices y catálogo de medios';
GO

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

PRINT 'Panel de administrador: migración aplicada correctamente.';
GO

PRINT '>>> PARTE 3 de 3: verificación';
GO

-- ============================================================================
-- VERIFICACIÓN DE LA BASE DE DATOS
--
-- Se ejecuta sobre una base recién creada (o sobre una existente) y dice qué le
-- falta para que la plataforma funcione. No modifica nada: solo consulta.
--
-- Sirve para el paso previo a un lanzamiento: si aquí sale algo en FALTA, la
-- aplicación va a fallar en producción con la sala de ventas llena.
--
-- Orden correcto de montaje de una base nueva:
--   1) Script de esquema generado desde la base de pruebas (solo esquema)
--   2) Scripts/PanelAdmin.sql   (completo, es idempotente)
--   3) Scripts/Asistencias.sql
--   4) Este archivo, para confirmar
-- ============================================================================

SET NOCOUNT ON;

PRINT '=== TABLAS ===';

;WITH Esperadas AS (
    SELECT * FROM (VALUES
        -- Núcleo de la plataforma: sin estas no arranca nada
        ('Usuarios',            'Núcleo'),
        ('Proyectos',           'Núcleo'),
        ('Inmuebles',           'Núcleo'),
        ('Ventas',              'Núcleo'),
        ('Clientes',            'Núcleo'),
        ('ProyectoAreaListas',  'Núcleo'),
        -- Agregadas por PanelAdmin.sql
        ('HistorialListas',     'PanelAdmin.sql'),
        ('AjustesPrecio',       'PanelAdmin.sql'),
        ('AjustesPrecioDetalle','PanelAdmin.sql'),
        ('MediosPublicitarios', 'PanelAdmin.sql'),
        ('ProyectoActividades', 'PanelAdmin.sql'),
        ('AsistenciaFranja',    'PanelAdmin.sql'),
        -- Agregadas por Asistencias.sql
        ('AsistenciaEvento',    'Asistencias.sql'),
        ('AsistenciaDia',       'Asistencias.sql'),
        ('AsistenciaTorre',     'Asistencias.sql')
    ) AS t(Tabla, Origen)
)
SELECT  e.Tabla,
        e.Origen,
        CASE WHEN OBJECT_ID(e.Tabla, 'U') IS NULL THEN '>>> FALTA' ELSE 'ok' END AS Estado
FROM    Esperadas e
ORDER BY CASE WHEN OBJECT_ID(e.Tabla, 'U') IS NULL THEN 0 ELSE 1 END, e.Origen, e.Tabla;

PRINT '=== COLUMNAS AGREGADAS POR MIGRACIONES ===';

;WITH Columnas AS (
    SELECT * FROM (VALUES
        ('Ventas',    'MotivoAnulacion',      'Anulación de ventas'),
        ('Ventas',    'FechaAnulacion',       'Anulación de ventas'),
        ('Ventas',    'IdUsuarioAnula',       'Anulación de ventas'),
        ('Ventas',    'Observaciones',        'Observaciones de la venta'),
        ('Ventas',    'Origen',               'Ventas importadas del Excel'),
        ('Inmuebles', 'Torre',                'Torre por inmueble (viene del esquema base)'),
        ('Inmuebles', 'Etapa',                'Etapas en un mismo proyecto'),
        ('Inmuebles', 'ObservacionReserva',   'Observación de la reserva'),
        ('Inmuebles', 'EnLanzamiento',        'Total del lanzamiento'),
        ('Proyectos', 'Actividad',            'Actividad comercial'),
        ('Proyectos', 'EtapaLanzamiento',     'Actividad comercial'),
        ('Proyectos', 'HorasVigenciaReserva', 'Vigencia de la reserva'),
        ('Clientes',  'MedioPublicitario',    'Medio por el que se enteró')
    ) AS c(Tabla, Columna, Para)
)
SELECT  c.Tabla + '.' + c.Columna AS Columna,
        c.Para,
        CASE WHEN OBJECT_ID(c.Tabla, 'U') IS NULL THEN '>>> NO EXISTE LA TABLA'
             WHEN COL_LENGTH(c.Tabla, c.Columna) IS NULL THEN '>>> FALTA'
             ELSE 'ok' END AS Estado
FROM    Columnas c
ORDER BY CASE WHEN OBJECT_ID(c.Tabla,'U') IS NULL OR COL_LENGTH(c.Tabla, c.Columna) IS NULL
              THEN 0 ELSE 1 END, c.Tabla, c.Columna;

PRINT '=== ÍNDICES ===';

-- Un script de esquema generado desde otra base suele venir sin los índices no
-- agrupados: el asistente de SSMS los omite salvo que se le pidan. La sección 16
-- de PanelAdmin.sql los crea; esto confirma que quedaron.
;WITH Indices AS (
    SELECT * FROM (VALUES
        ('HistorialListas',     'IX_HistorialListas_Proy'),
        ('AsistenciaFranja',    'IX_AsistFranja_Dia'),
        ('AjustesPrecio',       'IX_AjustesPrecio_Proy'),
        ('AjustesPrecioDetalle','IX_AjusteDet_Ajuste'),
        ('ProyectoActividades', 'IX_ProyActividades_Proy'),
        ('MediosPublicitarios', 'UX_Medios_Nombre')
    ) AS i(Tabla, Indice)
)
SELECT  i.Tabla, i.Indice,
        CASE WHEN OBJECT_ID(i.Tabla,'U') IS NULL THEN '>>> NO EXISTE LA TABLA'
             WHEN NOT EXISTS (SELECT 1 FROM sys.indexes x
                              WHERE x.name = i.Indice AND x.object_id = OBJECT_ID(i.Tabla))
             THEN '>>> FALTA: corre la sección 16 de PanelAdmin.sql'
             ELSE 'ok' END AS Estado
FROM    Indices i
ORDER BY CASE WHEN OBJECT_ID(i.Tabla,'U') IS NULL
              OR NOT EXISTS (SELECT 1 FROM sys.indexes x
                             WHERE x.name = i.Indice AND x.object_id = OBJECT_ID(i.Tabla))
              THEN 0 ELSE 1 END, i.Tabla;

PRINT '=== DATOS MÍNIMOS ===';

-- Los medios publicitarios son lista fija: sin ellos el formulario de venta sale
-- sin opciones y el reporte de "cómo se enteraron" queda vacío.
SELECT  'Medios publicitarios' AS Dato,
        CASE WHEN OBJECT_ID('MediosPublicitarios','U') IS NULL THEN 0
             ELSE (SELECT COUNT(*) FROM MediosPublicitarios) END AS Cantidad,
        CASE WHEN OBJECT_ID('MediosPublicitarios','U') IS NULL THEN '>>> FALTA LA TABLA'
             WHEN (SELECT COUNT(*) FROM MediosPublicitarios) = 0 THEN '>>> VACÍA: corre la sección 14'
             ELSE 'ok' END AS Estado
UNION ALL
-- Sin un administrador no hay forma de entrar: la plataforma no tiene registro.
SELECT  'Administradores',
        (SELECT COUNT(*) FROM Usuarios WHERE Rol IN ('Administrador','SuperAdministrador')),
        CASE WHEN (SELECT COUNT(*) FROM Usuarios WHERE Rol IN ('Administrador','SuperAdministrador')) = 0
             THEN '>>> NO HAY NINGUNO: nadie puede iniciar sesión'
             ELSE 'ok' END
UNION ALL
SELECT  'Proyectos activos',
        (SELECT COUNT(*) FROM Proyectos WHERE Activo = 1),
        'informativo';

-- La columna de la contraseña lleva eñe. Si el script de esquema se guardó con otra
-- codificación, la columna puede haber quedado creada con el nombre corrupto, y la
-- aplicación falla en el login sin decir por qué. Se busca por patrón para no
-- depender de la eñe en este mismo archivo.
SELECT  'Columna de contraseña' AS Comprobacion,
        ISNULL((SELECT TOP 1 name COLLATE DATABASE_DEFAULT FROM sys.columns
                WHERE object_id = OBJECT_ID('Usuarios') AND name LIKE 'Contrase%'), '(ninguna)') AS NombreReal,
        CASE WHEN NOT EXISTS (SELECT 1 FROM sys.columns
                              WHERE object_id = OBJECT_ID('Usuarios') AND name LIKE 'Contrase%')
             THEN '>>> FALTA LA COLUMNA'
             WHEN EXISTS (SELECT 1 FROM sys.columns
                          WHERE object_id = OBJECT_ID('Usuarios') AND name = N'Contraseña')
             THEN 'ok'
             ELSE '>>> NOMBRE CORRUPTO: la eñe no sobrevivió a la codificación' END AS Estado;

PRINT '=== FIN DE LA VERIFICACIÓN ===';
PRINT 'Todo lo que aparezca con >>> hay que resolverlo antes del lanzamiento.';
GO
