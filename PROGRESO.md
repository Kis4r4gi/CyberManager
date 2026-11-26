# Estado del proyecto y continuación automática

> Última actualización: 2026-11-19
> Este archivo permite retomar el trabajo exactamente donde quedó. Léalo completo antes de continuar.

## Objetivo

Sistema de gestión para cyber cafés y centros de videojuegos en Venezuela: servidor central en
red local, consola de administración/caja, cliente bloqueado por estación y agente de Windows
para control remoto. Todo el dinero contable en **USD**, con tasa Bs/USD diaria e IVA 16 %, e
instaladores separados por componente para Windows (servidor y estación), unificados por
arquitectura (x86/x64).

## Estado: COMPLETADO, INSTALADO Y VERIFICADO

La solución compila con **0 errores** y **0 advertencias**. Se generan **dos instaladores**:
**Servidor** y **Estación**. Cada uno incluye los payloads de x64 y x86 y selección automática de la
arquitectura al instalar. El **servidor 1.3.48 está instalado y corriendo** (tarea programada,
puerto 5080) en este equipo con la ruta `C:\CyberManager\Servidor`, al igual que la consola. El
paquete de actualización del cliente también está en 1.3.48. Tras un reinicio del equipo la IP del
servidor pasó a `192.168.1.10`; los equipos siguen reportando correctamente porque resuelven la
dirección por descubrimiento. La estación 11 (`14:AB:C5:D4:30:F5`, IP informada `192.168.1.4`)
estaba offline al verificar y conservaba 1.3.43; se actualizará al reconectarse.

Los fondos y los videos promocionales se administran en una única sección, **Sistema → Fondos y
videos**, con pestañas independientes. En la pantalla bloqueada ambos se alternan: un fondo durante
el tiempo configurado, un video y, al terminar, se vuelve al fondo. La subida solo acepta videos
compatibles (MP4/M4V con H.264, no fragmentados, hasta 1920 × 1080); la consola y el servidor
validan el archivo antes de publicarlo y muestran las especificaciones si no cumple. El formato
por defecto recomendado es el preset MP4/H.264 de Format Factory a **30 fps** (1280 × 720,
≈1,5 Mbps, sin audio): los 60 fps se aceptan, pero el reproductor WPF (`MediaElement`) topa en ~30
fps por estación con independencia del archivo, así que no aportan fluidez y solo ocupan más espacio.
El video fragmentado original se eliminó y se sustituyó por una codificación fluida.

La subida y descarga de videos promocionales se hace ahora en bloques (sin cargar el archivo
completo en memoria) y con un cliente HTTP de larga duración, para que un video de más de 100 MB
no cuelgue la consola ni impida que arranque automáticamente en el terminal bloqueado. El límite
por video pasó de 300 MB a 120 MB.

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
- Galería de fondos configurada desde la consola con tiempo por imagen, orden (`Ordered`/`Random`)
  y transición (`None`/`Fade`/`Slide`/`Zoom`); rotación local en cada equipo, imágenes en SQLite y
  caché SHA-256 en el cliente.
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
  entrega.
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
  Bs, IVA, descuentos, ticket promedio, sesiones, caja, gastos, abonos a proveedores,
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

- Inno Setup 6 con **dos instaladores**: Servidor y Estación.
  - `installer/CyberManager.Servidor.iss` → servidor + consola de administración.
  - `installer/CyberManager.Estacion.iss` → cliente + agente de Windows.
  - El instalador unificado (`installer/CyberManager.Unificado.iss`) se eliminó del proyecto en
    1.3.31; para instalar ambos componentes en el mismo equipo se ejecutan los dos instaladores.
  - Cada instalador incluye los payloads de `dist\x64` y `dist\x86`; `[Files]` usa
    `Check: IsX64InstallMode` / `IsX86InstallMode` para copiar solo el que corresponde.
    `ArchitecturesAllowed=x86compatible` permite x86/x64 y
    `ArchitecturesInstallIn64BitMode=x64compatible` activa el modo de 64 bits en equipos x64.
    Incluyen español e inglés en `[Languages]` y `[CustomMessages]`.
  - `[InstallDelete]` retira los binarios nativos de la arquitectura opuesta al reinstalar, sin
    tocar la base de datos del servidor ni la configuración local de la estación.
- `scripts/build-installer.ps1` publica ambos payloads (`dist\x64` y `dist\x86`), el desinstalador
  independiente y compila los **dos** instaladores (servidor y estación); `-Architecture`
  acepta `all` (por defecto), `x64` o `x86`, y las dos últimas solo se usan con `-SkipInstaller`
  para publicaciones de diagnóstico.
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
- Instaladores generados (1.3.0):
  - `artifacts/CyberManager-Unificado-1.3.0.exe` (~505,4 MB: servidor + estación en un solo bloque).
  - `artifacts/CyberManager-Servidor-1.3.0.exe` (~374,4 MB, payloads x64 + x86).
  - `artifacts/CyberManager-Estacion-1.3.0.exe` (~143,8 MB, payloads x64 + x86).
- Ambos instaladores llevan `SetupIconFile=..\assets\CyberManager.ico` y los ejecutables
  publicados en ambas arquitecturas incluyen el icono en sus recursos.
- **Reducción de tamaño**: las publicaciones omiten PDB y XML de documentación, usan
  `SatelliteResourceLanguages=en;es` y el script elimina recursos satélite de otros idiomas. El
  agente ya no fija `RuntimeIdentifier=win-x64`; la arquitectura se decide al publicar.

### Recordatorio de versiones y sincronización (1.0.2)

- **Aviso de nueva versión**: `UpdateService` consulta las versiones publicadas en
  `Kis4r4gi/CyberManager` y guarda el resultado en memoria durante 10 minutos para no agotar el
  límite de la API de GitHub. La consola consulta GitHub **una sola vez, al abrirse**, y si la
  versión más reciente supera la instalada y ya pasaron las horas de espera, muestra una ventana
  con botones **Sí** y **No**. **Sí** abre la página de versiones del proyecto en GitHub; **No**
  oculta el aviso para esa versión.
- **Espera de 2 horas**: las claves `UpdateReminderEnabled` (activado), `UpdateReminderDelayHours`
  (2) y `UpdateReminderDismissedVersion` controlan el aviso. No hay comprobaciones periódicas ni
  consultas en segundo plano.
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

### Fondos incluidos y cuadros de selección (1.0.7, 2026-11-09)

- **Cuadros de selección**: la plantilla de `ComboBox` en `Dark.xaml` ahora enlaza
  `ContentTemplateSelector` con `ItemTemplateSelector`. Sin ese enlace, `DisplayMemberPath`
  creaba un `DisplayMemberTemplateSelector` que la plantilla no aplicaba y el cuadro mostraba
  `Option { Value = ProductDto { ... } }` en lugar del nombre elegido. Se reprodujo y verificó
  con una app WPF aislada: antes `[Option { Value = ... }]`, después `[Snack]`.
- **30 fondos 4K incluidos**: se copiaron los JPG de `artifacts/Wallpapers Promo Logos 4K` a
  `src/CyberManager.Server/Assets/Wallpapers` (47,65 MB) y se publican junto con el servidor.
  `DbSeeder.SeedWallpapersAsync` los siembra una sola vez por versión de paquete
  (`BundledWallpapersVersion`), omite duplicados por SHA-256 y calcula las dimensiones con
  `ImageInspector`. Los nombres legibles y el orden 1-30 se definen en el sembrador.
- **Pruebas**: con una base temporal en el puerto 5098, `GET /api/wallpapers` devolvió los 30
  fondos con 3840 × 2160; tras borrar uno y reiniciar el servidor quedaron 29, lo que confirma
  que no se vuelven a sembrar. `dotnet build CyberManager.sln` terminó con **0 errores y
  0 advertencias**.
- **Instaladores**: `scripts/build-installer.ps1` subió `version.props` a `1.0.7` y generó
  `CyberManager-Servidor-1.0.7.exe` (265.461.043 bytes, ~253,2 MB; crece por los 30 fondos en
  x64 y x86) y `CyberManager-Estacion-1.0.7.exe` (147.548.868 bytes, ~140,7 MB).
- Release `v1.0.7` publicada con ambos instaladores en
  <https://github.com/Kis4r4gi/CyberManager/releases/tag/v1.0.7> el `2026-11-09T22:34:07Z`;
  el recordatorio de 2 horas quedará para `2026-11-10T00:34:07Z`.

### Cierre detallado, control remoto, WoL y NetFiltroWeb (1.0.8, 2026-11-09)

- **Cierre de ventas detallado**: `ReportService` dejó de limitar `TopProducts` a 15 elementos.
  `DailyClosingDialog`, el ticket (`TicketPrinter`) y el correo HTML (`EmailService`) muestran
  **todos los productos vendidos con su cantidad** y su importe, no solo los más vendidos.
- **Cuadros de escritura compactos**: `Dark.xaml` reduce `TextBox` y `PasswordBox` a `MinHeight`
  26, padding `8,4` y fuente 12,5; los campos multilínea alinean el texto arriba con padding
  `8,6`. Mejora la estética sin perder legibilidad.
- **Wake on LAN**: `WakeOnLanService` parsea MAC con separadores `:`, `-`, `.` o espacios y envía
  el paquete mágico por UDP a `255.255.255.255` y al broadcast de la subred en los puertos 9 y 7.
  `POST /api/stations/{id}/wake` (solo `Admin`) resuelve la MAC en el servidor. La acción está en
  `StationActionsDialog` («Encender equipo (WoL)») y en `DashboardView` (`case "wake"`).
- **Respaldo de tiempos del cliente**: `SessionBackupService` guarda
  `%LOCALAPPDATA%\CyberManager\Client\session-backup.json` de forma atómica (temporal +
  reemplazo) en cada consulta exitosa y lo borra si no hay sesión. Si el servidor no responde al
  iniciar, `App.RefreshAsync` restaura el respaldo del mismo equipo, avanza minutos e importe y la
  barra avisa «Sin conexión: tiempo restaurado desde el respaldo local».
- **Extensión sobre la marcha**: se mantiene `ExtendSessionAsync`/`ExtendSessionDialog`; la sesión
  continúa activa mientras se agregan minutos y el cobro se suma al cierre.
- **Barra flotante configurable**: `StationClientStateDto` incluye `SessionBarDescription` y
  `SessionBarScale` (70–160). El servidor los envía en `/api/station/state` desde las claves
  `SessionBarDescription` y `SessionBarScale`. `SessionBarWindow` aplica el texto y un
  `ScaleTransform`; la ventana sigue siendo flotante, superior y sin foco.
- **Control remoto de escritorio**: `RemoteInputInjector` inyecta mouse y teclado con `SendInput`
  en la sesión del cliente. `ScreenViewerWindow` captura movimiento, clics, rueda y teclas
  (incluye Ctrl/Shift/Alt y `Key.System`) y los envía por `AdminHub.SendRemoteInput` →
  `RealtimeService.SendRemoteInputAsync` → evento `RemoteInput` en `/hubs/station`. Se corrigió
  la estructura `INPUT`: ahora usa una unión anidada con `LayoutKind.Sequential`, de modo que en
  x64 mide 40 bytes con la unión en el desplazamiento 8 (antes el desplazamiento fijo 4 solo era
  válido en x86).
- **VNC externo**: botón **Abrir VNC** en el visualizador. Lee `VncViewerPath` y
  `VncDefaultPort` de la configuración y ejecuta `visor.exe IP:puerto` con la IP del equipo.
  CyberManager no instala el servidor VNC; solo lanza el visor configurado.
- **NetFiltroWeb**: entidad `WebFilterRule` (`Domain`/`Keyword`), 24 reglas iniciales sembradas
  una sola vez, CRUD en `/api/webfilter`, restauración sin duplicados y lista activa en
  `/api/station/webfilter`. `WebFilterService` del agente escribe los dominios en un bloque
  marcado del archivo `hosts`, aplica las palabras clave con un proxy HTTP/CONNECT local
  (`127.0.0.1:8877`, configurable) y descarga la lista cada 60 s y al recibir `WebFilterChanged`.
  `SettingsView` agrega la pestaña **Control remoto y filtro** con la barra, VNC, proxy y la
  tabla de reglas (`WebFilterRuleDialog`).
- **Pruebas**:
  - `dotnet build CyberManager.sln`: **0 errores y 0 advertencias**.
  - Servidor temporal en el puerto 5099: `/api/station/webfilter` devolvió **24 reglas** con
    `proxy=True:8877`; `PUT /api/settings` guardó la barra y `/api/station/state` devolvió
    `sessionBarDescription` y `sessionBarScale=125`; `POST /api/stations/{id}/wake` respondió
    `sent=true` con la MAC `AA:BB:CC:DD:EE:FF`; el token del equipo no cambió.
  - Respaldo del cliente con un arnés de reflexión: guardó, restauró tras simular 10 minutos
    (`transcurrido=40`, `restante=80`, `tiempo=1,33`), devolvió `null` para otro equipo y limpió
    el archivo. Se preservó cualquier respaldo real existente.
  - NetFiltroWeb: la coincidencia devolvió `true` para `ejemplo.com`, `www.ejemplo.com`,
    `sub.ejemplo.com`, `sitio-porno.net` y `pornografia.com`, y `false` para `otro.com` y
    `limpio.org`; el proxy local respondió **403** con la página de bloqueo. La prueba no escribió
    en el archivo `hosts` real.
  - Estructura `INPUT` verificada por reflexión en x64: tamaño **40**, unión en desplazamiento
    **8**, `MOUSEINPUT` de 32 bytes.
- **Pendiente de verificación real**: instalación/desinstalación con UAC, firewall, agente como
  servicio, control remoto contra una estación física, visor VNC instalado y filtrado con
  navegadores reales. El envío real a Gmail/Outlook sigue requiriendo credenciales del usuario.
- **Instaladores**: `scripts/build-installer.ps1` subió `version.props` a `1.0.8` y generó
  `CyberManager-Servidor-1.0.8.exe` (265.493.981 bytes, ~253,2 MB) y
  `CyberManager-Estacion-1.0.8.exe` (147.580.604 bytes, ~140,7 MB).
- Release `v1.0.8` publicada con ambos instaladores en
  <https://github.com/Kis4r4gi/CyberManager/releases/tag/v1.0.8> el `2026-11-09T22:33:29Z`;
  el recordatorio de 2 horas quedará para `2026-11-10T00:33:29Z`.

### Impresoras, interfaz de cajero y conexión por IP (1.0.9, 2026-11-10)

- **Impresora de tickets e impresora de trabajo**: nueva clave `WorkPrinterName` y campo
  `TicketSettings.WorkPrinterName`. `PrintSale` usa `PrinterName`; `PrintClosing` usa
  `WorkPrinterName` y cae a `PrinterName` si está vacía. Ambas se imprimen directamente en la cola
  configurada y solo abren el diálogo de Windows si no hay impresora guardada. En
  *Configuración → Ticket y operación* hay dos desplegables editables con las impresoras instaladas
  (`TicketPrinter.InstalledPrinters()`), y también se puede escribir un nombre manualmente.
- **Interfaz de cajero sin funciones administrativas**: `MainWindow.ApplyRoleVisibility` oculta y
  `Navigate` bloquea catálogo, inventario, compras, equipos, fondos, promos, reportes y
  configuración; también oculta la bitácora, su separador y las etiquetas de catálogo/sistema.
  El botón **Configuración** del tablero y el aviso de actualizaciones quedan ocultos, y
  `StationActionsDialog` recibe `systemActions: false` para no ofrecer control remoto, energía ni
  mantenimiento al cajero.
- **Eliminación de catálogo**: botones **Eliminar** para productos, categorías, tarifas, paquetes
  de tiempo y zonas. Se reutilizan los `DELETE` existentes con desactivación lógica, de modo que
  las ventas y movimientos históricos se conservan.
- **Eliminación permanente de cajeros**: `DELETE /api/users/{id}/permanent` borra la cuenta y
  limpia referencias opcionales en sesiones, cajas, ventas, compras, movimientos, clientes y
  auditoría. Bloquea la autoeliminación, el último administrador activo y los usuarios con cajas
  abiertas. En *Configuración → Usuarios* hay un botón **Eliminar** con confirmación.
- **Aceptar después de imprimir**: `SaleReceiptDialog` usa **Aceptar** como botón principal y
  agrega **Imprimir y aceptar**; **Imprimir ticket** sigue disponible sin cerrar el resumen.
- **Conexión por IP al primer uso**: `GET /api/stations/{id}/config` devuelve el host y puerto
  reales de la petición; si la consola entra por loopback, usa la IPv4 de la interfaz con puerta de
  enlace (evita adaptadores virtuales de Hyper-V/WSL). `ApiClient.Normalize` agrega `:5080` cuando
  se escribe solo la IP o el nombre del equipo (sin puerto y con `http`). `SetupWindow` incorpora
  **Buscar en la red**, que consulta `/api/health` en localhost y en las subredes locales.
- **Pruebas**:
  - `dotnet build CyberManager.sln`: **0 errores y 0 advertencias**.
  - Servidor temporal en el puerto 5099: `/api/stations/1/config` devolvió
    `http://192.168.1.7:5099` tanto al entrar por `127.0.0.1` como por la IP LAN; `/api/health`
    respondió `{"status":"ok"}`.
  - Catálogo: se crearon y eliminaron una categoría, tarifa, paquete, zona y producto; el producto
    quedó inactivo tras el borrado lógico.
  - Usuarios: se creó un cajero y se eliminó con `DELETE /api/users/{id}/permanent`; la lista
    confirmó que ya no existe. La autoeliminación del administrador respondió **400**.
  - `ApiClient.Normalize`: `192.168.1.7` → `http://192.168.1.7:5080/`, `192.168.1.7:5099` se
    conserva, `https://servidor` no recibe el puerto 5080 y una URL vacía usa
    `http://127.0.0.1:5080/`.
- **Pendiente de verificación real**: impresión física en dos colas distintas, instalación con UAC,
  firewall, eliminación de usuarios con historial de cajas y descubrimiento de servidor en una red
  con varias subredes.
- **Instaladores**: completado. `scripts/build-installer.ps1` subió `version.props` a `1.0.9` y generó
  `CyberManager-Servidor-1.0.9.exe` (253.2 MB, payloads x64 + x86) y
  `CyberManager-Estacion-1.0.9.exe` (140.7 MB, payloads x64 + x86), con payloads x64 y x86.
  Ambos instaladores fueron generados exitosamente el `2026-11-10`.

### Verificación integral y apertura del cliente al finalizar (1.0.11, 2026-11-10)

- **Proyecto real verificado**: se restauró el repositorio público `Kis4r4gi/CyberManager`
  (rama `main`) en `C:\Users\Justo Lopez\Documents\CyberManager`. Contiene los 7 proyectos, los
  scripts de empaquetado y las funciones de 1.0.9: impresoras separadas de tickets y de trabajo,
  interfaz de cajero sin opciones administrativas, borrado permanente de usuarios, conexión por
  IP/MAC, control remoto/VNC, RIF, tooltips a 2 s y videos promocionales.
- **Compilación**: `dotnet build CyberManager.sln -c Release` → **0 errores**. Quedan 8
  advertencias de nulabilidad en `CyberManager.Client` (`App.xaml.cs` y `SetupWindow.xaml.cs`)
  que no bloquean la compilación ni el empaquetado.
- **Instalador de estación**: la página final ahora ofrece **Abrir CyberManager Cliente** además de
  **Configurar el agente de esta estación**. `ShouldOpenClient` evita abrir una segunda instancia
  cuando el cliente ya estaba en ejecución (en ese caso se reinicia en `ssDone`). El instalador de
  servidor ya ofrecía **Abrir la consola de administración**.
- **Instaladores unificados regenerados** con Inno Setup 6.7.4 (x64 + x86):
  - `artifacts\CyberManager-Servidor-1.0.11.exe` — 265.509.659 bytes (~253,2 MB),
    SHA-256 `504550C679AF3589847E4BFF88644D128D9ED716DC2784478252D3BB09EDD1B0`.
  - `artifacts\CyberManager-Estacion-1.0.11.exe` — 147.584.297 bytes (~140,7 MB),
    SHA-256 `E03D4ED0D1CB681688F14002020EAE5E5DD4A5184F2923B507765E9222BB2377`.
- **Instalación local actualizada**: se ejecutó `CyberManager-Servidor-1.0.11.exe` en modo silencioso
  sobre `C:\CyberManager\Servidor` (antes 1.0.9). El instalador detuvo el servidor, reemplazó los
  archivos y lo reinició: proceso activo, puerto 5080 escuchando, tarea `CyberManagerServer` en
  ejecución y `GET /api/health` → `{"status":"ok"}`.
- **Casilla «Iniciar el servidor ahora» (preparada, pendiente de recompilar)**: la página final del
  instalador de servidor incluye una casilla junto al README que ejecuta
  `CyberManager.exe --start-server`. El lanzador comprueba `/api/health`, espera la respuesta y no
  duplica la instancia ni abre ventanas; `ShouldStartServer` la oculta si el servidor ya estaba en
  ejecución. No forma parte de los instaladores 1.0.11 ya publicados.
- **Aviso de actualización con ventana Sí/No (preparado, pendiente de recompilar)**: al abrir la
  consola se consulta GitHub una sola vez; si hay una versión nueva y ya pasó la espera, aparece
  una ventana con **Sí** y **No**. **Sí** abre `https://github.com/Kis4r4gi/CyberManager/releases`;
  **No** oculta el aviso para esa versión. Se retiró la barra superior y la comprobación duplicada
  del tablero. No forma parte de los instaladores 1.0.11 ya publicados.
- **Pendiente de verificación real**: instalación con UAC, firewall, servicio del agente,
  desinstalación y control remoto contra una estación física.

### TightVNC opcional en estaciones (1.0.12, 2026-11-10)

- **Componente opcional**: el instalador de estación agrega la tarea **Instalar TightVNC
  (servidor VNC)** en el grupo **Control remoto (opcional)**. El instalador del servidor agrega
  la tarea **Instalar el visor TightVNC**. Ambas están desmarcadas por defecto.
- **Página de configuración**: si se marca la tarea de estación, aparece una página con
  contraseña (4–8 caracteres ASCII) y puerto (1024–65535). La contraseña se escribe en un archivo
  temporal, se pasa a `instalar-vnc.ps1` y se borra al terminar; no aparece en la línea de
  comandos. En modo silencioso la tarea se omite para no instalar un VNC sin contraseña.
- **Instalación silenciosa**: `installer\scripts\instalar-vnc.ps1` usa los MSI oficiales de
  TightVNC 2.8.90 con `ADDLOCAL=Server` o `Viewer`, registra el servicio `tvnserver`, aplica
  `VALUE_OF_PASSWORD`, `VALUE_OF_RFBPORT`, `SERVER_ALLOW_SAS` y desactiva las excepciones de
  Firewall del MSI.
- **Firewall**: regla `CyberManager VNC (TightVNC)`, TCP, puerto configurable, perfiles
  `Domain,Private` y `RemoteAddress LocalSubnet`. Nunca se abre a Internet.
- **Acceso directo**: **Configurar servidor VNC** ejecuta
  `installer\scripts\configurar-vnc.ps1`, que se autoeleva, actualiza la regla de Firewall con
  el puerto leído de `HKLM\SOFTWARE\TightVNC\Server\RfbPort` y abre
  `tvnserver.exe -controlservice` (o `-configservice` si el servicio no responde).
- **Visor integrado**: `ScreenViewerWindow.OpenVncAsync` detecta automáticamente
  `tvnviewer.exe` en `Program Files\TightVNC` o `Program Files (x86)\TightVNC` cuando
  `VncViewerPath` está vacío o no existe, y usa `IP::puerto` (doble dos puntos) para el puerto
  RFB explícito. `SettingsView` documenta la detección automática.
- **Desinstalación compartida**: `desinstalar-servicios.ps1` invoca
  `instalar-vnc.ps1 -Action Uninstall -Component estacion|servidor`. El marcador
  `%ProgramData%\CyberManager\vnc-componentes.txt` evita borrar TightVNC si el otro componente
  todavía lo usa. Si el equipo ya tenía TightVNC antes de CyberManager, no se desinstala.
- **Licencia**: TightVNC se distribuye como programa separado bajo GPLv2. Se agregaron
  `THIRD-PARTY-NOTICES.md`, `LICENSE-TightVNC-GPLv2.txt`, los hashes SHA-256 de los MSI y del
  código fuente, y `scripts\download-tightvnc.ps1` (verifica tamaño y hash; `-IncludeSource`
  descarga el código fuente).
- **Compilación**: `scripts\build-installer.ps1` llama a `download-tightvnc.ps1` antes de
  compilar los instaladores y falla con un mensaje claro si faltan los MSI. `installer\vnc\` está
  en `.gitignore`; los binarios no se versionan. `-SkipVncDownload` reutiliza los MSI locales.
- **Verificación de propiedades MSI (2026-11-10)**: se abrieron los MSI 2.8.90 x64 y x86 con
  Windows Installer y se confirmó la tabla `Property` (`SET_PASSWORD`, `SET_RFBPORT`,
  `SET_USEVNCAUTHENTICATION`, `SET_ACCEPTRFBCONNECTIONS`, `VALUE_OF_RFBPORT`,
  `VALUE_OF_USEVNCAUTHENTICATION`, `VALUE_OF_ACCEPTRFBCONNECTIONS`,
  `SERVER_REGISTER_AS_SERVICE`, `SERVER_SERVICE_ONLY`, `SERVER_ALLOW_SAS`,
  `SERVER_ADD_FIREWALL_EXCEPTION`, `VIEWER_ADD_FIREWALL_EXCEPTION`,
  `VIEWER_ASSOCIATE_VNC_EXTENSION`). `VALUE_OF_PASSWORD` no está en la tabla `Property` porque la
  lee una acción personalizada, pero la cadena sí existe en el binario del MSI: se pasa en la
  línea de comandos y el instalador la aplica. `build-installer.ps1` compila ambos `.iss` en una
  carpeta temporal (`CyberManager-Estacion-1.0.11.exe` y `CyberManager-Servidor-1.0.11.exe`) sin
  tocar `artifacts`. La sintaxis de los cinco scripts de PowerShell también quedó validada.
- **Pendiente de verificación real**: instalar TightVNC en una estación x64 y x86, comprobar el
  servicio y la contraseña, abrir VNC desde la consola, cambiar el puerto y verificar la regla de
  Firewall, y desinstalar estación/servidor en distinto orden para validar el marcador compartido.

### Release v1.0.12 (2026-11-10)

- **Instaladores generados** con `scripts\build-installer.ps1 -IncludeVncSource`:
  - `artifacts\CyberManager-Servidor-1.0.12.exe` — 268.636.762 bytes (256,2 MB),
    SHA-256 `E84E7ADAD13D0BD21BC25698C230CF7D1B2F4A8D3079CB135C8FE15023C379A9`.
  - `artifacts\CyberManager-Estacion-1.0.12.exe` — 150.697.169 bytes (143,7 MB),
    SHA-256 `81D665D55F47DB43F0D50DF477C0533C3B510EE89C6668F61299C783C1141F2C`.
- **Assets de la release**: ambos instaladores y `tightvnc-2.8.90-src-gpl.zip`
  (3.018.194 bytes, SHA-256 `F3F76393A5865E1F1919C78C3EB43B9C937CF738927C2BFC920ECBD3913B061C`)
  para cumplir con la distribución del código fuente GPLv2.
- **Publicación**: release `v1.0.12` en
  <https://github.com/Kis4r4gi/CyberManager/releases/tag/v1.0.12>. La consola la detecta al
  abrir y ofrece el aviso **Sí/No** para abrir la página de versiones.
- **Commits**: `bd16a24` (actualizaciones al abrir e inicio silencioso del servidor) y `0a1ef68`
  (integración TightVNC). `version.props` quedó en **1.0.12**.

### Importación del stock real (2026-11-14)

- Se agregó `scripts/import-stock.ps1`, que importa listas CSV o HTML del sistema anterior usando
  el código como SKU: crea los productos nuevos y actualiza los existentes sin duplicar.
- La base instalada en `C:\CyberManager\Servidor\Server\cybermanager.db` quedó con **424 productos**
  importados de `resources\stock.csv`: 424 activos, IVA 16 %, 98 marcados como servicio (sin stock
  ni mínimo), 180 movimientos de stock inicial y 0 SKUs duplicados.
- Se eliminaron los 16 productos de ejemplo (`BEB-001`…`SRV-004`); no tenían ventas, compras ni
  movimientos asociados. Antes de escribir se creó el respaldo
  `respaldos\cybermanager_20261114_121852_pre-importacion.db`.
- Las tres filas con descripción que contenía comas (`mod2`, `port14`, `port15`) se reconstruyeron
  correctamente: `mod 2, opsu`, `portamina fosca plastico 0,5` y `portamina foska metal 0,7`.
- El importador aplica 16 % de IVA por defecto (`-TaxRate`), marca servicios según `-ServiceMode`
  (`Auto`, `None`, `All`) y permite `-DryRun`, `-Database`, `-ServerDir`, `-SkipBackup` y
  `-DeleteDemoProducts`. Documentado en README → *Importar productos desde una lista de stock*.

### Carpeta de respaldos, menú de producto y punto de venta (2026-11-14)

- **Carpeta de respaldos configurable**: nueva clave `SettingsKeys.BackupFolder`. Vacía significa
  la carpeta predeterminada `respaldos` junto a la base. `BackupService.DefaultFolder` calcula esa
  ruta, `Folder` devuelve la efectiva y `SetFolderAsync` valida que sea una ruta absoluta, que la
  carpeta se pueda crear y que admita escritura (`EnsureWritable` escribe y borra un archivo de
  prueba) antes de guardar el cambio. La ruta se resuelve una vez por instancia y todos los
  respaldos, listados, descargas y restauraciones usan la carpeta efectiva.
- **API**: `BackupListDto.DefaultFolder` informa la carpeta predeterminada y
  `PUT /api/backups/folder` (solo `Admin`) recibe `SetBackupFolderRequest`; un valor nulo o vacío
  restablece la predeterminada. `CyberApi.SetBackupFolderAsync` expone la llamada.
- **Interfaz**: en *Configuración → Respaldos* hay un desplegable con **Predeterminada**, la carpeta
  actual y las unidades fijas/extraíbles del equipo, un botón **Examinar…** que agrega cualquier
  ruta y **Aplicar carpeta**. El texto inferior muestra la carpeta efectiva en el servidor y se
  recuerda que los respaldos anteriores no se mueven al cambiar de ubicación.
- **Menú contextual de producto**: clic derecho sobre una fila de **Productos** la selecciona y abre
  **Editar producto…**, **Activar / desactivar** y **Eliminar producto**. El menú solo se instala
  cuando `App.Session.Role == UserRole.Admin`.
- **Punto de venta**: las tarjetas compactas de 184 × 108 px (escaladas por `ProductCardScale`)
  muestran nombre, categoría, precio y existencias del producto.
- **Prueba funcional**: con un servidor temporal (puerto 5099 y base nueva) se verificó la carpeta
  inicial y la predeterminada, el cambio a una carpeta nueva, la creación de un respaldo visible en
  la lista, el restablecimiento a la predeterminada y el rechazo de una ruta relativa.
- **Compilación**: `dotnet build CyberManager.sln -c Release` sin errores (solo los avisos nullable
  preexistentes). Se generaron los instaladores **1.0.15**
  (`CyberManager-Servidor-1.0.15.exe` y `CyberManager-Estacion-1.0.15.exe`) y se instaló el de
  servidor sobre `C:\CyberManager\Servidor`; `/api/health` responde `ok`, la versión instalada es
  **1.0.15** y la base conserva **424 productos** y **7 respaldos**.

### Menú de sesión (2026-11-14)

- **Menú de sesión**: el pie del panel izquierdo muestra un botón con el usuario actual y su rol
  (`SessionButton`, `SessionNameText`, `SessionRoleText`). Al pulsarlo se abre un `ContextMenu` con
  el usuario actual, **Cambiar de usuario…**, la lista de usuarios activos (solo `Admin`, vía
  `/api/users`, sin repetir al usuario actual) y **Salir de CyberManager**.
- **Cambio sin cerrar la aplicación**: `SwitchUserAsync` reutiliza `LoginWindow` con el usuario
  preseleccionado y siempre pide la contraseña. Si el acceso es correcto, `RestartConsoleAsync`
  cierra la conexión en tiempo real anterior, cierra la ventana y crea otra `MainWindow` con la
  nueva sesión; `ShutdownMode` pasa temporalmente a `OnExplicitShutdown` y `MainWindow_Closed` no
  termina la aplicación cuando `_switchingUser` está activo. Al cancelar el login se conserva la
  sesión anterior.
- **Cierre cómodo**: la opción **Salir de CyberManager** confirma y cierra la consola con
  `App.ShutdownApplication`; el servidor sigue en segundo plano.
- **Prueba** (`SessionTest`, STA, .NET 8, servidor 1.0.18 en el puerto 5080):
  - Menú de administrador: **7 elementos** (usuario, cambiar, separador, `Cajero · Cajero`, salir);
    menú de cajero: **5 elementos** sin lista de usuarios. El pie muestra **Cajero · Cajero** y
    `LoginWindow("cajero")` preselecciona el usuario.
  - Cambio de sesión real de administrador a cajero: se creó una **ventana nueva** con la sesión del
    cajero, las vistas administrativas quedaron en `Collapsed` y `App.IsShuttingDown` siguió en
    `false`.
- **Compilación**: `dotnet build CyberManager.sln -c Release` con **0 errores**.
- **Instaladores 1.0.19**: `CyberManager-Servidor-1.0.19.exe` (269.043.642 bytes) y
  `CyberManager-Estacion-1.0.19.exe` (150.711.457 bytes). Instalación silenciosa del servidor con
  las tareas `desktopicon,serverstartup,firewall`: `/api/health` responde `ok`, el servidor y la
  consola quedaron en **1.0.19**, la tarea `CyberManagerServer` está **Running** y la base conserva
  **424 productos** y **8 respaldos**. El proceso de instalación se esperó desde
  PowerShell hasta el tiempo límite porque el servidor iniciado por la tarea es un proceso hijo;
  la instalación y el arranque terminaron correctamente.

### Eliminación completa de imágenes e iconos de producto (1.0.20, 2026-11-15)

- **Alcance**: se quitó por completo el sistema de imágenes e iconos de productos en el modelo
  compartido, los DTOs, el servidor, la API, la consola, el punto de venta y la pantalla del
  cliente. Los productos conservan solo código, nombre, categoría, precio, costo, existencias,
  stock mínimo, unidad, IVA, tipo de servicio y estado.
- **Modelo y base**: se eliminaron `Product.Icon`, `Product.IconUpdatedUtc`, `Product.ImagePath`,
  la entidad `ProductIcon` y la tabla `ProductIcons`. La migración de arranque
  (`DbSeeder.EnsureCompatibilityAsync`) borra la tabla heredada y las columnas `Icon`,
  `IconUpdatedUtc` e `ImagePath` con `DROP TABLE`/`ALTER TABLE ... DROP COLUMN`; si una versión
  antigua de SQLite no admite `DROP COLUMN`, deja la columna sin usar y registra un aviso.
- **API**: se quitaron los endpoints `GET/POST/DELETE /api/products/{id}/icon` y
  `GET /api/station/products/{id}/icon`. `ProductDto` ya no expone `Icon`, `HasIcon` ni
  `IconUpdatedUtc`, y `SaveProductRequest` ya no recibe `Icon`.
- **Interfaz**: en **Productos** desaparecieron las columnas **Icono** e **Imagen**, el selector
  **Mostrar:** y las acciones **Cambiar imagen…**, **Buscar imagen en internet…**, **Quitar
  imagen** y **Redimensionar imagen…**. El diálogo de producto solo captura sus datos. En **Punto
  de venta** y **Pedir productos** las tarjetas muestran nombre, categoría, precio y existencias.
  En **Configuración → Apariencia** se quitó la preferencia de visualización de productos.
- **Código eliminado**: `ProductIconImage`, `ProductIconCache`, `ProductImage`, `WebImageSearch`,
  `IconSearchDialog`, `IconResizeDialog`, `BackgroundRemoval` y los scripts
  `import-product-icons.ps1`, `iconos-mapeo.csv` y `iconos-emparejamiento.csv`. `ImageInspector`
  se conserva porque lo usan los fondos y los videos promocionales.
- **Compatibilidad**: los `appearance.json` antiguos siguen cargando porque System.Text.Json
  ignora la clave `ProductDisplay` que ya no existe en el modelo.
- **Pruebas**:
  - Migración sobre una copia de la base real: se eliminaron `ProductIcons` y las columnas `Icon`,
    `IconUpdatedUtc` e `ImagePath`, y se conservaron **424 productos**.
  - Servidor temporal en el puerto 5099 con login `admin/admin123`: GET de 424 productos sin
    `Icon`/`HasIcon`, POST y PUT de producto sin icono correctos y
    `GET /api/products/{id}/icon` → **404**.
  - UI con UIAutomation: **Productos** muestra los encabezados Código, Producto, Categoría, Precio
    USD, Costo, Existencias, Mínimo, IVA y Estado, sin ComboBox ni botón **Buscar imagen…**;
    **Punto de venta** muestra **0 imágenes** y solo los botones Vaciar/Cobrar/Limpiar.
  - `dotnet build CyberManager.sln -c Release` con **0 errores y 0 advertencias**.
- **Instaladores 1.0.20**: `CyberManager-Servidor-1.0.20.exe` (268.993.803 bytes) y
  `CyberManager-Estacion-1.0.20.exe` (150.703.935 bytes). Instalación silenciosa del servidor con
  las tareas `desktopicon,serverstartup,firewall` (código de salida **0**): `/api/health` responde
  `ok`, el servidor y la consola quedaron en **1.0.20**, la tarea `CyberManagerServer` está
  **Running** y la base real quedó con **424 productos**, sin la tabla `ProductIcons` ni las
  columnas `Icon`, `IconUpdatedUtc` e `ImagePath`.
- **Release v1.0.20**: publicada en
  <https://github.com/Kis4r4gi/CyberManager/releases/tag/v1.0.20> con ambos instaladores y el
  código fuente GPLv2 de TightVNC 2.8.90.

### Carpeta de intercambio y mejoras de la consola (1.1.1, 2026-11-16)

- **Galería de fondos**: el buscador **Buscar fondo** filtra por nombre o estado y el resumen
  cambia según el filtro. Se agregó **Buscar en internet**, que abre un buscador de
  [Wallhaven](https://wallhaven.cc/api/v1/search) solo para todo público (SFW), formato 16:9 y
  resolución mínima de 1920 × 1080, 2560 × 1440 o 3840 × 2160, con categorías General, Anime,
  Personas o Todas, miniaturas, **Agregar** y paginación **Cargar más**. Se validan las URLs
  HTTPS de `wallhaven.cc`/`*.wallhaven.cc` y se rechazan imágenes de más de 20 MB antes y después
  de descargar. El mapeo JSON corrige `file_size` → `FileSize` y `last_page` → `LastPage` con
  `[JsonPropertyName]`.
- **Carpeta de intercambio**: **Abrir carpeta** exporta la galería a
  `%LOCALAPPDATA%\CyberManager\Admin\Fondos` como `NNN - Nombre.ext`, escribe el manifiesto
  `_sincronizacion.json` (`Id`, `File`, `Sha256`) y `_LEEME.txt`, y abre la carpeta en el
  Explorador. **Sincronizar carpeta** detecta archivos nuevos y modificados comparando el hash
  SHA-256, sube los cambios y elimina de la carpeta los fondos borrados en la consola. Los
  archivos nuevos se agregan con el nombre y el orden indicados al inicio del nombre. El nuevo
  endpoint `PUT /api/wallpapers/{id}/file` reemplaza la imagen y actualiza nombre, orden y estado,
  y emite `WallpaperChanged`; `ApiClient.PutFileAsync` y `CyberApi.UpdateWallpaperFileAsync`
  consumen ese endpoint. Los botones quedaron reorganizados: el encabezado solo conserva **Subir
  imágenes** y **Buscar en internet**; la barra de la galería tiene **Abrir carpeta**,
  **Sincronizar carpeta**, **Reenviar a equipos** y **Actualizar**; se quitó el duplicado **Vista
  previa** del panel lateral.
- **Notas y Registro**: el panel derecho muestra arriba las **notas** (texto simple, sin tarjetas,
  con autor y hora) y abajo el **registro** de actividad; se renombraron «Bitácora» → «Registro» y
  «Notas compartidas» → «Notas». Publicar y borrar una nota registra el contenido y el autor en el
  registro. El botón **Abrir** del registro lo exporta a
  `%LOCALAPPDATA%\CyberManager\Admin\registro-AAAAMMDD-HHMMSS.txt` en UTF-8 y lo muestra en el
  Bloc de notas; se eliminó **Limpiar**.
- **Identidad del negocio**: el nombre o razón social y el logo configurados en *Configuración →
  Negocio y facturación* aparecen juntos en una sola línea en la barra superior, sin segunda línea
  de texto. El logo se guarda en `Settings` (`BusinessLogo`, PNG en data URL), se excluye de
  `GetAllAsync`, tiene endpoints dedicados y se reescala a un máximo de 512 × 512 px.
- **Ventanas**: la consola recuerda la posición, el tamaño y el estado (normal o maximizado) de la
  ventana principal y del inicio de sesión por usuario de la aplicación y de Windows, en
  `%LOCALAPPDATA%\CyberManager\Admin\windows.json` (`main:<usuario>` y `login:<usuario de
  Windows>`); el inicio de sesión ahora es redimensionable.
- **Equipos**: la tabla ya no tiene marco, las columnas se ajustan automáticamente, el buscador
  **Buscar equipo** mide 200 px y los cuadros del área de equipos quedaron reorganizados. La
  eliminación de equipos se corrigió con `POST /api/stations/{id}/deactivate` (antes respondía
  **405**).
- **Política de versión**: `scripts/build-installer.ps1` ahora incrementa la **versión menor** y
  reinicia el patch (`1.1.1` → `1.2.0` → `1.3.0`…); `version.props`, el README y los comentarios
  del script quedaron alineados con esa política.
- **Pruebas**:
  - API real: `PUT /api/wallpapers/{id}/file` actualizó nombre, hash, orden y estado; el listado
    reflejó el hash nuevo y el fondo de prueba se eliminó (30 fondos finales).
  - UIAutomation: **Abrir carpeta** exportó 31 imágenes + `_sincronizacion.json` (31 entradas) +
    `_LEEME.txt`; modificar `999 - PRUEBA CARPETA.png` y **Sincronizar carpeta** actualizó el fondo
    (hash nuevo); copiar `900 - PRUEBA NUEVA.png` y sincronizar creó el fondo con nombre y orden
    correctos; al eliminar los fondos de prueba y volver a abrir la carpeta quedaron **30 fondos**,
    30 entradas y ningún archivo de prueba.
  - Wallhaven: búsqueda «gaming» con 50.382 resultados, página 1 de 2100, tamaños correctos y
    agregar/eliminar una imagen de prueba sin dejar residuos.
  - `dotnet build CyberManager.sln -c Release` con **0 errores** (8 advertencias de nulabilidad
    preexistentes en el cliente).
- **Instaladores 1.1.1**: `CyberManager-Servidor-1.1.1.exe` (269.033.089 bytes) y
  `CyberManager-Estacion-1.1.1.exe` (150.713.665 bytes), generados con
  `-Version 1.1.1 -SkipVncDownload`.
- **Release v1.1.1**: publicada en
  <https://github.com/Kis4r4gi/CyberManager/releases/tag/v1.1.1> con ambos instaladores y el
  código fuente GPLv2 de TightVNC 2.8.90.
- **Servidor local**: publicado y reiniciado con el nuevo endpoint; `/api/health` responde `ok` y
  la consola instalada quedó en **1.1.1** con la galería de 30 fondos.

### Escaneo de red, interfaz adaptable y actualización de clientes (1.2.0, 2026-11-16)

- **Escaneo de la red local**: el servidor difunde y barre la red por UDP (**5092** cliente,
  **5093** agente). El cliente y el agente responden con nombre de equipo, IP, MAC, versión,
  rol, `StationId` y token (`CyberManager.Shared.Discovery`, `DiscoveryMessage`,
  `NetworkIdentity`). `NetworkScanService` recibe las respuestas durante unos segundos y
  `POST /api/stations/scan` devuelve los equipos encontrados con `IsNew`, `NeedsToken` y la
  estación ya registrada si coincide por ID, IP o MAC.
- **Alta y configuración automática**: `POST /api/stations/scan/apply` crea o actualiza los
  equipos seleccionados, les asigna token si falta y se lo envía por UDP
  (`DiscoveryMessage` de configuración). El cliente lo guarda en `config.json` y el agente en
  `appsettings.json` (`AgentConfigStore`), y ambos reconectan solos. En la consola,
  **Equipos → Escanear la red** abre `NetworkScanDialog` con selección por fila, IP, MAC,
  versión y estado.
- **Interfaz adaptable**: `Kit.PageHeader` usa un `WrapPanel` y pasa los botones a una segunda
  fila cuando el ancho baja de 760 px; `FormDialog` limita ancho y alto al área de trabajo y
  desplaza el contenido en pantallas pequeñas.
- **Equipos compacto**: se quitaron las tarjetas de métricas; junto al buscador **Buscar equipo**
  hay un `ComboBox` de estado (Todos, En línea, Disponibles, En uso, En pausa, Mantenimiento,
  Desconectados) y el resumen de equipos visibles/en línea/en pausa.
- **Actualización automática de los clientes**: `ClientUpdateService` publica el manifiesto
  (`version`, `fileName`, `size`, `sha256`, `publishedUtc`) y el ZIP del cliente desde
  `Server/ClientUpdate`; endpoints anónimos `/api/client-update/manifest` y `/download`, y
  `/status` + `/notify` para administradores. `ClientUpdateInstaller` descarga, verifica el
  SHA-256, extrae, reemplaza los archivos (renombrando los que están en uso a `.old`) y deja
  `update.pending`; la estación se reinicia con los binarios nuevos.
- **Agente y cliente**: `ClientUpdateApplier` del agente (SYSTEM) aplica el paquete; el cliente
  también lo consulta cada 10 minutos y se autoactualiza si puede escribir su carpeta. El
  servidor difunde `ClientUpdateAvailable` por SignalR a estaciones, agentes y consola.
- **Box Clientes**: en *Equipos* muestra la versión del paquete, equipos al día, pendientes y
  sin versión, con **Actualizar clientes** (aviso inmediato) y **Revisar**.
- **Instalador del servidor**: `scripts/build-installer.ps1` genera
  `dist\<arch>\Server\ClientUpdate\client.zip` + `manifest.json` y el instalador copia la carpeta
  completa. El instalador de estación habilita el firewall UDP 5092/5093 y
  `configurar-agente.ps1` permite dejar el agente sin ID/token a la espera del escaneo.
- **Pruebas** (servidor temporal en el puerto **5099** con copia de la base real):
  `/api/health` → `ok`; login admin; `/api/client-update/status` y `/manifest` con el paquete de
  prueba; `/api/stations/scan` vacío sin respondedores; `/api/stations/scan/apply` creó
  `PC-PRUEBA-ESCANEO` (id 11) y devolvió `configured: 1`; `/api/client-update/notify` respondió
  `sent: true`; un respondedor UDP de PowerShell en 5092 permitió capturar `PC-UDP-TEST`
  (`isNew: true`, `needsToken: true`) de extremo a extremo. `dotnet build CyberManager.sln -c Debug`
  y `-c Release` con **0 errores** y **0 advertencias**.
- **Instaladores 1.2.0**: `CyberManager-Servidor-1.2.0.exe` (**396.164.653 bytes**) y
  `CyberManager-Estacion-1.2.0.exe` (**150.758.922 bytes**), generados con
  `build-installer.ps1 -SkipVncDownload`. El paquete del cliente quedó en
  `dist\x64\Server\ClientUpdate\client.zip` (67.536.484 bytes, SHA-256
  `9ce57489…f259d9`) y `dist\x86` (61.788.403 bytes).
- **Release v1.2.0**: publicada en
  <https://github.com/Kis4r4gi/CyberManager/releases/tag/v1.2.0> el `2026-11-16T17:48:34Z` con
  ambos instaladores y `tightvnc-2.8.90-src-gpl.zip` (3.018.194 bytes).
- **Servidor local 1.2.0**: detenido, respaldado en
  `C:\CyberManager\Servidor\_preupdate_20261116_134528`, actualizado desde `dist\x64` y
  reiniciado; `/api/health` responde `ok`, la tarea `CyberManagerServer` está **Running**, la
  consola quedó en **1.2.0** y `Server\ClientUpdate` contiene el paquete 1.2.0.

### Autoservicio prepago y acceso de administrador en el cliente (2026-11-17, sin publicar)

- **Backend prepago**: `Customer` incorpora `Username`, `PasswordHash`, `PasswordSalt` y
  `PrepaidMinutes` (migración e índice único en `DbSeeder`). `PrepaidService` y
  `PrepaidEndpoints` exponen `/api/station/prepaid/info|register|login|recharge|start|finish`,
  autenticados con el ID/token de la estación.
  - `register` crea la cuenta con `PasswordHasher` y saldo 0.
  - `recharge` cobra el paquete al `Customer.Balance`; si hay una sesión prepago activa en la
    estación, suma los minutos a `Session.PlannedMinutes` y el importe a `Session.AmountCharged`;
    si no, los suma al monedero `Customer.PrepaidMinutes`.
  - `start` crea la sesión `Prepaid`, descuenta los minutos del monedero y envía `Unlock` por
    SignalR.
  - `finish` llama a `SessionService.StopAsync` con `CustomerBalance` y `ChargeTime: false`,
    devuelve los minutos no usados al monedero y cobra los consumos pendientes al saldo.
    `SessionService.StopAsync` ya no exige una caja abierta cuando el cobro es con
    `PaymentMethod.CustomerBalance`; solo los medios en efectivo/tarjeta/transferencia/Pago Móvil
    la requieren.
- **Cliente**: `LockWindow` muestra el botón **Iniciar sesión** con el menú *Usuario prepagado* /
  *Administrador*; se eliminó el atajo Ctrl+Shift+F12 y el bloqueo de teclado ya no lo permite.
  `PrepaidWindow` gestiona registro, login, compra, inicio y cambio de cuenta; `AdminLoginWindow`
  exige rol `Admin`; `ClientSettingsWindow` lee y guarda la configuración del servidor
  (`/api/settings`) y el idioma local. `SessionBarWindow` muestra **Recargar tiempo** y
  **Finalizar sesión** solo en sesiones prepago. `App.xaml.cs` orquesta el modo administrador,
  la recarga/cierre y el refresco del estado.
- **Ventana de login del cliente**: `PrepaidWindow` ya no usa 680×820 fijos. Abre con ancho 560,
  `SizeToContent="Height"` (login 560×489, registro 560×684), `MaxWidth`/`MaxHeight` calculados
  desde `SystemParameters.WorkArea` y recentrado al cambiar de panel, por lo que nunca queda
  recortada en pantallas de 1366×768 o menores.
- **Pruebas**: `dotnet build CyberManager.sln -c Release` con **0 errores** y **0 advertencias**.
  Servidor temporal en **5099** con base nueva (`%TEMP%\cm-prepaid-test\test.db`): login admin;
  registro `jugador…`; recarga de saldo de 5 USD; compra de «30 minutos» (0,50 USD); inicio de
  10 min; estado `Prepaid/Active` con plan 10 y restante 10; recarga durante la sesión (+30 min,
  `extendedSession: true`, plan 40); cierre con devolución de 40 min y monedero 60; estado final
  `Available` sin sesión. Errores verificados: contraseña incorrecta, recarga sin saldo, inicio
  sin minutos, registro duplicado y cierre sin sesión.
- **Cierre con consumos pendientes**: con una sesión prepago activa y un pedido de 0,50 USD sin
  caja abierta, `finish` devolvió los 10 min y descontó el consumo del saldo (5,00 − 0,50 del
  paquete − 0,50 del producto = **4,00 USD**), dejando la estación `Available`. Se repitió el E2E
  completo con la base nueva y siguió pasando.
- **Pendiente**: probar la UI del cliente contra una estación real y regenerar instaladores y
  release cuando el usuario lo pida; no se publica nada en GitHub todavía.

### Dos cajas en la misma red (2026-11-17, sin publicar)

- **Caja por terminal**: `CashSession.Terminal` identifica el turno. `CashTerminalContext` lee la
  cabecera `X-Cash-Terminal` y la normaliza (mayúsculas, máximo 40 caracteres, `PRINCIPAL` si
  falta). `CashService` filtra apertura, turno actual, movimientos, cierre e historial por terminal,
  y rechaza un segundo turno en la misma caja. `DbSeeder` agrega la columna `Terminal` y rellena
  `PRINCIPAL` en los turnos existentes.
- **Consola**: el login del Admin pide **Caja de este equipo**, la guarda en
  `AppConfig.TerminalName` y la envía en todas las peticiones. `CashView` muestra el nombre de la
  caja en el estado y la columna **Caja** en el historial.
- **Cobros y anulaciones**: `SessionService` registra los cobros en la caja donde se abrió la sesión
  (`Session.CashSessionId`); si esa caja ya cerró, usa la del terminal que cierra. `SaleService`
  anula en la caja de la venta si sigue abierta. El prepago con saldo del cliente sigue sin exigir
  caja.
- **Pruebas**: `dotnet build CyberManager.sln -c Release` con **0 errores** y **0 advertencias**.
  Servidor temporal en **5099** con base nueva (`%TEMP%\cm-cash-test\test.db`): dos aperturas
  simultáneas (CAJA-1 y CAJA-2), rechazo **409** de un segundo turno en la misma caja, turnos y
  depósitos aislados, cierres independientes, historial con la columna de caja, compatibilidad sin
  cabecera (`PRINCIPAL`) y cierre de ambas cajas. También se abrió una base anterior a la migración
  (respaldo del 17/11): los turnos viejos quedaron en `PRINCIPAL` y se abrió/cerró un turno nuevo en
  otra caja.
- **Pendiente**: probar dos consolas reales en la red y regenerar instaladores/release cuando el
  usuario lo pida.

### Instalador unificado (1.3.0, 2026-11-17)

- **Un solo ejecutable para ambos roles**: `installer/CyberManager.Unificado.iss` usa el AppId
  `{E7F1C3A9-5B24-4D68-9F13-8C6A2D4B7E95}`, `DefaultDirName=C:\CyberManager`, `[Types]`
  `servidor`/`estacion`/`completo` y `[Components]` con los mismos nombres. Las tareas, los iconos,
  `[Files]`, `[Run]`, `[UninstallRun]`, `[InstallDelete]` y `[UninstallDelete]` se filtran con
  `Components:`/`Tasks:` para que cada rol reciba solo lo suyo; la página de VNC solo aparece con
  la estación.
- **Rutas equivalentes**: el servidor se copia en `{app}\Servidor` y la estación en
  `{app}\Estacion`, con los mismos payloads, scripts, lanzador y accesos directos que los
  instaladores separados, así que la base de datos y la configuración local siguen funcionando.
- **Migración automática**: `RemoveOldInstall` busca los AppId anteriores (`{A3B5D8E1-…}` servidor y
  `{C9D2E4F6-…}` estación) en el registro y ejecuta su desinstalador con
  `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART` desde `PrepareToInstall`, antes de copiar archivos. No
  borra `cybermanager.db` ni `%LOCALAPPDATA%\CyberManager\Client`.
- **Empaquetado**: registrado en `$installers` de `scripts/build-installer.ps1` como
  `CyberManager-Unificado-<versión>.exe` y compilado junto con los otros dos.
- **Pruebas de compilación**: `scripts/build-installer.ps1 -SkipPublish` compiló los tres
  instaladores con Inno Setup sin errores ni advertencias. `CyberManager-Unificado-1.3.0.exe` pesa
  505,4 MB (servidor 374,4 MB + estación 143,8 MB en un solo bloque sólido).
- **Prueba real en este equipo (2026-11-17, sesión elevada, servidor 1.2.0 → 1.3.0)**:
  - Respaldo previo en `artifacts\backup-pre-unificado-20261117-113642` (`cybermanager.db`,
    `appsettings.json` y XML de la tarea programada).
  - `/TYPE=servidor`: desinstaló en silencio el 1.2.0 anterior, migró a 1.3.0, recreó la tarea
    `CyberManagerServer` (Running), abrió el puerto 5080 y la regla de firewall, dejó solo los
    iconos del servidor y una única entrada `CyberManager 1.3.0` en **Aplicaciones instaladas**.
    Datos intactos: 53.796.864 bytes de base, login `admin/admin123`, 4 estaciones y 424 productos.
  - `/TYPE=estacion /MERGETASKS="!startupclient,!desktopicon_station"`: agregó
    `Estacion\Client`, `Estacion\Agent` y `Estacion\scripts`, sumó los iconos de ambos roles y no
    tocó los archivos ni el proceso del servidor. Sin arranque automático del cliente.
  - Desinstalación silenciosa (`C:\CyberManager\unins000.exe /VERYSILENT`): eliminó tareas,
    firewall, registro, menú Inicio y binarios, y **conservó** `Servidor\Server\cybermanager.db`
    (mismo tamaño).
  - Reinstalación final `/TYPE=servidor`: tarea Running, `/api/health` OK, versión 1.3.0, datos
    intactos y una sola entrada de registro. Queda `Estacion\vnc\instalacion.log` como resto de una
    prueba anterior (el desinstalador no borra `vnc`).
- **Pendiente**: publicar cuando el usuario lo pida.

---

### Correcciones del cliente: foco, contraseña, ESC, fondos, desinstalación, elevación y reloj (2026-11-17, sin publicar)

- **Foco del login**: `App.ShowLock` ya no llama a `Activate()` en cada refresco de 5 segundos.
  Solo activa la pantalla de bloqueo si no está activa y no hay diálogo abierto, menú flotante
  abierto, modo administrador ni `SuppressActivation`. El cursor permanece en los cuadros del
  login mientras se escribe.
- **Mostrar contraseña**: `PrepaidWindow` incluye `PasswordRevealBox` (login) y
  `RegisterPasswordRevealBox`/`RegisterConfirmRevealBox` (registro) con las casillas
  **Mostrar contraseña** y **Mostrar contraseñas**. Un `TextBox` sincronizado con el `PasswordBox`
  conserva el texto al alternar y mantiene el foco; `SwitchAccount_Click` limpia ambos.
- **ESC en el menú flotante**: `LockWindow` maneja `PreviewKeyDown` en la ventana y en el `Popup`;
  con `IsLoginMenuOpen` cierra el menú sin cerrar la pantalla de bloqueo.
- **Rotación de fondos tipo salvapantallas**: `WallpaperService` usa `GetLastInputInfo` y un
  temporizador de 1 s. La rotación empieza tras `IdleDelay = 2 min` sin teclado ni ratón, avanza
  con el intervalo configurado y se detiene con cualquier actividad o al desbloquear. Se mantiene
  el fondo actual y se reanuda al volver la inactividad. El orden `Random` elige el siguiente índice
  sin repetir el actual; `LockWindow` aplica la transición configurada (fundido, deslizamiento o
  zoom). Cada equipo avanza de forma independiente: ya no existe el evento SignalR
  `WallpaperChanged` ni el botón **Reenviar a equipos**.
- **Cerrar y desinstalar desde el cliente**: `ClientSettingsWindow` agrega la sección
  **Mantenimiento** con el botón `UninstallButton` (estilo `DangerButton`). Pide confirmación,
  lanza `%ProgramData%\CyberManager\CyberManager.Uninstaller.exe` con `Verb="runas"` y expone
  `UninstallRequested`; `App.OnAdminLoginRequested` cierra el cliente sin volver a mostrar el
  bloqueo. La base de datos y la configuración se conservan.
- **Cliente elevado**: `src/CyberManager.Client/app.manifest` usa
  `requestedExecutionLevel level="requireAdministrator"`. El instalador ya no crea el acceso
  directo de `{commonstartup}`; registra la tarea programada `CyberManagerClient` (XML con
  `GroupId S-1-5-32-545`, `RunLevel HighestAvailable`, `LogonTrigger` con `PT15S`) mediante el
  nuevo `installer/scripts/configurar-cliente.ps1` (`-Start`, `-Remove`, `-TaskName`,
  `-ClientExe`). La tarea se inicia con `schtasks /Run /TN "CyberManagerClient"` al finalizar la
  instalación y se elimina en `[UninstallRun]`. `scripts/install-client-startup.ps1` reutiliza el
  mismo script y se eliminó su parámetro `-AllUsers`.
- **Reloj de 12 horas**: `LockWindow` muestra `h:mm:ss tt` con la hora local del equipo
  (Windows). Ya no aplica el desfase de `state.ServerTimeUtc`.
- **`configurar-agente.ps1`**: se quitó `#Requires -RunAsAdministrator`, que impedía analizar el
  script y solo mostraba un error de sintaxis. Ahora se auto-eleva con `Start-Process -Verb RunAs`
  conservando `$PSBoundParameters` y `-NoExit`.
- **Instaladores**: `CyberManager.Unificado.iss` y `CyberManager.Estacion.iss` registran la tarea
  de inicio, eliminan el acceso directo anterior de `{commonstartup}` en `[InstallDelete]`, abren
  el cliente con `ShouldOpenClientTask`/`ShouldOpenClientDirect` y quitan la tarea al desinstalar.
  `scripts/build-installer.ps1` copia `configurar-cliente.ps1` a `dist\<arch>\Scripts`.
- **Menú desplegable por equipo (consola)**: `StationsView` eliminó la fila de botones que dependía
  de la selección y agregó la columna **Acciones** en cada fila. El menú incluye subir, bajar,
  editar, cambiar nombre, editar conexión, configuración/token, ver pantalla, bloquear, mensaje,
  mantenimiento, activar/desactivar y eliminar. El clic derecho abre el mismo menú y cada acción
  selecciona primero su fila.
- **Pruebas**: `dotnet build CyberManager.sln` con **0 errores y 0 advertencias**. Los seis
  scripts de `installer/scripts` parsean en PowerShell 5.1. La tarea de prueba
  (`CyberManagerClientTest`) se registró con `GroupId Usuarios`, `RunLevel Highest` y
  `Delay PT15S`, y `-Remove` la eliminó. Las variantes sin compresión de Estación y Unificado
  compilaron con ISCC. La instalación silenciosa de estación creó `Client`, `Agent` y `scripts`,
  registró `CyberManagerClient` y **no** creó el acceso directo en Inicio; el manifiesto del
  ejecutable instalado es `requireAdministrator`. El desinstalador silencioso eliminó la tarea y
  los binarios y conservó `Servidor\Server\cybermanager.db`.
- **Nota**: la tarea con `HighestAvailable` solo abre el cliente elevado si el usuario de Windows
  es administrador local; con una cuenta estándar Windows pedirá credenciales.
- **Pendiente**: recompilar los instaladores para incluir estos cambios y publicar cuando el
  usuario lo pida.

---

### Registros persistentes del servidor y despliegue de prueba en PC 01 (2026-11-17, sin publicar)

- **Registros persistentes**: `CyberManager.Server/Logging/FileLoggerProvider.cs` escribe cada
  entrada en `<servidor>\Logs\server-AAAA-MM-DD.log` en modo *append*, con fecha/hora, nivel y
  categoría. Los registros **no se borran al reiniciar**; los archivos con más de **30 días** se
  eliminan al arrancar y al cambiar de día. Si un archivo supera 32 MB continúa en `.1`, `.2`, etc.
  La retención se configura con `Server:LogRetentionDays` (30 por defecto).
- **Program.cs**: registra el proveedor, anota la carpeta del registro al arrancar y guarda en el
  archivo las excepciones no controladas y las tareas no observadas. `appsettings.json` silencia
  `System.Net.Http.HttpClient` para no llenar el archivo con cada consulta de tasas.
- **Pruebas**: se creó `server-2026-10-01.log` y se reinició el servidor: se eliminó; un archivo
  del 2026-11-10 se conservó. El archivo del 2026-11-17 mantiene las líneas de los reinicios
  anteriores y agrega las nuevas. `/api/health` responde `ok` y la tarea sigue `Running`.
- **Despliegue de prueba**: paquete `ClientUpdate` **1.3.1** (SHA-256 `34f20194…`) publicado en el
  servidor. PC 01 (`192.168.1.14`, `stationId 11`) descargó y aplicó la actualización y reporta
  **1.3.1** sin bucle de actualización. El servidor instalado también quedó en **1.3.1**.
- **Pendiente**: recompilar los instaladores 1.3.1 cuando el usuario lo pida; sin push ni release.

### Detalle de consumos, versión del cliente y acción por equipo (1.3.2, 2026-11-17, sin publicar)

- **Cobrar y cerrar**: el diálogo muestra los productos consumidos (cantidad, precio unitario y
  total) agrupados por producto y precio, e indica cuánto ya está cobrado y cuánto queda pendiente.
  Se alimenta del endpoint `GET /api/sessions/{id}/consumption`.
- **Versión visible**: la pantalla de bloqueo y el pie de **Configuración del cliente** muestran la
  versión instalada (`AppInfo.Version`).
- **Tabla de equipos**: se quitaron las columnas **Plataforma**, **Usuario Windows** y **Sesión**;
  la información sigue disponible en el resumen del equipo y en la edición.
- **Actualizar cliente por equipo**: nueva acción en el menú **Acciones ▾**. El endpoint
  `POST /api/client-update/notify?stationId=` envía el manifiesto solo al cliente y al agente de esa
  estación. La acción global **Actualizar clientes** del box **Clientes** sigue avisando a todos.
- **Despliegue local 1.3.2**: servidor y consola copiados a `C:\CyberManager\Servidor`, tarea
  `CyberManagerServer` en `Running`, `/api/health` → `ok`. El paquete `ClientUpdate` 1.3.2
  (SHA-256 `10050cf1…`) se envió a PC 01 y el estado quedó **1 al día, 0 pendientes**.

### Columnas de equipos y reloj local (1.3.3, 2026-11-18, sin publicar)

- **Tabla de equipos**: las columnas se ajustan automáticamente al ancho del encabezado y del
  texto de cada celda (`ColumnWidth` y `Width` en `Auto`, `MinWidth = 0`) y siguen siendo
  redimensionables arrastrando el borde del encabezado.
- **Reloj del cliente**: `LockWindow` usa `DateTime.Now` del propio equipo; se eliminó
  `_serverOffset` y la corrección con `state.ServerTimeUtc`.
- **Despliegue local 1.3.3**: servidor y consola copiados a `C:\CyberManager\Servidor`, tarea
  `CyberManagerServer` en `Running`, `/api/health` → `ok`, paquete `ClientUpdate` 1.3.3
  (SHA-256 `8ac9532a…`). PC 01 estaba desconectado al momento del aviso; aplicará la versión al
  volver a conectarse.

### Reloj, energía, latido y configuración del cliente (1.3.4, 2026-11-18, sin publicar)

- **Reloj irregular**: el `DispatcherTimer` de `LockWindow` nunca se iniciaba, así que la hora solo
  cambiaba cuando llegaba un estado del servidor (cada 5 s). Ahora el temporizador arranca en
  `Loaded` (250 ms, prioridad `Render`), se detiene al cerrar y solo redibuja al cambiar el segundo.
- **Barra de sesión**: el contador usa un ancla (`_anchorAt`/`_anchorSeconds`) que solo se reajusta
  al cambiar de sesión, al pausar/reanudar o si la desviación supera 90 s; antes retrocedía hasta
  59 s con cada aviso del servidor.
- **Energía**: `PowerManagementService` usa `SetThreadExecutionState` con `ES_CONTINUOUS |
  ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED` al iniciar y cada 30 s; impide suspensión, hibernación
  y apagado de pantalla. Se libera en `OnExit`.
- **Latido**: el cliente envía el latido aunque falle `StateAsync`, y `StationHub` (además de
  `AgentHub`) devuelve el equipo a **Disponible** y avisa cuando vuelve a reportar.
- **Configuración del cliente**: las rejillas de **Datos del negocio** y **Apariencia** no tenían
  filas, por lo que los campos se superponían. Se añadieron `RowDefinitions` y posiciones
  `Grid.Row` para que cada etiqueta y caja tenga su propia línea.
- **Despliegue local 1.3.4**: servidor y consola copiados a `C:\CyberManager\Servidor`, tarea
  `CyberManagerServer` en `Running`, `/api/health` → `ok`, paquete `ClientUpdate` 1.3.4
  (SHA-256 `6b553ef6…`). PC 01 se actualizó a 1.3.4 y quedó **Disponible/en línea**.

### Estado en línea confiable y refresco automático (1.3.5, 2026-11-18, sin publicar)

- **Presencia por latido**: `StationPresence` calcula si un equipo está en línea con su
  `LastSeenUtc` (margen de 90 s) en lugar de mirar el estado de ocupación. Así un PC apagado se
  muestra **Sin conexión** aunque tenga una sesión abierta, y un equipo en mantenimiento apagado
  ya no cuenta como «en línea».
- **Lista de equipos**: `StationsView` no se refrescaba después de abrirse, por lo que conservaba
  el estado viejo (PC 01 aparecía desconectado aunque estuviera reportando). Ahora se suscribe a
  `StationStateChanged` y a los eventos `StationOnline/Offline`, `AgentOnline/Offline`,
  `SessionStarted/Ended` y `StationMaintenance`, y además recarga cada 20 s. La fila seleccionada
  se conserva entre refrescos.
- **Filtros y resumen**: **En línea** / **Desconectados** usan la presencia por latido; el resumen
  «X en línea» ya no incluye equipos apagados en mantenimiento.
- **Tablero**: `DashboardService` cuenta las estaciones en línea con `StationPresence`, y cada
  tarjeta muestra **Sin conexión** si el equipo dejó de reportar, aunque el estado guardado sea
  «En uso» o «Disponible».
- **Despliegue local 1.3.5**: servidor y consola copiados a `C:\CyberManager\Servidor`, tarea
  `CyberManagerServer` en `Running`, `/api/health` → `ok`. El paquete `ClientUpdate` se mantuvo en
  1.3.4 porque el cliente no cambió; PC 01 sigue en 1.3.4 y reporta en línea (latido < 5 s).

### Barra de sesión compacta, icono flotante y reloj prepago regresivo (1.3.6, 2026-11-18, sin publicar)

- **Barra más pequeña**: se redujeron rellenos, tipografías e iconos de la barra de sesión para
  ocupar menos espacio sin perder el tiempo, el consumo ni los accesos a pedidos y cuenta.
- **Icono flotante**: el botón **▴** oculta la barra y deja un icono compacto con el tiempo de la
  sesión (y un signo de aviso cuando queda poco). Al pulsarlo se restaura la barra. El estado se
  conserva mientras el cliente siga abierto.
- **Reloj prepago regresivo**: el contador usaba el ancla del postpago y sumaba segundos, por lo
  que en prepago avanzaba en vez de descontar. Ahora `SessionBarWindow` distingue el sentido del
  reloj: en prepago resta desde `RemainingMinutes` y, al llegar a cero, muestra el excedente con
  signo **+**; en postpago sigue sumando el tiempo transcurrido.
- **Despliegue local 1.3.6**: servidor, consola y paquete `ClientUpdate` 1.3.6 copiados a
  `C:\CyberManager\Servidor` (SHA-256 `cfc00690…`, respaldo `_clientupdate-1.3.5-20261117-2231`),
  tarea `CyberManagerServer` en `Running`, `/api/health` → `ok`. PC 01 se actualizó a 1.3.6 y
  reporta en línea.

### Nombre del cliente en la barra y sin textos de relleno (1.3.7, 2026-11-18, sin publicar)

- **Nombre del cliente junto al equipo**: la barra muestra `PC 01 · Nombre del cliente`; si la
  sesión no tiene cliente registrado solo se muestra el nombre del equipo.
- **Sin «Sesión activa»**: se eliminaron los textos de relleno del cliente y de la descripción.
  La descripción (`SessionBarDescription`) ahora es opcional: si está vacía no se dibuja ningún
  renglón extra. El valor por defecto del sembrador quedó vacío y en la instalación local se
  limpió el texto anterior.
- **Despliegue local 1.3.7**: servidor, consola y paquete `ClientUpdate` 1.3.7 copiados a
  `C:\CyberManager\Servidor` (SHA-256 `5a8694c4…`, respaldo `_clientupdate-1.3.6-20261117-2236`),
  tarea `CyberManagerServer` en `Running`, `/api/health` → `ok`. PC 01 se actualizó a 1.3.7 y
  reporta en línea.

### Reporte de caja en turno abierto y «Actualizar cliente» en la tabla (1.3.8, 2026-11-18, sin publicar)

- **Reporte de caja con turno abierto**: `CashService.HistoryAsync` devolvía `sales`/`expected` en
  cero para el turno abierto porque leía `ExpectedAmount` persistido, que solo se calcula al
  cerrar. Ahora el historial calcula ventas, gastos y esperado a partir de los movimientos con el
  helper `ExpectedAmount(...)`, el mismo que usa `ToDtoAsync`. Verificado en producción:
  `/api/reports/cash` devuelve `sales: 3.0`, `expected: 3.0` para el turno abierto #3, igual que
  `/api/cash/current`.
- **Actualizar cliente en la tabla de equipos**: el menú **Acciones ▾** de `StationsView` no
  ofrecía la actualización del cliente que sí estaba en el diálogo de acciones. Se añadió
  `UpdateClient()` con confirmación, `NotifyClientUpdateAsync(station.Id)` y refresco posterior.
  El menú queda completo: Subir, Bajar, Editar, Cambiar nombre, Editar conexión,
  Configuración / token, Ver pantalla, Bloquear, Mensaje, **Actualizar cliente**, Mantenimiento,
  Activar / desactivar, Eliminar equipo.
- **Despliegue local 1.3.8**: servidor y consola copiados a `C:\CyberManager\Servidor` (respaldo
  `_bin-pre-1.3.8-20261117-2257`), tarea `CyberManagerServer` en `Running`, `/api/health` → `ok`.
  El paquete `ClientUpdate` se mantuvo en 1.3.7 porque el cliente no cambió; PC 01 sigue en 1.3.7
  y reporta en línea.

### Correcciones altas y medias del cliente y del servidor (1.3.9 → 1.3.10, 2026-11-18, sin publicar)

- **Órdenes del menú Acciones que no llegaban al cliente (alto)**: las conexiones SignalR de la
  estación (`StationRealtime`), de la consola (`AdminRealtime`) y del agente (`AgentWorker`) no
  registraban `JsonStringEnumConverter`, pero el servidor envía los enums como texto
  (`"command":"Message"`). El mensaje llegaba al socket y fallaba al deserializar, así que el
  manejador nunca se ejecutaba: mensajes, capturas de pantalla, control remoto, cambio de fondos,
  volumen y apagado remoto quedaban sin efecto. Se añadió
  `AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))`
  a las cuatro conexiones. Verificado en vivo: el mensaje se mostró, `KillProcess notepad` cerró el
  Bloc de notas y `KillProcess explorer` fue rechazado por la lista de procesos protegidos.
- **Sesión iniciada desde la consola dejaba el equipo bloqueado (alto)**: `SessionService.StartAsync`
  enviaba `Unlock` pero no limpiaba `Station.IsLocked`, por lo que el cliente volvía a bloquearse en
  el siguiente refresco de estado (5 s). Ahora el inicio guarda `IsLocked = false`; `StopAsync`
  guarda `IsLocked = true` y `TransferAsync` bloquea el origen y desbloquea el destino. Verificado:
  la sesión #6 iniciada por API desbloqueó el cliente y el cierre lo volvió a bloquear.
- **Cliente (medios)**: instancia única con mutex `Local\CyberManagerClient-<equipo>` y espera de
  15 s durante una actualización; manejadores globales de excepción con `client-errors.log`; registro
  `client.log` rotativo de 1 MB; token de estación cifrado con DPAPI (`TokenProtector`), migración
  desde texto plano y guardado atómico de `AppConfig`; `InputBlocker` con TTL de 10 min y liberación
  al desconectar; `KillProcess` con lista de procesos protegidos; `DiscoveryResponder` solo acepta
  `Configure` del servidor configurado y no entrega el token si el equipo ya está configurado;
  validación y normalización de IP/MAC en el asistente; `SessionBackupService` ya no infla el total
  en prepago; barra y ventana prepago usan el símbolo de moneda configurado y la contraseña se limpia
  al cerrar; relanzador de actualización con espera y marca `update.pending` borrada tras arrancar;
  retirada la casilla `KioskMode`; `configurar-cliente.ps1` registra la tarea
  `CyberManagerClientWatchdog`.
- **Servidor (medios)**: reembolso prepago con `SessionCalculator.BilledMinutes` (techo) para no
  devolver minutos no consumidos; `ExtendAsync` y `RechargeAsync` desbloquean el equipo al
  ampliar/recargar; login prepago bloqueado 60 s tras 5 fallos (HTTP 429); `RequireStationAsync`
  exige `X-Station-Token` cuando el equipo ya tiene token.
- **Versión 1.3.10**: el paquete 1.3.9 llegó a PC 01 antes del arreglo de SignalR y, como el cliente
  solo actualiza a una versión mayor, la corrección se publicó como **1.3.10**. PC 01 se actualizó
  sola y reporta 1.3.10 con la sesión prepago #2 (id 4) intacta; la base conserva los 424 productos.
- **Despliegue local 1.3.10**: respaldos `_bin-pre-1.3.9-20261117-2341`,
  `_admin-pre-1.3.9-20261117-2341`, `_bin-pre-1.3.9-fix-20261117-2352` y
  `_bin-pre-1.3.10-20261118-0749`; servidor y consola copiados a `C:\CyberManager\Servidor`
  (excluyendo `cybermanager.db*`, `appsettings.json` y `web.config`); `ClientUpdate\client.zip`
  1.3.10 (67.670.321 bytes, SHA-256 `82f48e47…`); tarea `CyberManagerServer` en `Running`,
  `/api/health` → `ok` y manifiesto 1.3.10.
- **Pruebas en vivo**: estaciones temporales 13 y 14 (desactivadas al terminar) con las sesiones #5
  y #6 cerradas con `ChargeTime=false`, sin movimientos de caja. Se verificaron bloqueo, login de
  administrador, configuración sin campos superpuestos y sin «Modo kiosco», barra compacta con el
  nombre del equipo, «Mi cuenta», «Pedir productos», cierre y rebloqueo.
- **Pendiente**: reinstalar el **agente** en PC 01 con `installer\scripts\configurar-agente.ps1`
  para que reciba el arreglo de SignalR (apagado, reinicio y cierre de sesión remotos); el
  actualizador de cliente solo reemplaza la carpeta `Client`.

---

### Reloj retirado y fondo más visible en el bloqueo (1.3.11, 2026-11-18, sin publicar)

- **Sin reloj ni fecha en la pantalla de bloqueo**: se quitaron `ClockText`/`DateText` de
  `LockWindow.xaml` y el temporizador que los actualizaba (`_clockTimer`, `_lastClockSecond`,
  `UpdateClock` y `Capitalize`). La pantalla conserva el icono, «EQUIPO BLOQUEADO», el mensaje, el
  botón de inicio de sesión y el pie con la tasa, el texto y la versión.
- **Fondo más visible**: el velo del bloqueo pasó de `#99`/`#CC`/`#F2` (60/80/95 %) a
  `#33`/`#73`/`#B3` (20/45/70 %), de modo que el fondo sincronizado se aprecia con claridad sin
  perder legibilidad en el texto.
- **Versión 1.3.11** en `version.props`; publicación x64 regenerada
  (`dist\x64\{Server,Admin,Client,Agent,Uninstaller}`) y paquete `ClientUpdate\client.zip` 1.3.11
  (67.669.609 bytes, SHA-256 `dfda5411…`, `publishedUtc` 2026-11-18T11:56:11.638Z).
- **Despliegue local**: respaldo `_bin-pre-1.3.11-20261118-0800`; servidor y consola copiados a
  `C:\CyberManager\Servidor` (sin tocar `cybermanager.db*`, `appsettings.json` ni `web.config`);
  tarea `CyberManagerServer` en `Running`, `/api/health` → `ok`, manifiesto 1.3.11; servidor y
  consola reportan 1.3.11.
- **Prueba en vivo**: estación temporal 15 (desactivada al terminar) con el cliente 1.3.11; la
  ventana de bloqueo no contiene reloj ni fecha, descargó la galería y el fondo se ve a través del
  velo (brillo medio 44,6/255 y desviación 33,3 en la captura). PC 01 recibirá 1.3.11 con el
  actualizador; su sesión prepago #2 (id 4) sigue activa.
- **Pendiente**: el **agente** de PC 01 sigue en 1.3.9; los puertos 445, 5985 y 3389 del equipo no
  están accesibles, así que la actualización debe hacerse en el propio equipo. El paquete final del
  agente se detalla en la sección 1.3.12.

---

### Tasa del día retirada del bloqueo (1.3.12, 2026-11-18, sin publicar)

- **Sin «Tasa del día» en la pantalla de bloqueo**: se quitó del pie el bloque con la etiqueta
  «Tasa del día:» y el valor `1 USD = … Bs` (`RateText`); el pie conserva el texto de bienvenida
  centrado y la versión a la derecha. La tasa sigue disponible en la consola, la caja y el resumen
  de «Mi cuenta».
- **Versión 1.3.12** en `version.props`; publicación x64 regenerada y paquete
  `ClientUpdate\client.zip` 1.3.12 (67.669.421 bytes, SHA-256 `1ed7102b…`,
  `publishedUtc` 2026-11-18T11:59:07.193Z).
- **Despliegue local**: respaldo `_bin-pre-1.3.12-20261118-0803`; servidor y consola copiados a
  `C:\CyberManager\Servidor` (sin tocar `cybermanager.db*`, `appsettings.json` ni `web.config`);
  tarea `CyberManagerServer` en `Running`, `/api/health` → `ok`, manifiesto 1.3.12; servidor 1.3.12.
- **Prueba en vivo**: estación temporal 16 (desactivada al terminar) con el cliente 1.3.12; la
  ventana de bloqueo muestra negocio, equipo, «EQUIPO BLOQUEADO», el mensaje, el botón de inicio de
  sesión, «¡Gracias por su visita!» y «Versión 1.3.12», sin reloj, sin fecha y sin tasa.
- **Agente para PC 01**: el paquete final se detalla en la sección 1.3.13.

---

### Más transparencia y fondos más vistosos (1.3.13, 2026-11-18, sin publicar)

- **Velo casi transparente**: el degradado del bloqueo pasó a `#00`/`#1F`/`#66` (0 % arriba,
  12 % al centro y 40 % abajo). La tarjeta del pie bajó a 35 % de opacidad y el recuadro del icono
  a 80 %.
- **Texto legible sin oscurecer la imagen**: se añadió una sombra negra (`DropShadowEffect`,
  desenfoque 12, opacidad 0,85) al nombre del negocio, equipo/zona, título, mensaje, saludo y
  versión, de modo que el fondo se aprecia vivo aunque la imagen sea clara.
- **Versión 1.3.13** en `version.props`; publicación x64 regenerada y paquete
  `ClientUpdate\client.zip` 1.3.13 (67.669.722 bytes, SHA-256 `bfd72bea…`,
  `publishedUtc` 2026-11-18T12:18:10.578Z).
- **Despliegue local**: respaldo `_bin-pre-1.3.13-20261118-0810`; servidor y consola copiados a
  `C:\CyberManager\Servidor` (sin tocar `cybermanager.db*`, `appsettings.json` ni `web.config`);
  tarea `CyberManagerServer` en `Running`, `/api/health` → `ok`, manifiesto 1.3.13; servidor 1.3.13.
- **Prueba en vivo**: estación temporal 17 (desactivada al terminar) con el cliente 1.3.13; la
  captura pasó de brillo medio 44,6 y desviación 33,3 (1.3.11) a **62,9 y 48,0**, señal de que el
  fondo se ve con más luz y contraste. El cliente arrancó sin errores de XAML.
- **Agente para PC 01**: el paquete final se detalla en la sección 1.3.14.

---

### Contorno en los textos del bloqueo (1.3.14, 2026-11-18, sin publicar)

- **Texto con contorno**: WPF no dibuja borde a un `TextBlock`, así que se añadió
  `OutlinedTextBlock`, un control que construye la geometría de los glifos y pinta primero el trazo
  y luego el relleno. Se aplicó al nombre del negocio, al equipo y la zona, al título, al mensaje,
  al saludo y a la versión, con trazo negro `#D9000000` de 3 px (2–2,5 px en los textos pequeños).
  El texto se lee sobre fondos claros sin volver a oscurecer la imagen; se retiró la sombra
  `DropShadowEffect` que se había usado en 1.3.13.
- **Versión 1.3.14** en `version.props`; publicación x64 regenerada y paquete
  `ClientUpdate\client.zip` 1.3.14 (67.673.044 bytes, SHA-256 `d9425bfc…`,
  `publishedUtc` 2026-11-18T12:22:21.766Z).
- **Despliegue local**: respaldo `_bin-pre-1.3.14-20261118-0825`; servidor y consola copiados a
  `C:\CyberManager\Servidor` (sin tocar `cybermanager.db*`, `appsettings.json` ni `web.config`);
  tarea `CyberManagerServer` en `Running`, `/api/health` → `ok`, manifiesto 1.3.14; servidor 1.3.14.
- **Prueba en vivo**: estación temporal 18 (desactivada al terminar) con el cliente 1.3.14; en la
  captura, el **100 %** de los píxeles del título y del texto de la versión tienen píxeles oscuros a
  3 px o menos, es decir, el contorno se dibuja. Brillo medio 63,2 y desviación 48,1; sin reloj, sin
  fecha y sin tasa. PC 01 ya pasó sola a 1.3.13 y su sesión prepago #4 sigue activa; recibirá 1.3.14
  en la siguiente comprobación.
- **Agente para PC 01**: el paquete final se detalla en la sección 1.3.15.

---

### Fuente más grande en el bloqueo (1.3.15, 2026-11-18, sin publicar)

- **Textos más grandes y legibles**: nombre del negocio 19 → **24**, equipo 17 → **22**, zona 12 → **15**,
  título 22 → **30**, mensaje 15 → **18**, saludo y versión 12 → **15**. El contorno subió a 3,5–4 px
  en los textos principales y 2,5 px en los pequeños, y el mensaje amplió su ancho de 560 a 620 px.
  El pie conserva el mismo alto porque el borde tiene relleno holgado.
- **Versión 1.3.15** en `version.props`; publicación x64 regenerada y paquete
  `ClientUpdate\client.zip` 1.3.15 (67.673.083 bytes, SHA-256 `1a30323c…`,
  `publishedUtc` 2026-11-18T12:27:48.887Z).
- **Despliegue local**: respaldo `_bin-pre-1.3.15-20261118-0831`; servidor y consola copiados a
  `C:\CyberManager\Servidor` (sin tocar `cybermanager.db*`, `appsettings.json` ni `web.config`);
  tarea `CyberManagerServer` en `Running`, `/api/health` → `ok`, manifiesto 1.3.15; servidor 1.3.15.
- **Prueba en vivo**: estación temporal 19 (desactivada al terminar) con el cliente 1.3.15. Las cajas
  de texto medidas por UI Automation crecieron: título 281×40 → **383×55**, equipo 176×32 → **226×41**,
  versión 94×22 → **118×28**. El contorno se mantiene: 96,8 % de los píxeles del título y 100 % de los
  del equipo y la versión tienen trazo oscuro a 3 px o menos. Brillo medio 62,5 y desviación 48,1; sin
  reloj, sin fecha y sin tasa. PC 01 seguía en 1.3.13 al cerrar la prueba y recibirá 1.3.15 en su
  siguiente comprobación.
- **Agente para PC 01**: el paquete final se detalla en la sección 1.3.16.

---

### Mensaje y saludo en blanco (1.3.16, 2026-11-18, sin publicar)

- **Color de fuente**: el mensaje «Solicite al cajero o inicie sesión para empezar.» y el saludo
  «¡Gracias por su visita!» pasaron de gris (`MutedBrush`, `#8FA4C4`) a **blanco** (`#FFFFFF`), con el
  mismo contorno oscuro. El resto de los textos conserva su color (equipo y título en acento, zona y
  versión en gris).
- **Versión 1.3.16** en `version.props`; publicación x64 regenerada y paquete
  `ClientUpdate\client.zip` 1.3.16 (67.673.034 bytes, SHA-256 `b1f4a3f9…`,
  `publishedUtc` 2026-11-18T12:32:11.768Z).
- **Despliegue local**: respaldo `_bin-pre-1.3.16-20261118-0833`; servidor y consola copiados a
  `C:\CyberManager\Servidor` (sin tocar `cybermanager.db*`, `appsettings.json` ni `web.config`);
  tarea `CyberManagerServer` en `Running`, `/api/health` → `ok`, manifiesto 1.3.16; servidor 1.3.16.
- **Prueba en vivo**: estación temporal 20 (desactivada al terminar) con el cliente 1.3.16; el mensaje
  tiene 967 píxeles blancos y el saludo 221, todos con contorno oscuro a 3 px o menos. La versión sigue
  en gris. Sin errores en el log del cliente ni en el del servidor.
- **Agente para PC 01**: el paquete final se detalla en la sección 1.3.18.

---

### Contorno fino con resplandor oscuro (1.3.18, 2026-11-18, sin publicar)

- **Contorno más fino**: el trazo del bloqueo bajó a 1,2–1,8 px (antes 2,5–4 px), de modo que el
  borde ya no engrosa las letras.
- **Resplandor hacia afuera**: `OutlinedTextBlock` incorpora `Glow`, `GlowRadius` y `GlowOpacity`.
  El halo se dibuja con 16 pasadas de trazo concéntricas, cada una más fina y opaca hacia el glifo,
  lo que produce un degradado suave hacia afuera en vez de un borde duro. Radios: 9 px en el título,
  7 px en negocio/equipo, 6 px en el mensaje y 5 px en zona, saludo y versión, con negro al 90 %.
  El control reserva ese espacio en la medida para que el halo no se recorte.
- **Versión 1.3.18** en `version.props`; publicación x64 regenerada y paquete
  `ClientUpdate\client.zip` 1.3.18 (67.673.889 bytes, SHA-256 `ee20b6d3…`,
  `publishedUtc` 2026-11-18T12:45:54.427Z). La 1.3.17 fue un ajuste intermedio con el halo demasiado
  sólido; se reemplazó por esta versión más suave.
- **Despliegue local**: respaldo `_bin-pre-1.3.18-20261118-0852`; servidor y consola copiados a
  `C:\CyberManager\Servidor` (sin tocar `cybermanager.db*`, `appsettings.json` ni `web.config`);
  tarea `CyberManagerServer` en `Running`, `/api/health` → `ok`, manifiesto 1.3.18; servidor 1.3.18.
- **Prueba en vivo**: estación temporal 22 (desactivada al terminar) con el cliente 1.3.18. El perfil
  de luminancia desde el borde del título sube de 28 (2 px) a 49 (3 px), 54 (4 px) y ~65 (12 px),
  frente al fondo de ~65–70: el halo se desvanece de forma gradual. El mensaje muestra la misma
  progresión (42 → 48 → 70). Sin errores en el cliente ni en el servidor.
- **Agente para PC 01**: el paquete final se detalla en la sección 1.3.19.

---

### Login de la consola sin servidor, caja ni datos fiscales (1.3.19, 2026-11-18, sin publicar)

- **Formulario más simple**: se quitaron del login de la consola los campos **Servidor** y
  **Caja de este equipo**, y los recuadros informativos **MONEDA BASE** (USD $ / Bs) e **IMPUESTOS**
  (IVA 16 %). Quedan solo **Usuario**, **Contraseña** y el botón **Entrar a la consola**.
- **Configuración automática**: la URL y el nombre de la caja se leen de
  `%LOCALAPPDATA%\CyberManager\Admin\config.json` (`ServerUrl` y `TerminalName`). Si no existen, se
  usan `http://127.0.0.1:5080` y el nombre del equipo; el nombre viaja al servidor en la cabecera
  `X-Cash-Terminal`. Al iniciar sesión se normalizan y se guardan de nuevo en ese archivo.
- **Versión 1.3.19** en `version.props`; publicación x64 regenerada y paquete
  `ClientUpdate\client.zip` 1.3.19 (67.673.867 bytes, SHA-256 `4a552dfa…`,
  `publishedUtc` 2026-11-18T14:12:27.385Z).
- **Despliegue local**: respaldo `_bin-pre-1.3.19-20261118-0856`; servidor y consola copiados a
  `C:\CyberManager\Servidor` (sin tocar `cybermanager.db*`, `appsettings.json` ni `web.config`);
  tarea `CyberManagerServer` en `Running`, `/api/health` → `ok`, manifiesto 1.3.19; servidor 1.3.19.
- **Prueba en vivo**: se abrió la consola 1.3.19 y UI Automation confirmó que ya no aparecen
  «Servidor», «Caja de este equipo», «MONEDA BASE» ni «IMPUESTOS»; solo hay dos campos editables
  (`UsernameBox` y `PasswordBox`). Se inició sesión con `admin/admin123` y abrió la ventana
  «CyberManager · Administración · v1.3.19» usando la configuración guardada. La consola de prueba se
  cerró al terminar.
- **Agente para PC 01**: `C:\CyberManager\CyberManager-Agente-1.3.19-x64.zip` (35.666.363 bytes,
  SHA-256 `013a9da3…`, sin `appsettings.json`). Copiar sobre `C:\CyberManager\Estacion\Agent` y
  reiniciar el servicio `CyberManagerAgent` para que reciba el arreglo de SignalR.

### Descubrimiento automático del servidor y desbloqueo sin conexión (1.3.20, 2026-11-18, sin publicar)

- **Causa del corte**: el servidor cambió de `192.168.1.10` a `192.168.1.8` y cada cliente seguía
  con la URL vieja en `%LOCALAPPDATA%\CyberManager\Client\config.json`, así que nunca volvía a
  conectar. Ahora se corrige solo.
- **El servidor se anuncia** (`ServerDiscoveryService`, UDP 5094): al recibir
  `CYBERMANAGER-FIND-SERVER-1` responde `CYBERMANAGER-SERVER-1` con su URL actual, nombre del
  equipo y versión. Se responde una vez por cada IPv4 local utilizable (la de la ruta al solicitante,
  las que tienen puerta de enlace y el resto) para que un adaptador virtual (Hyper-V, WSL, VPN) no
  tape la IP real. `/api/health` ahora incluye `app` y `version`.
- **Buscador compartido** (`CyberManager.Shared.ServerLocator`): prueba la URL guardada, el propio
  equipo, la difusión UDP y, como último recurso, recorre la subred con `/api/health`. El cliente, la
  consola y el agente lo usan.
- **Cliente**: si `/api/station/state` falla, busca el servidor (un intento cada 30 s), valida el
  candidato con el token de la estación (`/api/station/state`) y cambia la URL en caliente —
  reconstruye `StationApi` y SignalR sin cerrar el bloqueo ni la barra— guardando `ServerUrl`.
- **Consola**: si el login falla por red (nunca ante un 401), busca el servidor, reintenta con
  `admin` y guarda la URL encontrada en `%LOCALAPPDATA%\CyberManager\Admin\config.json`. El primer
  intento usa 8 s de tiempo máximo para que el descubrimiento empiece pronto.
- **Agente**: ante un fallo de conexión busca el servidor y actualiza `appsettings.json`; el
  trabajador reconecta solo.
- **Desbloqueo sin conexión** (`AdminCredentialCache`): cada login de administrador correcto en el
  cliente guarda un hash PBKDF2-SHA256 (150.000 iteraciones, sal aleatoria) cifrado con DPAPI en
  `%LOCALAPPDATA%\CyberManager\Client\admin-offline.dat`. Si el servidor no responde, la contraseña
  de administrador abre el equipo con esa copia; un 401 del servidor nunca cae en la caché. Tras 5
  fallos hay 5 minutos de espera.
- **Firewall**: `configurar-servidor.ps1` abre UDP 5094 además de TCP 5080, y las casillas de los
  instaladores lo mencionan.
- **Bloqueo**: el logo pasó del encabezado a la barra inferior izquierda junto al nombre del
  software; el nombre del equipo quedó en blanco y a 28 px.
- **Versión 1.3.20** en `version.props`; publicación x64 regenerada y paquete
  `ClientUpdate\client.zip` 1.3.20 (67.582.145 bytes, SHA-256 `de1ae1f0…`,
  `publishedUtc` 2026-11-18T14:47:31.884Z).
- **Despliegue local**: respaldos `_bin-pre-1.3.20-20261118-1037` y `_admin-pre-1.3.20-20261118-1040`;
  servidor y consola copiados a `C:\CyberManager\Servidor` (sin tocar `cybermanager.db*`,
  `appsettings.json` ni `web.config`); tarea `CyberManagerServer` en `Running`,
  `/api/health` → `{"status":"ok","app":"CyberManager","version":"1.3.20"}`; regla de firewall
  UDP 5094 creada; manifiesto 1.3.20.
- **Prueba en vivo**: una difusión a `255.255.255.255:5094` devolvió `192.168.1.8` y `172.30.64.1`
  (adaptador virtual) con versión 1.3.20, y `ServerLocator` los listó como candidatos. La consola se
  abrió con `ServerUrl` apuntando a una IP falsa (`192.168.1.99`) y, tras descubrir el servidor,
  inició sesión en 14 s y abrió «CyberManager · Administración · v1.3.20», corrigiendo el archivo de
  configuración. La consola de prueba se cerró al terminar.
- **Prueba de la caché local**: con un proyecto de prueba aislado se comprobó `NotAvailable` antes
  de sembrar, `Ok` con `admin/admin123`, `Invalid` con contraseña o usuario malos, `LockedOut` en el
  quinto fallo y al intentar de nuevo con la contraseña correcta, y 5 minutos de espera. El archivo
  `admin-offline.dat` quedó cifrado (sin «admin» en claro) y se borró al terminar la prueba.
- **Detalle corregido**: cuando vencía el bloqueo de 5 minutos, el siguiente fallo volvía a
  responder `LockedOut`; ahora se limpia el contador y la fecha al vencer.
- **Agente para PC 01**: `C:\CyberManager\CyberManager-Agente-1.3.20-x64.zip` (35.627.231 bytes,
  SHA-256 `4e4df824…`, sin `appsettings.json`). Copiar sobre `C:\CyberManager\Estacion\Agent` y
  reiniciar el servicio `CyberManagerAgent`: así ese equipo también podrá encontrar el servidor si
  vuelve a cambiar de IP.
- **Pendiente**: PC 01 (estación 11) sigue en 1.3.18 y sin latido; el agente debe actualizarse a
  mano porque la PC no está accesible por red.

### IP del servidor visible y diagnóstico de conexión (1.3.21, 2026-11-18)

- **Motivo**: el usuario informó que el servidor seguía sin conectar con los clientes. Revisión en
  vivo: `/api/health` correcto, `0.0.0.0:5080` y UDP `5094` escuchando, firewall con ambas reglas
  activas y `http://192.168.1.8:5080` respondiendo desde las dos IPv4 de este equipo. No hay ninguna
  petición de descubrimiento de estaciones en los registros.
- **Estado real del sistema**: solo hay una estación activa (PC 01, id 11, versión 1.3.18) y su
  último latido es del 18/11 a las 12:54 UTC; el resto de equipos de la lista están desactivados y
  las 22 estaciones temporales de prueba se dieron de baja. Un barrido de `192.168.1.1-254` encontró
  varios equipos encendidos, pero **ningún otro responde en el puerto 5080**. Por tanto, desde el
  servidor no hay clientes con los que conectar: hay que arrancar/actualizar el cliente en esos PCs.
- **IP visible**: `/api/health` ahora devuelve `ipAddress`, `serverUrl` y `urls` con las IPv4 del
  servidor (se priorizan las que tienen puerta de enlace, para no anunciar Hyper-V/WSL). La consola
  muestra una tarjeta **«IP del servidor» con botón «Copiar URL»** en *Equipos y zonas* y en la
  pestaña *Zonas*, con la dirección obtenida del propio servidor; si no responde, usa la URL guardada
  y la marca como «servidor sin conexión».
- **Diagnóstico futuro**: `ServerDiscoveryService` registra una petición por IP solicitante como
  máximo cada 5 minutos (`Petición de descubrimiento recibida desde …`). Así se puede comprobar en
  `Server\Logs\server-*.log` si un cliente está intentando encontrar el servidor.
- **Versión 1.3.21** en `version.props`; publicación x64 regenerada y paquete
  `ClientUpdate\client.zip` 1.3.21 (67.582.118 bytes, SHA-256 `a768be1f…`,
  `publishedUtc` 2026-11-18T15:04:05.438Z). El código del cliente no cambió: solo servidor y consola.
- **Despliegue local**: servidor y consola copiados a `C:\CyberManager\Servidor` (sin tocar
  `cybermanager.db*`, `appsettings.json` ni `web.config`); `/api/health` →
  `{"status":"ok","app":"CyberManager","version":"1.3.21","ipAddress":"192.168.1.8","serverUrl":"http://192.168.1.8:5080","urls":["http://192.168.1.8:5080"]}`.
- **Prueba de UI**: con UI Automation se entró a la consola 1.3.21 y se verificó el texto
  `IP del servidor` / `192.168.1.8  (puerto 5080)` / `Copiar URL` tanto en *Equipos y zonas* como en
  *Productos y tarifas → Zonas*. Las ventanas de prueba se cerraron al terminar.
- **Pendiente**: PC 01 (estación 11) sigue en 1.3.18 y sin latido; los demás PCs deben tener el
  cliente instalado y ejecutándose. El paquete de agente 1.3.20 sigue siendo válido para PC 01.

### Configuración manual de la IP del servidor desde la estación (1.3.22, 2026-11-18)

- **Motivo**: el usuario pidió poder configurar manualmente la IP de conexión al servidor desde el
  menú de administrador del cliente, para los casos en que el descubrimiento automático no baste.
- **Interfaz**: `ClientSettingsWindow` (XAML y code-behind) incluye ahora la sección **Conexión con
  el servidor** con `ServerUrlBox`, `DiscoverButton` (**Buscar en la red**), `TestButton`
  (**Probar conexión**) y el botón **Guardar dirección**. La dirección se normaliza con
  `ServerLocator.Normalize` y se comprueba con `ServerLocator.ProbeAsync` (`/api/health`) antes de
  guardarse. La ayuda recuerda que no se debe escribir `localhost` desde una estación.
- **Modo sin conexión**: el login offline sigue sin abrir los ajustes remotos, pero después de
  desbloquear el equipo pregunta si se desea indicar la IP; si se acepta, abre la misma ventana en
  modo reducido (sólo la dirección, sin ajustes remotos ni desinstalación) mediante
  `new ClientSettingsWindow(null)`. También se puede corregir la IP con el menú de administrador
  normal cuando los ajustes remotos no cargan.
- **Aplicación en caliente**: `App.ApplyServerAddressAsync` reutiliza `SwitchServerAsync`: guarda
  `Config.ServerUrl` de forma atómica, reconstruye `StationApi` y SignalR y refresca el estado sin
  cerrar la pantalla de bloqueo ni la barra de sesión. Si la dirección no responde, se muestra el
  error y no se guarda nada.
- **Prueba aislada**: con un arnés WPF que abre `ClientSettingsWindow(null)` se comprobó por UI
  Automation que aparecen **Conexión con el servidor**, **IP o URL del servidor**, **Buscar en la
  red**, **Probar conexión** y **Guardar dirección**, y que **Datos del negocio** y **Mantenimiento**
  quedan ocultos. Al escribir `192.168.1.8`, **Probar conexión** mostró «Conexión correcta con
  http://192.168.1.8:5080/» y **Guardar dirección** cerró la ventana y dejó
  `{"ServerUrl":"http://192.168.1.8:5080/",...}` en la configuración. **Buscar en la red** completó
  el campo con una dirección que responde. El archivo de configuración de prueba se borró.
- **Detalle corregido**: la ventana ya no asigna `DialogResult` al cerrarse, porque ese valor sólo
  es válido cuando se abre con `ShowDialog()`. Ningún llamador lo utilizaba.
- **Versión 1.3.22** en `version.props`; paquete `ClientUpdate\client.zip` regenerado con el
  cliente nuevo (67.583.851 bytes, SHA-256 `8bb3936d…`, `publishedUtc`
  2026-11-18T15:44:59.091Z, sin `appsettings.json`). Servidor y consola se publican con la misma
  versión para mantener la coherencia.
- **Despliegue local**: copiados servidor y consola a `C:\CyberManager\Servidor` con respaldos
  `_server-pre-1.3.22-20261118-1127` y `_admin-pre-1.3.22-20261118-1127`; base, `appsettings.json`
  y `web.config` intactos. `/api/health` → versión 1.3.22 e IP `192.168.1.8`. Manifiesto y zip del
  cliente verificados por tamaño y SHA-256.
- **Agente 1.3.22**: `C:\CyberManager\CyberManager-Agente-1.3.22-x64.zip` (35.627.237 bytes,
  SHA-256 `5382ac3b…`, sin `appsettings.json`) para actualizar PC 01 cuando vuelva a estar
  accesible.
- **Publicación**: a petición del usuario se recompiló el cliente en Release (0 errores y 0
  advertencias) y se hicieron push de los commits locales a `origin/main`
  (`b960a85..7d1a01a`, once commits: 1.3.10 a 1.3.22). También se envió la notificación de
  actualización a la estación 11 (`{"sent":true,"stationId":11,"version":"1.3.22"}`); el paquete
  sigue esperando a que PC 01 vuelva a conectarse.
- **Instaladores unificados 1.3.22**: compilados con
  `.\scripts\build-installer.ps1 -Version 1.3.22 -Architecture all -SkipVncDownload -NoBump`
  (x64 + x86, TightVNC ya descargado y verificado):
  - `artifacts\CyberManager-Servidor-1.3.22.exe`, 378,0 MB, SHA-256 `acb437f5…`.
  - `artifacts\CyberManager-Estacion-1.3.22.exe`, 143,9 MB, SHA-256 `59a8fe7c…`.
  - `artifacts\CyberManager-Unificado-1.3.22.exe`, 509,1 MB, SHA-256 `7289b2b7…`.
  Los ejecutables quedan en `artifacts\`, excluidos por `.gitignore` (además, superan el límite
  de 100 MB por archivo de GitHub), así que **no se suben al repositorio**; el "update del git" se
  refiere al código y a este registro. El paquete `client.zip` regenerado por esta compilación se
  copió al servidor y `/api/client-update/manifest` ya sirve el SHA-256 `8bb3936d…`.
- **Release de GitHub**: a petición del usuario se publicó
  https://github.com/Kis4r4gi/CyberManager/releases/tag/v1.3.22 con **sólo dos assets**:
  `CyberManager-Servidor-1.3.22.exe` (396.364.885 bytes, SHA-256 `acb437f5…`) y
  `CyberManager-Estacion-1.3.22.exe` (150.860.390 bytes, SHA-256 `59a8fe7c…`). El instalador
  *Unificado* quedó fuera de la release, como se pidió.
- **Pendiente**: instalar/actualizar el cliente 1.3.22 en los PCs y comprobar desde la estación que
  el campo guarda la IP correcta. PC 01 sigue sin ser accesible por red.

### Localización automática, IP local en el cliente y borrado definitivo (1.3.23–1.3.25, 2026-11-18)

- **Diagnóstico**: el cliente de `192.168.1.9` sí encontraba el servidor (logs UDP 5094 cada 5
  minutos), pero el escaneo inverso del servidor (`UDP 5092/5093`) devolvía cero equipos: el firewall
  del cliente bloquea las respuestas entrantes. Además, un escaneo anterior había creado el duplicado
  «Equipo 01» (id 23, IP `.9`, sin MAC) mientras que el PC real (id 11, MAC
  `14:AB:C5:D4:30:F5`) quedó inactivo con la IP vieja `.10`.
- **IP local del cliente**: `ClientSettingsWindow` muestra ahora «IP de este equipo: …» debajo de
  «IP o URL del servidor», dentro de **Conexión con el servidor**, visible también sin conexión.
  Usa `NetworkIdentity.LocalIPv4Addresses(onlyWithGateway: true)` con respaldo a todas las IPv4 y
  traducciones nuevas en `Localization.cs` (`L.T`). El menú administrador sigue sin campos
  superpuestos.
- **Anuncio de identidad al buscar servidor** (`ServerLocator` + `DiscoveryIdentity`): el paquete
  `CYBERMANAGER-FIND-SERVER-1` ahora incluye rol, StationId, token, nombre de máquina, IPv4, MAC y
  versión del cliente/agente. `ServerDiscoveryService`, al responder por UDP 5094, registra o
  actualiza la estación con esa identidad. Así el servidor la ubica **sin depender del firewall
  entrante del cliente** y le reenvía el token correcto cuando hace falta.
- **Descubrimiento automático del servidor**: `StationAutoDiscoveryWorker` escanea cada minuto
  (arranca 20 s después de iniciar) si `ScannerAutoAdd` está activo. `StationDiscoveryService`
  centraliza el emparejamiento por **MAC → ID → nombre → IP**, deduplica el lote, evita reactivar
  bajas sin MAC y corrige tokens desactualizados. El escaneo manual de la consola usa la misma
  lógica. Se conservan la sesión, la zona, la tarifa y el token de la fila original.
- **Eliminación permanente**: `DELETE /api/stations/{id}` ya no solo desactiva. Borra sesiones,
  ventas, artículos de venta, reservas, registros de agente y auditoría de la estación, y guarda su
  MAC/IP en `DeletedStationIdentities` para que el descubrimiento automático no vuelva a dar de alta
  un PC eliminado. El alta manual desde **Escanear la red** sí puede reincorporarlo y limpia la
  exclusión. La tabla nueva se crea en bases antiguas desde `DbSeeder.EnsureCompatibilityAsync`.
- **Pruebas**: compilación de la solución con 0 errores/0 advertencias. Con un anuncio UDP simulado
  (`AA:BB:CC:DD:EE:FF`) se comprobó el alta automática (id 24), que `DELETE` devuelve 204 y borra la
  fila, y que un segundo anuncio **no** la recrea (queda la exclusión en
  `DeletedStationIdentities`). Se borró además el duplicado real id 23 sin sesiones activas; queda
  la estación id 11 como único registro del PC 01. HttpClient también registró peticiones de
  descubrimiento desde `192.168.1.9`.
- **Versiones y despliegue**: 1.3.23 añadió la IP local y el borrado permanente; 1.3.24 incorporó el
  anuncio de identidad; 1.3.25 agregó la tabla de exclusiones. Se recompilaron los tres instaladores
  unificados para cada versión y se desplegó el servidor 1.3.25 en `C:\CyberManager\Servidor\Server`
  con respaldos `_server-pre-1.3.23-*`, `_server-pre-1.3.24-*` y `_server-pre-1.3.25-*`. Base,
  `appsettings.json`, `web.config`, `Logs` y `ClientUpdate` se conservaron; el manifiesto sirve
  `client.zip` 1.3.25 con SHA-256 verificado y se envió la notificación a la estación 11.
  `/api/health` devuelve `version: 1.3.25` e IP `192.168.1.8`.
- **Pendiente**: cuando el cliente de PC 01 aplique el paquete 1.3.25, su anuncio UDP traerá la MAC y el
  servidor debe emparejarlo con el id 11 y ponerlo en línea. La consola de este PC todavía ejecuta la
  compilación anterior; basta reiniciarla para ver el texto nuevo de confirmación de borrado.

### Consola maximizada, mantenimiento que abre el terminal y estados instantáneos (1.3.26–1.3.31, 2026-11-18)

- **Escaneo ARP en español** (1.3.26): `NetworkScanService.ScanArp` acepta tanto `dynamic` como
  `dinámico`, por lo que `/api/stations/scan` volvió a ver el PC 01 (`192.168.1.9`,
  `14:AB:C5:D4:30:F5`) en la tabla ARP del servidor. El escaneo manual devolvió 10 equipos.
- **Altas automáticas sin duplicados** (1.3.28): los equipos vistos solo por ARP viajan con rol `arp`
  y `StationDiscoveryService` permite con ellos actualizar una fila conocida por MAC, pero nunca dar
  de alta una nueva. El worker automático dejó de crear «Equipo» y los logs muestran
  «visto en ARP; se omite el alta automática».
- **Consola y mantenimiento** (1.3.27): `MainWindow` abre siempre maximizada, también al cambiar de
  usuario. Las acciones **Mantenimiento** del tablero y de **Equipos** envían la orden
  `AgentCommandType.OpenClient` después de cambiar el estado; el cliente abre la pantalla de acceso
  de administrador en la propia estación para poder manejarla.
- **Botones del administrador del cliente** (1.3.27): se sustituyó el único botón «Cerrar y desinstalar»
  por **Minimizar**, **Cerrar** y **Desinstalar** independientes, visibles también sin conexión.
  Minimizar deja el cliente en segundo plano; Cerrar pide confirmación y finaliza la aplicación;
  Desinstalar lanza el desinstalador. `ExitRequested` se maneja igual que `UninstallRequested` en
  `App.xaml.cs`.
- **Estado al cerrar el cliente** (1.3.29–1.3.31): el latido del agente ya no puede devolver un equipo
  a «Disponible»; ese estado lo decide el cliente. `StationHub` ahora ofrece `ClientClosing`, que
  marca «Sin conexión» de inmediato y avisa a la consola por SignalR. Si el proceso muere sin avisar,
  `OnDisconnectedAsync` hace lo mismo tras 2 s. El cliente invoca `ClientClosing` desde `OnExit`
  antes de cerrar SignalR. La llamada se espera de forma síncrona (máximo 2 s) porque WPF apaga el
  dispatcher en cuanto `OnExit` devuelve el control y un `await` podría no llegar a ejecutarse.
- **Pruebas y despliegue**: compilación con 0 errores/0 advertencias en cada versión; instaladores
  unificados regenerados para 1.3.26–1.3.31. Servidor y consola desplegados con respaldos
  `_server-pre-1.3.*` y `_admin-pre-1.3.*`. `/api/health` sirve `version: 1.3.31`, IP `192.168.1.8`;
  `manifest.json` y `client.zip` coinciden en tamaño (`67.588.141`) y SHA-256
  (`11a55bbe…`). Se envió la notificación de actualización a la estación 11. Con estaciones de prueba
  creadas por API se verificó que un latido pone el equipo «Disponible», que `ClientClosing` lo deja
  «Sin conexión» de inmediato y que una desconexión sin aviso lo hace en unos 2 s. Las estaciones de
  prueba se eliminaron con `DELETE` (204).

### Eliminación del instalador unificado y release 1.3.31 (2026-11-18)

- Se eliminó `installer/CyberManager.Unificado.iss` y la entrada correspondiente de
  `scripts/build-installer.ps1`. El script vuelve a generar solo **Servidor** y **Estación**, ambos
  con los payloads x64/x86. La opción de elegir ambos componentes en un solo ejecutable ya no está
  disponible: en ese caso se ejecutan los dos instaladores.
- Se quitó del repositorio el artefacto `artifacts/CyberManager-Unificado-1.3.31.exe` (estaba en
  Git LFS) y se actualizaron `README.md` y este documento para que dejen de anunciar tres
  instaladores.
- La release de GitHub **v1.3.31** se publicó/actualizó con **solo dos assets**:
  `CyberManager-Servidor-1.3.31.exe` y `CyberManager-Estacion-1.3.31.exe`.

---

## Rutas y comandos clave

Repositorio: la carpeta que contiene `CyberManager.sln` (por ejemplo, `C:\CyberManager`).

```powershell
# Compilar
$dotnet = "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe"
& $dotnet build CyberManager.sln -v q --nologo

# Generar los dos instaladores (servidor y estación; sube la versión menor)
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1

# Fijar una versión concreta o conservar la actual
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1 -Version 1.2.0
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1 -NoBump

# Recompilar los instaladores con dist\x64 y dist\x86 existentes (sin subir versión)
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1 -SkipPublish

# Descargar y verificar los MSI de TightVNC (build-installer lo hace automáticamente)
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\download-tightvnc.ps1
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\download-tightvnc.ps1 -IncludeSource

# Reutilizar los MSI de TightVNC ya descargados
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1 -SkipVncDownload

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
- **Cajas por terminal**: la cabecera `X-Cash-Terminal` (nombre de la PC) identifica el turno y se
  guarda en `CashSession.Terminal`; sin cabecera se usa `PRINCIPAL`. Varias consolas comparten un
  solo servidor y cada una abre/cierra su propio turno; nunca se comparte el archivo SQLite por red.
- La impresión automática solo ocurre con `AutoPrintTicket` y `PrinterName` configurados; el
  botón manual abre `PrintDialog` si no hay impresora.
- Los precios son con IVA incluido; `Sale.Total` es el total final y `Sale.TotalVes` usa la tasa
  de la venta. Solo `Cash`, `CashUsd`, `Card` y `Transfer` generan movimientos de caja.
- Respaldos: claves `BackupAutoEnabled` (true), `BackupIntervalHours` (24), `BackupRetention`
  (30) y `BackupFolder` (vacío = `respaldos` junto a la base) en `Settings`; el trabajador revisa
  cada 5 minutos. La carpeta se puede cambiar desde *Configuración → Respaldos* y debe ser una ruta
  absoluta escribible. Los respaldos anteriores no se mueven al cambiar de carpeta.
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
- Repositorio git en `main`: <https://github.com/Kis4r4gi/CyberManager.git>. `dist/`,
  `artifacts/`, `installer/vnc/`, `bin/` y `obj/` están ignorados.
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
- **Instalador unificado**: AppId propio `{E7F1C3A9-5B24-4D68-9F13-8C6A2D4B7E95}` y
  `DefaultDirName=C:\CyberManager`; los payloads van a `{app}\Servidor` y `{app}\Estacion` para
  conservar las rutas, los scripts y las tareas existentes. `[Types]`
  (`servidor`/`estacion`/`completo`) preselecciona `[Components]`, y las entradas de `[Tasks]`,
  `[Files]`, `[Icons]`, `[Run]`, `[UninstallRun]`, `[InstallDelete]` y `[UninstallDelete]` llevan
  `Components:` para no mezclar roles. Antes de copiar archivos desinstala en silencio los AppId de
  los instaladores separados, conservando la base de datos y la configuración local.
- **Versión**: `version.props` es la única fuente; no edite `Version` en los `.csproj`. El script
  de empaquetado sube la versión menor y reinicia el patch al publicar. Los instaladores usan el
  mismo valor en
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
- **Descubrimiento**: el servidor usa UDP **5092** (cliente) y **5093** (agente) con mensajes JSON
  (`magic` de petición, respuesta y configuración). El escaneo difunde a la red y barre cada /24;
  espera unos segundos y agrupa por MAC/IP. La autenticación de estación acepta `X-Station-Token`
  o, como compatibilidad, IP/MAC; el token es la credencial principal.
- **Descubrimiento del servidor (1.3.20)**: el servidor escucha además UDP **5094**
  (`CYBERMANAGER-FIND-SERVER-1` → `CYBERMANAGER-SERVER-1`) y anuncia todas sus IPv4. El cliente, la
  consola y el agente usan `CyberManager.Shared.ServerLocator` para encontrar la dirección nueva,
  validarla con el token del equipo y guardarla. La regla de firewall UDP 5094 se crea en
  `configurar-servidor.ps1` y en los instaladores.
- **Actualización de clientes**: el paquete se genera en `dist\<arch>\Server\ClientUpdate`
  (`client.zip` + `manifest.json` con versión y SHA-256) y el instalador del servidor copia la
  carpeta. El agente (SYSTEM) y el cliente comparan con `VersionUtil`; para reemplazar archivos en
  uso se renombran a `.old` y se deja `update.pending` para que la estación se reinicie.
- **Versiones**: `CyberManager.Shared.VersionUtil` centraliza la comparación numérica usada por el
  servidor, el agente y el cliente.

### Render del video y diagnóstico automático (1.3.39–1.3.42, 2026-11-19)

Se investigó por qué el video promocional se veía lento/a tirones en la estación 11 pese a estar
codificado a 30 fps. El diagnóstico se hizo sin acceso remoto al equipo: el cliente reporta ahora
en `AgentHeartbeat.Hardware` el nivel de render de WPF (`RenderCapability.Tier`), la GPU, el DPI,
la resolución y los Hz del monitor, y `StationHub.Heartbeat` lo escribe en el log del servidor solo
cuando cambia (sin ruido por latido). `LockWindow` mide además la posición del medio frente al
reloj y los fps de composición de WPF mientras hay video.

Datos obtenidos de la estación 11: **tier=2** (aceleración por hardware), Intel HD 520 + Radeon
R7 M360, 1366 × 768 a **60 Hz**, Windows 10; el video avanza a tiempo real (`x≈1.0`). Con un
archivo de prueba **60 fps real** WPF siguió componiendo a **30 fps**, lo que descarta el códec, la
GPU y el archivo: el tope es del reproductor `MediaElement`. El archivo de prueba se eliminó y se
restauró el video activo original (id 5).

Se aplicó también `BitmapCache` al velo y a los textos con contorno/resplandor de la pantalla
bloqueada, que se rasterizaban en cada fotograma. El usuario confirmó que la imagen mejoró y se
ve aceptable, por lo que se **conserva el motor actual** (no se sustituye por WebView2 ni LibVLC).

Despliegue verificado: servidor y consola en 1.3.42 con respaldos `_*-pre-1.3.42-*`, estación 11 en
1.3.42, `/api/client-update/status` → `upToDate=1`, `outdated=0`. Sin commit/push ni release.

### Controles multimedia en la consola (1.3.43, 2026-11-19)

El usuario pidió botones para manejar el contenido multimedia de los videos desde el servidor.
Se confirmó con él el significado de cada control antes de implementarlos:

- **Abrir carpeta en terminales**: nueva orden `AgentCommandType.OpenPromoCacheFolder = 19`,
  enrutada solo a la estación (como `PlayRandomWallpaper`) en `StationEndpoints.SendCommand` y
  `AdminHub.SendCommand`. El cliente abre con `Process.Start` su carpeta
  `%LOCALAPPDATA%\CyberManager\Client\promos`, expuesta por `PromoVideoService.CacheDirectory`.
  La consola **no envía rutas**, de modo que una orden malintencionada no puede abrir una carpeta
  arbitraria. El agente no interviene porque corre como servicio y su sesión no es visible.
- **Casillas Activo por tarjeta**: `PromosView.BuildCard` añade un `CheckBox` que llama a
  `PUT /api/promo-videos/{id}` con el estado nuevo; `ToggleActive` revierte la casilla si falla la
  API, usa `_suppressToggle` para no repetir la llamada y marca `MouseLeftButtonUp` como manejado
  para no abrir la vista previa. El panel lateral y el contador de la galería se refrescan solos.
- **Actualizar archivos en clientes**: se renombró el botón «Reenviar a terminales» para que
  coincida con el lenguaje del usuario. El usuario eligió expresamente que **solo reenvíe la
  playlist** (`POST /api/promo-videos/refresh`); no se añadió descarga forzada. El mensaje lo dice
  explícitamente para evitar confusión con una reinstalación de archivos.
- **Requisitos de codificación**: botón que abre `PromoRequirementsDialog`, un `FormDialog` oscuro
  con `PromoVideoValidator.Requirements` traducido (incluye el comando `ffmpeg` recomendado).

Se añadieron las claves nuevas en `Localization.cs` (botones, mensaje de reenvío, casilla y texto de
requisitos). Compilación con **0 errores / 0 advertencias**. Artefactos `dist\x64` 1.3.43 con
`-Version 1.3.43 -Architecture x64 -SkipInstaller -SkipVncDownload`.

Despliegue verificado: servidor, consola y paquete `ClientUpdate` en 1.3.43 con respaldos
`_*-pre-1.3.43-20261119-184754`; `/api/health` → `1.3.43`; aviso `POST /api/client-update/notify?stationId=11`
enviado y estación 11 actualizada a 1.3.43; `/api/client-update/status` → `upToDate=1`, `outdated=0`.
Consola reabierta (PID 11112). Sin commit/push ni release.

### Menú único y barra compartida multimedia (1.3.44, 2026-11-19)

El usuario pidió unificar el menú de fondos y videos con **todas las opciones compartidas del
servidor** y menús desplegables visualmente funcionales. Se añadió `Kit.MenuButton` (un `Button`
con `ContextMenu` que se abre al hacer clic) y se reescribió `MediaView` como centro de mando:

- **Agregar**: subir imágenes, buscar en internet, sincronizar carpeta de fondos, subir videos.
- **Enviar a terminales**: reproducir fondo aleatorio, actualizar archivos en clientes, abrir
  carpeta de videos en terminales.
- **Configuración**: guardar y aplicar cambios, recargar configuración.
- **Herramientas**: requisitos de codificación, abrir carpeta de fondos (consola), actualizar vista.
- Barra **CONFIGURACIÓN DEL SERVIDOR** siempre visible con segundos, orden, transición y botón
  guardar; el estado explica que los equipos aplican los cambios al consultar el estado.
- `WallpapersView` quedó como galería/edición contextual: se eliminaron su panel de configuración,
  sus botones de cabecera y los métodos migrados a `MediaView`; sus acciones se expusieron como
  `internal` para que el menú superior las invoque. `PromosView` ya tenía sus acciones expuestas.
- `MediaView.RefreshAsync` carga la configuración y recarga la pestaña activa; al cambiar de
  pestaña se refresca la galería correspondiente.

Se añadieron las claves nuevas en `Localization.cs`. Compilación con **0 errores / 0 advertencias**.
Artefactos `dist\x64` 1.3.44 con `-Version 1.3.44 -Architecture x64 -SkipInstaller -SkipVncDownload`
(`client.zip` 67.598.012 bytes, SHA-256 `f19412bb071c09d3dbe7dfadb8f679d1d6481cebec9f1eda1221d582d4540bcd`).

Despliegue verificado: servidor, consola y paquete `ClientUpdate` en 1.3.44 con respaldos
`_*-pre-1.3.44-20261119-202027`; `/api/health` → `1.3.44`; `/api/client-update/status` →
`packageVersion=1.3.44`, `outdated=1` (estación 11 offline en 1.3.43). Sin commit/push ni release.

### Menús desplegables oscuros (1.3.45, 2026-11-19)

El usuario informó que los desplegables de **Fondos y videos** se veían como cuadros blancos.
`Dark.xaml` no definía estilos para `ContextMenu` ni `MenuItem`, así que WPF aplicaba el tema claro
del sistema. Se añadieron plantillas propias: `ContextMenu` con superficie `SurfaceAltBrush`, borde
`BorderBrush`, esquinas redondeadas y sombra; `MenuItem` con fondo transparente, resaltado
`SelectionBrush`/`RowHoverBrush` y texto `TextBrush`. Esto corrige también los menús contextuales
ya existentes de sesión, productos y equipos.

Compilación con **0 errores / 0 advertencias**. Artefactos y despliegue 1.3.45 con respaldos
`_*-pre-1.3.45-20261119-231645`. `/api/health` → `1.3.45`. Verificación objetiva de la interfaz:
el fondo muestreado de un `MenuItem` del menú «Agregar» es `RGB(22,35,58)` = `#16233A`
(`SurfaceAltBrush`), ya no blanco. Sin commit/push ni release.

### Nombre del terminal al doble de tamaño (1.3.46, 2026-11-19)

El usuario indicó que el número/nombre del terminal mostrado arriba a la derecha de la pantalla
bloqueada debía ser el doble de grande. En `LockWindow.xaml`, `StationText` (`PC-01`, alineado a la
derecha en el encabezado) pasó de `FontSize="28"` a `FontSize="56"`. La zona y el nombre del negocio
se mantuvieron igual. Se descartó y revirtió un cambio previo en el encabezado de la consola al
aclarar el usuario cuál era el elemento.

Compilación con **0 errores / 0 advertencias**. Artefactos y despliegue 1.3.46; como las carpetas
ya existían, se conservó la base de datos y se usaron como punto de restauración los respaldos
`_*-pre-1.3.45-20261119-231645` (el único backup nuevo fue `_admin-pre-1.3.46-20261119-232245`).
Durante el despliegue se detectó y corrigió una copia anidada `Server\Server` que había dejado el
servidor en 1.3.45. `/api/health` y `/api/client-update/status` → `1.3.46`; consola reabierta con
`CyberManager 1.3.46`. La estación 11 sigue offline en 1.3.43 y tomará el paquete al reconectarse.
Sin commit/push ni release.

### Nombre del terminal en el centro de la pantalla bloqueada (1.3.47, 2026-11-19)

El usuario pidió colocar el mismo nombre del terminal en el centro, entre el candado y el texto
«EQUIPO BLOQUEADO», y usar una fuente más estilizada acorde al proyecto. En `LockWindow.xaml` se
añadió `CenterStationText` (`FontSize="40"`) en ese punto, con `FontFamily="Bahnschrift SemiBold,
Segoe UI"` (la misma tipografía aplicada al nombre superior). En `LockWindow.xaml.cs` se sincronizó
con `StationText` tanto sin conexión como con estado, y se incluyó en `ApplyRenderCache()`.

Compilación con **0 errores / 0 advertencias**. Artefactos y despliegue 1.3.47 con respaldos
`_*-pre-1.3.47-20261119-232736`. `/api/health` y `/api/client-update/status` → `1.3.47`; consola
reabierta con `CyberManager 1.3.47`. Verificado que `client.zip` contiene `CenterStationText` y
`Bahnschrift` en `CyberManager.Client.dll`. La estación 11 sigue offline en 1.3.43 y tomará el
paquete al reconectarse. Sin commit/push ni release.

### Nombre del terminal: solo en el centro y más grande (1.3.48, 2026-11-19)

El usuario pidió eliminar el nombre que aparecía arriba a la derecha (`StationText`, `PC-01`) y
agrandar el nombre central (`CenterStationText`). Se quitó el `OutlinedTextBlock` superior derecho
(la zona se conserva), se limpiaron sus asignaciones y su entrada en `ApplyRenderCache()` en
`LockWindow.xaml.cs`, y el texto central pasó de `FontSize="40"` a `FontSize="64"` manteniendo
`Bahnschrift SemiBold`. Ahora el nombre del terminal aparece una sola vez, bajo el candado.

Compilación con **0 errores / 0 advertencias**. Artefactos y despliegue 1.3.48 con respaldos
`_*-pre-1.3.48-20261119-233542`. `/api/health` y `/api/client-update/status` → `1.3.48`; consola
reabierta con `CyberManager 1.3.48`. Verificado en `client.zip`: presente `CenterStationText` y
`Bahnschrift`, ausente `StationText` en `LockWindow`. La estación 11 sigue offline en 1.3.43 y
tomará el paquete al reconectarse. Sin commit/push ni release.

### Comprobación de conexión servidor ↔ estación (2026-11-19)

El usuario pidió verificar la conexión. Resultado: **correcta y operativa**.

- Servidor `1.3.48` saludable en `192.168.1.10:5080`, tarea programada `CyberManagerServer` en
  ejecución, con dos conexiones TCP `ESTABLISHED` desde `192.168.1.4` al puerto 5080 (SignalR).
- Estación 11 «Estacion 01»: `Available`, alive, MAC ARP `14-AB-C5-D4-30-F5` en estado
  `Reachable`, diagnóstico recibido vía `StationHub` (Intel HD 520 / R7 M360, 1366×768, 60 Hz).
- El `ping` ICMP a `192.168.1.4` falla por el firewall del equipo, pero no afecta a SignalR ni al
  descubrimiento UDP; el servidor sigue recibiendo heartbeats cada 5 s.
- Se envió `POST /api/client-update/notify?stationId=11` para forzar el aviso pendiente. La estación
  descargó y aplicó `client.zip` en menos de un minuto: versión reportada `1.3.48` y
  `/api/client-update/status` → `upToDate=1`, `outdated=0`. La ruta de actualización quedó
  verificada de extremo a extremo. Sin commit/push ni release.

### Eliminación total del IGTF (1.3.49, 2026-11-20)

El usuario pidió eliminar el IGTF de todo el proyecto. Se quitó el cálculo del 3 % en
`SaleService.CreateAsync` (ahora `Total = Subtotal - Descuento` sin recargo) y la estimación en el
POS; se eliminaron `IgtfAmount`/`IgtfAmountVes` de la entidad `Sale`, `IgtfAmount` de `SaleDto`,
`Igtf`/`IgtfVes` de `InvoiceDto` y `Igtf` de `DailyClosingDto`. Desaparecieron las claves
`IgtfEnabled`/`IgtfRate`, los controles de configuración, las filas en ticket/recibo/cierre, los
correos y CSV, las etiquetas «Efectivo USD (IGTF)», las entradas de localización y toda la
documentación. `SaleService` ya no inyecta `SettingsService` porque no la usaba para otra cosa.

`DbSeeder.EnsureCompatibilityAsync` elimina las columnas heredadas `Sales.IgtfAmount` y
`Sales.IgtfAmountVes` y las filas de `Settings` en instalaciones existentes; verificado en una copia
de la base con SQLite 3.50: `DROP COLUMN` funciona, la integridad queda `ok` y se conservan las
ventas. Las ventas históricas mantienen su total original (con el impuesto que se cobró entonces).

Compilación con **0 errores / 0 advertencias**. Artefactos y despliegue 1.3.49 con respaldos
`_*-pre-1.3.49-20261120-000014`. `/api/health` y `/api/client-update/status` → `1.3.49`; consola
reabierta con `CyberManager 1.3.49` y la estación 11 actualizada a 1.3.49 (`upToDate=1`). Verificado
en la base real: ya no existen las columnas `IgtfAmount`/`IgtfAmountVes` ni las claves de ajustes,
sin errores en el log. Tampoco aparece la cadena `IGTF` en los DLL publicados ni en `client.zip`.
Sin commit/push ni release.

### Control de equipos reorganizado y solucionador con IA (1.3.50 / 1.3.51, 2026-11-20)

- Se desplegó **1.3.50** con la columna compacta «Sel.» (casilla `SmallCheckBox` de 14×14 en
  `Dark.xaml`), selección preservada por `Id` entre refrescos, menús desplegables por acción
  («Encender», «Apagar», «Reiniciar» y «Selección») y opciones en el menú «Acciones» de cada fila
  para apagar o reiniciar solo ese equipo. Los botones dejaron de decir «todos»: el alcance se elige
  dentro del menú («Equipos marcados» o «Toda la lista»). Respaldos
  `_*-pre-1.3.50-20261120-024905`; servidor, consola y estación 11 en 1.3.50, `upToDate=1`.
- El usuario informó que el apagado de la estación 01 no surtió efecto. El endpoint
  `POST /api/stations/11/command` con `Shutdown` enruta la orden **solo al hub del agente**
  (`RealtimeService.SendAgentCommandAsync`), mientras que el cliente recibe las órdenes de monitor,
  audio y bloqueo. No hay líneas `Agente conectado: equipo 11` en el registro, y el actualizador de
  cliente solo reemplaza la carpeta `Client`, no `Agent`: la causa probable es que el servicio
  `CyberManagerAgent` no está conectado o quedó en una versión anterior. RPC/WinRM desde este equipo
  hacia `192.168.1.4` están bloqueados, así que el paquete del agente debe copiarse a mano.
- Se añadió un solucionador con reglas + explicación de **IA en la nube** (proveedor compatible con
  OpenAI): DTO `Troubleshoot`, servicio `TroubleshootService`, endpoint Admin
  `POST /api/troubleshoot/diagnose`, vista **Sistema → Solucionador** y configuración
  `Troubleshoot:Ai` con clave en `CYBERMANAGER_AI_API_KEY`. Los registros se sanitizan antes de
  enviarlos (tokens, correos, RIF, IPs y números largos). Sin clave sigue funcionando solo con reglas.
- `AgentHub` ahora mantiene un contador de conexiones, registra conexión/desconexión y permite al
  solucionador marcar como crítico que un equipo no tenga agente conectado, con la causa y el paso
  concreto para corregirlo. Además, `POST /api/stations/{id}/command` devuelve 409 con un mensaje
  claro cuando se pide apagar, reiniciar o cerrar sesión y el agente no está conectado, en lugar de
  responder «orden enviada» sin efecto.
- **Despliegue y verificación 1.3.51**: compilación final con 0 errores/0 advertencias; respaldos
  `_*-pre-1.3.51-20261120-031450`; `/api/health` → `1.3.51 ok`, manifiesto `1.3.51` con SHA-256
  coincidente en el `client.zip` instalado, sección `Troubleshoot:Ai` agregada sin sobrescribir
  `appsettings.json`, tarea `CyberManagerServer` en `Running`. Se notificó a la estación 11: versión
  `1.3.51`, `Available`, `upToDate=1`, `outdated=0`. Consola abierta como
  `CyberManager · Administración · v1.3.51`; la vista **Sistema → Solucionador** se abre, ejecuta
  reglas automáticamente y reporta como crítico «1 equipos sin agente conectado → Estacion 01», con
  la instrucción de actualizar el agente. Sin clave de IA muestra «IA no disponible: No hay API key
  configurada» y mantiene los hallazgos locales. Paquete `C:\CyberManager\CyberManager-Agente-1.3.51-x64.zip`
  listo para instalar manualmente en la estación.

### Reparación remota del agente y reinicio verificado (1.3.53, 2026-11-20)

El servicio `CyberManagerAgent` de la Estación 01 no tenía conexión SignalR. Como RPC/WinRM hacia
`192.168.1.4` están bloqueados (error 1722 y `TrustedHosts`), se aprovechó que el cliente se ejecuta
elevado (`requireAdministrator`) y ya descarga actualizaciones del servidor:

- `ClientUpdateManifestDto` ahora incluye `agentVersion`, `agentFileName`, `agentSize` y
  `agentSha256`. `ClientUpdateService` completa esos campos aunque el manifiesto provenga de una
  versión anterior, y expone `GET /api/client-update/agent-download` (anónimo, igual que
  `/download`, porque solo entrega binarios).
- `scripts\build-installer.ps1` genera `agent.zip` junto a `client.zip` y escribe ambos hashes en
  el manifiesto. `scripts\build-installer.ps1 -Version 1.3.53 -SkipVncDownload -Clean` terminó con
  0 errores/0 advertencias y produjo los instaladores de servidor (441,8 MB) y estación (143,9 MB).
- `AgentInstaller` (en `CyberManager.Shared`) descarga y verifica `agent.zip`, detiene/elimina el
  servicio anterior, copia `..\Agent`, conserva/reescribe `appsettings.json` con `ServerUrl`,
  `StationId` y `Token` del `config.json` del cliente, registra `CyberManagerAgent` con `sc.exe`,
  configura reinicio automático ante fallos y lo inicia. Usa un marcador `agent.installed` con la
  versión y el hash para no reiniciarlo en cada comprobación. En 1.3.53 se corrigió la lectura de
  ese marcador (antes comparaba solo la primera línea con el hash, así que reinstalaría el servicio
  cada 10 minutos).
- `App.xaml.cs` llama a `EnsureAgentInstalledAsync` antes de aplicar la versión nueva, también
  cuando la versión del cliente ya coincide; por eso la estación 1.3.51 instaló el agente apenas
  arrancó con el cliente 1.3.52, y 1.3.53 desplegó el marcador corregido.

**Despliegue**: se publicaron primero 1.3.52 (respaldos `_*-pre-1.3.52-20261120-091051`) y luego
1.3.53 (respaldos `_*-pre-1.3.53-20261120-092620`); en ambos se copiaron servidor, consola,
`client.zip`, `agent.zip` y manifiesto, conservando `appsettings.json` con su sección
`Troubleshoot:Ai`. Paquete manual final:
`C:\CyberManager\CyberManager-Agente-1.3.53-x64.zip`.

**Verificación en vivo**: `/api/health` → `1.3.53 ok`; hashes de cliente y agente coincidentes con
el manifiesto; `GET /api/client-update/agent-download` → 200 y 35.644.406 bytes; aviso enviado a la
estación 11; registro `Agente conectado: equipo 11` a las 09:27:08; versión `1.3.53`,
`upToDate=1`/`outdated=0` y solucionador sin incidencias. Ya con 1.3.53 se envió la orden
`Restart` (enum 3): respuesta `{"sent":true}`, `Agente desconectado: equipo 11` a las 09:28:20 y
`Agente conectado: equipo 11` a las 09:29:08; la estación volvió a `Available` y el diagnóstico
final quedó sin hallazgos (la advertencia transitoria «sin latido» desapareció al arrancar el
cliente). La orden ahora sí se entrega al servicio de Windows.

### Ticket de venta compacto y editable (1.3.54, 2026-11-25)

Las líneas del carrito del POS eran `Border` con estilo `Panel`, margen inferior de 8 px y dos
filas por producto; eso hacía muy larga la revisión antes de emitir la factura. En
`src\\CyberManager.Admin\\Views\\PosView.cs` se sustituyeron por filas `Grid` sin recuadro, con una
sola línea `cantidad × producto` a la izquierda, el total a la derecha y acciones compactas.

- Cada línea tiene **Editar** y **✕**. `EditCartLineDialog` permite cambiar el producto y la
  cantidad; valida stock, impide duplicados ya presentes en el ticket y conserva el producto actual
  aunque esté agotado. `PosLine.Product` pasó a tener `set` para aplicar el cambio.
- El encabezado del ticket tiene **Cancelar ticket** (antes «Vaciar», con confirmación) y el botón
  principal pasó de «Cobrar» a **Cerrar ticket · monto**, sin alterar el registro de la venta.
- Se redujeron márgenes e interlineados de cabecera, totales, pago y botón; la columna del ticket
  bajó de 392 a 360 px para dar espacio al catálogo. Compilación: 0 errores, 0 advertencias.

**Despliegue**: `scripts\build-installer.ps1 -Version 1.3.54 -SkipVncDownload -Clean` generó el
servidor (441,8 MB) y la estación (143,9 MB). Respaldos
`_*-pre-1.3.54-20261125-205034`; servidor y consola copiados, `appsettings.json` conservado.

**Verificación**: la IP del equipo servidor cambió a `192.168.1.11` (ya no responde `192.168.1.10`),
así que las pruebas HTTP se hicieron por `127.0.0.1`. `/api/health` → `1.3.54 ok`; manifiesto
1.3.54 con hashes de cliente y agente correctos; `GET /api/client-update/agent-download` → 200 y
35.644.403 bytes. Se envió el aviso a la estación 11, pero sigue **Offline** desde el 23/11 y WoL
no la despertó: permanecerá en 1.3.53 hasta que encienda y aplique el paquete 1.3.54. La consola
1.3.54 se abrió y, vía Automatización de UI, se comprobó que la vista *Punto de venta* carga y
muestra «Ticket de venta», «Cancelar ticket» y «Cerrar ticket».

### Explicación con IA gratuita (2026-11-25)

El solucionador seguía sin clave: OpenAI, Groq y OpenRouter exigen cuenta y token, que no se pueden
crear desde aquí. Se eligió **Pollinations Classic** (`https://text.pollinations.ai/v1`, modelo
`openai-fast` / GPT-OSS 20B): habla `/v1/chat/completions`, no pide registro y acepta la petición
aunque se envíe un `Authorization: Bearer` marcador. La petición de prueba devolvió HTTP 200 con una
respuesta OpenAI-compatible.

- `Troubleshoot:Ai:BaseUrl` y `Model` actualizados tanto en `src\CyberManager.Server\appsettings.json`
  como en `C:\CyberManager\Servidor\Server\appsettings.json` (el servidor en producción se editó aparte
  porque la instalación conserva su propia configuración).
- Variable de máquina `CYBERMANAGER_AI_API_KEY=pollinations-free` (el proveedor anónimo ignora el
  valor; el servidor solo exige que no esté vacía). Como `CyberManagerServer` corre como `SYSTEM`, se
  reinició la tarea programada para que tomase la variable y el nuevo `appsettings.json`.
- `/api/health` → `1.3.54 ok`; `POST /api/troubleshoot/diagnose` con `useAi=true` → `aiConfigured=true`,
  `aiUsed=true`, sin error. Límites del tier anónimo: ~100 respuestas/día y una petición cada 45 s; al
  agotarse se muestran solo las reglas. Inicialmente era solo configuración; la nueva `BaseUrl`/`Model`
  y el prompt corregido quedaron incluidos en la release 1.3.56.

### Buscador de tickets y prompt de IA para Windows (1.3.56, 2026-11-25)

El solucionador indicaba `systemctl` y `/var/log` aunque el agente es un servicio de Windows. Se
amplió el prompt en `TroubleshootService.BuildPrompt` para declarar el entorno (tarea
`CyberManagerServer`, consola WPF y servicio `CyberManagerAgent`) y exigir comandos de Windows
(`Get-Service`, `sc.exe query`, Visor de eventos, `Test-NetConnection -Port 5080`), prohibiendo
explícitamente comandos de Linux.

Para localizar comprobantes ya emitidos se agregó la vista **Buscar tickets**:

- `GET /api/sales` acepta `number`, `customer`, `product`, `status` y `take` (tope 1000) además de
  `from`/`to` y `stationId`. `SaleService.ListAsync` aplica búsqueda parcial por número, nombre/RIF
  del cliente y descripción de productos, con un único `ToDto` compartido con el detalle.
- `SaleDto` expone ahora `CustomerTaxId` para mostrar el RIF en la lista.
- `CyberApi.SearchSalesAsync` construye la query con escape; la consola nueva `TicketsView` filtra
  por número, fechas, cliente/RIF, producto y estado, y ofrece **Ver** (reutiliza
  `SaleReceiptDialog`) e **Imprimir** (reutiliza `TicketPrinter` y la configuración de impresora).
- Botón *Buscar tickets* en Operación, disponible también para cajeros; rango inicial de un año y
  búsqueda desde 2000 si se deja «Desde» vacío. Compilación: 0 errores, 0 advertencias.

---

## Pendientes / siguientes pasos

1. Probar **Escanear la red** y el box **Clientes** desde la consola local 1.3.0 con una estación
   real encendida.
2. (Opcional) Con una sesión **elevada**, probar los instaladores separados 1.3.0 en modo silencioso
   y verificar: selección automática de x64 en este equipo, archivos instalados, tarea programada,
   firewall UDP 5092/5093, servicio del agente, reinstalación cruzada x64/x86, confirmación al
   cerrar procesos, reinicio posterior y desinstalación.
3. (Pendiente) Probar el **instalador unificado** en un equipo limpio (sin instalación previa):
   elegir *Servidor*, luego *Estación* y confirmar que cada rol recibe solo sus tareas e iconos y
   que la configuración local del cliente se conserva. En este equipo ya se validaron la migración
   desde los instaladores separados, ambos roles, la desinstalación y la reinstalación del servidor
   (2026-11-17).
4. (Opcional) Probar la ruta x86 en un Windows de 32 bits o en una máquina virtual; el mecanismo
   equivalente se validó con un instalador Inno de prueba en x64.
5. (Opcional) Con una sesión elevada, probar la elevación de primer inicio (UAC), el marcador
   `%ProgramData%\CyberManager\server-elevated.marker` y los comandos remotos de volumen, monitor y
   bloqueo de entrada contra una estación real.
6. Probar dos consolas reales en la red local (servidor + CAJA-1/CAJA-2 con usuarios *Cajero*):
   apertura y cierre simultáneos, cobros en la caja de cada sesión y visibilidad de las vistas.
7. Mantener actualizado `README.md` si se agregan funciones nuevas.
8. Recompilar los instaladores cuando cambie el código para incluir la última versión de la
   consola, el servidor, las promos y la localización. La versión menor sube automáticamente; use
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
- Release actual: **v1.3.54**
  (<https://github.com/Kis4r4gi/CyberManager/releases/tag/v1.3.54>) con
  `CyberManager-Servidor-1.3.54.exe`, `CyberManager-Estacion-1.3.54.exe` y
  `CyberManager-Agente-1.3.54-x64.zip`. El instalador unificado quedó fuera del proyecto y de la
  release. La release anterior publicada es `v1.3.53`
  (<https://github.com/Kis4r4gi/CyberManager/releases/tag/v1.3.53>).
- La sincronización automática se registra con
  `pwsh -File .\scripts\sync-github.ps1 -InstallTask` (tarea programada `CyberManagerGitSync`,
  cada 15 minutos con la sesión del usuario).
