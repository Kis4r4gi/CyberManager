# Estado del proyecto y continuación automática

> Última actualización: 2026-11-09
> Este archivo permite retomar el trabajo exactamente donde quedó. Léalo completo antes de continuar.

## Objetivo

Sistema de gestión para cyber cafés y centros de videojuegos en Venezuela: servidor central en
red local, consola de administración/caja, cliente bloqueado por estación y agente de Windows
para control remoto. Todo el dinero contable en **USD**, con tasa Bs/USD diaria, IVA 16 % e IGTF
3 % sobre pagos en divisas, e instaladores separados por componente para Windows (servidor y
estación), unificados por arquitectura (x86/x64).

## Estado: COMPLETADO, INSTALADO Y VERIFICADO

La solución compila con **0 errores y 0 advertencias**. Se generan **dos instaladores unificados**
(Servidor y Estación), cada uno con los payloads de x64 y x86 y selección automática de la
arquitectura al instalar. El **servidor está instalado y corriendo** como tarea programada en este
equipo (`C:\CyberManager\Servidor`, puerto 5080).

---

## Arquitectura

| Proyecto | Responsabilidad |
|----------|-----------------|
| `CyberManager.Server` | ASP.NET Core 8 minimal API, EF Core + SQLite, JWT, SignalR. |
| `CyberManager.Admin` | Consola WPF de administración, caja, inventario y reportes. |
| `CyberManager.Client` | Cliente WPF de estación: pantalla de bloqueo, sesión y pedidos. |
| `CyberManager.Agent` | Servicio de Windows: apagado, reinicio, cierre de sesión, mensajes y métricas. |
| `CyberManager.Desktop` | Cliente HTTP tipado, SignalR, MVVM, tema oscuro y utilidades WPF. |
| `CyberManager.Shared` | Entidades, enums, DTOs e inspección de imágenes. |

Hubs: `/hubs/admin` (JWT), `/hubs/agent` y `/hubs/station` (token de equipo por query).
Base de datos: `AppContext.BaseDirectory/cybermanager.db` (SQLite). La primera ejecución siembra
datos de demostración.

Credenciales demo: `admin` / `admin123` y `cajero` / `cajero123`.

---

## Funcionalidades implementadas

### Operación y estaciones

- **Tablero con los terminales al principio**: cuadrícula de tarjetas rectangulares (320×140
  escaladas) antes de los indicadores resumidos, con textos recortados y ajustados para evitar
  superposiciones. Cada tarjeta muestra estado, tiempo transcurrido/restante e importe acumulado,
  y siempre incluye un bloque de tiempo (sesión, `00:00` libre o `—` fuera de servicio).
- **Tablero personalizable**: la barra de configuración queda oculta tras el botón
  **Configuración** del encabezado; incluye buscador, orden por
  manual/nombre/estado/zona/tipo/tiempo/importe, sentido ascendente o descendente, filtro por zona,
  «Solo en uso», tamaño de tarjetas (80–160 %) y visibilidad de métricas y leyenda. Las
  preferencias se guardan en `appearance.json` y no se pierden al cambiar de tema, tamaño, idioma
  ni al restablecer la apariencia.
- **Barras laterales ajustables**: el menú de navegación y la bitácora de terminales se redimensionan
  arrastrando los separadores, con anchos mínimos y máximos.
- **Control remoto del terminal**: volumen maestro, silenciar, reactivar sonido, apagar/encender
  monitor, bloquear/desbloquear teclado y mouse, y cerrar un programa por nombre. El audio usa
  Core Audio, el monitor `WM_SYSCOMMAND` y el bloqueo de entrada hooks de bajo nivel; los comandos
  se enrutan al cliente interactivo, mientras apagado/reinicio/cierre de sesión siguen en el
  agente de Windows.
- Sesiones prepago, postpago y cortesía; pausar, reanudar, extender, transferir y finalizar.
- Paquetes de tiempo con minutos de bono, seleccionables al iniciar la sesión; el paquete cobra
  su precio por adelantado y suma los minutos de bono. Reservas y clientes con saldo.
- Formas de pago venezolanas: efectivo Bs/USD, tarjeta, transferencia, **Pago Móvil**, saldo del
  cliente, mixto y pendiente. Pago Móvil se trata como cobro electrónico: suma a los totales de
  transferencia y no al efectivo esperado en caja.
- Consumos cargados a la sesión (`Pending`) y cobrados al cerrar, o venta directa en POS.
- **Consolas de videojuegos**: `StationKind.Pc/Console/Other`, plataforma y cantidad de mandos.
  El tablero muestra tipo, plataforma y mandos; el alta/edición de equipos los persiste.
- Mensajes y órdenes de energía/bloqueo al cliente y al agente.
- Galería de fondos de pantalla con modos `None`/`Single`/`Slideshow`, rotación y sincronización
  en tiempo real; imágenes en SQLite y caché SHA-256 en el cliente.
- **Videos promocionales** en terminales bloqueados: tabla `PromoVideos` en SQLite, subida
  multipart (`.mp4`, `.m4v`, `.wmv`, `.avi`, máx. 300 MB), orden, activación, reenvío por SignalR
  (`PromoVideosChanged`) y descarga con token de equipo. El cliente los reproduce silenciados y
  los cachea por SHA-256 en `%LOCALAPPDATA%\CyberManager\Client\promos`.
- **Localización español/inglés**: diccionario bidireccional `CyberManager.Shared.L`, traducción
  del árbol visual `Desktop.Services.Loc.Apply`, selector en *Configuración → Apariencia* y en la
  configuración de cada estación. Los instaladores Inno Setup incluyen ambos idiomas.

### Caja, ventas y facturación

- Apertura/cierre de caja con fondo, esperado, contado y diferencia.
- Formas de pago: `Cash`, `CashUsd`, `Card`, `Transfer`, `PagoMovil`, `CustomerBalance`, `Mixed`
  y `Pending`.
- Precios con IVA incluido, RIF del negocio y del cliente, numeración de facturas y notas de
  entrega. IGTF solo en pagos en efectivo en dólares.
- Descuentos, devoluciones y gastos de caja.
- **Lector de códigos de barras** como teclado: Enter con SKU/nombre exacto agrega al POS.
- **Tickets de 80 mm** para venta y cierre diario con `FlowDocument`/`PrintDialog`; copias de
  1 a 5, impresora configurable e impresión automática opcional.

### Inventario, compras y proveedores

- Productos por categoría con SKU, costo, precio, stock, stock mínimo y servicios sin stock.
- Compras que actualizan stock y costo; kardex de movimientos.
- **Saldos de proveedores** = compras − pagos, con abonos en efectivo, tarjeta o transferencia,
  referencia, notas e historial. Los abonos en efectivo requieren caja abierta y mueven caja;
  los no efectivos no. Saldo negativo = anticipo a favor.
- Alertas de stock bajo en el tablero y reporte de inventario valorizado.

### Reportes, cierre diario y personalización

- Reportes de ventas por día, productos, equipos, inventario y cajas, con rango de fechas y CSV.
- **Cierre diario** (`/api/reports/daily-closing`) con ventas de productos/tiempo, totales USD y
  Bs, IVA, IGTF, descuentos, ticket promedio, sesiones, caja, gastos, abonos a proveedores,
  desglose por forma de pago y productos más vendidos. Se imprime en 80 mm desde
  *Reportes → Cierre diario*.
- **Apariencia**: seis presets, doce colores, tamaño de iconos/texto de navegación,
  **tamaño de los cuadros de artículos y terminales** (80 a 160 %, escala dimensiones, relleno,
  esquinas y tipografía) e **idioma español/inglés**, con restablecimiento. Se guarda por usuario en
  `%LOCALAPPDATA%\CyberManager\Admin\appearance.json` y se aplica antes del login.
  Los cambios se guardan automáticamente (800 ms después de editar) y los bordes usan un tono
  más claro (`#33517A`) con migración automática del valor antiguo `#24344F`.
- **Tasa oficial del BCV**: `BcvRateService` descarga `https://www.bcv.org.ve/`, extrae el valor
  del dólar (`div#dolar strong.strong-tb`) y la fecha valor (`property="dc:date"`), valida el
  rango y registra la tasa del día como `RateSource.Bcv`. `BcvRateWorker` la revisa al arrancar y
  cada 30 minutos; la casilla `BcvAutoUpdate` (por defecto activa) la desactiva. El botón
  **Consultar BCV ahora** fuerza la consulta desde *Configuración → Tasa de cambio*.
- **Icono compartido**: `assets/CyberManager.ico` (16 a 256 px) aplicado a Admin, Cliente,
  Servidor y Agente con `<ApplicationIcon>`, y a ambos instaladores con `SetupIconFile`.
  Se regenera con `scripts/generate-icon.ps1`.
- La ventana principal usa la **barra de título nativa de Windows** (modo oscuro en Windows
  10/11), por lo que Minimizar, Maximizar/Restaurar y Cerrar son los botones del sistema y siempre
  están visibles. Se encuadra en el área de trabajo visible (sin quedar bajo la barra de tareas) y
  se reajusta al cambiar DPI o la barra de tareas.
- **Bitácora de terminales** en el panel derecho: eventos con hora (sesiones, conexión,
  mantenimiento, comandos, avisos, tiempo agotado y cambios de estado), con limpieza manual.
- **Visualizador de pantalla remoto**: solicitud por SignalR a la estación, captura GDI en la
  sesión del usuario, JPEG en base64 de vuelta al servidor y vista en vivo cada 2 segundos.
  Disponible desde la ficha del equipo en el tablero y desde *Equipos*.
- **Respaldos**: copia manual/automática con la API en línea de SQLite, exportación, restauración
  desde lista o archivo, respaldo de seguridad previo, retención configurable y carpeta
  `respaldos` junto a la base. Panel en *Configuración → Respaldos*.
- **Autoguardado** de la configuración del negocio y de la apariencia, con botones para forzar el
  guardado inmediato.
- Usuarios con roles administrador, cajero y técnico.

### Instaladores y despliegue

- Inno Setup 6 con **dos instaladores unificados** que mantienen la separación por componente:
  - `installer/CyberManager.Servidor.iss` → servidor + consola de administración.
  - `installer/CyberManager.Estacion.iss` → cliente + agente de Windows.
  - Cada instalador incluye los payloads de `dist\x64` y `dist\x86`; `[Files]` usa
    `Check: IsX64InstallMode` / `IsX86InstallMode` para copiar solo el que corresponde.
    `ArchitecturesAllowed=x86compatible` permite x86/x64 y
    `ArchitecturesInstallIn64BitMode=x64compatible` activa el modo de 64 bits en equipos x64.
    Incluyen español e inglés en `[Languages]` y `[CustomMessages]`.
  - `[InstallDelete]` retira los binarios nativos de la arquitectura opuesta al reinstalar, sin
    tocar la base de datos del servidor ni la configuración local de la estación.
- `scripts/build-installer.ps1` publica ambos payloads (`dist\x64` y `dist\x86`), el desinstalador
  independiente y compila los dos instaladores unificados; `-Architecture` acepta `all` (por
  defecto), `x64` o `x86`, y las dos últimas solo se usan con `-SkipInstaller` para publicaciones
  de diagnóstico.
- **Versión automática**: `version.props` (`CyberManagerVersion`) es la fuente única.
  `Directory.Build.props` la aplica a `Version`, `FileVersion` e `InformationalVersion`, y
  `AppInfo.Version` la expone en tiempo de ejecución (sin el sufijo `+hash` del SDK). Cada
  empaquetado incrementa el patch (`1.0.0` → `1.0.1` → `1.0.2`…). `-Version x.y.z` fija un valor y
  `-NoBump` conserva el actual; con `-SkipPublish` tampoco se incrementa.
- **Lanzador único**: el instalador copia `Server\CyberManager.Server.exe` como
  `Server\CyberManager.exe`. El acceso directo principal ejecuta `CyberManager.exe
  --launch-console`: si el servidor no responde, lo inicia en segundo plano, espera `/api/health`
  y abre `Admin\CyberManager.Admin.exe`. Si ya estaba activo, solo abre la consola. También hay
  accesos directos separados para la consola y para el servidor en primer plano.
- **Actualización segura**: ambos instaladores usan `CloseApplications=no` y su propio código.
  `PrepareToInstall` detecta procesos con `tasklist`, pide confirmación (salvo en modo silencioso),
  detiene la tarea programada/servicio y los procesos, y `CurStepChanged(ssDone)` vuelve a iniciar
  el servidor, el agente, el cliente y/o la consola que estaban activos.
- **Desinstalación**: `CyberManager.Uninstaller` es un ejecutable autocontenido que busca las
  entradas de Inno Setup en el registro, detiene los procesos y ejecuta los desinstaladores en
  silencio. Se copia a `%ProgramData%\CyberManager\CyberManager.Uninstaller.exe`, se enlaza en el
  menú Inicio y en **Configuración → Instalación**, y se programa para borrarse al terminar.
  Conserva la base de datos del negocio y la configuración local.
- Rutas por defecto: `C:\CyberManager\Servidor` y `C:\CyberManager\Estacion` (pueden convivir).
- Tareas del servidor: tarea programada `CyberManagerServer` (SYSTEM, al inicio) y regla de
  firewall TCP 5080 creada por el instalador o automáticamente en el primer inicio elevado.
- **Elevación del servidor**: al abrir el ejecutable por primera vez sin privilegios se relanza con
  UAC y se escribe `%ProgramData%\CyberManager\server-elevated.marker`; las tareas SYSTEM y los
  inicios con `--no-elevate`/`--service` se omiten. `run-dev.ps1` arranca con `--no-elevate`.
- **Cierre con confirmación**: la consola pide confirmación antes de cerrarse y aclara que el
  servidor sigue en segundo plano; Windows puede cerrarla sin preguntar al apagar o cerrar sesión.
  El servidor en primer plano también pide confirmación al recibir Ctrl+C.
- Tareas de la estación: inicio del cliente con Windows y configuración del agente
  (`configurar-agente.ps1`): ID, token y URL LAN.
- Desinstalación selectiva: `desinstalar-servicios.ps1 -Servidor` o `-Agente`. El servidor
  conserva la base de datos; la estación conserva la configuración local del cliente.
- Instaladores generados:
  - `artifacts/CyberManager-Servidor-1.0.4.exe` (~158,8 MB, payloads x64 + x86).
  - `artifacts/CyberManager-Estacion-1.0.4.exe` (~140,7 MB, payloads x64 + x86).
- Ambos instaladores llevan `SetupIconFile=..\assets\CyberManager.ico` y los ejecutables
  publicados en ambas arquitecturas incluyen el icono en sus recursos.
- **Reducción de tamaño**: las publicaciones omiten PDB y XML de documentación, usan
  `SatelliteResourceLanguages=en;es` y el script elimina recursos satélite de otros idiomas. El
  agente ya no fija `RuntimeIdentifier=win-x64`; la arquitectura se decide al publicar.

### Recordatorio de versiones y sincronización (1.0.2)

- **Aviso de nueva versión**: `UpdateService` consulta las versiones publicadas en
  `Kis4r4gi/CyberManager` y guarda el resultado en memoria durante 10 minutos para no agotar el
  límite de la API de GitHub. `UpdateReminderWorker` revisa cada 15 minutos y, cuando la versión
  más reciente supera la instalada y han pasado las horas de espera, envía el evento SignalR
  `UpdateAvailable` a las consolas.
- **Espera de 2 horas**: las claves `UpdateReminderEnabled` (activado), `UpdateReminderDelayHours`
  (2) y `UpdateReminderDismissedVersion` controlan el aviso. El recordatorio aparece en una barra
  superior de la consola con «Ver novedades» y «Recordar luego»; el segundo oculta esa versión
  hasta la próxima.
- **Configuración**: **Configuración → Instalación → Actualizaciones** permite activar/desactivar,
  cambiar la espera, consultar GitHub inmediatamente y ver la fecha en que aparecerá el aviso.
- **API**: `GET /api/updates/latest?refresh=true|false` y `POST /api/updates/dismiss`. Solo lectura
  de datos públicos del repositorio; no se envían datos del negocio.
- **Sincronización automática**: `scripts/sync-github.ps1` hace `git add`, commit (si hay cambios),
  `pull --rebase --autostash` y `push`. Con `-InstallTask` registra la tarea
  `CyberManagerGitSync` cada 15 minutos con la sesión del usuario; `-RemoveTask` la elimina y
  `-NoPush` crea solo el commit local. El registro va a
  `%LOCALAPPDATA%\CyberManager\logs\github-sync.log`.

---

## Verificación realizada

- Compilación de `CyberManager.sln`: **0 errores y 0 advertencias**. La advertencia EF1002 del
  sembrador se suprime localmente porque los nombres de tabla/columna son constantes internas.
- Pruebas de API: login, tablero, cierre diario, saldos de proveedores, compra, abonos
  (tarjeta/transferencia), detalle de proveedores en el cierre, alta/lectura/baja de consola.
- Pruebas de arranque de Admin y Client WPF.
- Publicación autocontenida e instaladores unificados generados correctamente.

### Prueba del instalador de servidor (2026-11-05)

- Instalación silenciosa de `CyberManager-Servidor-1.0.0.exe` con las tareas
  `desktopicon,serverstartup,firewall`: código de salida **0**.
- Instalado en `C:\CyberManager\Servidor` (servidor, consola, scripts y README).
- Tarea programada `CyberManagerServer`: registrada como **SYSTEM al inicio** y en estado
  **Running**.
- Regla de firewall `CyberManager Server 5080`: activa, entrada TCP, perfiles
  Dominio/Privado/Público.
- Servidor escuchando en `0.0.0.0:5080`; `GET /api/health` responde `{"status":"ok"}` tanto en
  `127.0.0.1` como en la IP LAN `192.168.1.7`.
- Login `admin` / `admin123`: correcto (rol Admin).
- Base creada en `C:\CyberManager\Servidor\Server\cybermanager.db` con datos de demostración:
  16 productos, 4 categorías, 10 estaciones, 3 tasas, 2 usuarios y configuración inicial.
- Accesos directos de escritorio y menú Inicio creados.
- Consola de administración: login correcto y **tablero abre sin errores**.
- Corregido un fallo de WPF en `DashboardView` que cerraba la aplicación al iniciar sesión:
  el `ScrollViewer` se agregaba al `DockPanel` sin quitarlo antes de su elemento padre lógico
  (`El elemento especificado ya es el elemento secundario lógico de otro elemento`).
- Instaladores recompilados con la corrección incluida.
- Datos de prueba eliminados de SQLite: proveedores, compras, abonos, movimientos de stock y
  secuencias; producto 2 restaurado a costo 0,25 y stock 30.

### Pruebas de la iteración de respaldos, apariencia y tablero (2026-11-05)

- `POST /api/backups`: respaldo manual creado (≈264 KB); `GET /api/backups` lista los archivos.
- Descarga del respaldo y `POST /api/backups/{archivo}/restore`: correctos; el servidor sigue
  respondiendo y el login funciona después de restaurar.
- `POST /api/backups/restore-upload` con un `.db` externo: correcto; se creó el respaldo
  `antes_restaurar` antes de sobrescribir.
- Corregido bloqueo de archivo durante la restauración: las conexiones SQLite de respaldo usan
  `Pooling = false` y la validación abre el archivo con `FileShare.ReadWrite | Delete`.
- Respaldo automático verificado: el servidor generó `cybermanager_*_auto.db` al vencer el
  intervalo y el panel muestra 6 respaldos.
- Autoguardado del negocio verificado por UI: cambiar `BusinessName` persistió en la API en ~3 s.
- Autoguardado de apariencia verificado por UI: cambiar el borde a `#3A5C88` actualizó
  `appearance.json` en ~3 s; se restauró a `#33517A`.
- Migración del borde antiguo verificada: al reiniciar la consola, `appearance.json` pasó de
  `#24344F` a `#33517A`.
- Botones de ventana visibles: `Minimizar` (1654,11), `Maximizar` (1714,11) y `Cerrar` (1774,11),
  todos dentro del área de trabajo; ventana `85,10–1835,1010`.
- Tablero: las tarjetas sin sesión muestran bloque de tiempo y texto de estado; las fuera de
  línea muestran `—` y «equipo apagado».
- `BusinessAddress` restaurado a `Av. Principal, Local 1` tras una prueba de UI que lo modificó
  por error.

### Pruebas de la iteración de barra nativa, bitácora y pantalla remota (2026-11-05)

- La consola usa la barra de título nativa de Windows; UIAutomation encuentra el botón nativo
  **Minimizar** y la ventana respeta el área de trabajo: `35,15–1885,1005` físicos con área útil
  `0,0–1920,1020` (DPI 125 %), sin quedar debajo de la barra de tareas.
- El panel derecho **BITÁCORA** se muestra en la ventana principal y registra eventos con hora.
- Prueba completa del visualizador remoto: se inició el cliente WPF como `PC-01`, un cliente
  SignalR de administración solicitó la captura y recibió un JPEG de `1000×563` px y 17 665 bytes
  a través del servidor, sin errores. La configuración temporal del cliente se eliminó al terminar.
- Prueba de la interfaz del visualizador dentro de la consola: con una estación simulada conectada,
  se abrió *Equipos y tokens → Ver pantalla* y la ventana **Pantalla remota · PC-01** mostró la
  imagen con el estado `Actualizado … 1000×563 px · 0,0 s de retardo` y el subtítulo
  `PC-01 · captura en vivo`; la actualización automática cada 2 s quedó funcionando.
- Servidor y consola publicados y desplegados en `C:\CyberManager\Servidor`; servidor en ejecución
  y `/api/health` responde `ok`.
- Límite de mensajes SignalR elevado a 4 MB para transportar las capturas JPEG en base64.

### Pruebas de la iteración de cuadros, iconos y tasa BCV (2026-11-05)

- `AppearanceSettings` incluye `ProductCardScale` y `StationCardScale`, limitadas a 80–160 %.
  La pestaña **Apariencia** muestra cuatro deslizadores: iconos, texto del menú, cuadros de
  artículos y cuadros de terminales.
- Prueba de UI: los deslizadores 3 y 4 aceptaron `140` y `130`; `appearance.json` guardó
  `ProductCardScale=140` y `StationCardScale=130`; las tarjetas del tablero pasaron a 130 % y los
  cuadros del POS a 140 %. Después se restablecieron a `100` / `100`.
- `scripts/generate-icon.ps1` generó `assets/CyberManager.ico` (7 entradas: 16, 24, 32, 48, 64,
  128 y 256 px; 363,8 KB) y se verificó que Admin, Cliente, Servidor y Agente publicados tienen
  icono asociado.
- Se consultó el BCV en vivo: `1 USD = 896,1225 Bs`, fecha valor `06/11/2026`, registrado como
  tasa del `05/11/2026`. La segunda consulta devolvió `updated=false` y conservó la tasa.
- La API `/api/exchange-rates/today` devolvió `rate=896,1225`, `source=Bcv`; la clave
  `BcvAutoUpdate=true` existe en la base instalada. En la consola, la casilla aparece activa y el
  estado muestra «La tasa del BCV no cambió: 1 USD = 896,12 Bs · fecha valor 06/11/2026.».
- Compilación correcta (solo la advertencia EF1002 preexistente en `DbSeeder.cs`).

### Pruebas de la iteración de tablero, promos, idiomas y x86/x64 (2026-11-06)

- `dotnet build CyberManager.sln`: **0 errores y 0 advertencias**.
- Publicación `-Architecture all` completada para los cuatro proyectos en `dist\x64` y `dist\x86`;
  se generaron los **dos instaladores unificados** con Inno Setup (tamaños indicados arriba), sin
  advertencias de compilación.
- Prueba del mecanismo de selección de arquitectura con un instalador Inno de prueba que usa las
  mismas directivas (`ArchitecturesAllowed=x86compatible`,
  `ArchitecturesInstallIn64BitMode=x64compatible`) y funciones `Check`: en este equipo x64
  registró `64-bit install mode: Yes` y copió únicamente el marcador x64. La ruta x86 es simétrica
  y se activa en Windows de 32 bits.
- El instalador real conserva la separación servidor/estación: `CyberManager-Servidor-1.0.0.exe`
  incluye Server + Admin y `CyberManager-Estacion-1.0.0.exe` incluye Client + Agent, cada uno con
  ambos payloads.
- Prueba de API con el servidor x64 publicado y una base temporal en el puerto 5099:
  - `/api/health` → `ok`; login `admin` / `admin123` → rol `Admin`.
  - `GET /api/promo-videos/playlist` → 0 videos inicialmente.
  - Subida de un MP4 de prueba → id 1, SHA-256 calculado, 12 bytes.
  - La lista pasó a 1 video con revisión generada; `GET /api/promo-videos/{id}/file` → HTTP 200.
  - Eliminación del video → la lista volvió a 0.
- Prueba de la biblioteca de idioma cargando `CyberManager.Shared.dll` publicado:
  `Iniciar sesión` → `Sign in`, `Guardar y aplicar` → `Save and apply`, y vuelta a español
  correctamente. El diccionario no lanza excepciones por claves duplicadas.
- Arranque del `CyberManager.Admin.exe` x64 publicado: el proceso permanece activo en la pantalla
  de inicio de sesión (sin fallos de inicialización); se cerró al terminar la prueba.
- Prueba x86: `CyberManager.Admin.exe` x86 inicia y permanece activo; el servidor x86 publicado
  responde `ok` en `/api/health` sobre una base temporal en el puerto 5098.
- `-Clean` eliminó `bin` y `obj` al final para reducir el tamaño del repositorio; las carpetas se
  regeneran al compilar.

### Pruebas de la iteración de tablero personalizable, Pago Móvil y control remoto (2026-11-06)

- `dotnet build CyberManager.sln`: **0 errores y 0 advertencias** tras añadir el tablero
  personalizable, los comandos remotos, Pago Móvil y la elevación del servidor.
- `PaymentMethod.PagoMovil` agregado a inicio/cierre/extensión de sesión, clientes, compras, POS y
  recargas; `SaleService`, `CashService` y reportes lo agrupan con los cobros electrónicos y no con
  el efectivo esperado.
- Paquetes de tiempo: el diálogo de inicio carga el paquete activo, fija los minutos con bono, el
  importe por adelantado y la forma de pago, incluido Pago Móvil.
- Control remoto: el servidor enruta `SetVolume`, `Mute`, `Unmute`, `LockInput`, `UnlockInput`,
  `MonitorOff`, `MonitorOn` al cliente interactivo; `Shutdown`, `Restart`, `Logoff` y `Screenshot`
  siguen en el agente. `KillProcess` se envía a ambos.
- El cliente implementa Core Audio (`IAudioEndpointVolume`), apagado de monitor
  (`WM_SYSCOMMAND`/`SC_MONITORPOWER`) y bloqueo de teclado/mouse con hooks de bajo nivel; el bloqueo
  se libera al cerrar sesión o salir de la aplicación.
- Elevación: `Elevation.cs` detecta administrador, relanza con `runas` solo en el primer inicio
  interactivo, omite servicios y `--no-elevate`, valida el puerto desde argumentos, entorno o
  `appsettings.json` y crea la regla de Firewall idempotente. El marcador se escribe solo si el
  relanzamiento tuvo éxito y su error de escritura no interrumpe el arranque.
- Instaladores recompilados con todo el código de esta iteración:
  `CyberManager-Servidor-1.0.0.exe` (~151,3 MB) y `CyberManager-Estacion-1.0.0.exe` (~133,2 MB),
  sin advertencias ni errores de Inno Setup.
- El servidor publicado en `dist\x64\Server` se inició con `--no-elevate` y una base temporal:
  `/api/health` respondió `ok`, el login funcionó y `/api/dashboard` devolvió 10 estaciones.
- Prueba pendiente: ejecutar los instaladores reales y la elevación UAC requieren una sesión con
  permisos de administrador.

### Pruebas de la iteración de versión, lanzador, desinstalador y orden manual (2026-11-06)

- `version.props` pasó de `1.0.0` a `1.0.1` automáticamente al ejecutar
  `scripts\build-installer.ps1`; `CyberManager.Server.exe`, `CyberManager.Admin.exe`,
  `CyberManager.Uninstaller.exe` y los binarios x86 reportan `1.0.1` en sus propiedades.
- `dotnet build CyberManager.sln`: 0 errores y 0 advertencias con el nuevo proyecto
  `CyberManager.Uninstaller` y los cambios de lanzador, cierre, orden manual y desinstalación.
- Publicación del desinstalador recortado: `dist\x64\Uninstaller\CyberManager.Uninstaller.exe`
  (~10,5 MB), autocontenido y con manifiesto `requireAdministrator`.
- Instaladores recompilados sin errores de Inno Setup:
  `CyberManager-Servidor-1.0.1.exe` (~158,8 MB) y `CyberManager-Estacion-1.0.1.exe` (~140,7 MB).
- Prueba de API con base temporal y puerto 5195: `/api/health` → `ok`, `/` → versión `1.0.1`,
  login correcto, 10 estaciones, `POST /api/stations/reorder` invirtió el orden y
  `GET /api/stations` devolvió la secuencia esperada con `SortOrder` 1…10.
- Prueba del lanzador con `--launch-console --no-elevate` y puerto 5196: el proceso lanzador
  terminó, el servidor hijo quedó activo (`/api/health` → `ok`) y se abrió
  `CyberManager.Admin.exe`. Los procesos de prueba se cerraron al finalizar.
- Empaquetado final con `-NoBump` tras los últimos ajustes (confirmación de cierre, reinicio en
  `ssDone`, desinstalador y documentación): ambos instaladores recompilaron sin errores y las
  pruebas de API, orden manual y lanzador se repitieron con éxito sobre el `dist` recién publicado.
  El archivo `Server\CyberManager.exe` del lanzador lo crea el instalador al copiar el apphost; en
  `dist` solo existe `CyberManager.Server.exe`, que acepta igualmente `--launch-console`.
- Pendiente: la ejecución real de los instaladores (confirmación de cierre, reinicio de procesos y
  desinstalación) requiere una sesión elevada.

### Pruebas de la iteración de recordatorio de versiones y sincronización (2026-11-07)

- `dotnet build CyberManager.sln`: **0 errores y 0 advertencias** con las clases
  `UpdateService`, `UpdateReminderService`, `UpdateReminderWorker`, el DTO compartido, los
  endpoints y el aviso de la consola.
- Servidor de prueba con base temporal y puerto 5197: `GET /api/updates/latest` leyó la release
  real `v1.0.1` de GitHub (`currentVersion=1.0.1`, `latestVersion=1.0.1`, `isNewer=false`,
  `reminderEnabled=true`, `delayHours=2`, `reminderAtUtc=publishedUtc+2h`), sin errores.
- `POST /api/updates/dismiss` guardó `UpdateReminderDismissedVersion=1.0.1` y devolvió
  `isDismissed=true`; `GET /api/settings` confirmó las tres claves nuevas con sus valores por
  defecto.
- `UpdateReminderWorker` se ejecutó en segundo plano y consultó la API de GitHub sin afectar el
  arranque; no emitió aviso porque la versión instalada era la última.
- La consulta a GitHub se cachea 10 minutos y solo lee datos públicos del repositorio; el registro
  no mostró advertencias ni errores de la API.
- El script de sincronización se probó de extremo a extremo con el commit de esta iteración
  (ver sección Publicación).
- `scripts/build-installer.ps1` subió `version.props` de `1.0.1` a `1.0.2` y generó sin errores
  `CyberManager-Servidor-1.0.2.exe` (~158,8 MB) y `CyberManager-Estacion-1.0.2.exe` (~140,7 MB),
  ambos con los payloads x64 y x86.
- Release `v1.0.2` publicada con ambos instaladores; el recordatorio de 2 horas toma como
  referencia la fecha `publishedUtc` de esa release.

### Corrección de contraste y ayudas emergentes (1.0.3, 2026-11-07)

- **Cajas de selección**: `Dark.xaml` ahora define plantillas oscuras propias para `CheckBox`,
  `RadioButton`, `ComboBox`, `ComboBoxItem` y `DatePickerTextBox`. El fondo usa
  `InputBrush` (`#0B1424`) y el texto `TextBrush` (`#E8EFFA`), con un contraste medido de
  **7,85:1**; las etiquetas ya no desaparecen sobre fondos claros. También se dibuja el foco,
  el estado deshabilitado y la lista desplegable con la misma paleta.
- **Ayudas emergentes**: el estilo `BaseButton` aplica `ToolTipService.InitialShowDelay=2000`
  y `ShowDuration=15000`; el estilo `ToolTip` usa fondo oscuro, borde de acento y texto ajustado.
  `Kit.Button` y `Kit.SmallButton` asignan la descripción desde el mapa `ButtonTips` de
  `UiKit.cs`, y los botones creados directamente (`FormDialog`, `StationActionsDialog`,
  `CashView`, `ScreenViewerWindow`) y los de XAML también la reciben.
- **Cobertura**: se verificó por reflexión que los **93 botones** creados con `Kit.Button`
  tienen descripción y que **todos los botones XAML** incluyen `ToolTip`. Las descripciones se
  guardan en español y `L.T`/`Loc.Apply` las traducen al inglés; se comprobó que no queda
  ninguna cadena sin traducción.
- **Compilación**: `dotnet build CyberManager.sln` terminó con **0 errores y 0 advertencias**.
- **Instaladores**: `scripts/build-installer.ps1` subió `version.props` a `1.0.3` y generó
  `CyberManager-Servidor-1.0.3.exe` (~158,8 MB) y `CyberManager-Estacion-1.0.3.exe`
  (~140,7 MB), con payloads x64 y x86.
- Release `v1.0.3` publicada con ambos instaladores en
  <https://github.com/Kis4r4gi/CyberManager/releases/tag/v1.0.3> el `2026-11-07T13:02:06Z`;
  el recordatorio de 2 horas aparecerá a las `15:02:06Z`.

### Cierre diario por correo y ayudas solo en español (1.0.4, 2026-11-08)

- **Envío del cierre diario por correo**: `EmailService` arma el resumen del día en HTML y adjunta
  el detalle en CSV. Usa `System.Net.Mail.SmtpClient` (sin dependencias nuevas), con host, puerto,
  SSL/TLS, usuario, contraseña, remitente y nombre configurables. El envío es idempotente por
  fecha mediante `DailyReportEmailLastSentDate`; `force=true` permite reenviar.
- **Disparo automático**: `ReportsView` lee `DailyReportEmailEnabled` y `DailyClosingDialog` envía
  el correo al abrirse. El diálogo muestra el estado y permite **Reenviar por correo**; si el
  envío falla, el cierre y la caja continúan sin interrupción.
- **Configuración**: nueva pestaña **Reportes por correo** en `SettingsView` con activación,
  destinatarios, datos SMTP, **Guardar ahora**, **Enviar correo de prueba** y **Borrar contraseña
  guardada**. La contraseña nunca se devuelve en `GET /api/settings`; solo se envía cuando se
  escribe una nueva.
- **Endpoints**: `POST /api/reports/daily-closing/email?date=...&force=...` y
  `POST /api/reports/daily-closing/email/test`, ambos solo para administradores. Los DTO están en
  `CyberManager.Shared/Dtos/Notifications.cs` y los métodos en `CyberApi`.
- **Ayudas de botones solo en español**: `Kit.Tip` ya no traduce el texto y `Loc.Apply` omite los
  `ToolTip` de `ButtonBase`, por lo que las descripciones de botones se muestran siempre en
  español aunque la interfaz esté en inglés. `FormDialog` fija sus dos ayudas en español.
- **Globo flotante a los 2 segundos**: el estilo `ToolTip` de `Dark.xaml` usa `Placement=Mouse`,
  desplazamiento respecto al cursor, esquinas redondeadas y sombra; `BaseButton` aplica
  `InitialShowDelay=2000`, `ShowDuration=15000`, `BetweenShowDelay=200` y `ShowOnDisabled=True`.
  No hace falta hacer clic: basta con dejar el cursor sobre el botón.
- **Pruebas**: con un servidor temporal y un SMTP local simulado se verificó que el envío
  desactivado se omite; sin SMTP la API responde **400** controlado; con SMTP inaccesible responde
  **502** controlado; el correo de prueba y el cierre con CSV adjunto se envían; el segundo envío
  sin `force` se omite y con `force=true` se reenvía. También se comprobó que
  `GET /api/settings` no incluye `SmtpPassword`.
- **Compilación**: `dotnet build CyberManager.sln` terminó con **0 errores y 0 advertencias**.
- **Instaladores**: `scripts/build-installer.ps1` subió `version.props` a `1.0.4` y generó
  `CyberManager-Servidor-1.0.4.exe` (166.552.643 bytes, ~158,8 MB) y
  `CyberManager-Estacion-1.0.4.exe` (147.543.719 bytes, ~140,7 MB), con payloads x64 y x86.
- Release `v1.0.4` publicada con ambos instaladores en
  <https://github.com/Kis4r4gi/CyberManager/releases/tag/v1.0.4> el `2026-11-08T23:46:40Z`;
  el recordatorio de 2 horas quedará para `2026-11-09T01:46:40Z`.

### Interfaz compacta, cierre configurable y guardado manual (1.0.5, 2026-11-08)

- **Cuadros de selección compactos**: `Dark.xaml` reduce `ComboBox` a 28 px de alto, padding
  `8,2`, fuente 12,5 y flecha de 10 px; `ComboBoxItem` usa padding `8,4` y la misma fuente. Las
  opciones ya no incluyen detalles dinámicos (precio de tarifa, saldo de cliente, stock de
  producto, zona de equipo, minutos/precio de paquete): solo muestran el nombre a elegir.
- **Formato del cierre diario**: nueva clave `DailyClosingFormat` (`ticket80`, `a4`, `carta`) en
  `SettingsKeys`, `TicketSettings` y el selector de *Configuración → Negocio → Ticket y operación*.
  `TicketPrinter.NormalizeClosingFormat` valida el valor y `NewDocument` ajusta `PageWidth`,
  márgenes y fuente (302 px / 794 px / 816 px). El ticket de venta sigue en 80 mm y el botón del
  cierre pasa a **Imprimir cierre**.
- **Guardado solo con «Aplicar y guardar»**: se eliminaron los temporizadores de autoguardado de
  negocio y apariencia. Los cambios quedan como borrador o vista previa; el botón global del
  encabezado y los botones locales de Negocio, Reportes por correo y Apariencia llaman a
  `SaveAllAsync`, que valida la apariencia y guarda ambos bloques en una sola acción. El correo de
  prueba usa los valores actuales del formulario mediante `TestEmailRequest` ampliado y no guarda
  nada; si faltan datos SMTP, `EmailService.LoadConfigAsync` completa con la configuración
  guardada. «Borrar contraseña guardada» queda pendiente hasta el guardado.
- **Barra del tablero en menú desplegable**: `DashboardView` oculta la barra de personalización y
  agrega el botón **Configuración** / **Ocultar configuración** en el encabezado. Los controles se
  compactaron (buscador 200, orden 120, tamaño 72, zona 130).
- **Barras laterales ajustables**: `MainWindow.xaml` usa columnas `228/4/*/4/322` con
  `MinWidth`/`MaxWidth` y dos `GridSplitter` (`ResizeDirection=Columns`) para el menú de navegación
  y la bitácora de terminales.
- **Pruebas**: `dotnet build CyberManager.sln` terminó con **0 errores y 0 advertencias**. Con un
  servidor temporal en el puerto 5099 se comprobó que `DailyClosingFormat=a4` persiste y se
  devuelve en `GET /api/settings`, que `SmtpPassword` no se expone y que el correo de prueba con
  SMTP transitorio `127.0.0.1:1` responde **502** (usa los datos del formulario sin guardarlos).
- **Pendiente de verificación visual**: splitters, desplegable del tablero y tamaño de los
  `ComboBox` en pantalla; el envío real a Gmail/Outlook requiere credenciales del usuario.
- **Instaladores**: `scripts/build-installer.ps1` subió `version.props` a `1.0.5` y generó
  `CyberManager-Servidor-1.0.5.exe` (166.576.316 bytes, ~158,9 MB) y
  `CyberManager-Estacion-1.0.5.exe` (147.552.394 bytes, ~140,7 MB), con payloads x64 y x86.
- Release `v1.0.5` publicada con ambos instaladores en
  <https://github.com/Kis4r4gi/CyberManager/releases/tag/v1.0.5> el `2026-11-09T01:09:13Z`;
  el recordatorio de 2 horas quedará para `2026-11-09T03:09:13Z`.

### Actualizaciones al abrir y versión en el título (1.0.6, 2026-11-09)

- **Consulta única al abrir**: se eliminó `UpdateReminderWorker`, el servicio periódico que
  consultaba GitHub cada 15 minutos y enviaba `UpdateAvailable` por SignalR. La consola hace una
  consulta forzada una sola vez en `MainWindow_Loaded` y el temporizador del encabezado ya no
  refresca el aviso cada 20 segundos. El aviso se muestra al abrir la consola cuando ya pasaron
  las horas de espera; el botón **Buscar actualizaciones ahora** sigue disponible para una
  consulta manual.
- **Sin consultas al abrir Configuración**: `GET /api/updates/latest?cached=true` devuelve solo el
  resultado en memoria. `SettingsView` usa esa variante al cargar y reserva `refresh=true` para el
  botón manual; así no se consulta GitHub por el simple hecho de entrar a la pestaña Instalación.
- **Versión en la barra de título**: la consola muestra `CyberManager · Administración · v1.0.6`
  (traducido al inglés) y la pantalla de acceso `CyberManager · Iniciar sesión · v1.0.6`. La
  versión de la barra lateral y de **Configuración → Instalación** se mantiene.
- **Textos**: la pestaña Instalación explica que la comprobación ocurre solo al abrir la consola y
  que el botón manual es la forma de reintentar si GitHub no responde.
- **Pruebas de API**: con un servidor temporal en el puerto 5099 se comprobó que
  `?cached=true` devuelve `checkedUtc=0001-01-01` sin consultar GitHub, que `?refresh=true`
  actualiza `latestVersion` a `1.0.5` y que una consulta posterior a `cached=true` conserva ese
  resultado. `dotnet build CyberManager.sln` terminó con **0 errores y 0 advertencias**.
- **Pendiente de verificación visual**: el texto de la barra de título en pantalla.
- **Instaladores**: `scripts/build-installer.ps1` subió `version.props` a `1.0.6` y generó
  `CyberManager-Servidor-1.0.6.exe` (166.581.814 bytes, ~158,9 MB) y
  `CyberManager-Estacion-1.0.6.exe` (147.536.533 bytes, ~140,7 MB), con payloads x64 y x86.
- Release `v1.0.6` publicada con ambos instaladores en
  <https://github.com/Kis4r4gi/CyberManager/releases/tag/v1.0.6> el `2026-11-09T18:13:34Z`;
  el recordatorio de 2 horas quedará para `2026-11-09T20:13:34Z`.

---

## Rutas y comandos clave

Repositorio: la carpeta que contiene `CyberManager.sln` (por ejemplo, `C:\CyberManager`).

```powershell
# Compilar
$dotnet = "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe"
& $dotnet build CyberManager.sln -v q --nologo

# Generar los dos instaladores unificados (publica x64 y x86; sube el patch)
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1

# Fijar una versión concreta o conservar la actual
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1 -Version 1.1.0
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1 -NoBump

# Recompilar los instaladores con dist\x64 y dist\x86 existentes (sin subir versión)
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1 -SkipPublish

# Publicación de diagnóstico de una sola arquitectura (sin instalador; sube la versión)
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1 -Architecture x64 -SkipInstaller -NoBump

# Sincronizar con GitHub (commit automático + pull --rebase + push)
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\sync-github.ps1

# Registrar o eliminar la tarea que sincroniza cada 15 minutos
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\sync-github.ps1 -InstallTask
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\sync-github.ps1 -RemoveTask
```

Servidor en desarrollo (usar `dotnet` + DLL; el `.exe` falla sin runtime ASP.NET global):

```powershell
$out = (Resolve-Path 'src\CyberManager.Server\bin\Debug\net8.0').Path
$dotnet = "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe"
Start-Process -FilePath $dotnet -ArgumentList "`"$out\CyberManager.Server.dll`" --no-elevate" `
  -WorkingDirectory $out -PassThru -WindowStyle Hidden
```

Usar `http://127.0.0.1:5080`, no `localhost`, en pruebas locales.

Inno Setup: `%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe`.
Compilación directa (requiere ambos payloads en `dist\x64` y `dist\x86`):
`& $iscc "/DMyAppVersion=$Version" installer\CyberManager.Servidor.iss` y
`& $iscc "/DMyAppVersion=$Version" installer\CyberManager.Estacion.iss`.

---

## Decisiones y notas importantes

- EF/SQLite: no usar `SumAsync`, `AverageAsync` ni `OrderBy` sobre `decimal`; agregar/ordenar en
  memoria.
- El cierre diario usa límites del día local convertidos con `.ToUniversalTime()`.
- `TotalExpenses` = gastos de caja en efectivo + abonos a proveedores no efectivos (los abonos
  en efectivo ya aparecen como movimiento `Expense`).
- Los abonos en efectivo exigen caja abierta; los no efectivos no.
- La impresión automática solo ocurre con `AutoPrintTicket` y `PrinterName` configurados; el
  botón manual abre `PrintDialog` si no hay impresora.
- Los precios son con IVA incluido; `Sale.Total` incluye IGTF y `Sale.TotalVes` usa la tasa de la
  venta. Solo `Cash`, `CashUsd`, `Card` y `Transfer` generan movimientos de caja.
- Respaldos: claves `BackupAutoEnabled` (true), `BackupIntervalHours` (24) y `BackupRetention`
  (30) en `Settings`; el trabajador revisa cada 5 minutos. Carpeta `respaldos` junto a la base.
- Las conexiones SQLite usadas por respaldos/restauraciones deben usar `Pooling=false`; de lo
  contrario el archivo queda bloqueado y la restauración falla en Windows.
- El autoguardado usa temporizadores de 800 ms para configuración y apariencia; `RefreshAsync`
  suspende el autoguardado mientras carga valores para no reescribirlos.
- La migración de apariencia escribe el archivo al iniciar solo cuando detecta el borde antiguo.
- La base de demostración solo crea `CON-01`/`CON-02` en bases nuevas; una base ya sembrada no
  recibe esos equipos automáticamente.
- El BCV se consulta con un `HttpClient` nombrado (`rates`), con expresiones regulares tolerantes
  al HTML actual. La tasa se guarda con la **fecha local del día** para que rija de inmediato; la
  fecha valor del BCV queda en `Notes` para auditoría. Si el sitio falla o cambia, se conserva la
  última tasa y se registra la advertencia en el log.
- Los cuadros del POS escalan al **volver a abrir** la vista, porque las dimensiones se calculan al
  construir cada tarjeta. En el tablero, cambiar el tamaño o los filtros reconstruye las tarjetas
  de inmediato.
- **Tablero**: las preferencias (`DashboardSort`, sentido, filtros, zona, métricas y leyenda) se
  guardan en `appearance.json` mediante `AppearanceService.Save()`. `SettingsView` las conserva al
  aplicar un tema, cambiar tamaños/idioma o restablecer la apariencia. Los filtros usan
  `StationDto` (zona, tipo, usuario) porque `StationSummaryDto` no incluye esos campos.
- El icono se genera con `return ,$bytes` y una tabla hash normal (`@{}`), no `[ordered]@{}`:
  PowerShell interpreta las claves enteras ordenadas como índices y falla al asignar `byte[]`.
- No hay repositorio git; los cambios están solo en disco.
- `dist/` y `artifacts/` son salidas generadas. La publicación usa `dist\x64` y `dist\x86`.
- **Localización**: `L.T` traduce español→inglés y inglés→español; el diccionario usa asignaciones
  por indexador, por lo que las claves repetidas no lanzan excepción (gana la última). Las cadenas
  interpoladas generadas en tiempo de ejecución se traducen manualmente en cada vista.
- **Promos**: los videos se guardan como BLOB en SQLite; el cliente los cachea por SHA-256 y borra
  los archivos que ya no están en la lista activa. La reproducción es silenciada y tolera códecs
  ausentes (se omite el video y sigue el siguiente).
- **x86/x64**: no se fija un RID en los `.csproj`; el script publica con `win-x64` o `win-x86`.
  Los instaladores unificados usan `ArchitecturesAllowed=x86compatible` y
  `ArchitecturesInstallIn64BitMode=x64compatible`; `Is64BitInstallMode` decide si se copia
  `dist\x64` o `dist\x86`. `[InstallDelete]` limpia los binarios nativos de la arquitectura
  opuesta al reinstalar. No existe un modificador documentado para forzar x86 en un Windows x64.
- **Versión**: `version.props` es la única fuente; no edite `Version` en los `.csproj`. El script
  de empaquetado sube el patch al publicar. Los instaladores usan el mismo valor en
  `AppVersion`, `OutputBaseFilename` y en la etiqueta visible de la consola.
- **Lanzador**: `CyberManager.exe` es una copia del apphost del servidor. El apphost conserva el
  nombre `CyberManager.Server.dll` grabado, por eso funciona dentro de `Server\`. El lanzador solo
  espera a `/api/health`; si el puerto no responde en 30 s, abre igualmente la consola para que el
  usuario vea el error del servidor.
- **Actualizaciones**: `CloseApplications=no` desactiva el Restart Manager de Windows para que el
  instalador controle el cierre. El reinicio se hace en `ssDone` (después de `[Run]`), cuando la
  tarea programada y la configuración del agente ya existen. En modo silencioso se omite la
  confirmación.
- **Desinstalador independiente**: se publica recortado (`PublishTrimmed`, `TrimMode=partial`) y
  `InvariantGlobalization=true`; no referencia WPF. Se copia a `{commonappdata}\CyberManager` desde
  cualquiera de los dos instaladores, así que desinstalar uno no borra el desinstalador del otro
  hasta que ambos se quitan.
- **Orden manual**: `POST /api/stations/reorder` renumera `SortOrder` según la lista de IDs. La
  vista de equipos mueve el seleccionado una posición y recarga; el tablero lo respeta con el
  criterio `manual`.
- El README se copia a `dist\x64` y `dist\x86` al compilar; tras editar la documentación hay que
  recompilar con `-SkipPublish` para que el instalador incluya la versión nueva.
- `SatelliteResourceLanguages=en;es` y la limpieza de PDB/XML reducen el tamaño de los instaladores.

---

## Pendientes / siguientes pasos

1. (Opcional) Con una sesión **elevada**, probar los dos instaladores unificados en modo silencioso
   y verificar: selección automática de x64 en este equipo, archivos instalados, tarea programada,
   firewall, servicio del agente, reinstalación cruzada x64/x86, confirmación al cerrar procesos,
   reinicio posterior y desinstalación. No se ejecutó una instalación real porque la sesión actual
   no es administrador.
2. (Opcional) Probar la ruta x86 en un Windows de 32 bits o en una máquina virtual; el mecanismo
   equivalente se validó con un instalador Inno de prueba en x64.
3. (Opcional) Desplegar la nueva versión en `C:\CyberManager\Servidor`, comprobar el acceso directo
   **CyberManager** (lanzador único), la versión en la barra lateral y en Configuración, el orden
   manual con Subir/Bajar, y probar el desinstalador desde el menú Inicio y desde la consola.
4. (Opcional) Con una sesión elevada, probar la elevación de primer inicio (UAC), el marcador
   `%ProgramData%\CyberManager\server-elevated.marker` y los comandos remotos de volumen, monitor y
   bloqueo de entrada contra una estación real.
5. Mantener actualizado `README.md` si se agregan funciones nuevas.
6. Recompilar los instaladores cuando cambie el código para incluir la última versión de la
   consola, el servidor, las promos y la localización. El patch sube automáticamente; use
   `-NoBump` solo si necesita conservar la versión.

---

## Publicación

- Repositorio público: <https://github.com/Kis4r4gi/CyberManager> (rama `main`, licencia MIT).
- Cuenta de GitHub: `Kis4r4gi`. La autenticación se hizo con `gh auth login` (flujo de dispositivo);
  no se almacenan credenciales en el proyecto.
- Instaladores publicados en *Releases*:
  <https://github.com/Kis4r4gi/CyberManager/releases/latest>.
- Para publicar una versión nueva: recompilar con `scripts/build-installer.ps1`, confirmar el
  cambio de `version.props` y ejecutar
  `gh release create v<versión> --target main artifacts/CyberManager-Servidor-<versión>.exe artifacts/CyberManager-Estacion-<versión>.exe`.
- Release actual: **v1.0.4**
  (<https://github.com/Kis4r4gi/CyberManager/releases/tag/v1.0.4>) con
  `CyberManager-Servidor-1.0.4.exe` y `CyberManager-Estacion-1.0.4.exe`, publicada el
  `2026-11-08T23:46:40Z`. Las releases anteriores `v1.0.3`
  (<https://github.com/Kis4r4gi/CyberManager/releases/tag/v1.0.3>) y `v1.0.2`
  (<https://github.com/Kis4r4gi/CyberManager/releases/tag/v1.0.2>) se conservan como historial.
- La sincronización automática se registra con
  `pwsh -File .\scripts\sync-github.ps1 -InstallTask` (tarea programada `CyberManagerGitSync`,
  cada 15 minutos con la sesión del usuario).
