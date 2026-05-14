CREATE DATABASE IF NOT EXISTS banrural_crm
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

USE banrural_crm;

SET FOREIGN_KEY_CHECKS = 0;
DROP TABLE IF EXISTS logsAuditoria;
DROP TABLE IF EXISTS kpis;
DROP TABLE IF EXISTS tickets;
DROP TABLE IF EXISTS prestamosEquipos;
DROP TABLE IF EXISTS movimientosInventario;
DROP TABLE IF EXISTS mantenimientos;
DROP TABLE IF EXISTS visitasTecnicas;
DROP TABLE IF EXISTS inventario;
DROP TABLE IF EXISTS bodegas;
DROP TABLE IF EXISTS categorias;
DROP TABLE IF EXISTS agencias;
DROP TABLE IF EXISTS usuarios;
DROP TABLE IF EXISTS roles;
SET FOREIGN_KEY_CHECKS = 1;

CREATE TABLE roles (
    id_rol INT AUTO_INCREMENT PRIMARY KEY,
    nombre_rol VARCHAR(50) NOT NULL,
    descripcion TEXT
) ENGINE=InnoDB;

CREATE TABLE agencias (
    id_agencia INT AUTO_INCREMENT PRIMARY KEY,
    nombre_agencia VARCHAR(100) NOT NULL,
    direccion TEXT NOT NULL,
    region VARCHAR(100) NOT NULL,
    telefono VARCHAR(20)
) ENGINE=InnoDB;

CREATE TABLE categorias (
    id_categoria INT AUTO_INCREMENT PRIMARY KEY,
    nombre_categoria VARCHAR(100) NOT NULL,
    descripcion TEXT
) ENGINE=InnoDB;

CREATE TABLE usuarios (
    id_usuario INT AUTO_INCREMENT PRIMARY KEY,
    nombre VARCHAR(100) NOT NULL,
    apellido VARCHAR(100) NOT NULL,
    correo VARCHAR(150) NOT NULL UNIQUE,
    contrasena VARCHAR(255) NOT NULL,
    telefono VARCHAR(20),
    rol_id INT NOT NULL,
    estado VARCHAR(20) NOT NULL DEFAULT 'activo',
    fecha_creacion TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT chk_usuarios_estado CHECK (estado IN ('activo', 'inactivo')),
    CONSTRAINT fk_usuarios_roles FOREIGN KEY (rol_id) REFERENCES roles(id_rol)
) ENGINE=InnoDB;

CREATE TABLE bodegas (
    id_bodega INT AUTO_INCREMENT PRIMARY KEY,
    nombre_bodega VARCHAR(100) NOT NULL,
    ubicacion TEXT NOT NULL,
    encargado_id INT NOT NULL,
    CONSTRAINT fk_bodegas_usuarios FOREIGN KEY (encargado_id) REFERENCES usuarios(id_usuario)
) ENGINE=InnoDB;

CREATE TABLE inventario (
    id_equipo INT AUTO_INCREMENT PRIMARY KEY,
    serial VARCHAR(100) NOT NULL UNIQUE,
    categoria_id INT NOT NULL,
    marca VARCHAR(100) NOT NULL,
    modelo VARCHAR(100) NOT NULL,
    estado VARCHAR(50) NOT NULL DEFAULT 'Disponible',
    bodega_id INT NOT NULL,
    fecha_compra DATE,
    garantia_fin DATE,
    CONSTRAINT chk_inventario_estado CHECK (estado IN ('Disponible', 'Prestado', 'Devuelto', 'Dañado', 'Baja')),
    CONSTRAINT fk_inventario_categorias FOREIGN KEY (categoria_id) REFERENCES categorias(id_categoria),
    CONSTRAINT fk_inventario_bodegas FOREIGN KEY (bodega_id) REFERENCES bodegas(id_bodega)
) ENGINE=InnoDB;

CREATE TABLE visitasTecnicas (
    id_visita INT AUTO_INCREMENT PRIMARY KEY,
    tecnico_id INT NOT NULL,
    agencia_id INT NOT NULL,
    fecha_visita TIMESTAMP NOT NULL,
    estado VARCHAR(30) NOT NULL DEFAULT 'Pendiente',
    descripcion TEXT,
    fecha_registro TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT chk_visitas_estado CHECK (estado IN ('Pendiente', 'En proceso', 'Completada')),
    CONSTRAINT fk_visitas_usuarios FOREIGN KEY (tecnico_id) REFERENCES usuarios(id_usuario),
    CONSTRAINT fk_visitas_agencias FOREIGN KEY (agencia_id) REFERENCES agencias(id_agencia)
) ENGINE=InnoDB;

CREATE TABLE mantenimientos (
    id_mantenimiento INT AUTO_INCREMENT PRIMARY KEY,
    visita_id INT NOT NULL,
    tipo_mantenimiento VARCHAR(100) NOT NULL,
    descripcion TEXT,
    estado VARCHAR(30) NOT NULL DEFAULT 'Pendiente',
    CONSTRAINT chk_mantenimientos_estado CHECK (estado IN ('Pendiente', 'En proceso', 'Completado')),
    CONSTRAINT fk_mantenimientos_visitas FOREIGN KEY (visita_id) REFERENCES visitasTecnicas(id_visita)
) ENGINE=InnoDB;

CREATE TABLE movimientosInventario (
    id_movimiento INT AUTO_INCREMENT PRIMARY KEY,
    equipo_id INT NOT NULL,
    usuario_id INT NOT NULL,
    tipo_movimiento VARCHAR(50) NOT NULL,
    fecha_movimiento TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    descripcion TEXT,
    CONSTRAINT chk_movimientos_tipo CHECK (tipo_movimiento IN ('Entrada', 'Salida', 'Transferencia')),
    CONSTRAINT fk_movimientos_inventario FOREIGN KEY (equipo_id) REFERENCES inventario(id_equipo),
    CONSTRAINT fk_movimientos_usuarios FOREIGN KEY (usuario_id) REFERENCES usuarios(id_usuario)
) ENGINE=InnoDB;

CREATE TABLE prestamosEquipos (
    id_prestamo INT AUTO_INCREMENT PRIMARY KEY,
    equipo_id INT NOT NULL,
    usuario_id INT NOT NULL,
    fecha_salida TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    fecha_retorno TIMESTAMP NULL,
    estado VARCHAR(30) NOT NULL DEFAULT 'Prestado',
    CONSTRAINT chk_prestamos_estado CHECK (estado IN ('Prestado', 'Devuelto', 'Vencido')),
    CONSTRAINT fk_prestamos_inventario FOREIGN KEY (equipo_id) REFERENCES inventario(id_equipo),
    CONSTRAINT fk_prestamos_usuarios FOREIGN KEY (usuario_id) REFERENCES usuarios(id_usuario)
) ENGINE=InnoDB;

CREATE TABLE tickets (
    id_ticket INT AUTO_INCREMENT PRIMARY KEY,
    usuario_id INT NOT NULL,
    tecnico_id INT NULL,
    titulo VARCHAR(150) NOT NULL,
    descripcion TEXT NOT NULL,
    estado VARCHAR(30) NOT NULL DEFAULT 'Pendiente',
    prioridad VARCHAR(20) NOT NULL DEFAULT 'Media',
    fecha_creacion TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT chk_tickets_estado CHECK (estado IN ('Pendiente', 'En proceso', 'Resuelto')),
    CONSTRAINT chk_tickets_prioridad CHECK (prioridad IN ('Baja', 'Media', 'Alta', 'Crítica')),
    CONSTRAINT fk_tickets_reportante FOREIGN KEY (usuario_id) REFERENCES usuarios(id_usuario),
    CONSTRAINT fk_tickets_tecnico FOREIGN KEY (tecnico_id) REFERENCES usuarios(id_usuario)
) ENGINE=InnoDB;

CREATE TABLE kpis (
    id_kpi INT AUTO_INCREMENT PRIMARY KEY,
    nombre_kpi VARCHAR(100) NOT NULL,
    descripcion TEXT,
    formula TEXT,
    valor_actual DECIMAL(10,2)
) ENGINE=InnoDB;

CREATE TABLE logsAuditoria (
    id_log INT AUTO_INCREMENT PRIMARY KEY,
    usuario_id INT NOT NULL,
    accion VARCHAR(100) NOT NULL,
    modulo VARCHAR(100) NOT NULL,
    fecha_accion TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    detalle TEXT,
    CONSTRAINT fk_logs_usuarios FOREIGN KEY (usuario_id) REFERENCES usuarios(id_usuario)
) ENGINE=InnoDB;

INSERT INTO roles (nombre_rol, descripcion) VALUES
    ('Administrador', 'Control total del sistema'),
    ('Técnico IT', 'Registra visitas, mantenimientos y tickets'),
    ('Supervisor IT', 'Aprueba y monitorea actividades técnicas'),
    ('Gerencia', 'Visualización de dashboards y reportes'),
    ('Encargado de Bodega', 'Gestiona inventario y suministros'),
    ('Auditor', 'Consulta logs y movimientos del sistema');

INSERT INTO agencias (nombre_agencia, direccion, region, telefono) VALUES
    ('Agencia Central', '6a Avenida 9-51 Zona 1', 'Metropolitana', '22220001'),
    ('Agencia Zona 10', 'Blvd. Los Próceres 15-60 Zona 10', 'Metropolitana', '22220002'),
    ('Agencia Quetzaltenango', '14 Av. 3-31 Zona 3, Xela', 'Occidente', '77760001'),
    ('Agencia Escuintla', '4a Calle 4-15 Zona 1, Escuintla', 'Sur', '78800001');

INSERT INTO categorias (nombre_categoria, descripcion) VALUES
    ('Computadora de Escritorio', 'Desktop y workstations'),
    ('Laptop', 'Equipos portátiles'),
    ('Servidor', 'Servidores físicos y rack'),
    ('Impresora', 'Impresoras láser e inyección'),
    ('Red y Conectividad', 'Switches, routers, access points'),
    ('Periférico', 'Teclados, mouse, monitores');

INSERT INTO usuarios (nombre, apellido, correo, contrasena, telefono, rol_id, estado) VALUES
    ('Fernando', 'Pineda', 'fernando.pineda@banrural.com', 'hashed_pwd_001', '55550001', 1, 'activo'),
    ('Carlos', 'Méndez', 'carlos.mendez@banrural.com', 'hashed_pwd_002', '55550002', 2, 'activo'),
    ('Andrea', 'López', 'andrea.lopez@banrural.com', 'hashed_pwd_003', '55550003', 2, 'activo'),
    ('Roberto', 'Hernández', 'roberto.hernandez@banrural.com', 'hashed_pwd_004', '55550004', 3, 'activo'),
    ('Silvia', 'Castillo', 'silvia.castillo@banrural.com', 'hashed_pwd_005', '55550005', 4, 'activo'),
    ('Marcos', 'Fuentes', 'marcos.fuentes@banrural.com', 'hashed_pwd_006', '55550006', 5, 'activo'),
    ('Patricia', 'Ruiz', 'patricia.ruiz@banrural.com', 'hashed_pwd_007', '55550007', 6, 'activo');

INSERT INTO bodegas (nombre_bodega, ubicacion, encargado_id) VALUES
    ('Bodega Central', 'Edificio Principal, Sótano 1, Zona 1', 6),
    ('Bodega Zona 10', 'Torre Empresarial, Nivel -1, Zona 10', 6),
    ('Bodega Quetzaltenango', 'Sede Regional Xela, Planta Baja', 6);

INSERT INTO inventario (serial, categoria_id, marca, modelo, estado, bodega_id, fecha_compra, garantia_fin) VALUES
    ('SN-2024-PC-001', 1, 'Dell', 'OptiPlex 7090', 'Disponible', 1, '2024-01-15', '2027-01-15'),
    ('SN-2024-PC-002', 1, 'HP', 'EliteDesk 800 G6', 'Disponible', 1, '2024-02-10', '2027-02-10'),
    ('SN-2024-LT-001', 2, 'Lenovo', 'ThinkPad E15', 'Disponible', 2, '2024-03-05', '2026-03-05'),
    ('SN-2024-LT-002', 2, 'HP', 'ProBook 450 G9', 'Prestado', 2, '2024-03-20', '2026-03-20'),
    ('SN-2024-SV-001', 3, 'Dell', 'PowerEdge R750', 'Disponible', 1, '2023-08-01', '2026-08-01'),
    ('SN-2024-NW-001', 5, 'Cisco', 'Catalyst 2960-X', 'Disponible', 1, '2023-11-12', '2025-11-12'),
    ('SN-2024-PR-001', 4, 'HP', 'LaserJet Pro M404', 'Dañado', 3, '2022-06-01', '2024-06-01');

INSERT INTO visitasTecnicas (tecnico_id, agencia_id, fecha_visita, estado, descripcion) VALUES
    (2, 1, '2025-05-01 08:30:00', 'Completada', 'Mantenimiento preventivo de red y equipos de escritorio.'),
    (3, 2, '2025-05-05 09:00:00', 'Completada', 'Instalación de nuevo switch en rack principal.'),
    (2, 3, '2025-05-08 10:00:00', 'En proceso', 'Diagnóstico de falla en servidor de sucursal.'),
    (3, 4, '2025-05-09 08:00:00', 'Pendiente', 'Revisión periódica programada de impresoras.');

INSERT INTO mantenimientos (visita_id, tipo_mantenimiento, descripcion, estado) VALUES
    (1, 'Preventivo', 'Limpieza de equipos, actualización de drivers y SO.', 'Completado'),
    (1, 'Correctivo', 'Reemplazo de cable de red dañado en punto de acceso 3.', 'Completado'),
    (2, 'Instalación', 'Configuración y cableado de switch Cisco Catalyst.', 'Completado'),
    (3, 'Correctivo', 'Diagnóstico en curso: posible falla en disco RAID.', 'En proceso'),
    (4, 'Preventivo', 'Revisión de niveles de tóner y estado mecánico.', 'Pendiente');

INSERT INTO movimientosInventario (equipo_id, usuario_id, tipo_movimiento, descripcion) VALUES
    (4, 2, 'Salida', 'Préstamo de laptop a técnico para visita a Agencia Zona 10.'),
    (1, 6, 'Entrada', 'Recepción de equipo nuevo desde proveedor.'),
    (5, 6, 'Transferencia', 'Traslado de servidor de Bodega Central a Bodega Zona 10.'),
    (7, 6, 'Salida', 'Retiro de impresora dañada para revisión técnica externa.');

INSERT INTO prestamosEquipos (equipo_id, usuario_id, fecha_salida, fecha_retorno, estado) VALUES
    (4, 3, '2025-05-05 08:00:00', '2025-05-06 17:00:00', 'Devuelto'),
    (3, 2, '2025-05-08 07:30:00', NULL, 'Prestado');

INSERT INTO tickets (usuario_id, tecnico_id, titulo, descripcion, estado, prioridad) VALUES
    (2, NULL, 'Sin conectividad en agencia Zona 10', 'Todos los equipos de la agencia perdieron acceso a internet desde las 8:00 AM.', 'Pendiente', 'Crítica'),
    (3, 2, 'Impresora no reconocida por el sistema', 'La impresora HP en el 2do nivel no aparece en la red.', 'En proceso', 'Media'),
    (5, 3, 'Solicitud de nuevo equipo para gerencia', 'El equipo actual del gerente regional presenta lentitud severa.', 'Pendiente', 'Alta'),
    (2, 2, 'Actualización de antivirus en 10 equipos', 'Actualización programada de antivirus en equipos de la agencia central.', 'Resuelto', 'Baja');

INSERT INTO kpis (nombre_kpi, descripcion, formula) VALUES
    ('Tiempo promedio de resolución', 'Tiempo promedio de cierre de tickets', 'AVG(fecha_cierre - fecha_creacion) FROM tickets WHERE estado = ''Resuelto'''),
    ('Equipos dañados', 'Equipos fuera de servicio', 'COUNT(*) FROM inventario WHERE estado = ''Dañado'''),
    ('Stock crítico', 'Suministros bajo mínimo establecido', 'COUNT(*) FROM inventario WHERE estado = ''Baja'''),
    ('SLA cumplidos', 'Porcentaje de incidencias resueltas a tiempo', 'SUM(estado=''Resuelto'') * 100 / COUNT(*) FROM tickets'),
    ('Rotación de inventario', 'Frecuencia de uso de activos', 'COUNT(*) FROM movimientosInventario WHERE tipo_movimiento = ''Salida''');

INSERT INTO logsAuditoria (usuario_id, accion, modulo, detalle) VALUES
    (1, 'Creación de usuario', 'Usuarios', 'Se creó el usuario carlos.mendez con rol Técnico IT.'),
    (6, 'Registro de equipo', 'Inventario', 'Se registró laptop ThinkPad E15 SN-2024-LT-001.'),
    (2, 'Registro de visita', 'Visitas', 'Se registró visita técnica a Agencia Central el 2025-05-01.'),
    (1, 'Modificación de rol', 'Usuarios', 'Se cambió el rol del usuario patricia.ruiz a Auditor.'),
    (6, 'Transferencia de activo', 'Inventario', 'Servidor PowerEdge R750 trasladado a Bodega Zona 10.');
