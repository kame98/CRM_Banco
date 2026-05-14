# CRM Reporter Banrural

Aplicacion de escritorio WPF para .NET Framework 4.8 conectada a MySQL. El alcance se baso en la documentacion de reportería CRM para Banrural: visitas tecnicas, mantenimientos, inventario, bodegas, prestamos, tickets, auditoria y KPIs.

## Requisitos

- Visual Studio con soporte para .NET Framework 4.8 y WPF.
- MySQL Server 8.x recomendado.
- Paquete NuGet `MySqlConnector` 2.4.0.

## Base de datos

1. Abra MySQL Workbench o la consola de MySQL.
2. Ejecute `scripts/schema_mysql.sql`.
3. Verifique que exista la base `banrural_crm`.

El script convierte el modelo recibido a sintaxis MySQL y carga datos iniciales de roles, agencias, categorias, usuarios, bodegas, inventario, visitas, mantenimientos, prestamos, tickets, KPIs y auditoria.

## Ejecutar

1. Abra `BanruralCrmReporter.sln` en Visual Studio.
2. Compile y ejecute el proyecto.
3. En la pestaña `Conexion`, configure servidor, puerto, base de datos, usuario y contrasena.
4. Use `Probar conexion` y luego `Cargar datos`.

## Modulos incluidos

- Dashboard con KPIs, garantias proximas a vencer y bitacora.
- Reportes para usuarios, visitas, inventario, tickets, mantenimientos, prestamos, movimientos, garantias y logs.
- Registro de usuarios.
- Registro de equipos de inventario.
- Registro de visitas tecnicas con validacion de 48 horas.
- Registro de tickets de incidencias.
