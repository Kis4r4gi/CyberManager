# CyberManager

Sistema de gestión para cyber cafés y centros de videojuegos en Venezuela. Servidor central en
red local, consola de administración/caja para el negocio, cliente bloqueado para cada PC o
consola y agente de Windows para controlar la energía y el bloqueo de los equipos.

Todo el dinero contable se lleva en **USD**, con una **tasa Bs/USD del día** que se guarda como
foto en cada venta, y facturación con **RIF, IVA 16 % e IGTF 3 %** sobre pagos en divisas.

Incluye **dos instaladores unificados para Windows x64 y x86**: uno para el servidor (servidor +
consola) y otro para cada estación (cliente + agente), sin necesidad de instalar .NET en los
equipos de destino. Cada instalador detecta automáticamente la arquitectura del equipo e instala
los binarios correspondientes. La interfaz está disponible **solo en español e inglés** y los
terminales bloqueados pueden reproducir **videos promocionales** sincronizados desde el servidor.

---

## Descarga

Los instaladores listos para usar se publican en
[**Releases**](https://github.com/Kis4r4gi/CyberManager/releases/latest). Cada versión incluye los
dos paquetes para Windows 10/11 (x64 y x86 en un mismo instalador):

| Paquete | Archivo | Contenido |
| --- | --- | --- |
| Servidor | `CyberManager-Servidor-<versión>.exe` | Servidor, consola de administración y lanzador |
| Estación | `CyberManager-Estacion-<versión>.exe` | Cliente bloqueado, agente y control remoto |

Ambos instaladores requieren permisos de administrador, detienen las aplicaciones en ejecución
antes de actualizar y crean su propia entrada de desinstalación. También puedes compilar el
proyecto desde el código fuente siguiendo la sección [Compilación](#compilación).

---

## Contenido

- [Descarga](#descarga)
- [Características](#características)
- [Arquitectura](#arquitectura)
- [Requisitos](#requisitos)
- [Estructura del repositorio](#estructura-del-repositorio)
- [Compilación](#compilación)
- [Puesta en marcha rápida](#puesta-en-marcha-rápida)
- [Instaladores de Windows](#instaladores-de-windows)
- [Avisos de nuevas versiones](#avisos-de-nuevas-versiones)
- [Credenciales de demostración](#credenciales-de-demostración)
- [Cambiar de usuario y cerrar la consola](#cambiar-de-usuario-y-cerrar-la-consola)
- [Flujo operativo diario](#flujo-operativo-diario)
- [Moneda, tasa, IVA e IGTF](#moneda-tasa-iva-e-igtf)
- [Tickets, impresión y lector de códigos](#tickets-impresión-y-lector-de-códigos)
- [Consolas de videojuegos](#consolas-de-videojuegos)
- [Pagos a proveedores y cierre diario](#pagos-a-proveedores-y-cierre-diario)
- [Apariencia y personalización](#apariencia-y-personalización)
- [Idioma de la interfaz](#idioma-de-la-interfaz)
- [Respaldos de la base de datos](#respaldos-de-la-base-de-datos)
- [Configurar un equipo (PC)](#configurar-un-equipo-pc)
- [Instalar el agente de Windows](#instalar-el-agente-de-windows)
- [Inicio automático del cliente](#inicio-automático-del-cliente)
- [Galería de fondos de pantalla](#galería-de-fondos-de-pantalla)
- [Videos promocionales](#videos-promocionales)
- [Control remoto, Wake on LAN y NetFiltroWeb](#control-remoto-wake-on-lan-y-netfiltroweb)
- [Respaldo de tiempos del cliente](#respaldo-de-tiempos-del-cliente)
- [Scripts incluidos](#scripts-incluidos)
- [Sincronización automática con GitHub](#sincronización-automática-con-github)
- [Firewall y red](#firewall-y-red)
- [API y hubs en tiempo real](#api-y-hubs-en-tiempo-real)
- [Base de datos](#base-de-datos)
- [Seguridad recomendada](#seguridad-recomendada)
- [Solución de problemas](#solución-de-problemas)

---

## Características

**Operación del cyber**

- **Tablero con los terminales al principio**: las estaciones aparecen en una cuadrícula de
  tarjetas rectangulares antes de los indicadores resumidos. Cada tarjeta muestra estado
  (disponible, en uso, en pausa, mantenimiento, fuera de línea), tiempo transcurrido, tiempo
  restante e importe acumulado, con textos ajustados para no superponerse. Siempre hay un bloque
  de tiempo: la sesión activa, `00:00` si está libre o `—` si está fuera de servicio.
- **Tablero personalizable**: búsqueda por nombre, zona, tipo o usuario; orden por manual,
  nombre, estado, zona, tipo, tiempo o importe; orden ascendente o descendente; filtro por zona;
  filtro «Solo en uso»; tamaño de las tarjetas (80 a 160 %) y visibilidad de métricas y leyenda.
  Todas las preferencias se guardan por equipo y sobreviven al cambio de tema o idioma.
- Equipos de tipo **computadora, consola de videojuegos u otro**, con plataforma y cantidad de
  mandos visibles en el tablero y en la ficha del equipo.
- **Escaneo de la red local**: desde *Equipos* el servidor descubre las estaciones instaladas
  (cliente y agente por UDP), muestra su IP, MAC y versión, y tras confirmar las registra y les
  envía el ID y el token automáticamente.
- **Actualización automática de los clientes**: el servidor publica el paquete del cliente junto
  a su instalación; las estaciones lo descargan, verifican el SHA-256 y se reinician con la
  versión nueva, sin reinstalar.
- Sesiones **prepago** y **postpago**, con tarifas por hora, bloques de cobro y tiempo mínimo.
- Pausar, reanudar, **extender sobre la marcha** (sin detener la sesión), transferir de equipo y
  finalizar sesiones.
- Paquetes de tiempo con minutos de bono, seleccionables al iniciar la sesión; el precio del
  paquete se cobra por adelantado y los minutos incluyen el bono.
- **Autoservicio prepago en la pantalla de bloqueo**: el cliente puede crear una cuenta, comprar
  paquetes de tiempo con su saldo y abrir o cerrar la sesión del equipo sin pasar por caja. Los
  minutos no usados vuelven al monedero al finalizar.
- **Acceso de administrador desde el cliente**: el botón de inicio de sesión de la pantalla de
  bloqueo permite entrar con una cuenta con rol *Admin* y editar la configuración del cliente
  enlazada al servidor (negocio, barra de sesión, fondos, avisos y sesiones).
- **Login cómodo en la pantalla de bloqueo**: el cursor permanece en el campo de usuario mientras
  se escribe, el menú flotante se cierra con **ESC** y los formularios de acceso y registro
  incluyen la casilla **Mostrar contraseña**.
- **Reloj de 12 horas**: la pantalla de bloqueo muestra la hora con formato `h:mm:ss tt` usando
  la hora local del equipo (Windows), no la del servidor.
- **Acciones por equipo en «Equipos»**: cada fila incluye un menú desplegable **Acciones** con
  subir, bajar, editar, cambiar nombre, editar conexión, configuración/token, ver pantalla,
  bloquear, enviar mensaje, actualizar cliente, mantenimiento, activar/desactivar y eliminar. La
  barra de botones inferiores se retiró; el clic derecho abre el mismo menú.
- **Estado en línea confiable**: la presencia se calcula por el último latido (90 s) y no por la
  ocupación del equipo, así que un PC apagado se muestra **Sin conexión** aunque conserve una
  sesión abierta. La lista de *Equipos* y el tablero se refrescan solos al recibir cambios y cada
  pocos segundos.
- Consumo de productos cargado a la sesión (se cobra al cerrar) o cobrado directo en caja.
- Reservas de equipos por fecha y hora, con confirmación y cancelación.
- Mensajes al cliente y órdenes de bloqueo, apagado, reinicio y cierre de sesión.
- **Control remoto ampliado del terminal**: ajuste de volumen maestro, silenciar o reactivar el
  audio, apagar o encender el monitor, bloquear teclado y mouse (solo se liberan con
  «Desbloquear entrada») y cerrar un programa por nombre de proceso.
- **Control remoto de escritorio**: desde «Ver pantalla remota» active **Control remoto** para
  enviar el mouse y el teclado de la consola al equipo, incluidas las teclas Ctrl, Shift y Alt.
  **Abrir VNC** lanza el visor de TightVNC (se detecta automáticamente) o el visor configurado.
  El instalador de estación puede instalar TightVNC opcionalmente, con contraseña, puerto y
  Firewall limitado a la red local.
- **Barra de sesión configurable**: el cliente muestra el tiempo y el monto a cancelar en una
  ventana flotante; el administrador define el texto descriptivo y el tamaño (70–160 %) desde
  *Configuración → Control remoto y filtro*.
- **Encendido remoto (Wake on LAN)**: con la MAC registrada en el equipo, la consola envía el
  paquete mágico desde la ficha del equipo o el tablero cuando está fuera de línea.
- **Respaldo local de tiempos**: el cliente guarda el estado de la sesión de forma atómica y, si
  se corta la energía o el servidor no responde, restaura el tiempo transcurrido al reiniciar.
- **NetFiltroWeb**: bloqueo de sitios y palabras prohibidas con una lista inicial editable por el
  administrador; se aplica en cada equipo mediante el archivo `hosts` y un proxy local opcional.
- Aviso y bloqueo automático al agotarse el tiempo contratado (configurable).
- **Equipo siempre despierto y reloj local**: mientras el cliente está en ejecución impide la
  suspensión, la hibernación y el apagado automático de la pantalla, y la hora de la pantalla de
  bloqueo se toma del reloj de Windows del propio equipo.
- Galería de fondos sincronizada: los equipos bloqueados muestran un fondo fijo o una
  presentación rotativa controlada desde la consola; la rotación avanza solo tras 2 minutos sin
  actividad, como un salvapantallas.
- **Videos promocionales** en los terminales bloqueados, con reproducción silenciada, orden
  configurable y sincronización inmediata desde el servidor.
- **Notas y Registro en vivo**: el panel derecho muestra arriba las **notas** compartidas (con
  autor y hora; se registran al publicar y al borrar) y abajo el **registro** de actividad de los
  terminales (conexión/desconexión, inicio y cierre de sesión, cambios de estado, comandos,
  mantenimiento, avisos y tiempo agotado). El registro se puede exportar a un archivo de texto con
  el botón **Abrir**, que lo muestra en el Bloc de notas.
- **Visualizador de pantalla remoto**: desde el tablero o desde *Equipos* puede abrir la pantalla
  de una estación y verla en vivo (actualización automática cada 2 segundos o captura manual). La
  captura la realiza el cliente WPF en la sesión del usuario y viaja por SignalR al servidor.

**Caja y ventas**

- Apertura y cierre de caja con fondo, arqueo, esperado, contado y diferencia.
- Ventas de productos, tiempo y consumos de sesión.
- Formas de pago: efectivo en bolívares, efectivo en dólares, tarjeta, transferencia,
  **Pago Móvil**, saldo del cliente, mixto y pendiente (cargado a la sesión).
- Descuentos, devoluciones y gastos de caja.
- **Tickets de 80 mm** para ventas y **cierre diario configurable** (ticket 80 mm, A4 o Carta),
  con impresión automática opcional, copias y **dos impresoras configurables**: una para los
  tickets de venta y otra para cierres, reportes y documentos de trabajo.
- **Lector de códigos de barras** tipo teclado: al escanear un SKU y pulsar Enter, el producto se
  agrega al POS si coincide exactamente.
- **Cierre de ventas detallado**: el cierre diario, el ticket y el correo incluyen **todos los
  productos vendidos con sus cantidades**, no solo un resumen de los más vendidos.
- Facturación venezolana: RIF del negocio y del cliente, IVA desglosado, IGTF sobre divisas,
  numeración de facturas y notas de entrega.

**Inventario y compras**

- Productos por categoría, con SKU, costo, precio, stock y stock mínimo, visibles en el punto de
  venta y en la pantalla del cliente. El menú de clic derecho permite editar, activar, desactivar o
  eliminar el producto (solo administrador).
- **Eliminación desde el catálogo** de productos, categorías, tarifas, paquetes de tiempo y zonas;
  las ventas y movimientos históricos se conservan.
- Servicios sin control de stock (impresión, escaneo, fotocopias).
- Compras a proveedores que actualizan stock y costo.
- **Saldos y abonos a proveedores**: compras menos pagos, con abonos en efectivo, tarjeta o
  transferencia e historial por proveedor.
- Kardex de movimientos: inicial, compra, venta, ajuste, merma y devolución.
- Alertas de stock bajo en el tablero.

**Clientes, reportes y configuración**

- Clientes con cédula/RIF, teléfono, correo, dirección fiscal, límite de crédito y membresía.
- Recargas de saldo y saldo a favor descontado al cobrar.
- Reportes de ventas por día, productos, equipos, inventario valorizado y cajas, con filtro de
  fechas y exportación a CSV.
- **Cierre diario** imprimible con ventas, caja, formas de pago, gastos, pagos a proveedores y
  productos más vendidos, en ticket de 80 mm, A4 o Carta.
- **Envío del cierre diario por correo**: opción configurable que envía el resumen del día y un
  CSV adjunto a uno o varios destinatarios al abrir el cierre, con reenvío manual y correo de
  prueba desde *Configuración → Reportes por correo*.
- Usuarios con roles: administrador, cajero y técnico, con **eliminación permanente** de cuentas
  (bloqueada para el propio usuario, el último administrador activo y usuarios con cajas abiertas).
- **Menú de sesión en el panel izquierdo**: muestra el usuario y su rol, permite **cambiar de
  cajero o administrador** sin cerrar la aplicación y **salir de CyberManager**. El administrador
  ve la lista de usuarios activos y el cambio siempre pide la contraseña.
- **Interfaz de cajero simplificada**: oculta y bloquea catálogo, inventario, compras, equipos,
  fondos, promos, reportes, configuración y registro; solo quedan tablero, caja/POS, clientes y
  reservas.
- Configuración del negocio, IVA, IGTF, avisos, ticket, **impresora de tickets e impresora de
  trabajo** y datos de instalación.
  Los cambios quedan como borrador y se guardan solo al pulsar **Aplicar y guardar**.
- **Apariencia personalizable**: seis temas predefinidos, doce colores, tamaño de iconos y texto
  de navegación, y tamaño de los cuadros de artículos y de terminales (80 a 160 %), guardados por
  usuario. Los bordes tienen mayor contraste para mejorar la legibilidad y los botones de
  minimizar, maximizar y cerrar siempre quedan visibles. Los campos de escritura son compactos
  (altura mínima de 26 px, relleno reducido y fuente de 12,5). Los cambios se confirman con
  **Aplicar y guardar**.
- **Dos idiomas**: español e inglés, seleccionables en la consola y en la configuración de cada
  estación. El cambio de idioma se aplica al instante.
- **Tasa oficial del BCV automática**: el servidor consulta el sitio del Banco Central de
  Venezuela cada 30 minutos y registra la tasa oficial del dólar; también puede consultarla al
  instante o registrar una tasa manual.
- **Primer inicio con elevación**: al abrir el servidor por primera vez se solicita permiso de
  administrador para crear la regla de Firewall del puerto. La tarea programada se ejecuta como
  SYSTEM y no muestra avisos; use `--no-elevate` para iniciar sin UAC en pruebas.
- **Respaldos integrados**: copia manual o automática de la base SQLite, exportación a un archivo,
  restauración con respaldo de seguridad previo, retención configurable y **carpeta de destino
  elegible** desde un desplegable.
- **Instaladores de Windows** autocontenidos y unificados: **Servidor** (servidor + consola) y
  **Estación** (cliente + agente). Cada instalador incluye los binarios de **x64 y x86** y elige
  la arquitectura adecuada al instalar. Todos los ejecutables y ambos instaladores comparten el
  icono de CyberManager (`assets/CyberManager.ico`).
- **Distribuciones optimizadas**: se publican sin archivos PDB, sin documentación XML y con
  recursos satélite solo en español e inglés. El cliente descarga los videos promocionales solo
  una vez y los guarda en caché por hash.

---

## Arquitectura

```
                 LAN (HTTP + SignalR)
  ┌─────────────────────────────────────────────────────────┐
  │                                                         │
  │   CyberManager.Server  (ASP.NET Core 8 + SQLite)        │
  │   · API REST /api/*                                     │
  │   · Hubs /hubs/admin /hubs/agent /hubs/station          │
  │   · JWT para usuarios, token de equipo para estaciones  │
  │                                                         │
  └───────┬──────────────────────┬──────────────────┬───────┘
          │                      │                  │
  ┌───────▼────────┐   ┌─────────▼────────┐  ┌──────▼─────────────┐
  │ Admin (WPF)    │   │ Client (WPF)     │  │ Agent (servicio)   │
  │ caja, tablero, │   │ pantalla de      │  │ apagar/reiniciar/  │
  │ inventario,    │   │ bloqueo + barra  │  │ cerrar sesión,     │
  │ reportes       │   │ de sesión        │  │ heartbeat, msg.exe │
  └────────────────┘   └──────────────────┘  └────────────────────┘
```

- **CyberManager.Server** — ASP.NET Core 8 minimal API, EF Core + SQLite, JWT, SignalR.
  Valida el token de cada equipo y concentra toda la lógica de negocio.
- **CyberManager.Admin** — consola WPF de administración y caja. Es la aplicación que usa el
  personal del local.
- **CyberManager.Client** — cliente WPF que se ejecuta en cada PC. Muestra una pantalla de
  bloqueo a pantalla completa cuando no hay sesión pagada, y una barra de sesión con tiempo
  restante y acceso a pedidos cuando la sesión está activa. Incluye bloqueo de teclado.
- **CyberManager.Agent** — servicio de Windows que atiende las órdenes del servidor (apagar,
  reiniciar, cerrar sesión, matar procesos, mostrar mensajes) y reporta CPU/RAM/usuario.
- **CyberManager.Desktop** — capa compartida: cliente HTTP tipado, SignalR, MVVM, convertidores
  y tema oscuro.
- **CyberManager.Shared** — entidades, enums y DTOs usados por todos los proyectos.
- **CyberManager.Uninstaller** — desinstalador independiente que localiza las instalaciones de
  Inno Setup del servidor y/o la estación, detiene sus procesos y las elimina. Conserva la base
  de datos del negocio.

---

## Requisitos

- **Windows 10** (también funciona en Windows 11) para el servidor y las estaciones, en
  **x64 o x86**. Los instaladores detectan la arquitectura del equipo y copian los binarios
  correspondientes; no hace falta elegir una versión distinta.
- **.NET 8 SDK** para compilar; **.NET 8 Desktop Runtime + ASP.NET Core Runtime** para ejecutar
  compilaciones dependientes del framework. Los scripts de instalación publican
  *self-contained* por defecto, así que las estaciones no necesitan instalar .NET.
- Puertos: **5080/TCP** abierto en el PC servidor.
- Todos los equipos en la **misma red local**. Use la IP LAN del servidor
  (por ejemplo `192.168.1.10`), no `localhost`, en las estaciones.
- Para el agente: permisos de administrador y, preferiblemente, una cuenta de servicio con
  privilegios suficientes para apagar el equipo.

---

## Estructura del repositorio

```
CyberManager/
├─ CyberManager.sln
├─ Directory.Build.props
├─ version.props
├─ README.md
├─ PROGRESO.md
├─ scripts/
│  ├─ build-installer.ps1
│  ├─ install-agent.ps1
│  ├─ install-client-startup.ps1
│  └─ run-dev.ps1
├─ installer/
│  ├─ CyberManager.Servidor.iss  Instalador del servidor + consola
│  ├─ CyberManager.Estacion.iss  Instalador del cliente + agente
│  └─ scripts/                 Configuración de servidor, agente y desinstalación
└─ src/
   ├─ CyberManager.Shared/    Dominio, enums y DTOs
   ├─ CyberManager.Desktop/   HTTP, SignalR, MVVM y tema oscuro
   ├─ CyberManager.Server/    ASP.NET Core 8, EF Core SQLite, JWT, SignalR
   ├─ CyberManager.Agent/     Servicio de Windows
   ├─ CyberManager.Client/    Cliente WPF de estación
   ├─ CyberManager.Admin/     Consola WPF de administración y caja
   └─ CyberManager.Uninstaller/ Desinstalador independiente
```

`dist/` y `artifacts/` son carpetas generadas: `dist\x64` y `dist\x86` contienen las
publicaciones autocontenidas de cada arquitectura, y `artifacts` los instaladores y los registros
de desarrollo. Al terminar puede eliminarlas: se reconstruyen con los scripts.

---

## Compilación

Desde la carpeta `CyberManager`:

```powershell
dotnet restore CyberManager.sln
dotnet build CyberManager.sln -c Release
```

Si el SDK está instalado solo para el usuario actual, use la ruta completa:

```powershell
& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" build CyberManager.sln -c Release
```

Publicar los ejecutables (ejemplo para x64; use `win-x86` y `dist\x86` para 32 bits):

```powershell
# Servidor (self-contained, no requiere .NET en el PC servidor)
dotnet publish src\CyberManager.Server\CyberManager.Server.csproj -c Release -r win-x64 --self-contained true -o dist\x64\Server

# Consola de administración
dotnet publish src\CyberManager.Admin\CyberManager.Admin.csproj -c Release -r win-x64 --self-contained true -o dist\x64\Admin
```

> El cliente y el agente se publican con los scripts de instalación, que además escriben su
> configuración.

Para generar los **instaladores unificados** (x64 + x86):

```powershell
.\scripts\build-installer.ps1
```

La versión vive en `version.props` y **se incrementa automáticamente en cada empaquetado**:
sube la versión menor y reinicia el patch (`1.1.1` → `1.2.0` → `1.3.0`…). Ese valor se aplica a
todos los ensamblados, al servidor, a la consola y al nombre de los instaladores. Para fijar una
versión concreta use `.\scripts\build-installer.ps1 -Version 1.1.1`; para conservar la actual,
`-NoBump`.

El script publica los cinco proyectos en modo autocontenido para `win-x64` y `win-x86`, copia los
scripts de configuración en `dist\x64` y `dist\x86`, publica el desinstalador independiente en
`dist\<arquitectura>\Uninstaller` y compila con **Inno Setup 6**:

- `artifacts\CyberManager-Servidor-<versión>.exe` — servidor + consola + lanzador; elige x64/x86.
- `artifacts\CyberManager-Estacion-<versión>.exe` — cliente + agente; elige x64/x86.

Cada instalador contiene ambos payloads, activa el modo de 64 bits en equipos x64 y copia el
payload de 32 bits en equipos x86. La separación servidor/estación se mantiene: siguen siendo dos
instaladores independientes.

Opciones: `-Version <x.y.z>`, `-NoBump`, `-Architecture all|x64|x86` (`all` por defecto; `x64`/`x86`
solo tienen sentido junto con `-SkipInstaller` para publicaciones de diagnóstico), `-SkipPublish`
(reutiliza `dist\x64` y `dist\x86`), `-SkipInstaller` (solo publica), `-InstallInnoSetup` (instala
Inno Setup con `winget` si no está disponible) y `-Clean` (elimina `bin`/`obj` al terminar). La
publicación elimina PDB, documentación XML y recursos de idiomas no soportados para reducir el
tamaño de los instaladores.

### Actualización y desinstalación

Los instaladores detectan si el servidor, la consola, el cliente o el agente están en ejecución.
Antes de reemplazar archivos muestran una confirmación; si el usuario acepta, detienen la tarea
programada y los procesos, actualizan y vuelven a iniciar lo que estaba activo. Si rechaza, la
instalación se cancela sin interrumpir las sesiones en curso. En instalaciones silenciosas no se
muestra la confirmación, pero sí se detienen y reinician los procesos.

El desinstalador independiente se copia en `%ProgramData%\CyberManager\CyberManager.Uninstaller.exe`
y está disponible en:

- el menú Inicio, como **Desinstalar todo CyberManager**;
- **Configuración → Instalación** de la consola;
- **Configuración → Mantenimiento → Cerrar y desinstalar** del cliente de estación, que pide
  confirmación, cierra el cliente y ejecuta el desinstalador con permisos de administrador;
- **Aplicaciones instaladas** de Windows (los desinstaladores de Inno Setup de cada componente).

Elimina el servidor y/o la estación, sus servicios y accesos directos, pero **conserva la base de
datos del negocio** y la configuración local de las estaciones.

---

## Puesta en marcha rápida

1. **Arranque el servidor** (en el PC principal):

   ```powershell
   dotnet run --project src\CyberManager.Server\CyberManager.Server.csproj
   ```

   El servidor escucha en `http://0.0.0.0:5080`, crea `cybermanager.db` junto al ejecutable y
   carga los datos de demostración la primera vez.

   O use el script de desarrollo, que además abre la consola:

   ```powershell
   .\scripts\run-dev.ps1
   ```

2. **Abra la consola de administración**:

   ```powershell
   dotnet run --project src\CyberManager.Admin\CyberManager.Admin.csproj
   ```

   En la pantalla de conexión use `http://127.0.0.1:5080` si está en el mismo PC, o la IP LAN
   del servidor si está en otro equipo.

3. **Inicie sesión** con `admin` / `admin123`.

4. **Revise la tasa del día** en *Configuración → Tasa de cambio*. El servidor consulta el BCV
   automáticamente al arrancar; si prefiere hacerlo al instante use **Consultar BCV ahora**.
   La base de demostración trae `1 USD = 40,00 Bs` como ejemplo; puede registrar una tasa manual
   antes de cobrar.

5. **Abra la caja** en *Caja → Abrir caja* (la apertura exige una tasa mayor que cero).

6. **Registre sus equipos** en *Equipos*. Cada equipo recibe un ID y un token.

7. **Configure el cliente** en cada PC con el ID, el token y la URL LAN del servidor, e instale
   el agente como servicio.

> Si prefiere una instalación guiada, use los instaladores descritos en la sección siguiente.

---

## Instaladores de Windows

Hay **tres instaladores autocontenidos**: el **unificado** (al abrirlo se elige servidor o estación)
y los dos separados (servidor y estación). Todos incluyen los binarios de **x64 y x86** y detectan
automáticamente la arquitectura del equipo: en Windows x64 instalan la versión de 64 bits y en
Windows x86 la de 32 bits. Ninguno requiere .NET instalado en el equipo de destino. Ejecútelos
**como administrador**. Los instaladores incluyen **español e inglés**: el idioma se elige al
inicio y puede cambiarse después en la aplicación. Todos los instaladores y ejecutables usan el
icono de CyberManager.

### Instalador unificado (recomendado)

`CyberManager-Unificado-<versión>.exe` sirve para cualquier equipo. En la página **¿Qué desea
instalar?** se escoge una de tres opciones:

| Opción | Qué instala | Carpeta |
|---|---|---|
| **Servidor (equipo principal con la base de datos)** | API, base de datos y consola de administración | `C:\CyberManager\Servidor` |
| **Estación / cliente (PC de caja o de juego)** | Cliente, agente de Windows y scripts de configuración | `C:\CyberManager\Estacion` |
| **Servidor y estación en este mismo equipo** | Ambos componentes | `C:\CyberManager\Servidor` y `C:\CyberManager\Estacion` |

Las tareas, los accesos directos y el arranque automático se ajustan a lo elegido: las tareas del
servidor (tarea programada, puerto 5080, visor VNC) solo aparecen con el servidor y las de la
estación (inicio del cliente, agente, servidor VNC) solo con la estación. La carpeta final es la
misma que usan los instaladores separados, así que los scripts, la base de datos y la configuración
local siguen funcionando sin cambios.

Si el equipo tiene instalada una versión anterior de los instaladores separados, el unificado la
desinstala en silencio antes de copiar los archivos, para no dejar dos entradas en **Aplicaciones
instaladas**. La base de datos (`cybermanager.db`) y la configuración local del cliente se
conservan.

> Para agregar el otro componente a un equipo ya instalado, vuelva a ejecutar el unificado y elija
> **Servidor y estación en este mismo equipo**.

### Servidor

`CyberManager-Servidor-<versión>.exe` instala el **servidor central y la consola de
administración** en `C:\CyberManager\Servidor`.

1. Ejecute el instalador y confirme la carpeta (por defecto `C:\CyberManager\Servidor`).
2. Deje marcadas las tareas recomendadas:
   - **Iniciar el servidor al encender el equipo**: crea la tarea programada
     `CyberManagerServer` (SYSTEM, al inicio) y arranca el servidor.
   - **Abrir el puerto 5080 en el Firewall de Windows**: regla de entrada TCP para todos los
     perfiles de red (Windows suele clasificar como «Pública» la red del local).
3. Al finalizar puede abrir la consola de administración. La página final también ofrece la casilla
   **Iniciar el servidor ahora**, que deja el servicio activo sin abrir ventanas ni duplicar la
   instancia si ya estaba en ejecución.

El acceso directo principal **CyberManager** usa `Server\CyberManager.exe --launch-console`: inicia
el servidor en segundo plano si aún no está activo, espera a que responda y abre la consola. También
se crean accesos directos separados para la consola y para el servidor en primer plano.

Después de instalar:

- La base de datos se crea en `C:\CyberManager\Servidor\Server\cybermanager.db`.
- Inicie sesión en la consola con `admin` / `admin123` y cambie la contraseña.
- Compruebe el servidor con `Invoke-RestMethod http://127.0.0.1:5080/api/health`.
- La versión instalada aparece en la **barra de título**, en la barra lateral y en
  **Configuración → Instalación**.

### Estación

`CyberManager-Estacion-<versión>.exe` instala el **cliente y el agente de Windows** en
`C:\CyberManager\Estacion`.

1. Ejecute el instalador en cada PC o consola.
2. Deje marcada la tarea **Iniciar el cliente con Windows** para registrar la tarea programada
   `CyberManagerClient`, que abre el cliente con permisos elevados al iniciar sesión (sin UAC).
   Ya no se crea un acceso directo en la carpeta Inicio.
3. Al terminar, el instalador abre la configuración del agente. Indique el **ID**, el **token** y
   la **URL LAN del servidor**, y siga las instrucciones para registrar el servicio
   `CyberManagerAgent`.
4. En la página final también puede marcar **Abrir CyberManager Cliente** para iniciar el cliente
   de inmediato, sin esperar al siguiente inicio de sesión.

### Desinstalación

Use **Configuración → Instalación**, el acceso directo **Desinstalar todo CyberManager** o el
desinstalador de cada componente en **Aplicaciones instaladas** de Windows. Cada instalador detiene
únicamente sus propios servicios:

- **Servidor**: elimina la tarea programada y la regla de firewall, y **conserva la base de
  datos** en `Server` para no perder la información del negocio.
- **Estación**: elimina el servicio del agente. La configuración local del cliente se conserva en
  `%LOCALAPPDATA%\CyberManager\Client`.

> Los componentes pueden convivir en el mismo equipo porque usan carpetas distintas (`Servidor` y
> `Estacion`). Los instaladores eligen la arquitectura del equipo; si se vuelve a instalar sobre la
> misma carpeta, reemplazan los binarios de la arquitectura anterior y conservan la base de datos del
> servidor y la configuración local de la estación.

---

## Avisos de nuevas versiones

El servidor consulta **GitHub Releases** una sola vez, al abrir la consola de administración. Si
detecta una versión más reciente que la instalada y ya transcurrieron las **2 horas de espera**
desde la publicación (el tiempo es configurable), muestra una ventana que pregunta si desea
actualizar, con botones **Sí** y **No**. **Sí** abre la página de versiones del proyecto en GitHub;
**No** oculta el aviso para esa versión. No hay consultas periódicas: la comprobación solo se repite
al volver a abrir el programa o al pulsar **Buscar actualizaciones ahora**.

- Se configura en **Configuración → Instalación → Actualizaciones**.
- **Avisar cuando haya una nueva versión disponible**: activa o desactiva el aviso.
- **Horas de espera después de la publicación**: 2 por defecto; puede ajustarse o ponerse en 0.
- **Buscar actualizaciones ahora**: fuerza una consulta inmediata a GitHub.
- **No** en la ventana de actualización oculta el aviso de esa versión concreta. Volverá a aparecer
  cuando se publique una versión más nueva.

El servidor guarda el resultado en memoria y no satura la API de GitHub. La consulta no requiere
autenticación ni envía datos del negocio: solo lee las versiones públicas del repositorio.

---

## Credenciales de demostración

| Usuario  | Contraseña  | Rol           |
|----------|-------------|---------------|
| `admin`  | `admin123`  | Administrador |
| `cajero` | `cajero123` | Cajero        |

> Cámbielas antes de usar el sistema en producción. Los usuarios se gestionan en
> *Configuración → Usuarios*.

La base de demostración también incluye:

- 8 equipos `PC-01` … `PC-08` en la zona *Estándar*.
- 2 consolas de demostración `CON-01` y `CON-02` en la zona *Consolas* (solo en bases nuevas).
- Zonas *Estándar*, *VIP* y *Consolas*; tarifas de 1,00 / 1,50 / 2,00 USD por hora.
- Paquetes de tiempo de 30 min a 5 h.
- 16 productos en las categorías *Bebidas*, *Snacks*, *Comida* y *Servicios*.
- Tasa de ejemplo de 40,00 Bs/USD.

---

## Cambiar de usuario y cerrar la consola

En el pie del panel izquierdo de la consola hay un botón con el **usuario actual y su rol**. Al
pulsarlo se abre el menú de sesión:

- **Cambiar de usuario…**: pide el usuario y la contraseña, y vuelve a abrir la consola con la
  nueva sesión. La aplicación no se cierra y el servidor sigue funcionando.
- **Lista de usuarios activos** (solo para el administrador): muestra los demás usuarios con su
  nombre y rol para cambiar con un clic; siempre se pide la contraseña.
- **Salir de CyberManager**: cierra la consola después de confirmar. El servidor continúa
  ejecutándose en segundo plano.

El cambio de usuario cierra la conexión en tiempo real de la sesión anterior y la vuelve a abrir con
el token del nuevo usuario, de modo que el registro, el tablero y los permisos corresponden de
inmediato al rol que acaba de entrar. Si el inicio de sesión se cancela o falla, se mantiene la
sesión anterior.

---

## Flujo operativo diario

1. **Abrir caja** con el fondo inicial (cada PC abre su propia caja; ver
   *Dos o más cajas en la misma red*).
2. **Registrar la tasa del día** (si aún no está registrada).
3. **Iniciar sesiones**: desde el tablero elija un PC libre e indique tiempo, tarifa y cliente.
   - *Prepago*: cobra el tiempo por adelantado; si el cobro es en efectivo o por medios de pago,
     requiere caja abierta. El autoservicio del cliente usa el saldo de su cuenta y no pasa por caja.
   - *Postpago*: acumula tiempo y consumos; se cobra al finalizar.
4. **Consumos**: el cliente puede pedir productos desde la barra de sesión; quedan pendientes y
   se cobran al cerrar. También se pueden vender directo en *Caja / POS*.
5. **Extender, pausar o transferir** la sesión según lo necesite el cliente.
6. **Finalizar sesión**: se cobra tiempo + consumos pendientes en un solo movimiento de caja.
7. **Registrar compras y abonos a proveedores** en *Compras*: el saldo del proveedor es
   compras menos pagos. Los abonos en efectivo requieren caja abierta.
8. **Cerrar caja** al final del turno: el sistema muestra el esperado, usted digita el contado y
   se registra la diferencia.
9. **Emitir el cierre diario** en *Reportes → Cierre diario*: resume ventas, caja, formas de pago,
   gastos, pagos a proveedores y productos más vendidos, y se puede imprimir en ticket de 80 mm,
   A4 o Carta según el formato configurado.

---

## Autoservicio prepago y acceso de administrador

La pantalla de bloqueo ya no muestra configuración ni atajos ocultos. El botón **Iniciar sesión**
abre un menú con dos opciones:

- **Usuario prepagado** — autoservicio del cliente:
  1. **Crear cuenta** con nombre, usuario, contraseña y teléfono opcional.
  2. **Comprar un paquete de tiempo** con el saldo de la cuenta. Los paquetes y sus bonos se
     administran en *Configuración → Paquetes de tiempo*; el saldo se recarga en caja.
  3. **Entrar al equipo** indicando cuántos minutos usar. Si ya hay una sesión prepago activa en
     la estación, las compras adicionales se suman a la sesión en curso.
  4. **Finalizar sesión** desde la barra flotante: los minutos no usados vuelven al monedero y los
     consumos pendientes se cobran al saldo de la cuenta.
- **Administrador** — valida las credenciales contra el servidor y exige el rol *Admin*. Al entrar,
  la pantalla de bloqueo se oculta y se abre **Configuración del cliente**, donde se editan los
  datos del negocio, la barra de sesión, los fondos, los avisos y el control de sesiones. Los
  cambios se guardan en `/api/settings` y se aplican a todas las estaciones; el idioma se guarda
  solo en el equipo. Al cerrar, la pantalla de bloqueo vuelve y se refresca el estado.
  La sección **Mantenimiento** incluye **Cerrar y desinstalar**: pide confirmación, cierra el
  cliente y ejecuta el desinstalador con permisos de administrador, conservando la base de datos.

El servidor expone las rutas anónimas `/api/station/prepaid/info`, `register`, `login`,
`recharge`, `start` y `finish`, autenticadas con el ID/token de la estación. Las cuentas usan
`Customer.Username`, `PasswordHash`, `PasswordSalt` y `PrepaidMinutes`, y reutilizan el saldo
monetario del cliente (`Customer.Balance`).

---

## Dos o más cajas en la misma red

Una sola instalación del **servidor** puede atender varias consolas de administración al mismo
tiempo; todas comparten la misma base de datos y cada PC abre y cierra su propio turno de caja.

- **Servidor**: se instala una sola vez en el equipo principal (por ejemplo, `192.168.1.10`, puerto
  5080). No instale el servidor en las demás PCs ni comparta el archivo `cybermanager.db` por red.
- **Consolas**: en cada PC de caja instale la consola de administración y escriba en el login la URL
  del servidor (`http://192.168.1.10:5080`) y un nombre de **Caja de este equipo** distinto en cada
  PC (`CAJA-1`, `CAJA-2`, …). El nombre se guarda en `config.json` y viaja al servidor en la
  cabecera `X-Cash-Terminal`.
- **Turnos independientes**: cada caja abre su turno, registra sus movimientos y lo cierra sin
  afectar a las demás. El historial de *Caja* muestra la columna **Caja** para saber a qué terminal
  pertenece cada turno. Una misma caja no puede tener dos turnos abiertos a la vez.
- **Solo cajeros**: cree usuarios con rol *Cajero* para las PCs secundarias. Esa consola oculta las
  vistas administrativas (reportes, configuración, inventario, usuarios, etc.) y muestra solo
  tablero, caja/POS, sesiones y notas, con las mismas funciones del equipo principal. No hace falta
  un ejecutable aparte.
- **Cobros de sesiones**: el cobro se registra en la caja que abrió la sesión, aunque el cierre se
  haga desde otra consola; si esa caja ya cerró, se usa la caja del terminal que cierra. El
  autoservicio prepago con saldo del cliente no requiere caja abierta.
- **Compatibilidad**: si la consola no envía el nombre de caja (versiones anteriores o instalaciones
  de una sola caja), el servidor usa `PRINCIPAL` y todo sigue funcionando como antes. Al actualizar
  una base existente, los turnos anteriores quedan en `PRINCIPAL`.

---

## Moneda, tasa, IVA e IGTF

- **Moneda base: USD.** Todos los montos contables, reportes y arqueos se llevan en dólares.
- **Tasa Bs/USD diaria.** Se registra por fecha en *Configuración → Tasa de cambio*. Cada venta
  guarda la tasa con la que se cobró (`ExchangeRate`), de modo que los reportes históricos en Bs
  no cambian si la tasa sube o baja después.
- **Tasa oficial del BCV automática.** El servidor consulta
  [bcv.org.ve](https://www.bcv.org.ve/) al arrancar y luego cada **30 minutos**, y registra la
  tasa oficial del dólar como la tasa del día. La fecha valor publicada por el BCV queda anotada
  para auditoría. La actualización se puede desactivar con la casilla
  **Actualizar automáticamente desde el BCV** o forzar con **Consultar BCV ahora**. Si el sitio
  no responde o cambia de formato, el sistema conserva la última tasa y permite registrarla
  manualmente.
- **Precios con IVA incluido.** El sistema desglosa el IVA (16 % por defecto) desde el precio de
  venta al facturar; no suma el impuesto encima del precio mostrado.
- **IGTF (3 % por defecto).** Se aplica **solo** cuando el pago se registra en efectivo en
  dólares (`CashUsd`). Puede desactivarlo o cambiar el porcentaje en
  *Configuración → Negocio y facturación*.
- **Nombre y logo del negocio.** El nombre o razón social y el logo configurados en
  *Configuración → Negocio y facturación* aparecen juntos en una sola línea en la barra superior
  de la consola. El logo se ajusta a un máximo de 512 × 512 px, se guarda en el servidor y se
  comparte con todas las consolas.
- **Formas de pago y moneda del monto recibido**:
  - `Cash`: el monto recibido y el cambio se expresan en **Bs**.
  - `CashUsd`: el monto recibido y el cambio se expresan en **USD**; genera IGTF.
  - `Card`, `Transfer`, `CustomerBalance`: se cobra el total exacto.
  - `Mixed`: combina efectivo y otra forma de pago.
  - `Pending`: no cobra ahora; queda cargado a la sesión del equipo y se cobra al cerrarla.
- **Recargas de saldo** y **pagos con saldo del cliente** se registran en USD.

---

## Tickets, impresión y lector de códigos

En *Configuración → Ticket y scanner* puede definir:

- **Datos del negocio** que aparecen en el encabezado: nombre, RIF, dirección, teléfono,
  encabezado adicional y pie del ticket.
- **Impresora de tickets**: nombre exacto de la cola de impresión de Windows para los tickets de
  venta (vacío = preguntar cada vez). El desplegable muestra las impresoras instaladas y permite
  escribir un nombre manualmente.
- **Impresora de trabajo**: se usa para cierres, reportes y documentos. Si queda vacía, se usa la
  impresora de tickets.
- **Copias**: de 1 a 5.
- **Impresión automática**: imprime el ticket al registrar una venta.
- **Agregar al POS al escanear**: interpreta el lector como teclado.

Los tickets de venta se generan como documento de **80 mm** e incluyen número, fecha, forma de pago,
cliente, artículos, subtotal, IVA incluido, IGTF, totales en USD y Bs, tasa aplicada, recibido y
cambio. El **cierre diario** usa el formato elegido en *Configuración → Ticket y operación*:
ticket de 80 mm para impresoras térmicas, **A4** o **Carta** para hojas normales.

**Lector de códigos de barras**

1. Conecte el lector en modo *keyboard wedge* (envía los caracteres y un Enter).
2. En *Caja / POS*, escanee el código. Si el SKU o el nombre coinciden exactamente con un
   producto, se agrega al carrito y el foco vuelve al buscador.
3. Si el código no existe, use el buscador para agregarlo manualmente.

La impresión automática solo se ejecuta si **Impresión automática** está activa y hay una
**impresora de tickets** configurada. El botón **Imprimir ticket** de cada venta abre el diálogo de
impresión si no hay impresora guardada. En el mensaje de la venta, **Aceptar** cierra el resumen y
**Imprimir y aceptar** imprime el ticket y cierra en un solo paso.

---

## Consolas de videojuegos

Los equipos pueden ser **computadora**, **consola de videojuegos** u **otro**. En la ficha del
equipo (*Equipos → Nuevo / Editar*) indique:

- **Tipo de equipo**: PC, consola u otro.
- **Plataforma**: por ejemplo *PlayStation 5*, *Xbox Series X* o *Nintendo Switch*.
- **Cantidad de mandos**: visible en el tablero y en los reportes.

El tablero muestra el tipo, la plataforma y los mandos junto al nombre y la zona. Las consolas
usan el mismo flujo de sesiones, tarifas, consumos y caja que los PC; si el equipo tiene un PC
asociado, puede instalar allí el cliente y el agente para el bloqueo y las órdenes de energía.

---

## Pagos a proveedores y cierre diario

**Saldos y abonos**

- En *Compras* cada proveedor muestra **compras**, **pagos** y **saldo** (compras − pagos).
- **Abonar** registra un pago con fecha, monto, método, referencia y nota.
- **Historial** lista todos los abonos del proveedor.
- Los abonos en **efectivo** requieren una caja abierta y se registran como gasto de caja. Los
  abonos con tarjeta o transferencia no requieren caja.
- Un saldo negativo indica que el proveedor tiene un anticipo a favor.

**Cierre diario**

- En *Reportes* seleccione la fecha final del rango y pulse **Cierre diario**.
- El resumen incluye ventas de productos y tiempo, totales USD/Bs, IVA, IGTF, descuentos, ticket
  promedio, sesiones, fondo y arqueo de caja, gastos, abonos a proveedores, desglose por forma de
  pago y **todos los productos vendidos con sus cantidades**.
- Pulse **Imprimir cierre** para emitirlo en el formato configurado: ticket de 80 mm, A4 o Carta.
- *Exportar CSV* sigue disponible para cada pestaña del reporte.

**Envío automático por correo**

- En *Configuración → Reportes por correo* active **Enviar automáticamente el cierre diario por
  correo** e indique los destinatarios (separados por coma o punto y coma).
- Complete los datos del servidor SMTP: host, puerto, SSL/TLS, usuario, contraseña, correo y
  nombre del remitente. Funciona con Gmail (contraseña de aplicación), Outlook y servidores SMTP
  del negocio. La contraseña se guarda en el servidor y no se devuelve a las consolas.
- **Enviar correo de prueba** valida la configuración sin esperar al cierre.
- Al abrir el cierre diario, el servidor envía el resumen en HTML con el detalle en CSV adjunto.
  El envío se hace **una sola vez por fecha**; el botón **Reenviar por correo** del cierre lo
  repite. Si el correo falla, el cierre y la caja siguen funcionando normalmente.

---

## Apariencia y personalización

En *Configuración → Apariencia* puede cambiar la interfaz de la consola:

- **Seis temas predefinidos** (oscuro, azul, violeta, verde, ámbar y claro) y doce colores
  individuales para fondo, tarjetas, texto, acentos y estados.
- **Tamaño de los iconos** y del texto de navegación.
- **Tamaño de los cuadros**: los artículos del punto de venta y las terminales del tablero se
  escalan entre **80 % y 160 %**. El tamaño afecta dimensiones, relleno, esquinas y tipografía; se
  aplica al volver a abrir la vista correspondiente.
- **Idioma de la interfaz**: español o inglés; el cambio se aplica al guardar (ver
  [Idioma de la interfaz](#idioma-de-la-interfaz)).
- **Restablecer** vuelve a los valores del tema oscuro original (incluidos los cuadros al 100 %).

Las cajas de selección (casillas, botones de opción, listas desplegables y campos de fecha) usan
siempre un fondo oscuro con texto claro, de modo que sus etiquetas se distinguen incluso sobre
superficies claras. Las **listas desplegables son compactas** (altura mínima y sin texto adicional
en cada opción): solo muestran el nombre de la opción a elegir. Los **campos de escritura** también
son compactos: altura mínima de 26 px, relleno reducido y fuente de 12,5; los campos de varias
líneas alinean el texto arriba. Al dejar el cursor **2 segundos** sobre un botón aparece un globo
flotante con una descripción breve de su función, sin necesidad de hacer clic; las descripciones de
los botones se muestran **siempre en español**, aunque la interfaz esté en inglés.

**Guardado manual**: los cambios de configuración y de apariencia quedan como borrador o vista
previa y **solo se guardan al pulsar «Aplicar y guardar»**. No hay autoguardado: si cierra la
ventana o recarga sin guardar, se conservan los valores anteriores. El botón está en el encabezado
de *Configuración* y también en las pestañas Negocio, Reportes por correo y Apariencia.

**Tablero de equipos**: la barra de personalización (buscador, orden, tamaño, zona, métricas y
leyenda) está oculta por defecto; pulse **Configuración** en el encabezado del tablero para
mostrarla u **Ocultar configuración** para plegarla.

**Barras laterales ajustables**: arrastre los separadores a los lados del área central para
cambiar el ancho del menú de navegación y del registro de actividad. Cada barra tiene un ancho
mínimo y máximo para no perder accesos ni contenido.
La paleta trae bordes más luminosos que el tema original y la consola dibuja un marco de 1 píxel
alrededor de la ventana, de modo que los límites de paneles y tarjetas se distinguen con claridad.
La consola usa la **barra de título nativa de Windows**, de modo que los botones **Minimizar**,
**Maximizar/Restaurar** y **Cerrar** son los del sistema y siempre están visibles; en Windows 10/11
se aplica el modo oscuro a esa barra. La barra muestra el nombre del programa y la versión
instalada (por ejemplo, `CyberManager · Administración · v1.0.11`). La ventana se encuadra
automáticamente dentro del área de trabajo visible (sin quedar debajo de la barra de tareas) y se
reajusta al cambiar de monitor o de escala (DPI). La consola **recuerda la posición, el tamaño y el
estado (normal o maximizado) de la ventana principal y de la pantalla de inicio de sesión** para
cada usuario de Windows y de la aplicación; se guardan en
`%LOCALAPPDATA%\CyberManager\Admin\windows.json`.

La apariencia se guarda por usuario en
`%LOCALAPPDATA%\CyberManager\Admin\appearance.json` y se aplica al abrir la consola, antes de la
pantalla de inicio de sesión. No afecta al cliente de las estaciones.

---

## Idioma de la interfaz

CyberManager admite **solo dos idiomas: español e inglés**.

- **Consola de administración**: *Configuración → Apariencia → Idioma de la interfaz*. El cambio
  se guarda en `appearance.json` y se aplica de inmediato, sin reiniciar la consola.
- **Estación (cliente)**: en la ventana **Configurar esta estación** elija *Idioma de la
  estación*. También se puede cambiar en *Configuración / token* del cliente. La selección se
  guarda en `%LOCALAPPDATA%\CyberManager\Client\config.json`.
- Los instaladores también están traducidos: el idioma del asistente se elige al comenzar.
- Los textos internos que el servidor envía (nombres de productos, mensajes escritos por el
  administrador) se muestran tal cual; la traducción cubre las etiquetas, títulos, botones,
  estados, formas de pago y mensajes del sistema.

---

## Respaldos de la base de datos

En *Configuración → Respaldos* el administrador puede proteger la información del negocio sin
detener el servidor:

- **Respaldo automático**: activado por defecto, con frecuencia configurable (24 horas por defecto)
  y retención de los últimos 30 respaldos automáticos. El servidor revisa cada pocos minutos si
  venció el intervalo y crea la copia en segundo plano.
- **Carpeta de respaldos**: un desplegable permite elegir la **carpeta del servidor** donde se
  guardan las copias (por ejemplo otra unidad o un disco externo conectado al servidor). Use
  **Examinar…** para agregar una ruta y **Aplicar carpeta** para guardarla; **Predeterminada**
  restablece la carpeta `respaldos` junto a la base. El servidor comprueba que la carpeta exista o
  se pueda crear y que admita escritura antes de aceptar el cambio.
- **Crear respaldo ahora**: genera una copia manual en el momento.
- **Exportar copia**: descarga el respaldo elegido a su equipo para guardarlo en un disco externo
  o en la nube.
- **Restaurar seleccionado**: reemplaza la base activa por un respaldo de la lista.
- **Restaurar desde archivo**: carga un `.db` exportado previamente y lo restaura.
- **Abrir carpeta**: abre en el Explorador la carpeta de respaldos del servidor.

Por omisión los archivos se guardan en `respaldos` junto a la base de datos (por ejemplo
`C:\CyberManager\Servidor\Server\respaldos`) con nombres
`cybermanager_AAAAMMDD_HHMMSS_manual.db`, `..._auto.db` o `..._antes_restaurar.db`. Al cambiar la
carpeta, los respaldos anteriores **permanecen en su ubicación original**; las copias nuevas se
crean en la carpeta elegida.

Antes de restaurar, el sistema **crea automáticamente un respaldo de seguridad**
(`antes_restaurar`) y valida que el archivo sea una base SQLite íntegra y corresponda a
CyberManager. La copia se hace con la API de respaldo en línea de SQLite, por lo que no es
necesario cerrar la caja ni detener el servidor.

---

## Importar productos desde una lista de stock

`scripts/import-stock.ps1` carga la lista exportada por el sistema anterior (CSV o tabla HTML con
las columnas Código, Descripción, Precio, %IVA, Stock y Mínimo). Usa el código como SKU: crea los
productos que no existen y actualiza nombre, precio, stock, mínimo, IVA y tipo de los que ya
existen, sin duplicar códigos. Las descripciones que contienen comas se reconstruyen
automáticamente.

```powershell
# Ver qué haría, sin escribir nada
.\scripts\import-stock.ps1 -File .\resources\stock.csv -DryRun

# Importar y eliminar los 16 productos de ejemplo del sembrado inicial
.\scripts\import-stock.ps1 -File .\resources\stock.csv -DeleteDemoProducts
```

Por defecto aplica **16 % de IVA** a todos los productos (el %IVA del archivo se ignora), crea un
respaldo en `respaldos` antes de escribir y marca como servicio los productos sin stock, sin
mínimo y con precio mayor que cero, para que sigan siendo vendibles. Opciones útiles:
`-TaxRate`, `-ServiceMode Auto|None|All`, `-Database`, `-ServerDir` y `-SkipBackup`.

---

## Imágenes e iconos de productos (eliminados)

Las versiones anteriores permitían asignar una imagen o un icono (emoji o texto) a cada producto.
**A partir de la versión 1.0.20 esa función ya no existe**: los productos solo tienen código, nombre,
categoría, precio, costo, existencias, stock mínimo, unidad, IVA y estado.

- El catálogo, el punto de venta y la pantalla del cliente muestran únicamente los datos del
  producto, sin columnas ni tarjetas con imágenes.
- La API ya no expone los campos `Icon`, `HasIcon` ni `IconUpdatedUtc`, y los endpoints
  `/api/products/{id}/icon` y `/api/station/products/{id}/icon` responden **404**.
- Al arrancar, el servidor **elimina automáticamente** la tabla `ProductIcons` y las columnas
  `Icon`, `IconUpdatedUtc` e `ImagePath` de `Products` si existen en una instalación anterior. Los
  productos y el resto de los datos se conservan intactos.
- Los archivos `appearance.json` antiguos siguen cargando sin errores; la clave de visualización de
  productos se ignora.

---

## Configurar un equipo (PC)

### Alta automática con «Escanear la red»

El servidor busca en la red local las estaciones que tienen el cliente o el agente
instalado (UDP **5092** y **5093**). Pulse **Equipos → Escanear la red**, revise la lista y
confirme: el servidor crea los equipos que faltan, actualiza su IP, MAC y versión, y les envía
por UDP el **ID y el token** definitivos. La estación los guarda en `config.json` (o el agente
en `appsettings.json`) y reconecta sola, sin escribirlos a mano.

Los equipos deben estar encendidos y el firewall debe permitir esos puertos UDP. El instalador
de estación crea la regla automáticamente.

### Configuración manual

1. En la consola vaya a **Equipos**, abra el menú **Acciones** de la fila del PC y elija
   **Configuración / token**.
2. Anote el **ID del equipo** y el **token**. Puede copiar el token al portapapeles.
3. En el PC de la estación, abra `CyberManager.Client.exe`. Si no está configurado, aparece la
   ventana de configuración. Indique:
   - **URL del servidor**: `http://192.168.1.10:5080` (IP LAN del servidor) o pulse
     **Buscar en la red** para localizarlo automáticamente con `/api/health`. Si escribe solo la
     IP o el nombre del equipo, el sistema agrega el puerto `5080`.
   - **ID del equipo**: el número anotado.
   - **Token**: el token del equipo.
   - **Idioma de la estación**: español o inglés.
4. Pulse **Probar conexión** y luego **Guardar**.

La configuración se guarda en:

```
%LOCALAPPDATA%\CyberManager\Client\config.json
```

> **Regenerar el token** invalida la configuración anterior: el cliente dejará de conectar hasta
> que actualice su token.

También puede escribir la configuración desde PowerShell:

```powershell
$dir = "$env:LOCALAPPDATA\CyberManager\Client"
New-Item -ItemType Directory -Force -Path $dir | Out-Null
@{
  ServerUrl    = "http://192.168.1.10:5080"
  LastUsername = $null
  StationId    = 1
  StationToken = "PEGUE-AQUI-EL-TOKEN"
  Language     = "es"   # "es" o "en"
} | ConvertTo-Json | Set-Content "$dir\config.json" -Encoding UTF8
```

---

## Instalar el agente de Windows

El agente es el que puede **apagar, reiniciar, cerrar sesión, matar procesos y mostrar mensajes**
en el PC. El cliente WPF se encarga del bloqueo visual y de la sesión.

En cada estación, abra **PowerShell como administrador** y ejecute:

```powershell
cd C:\ruta\a\CyberManager\scripts
Set-ExecutionPolicy -Scope Process Bypass -Force
.\install-agent.ps1 -StationId 1 -Token "TOKEN_DEL_EQUIPO" -ServerUrl "http://192.168.1.10:5080"
```

El script:

1. Publica `CyberManager.Agent` en modo *self-contained*.
2. Copia los binarios a `C:\CyberManager\Agent` (configurable con `-InstallDir`).
3. Escribe `appsettings.json` con `ServerUrl`, `StationId` y `Token`.
4. Registra el servicio **CyberManagerAgent** con inicio automático y reinicio ante fallos.
5. Lo inicia.

Opciones útiles:

| Parámetro              | Descripción                                                        |
|------------------------|--------------------------------------------------------------------|
| `-InstallDir`          | Carpeta de instalación (por defecto `C:\CyberManager\Agent`).      |
| `-ServiceName`         | Nombre del servicio (por defecto `CyberManagerAgent`).             |
| `-FrameworkDependent`  | Publica sin incluir el runtime (requiere .NET 8 instalado).        |

Comprobar el servicio:

```powershell
Get-Service CyberManagerAgent
sc.exe query CyberManagerAgent
```

Desinstalar:

```powershell
Stop-Service CyberManagerAgent
sc.exe delete CyberManagerAgent
```

> Configure el servicio para que inicie sesión con una cuenta que tenga permisos sobre el equipo.
> La cuenta `LocalSystem` (predeterminada) suele ser suficiente para apagar y reiniciar. Los
> mensajes usan `msg.exe`, que requiere que el servicio de mensajes esté disponible en la red.

---

## Actualización automática de los clientes

El instalador del servidor guarda junto a `CyberManager.Server.exe` la carpeta `ClientUpdate`
con `client.zip` y `manifest.json` (versión, tamaño y SHA-256) del cliente de esa misma versión.

- Cuando el servidor se actualiza, el paquete cambia y las estaciones lo detectan.
- El agente (servicio SYSTEM) descarga el ZIP, verifica el SHA-256, reemplaza los archivos de
  `..\Client` y deja la marca `update.pending`; el cliente se reinicia con la versión nueva.
- El cliente también consulta el manifiesto cada 10 minutos y puede aplicarlo si tiene permisos
  de escritura en su carpeta.
- En **Equipos** el box **Clientes** muestra cuántos equipos están al día y permite forzar el
  aviso con **Actualizar clientes**.
- Desde el menú **Acciones ▾** de cada equipo, **Actualizar cliente** envía el aviso solo a ese
  equipo (cliente y agente), sin afectar a las demás estaciones.

El paquete se genera automáticamente con `scripts\build-installer.ps1` a partir de
`dist\<arch>\Client` y queda en `dist\<arch>\Server\ClientUpdate`, que el instalador del servidor
copia a `C:\CyberManager\Servidor\Server\ClientUpdate`.

---

## Inicio automático del cliente

En cada PC, ejecute como administrador:

```powershell
cd C:\ruta\a\CyberManager\scripts
.\install-client-startup.ps1 -StationId 1 -Token "TOKEN_DEL_EQUIPO" -ServerUrl "http://192.168.1.10:5080"
```

El script publica el cliente, lo copia a `C:\CyberManager\Client`, escribe el `config.json` del
equipo y registra la tarea programada `CyberManagerClient`. Esa tarea abre el cliente al iniciar
sesión con el **nivel de privilegio más alto disponible**, de modo que el cliente se ejecuta como
administrador sin mostrar el UAC en cada arranque. Ya no se crea un acceso directo en la carpeta
Inicio; al actualizar desde una versión anterior, el instalador lo elimina.

Opciones:

| Parámetro              | Descripción                                                              |
|------------------------|--------------------------------------------------------------------------|
| `-InstallDir`          | Carpeta de instalación (por defecto `C:\CyberManager\Client`).            |
| `-NoPublish`           | No publica; solo copia la configuración y registra la tarea de inicio.    |
| `-FrameworkDependent`  | Publica sin incluir el runtime.                                          |

Para abrir el cliente sin cerrar sesión use el menú Inicio o:

```powershell
schtasks /Run /TN "CyberManagerClient"
```

Para quitar solo el inicio automático (sin desinstalar el cliente):

```powershell
.\installer\scripts\configurar-cliente.ps1 -Remove
```

**Recomendaciones de kiosco**

- El cliente pide permisos de administrador (`requireAdministrator`) y la tarea de inicio usa
  `HighestAvailable`. Por eso el usuario de Windows de la estación debe ser **administrador local**
  del equipo; con una cuenta estándar Windows pedirá credenciales al abrir el cliente.
- Configure el inicio de sesión automático de Windows para esa cuenta si el equipo debe quedar
  operativo sin intervención.
- El instalador registra **dos** tareas: `CyberManagerClient` (arranca con la sesión) y
  `CyberManagerClientWatchdog` (revisa cada 2 minutos y vuelve a abrir el cliente si se cerró).
  Es una red de seguridad, no un kiosco completo.
- CyberManager **no** implementa un modo kiosco de Windows. Para impedir el acceso al escritorio
  use las políticas del equipo (shell personalizado, bloqueo de la barra de tareas, del
  Administrador de tareas y de las teclas Windows). La antigua opción «Modo kiosco» no tenía
  efecto y se retiró en la versión 1.3.9.
- `Ctrl+Alt+Del` no se puede interceptar desde el cliente: si el usuario de la estación es
  administrador local, puede cerrar el cliente o la sesión. Para un kiosco real use una cuenta
  estándar con políticas restrictivas.

---

## Galería de fondos de pantalla

La consola incluye **Sistema → Fondos de pantalla** para administrar las imágenes que ven los
clientes mientras el equipo está bloqueado.

El servidor incluye de fábrica **30 fondos 4K (3840 × 2160)** con los logos de los juegos más
populares (League of Legends, Free Fire, Valorant, Counter-Strike 2, GTA V/VI, Minecraft, Roblox,
Fortnite, Call of Duty, Apex Legends, Dota 2, PUBG, Among Us, Elden Ring, Cyberpunk 2077,
EA Sports FC, eFootball, Genshin Impact, Mobile Legends, Clash Royale, Brawl Stars, Rocket League,
Rainbow Six Siege, Overwatch 2, Red Dead Redemption 2, God of War, Spider-Man 2, Dragon Ball y
Mortal Kombat 1). La primera vez que arranca el servidor se copian a la tabla `Wallpapers`; si el
administrador los borra, no se vuelven a agregar. Para incorporar un paquete nuevo se cambia la
versión del paquete en `DbSeeder` y se reconstruye el instalador.

1. Pulse **Subir imágenes** y seleccione uno o varios archivos **PNG, JPG, GIF o BMP** (máximo
   20 MB por imagen). Cada fondo se guarda como BLOB en la tabla `Wallpapers` de SQLite, por lo
   que no depende de carpetas compartidas ni de rutas locales.
2. **Buscar en internet** abre un buscador de fondos de [Wallhaven](https://wallhaven.cc) con
   resultados solo para todo público (SFW), formato 16:9 y resolución mínima de 1920 × 1080,
   2560 × 1440 o 3840 × 2160. Elija una miniatura y pulse **Agregar** para incorporarla a la
   galería; **Cargar más** sigue paginando los resultados. También puede filtrar por categoría
   (General, Anime, Personas o Todas).
3. **Abrir carpeta** exporta la galería como archivos a
   `%LOCALAPPDATA%\CyberManager\Admin\Fondos` y abre la carpeta en el Explorador. Desde allí puede
   reemplazar una imagen conservando el nombre, o copiar imágenes nuevas. **Sincronizar carpeta**
   detecta los archivos nuevos o modificados (por hash SHA-256) y los sube al servidor: el número
   al inicio del nombre define el orden y las imágenes nuevas se agregan con su nombre de archivo.
   Para eliminar un fondo siga usando **Eliminar** en la consola.
4. Elija el modo de presentación:
   - **Sin fondos**: se mantiene el degradado original del cliente.
   - **Fondo fijo**: un único fondo seleccionado.
   - **Presentación rotativa**: todos los fondos activos, con rotación entre **5 y 3600**
     segundos.
5. Use **Guardar y aplicar** para publicar los cambios. También puede activar/desactivar fondos,
   cambiar su nombre y orden, o eliminarlos desde el panel derecho. El buscador **Buscar fondo**
   filtra la galería por nombre o estado.
6. **Reenviar a equipos** vuelve a publicar la galería sin cambiar la configuración (útil si un
   equipo estuvo desconectado).

La lista se envía por SignalR (`WallpaperChanged`) en cuanto se guarda, de modo que los clientes
bloqueados cambian de fondo sin reiniciar. Cada cliente descarga las imágenes por la LAN con su
token de equipo y las cachea en
`%LOCALAPPDATA%\CyberManager\Client\wallpapers` usando el hash SHA-256 como nombre; así los
reinicios posteriores no vuelven a descargarlas. Si una imagen no se puede descargar, el cliente
continúa con las demás.

En modo **Presentación rotativa** la rotación funciona como un salvapantallas: solo avanza cuando
el equipo lleva **2 minutos sin actividad** (teclado o ratón). Cualquier movimiento detiene la
rotación y mantiene el fondo actual; al volver a quedar inactivo, se reanuda.

> El fondo se muestra con una capa oscura para mantener legibles el reloj, el nombre del equipo y
> los mensajes. Al iniciar una sesión, la pantalla de bloqueo se oculta y la rotación se detiene.

---

## Videos promocionales

La consola incluye **Sistema → Videos promocionales** para reproducir publicidad o promociones en
los equipos que están bloqueados.

1. Pulse **Subir videos** y seleccione archivos **MP4, M4V, WMV o AVI** (máximo **300 MB** por
   video). Cada video se guarda como BLOB en la tabla `PromoVideos` de SQLite, igual que los
   fondos, por lo que no depende de carpetas compartidas.
2. Edite el **nombre**, el **orden** y el estado **activo** de cada video. Solo se reproducen los
   videos activos, en orden ascendente.
3. Use **Guardar y aplicar** o **Reenviar a terminales** para publicar la lista. Los clientes
   conectados reciben el evento `PromoVideosChanged` por SignalR y actualizan la cola sin
   reiniciarse.

Los videos se reproducen **silenciados** sobre la pantalla de bloqueo, en bucle continuo, y se
detienen al iniciar una sesión. Cada cliente descarga los archivos por la LAN con su token de
equipo y los cachea por hash SHA-256 en
`%LOCALAPPDATA%\CyberManager\Client\promos`; los archivos que ya no pertenecen a la lista activa se
eliminan para no acumular datos. Si un códec no está disponible en el equipo, el cliente omite ese
video y continúa con el siguiente.

> Para reducir el tamaño del servidor, suba videos ya comprimidos (H.264/MP4 es la opción más
> compatible). La base de datos crecerá con cada video; use la papelera de la galería para eliminar
> los que ya no necesite.

---

## Control remoto, Wake on LAN y NetFiltroWeb

### Control remoto integrado

En el tablero o en *Equipos* abra **Ver pantalla remota**. La ventana muestra la pantalla del
cliente en vivo (actualización automática cada 2 segundos o captura manual). Marque **Control
remoto** para enviar el mouse y el teclado de la consola al equipo:

- El mouse se envía con coordenadas relativas a la imagen, por lo que funciona con cualquier
  resolución o escala de Windows.
- Se admiten clic izquierdo, derecho, central, rueda, escritura y combinaciones con **Ctrl**,
  **Shift** y **Alt**.
- El cliente inyecta los eventos en su sesión interactiva mediante la API `SendInput` de Windows.
  No hace falta instalar software adicional ni abrir puertos nuevos: el tráfico viaja por SignalR
  a través del servidor.
- El control funciona aunque el equipo esté bloqueado o en la pantalla de inicio de sesión del
  cliente; para operar el escritorio de Windows con otra cuenta de usuario, use VNC o Escritorio
  remoto.

### VNC externo con TightVNC (opcional)

El control remoto integrado no requiere software adicional, pero no puede operar la pantalla
de inicio de sesión de Windows ni cambiar de cuenta. Para esos casos, CyberManager puede
instalar **TightVNC 2.8.90** como componente opcional.

**En cada estación**

1. Ejecute el instalador de estación y marque **Instalar TightVNC (servidor VNC)** en la
   sección **Control remoto (opcional)**.
2. Defina una contraseña de **4 a 8 caracteres** y el **puerto VNC** (1024–65535). La
   contraseña la guarda cifrada el instalador de TightVNC.
3. El instalador registra el servicio `tvnserver`, lo inicia y crea la regla de Firewall
   `CyberManager VNC (TightVNC)` limitada a **Dominio/Privado** y a la **subred local**.
4. El acceso directo **Configurar servidor VNC** abre la configuración de TightVNC y
   actualiza la regla de Firewall al puerto actual.

**En el servidor**

1. Ejecute el instalador del servidor y marque **Instalar el visor TightVNC**.
2. En *Configuración → Control remoto y filtro* puede dejar vacía la **ruta del visor**:
   la consola detecta `tvnviewer.exe` en `C:\Program Files\TightVNC` o
   `C:\Program Files (x86)\TightVNC`.
3. En la ventana de pantalla remota pulse **Abrir VNC**. Se ejecutará
   `tvnviewer.exe <IP del equipo>::<puerto>`.

La IP se toma de la ficha del equipo (*Equipos → Editar*), por lo que conviene asignar una IP
fija o una reserva DHCP. Si usa otro visor VNC, indique su ruta en la configuración y ajuste
el puerto.

**Seguridad y licencia**

- VNC clásico usa una contraseña de máximo 8 caracteres y cifrado débil. Use VNC solo en la
  red local y nunca exponga el puerto a Internet.
- TightVNC se distribuye como **programa separado bajo GPLv2**. Consulte
  [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md) y
  [`LICENSE-TightVNC-GPLv2.txt`](LICENSE-TightVNC-GPLv2.txt).
- La desinstalación solo quita TightVNC si lo instaló CyberManager y ningún otro componente
  lo sigue usando. El marcador compartido es
  `%ProgramData%\CyberManager\vnc-componentes.txt`.

### Wake on LAN

Permite encender un equipo apagado desde la consola:

1. En *Equipos → Editar* registre la **dirección MAC** del equipo. Se aceptan formatos como
   `AA:BB:CC:DD:EE:FF`, `AA-BB-CC-DD-EE-FF` o `AABBCCDDEEFF`.
2. En la ficha del equipo (o en el tablero cuando está fuera de línea) pulse **Encender equipo
   (WoL)**. El servidor envía el paquete mágico por UDP a `255.255.255.255` y al broadcast de la
   subred, en los puertos 9 y 7.
3. Active **Wake on LAN / Magic Packet** en el BIOS/UEFI y en las propiedades de la tarjeta de
   red. Si el equipo no enciende, desactive el **inicio rápido** de Windows y confirme que la
   tarjeta recibe energía en modo apagado.

El servidor debe estar en la misma red local (o tener una ruta hacia ella). El endpoint es
`POST /api/stations/{id}/wake`.

### NetFiltroWeb

Módulo para bloquear sitios de contenido no deseado. Se administra en
*Configuración → Control remoto y filtro → NetFiltroWeb*:

- **Lista inicial**: 24 reglas de fábrica (dominios y palabras clave) que se siembran una sola vez.
  **Restaurar lista de fábrica** agrega las reglas que falten sin duplicar las existentes.
- **Reglas de tipo sitio/dominio**: bloquean el dominio exacto y todos sus subdominios. Se escriben
  en el archivo `hosts` de Windows dentro de un bloque marcado
  (`# >>> CyberManager NetFiltroWeb >>>`), que el agente actualiza y puede retirar sin tocar el
  resto del archivo.
- **Reglas de tipo palabra clave**: bloquean cualquier sitio cuyo nombre de host contenga la
  palabra. Se aplican con un **proxy HTTP local** en `127.0.0.1:8877` (puerto configurable).
  Configure el navegador o el proxy del sistema para usar esa dirección. El proxy solo puede
  inspeccionar el nombre del host; en HTTPS no puede leer la ruta ni el contenido.
- Los equipos descargan la lista cada **60 segundos** y también al instante cuando el administrador
  guarda un cambio (evento SignalR `WebFilterChanged`). No hay que reiniciar el cliente.
- El agente debe ejecutarse como servicio con permisos de administrador (la cuenta `LocalSystem`
  predeterminada es suficiente) para poder escribir en `hosts` y abrir el puerto del proxy.
- Los cambios en `hosts` son inmediatos, pero los navegadores pueden conservar DNS en caché:
  ciérrelos y vuelva a abrirlos si un sitio sigue cargando.
- **Alcance**: el bloqueo por `hosts` no impide el acceso por dirección IP y el proxy no descifra
  HTTPS. NetFiltroWeb es una barrera de disuasión; para un filtrado exhaustivo combine el módulo
  con las reglas del router o un servidor DNS filtrado.

### Barra flotante del cliente

La barra de sesión muestra el nombre del equipo, el nombre del cliente, el tiempo de la sesión y
el monto a cancelar sobre el escritorio del cliente. En **prepago** el reloj es **regresivo**
(tiempo restante) y, al agotarse, sigue en positivo con el excedente; en **postpago** muestra el
tiempo transcurrido. El botón **▴** oculta la barra en un **icono flotante** con el tiempo y un
aviso cuando queda poco; al pulsarlo se restaura la barra completa. En
*Configuración → Control remoto y filtro* puede cambiar:

- **Descripción de la barra** (`SessionBarDescription`, opcional): texto extra bajo el nombre del
  equipo. Si se deja vacío no se muestra ningún renglón adicional.
- **Tamaño de la barra** (`SessionBarScale`): porcentaje entre **70 y 160**.

Los cambios se aplican en la siguiente actualización de estado, sin reiniciar los equipos. La
ventana es flotante, siempre visible y no roba el foco al usuario.

---

## Respaldo de tiempos del cliente

Para que un corte de energía o una caída del servidor no borre el tiempo de una sesión, el cliente
guarda una copia local del estado:

- Archivo: `%LOCALAPPDATA%\CyberManager\Client\session-backup.json`.
- Se escribe de forma **atómica** (archivo temporal y reemplazo) en cada consulta exitosa al
  servidor, para que el equipo pueda apagarse en cualquier momento sin dejar el archivo corrupto.
- Cuando no hay sesión activa, el respaldo se elimina.
- Al iniciar el cliente, si el servidor no responde y existe un respaldo del **mismo equipo**, se
  restaura la sesión y se avanza el tiempo transcurrido desde el último guardado, incluido el
  importe acumulado. La barra muestra «Sin conexión: tiempo restaurado desde el respaldo local».
- El respaldo es una red de seguridad local: cuando el servidor vuelve a responder, su estado es
  la fuente de verdad y reemplaza al respaldo. Si la sesión ya se cerró en el servidor, el siguiente
  estado correcto descarta la copia local.

---

## Scripts incluidos

| Script                          | Uso                                                                 |
|---------------------------------|---------------------------------------------------------------------|
| `scripts/run-dev.ps1`           | Compila, arranca el servidor, espera `/api/health` y abre la consola.|
| `scripts/build-installer.ps1`   | Publica x64/x86 y genera los instaladores unificados con Inno Setup. |
| `scripts/generate-icon.ps1`     | Genera el icono compartido `assets/CyberManager.ico`.               |
| `scripts/install-agent.ps1`     | Instala el agente como servicio de Windows.                        |
| `scripts/install-client-startup.ps1` | Instala el cliente y su inicio automático elevado.             |
| `scripts/import-stock.ps1`      | Importa o actualiza productos desde un CSV/HTML de stock.           |
| `scripts/sync-github.ps1`       | Sincroniza automáticamente el proyecto con GitHub.                  |
| `installer/scripts/configurar-cliente.ps1` | Registra o quita la tarea `CyberManagerClient` (inicio elevado). |
| `installer/scripts/configurar-agente.ps1`  | Configura el agente y su servicio; se auto-eleva si hace falta.  |
| `installer/scripts/configurar-servidor.ps1`| Registra el servidor y abre el puerto 5080 en el firewall.       |

`generate-icon.ps1` dibuja el icono en siete tamaños (16 a 256 px) y produce un `.ico` con
entradas BMP de 32 bits compatible con los ejecutables de .NET y con Inno Setup. Los cuatro
proyectos lo referencian con `<ApplicationIcon>` y ambos instaladores con `SetupIconFile`; si
cambia el diseño, vuelva a ejecutarlo y recompile.

`run-dev.ps1` acepta `-Port`, `-ResetDb` (borra la base y vuelve a sembrar), `-NoAdmin` y
`-SkipBuild`. Los registros del servidor se guardan en `artifacts/server.log` y
`artifacts/server.err.log`.

El servidor instalado (tarea `CyberManagerServer`) escribe sus registros en
`<instalación>\Server\Logs\server-AAAA-MM-DD.log`. Los archivos se abren en modo *append*, así que
**sobreviven a los reinicios**, y se eliminan automáticamente los que tienen más de **30 días**
(`Server:LogRetentionDays` en `appsettings.json`). Si un archivo supera 32 MB continúa en
`server-AAAA-MM-DD.1.log`, `.2`, etc. La ruta también queda anotada en la primera línea del
registro al arrancar.

`build-installer.ps1` acepta `-Version`, `-Architecture` (`all` por defecto, `x64` o `x86` para
publicaciones de diagnóstico), `-Configuration`, `-SkipPublish`, `-SkipInstaller`,
`-InstallInnoSetup` y `-Clean`. Publica en `dist\x64` y `dist\x86` y genera dos instaladores
unificados que eligen la arquitectura al instalar. La publicación elimina PDB, XML de
documentación y recursos satélite de otros idiomas para reducir el tamaño.

> Ejecute los scripts con `Set-ExecutionPolicy -Scope Process Bypass -Force` si la política de
> ejecución de PowerShell los bloquea.

---

## Sincronización automática con GitHub

`scripts/sync-github.ps1` hace `git add` de todos los cambios, crea un commit si hay algo nuevo,
integra los cambios del remoto con `rebase --autostash` y publica la rama `main`. El registro se
guarda en `%LOCALAPPDATA%\CyberManager\logs\github-sync.log`.

```powershell
# Sincronizar una vez (commit automático con fecha y hora)
.\scripts\sync-github.ps1

# Commit con un mensaje propio
.\scripts\sync-github.ps1 -Message "Agregar recordatorio de versiones"

# Instalar la tarea programada cada 15 minutos
.\scripts\sync-github.ps1 -InstallTask

# Cambiar el intervalo y quitarla
.\scripts\sync-github.ps1 -InstallTask -TaskMinutes 30
.\scripts\sync-github.ps1 -RemoveTask
```

La tarea **CyberManagerGitSync** se ejecuta con la sesión del usuario (no necesita administrador) y
requiere que GitHub CLI siga autenticado (`gh auth status`). Si no hay cambios pendientes no crea
commits vacíos. Use `-NoPush` para crear solo el commit local.

> La sincronización automática publica el estado actual del proyecto cada 15 minutos. Si prefiere
> revisar antes de publicar, no instale la tarea y ejecute el script manualmente.

---

## Firewall y red

El servidor crea la regla de Firewall automáticamente la primera vez que se inicia con permisos de
administrador. También puede crearla a mano en el PC servidor (PowerShell como administrador):

```powershell
netsh advfirewall firewall add rule name="CyberManager Server 5080" dir=in action=allow protocol=TCP localport=5080 profile=any
```

Las estaciones necesitan además el descubrimiento por UDP (el instalador de estación crea la
regla; en un equipo ya instalado puede crearla a mano):

```powershell
netsh advfirewall firewall add rule name="CyberManager Estación (descubrimiento)" dir=in action=allow protocol=UDP localport=5092-5093 profile=any
```

Compruebe la conectividad desde una estación:

```powershell
Invoke-RestMethod http://192.168.1.10:5080/api/health
# status: ok
```

- El servidor escucha en `0.0.0.0:5080`, por lo que acepta conexiones de la LAN.
- La primera ejecución interactiva solicita UAC y guarda el marcador
  `%ProgramData%\CyberManager\server-elevated.marker`. Si cancela el aviso, se volverá a solicitar
  en el siguiente inicio. La tarea programada `CyberManagerServer` corre como SYSTEM y no muestra
  avisos. Para iniciar sin UAC use `--no-elevate`; `run-dev.ps1` ya lo hace.
- La regla del instalador cubre todos los perfiles de red, incluido **Pública**, porque Windows
  suele clasificar así la red del local. Si prefiere restringirla, cambie el perfil de la red a
  **Privada** en Configuración → Red e Internet.
- No use `localhost` ni `127.0.0.1` en las estaciones: esos valores apuntan al propio PC.
- Si cambia el puerto, ajuste `Server:Port` en `src/CyberManager.Server/appsettings.json` y las
  URLs de servidor en la consola y en cada cliente.
- Para operar fuera de la LAN, publique el servidor detrás de una VPN; no exponga la API
  directamente a Internet.

---

## API y hubs en tiempo real

- **Autenticación de usuarios**: JWT emitido por `POST /api/auth/login`. Los endpoints de
  administración y caja requieren el token `Bearer`. Algunas operaciones (configuración, usuarios,
  compras) requieren el rol `Admin`.
- **Autenticación de estaciones**: cabeceras `X-Station-Id` y `X-Station-Token` enviadas por el
  cliente y el agente. Como compatibilidad, los equipos antiguos pueden autenticarse con
  `X-Station-Ip`/`X-Station-Mac` (o los parámetros `ipAddress`/`macAddress` del hub).
- **Hubs SignalR**:
  - `/hubs/admin` — eventos del tablero y de las sesiones para la consola (JWT); también recibe
    `SendRemoteInput` para reenviar mouse y teclado a una estación.
  - `/hubs/agent` — comandos, heartbeat y `WebFilterChanged` del servicio de Windows (token de
    equipo por query).
  - `/hubs/station` — estado, avisos, comandos, `RemoteInput` y `WebFilterChanged` del cliente
    (token de equipo por query).

Identificadores clave: `/api/health`, `/api/auth/login`, `/api/dashboard`, `/api/stations`,
`/api/stations/scan` (escaneo UDP de la red, solo rol `Admin`),
`/api/stations/scan/apply` (alta/actualización de los equipos encontrados, solo rol `Admin`),
`/api/client-update/manifest` y `/api/client-update/download` (paquete del cliente, anónimos),
`/api/client-update/status` y `/api/client-update/notify` (solo rol `Admin`),
`/api/stations/{id}/wake` (Wake on LAN, solo rol `Admin`), `/api/sessions/active`, `/api/sales`,
`/api/cash`, `/api/products`, `/api/inventory`,
`/api/purchases`, `/api/suppliers/balances`, `/api/suppliers/{id}/payments`,
`/api/supplier-payments`, `/api/reports/*`, `/api/reports/daily-closing`, `/api/settings`,
`/api/exchange-rates`, `/api/exchange-rates/bcv` (consulta forzada al BCV, solo rol `Admin`),
`/api/wallpapers` (galería, solo rol `Admin`),
`/api/promo-videos` (galería de videos, solo rol `Admin`), `/api/promo-videos/playlist`,
`/api/promo-videos/{id}/file`, `/api/promo-videos/refresh`,
`/api/station/promo-videos/{id}/file` (descarga con token de equipo),
`/api/webfilter` (CRUD de NetFiltroWeb, solo rol `Admin`), `/api/webfilter/restore`,
`/api/station/webfilter` (lista activa con token de equipo), `/api/station/state` (estado y barra
flotante con token de equipo), `/api/backups` (respaldos, solo rol `Admin`),
`/api/stations/{id}/config` (devuelve la IP LAN real del servidor) y
`DELETE /api/users/{id}/permanent` (eliminación definitiva de una cuenta, solo rol `Admin`).

---

## Base de datos

- Motor: **SQLite** mediante EF Core.
- Archivo: `cybermanager.db` en la misma carpeta del ejecutable del servidor. En desarrollo:
  `src/CyberManager.Server/bin/Debug/net8.0/cybermanager.db`.
- El esquema se crea con `EnsureCreated` y los datos de demostración se cargan solo si las tablas
  están vacías.
- **Reiniciar los datos de demostración**: detenga el servidor, elimine el archivo `.db` y vuelva
  a arrancar. También puede usar `.\scripts\run-dev.ps1 -ResetDb`.
- **Copias de seguridad**: use *Configuración → Respaldos* para crear, exportar o restaurar copias
  con el servidor en marcha. Los respaldos se guardan en `respaldos`, junto a la base. También
  puede detener el servicio y copiar el archivo `.db` manualmente.

---

## Seguridad recomendada

1. Cambie las contraseñas de demostración y cree usuarios individuales por empleado.
2. Cambie `Jwt:Key` en `appsettings.json` por una clave aleatoria de al menos 32 caracteres.
3. Mantenga la tasa del día actualizada antes de abrir la caja.
4. Revise el arqueo de caja al cerrar cada turno.
5. Use una cuenta de Windows estándar para el cliente y limite el acceso físico al PC servidor.
6. Regenere el token de un equipo si sospecha que se filtró.
7. No exponga el puerto 5080 a Internet; use VPN para acceso remoto.
8. Mantenga activo el respaldo automático y exporte copias periódicas fuera del PC servidor.

---

## Solución de problemas

**La consola no conecta con el servidor**

- Verifique que el servidor esté ejecutándose (`/api/health`).
- Use la IP LAN del servidor, no `localhost`, desde otras máquinas.
- Revise el firewall del PC servidor (puerto 5080/TCP).
- Confirme que ambos equipos estén en la misma red y que no haya aislamiento de clientes en el
  router o Wi-Fi.

**El cliente dice «No se pudo conectar»**

- Confirme el ID y el token del equipo en *Equipos → Configuración / token*.
- Pruebe la URL desde el navegador del PC: `http://IP-DEL-SERVIDOR:5080/api/health`.
- Si regeneró el token, actualice la configuración del cliente.

**No se puede abrir la caja**

- Debe existir una tasa del día mayor que cero en *Configuración → Tasa de cambio*.
- Solo un turno de caja puede estar abierto a la vez por terminal.

**No se puede iniciar una sesión prepago o cerrar con consumos**

- Las sesiones prepago iniciadas desde el autoservicio no requieren caja abierta: el tiempo se
  paga con el saldo de la cuenta y los consumos pendientes se descuentan de ese mismo saldo.
- Las sesiones iniciadas desde la consola con cobro en efectivo, tarjeta, transferencia o Pago
  Móvil sí requieren una caja abierta. Abra la caja primero.
- Si el cierre falla por saldo insuficiente, recargue la cuenta en caja antes de finalizar.

**El autoservicio prepago no aparece en el cliente**

- El cliente debe estar conectado al servidor: compruebe que `/api/station/prepaid/info` responde
  y que la estación tiene ID y token válidos.
- Si no hay conexión, la pantalla de bloqueo muestra el aviso y el botón de inicio de sesión no
  puede cargar las cuentas ni los paquetes.

**La compra de un paquete dice «Saldo insuficiente»**

- El saldo de la cuenta prepago se recarga en caja; los paquetes se pagan con ese saldo, no con
  los minutos del monedero.
- Verifique que el cliente tenga saldo suficiente para el paquete elegido.

**El agente no ejecuta órdenes**

- Compruebe el estado del servicio: `Get-Service CyberManagerAgent`.
- Revise el visor de eventos de Windows (origen `CyberManagerAgent`) y el `appsettings.json` del
  agente: `C:\CyberManager\Estacion\Agent\appsettings.json` (instalador) o
  `C:\CyberManager\Agent\appsettings.json` (script manual).
- Verifique que el `StationId` y el `Token` coincidan con el equipo registrado.
- El servicio debe ejecutarse con permisos suficientes para apagar o reiniciar el equipo.

**El control remoto no mueve el mouse ni escribe**

- Confirme que la casilla **Control remoto** esté marcada en la ventana de pantalla remota.
- El cliente WPF debe estar ejecutándose en la **sesión interactiva** del usuario (no como
  servicio) y el equipo debe aparecer conectado en el tablero.
- Si la consola perdió la conexión SignalR, ciérrela y vuelva a abrirla.
- Para escribir en la pantalla de inicio de sesión de Windows o con otra cuenta, use VNC o
  Escritorio remoto.

**El equipo no enciende con Wake on LAN**

- Verifique la MAC en *Equipos → Editar* y que el servidor esté en la misma subred.
- Active **Magic Packet** en el BIOS/UEFI y en las propiedades de la tarjeta de red.
- Desactive el **inicio rápido** de Windows y confirme que la tarjeta de red mantiene energía en
  modo apagado.
- Algunos switches o repetidores Wi-Fi descartan los paquetes de broadcast; pruebe con cable.

**NetFiltroWeb no bloquea un sitio**

- Compruebe que el servicio del agente esté activo y ejecutándose como administrador/SYSTEM.
- Revise el bloque marcado de CyberManager en `C:\Windows\System32\drivers\etc\hosts`.
- Cierre y vuelva a abrir el navegador para descartar la caché de DNS.
- Las **palabras clave** requieren configurar el navegador o el proxy del sistema en
  `127.0.0.1:8877` (o el puerto elegido). El bloqueo por dominio no necesita el proxy.
- Recuerde que `hosts` no bloquea el acceso por IP y que el proxy no puede leer rutas HTTPS.

**La sesión del cliente no se restaura tras un corte**

- El respaldo se guarda en `%LOCALAPPDATA%\CyberManager\Client\session-backup.json` y solo se
  restaura si pertenece al mismo ID de equipo.
- Si el servidor responde al iniciar, su estado reemplaza al respaldo; la restauración es solo para
  cortes de energía o caídas del servidor.
- Si la sesión ya se había cerrado en el servidor, el respaldo se descarta en la siguiente
  consulta exitosa.

**El cliente no inicia con Windows**

- Confirme que el acceso directo exista en
  `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup` o en el inicio común.
- Revise que la cuenta de Windows no tenga políticas que bloqueen el inicio automático.

**El servidor instalado no arranca**

- Revise la tarea programada: `Get-ScheduledTask CyberManagerServer` y
  `Get-ScheduledTaskInfo CyberManagerServer`.
- Iníciela manualmente con `Start-ScheduledTask CyberManagerServer`.
- Ejecute `C:\CyberManager\Servidor\Server\CyberManager.Server.exe` desde una consola para ver
  el error.
- Confirme que el puerto 5080 esté libre y que la regla de firewall exista
  (`Get-NetFirewallRule -DisplayName "CyberManager Server 5080"`).
- La base de datos y sus permisos deben estar disponibles para la cuenta SYSTEM.

**Cambié la tasa y los reportes antiguos muestran otro valor**

- Es el comportamiento esperado: cada venta conserva la tasa con la que se cobró. Los montos en
  USD son la referencia contable.

---

## Licencia

Este proyecto se distribuye bajo la licencia **MIT**. Puede usarlo, modificarlo y redistribuirlo,
incluso comercialmente, conservando el aviso de copyright. Consulte el archivo
[`LICENSE`](LICENSE) para el texto completo.

Los instaladores pueden incluir **TightVNC 2.8.90** como programa separado bajo **GPLv2**.
Consulte [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md) y
[`LICENSE-TightVNC-GPLv2.txt`](LICENSE-TightVNC-GPLv2.txt) para los avisos, hashes y enlaces
al código fuente.
---

