namespace CyberManager.Shared;

/// <summary>Idiomas soportados por la aplicación (solo español e inglés).</summary>
public enum AppLanguage
{
    Spanish = 0,
    English = 1
}

/// <summary>
/// Traducciones de la interfaz. Las cadenas de origen están en español y el diccionario
/// se usa en ambos sentidos, de modo que la interfaz puede alternar entre los dos idiomas
/// sin reconstruir los controles.
/// </summary>
public static class L
{
    public static AppLanguage Language { get; private set; } = AppLanguage.Spanish;
    public static bool IsEnglish => Language == AppLanguage.English;
    public static string Code => IsEnglish ? "en" : "es";
    public static string DisplayName => IsEnglish ? "English" : "Español";

    /// <summary>Se dispara cuando cambia el idioma de la interfaz.</summary>
    public static event Action? Changed;

    public static AppLanguage Parse(string? code)
        => string.Equals(code?.Trim(), "en", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(code?.Trim(), "english", StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.English
            : AppLanguage.Spanish;

    public static void SetLanguage(AppLanguage language)
    {
        if (Language == language) return;
        Language = language;
        Changed?.Invoke();
    }

    public static void SetLanguage(string? code) => SetLanguage(Parse(code));

    /// <summary>Traduce una cadena entre español e inglés. Si no existe, la devuelve igual.</summary>
    public static string T(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
        if (IsEnglish && EsToEn.TryGetValue(text, out var english)) return english;
        if (!IsEnglish && EnToEs.TryGetValue(text, out var spanish)) return spanish;
        return text;
    }

    /// <summary>Igual que <see cref="T(string)"/> pero conserva el valor original si es null.</summary>
    public static string? TN(string? text) => text is null ? null : T(text);

    private static readonly Dictionary<string, string> EsToEn = BuildEsToEn();
    private static readonly Dictionary<string, string> EnToEs = BuildReverse();

    private static Dictionary<string, string> BuildReverse()
    {
        var reverse = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in EsToEn)
        {
            if (!reverse.ContainsKey(pair.Value)) reverse[pair.Value] = pair.Key;
        }
        return reverse;
    }

    private static Dictionary<string, string> BuildEsToEn()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // ---------- Navegación y secciones ----------
            ["Inicio"] = "Home",
            ["OPERACIÓN"] = "OPERATIONS",
            ["CATÁLOGO"] = "CATALOG",
            ["PERSONAS"] = "PEOPLE",
            ["SISTEMA"] = "SYSTEM",
            ["Tablero de equipos"] = "Station board",
            ["Punto de venta"] = "Point of sale",
            ["Caja del turno"] = "Shift cash",
            ["Productos y tarifas"] = "Products and rates",
            ["Inventario y kardex"] = "Inventory and kardex",
            ["Compras y proveedores"] = "Purchases and suppliers",
            ["Compras registradas"] = "Recorded purchases",
            ["Equipos y tokens"] = "Stations and tokens",
            ["Equipos y zonas"] = "Stations and zones",
            ["Clientes y saldos"] = "Customers and balances",
            ["Reservas"] = "Reservations",
            ["Fondos de pantalla"] = "Wallpapers",
            ["Videos promocionales"] = "Promo videos",
            ["Reportes"] = "Reports",
            ["Reportes y estadísticas"] = "Reports and statistics",
            ["Configuración"] = "Settings",
            ["Consola de administración"] = "Administration console",
            ["Actividad de los terminales"] = "Station activity",
            ["BITÁCORA"] = "LOG",
            ["0 eventos"] = "0 events",
            ["● En vivo"] = "● Live",
            ["Limpiar"] = "Clear",
            ["Borrar los eventos de la bitácora"] = "Clear log events",
            ["TASA DEL DÍA"] = "TODAY'S RATE",
            ["Conectando..."] = "Connecting...",
            ["Usuario"] = "User",
            ["Administrador"] = "Administrator",
            ["Cajero"] = "Cashier",
            ["Técnico"] = "Technician",
            ["Mi cuenta"] = "My account",
            ["Cerrar sesión"] = "Sign out",
            ["Cerrar"] = "Close",

            // ---------- Acciones comunes ----------
            ["Actualizar"] = "Refresh",
            ["Actualizar lista"] = "Refresh list",
            ["Guardar"] = "Save",
            ["Guardar ahora"] = "Save now",
            ["Guardar cambios"] = "Save changes",
            ["Guardar y aplicar"] = "Save and apply",
            ["Guardar y continuar"] = "Save and continue",
            ["Cancelar"] = "Cancel",
            ["Confirmar"] = "Confirm",
            ["Cerrar"] = "Close",
            ["Editar"] = "Edit",
            ["Eliminar"] = "Delete",
            ["Agregar"] = "Add",
            ["Quitar"] = "Remove",
            ["Nuevo"] = "New",
            ["Copiar"] = "Copy",
            ["Copiar token"] = "Copy token",
            ["Regenerar token"] = "Regenerate token",
            ["Consultar"] = "Check",
            ["Consultar BCV ahora"] = "Check BCV now",
            ["Registrar"] = "Register",
            ["Registrar compra"] = "Record purchase",
            ["Registrar movimiento"] = "Record movement",
            ["Registrar tasa"] = "Record rate",
            ["Exportar CSV"] = "Export CSV",
            ["Imprimir ticket"] = "Print ticket",
            ["Ticket de venta"] = "Sales receipt",
            ["Cobrar"] = "Charge",
            ["Abonar"] = "Deposit",
            ["Recargar"] = "Recharge",
            ["Recargar saldo"] = "Recharge balance",
            ["Desactivar"] = "Deactivate",
            ["Activar / desactivar"] = "Activate / deactivate",
            ["Abrir caja"] = "Open cash drawer",
            ["Abrir carpeta"] = "Open folder",
            ["Restablecer"] = "Reset",
            ["Vaciar"] = "Empty",
            ["Bloquear"] = "Lock",
            ["Probar conexión"] = "Test connection",
            ["Vista previa"] = "Preview",
            ["Reenviar a equipos"] = "Resend to stations",
            ["Reenviar a terminales"] = "Resend to terminals",
            ["Subir imágenes"] = "Upload images",
            ["Subir videos"] = "Upload videos",
            ["Crear respaldo ahora"] = "Create backup now",
            ["Exportar copia"] = "Export copy",
            ["Restaurar seleccionado"] = "Restore selected",
            ["Restaurar desde archivo"] = "Restore from file",
            ["Ver pantalla"] = "View screen",
            ["Ver todo el kardex"] = "View full kardex",
            ["Ver kardex del producto seleccionado"] = "View selected product kardex",
            ["Pedir productos"] = "Order products",
            ["Enviar pedido a la estación"] = "Send order to station",
            ["Aplicar y guardar ahora"] = "Apply and save now",
            ["Nueva categoría"] = "New category",
            ["Nueva reserva"] = "New reservation",
            ["Nueva tarifa"] = "New rate",
            ["Nueva zona"] = "New zone",
            ["Nuevo cliente"] = "New customer",
            ["Nuevo equipo"] = "New station",
            ["Nuevo paquete"] = "New package",
            ["Nuevo producto"] = "New product",
            ["Nuevo usuario"] = "New user",
            ["Cancelar reserva"] = "Cancel reservation",
            ["Cierre diario"] = "Daily closing",
            ["Turno actual"] = "Current shift",
            ["Subir"] = "Move up",
            ["Bajar"] = "Move down",
            ["Orden manual"] = "Manual order",
            ["Versión de CyberManager"] = "CyberManager version",
            ["Versión instalada:"] = "Installed version:",
            ["Desinstalación"] = "Uninstallation",
            ["Desinstalar CyberManager"] = "Uninstall CyberManager",
            ["Cerrar CyberManager"] = "Close CyberManager",
            ["¿Cerrar la consola de administración?"] = "Close the administration console?",
            ["El servidor seguirá ejecutándose en segundo plano."] =
                "The server will keep running in the background.",
            ["Use «Subir» y «Bajar» para el orden manual. Cada equipo necesita su ID y token; use la IP LAN del servidor, no localhost."] =
                "Use “Move up” and “Move down” for manual ordering. Each station needs its ID and token; use the server's LAN IP, not localhost.",
            ["El desinstalador detiene el servidor, el agente y el cliente antes de quitar los archivos. La base de datos del negocio no se elimina."] =
                "The uninstaller stops the server, agent and client before removing files. The business database is not deleted.",

            // ---------- Tablero personalizable ----------
            ["Buscar por nombre, zona, tipo o usuario"] = "Search by name, zone, type or user",
            ["Buscar equipo..."] = "Search station...",
            ["Solo en uso"] = "Only in use",
            ["Métricas"] = "Metrics",
            ["Leyenda"] = "Legend",
            ["Todas las zonas"] = "All zones",
            ["Tipo de equipo"] = "Station type",
            ["Importe"] = "Amount",
            ["↑ Ascendente"] = "↑ Ascending",
            ["↓ Descendente"] = "↓ Descending",
            ["No hay equipos que coincidan con los filtros."] =
                "No stations match the filters.",
            ["Ajuste la búsqueda, la zona o el filtro «Solo en uso»."] =
                "Adjust the search, zone or the “Only in use” filter.",
            ["Equipo libre"] = "Station free",
            ["Sesión en curso"] = "Session in progress",
            ["tiempo restante"] = "time remaining",
            ["tiempo transcurrido"] = "time elapsed",
            ["sin cobro"] = "no charge",
            ["fuera de servicio"] = "out of service",
            ["equipo apagado"] = "station off",

            // ---------- Control remoto del equipo ----------
            ["Control de volumen"] = "Volume control",
            ["Volumen del equipo"] = "Station volume",
            ["Silenciar"] = "Mute",
            ["Activar sonido"] = "Unmute",
            ["Apagar monitor"] = "Turn off monitor",
            ["Encender monitor"] = "Turn on monitor",
            ["Bloquear teclado y mouse"] = "Lock keyboard and mouse",
            ["Desbloquear entrada"] = "Unlock input",
            ["Cerrar programa"] = "Close program",
            ["Volumen"] = "Volume",
            ["Audio"] = "Audio",
            ["Entrada"] = "Input",
            ["Ajuste el volumen maestro de este equipo."] =
                "Adjust the master volume of this station.",
            ["El cambio se aplica de inmediato en el equipo seleccionado."] =
                "The change is applied immediately on the selected station.",

            // ---------- Paquetes de tiempo y Pago Móvil ----------
            ["Paquete de tiempo"] = "Time package",
            ["Sin paquete (minutos manuales)"] = "No package (manual minutes)",
            ["Pago Móvil"] = "Mobile payment",

            // ---------- Campos y etiquetas ----------
            ["Acciones"] = "Actions",
            ["Arquitectura"] = "Architecture",
            ["Caja"] = "Cash",
            ["Cantidad"] = "Quantity",
            ["Categoría"] = "Category",
            ["Cliente"] = "Customer",
            ["Clientes"] = "Customers",
            ["Código"] = "Code",
            ["Concepto"] = "Concept",
            ["Contraseña"] = "Password",
            ["Costo unitario (USD)"] = "Unit cost (USD)",
            ["Cuadros de artículos (80 a 160 %)"] = "Product cards (80 to 160%)",
            ["Cuadros de artículos y terminales"] = "Product and station cards",
            ["Cuadros de terminales (80 a 160 %)"] = "Station cards (80 to 160%)",
            ["Datos de conexión del servidor"] = "Server connection details",
            ["Datos del negocio"] = "Business details",
            ["Desde"] = "From",
            ["Descuento (USD)"] = "Discount (USD)",
            ["Dirección del servidor"] = "Server address",
            ["Documento"] = "Document",
            ["Estado"] = "Status",
            ["Existencias"] = "Stock",
            ["Facturación"] = "Invoicing",
            ["Fecha"] = "Date",
            ["Forma de pago"] = "Payment method",
            ["Fuente"] = "Source",
            ["Historial"] = "History",
            ["Historial (últimos 30 días)"] = "History (last 30 days)",
            ["Kardex general"] = "General kardex",
            ["Mensaje"] = "Message",
            ["Menú lateral"] = "Sidebar",
            ["Modo"] = "Mode",
            ["MONEDA BASE"] = "BASE CURRENCY",
            ["Monto recibido (Bs)"] = "Amount received (Bs)",
            ["Movimientos"] = "Movements",
            ["Nombre"] = "Name",
            ["Notas"] = "Notes",
            ["Notas de apertura"] = "Opening notes",
            ["Notas del ticket"] = "Receipt notes",
            ["Número de equipo (StationId)"] = "Station number (StationId)",
            ["Orden"] = "Order",
            ["Paleta de colores"] = "Color palette",
            ["Pasos sugeridos"] = "Suggested steps",
            ["Presentación"] = "Presentation",
            ["Producto"] = "Product",
            ["Productos"] = "Products",
            ["Proveedores"] = "Suppliers",
            ["Referencia"] = "Reference",
            ["Resumen de la sesión"] = "Session summary",
            ["Saldo"] = "Balance",
            ["Servidor"] = "Server",
            ["Sesión activa"] = "Active session",
            ["Solo los administradores pueden modificar la configuración y gestionar usuarios. "] =
                "Only administrators can change settings and manage users. ",
            ["Su pedido"] = "Your order",
            ["Tamaño de iconos (12 a 26)"] = "Icon size (12 to 26)",
            ["Tamaño del texto del menú (11 a 17)"] = "Menu text size (11 to 17)",
            ["Tasa (Bs por USD)"] = "Rate (Bs per USD)",
            ["Tasa del día"] = "Today's rate",
            ["Tasa del día (Bs por USD)"] = "Today's rate (Bs per USD)",
            ["Tasa oficial del BCV"] = "Official BCV rate",
            ["Tasa sin definir"] = "Rate not set",
            ["Tema predefinido"] = "Theme preset",
            ["Tiempo"] = "Time",
            ["TIEMPO RESTANTE"] = "TIME REMAINING",
            ["Tipo"] = "Type",
            ["Tipo de movimiento"] = "Movement type",
            ["Token del equipo"] = "Station token",
            ["Total"] = "Total",
            ["Zona"] = "Zone",
            ["Zona estándar"] = "Standard zone",
            ["IMPUESTOS"] = "TAXES",
            ["IGTF 3%"] = "IGTF 3%",
            ["IVA 16% · IGTF 3%"] = "VAT 16% · IGTF 3%",
            ["EQUIPO BLOQUEADO"] = "STATION LOCKED",
            ["PC-01"] = "PC-01",
            ["Consumos"] = "Consumptions",
            ["CONSUMO"] = "CONSUMPTION",
            ["Referencia"] = "Reference",
            ["Modo"] = "Mode",
            ["Todos los importes se guardan en USD."] = "All amounts are stored in USD.",

            // ---------- Estados y tipos ----------
            ["Sin conexión"] = "Offline",
            ["Disponible"] = "Available",
            ["En uso"] = "In use",
            ["En pausa"] = "Paused",
            ["Mantenimiento"] = "Maintenance",
            ["Reservado"] = "Reserved",
            ["Consola"] = "Console",
            ["Otro"] = "Other",
            ["Prepago"] = "Prepaid",
            ["Postpago"] = "Postpaid",
            ["Cortesía"] = "Courtesy",
            ["Efectivo Bs"] = "Cash Bs",
            ["Efectivo USD"] = "Cash USD",
            ["Tarjeta"] = "Card",
            ["Transferencia"] = "Transfer",
            ["Saldo del cliente"] = "Customer balance",
            ["Mixto"] = "Mixed",
            ["Pendiente"] = "Pending",
            ["Ticket"] = "Receipt",
            ["Factura"] = "Invoice",
            ["Nota de entrega"] = "Delivery note",
            ["Inicial"] = "Initial",
            ["Compra"] = "Purchase",
            ["Venta"] = "Sale",
            ["Ajuste"] = "Adjustment",
            ["Merma"] = "Shrinkage",
            ["Devolución"] = "Refund",
            ["Apertura"] = "Opening",
            ["Depósito"] = "Deposit",
            ["Retiro"] = "Withdrawal",
            ["Gasto"] = "Expense",
            ["Venta de tiempo"] = "Time sale",
            ["Confirmada"] = "Confirmed",
            ["En curso"] = "In progress",
            ["Completada"] = "Completed",
            ["Cancelada"] = "Cancelled",
            ["No asistió"] = "No-show",
            ["Recarga"] = "Recharge",
            ["Crédito"] = "Credit",
            ["Manual"] = "Manual",
            ["BCV"] = "BCV",
            ["Paralelo"] = "Parallel",
            ["Agente de Windows"] = "Windows agent",
            ["Estación de trabajo"] = "Workstation",
            ["Configuración del equipo"] = "Station setup",
            ["Configurar esta estación"] = "Set up this station",
            ["Consola de administración"] = "Administration console",
            ["CyberManager - Equipo bloqueado"] = "CyberManager - Station locked",
            ["CyberManager - Sesión"] = "CyberManager - Session",
            ["CyberManager · Administración"] = "CyberManager · Administration",
            ["CyberManager · Iniciar sesión"] = "CyberManager · Sign in",
            ["CyberManager · pantalla de equipo"] = "CyberManager · station screen",
            ["CyberManager · Venezuela"] = "CyberManager · Venezuela",
            ["Gestión integral de cybercafé"] = "Complete cybercafé management",
            ["Controle cada equipo,\ncada minuto y cada bolívar."] =
                "Control every station,\nevery minute and every bolivar.",
            ["Sesiones prepago y postpago, caja en bolívares y dólares, inventario, facturación venezolana y reportes en tiempo real."] =
                "Prepaid and postpaid sessions, cash in bolivars and dollars, inventory, Venezuelan invoicing and real-time reports.",
            ["Iniciar sesión"] = "Sign in",
            ["Ingrese sus credenciales para abrir la consola."] = "Enter your credentials to open the console.",
            ["Entrar a la consola"] = "Enter console",
            ["Acceso inicial: usuario admin y contraseña admin123. Cambie la contraseña en Configuración › Usuarios."] =
                "Initial access: user admin and password admin123. Change it in Settings › Users.",
            ["Indique el usuario y la contraseña."] = "Enter the user name and password.",
            ["Indique un nombre para el fondo."] = "Enter a name for the wallpaper.",
            ["Indique un nombre para el video."] = "Enter a name for the video.",
            ["Indique una tasa mayor que cero."] = "Enter a rate greater than zero.",
            ["La cantidad debe ser un número mayor que cero."] = "The quantity must be a number greater than zero.",
            ["La fecha final no puede ser anterior a la inicial."] = "The end date cannot be before the start date.",
            ["La sesión ya no está activa."] = "The session is no longer active.",
            ["La caja debe estar abierta para cobrar."] = "Cash must be open to charge.",
            ["Debe registrar la tasa de cambio del día antes de vender."] =
                "You must record the exchange rate of the day before selling.",
            ["Seleccione un cliente de la lista."] = "Select a customer from the list.",
            ["Seleccione un equipo de la lista."] = "Select a station from the list.",
            ["Seleccione un producto."] = "Select a product.",
            ["Seleccione un producto para ver su kardex."] = "Select a product to view its kardex.",
            ["Seleccione un respaldo de la lista."] = "Select a backup from the list.",
            ["Seleccione un usuario de la lista."] = "Select a user from the list.",
            ["Seleccione un video"] = "Select a video",
            ["Seleccione una reserva de la lista."] = "Select a reservation from the list.",
            ["Seleccione un fondo"] = "Select a wallpaper",
            ["Seleccione el cliente al que se cargará el consumo."] = "Select the customer to charge the consumption to.",
            ["Seleccione el fondo que se mostrará de forma fija."] = "Select the wallpaper to show as fixed background.",
            ["Seleccione la fecha de la tasa."] = "Select the rate date.",
            ["Cargando..."] = "Loading...",
            ["Cargando clientes..."] = "Loading customers...",
            ["Cargando equipos..."] = "Loading stations...",
            ["Cargando galería..."] = "Loading gallery...",
            ["Cargando inventario..."] = "Loading inventory...",
            ["Cargando reservas..."] = "Loading reservations...",
            ["Cargando videos..."] = "Loading videos...",
            ["Cargue la configuración para comenzar."] = "Load the settings to begin.",
            ["Consultando..."] = "Checking...",
            ["Consultando caja..."] = "Checking cash...",
            ["Consultando carpeta..."] = "Checking folder...",
            ["Consultando tasa..."] = "Checking rate...",
            ["Aún no hay fondos. Use «Subir imágenes» para agregar el primero."] =
                "No wallpapers yet. Use “Upload images” to add the first one.",
            ["Aún no hay videos. Use «Subir videos» para agregar el primero."] =
                "No videos yet. Use “Upload videos” to add the first one.",
            ["Haga clic en una imagen de la galería para editar sus datos."] =
                "Click a wallpaper in the gallery to edit its details.",
            ["Haga clic en un video de la galería para editar sus datos."] =
                "Click a video in the gallery to edit its details.",
            ["Suba una imagen PNG, JPG, GIF o BMP (máximo 20 MB)."] =
                "Upload a PNG, JPG, GIF or BMP image (maximum 20 MB).",
            ["Suba un video MP4, M4V, WMV o AVI (máximo 300 MB)."] =
                "Upload an MP4, M4V, WMV or AVI video (maximum 300 MB).",
            ["Los cambios se guardan automáticamente en este equipo."] =
                "Changes are saved automatically on this computer.",
            ["Los cambios se guardan automáticamente unos instantes después de editarlos. "] =
                "Changes are saved automatically a moment after editing. ",
            ["Los terminales reciben los cambios automáticamente."] =
                "Terminals receive the changes automatically.",
            ["Los respaldos se guardan en el servidor."] = "Backups are stored on the server.",
            ["La galería se reenvió a todos los equipos conectados."] =
                "The gallery was resent to all connected stations.",
            ["La lista de videos se reenvió a todos los terminales conectados."] =
                "The video list was resent to all connected terminals.",
            ["La carpeta de respaldos aún no está disponible."] = "The backup folder is not available yet.",
            ["No se seleccionó una impresora."] = "No printer was selected.",
            ["Texto copiado al portapapeles."] = "Text copied to clipboard.",
            ["Sin cambios pendientes."] = "No pending changes.",
            ["Debe indicar el token del equipo."] = "You must enter the station token.",
            ["El número de equipo debe ser un número mayor que cero."] =
                "The station number must be a number greater than zero.",
            ["El número de equipo y el token se copian desde la consola de administración, en Equipos › Token. El token es único para cada PC."] =
                "The station number and token are copied from the administration console, under Stations › Token. The token is unique for each PC.",
            ["Configuración / token"] = "Setup / token",
            ["Datos de conexión del servidor"] = "Server connection details",
            ["Solicite al cajero para iniciar su sesión."] = "Ask the cashier to start your session.",
            ["¡Tiempo agotado!"] = "Time is up!",
            ["¿Vaciar el ticket actual?"] = "Empty the current ticket?",
            ["Doble clic en una reserva para editarla."] = "Double-click a reservation to edit it.",
            ["Registre al menos un equipo antes de crear reservas."] =
                "Register at least one station before creating reservations.",
            ["Cierre la sesión antes de poner el equipo en mantenimiento."] =
                "Close the session before putting the station under maintenance.",
            ["Cierre la sesión activa antes de poner el equipo en mantenimiento."] =
                "Close the active session before putting the station under maintenance.",
            ["Cierre la sesión antes de poner el equipo en mantenimiento."] =
                "Close the session before putting the station under maintenance.",
            ["Orden de apagado enviada."] = "Shutdown order sent.",
            ["Orden de cierre de sesión enviada."] = "Sign-out order sent.",
            ["Orden de reinicio enviada."] = "Restart order sent.",
            ["Actualización automática desde el BCV disponible."] =
                "Automatic update from BCV is available.",
            ["Credenciales de demostración"] = "Demo credentials",
            ["El IGTF (3% por defecto) se aplica solo cuando el pago se registra en efectivo en dólares. "] =
                "IGTF (3% by default) applies only when payment is recorded as cash in dollars. ",
            ["El saldo se descuenta cuando el cliente paga con «Saldo del cliente»."] =
                "The balance is deducted when the customer pays with “Customer balance”.",
            ["Elija un tema predefinido o escriba colores hexadecimales (por ejemplo #22D3EE). "] =
                "Choose a preset theme or enter hexadecimal colors (for example #22D3EE). ",
            ["Los importes en Bs usan la tasa registrada en cada venta."] =
                "Amounts in Bs use the rate recorded for each sale.",
            ["Presets: Cyber, Neón, Esmeralda, Ámbar, Océano y Grafito."] =
                "Presets: Cyber, Neon, Emerald, Amber, Ocean and Graphite.",
            ["Verde: disponible · Celeste: en uso · Ámbar: en pausa · Violeta: mantenimiento"] =
                "Green: available · Cyan: in use · Amber: paused · Purple: maintenance",
            ["SESIÓN EN PAUSA"] = "SESSION PAUSED",
            ["SISTEMA"] = "SYSTEM",
            ["Estación"] = "Station",
            ["Solicite al cajero para iniciar su sesión."] = "Ask the cashier to start your session.",
            ["No hay conexión con el servidor. Avise al administrador."] =
                "No connection to the server. Please notify the administrator.",
            ["Conectando con el servidor..."] = "Connecting to the server...",
            ["sin sesión activa"] = "no active session",
            ["No se pudo conectar:"] = "Could not connect:",
            ["Conexión correcta con"] = "Connection successful with",
            ["Idioma de la estación"] = "Station language",
            ["Idioma de la interfaz"] = "Interface language",
            ["Solo se admiten español e inglés. La consola se actualiza al instante."] =
                "Only Spanish and English are supported. The console updates instantly.",
            ["SESIÓN"] = "SESSION",
            ["TIEMPO TRANSCURRIDO"] = "ELAPSED TIME",
            ["Sesión activa"] = "Active session",
            ["Cliente"] = "Customer",
            ["Todas las categorías"] = "All categories",
            ["No se pudo cargar el catálogo"] = "Could not load the catalog",
            ["Prepago"] = "Prepaid",
            ["Postpago"] = "Postpaid",
            ["Cortesía"] = "Courtesy",

            // ---------- Actualizaciones ----------
            ["Versión"] = "Version",
            ["Nueva versión disponible"] = "New version available",
            ["Hay una nueva versión disponible:"] = "A new version is available:",
            ["¿Desea actualizar ahora?"] = "Do you want to update now?",
            ["Versión publicada:"] = "Published:",
            ["Actualización disponible"] = "Update available",
            ["Actualización"] = "Update",
            ["Actualizaciones"] = "Updates",
            ["Última versión:"] = "Latest version:",
            ["No hay versiones publicadas todavía."] = "No versions have been published yet.",
            ["Ya tiene la última versión."] = "You already have the latest version.",
            ["No se pudo consultar GitHub."] = "Could not check GitHub.",
            ["Se reintentará automáticamente en la próxima comprobación."] =
                "It will be retried automatically on the next check.",
            ["Publique una versión en GitHub para activar los avisos."] =
                "Publish a release on GitHub to enable reminders.",
            ["Los recordatorios de nuevas versiones están desactivados."] =
                "New version reminders are disabled.",
            ["Aviso oculto para esta versión. Se mostrará con la próxima."] =
                "Reminder hidden for this version. It will show again for the next one.",
            ["El aviso se muestra al abrir la consola."] =
                "The reminder is shown when the console opens.",
            ["El aviso aparecerá el"] = "The reminder will appear on",
            ["El aviso aparecerá cuando GitHub informe la fecha de publicación."] =
                "The reminder will appear once GitHub reports the release date.",
            ["Avisar cuando haya una nueva versión disponible"] =
                "Notify when a new version is available",
            ["Horas de espera después de la publicación"] =
                "Hours to wait after publication",
            ["Buscar actualizaciones ahora"] = "Check for updates now",
            ["Consultando GitHub..."] = "Checking GitHub...",
            ["El servidor consulta GitHub automáticamente."] =
                "The server checks GitHub automatically.",
            ["CyberManager consulta las versiones publicadas en GitHub una sola vez, al abrir la consola. Cuando aparece una versión nueva, la consola pregunta si desea actualizar y, si acepta, abre la página de versiones del proyecto. Use «Buscar actualizaciones ahora» si desea comprobarlo manualmente."] =
                "CyberManager checks the versions published on GitHub only once, when the console opens. When a new version appears, the console asks whether to update and, if accepted, opens the project's releases page. Use “Check for updates now” to check manually.",
            ["El aviso aparece al abrir la consola una vez transcurridas las horas de espera (2 por defecto). Puede desactivarlo o cambiar la espera; al responder «No» se oculta ese aviso hasta la próxima versión."] =
                "The reminder appears when the console opens, once the waiting hours have passed (2 by default). You can disable it or change the wait; answering “No” hides that reminder until the next version.",

            // ---------- Descripciones emergentes de los botones ----------
            ["Movimiento"] = "Movement",
            ["Cerrar caja"] = "Close register",
            ["Capturar ahora"] = "Capture now",
            ["Actualizar automáticamente"] = "Refresh automatically",
            ["Borrar los eventos de la bitácora"] = "Clear the log events",
            ["Cambia el orden de la lista."] = "Changes the order of the list.",
            ["Aumenta la cantidad."] = "Increases the quantity.",
            ["Disminuye la cantidad."] = "Decreases the quantity.",
            ["Quita el producto del pedido."] = "Removes the product from the order.",
            ["Agregar el producto al pedido."] = "Adds the product to the order.",
            ["Muestra la ayuda de esta pantalla."] = "Shows help for this screen.",
            ["Registra un abono a la cuenta del cliente."] = "Records a payment to the customer account.",
            ["Abre la caja del turno actual."] = "Opens the current shift register.",
            ["Abre la caja del turno."] = "Opens the shift register.",
            ["Abre la carpeta donde se guardó el archivo."] = "Opens the folder where the file was saved.",
            ["Guarda los datos del formulario."] = "Saves the form data.",
            ["Activa o desactiva el registro seleccionado."] = "Enables or disables the selected record.",
            ["Activa el audio del equipo."] = "Turns on the station audio.",
            ["Vuelve a cargar la información desde el servidor."] = "Reloads the information from the server.",
            ["Actualiza la lista con los datos más recientes."] = "Refreshes the list with the latest data.",
            ["Añade el elemento a la lista."] = "Adds the item to the list.",
            ["Apaga el equipo."] = "Shuts down the station.",
            ["Apaga el monitor del equipo."] = "Turns off the station monitor.",
            ["Enciende el monitor del equipo."] = "Turns on the station monitor.",
            ["Aplica los cambios y los guarda de inmediato."] = "Applies and saves the changes immediately.",
            ["Mueve el elemento una posición hacia abajo."] = "Moves the item one position down.",
            ["Mueve el elemento una posición hacia arriba."] = "Moves the item one position up.",
            ["Bloquea el acceso del equipo."] = "Locks access to the station.",
            ["Bloquea la pantalla del equipo."] = "Locks the station screen.",
            ["Bloquea el teclado y el mouse del equipo."] = "Locks the station keyboard and mouse.",
            ["Consulta GitHub si hay una versión nueva."] = "Checks GitHub for a new version.",
            ["Cierra sin guardar cambios."] = "Closes without saving changes.",
            ["Cierra la ventana."] = "Closes the window.",
            ["Anula la reserva seleccionada."] = "Cancels the selected reservation.",
            ["Toma una captura de la pantalla remota."] = "Takes a screenshot of the remote screen.",
            ["Cierra la caja y muestra el resumen del turno."] = "Closes the register and shows the shift summary.",
            ["Cierra el programa indicado en el equipo."] = "Closes the selected program on the station.",
            ["Cierra la sesión de Windows del equipo."] = "Signs out of the station Windows session.",
            ["Abre el cierre de caja del día."] = "Opens the daily register closing.",
            ["Cobra la sesión o la venta actual."] = "Charges the current session or sale.",
            ["Cobra la sesión y libera el equipo."] = "Charges the session and frees the station.",
            ["Muestra la configuración y el token del equipo."] = "Shows the station settings and token.",
            ["Confirma la operación."] = "Confirms the operation.",
            ["Consulta los datos seleccionados."] = "Looks up the selected data.",
            ["Descarga la tasa oficial del BCV."] = "Downloads the official BCV rate.",
            ["Abre el control de volumen del equipo."] = "Opens the station volume control.",
            ["Copia el texto al portapapeles."] = "Copies the text to the clipboard.",
            ["Copia el token de vinculación del equipo."] = "Copies the station linking token.",
            ["Crea una copia de seguridad de la base de datos."] = "Creates a database backup.",
            ["Desactiva el registro seleccionado."] = "Disables the selected record.",
            ["Devuelve el control del teclado y mouse."] = "Restores keyboard and mouse control.",
            ["Quita la aplicación y sus datos de este equipo."] = "Removes the app and its data from this PC.",
            ["Abre el registro seleccionado para editarlo."] = "Opens the selected record for editing.",
            ["Elimina el registro seleccionado."] = "Deletes the selected record.",
            ["Muestra un mensaje en el equipo."] = "Shows a message on the station.",
            ["Envía un mensaje al equipo."] = "Sends a message to the station.",
            ["Envía el pedido al administrador."] = "Sends the order to the administrator.",
            ["Inicia sesión en la consola."] = "Signs in to the console.",
            ["Guarda una copia de la base de datos en un archivo."] = "Saves a copy of the database to a file.",
            ["Exporta los datos a un archivo CSV."] = "Exports the data to a CSV file.",
            ["Añade tiempo a la sesión activa."] = "Adds time to the active session.",
            ["Guarda los cambios pendientes."] = "Saves the pending changes.",
            ["Guarda los cambios del formulario."] = "Saves the form changes.",
            ["Guarda y aplica la configuración."] = "Saves and applies the settings.",
            ["Guarda la configuración y abre el equipo."] = "Saves the settings and opens the station.",
            ["Muestra el historial del registro."] = "Shows the record history.",
            ["Imprime un ticket de la venta actual."] = "Prints a ticket for the current sale.",
            ["Inicia una sesión en el equipo."] = "Starts a session on the station.",
            ["Limpia la búsqueda o el filtro."] = "Clears the search or filter.",
            ["Pone el equipo en mantenimiento."] = "Puts the station under maintenance.",
            ["Muestra el consumo de la sesión."] = "Shows the session consumption.",
            ["Registra un ingreso o egreso de caja."] = "Records a register income or expense.",
            ["Muestra los movimientos de caja."] = "Shows the register movements.",
            ["Crea una categoría de productos."] = "Creates a product category.",
            ["Crea una reserva de equipo."] = "Creates a station reservation.",
            ["Crea una tarifa por tiempo."] = "Creates an hourly rate.",
            ["Crea una zona del cyber."] = "Creates a cyber zone.",
            ["Crea un registro nuevo."] = "Creates a new record.",
            ["Registra un cliente nuevo."] = "Registers a new customer.",
            ["Registra un equipo nuevo."] = "Registers a new station.",
            ["Crea un paquete de tiempo."] = "Creates a time package.",
            ["Registra un producto nuevo."] = "Registers a new product.",
            ["Crea un usuario de la consola."] = "Creates a console user.",
            ["Pausa el tiempo de la sesión."] = "Pauses the session time.",
            ["Abre el pedido de productos."] = "Opens the product order.",
            ["Marca el equipo en mantenimiento."] = "Marks the station under maintenance.",
            ["Comprueba la conexión con el servidor."] = "Checks the connection to the server.",
            ["Quita el elemento de la lista."] = "Removes the item from the list.",
            ["Devuelve el equipo a servicio."] = "Returns the station to service.",
            ["Reanuda el tiempo de la sesión."] = "Resumes the session time.",
            ["Recarga la información de la pantalla."] = "Reloads the screen information.",
            ["Añade saldo a la cuenta del cliente."] = "Adds balance to the customer account.",
            ["Oculta el aviso durante 2 horas."] = "Hides the reminder for 2 hours.",
            ["Vuelve a enviar la orden a los equipos."] = "Resends the order to the stations.",
            ["Vuelve a enviar la orden a las terminales."] = "Resends the order to the terminals.",
            ["Genera un token nuevo para el equipo."] = "Generates a new token for the station.",
            ["Registra la compra de productos."] = "Records the product purchase.",
            ["Guarda una nueva tasa de cambio."] = "Saves a new exchange rate.",
            ["Reinicia el equipo."] = "Restarts the station.",
            ["Restaura los valores predeterminados."] = "Restores the default values.",
            ["Restaura la base de datos desde un archivo."] = "Restores the database from a file.",
            ["Restaura la copia de seguridad seleccionada."] = "Restores the selected backup.",
            ["Silencia el audio del equipo."] = "Mutes the station audio.",
            ["Agrega imágenes a la galería."] = "Adds images to the gallery.",
            ["Agrega videos a las promociones."] = "Adds videos to the promotions.",
            ["Cambia la sesión a otro equipo."] = "Moves the session to another station.",
            ["Quita todos los productos del pedido."] = "Removes all products from the order.",
            ["Muestra los movimientos del producto."] = "Shows the product movements.",
            ["Muestra los movimientos de todos los productos."] = "Shows the movements of all products.",
            ["Abre la página de la nueva versión."] = "Opens the new version page.",
            ["Abre la pantalla remota del equipo."] = "Opens the station remote screen.",
            ["Muestra la pantalla del equipo."] = "Shows the station screen.",
            ["Muestra una vista previa del contenido."] = "Shows a preview of the content.",
            ["Abre el tablero de equipos."] = "Opens the station dashboard.",
            ["Abre el punto de venta."] = "Opens the point of sale.",
            ["Gestiona productos y tarifas."] = "Manages products and rates.",
            ["Consulta el inventario y el kardex."] = "Views the inventory and stock ledger.",
            ["Gestiona compras y proveedores."] = "Manages purchases and suppliers.",
            ["Administra equipos y tokens."] = "Manages stations and tokens.",
            ["Gestiona clientes y saldos."] = "Manages customers and balances.",
            ["Gestiona las reservas de equipos."] = "Manages station reservations.",
            ["Gestiona los fondos de pantalla."] = "Manages wallpapers.",
            ["Gestiona los videos promocionales."] = "Manages promotional videos.",
            ["Consulta los reportes del negocio."] = "Views the business reports.",
            ["Abre la configuración del sistema."] = "Opens the system settings.",
            ["Buscar producto por nombre o código"] = "Search for a product by name or code",
            ["Número que aparece en Administración > Equipos"] =
                "Number shown in Administration > Stations",
        };

        return map;
    }
}
