using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace BanruralCrmReporter
{
    public partial class MainWindow : Window
    {
        private readonly Dictionary<string, string> _reports = new Dictionary<string, string>
        {
            ["usuarios"] = @"
                SELECT u.id_usuario, u.nombre, u.apellido,
                       CONCAT(u.nombre, ' ', u.apellido) AS nombre_completo,
                       u.correo, u.telefono, u.rol_id, r.nombre_rol, u.estado, u.fecha_creacion
                FROM usuarios u
                INNER JOIN roles r ON u.rol_id = r.id_rol
                ORDER BY r.nombre_rol, u.nombre;",
            ["visitas"] = @"
                SELECT v.id_visita, v.tecnico_id, CONCAT(u.nombre, ' ', u.apellido) AS tecnico,
                       a.nombre_agencia, a.region, v.fecha_visita, v.estado, v.descripcion
                FROM visitasTecnicas v
                INNER JOIN usuarios u ON v.tecnico_id = u.id_usuario
                INNER JOIN agencias a ON v.agencia_id = a.id_agencia
                ORDER BY v.fecha_visita DESC;",
            ["inventario"] = @"
                SELECT i.id_equipo AS ID, i.id_equipo, i.serial,
                       CONCAT(i.marca, ' ', i.modelo) AS nombre_equipo,
                       c.nombre_categoria, i.categoria_id, i.marca, i.modelo,
                       i.estado, i.bodega_id, b.nombre_bodega AS responsable_bodega,
                       CONCAT(enc.nombre, ' ', enc.apellido) AS responsable,
                       i.fecha_compra AS fecha_ingreso, i.garantia_fin
                FROM inventario i
                INNER JOIN categorias c ON i.categoria_id = c.id_categoria
                INNER JOIN bodegas b ON i.bodega_id = b.id_bodega
                INNER JOIN usuarios enc ON b.encargado_id = enc.id_usuario
                ORDER BY c.nombre_categoria, i.estado;",
            ["tickets"] = @"
                SELECT t.id_ticket, t.usuario_id, t.tecnico_id, t.titulo, t.descripcion,
                       t.prioridad, t.estado,
                       CONCAT(rep.nombre, ' ', rep.apellido) AS reportado_por,
                       COALESCE(CONCAT(tec.nombre, ' ', tec.apellido), 'Sin asignar') AS tecnico_asignado,
                       COALESCE(t.tipificacion, '') AS tipificacion,
                       COALESCE(t.resultado_atencion, '') AS resultado_atencion,
                       COALESCE(t.descripcion_atencion, '') AS descripcion_atencion,
                       COALESCE(CONCAT(cierre.nombre, ' ', cierre.apellido), 'Sin cierre') AS cerrado_por,
                       t.fecha_creacion, t.fecha_cierre
                FROM tickets t
                INNER JOIN usuarios rep ON t.usuario_id = rep.id_usuario
                LEFT JOIN usuarios tec ON t.tecnico_id = tec.id_usuario
                LEFT JOIN usuarios cierre ON t.cerrado_por_id = cierre.id_usuario
                ORDER BY CASE t.prioridad WHEN 'Alta' THEN 2 WHEN 'Media' THEN 3 WHEN 'Baja' THEN 4 ELSE 1 END, t.fecha_creacion DESC;",
            ["mantenimientos"] = @"
                SELECT m.id_mantenimiento, m.tipo_mantenimiento,
                       m.estado AS estado_mantenimiento, v.fecha_visita,
                       a.nombre_agencia, CONCAT(u.nombre, ' ', u.apellido) AS tecnico,
                       m.descripcion
                FROM mantenimientos m
                INNER JOIN visitasTecnicas v ON m.visita_id = v.id_visita
                INNER JOIN agencias a ON v.agencia_id = a.id_agencia
                INNER JOIN usuarios u ON v.tecnico_id = u.id_usuario
                ORDER BY v.fecha_visita DESC;",
            ["prestamos"] = @"
                SELECT p.id_prestamo, i.serial, CONCAT(i.marca, ' ', i.modelo) AS equipo,
                       CONCAT(u.nombre, ' ', u.apellido) AS usuario_prestamo,
                       p.fecha_salida, p.fecha_retorno, p.estado
                FROM prestamosEquipos p
                INNER JOIN inventario i ON p.equipo_id = i.id_equipo
                INNER JOIN usuarios u ON p.usuario_id = u.id_usuario
                WHERE p.estado = 'Prestado'
                ORDER BY p.fecha_salida DESC;",
            ["movimientos"] = @"
                SELECT mv.id_movimiento, mv.tipo_movimiento, i.serial,
                       CONCAT(i.marca, ' ', i.modelo) AS equipo,
                       CONCAT(u.nombre, ' ', u.apellido) AS responsable,
                       mv.fecha_movimiento, mv.descripcion
                FROM movimientosInventario mv
                INNER JOIN inventario i ON mv.equipo_id = i.id_equipo
                INNER JOIN usuarios u ON mv.usuario_id = u.id_usuario
                ORDER BY mv.fecha_movimiento DESC;",
            ["garantias"] = @"
                SELECT i.id_equipo, i.serial, CONCAT(i.marca, ' ', i.modelo) AS equipo,
                       c.nombre_categoria, b.nombre_bodega, i.garantia_fin,
                       DATEDIFF(i.garantia_fin, CURDATE()) AS dias_restantes
                FROM inventario i
                INNER JOIN categorias c ON i.categoria_id = c.id_categoria
                INNER JOIN bodegas b ON i.bodega_id = b.id_bodega
                WHERE i.garantia_fin IS NOT NULL
                  AND i.garantia_fin BETWEEN CURDATE() AND DATE_ADD(CURDATE(), INTERVAL 90 DAY)
                ORDER BY i.garantia_fin;",
            ["logs"] = @"
                SELECT l.id_log, CONCAT(u.nombre, ' ', u.apellido) AS usuario,
                       r.nombre_rol, l.accion, l.modulo, l.fecha_accion, l.detalle
                FROM logsAuditoria l
                INNER JOIN usuarios u ON l.usuario_id = u.id_usuario
                INNER JOIN roles r ON u.rol_id = r.id_rol
                ORDER BY l.fecha_accion DESC;"
        };

        private int _editingUserId = 0;
        private int _editingAssetId = 0;
        private int _currentUserId = 0;
        private int _selectedTicketId = 0;
        private string _currentUserName = "";
        private string _currentRoleName = "";

        public MainWindow()
        {
            InitializeComponent();
            ReportBox.SelectedIndex = 0;
            VisitDatePicker.SelectedDate = DateTime.Today;
            PurchaseDatePicker.SelectedDate = DateTime.Today;
            LoginUserBox.Focus();
        }

        private string ConnectionString
        {
            get
            {
                var builder = new MySqlConnectionStringBuilder
                {
                    Server = "",
                    Port = ,
                    Database = "",
                    UserID = "",
                    Password = "",
                    CharacterSet = "utf8mb4",
                    AllowUserVariables = true
                };
                return builder.ConnectionString;
            }
        }

        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            await RunSafeAsync(async () =>
            {
                using (var connection = new MySqlConnection(ConnectionString))
                {
                    await connection.OpenAsync();
                }

                SetStatus("Conexion exitosa", true);
            });
        }

        private async void RefreshAll_Click(object sender, RoutedEventArgs e)
        {
            await LoadAllAsync();
        }

        private async void ReportBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await LoadSelectedReportAsync();
        }

        private async void LoadSelectedReport_Click(object sender, RoutedEventArgs e)
        {
            await LoadSelectedReportAsync();
        }

        private async void Login_Click(object sender, RoutedEventArgs e)
        {
            await LoginAsync();
        }

        private async void LoginPasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await LoginAsync();
            }
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            _currentUserId = 0;
            _currentUserName = "";
            _currentRoleName = "";
            AppShell.Visibility = Visibility.Collapsed;
            LoginPanel.Visibility = Visibility.Visible;
            LoginPasswordBox.Clear();
            LoginMessageText.Text = "Sesion cerrada.";
            LoginUserBox.Focus();
        }

        private async Task LoginAsync()
        {
            LoginMessageText.Text = "";

            if (string.IsNullOrWhiteSpace(LoginUserBox.Text) || string.IsNullOrWhiteSpace(LoginPasswordBox.Password))
            {
                LoginMessageText.Text = "Ingrese usuario/correo y contrasena.";
                return;
            }

            await RunSafeAsync(async () =>
            {
                await EnsureDefaultUsersAsync();
                await EnsureTicketWorkflowColumnsAsync();

                const string sql = @"
                    SELECT u.id_usuario, CONCAT(u.nombre, ' ', u.apellido) AS nombre_completo,
                           u.nombre, u.correo, r.nombre_rol
                    FROM usuarios u
                    INNER JOIN roles r ON u.rol_id = r.id_rol
                    WHERE u.estado = 'activo'
                      AND (u.correo = @login OR u.nombre = @login OR CONCAT(u.nombre, ' ', u.apellido) = @login)
                      AND u.contrasena = @password
                    LIMIT 1;";

                var user = await QueryAsyncWithParameters(sql,
                    ("@login", LoginUserBox.Text.Trim()),
                    ("@password", LoginPasswordBox.Password));

                if (user.Rows.Count == 0)
                {
                    LoginMessageText.Text = "Credenciales invalidas o usuario inactivo.";
                    SetStatus("Login fallido", false);
                    return;
                }

                var row = user.Rows[0];
                _currentUserId = Convert.ToInt32(row["id_usuario"]);
                _currentUserName = Convert.ToString(row["nombre_completo"]);
                _currentRoleName = Convert.ToString(row["nombre_rol"]);

                CurrentUserText.Text = $"{_currentUserName} ({_currentRoleName})";
                LoginPanel.Visibility = Visibility.Collapsed;
                AppShell.Visibility = Visibility.Visible;
                ApplyPermissions();
                await LoadAllAsync();
                SetStatus("Sesion iniciada", true);
            });
        }

        private async void SaveUser_Click(object sender, RoutedEventArgs e)
        {
            await RunSafeAsync(async () =>
            {
                if (!ValidateUserForm())
                {
                    return;
                }

                if (_editingUserId > 0)
                {
                    if (string.IsNullOrWhiteSpace(UserPasswordBox.Password))
                    {
                        const string updateSql = @"
                            UPDATE usuarios
                            SET nombre = @nombre, apellido = @apellido, correo = @correo,
                                telefono = @telefono, rol_id = @rol_id, estado = @estado
                            WHERE id_usuario = @id_usuario;";

                        await ExecuteAsync(updateSql,
                            ("@id_usuario", _editingUserId),
                            ("@nombre", UserNameBox.Text.Trim()),
                            ("@apellido", UserLastNameBox.Text.Trim()),
                            ("@correo", UserEmailBox.Text.Trim()),
                            ("@telefono", UserPhoneBox.Text.Trim()),
                            ("@rol_id", UserRoleBox.SelectedValue),
                            ("@estado", ComboText(UserStatusBox)));
                    }
                    else
                    {
                        const string updateWithPasswordSql = @"
                            UPDATE usuarios
                            SET nombre = @nombre, apellido = @apellido, correo = @correo,
                                contrasena = @contrasena, telefono = @telefono, rol_id = @rol_id, estado = @estado
                            WHERE id_usuario = @id_usuario;";

                        await ExecuteAsync(updateWithPasswordSql,
                            ("@id_usuario", _editingUserId),
                            ("@nombre", UserNameBox.Text.Trim()),
                            ("@apellido", UserLastNameBox.Text.Trim()),
                            ("@correo", UserEmailBox.Text.Trim()),
                            ("@contrasena", UserPasswordBox.Password),
                            ("@telefono", UserPhoneBox.Text.Trim()),
                            ("@rol_id", UserRoleBox.SelectedValue),
                            ("@estado", ComboText(UserStatusBox)));
                    }

                    SetStatus("Usuario actualizado correctamente", true);
                }
                else
                {
                    const string insertSql = @"
                        INSERT INTO usuarios (nombre, apellido, correo, contrasena, telefono, rol_id, estado)
                        VALUES (@nombre, @apellido, @correo, @contrasena, @telefono, @rol_id, @estado);";

                    await ExecuteAsync(insertSql,
                        ("@nombre", UserNameBox.Text.Trim()),
                        ("@apellido", UserLastNameBox.Text.Trim()),
                        ("@correo", UserEmailBox.Text.Trim()),
                        ("@contrasena", UserPasswordBox.Password),
                        ("@telefono", UserPhoneBox.Text.Trim()),
                        ("@rol_id", UserRoleBox.SelectedValue),
                        ("@estado", ComboText(UserStatusBox)));

                    SetStatus("Usuario registrado correctamente", true);
                }

                ClearUserFormInternal();
                await LoadLookupsAsync();
                await LoadUsersAsync();
                await LoadDashboardAsync();
            });
        }

        private async void SaveAsset_Click(object sender, RoutedEventArgs e)
        {
            await RunSafeAsync(async () =>
            {
                if (!ValidateAssetForm())
                {
                    return;
                }

                if (_editingAssetId > 0)
                {
                    const string updateSql = @"
                        UPDATE inventario
                        SET serial = @serial, categoria_id = @categoria_id, marca = @marca,
                            modelo = @modelo, estado = @estado, bodega_id = @bodega_id,
                            fecha_compra = @fecha_compra, garantia_fin = @garantia_fin
                        WHERE id_equipo = @id_equipo;";

                    await ExecuteAsync(updateSql,
                        ("@id_equipo", _editingAssetId),
                        ("@serial", SerialBox.Text.Trim()),
                        ("@categoria_id", CategoryBox.SelectedValue),
                        ("@marca", BrandBox.Text.Trim()),
                        ("@modelo", ModelBox.Text.Trim()),
                        ("@estado", DatabaseStatus(ComboText(AssetStatusBox))),
                        ("@bodega_id", WarehouseBox.SelectedValue),
                        ("@fecha_compra", DateOrNull(PurchaseDatePicker.SelectedDate)),
                        ("@garantia_fin", DateOrNull(WarrantyDatePicker.SelectedDate)));

                    SetStatus("Equipo actualizado correctamente", true);
                }
                else
                {
                    const string insertSql = @"
                        INSERT INTO inventario (serial, categoria_id, marca, modelo, estado, bodega_id, fecha_compra, garantia_fin)
                        VALUES (@serial, @categoria_id, @marca, @modelo, @estado, @bodega_id, @fecha_compra, @garantia_fin);";

                    await ExecuteAsync(insertSql,
                        ("@serial", SerialBox.Text.Trim()),
                        ("@categoria_id", CategoryBox.SelectedValue),
                        ("@marca", BrandBox.Text.Trim()),
                        ("@modelo", ModelBox.Text.Trim()),
                        ("@estado", DatabaseStatus(ComboText(AssetStatusBox))),
                        ("@bodega_id", WarehouseBox.SelectedValue),
                        ("@fecha_compra", DateOrNull(PurchaseDatePicker.SelectedDate)),
                        ("@garantia_fin", DateOrNull(WarrantyDatePicker.SelectedDate)));

                    SetStatus("Equipo registrado correctamente", true);
                }

                ClearAssetFormInternal();
                await LoadInventoryAsync();
                await LoadDashboardAsync();
            });
        }

        private async void SaveVisit_Click(object sender, RoutedEventArgs e)
        {
            await RunSafeAsync(async () =>
            {
                if (VisitTechnicianBox.SelectedValue == null || AgencyBox.SelectedValue == null || !VisitDatePicker.SelectedDate.HasValue)
                {
                    ShowValidation("Seleccione tecnico, agencia y fecha de visita.");
                    return;
                }

                if (VisitDatePicker.SelectedDate.Value < DateTime.Today.AddDays(-2))
                {
                    ShowValidation("La visita supera el limite de 48 horas para registro.");
                    return;
                }

                const string sql = @"
                    INSERT INTO visitasTecnicas (tecnico_id, agencia_id, fecha_visita, estado, descripcion)
                    VALUES (@tecnico_id, @agencia_id, @fecha_visita, @estado, @descripcion);";

                await ExecuteAsync(sql,
                    ("@tecnico_id", VisitTechnicianBox.SelectedValue),
                    ("@agencia_id", AgencyBox.SelectedValue),
                    ("@fecha_visita", DateOrNull(VisitDatePicker.SelectedDate)),
                    ("@estado", ComboText(VisitStatusBox)),
                    ("@descripcion", VisitDescriptionBox.Text.Trim()));

                VisitDescriptionBox.Clear();
                await LoadVisitsAsync();
                await LoadDashboardAsync();
                SetStatus("Visita registrada correctamente", true);
            });
        }

        private async void SaveTicket_Click(object sender, RoutedEventArgs e)
        {
            await RunSafeAsync(async () =>
            {
                if (TicketReporterBox.SelectedValue == null || string.IsNullOrWhiteSpace(TicketTitleBox.Text) || string.IsNullOrWhiteSpace(TicketDescriptionBox.Text))
                {
                    ShowValidation("Seleccione reportante e ingrese titulo y descripcion del ticket.");
                    return;
                }

                const string sql = @"
                    INSERT INTO tickets (usuario_id, tecnico_id, titulo, descripcion, estado, prioridad)
                    VALUES (@usuario_id, @tecnico_id, @titulo, @descripcion, @estado, @prioridad);";

                await ExecuteAsync(sql,
                    ("@usuario_id", TicketReporterBox.SelectedValue),
                    ("@tecnico_id", TicketTechnicianBox.SelectedValue ?? DBNull.Value),
                    ("@titulo", TicketTitleBox.Text.Trim()),
                    ("@descripcion", TicketDescriptionBox.Text.Trim()),
                    ("@estado", ComboText(TicketStatusBox)),
                    ("@prioridad", DatabasePriority(ComboText(TicketPriorityBox))));

                ClearTicketForm();
                await LoadTicketsAsync();
                await LoadUserTicketsAsync();
                await LoadDashboardAsync();
                SetStatus("Ticket registrado correctamente", true);
            });
        }

        private async void CreateUserTicket_Click(object sender, RoutedEventArgs e)
        {
            await RunSafeAsync(async () =>
            {
                if (_currentUserId <= 0)
                {
                    ShowValidation("Debe iniciar sesion para crear solicitudes.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(RequestTicketTitleBox.Text) ||
                    string.IsNullOrWhiteSpace(RequestTicketDescriptionBox.Text))
                {
                    ShowValidation("Ingrese titulo y descripcion de la solicitud.");
                    return;
                }

                const string sql = @"
                    INSERT INTO tickets (usuario_id, tecnico_id, titulo, descripcion, estado, prioridad, tipificacion)
                    VALUES (@usuario_id, NULL, @titulo, @descripcion, 'Pendiente', @prioridad, @tipificacion);";

                await ExecuteAsync(sql,
                    ("@usuario_id", _currentUserId),
                    ("@titulo", RequestTicketTitleBox.Text.Trim()),
                    ("@descripcion", RequestTicketDescriptionBox.Text.Trim()),
                    ("@prioridad", DatabasePriority(ComboText(RequestTicketPriorityBox))),
                    ("@tipificacion", ComboText(RequestTicketTypeBox)));

                RequestTicketTitleBox.Clear();
                RequestTicketDescriptionBox.Clear();
                RequestTicketMessageText.Text = "Solicitud enviada correctamente. Estado inicial: Pendiente.";
                await LoadUserTicketsAsync();
                await LoadTicketsAsync();
                await LoadDashboardAsync();
                SetStatus("Solicitud de ticket creada", true);
            });
        }

        private async void CloseTicket_Click(object sender, RoutedEventArgs e)
        {
            await RunSafeAsync(async () =>
            {
                if (_selectedTicketId <= 0)
                {
                    ShowValidation("Seleccione un ticket de la tabla antes de guardar la atencion.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(TicketWorkDescriptionBox.Text))
                {
                    ShowValidation("Ingrese la descripcion del trabajo realizado.");
                    return;
                }

                string result = ComboText(TicketResolutionResultBox);
                string ticketStatus = TicketStatusFromResult(result);

                const string sql = @"
                    UPDATE tickets
                    SET estado = @estado,
                        tecnico_id = COALESCE(tecnico_id, @usuario_id),
                        tipificacion = @tipificacion,
                        resultado_atencion = @resultado,
                        descripcion_atencion = @descripcion_atencion,
                        cerrado_por_id = @usuario_id,
                        fecha_cierre = CASE WHEN @estado = 'Resuelto' THEN CURRENT_TIMESTAMP ELSE NULL END
                    WHERE id_ticket = @id_ticket;";

                await ExecuteAsync(sql,
                    ("@estado", ticketStatus),
                    ("@usuario_id", _currentUserId),
                    ("@tipificacion", ComboText(TicketResolutionTypeBox)),
                    ("@resultado", result),
                    ("@descripcion_atencion", TicketWorkDescriptionBox.Text.Trim()),
                    ("@id_ticket", _selectedTicketId));

                TicketWorkDescriptionBox.Clear();
                TicketWorkMessageText.Text = $"Ticket #{_selectedTicketId} actualizado como {result}.";
                await LoadTicketsAsync();
                await LoadUserTicketsAsync();
                if (DashboardTab.Visibility == Visibility.Visible)
                {
                    await LoadDashboardAsync();
                }
                SetStatus("Atencion de ticket guardada", true);
            });
        }

        private async Task LoadAllAsync()
        {
            await RunSafeAsync(async () =>
            {
                await LoadLookupsAsync();
                if (DashboardTab.Visibility == Visibility.Visible)
                {
                    await LoadDashboardAsync();
                }

                if (UsersTab.Visibility == Visibility.Visible)
                {
                    await LoadUsersAsync();
                }

                if (InventoryTab.Visibility == Visibility.Visible)
                {
                    await LoadInventoryAsync();
                }

                if (WorkTab.Visibility == Visibility.Visible)
                {
                    await LoadVisitsAsync();
                    await LoadTicketsAsync();
                }

                await LoadUserTicketsAsync();
                if (ReportsTab.Visibility == Visibility.Visible)
                {
                    await LoadSelectedReportAsync();
                }

                SetStatus("Datos cargados correctamente", true);
            });
        }

        private async Task LoadLookupsAsync()
        {
            UserRoleBox.ItemsSource = (await QueryAsync("SELECT id_rol, nombre_rol FROM roles ORDER BY nombre_rol;")).DefaultView;
            CategoryBox.ItemsSource = (await QueryAsync("SELECT id_categoria, nombre_categoria FROM categorias ORDER BY nombre_categoria;")).DefaultView;
            WarehouseBox.ItemsSource = (await QueryAsync("SELECT id_bodega, nombre_bodega FROM bodegas ORDER BY nombre_bodega;")).DefaultView;
            AgencyBox.ItemsSource = (await QueryAsync("SELECT id_agencia, nombre_agencia FROM agencias ORDER BY nombre_agencia;")).DefaultView;

            const string usersSql = @"
                SELECT u.id_usuario, CONCAT(u.nombre, ' ', u.apellido, ' - ', r.nombre_rol) AS nombre_completo
                FROM usuarios u
                INNER JOIN roles r ON u.rol_id = r.id_rol
                WHERE u.estado = 'activo'
                ORDER BY u.nombre, u.apellido;";

            var users = await QueryAsync(usersSql);

            VisitTechnicianBox.ItemsSource = users.DefaultView;
            TicketReporterBox.ItemsSource = users.Copy().DefaultView;
            TicketTechnicianBox.ItemsSource = users.Copy().DefaultView;

            SelectFirstIfEmpty(UserRoleBox);
            SelectFirstIfEmpty(CategoryBox);
            SelectFirstIfEmpty(WarehouseBox);
            SelectFirstIfEmpty(AgencyBox);
            SelectFirstIfEmpty(VisitTechnicianBox);
            SelectFirstIfEmpty(TicketReporterBox);
        }

        private async Task LoadDashboardAsync()
        {
            var summary = await QueryAsyncWithParameters(@"
                SELECT
                    (SELECT COUNT(*) FROM tickets) AS tickets_total,
                    (SELECT COUNT(*) FROM tickets WHERE estado = 'Resuelto') AS tickets_resueltos,
                    (SELECT COUNT(*) FROM tickets WHERE estado = 'Pendiente') AS tickets_pendientes,
                    (SELECT COUNT(*) FROM inventario) AS equipos_total,
                    (SELECT COUNT(*) FROM inventario WHERE estado = 'Disponible') AS equipos_disponibles,
                    (SELECT COUNT(*) FROM inventario WHERE estado = @estado_danado) AS equipos_danados,
                    (SELECT COUNT(*) FROM prestamosEquipos WHERE estado = 'Prestado') AS prestamos_activos;",
                ("@estado_danado", DatabaseStatus("Danado")));

            KpiPanel.Children.Clear();
            TicketStatusPanel.Children.Clear();
            AssetStatusPanel.Children.Clear();

            if (summary.Rows.Count > 0)
            {
                var row = summary.Rows[0];
                decimal totalTickets = ToDecimal(row["tickets_total"]);
                decimal resolvedTickets = ToDecimal(row["tickets_resueltos"]);
                decimal pendingTickets = ToDecimal(row["tickets_pendientes"]);
                decimal totalAssets = ToDecimal(row["equipos_total"]);
                decimal availableAssets = ToDecimal(row["equipos_disponibles"]);
                decimal damagedAssets = ToDecimal(row["equipos_danados"]);
                decimal activeLoans = ToDecimal(row["prestamos_activos"]);

                KpiPanel.Children.Add(CreateKpiCard("Tickets resueltos", resolvedTickets.ToString("0"), Percent(resolvedTickets, totalTickets), "Porcentaje de tickets cerrados sobre el total.", true));
                KpiPanel.Children.Add(CreateKpiCard("Tickets pendientes", pendingTickets.ToString("0"), Percent(pendingTickets, totalTickets), "Casos abiertos que requieren seguimiento.", false));
                KpiPanel.Children.Add(CreateKpiCard("Equipos disponibles", availableAssets.ToString("0"), Percent(availableAssets, totalAssets), "Inventario listo para asignarse.", true));
                KpiPanel.Children.Add(CreateKpiCard("Equipos danados", damagedAssets.ToString("0"), Percent(damagedAssets, totalAssets), "Equipos fuera de servicio.", false));
                KpiPanel.Children.Add(CreateKpiCard("Prestamos activos", activeLoans.ToString("0"), Percent(activeLoans, totalAssets), "Equipos actualmente prestados.", false));
            }

            var ticketStatus = await QueryAsync("SELECT estado, COUNT(*) AS total FROM tickets GROUP BY estado ORDER BY estado;");
            var assetStatus = await QueryAsync("SELECT estado, COUNT(*) AS total FROM inventario GROUP BY estado ORDER BY estado;");

            FillDistributionPanel(TicketStatusPanel, ticketStatus, "estado", "total");
            FillDistributionPanel(AssetStatusPanel, assetStatus, "estado", "total");

            WarrantyGrid.ItemsSource = (await QueryAsync(_reports["garantias"])).DefaultView;
            AuditGrid.ItemsSource = (await QueryAsync(_reports["logs"])).DefaultView;
        }

        private async Task LoadUsersAsync()
        {
            UsersGrid.ItemsSource = (await QueryAsync(_reports["usuarios"])).DefaultView;
        }

        private async Task LoadInventoryAsync()
        {
            InventoryGrid.ItemsSource = (await QueryAsync(_reports["inventario"])).DefaultView;
        }

        private async Task LoadVisitsAsync()
        {
            VisitsGrid.ItemsSource = (await QueryAsync(_reports["visitas"])).DefaultView;
        }

        private async Task LoadTicketsAsync()
        {
            TicketsGrid.ItemsSource = (await QueryAsync(_reports["tickets"])).DefaultView;
        }

        private async Task LoadUserTicketsAsync()
        {
            const string baseSql = @"
                SELECT t.id_ticket, t.titulo, t.descripcion, t.prioridad, t.estado,
                       CONCAT(rep.nombre, ' ', rep.apellido) AS creado_por,
                       COALESCE(CONCAT(tec.nombre, ' ', tec.apellido), 'Sin asignar') AS tecnico_asignado,
                       COALESCE(t.tipificacion, '') AS tipificacion,
                       COALESCE(t.resultado_atencion, '') AS resultado_atencion,
                       COALESCE(t.descripcion_atencion, '') AS descripcion_atencion,
                       COALESCE(CONCAT(cierre.nombre, ' ', cierre.apellido), 'Sin cierre') AS cerrado_por,
                       t.fecha_creacion, t.fecha_cierre
                FROM tickets t
                INNER JOIN usuarios rep ON t.usuario_id = rep.id_usuario
                LEFT JOIN usuarios tec ON t.tecnico_id = tec.id_usuario
                LEFT JOIN usuarios cierre ON t.cerrado_por_id = cierre.id_usuario";

            if (RoleIs("Usuario"))
            {
                UserTicketsTitleText.Text = "Mis solicitudes de tickets";
                UserTicketsGrid.ItemsSource = (await QueryAsyncWithParameters(
                    baseSql + " WHERE t.usuario_id = @usuario_id ORDER BY t.fecha_creacion DESC;",
                    ("@usuario_id", _currentUserId))).DefaultView;
            }
            else
            {
                UserTicketsTitleText.Text = "Tickets creados por usuarios";
                UserTicketsGrid.ItemsSource = (await QueryAsync(
                    baseSql + " ORDER BY t.fecha_creacion DESC;")).DefaultView;
            }
        }

        private async Task EnsureDefaultUsersAsync()
        {
            var roles = new[]
            {
                ("Administrador", "Control total del sistema"),
                ("Auditor", "Consulta auditoria, logs e historial"),
                ("Encargado de Bodega", "Gestiona inventario y bodega"),
                ("Gerencia", "Consulta dashboards, KPIs y reportes"),
                ("Supervisor IT", "Supervisa visitas, tickets, tecnicos y KPIs"),
                ("T\u00e9cnico IT", "Registra visitas, mantenimientos y atiende tickets"),
                ("Usuario", "Crea solicitudes de tickets y consulta su estado")
            };

            foreach (var role in roles)
            {
                await ExecuteAsync(@"
                    INSERT INTO roles (nombre_rol, descripcion)
                    SELECT @nombre_rol, @descripcion
                    WHERE NOT EXISTS (SELECT 1 FROM roles WHERE nombre_rol = @nombre_rol);",
                    ("@nombre_rol", role.Item1),
                    ("@descripcion", role.Item2));
            }

            var users = new[]
            {
                ("Administrador", "Sistema", "admin@banrural.local", "admin123", "Administrador"),
                ("Auditor", "Sistema", "auditor@banrural.local", "auditor123", "Auditor"),
                ("Bodega", "Sistema", "bodega@banrural.local", "bodega123", "Encargado de Bodega"),
                ("Gerencia", "Sistema", "gerencia@banrural.local", "gerencia123", "Gerencia"),
                ("Supervisor", "IT", "supervisor@banrural.local", "supervisor123", "Supervisor IT"),
                ("Tecnico", "IT", "tecnico@banrural.local", "tecnico123", "T\u00e9cnico IT"),
                ("usuario", "General", "usuario@banrural.local", "usuario123", "Usuario")
            };

            foreach (var user in users)
            {
                await ExecuteAsync(@"
                    INSERT INTO usuarios (nombre, apellido, correo, contrasena, telefono, rol_id, estado)
                    SELECT @nombre, @apellido, @correo, @contrasena, '', r.id_rol, 'activo'
                    FROM roles r
                    WHERE r.nombre_rol = @rol
                      AND NOT EXISTS (SELECT 1 FROM usuarios WHERE correo = @correo);",
                    ("@nombre", user.Item1),
                    ("@apellido", user.Item2),
                    ("@correo", user.Item3),
                    ("@contrasena", user.Item4),
                    ("@rol", user.Item5));
            }
        }

        private async Task EnsureTicketWorkflowColumnsAsync()
        {
            await AddTicketColumnIfMissingAsync("tipificacion", "ALTER TABLE tickets ADD COLUMN tipificacion VARCHAR(100) NULL;");
            await AddTicketColumnIfMissingAsync("resultado_atencion", "ALTER TABLE tickets ADD COLUMN resultado_atencion VARCHAR(30) NULL;");
            await AddTicketColumnIfMissingAsync("descripcion_atencion", "ALTER TABLE tickets ADD COLUMN descripcion_atencion TEXT NULL;");
            await AddTicketColumnIfMissingAsync("cerrado_por_id", "ALTER TABLE tickets ADD COLUMN cerrado_por_id INT NULL;");
            await AddTicketColumnIfMissingAsync("fecha_cierre", "ALTER TABLE tickets ADD COLUMN fecha_cierre TIMESTAMP NULL;");
        }

        private async Task AddTicketColumnIfMissingAsync(string columnName, string alterSql)
        {
            const string existsSql = @"
                SELECT COUNT(*) AS total
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'tickets'
                  AND COLUMN_NAME = @column_name;";

            var result = await QueryAsyncWithParameters(existsSql, ("@column_name", columnName));
            if (result.Rows.Count == 0 || ToDecimal(result.Rows[0]["total"]) == 0)
            {
                await ExecuteAsync(alterSql);
            }
        }

        private void ApplyPermissions()
        {
            bool admin = RoleIs("Administrador");
            bool auditor = RoleIs("Auditor");
            bool bodega = RoleIs("Encargado de Bodega");
            bool gerencia = RoleIs("Gerencia");
            bool supervisor = RoleIs("Supervisor IT");
            bool tecnico = RoleIs("Tecnico IT");
            bool usuario = RoleIs("Usuario");

            UsersTab.Visibility = admin ? Visibility.Visible : Visibility.Collapsed;
            DashboardTab.Visibility = (admin || auditor || gerencia || supervisor) ? Visibility.Visible : Visibility.Collapsed;
            ReportsTab.Visibility = (admin || auditor || gerencia || supervisor) ? Visibility.Visible : Visibility.Collapsed;
            InventoryTab.Visibility = (admin || bodega || supervisor) ? Visibility.Visible : Visibility.Collapsed;
            WorkTab.Visibility = (admin || tecnico || supervisor || bodega) ? Visibility.Visible : Visibility.Collapsed;
            UserRequestsTab.Visibility = Visibility.Visible;

            VisitEntryGroup.Visibility = (admin || tecnico || supervisor) ? Visibility.Visible : Visibility.Collapsed;
            TicketEntryGroup.Visibility = (admin || tecnico || supervisor) ? Visibility.Visible : Visibility.Collapsed;
            TicketWorkGroup.Visibility = (admin || tecnico || supervisor || bodega) ? Visibility.Visible : Visibility.Collapsed;

            RequestTicketTitleBox.IsEnabled = usuario || admin || tecnico || supervisor;
            RequestTicketTypeBox.IsEnabled = usuario || admin || tecnico || supervisor;
            RequestTicketPriorityBox.IsEnabled = usuario || admin || tecnico || supervisor;
            RequestTicketDescriptionBox.IsEnabled = usuario || admin || tecnico || supervisor;
            RequestTicketButton.IsEnabled = usuario || admin || tecnico || supervisor;

            SelectDefaultReportForRole();
            SelectFirstVisibleTab();
        }

        private bool CanViewReport(string key)
        {
            if (RoleIs("Administrador") || RoleIs("Gerencia") || RoleIs("Supervisor IT"))
            {
                return true;
            }

            if (RoleIs("Auditor"))
            {
                return key == "logs" || key == "movimientos";
            }

            return false;
        }

        private void SelectDefaultReportForRole()
        {
            string defaultKey = RoleIs("Auditor") ? "logs" : "usuarios";

            foreach (var rawItem in ReportBox.Items)
            {
                if (rawItem is ComboBoxItem item && Convert.ToString(item.Tag) == defaultKey)
                {
                    ReportBox.SelectedItem = item;
                    return;
                }
            }
        }

        private void SelectFirstVisibleTab()
        {
            if (RoleIs("Usuario"))
            {
                MainTabs.SelectedItem = UserRequestsTab;
                return;
            }

            foreach (TabItem item in MainTabs.Items)
            {
                if (item.Visibility == Visibility.Visible)
                {
                    MainTabs.SelectedItem = item;
                    return;
                }
            }
        }

        private async Task LoadSelectedReportAsync()
        {
            if (_currentUserId == 0)
            {
                return;
            }

            if (!(ReportBox.SelectedItem is ComboBoxItem item) || item.Tag == null)
            {
                return;
            }

            var key = Convert.ToString(item.Tag);

            if (!_reports.ContainsKey(key) || !CanViewReport(key))
            {
                SetStatus("Reporte no permitido para el rol actual", false);
                return;
            }

            await RunSafeAsync(async () =>
            {
                ReportGrid.ItemsSource = (await QueryAsync(_reports[key])).DefaultView;
            });
        }

        private async Task<DataTable> QueryAsync(string sql)
        {
            using (var connection = new MySqlConnection(ConnectionString))
            using (var command = new MySqlCommand(sql, connection))
            using (var adapter = new MySqlDataAdapter(command))
            {
                var table = new DataTable();
                await connection.OpenAsync();
                adapter.Fill(table);
                return table;
            }
        }

        private async Task<DataTable> QueryAsyncWithParameters(string sql, params (string Name, object Value)[] parameters)
        {
            using (var connection = new MySqlConnection(ConnectionString))
            using (var command = new MySqlCommand(sql, connection))
            using (var adapter = new MySqlDataAdapter(command))
            {
                foreach (var parameter in parameters)
                {
                    command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
                }

                var table = new DataTable();
                await connection.OpenAsync();
                adapter.Fill(table);
                return table;
            }
        }

        private async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
        {
            using (var connection = new MySqlConnection(ConnectionString))
            using (var command = new MySqlCommand(sql, connection))
            {
                foreach (var parameter in parameters)
                {
                    command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
                }

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();
            }
        }

        private async Task RunSafeAsync(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                SetStatus("Error: revise los datos", false);
                MessageBox.Show(ex.Message, "CRM Reporter Banrural", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Border CreateKpiCard(string title, string value, decimal percent, string tooltip, bool positive)
        {
            var color = positive
                ? new SolidColorBrush(Color.FromRgb(14, 122, 63))
                : new SolidColorBrush(Color.FromRgb(185, 28, 28));

            return new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(6),
                Padding = new Thickness(12),
                ToolTip = tooltip,
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock
                        {
                            Text = value,
                            FontSize = 24,
                            FontWeight = FontWeights.Bold,
                            Foreground = color
                        },
                        new TextBlock
                        {
                            Text = title,
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105))
                        },
                        new ProgressBar
                        {
                            Minimum = 0,
                            Maximum = 100,
                            Value = Convert.ToDouble(percent),
                            Height = 8,
                            Margin = new Thickness(0, 8, 0, 4),
                            ToolTip = tooltip
                        },
                        new TextBlock
                        {
                            Text = percent.ToString("0.##") + "%",
                            Foreground = color,
                            FontWeight = FontWeights.SemiBold
                        }
                    }
                }
            };
        }

        private void FillDistributionPanel(StackPanel panel, DataTable table, string labelColumn, string valueColumn)
        {
            decimal total = 0;
            foreach (DataRow row in table.Rows)
            {
                total += ToDecimal(row[valueColumn]);
            }

            if (total == 0)
            {
                panel.Children.Add(new TextBlock { Text = "Sin datos para mostrar", Foreground = Brushes.Gray });
                return;
            }

            foreach (DataRow row in table.Rows)
            {
                string label = Convert.ToString(row[labelColumn]);
                decimal value = ToDecimal(row[valueColumn]);
                decimal percent = Percent(value, total);

                panel.Children.Add(new TextBlock
                {
                    Text = $"{label}: {value:0} ({percent:0.##}%)",
                    Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                    Margin = new Thickness(0, 4, 0, 2),
                    ToolTip = "Distribucion porcentual segun el total de registros."
                });

                panel.Children.Add(new ProgressBar
                {
                    Minimum = 0,
                    Maximum = 100,
                    Value = Convert.ToDouble(percent),
                    Height = 10,
                    Margin = new Thickness(0, 0, 0, 6)
                });
            }
        }

        private void SetStatus(string message, bool ok)
        {
            StatusText.Text = message;
            StatusText.Foreground = ok
                ? new SolidColorBrush(Color.FromRgb(215, 243, 227))
                : new SolidColorBrush(Color.FromRgb(254, 202, 202));
        }

        private bool ValidateUserForm()
        {
            if (string.IsNullOrWhiteSpace(UserNameBox.Text))
            {
                ShowValidation("El nombre del usuario es obligatorio.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(UserEmailBox.Text) || !UserEmailBox.Text.Contains("@"))
            {
                ShowValidation("Ingrese un correo valido.");
                return false;
            }

            if (UserRoleBox.SelectedValue == null)
            {
                ShowValidation("Seleccione un rol.");
                return false;
            }

            if (_editingUserId == 0 && string.IsNullOrWhiteSpace(UserPasswordBox.Password))
            {
                ShowValidation("La contrasena es obligatoria al crear usuarios.");
                return false;
            }

            return true;
        }

        private bool ValidateAssetForm()
        {
            if (string.IsNullOrWhiteSpace(SerialBox.Text) ||
                string.IsNullOrWhiteSpace(BrandBox.Text) ||
                string.IsNullOrWhiteSpace(ModelBox.Text) ||
                CategoryBox.SelectedValue == null ||
                WarehouseBox.SelectedValue == null ||
                !PurchaseDatePicker.SelectedDate.HasValue)
            {
                ShowValidation("Complete serial, categoria, marca, nombre del equipo, estado, fecha de ingreso y responsable/bodega.");
                return false;
            }

            return true;
        }

        private void ShowValidation(string message)
        {
            SetStatus("Validacion pendiente", false);
            MessageBox.Show(message, "Validacion", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private static string ComboText(ComboBox comboBox)
        {
            return comboBox.SelectedItem is ComboBoxItem item
                ? Convert.ToString(item.Content)
                : Convert.ToString(comboBox.Text);
        }

        private static string DatabaseStatus(string value)
        {
            return value == "Danado" ? "Da\u00f1ado" : value;
        }

        private static string DatabasePriority(string value)
        {
            return value == "Critica" ? "Cr\u00edtica" : value;
        }

        private static string TicketStatusFromResult(string result)
        {
            if (result == "Exitoso")
            {
                return "Resuelto";
            }

            if (result == "No resuelto")
            {
                return "En proceso";
            }

            return "Pendiente";
        }

        private static object DateOrNull(DateTime? value)
        {
            return value.HasValue ? value.Value : (object)DBNull.Value;
        }

        private static decimal Percent(decimal value, decimal total)
        {
            return total <= 0 ? 0 : Math.Round(value * 100 / total, 2);
        }

        private static decimal ToDecimal(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return 0;
            }

            return Convert.ToDecimal(value);
        }

        private static string ColumnText(DataRow row, string columnName, string fallback = "")
        {
            if (row == null || !row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value)
            {
                return fallback;
            }

            var value = Convert.ToString(row[columnName]);
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private bool RoleIs(string role)
        {
            return NormalizeRole(_currentRoleName) == NormalizeRole(role);
        }

        private static string NormalizeRole(string role)
        {
            return (role ?? "")
                .Trim()
                .ToLowerInvariant()
                .Replace("\u00e9", "e")
                .Replace("\u00c9", "e")
                .Replace("\u00ed", "i")
                .Replace("\u00cd", "i")
                .Replace("\u00f3", "o")
                .Replace("\u00d3", "o")
                .Replace("\u00fa", "u")
                .Replace("\u00da", "u");
        }

        private static void SelectFirstIfEmpty(ComboBox comboBox)
        {
            if (comboBox.SelectedIndex < 0 && comboBox.Items.Count > 0)
            {
                comboBox.SelectedIndex = 0;
            }
        }

        private void ClearUserForm()
        {
            UserNameBox.Clear();
            UserLastNameBox.Clear();
            UserEmailBox.Clear();
            UserPasswordBox.Clear();
            UserPhoneBox.Clear();
        }

        private void ClearUserFormInternal()
        {
            ClearUserForm();
            _editingUserId = 0;
            DeleteUserButton.Visibility = Visibility.Collapsed;
            UserPasswordBox.ToolTip = "Obligatoria al crear. En edicion, escriba una nueva solo si desea cambiarla.";
        }

        private void ClearAssetFormInternal()
        {
            AssetIdBox.Clear();
            SerialBox.Clear();
            BrandBox.Clear();
            ModelBox.Clear();
            PurchaseDatePicker.SelectedDate = DateTime.Today;
            WarrantyDatePicker.SelectedDate = null;
            _editingAssetId = 0;
            DeleteAssetButton.Visibility = Visibility.Collapsed;
        }

        private void ClearTicketForm()
        {
            TicketTitleBox.Clear();
            TicketDescriptionBox.Clear();
            TicketDetailsText.Text = "Busque un ID o doble clic en un ticket para ver detalles";
        }

        private void ClearUserForm_Click(object sender, RoutedEventArgs e)
        {
            ClearUserFormInternal();
        }

        private void ClearAssetForm_Click(object sender, RoutedEventArgs e)
        {
            ClearAssetFormInternal();
        }

        private async void DeleteUser_Click(object sender, RoutedEventArgs e)
        {
            if (_editingUserId <= 0)
            {
                ShowValidation("No hay usuario seleccionado.");
                return;
            }

            var result = MessageBox.Show(
                $"Esta seguro de eliminar el usuario {UserNameBox.Text} {UserLastNameBox.Text}?",
                "Confirmar eliminacion",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                await RunSafeAsync(async () =>
                {
                    await ExecuteAsync("DELETE FROM usuarios WHERE id_usuario = @id;", ("@id", _editingUserId));
                    SetStatus("Usuario eliminado correctamente", true);
                    ClearUserFormInternal();
                    await LoadLookupsAsync();
                    await LoadUsersAsync();
                    await LoadDashboardAsync();
                });
            }
        }

        private async void DeleteAsset_Click(object sender, RoutedEventArgs e)
        {
            if (_editingAssetId <= 0)
            {
                ShowValidation("No hay equipo seleccionado.");
                return;
            }

            var result = MessageBox.Show(
                $"Esta seguro de eliminar el equipo {SerialBox.Text}?",
                "Confirmar eliminacion",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                await RunSafeAsync(async () =>
                {
                    await ExecuteAsync("DELETE FROM inventario WHERE id_equipo = @id;", ("@id", _editingAssetId));
                    SetStatus("Equipo eliminado correctamente", true);
                    ClearAssetFormInternal();
                    await LoadInventoryAsync();
                    await LoadDashboardAsync();
                });
            }
        }

        private void UsersGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (UsersGrid.SelectedItem is DataRowView row)
            {
                _editingUserId = Convert.ToInt32(row["id_usuario"]);
                UserNameBox.Text = Convert.ToString(row["nombre"]);
                UserLastNameBox.Text = Convert.ToString(row["apellido"]);
                UserEmailBox.Text = Convert.ToString(row["correo"]);
                UserPhoneBox.Text = Convert.ToString(row["telefono"]);
                UserPasswordBox.Clear();
                UserPasswordBox.ToolTip = "Deje vacio para conservar la contrasena actual.";
                DeleteUserButton.Visibility = Visibility.Visible;

                SelectComboByValue(UserRoleBox, row["rol_id"]);
                SelectComboByText(UserStatusBox, Convert.ToString(row["estado"]));
                SetStatus($"Editando usuario #{_editingUserId}", true);
            }
        }

        private void InventoryGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (InventoryGrid.SelectedItem is DataRowView row)
            {
                _editingAssetId = Convert.ToInt32(row["id_equipo"]);
                AssetIdBox.Text = Convert.ToString(row["id_equipo"]);
                SerialBox.Text = Convert.ToString(row["serial"]);
                BrandBox.Text = Convert.ToString(row["marca"]);
                ModelBox.Text = Convert.ToString(row["modelo"]);
                PurchaseDatePicker.SelectedDate = DateFromRow(row["fecha_ingreso"]);
                WarrantyDatePicker.SelectedDate = DateFromRow(row["garantia_fin"]);
                DeleteAssetButton.Visibility = Visibility.Visible;

                SelectComboByValue(CategoryBox, row["categoria_id"]);
                SelectComboByValue(WarehouseBox, row["bodega_id"]);
                SelectComboByText(AssetStatusBox, DisplayStatus(Convert.ToString(row["estado"])));
                SetStatus($"Editando equipo #{_editingAssetId}", true);
            }
        }

        private async void SearchTicketById_Click(object sender, RoutedEventArgs e)
        {
            await RunSafeAsync(async () =>
            {
                if (!int.TryParse(TicketIdSearchBox.Text.Trim(), out int ticketId))
                {
                    ShowValidation("Ingrese un ID de ticket valido.");
                    return;
                }

                const string sql = @"
                    SELECT t.id_ticket, t.usuario_id, t.tecnico_id, t.titulo, t.descripcion,
                           t.prioridad, t.estado,
                           CONCAT(rep.nombre, ' ', rep.apellido) AS reportado_por,
                           COALESCE(CONCAT(tec.nombre, ' ', tec.apellido), 'Sin asignar') AS tecnico_asignado,
                           COALESCE(t.tipificacion, '') AS tipificacion,
                           COALESCE(t.resultado_atencion, '') AS resultado_atencion,
                           COALESCE(t.descripcion_atencion, '') AS descripcion_atencion,
                           COALESCE(CONCAT(cierre.nombre, ' ', cierre.apellido), 'Sin cierre') AS cerrado_por,
                           t.fecha_creacion, t.fecha_cierre
                    FROM tickets t
                    INNER JOIN usuarios rep ON t.usuario_id = rep.id_usuario
                    LEFT JOIN usuarios tec ON t.tecnico_id = tec.id_usuario
                    LEFT JOIN usuarios cierre ON t.cerrado_por_id = cierre.id_usuario
                    WHERE t.id_ticket = @ticket_id;";

                var data = await QueryAsyncWithParameters(sql, ("@ticket_id", ticketId));
                if (data.Rows.Count > 0)
                {
                    TicketsGrid.ItemsSource = data.DefaultView;
                    LoadTicketIntoForm(data.Rows[0]);
                    SetStatus($"Ticket {ticketId} encontrado", true);
                }
                else
                {
                    TicketsGrid.ItemsSource = null;
                    TicketDetailsText.Text = $"No se encontro ticket con ID {ticketId}.";
                    SetStatus("Ticket no encontrado", false);
                }
            });
        }

        private async void ClearTicketSearch_Click(object sender, RoutedEventArgs e)
        {
            await LoadTicketsAsync();
            TicketIdSearchBox.Clear();
            TicketDetailsText.Text = "Busque un ID o doble clic en un ticket para ver detalles";
            SetStatus("Todos los tickets cargados", true);
        }

        private async void SearchVisitsByTechnician_Click(object sender, RoutedEventArgs e)
        {
            await RunSafeAsync(async () =>
            {
                string technicianName = VisitTechnicianSearchBox.Text.Trim();
                if (string.IsNullOrEmpty(technicianName))
                {
                    ShowValidation("Ingrese el nombre o apellido del tecnico.");
                    return;
                }

                const string sql = @"
                    SELECT v.id_visita, v.tecnico_id, CONCAT(u.nombre, ' ', u.apellido) AS tecnico,
                           a.nombre_agencia, a.region, v.fecha_visita, v.estado, v.descripcion
                    FROM visitasTecnicas v
                    INNER JOIN usuarios u ON v.tecnico_id = u.id_usuario
                    INNER JOIN agencias a ON v.agencia_id = a.id_agencia
                    WHERE CONCAT(u.nombre, ' ', u.apellido) LIKE CONCAT('%', @tecnico, '%')
                    ORDER BY v.fecha_visita DESC;";

                var data = await QueryAsyncWithParameters(sql, ("@tecnico", technicianName));
                if (data.Rows.Count > 0)
                {
                    VisitsGrid.ItemsSource = data.DefaultView;
                    VisitDetailsText.Text = $"Resultados encontrados: {data.Rows.Count}. Doble clic en una fila para ver detalles.";
                    SetStatus($"Se encontraron {data.Rows.Count} visitas", true);
                }
                else
                {
                    VisitsGrid.ItemsSource = null;
                    VisitDetailsText.Text = $"No se encontraron visitas para '{technicianName}'.";
                    SetStatus("Sin visitas encontradas", false);
                }
            });
        }

        private async void ClearVisitSearch_Click(object sender, RoutedEventArgs e)
        {
            await LoadVisitsAsync();
            VisitTechnicianSearchBox.Clear();
            VisitDetailsText.Text = "Doble clic en una visita para ver detalles";
            SetStatus("Todas las visitas cargadas", true);
        }

        private void TicketsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (TicketsGrid.SelectedItem is DataRowView row)
            {
                LoadTicketIntoForm(row.Row);
            }
        }

        private void VisitsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (VisitsGrid.SelectedItem is DataRowView row)
            {
                VisitDetailsText.Text =
                    $"Visita #{row["id_visita"]}\nTecnico: {row["tecnico"]}\nAgencia: {row["nombre_agencia"]}\nEstado: {row["estado"]}\nFecha: {row["fecha_visita"]}\nDetalle: {row["descripcion"]}";
            }
        }

        private void UserTicketsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (UserTicketsGrid.SelectedItem is DataRowView row)
            {
                LoadTicketWorkInfo(row.Row);
                MessageBox.Show(
                    $"Ticket #{row["id_ticket"]}\nCreado por: {row["creado_por"]}\nTecnico: {row["tecnico_asignado"]}\nPrioridad: {row["prioridad"]}\nEstado: {row["estado"]}\nTipificacion: {ColumnText(row.Row, "tipificacion")}\nResultado: {ColumnText(row.Row, "resultado_atencion")}\nCerrado por: {ColumnText(row.Row, "cerrado_por")}\n\nSolicitud:\n{row["descripcion"]}\n\nAtencion:\n{ColumnText(row.Row, "descripcion_atencion")}",
                    "Detalle de solicitud",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void LoadTicketIntoForm(DataRow row)
        {
            TicketTitleBox.Text = Convert.ToString(row["titulo"]);
            TicketDescriptionBox.Text = Convert.ToString(row["descripcion"]);
            SelectComboByValue(TicketReporterBox, row["usuario_id"]);
            SelectComboByValue(TicketTechnicianBox, row["tecnico_id"]);
            SelectComboByText(TicketPriorityBox, DisplayPriority(Convert.ToString(row["prioridad"])));
            SelectComboByText(TicketStatusBox, Convert.ToString(row["estado"]));
            LoadTicketWorkInfo(row);

            TicketDetailsText.Text =
                $"Ticket #{row["id_ticket"]}\nReportado por: {row["reportado_por"]}\nTecnico: {row["tecnico_asignado"]}\nPrioridad: {row["prioridad"]}\nEstado: {row["estado"]}\nTipificacion: {ColumnText(row, "tipificacion")}\nResultado: {ColumnText(row, "resultado_atencion")}\nDetalle: {row["descripcion"]}";
        }

        private void LoadTicketWorkInfo(DataRow row)
        {
            _selectedTicketId = Convert.ToInt32(row["id_ticket"]);
            TicketResolutionTypeBox.SelectedIndex = 0;
            TicketResolutionResultBox.SelectedIndex = 0;
            SelectComboByText(TicketResolutionTypeBox, ColumnText(row, "tipificacion"));
            SelectComboByText(TicketResolutionResultBox, ColumnText(row, "resultado_atencion"));
            TicketWorkDescriptionBox.Text = ColumnText(row, "descripcion_atencion");
            WorkTicketInfoText.Text =
                $"Ticket #{row["id_ticket"]}\nSolicitante: {ColumnText(row, "reportado_por", ColumnText(row, "creado_por"))}\nTecnico: {ColumnText(row, "tecnico_asignado")}\nPrioridad: {row["prioridad"]}\nEstado actual: {row["estado"]}\nFecha: {ColumnText(row, "fecha_creacion")}\n\nSolicitud:\n{row["descripcion"]}";
            TicketWorkMessageText.Text = "Listo para registrar la atencion o cierre.";
        }

        private void SelectComboByValue(ComboBox comboBox, object value)
        {
            if (value == null || value == DBNull.Value)
            {
                comboBox.SelectedIndex = -1;
                return;
            }

            foreach (DataRowView item in comboBox.Items)
            {
                if (Convert.ToString(item[comboBox.SelectedValuePath]) == Convert.ToString(value))
                {
                    comboBox.SelectedItem = item;
                    return;
                }
            }
        }

        private static void SelectComboByText(ComboBox comboBox, string value)
        {
            foreach (var rawItem in comboBox.Items)
            {
                if (rawItem is ComboBoxItem item && Convert.ToString(item.Content) == value)
                {
                    comboBox.SelectedItem = item;
                    return;
                }
            }
        }

        private static DateTime? DateFromRow(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return null;
            }

            return Convert.ToDateTime(value);
        }

        private static string DisplayStatus(string value)
        {
            return value == "Da\u00f1ado" ? "Danado" : value;
        }

        private static string DisplayPriority(string value)
        {
            return value == "Cr\u00edtica" ? "Critica" : value;
        }
    }
}
