; Instalador unificado de CyberManager: en una sola pantalla el usuario elige si
; instala el servidor (equipo principal), la estación (cliente / caja) o ambos.
; Reutiliza los mismos payloads, scripts y tareas que los instaladores separados.
#define MyAppName "CyberManager"
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#define MyDistX64 "..\dist\x64"
#define MyDistX86 "..\dist\x86"
#define MyAppPublisher "CyberManager"
#define MyAppURL "http://127.0.0.1:5080"

; Identificadores de las instalaciones separadas anteriores. Si existen, el
; instalador unificado las desinstala en silencio para no duplicar archivos ni
; entradas en "Aplicaciones instaladas". La base de datos y la configuración local
; se conservan.
#define OldServerAppId "{A3B5D8E1-7C42-4F19-9A6B-2D8E5F1C7A31}"
#define OldStationAppId "{C9D2E4F6-1A83-4B57-8C0D-6E3F9A2B5D74}"

[Setup]
AppId={{E7F1C3A9-5B24-4D68-9F13-8C6A2D4B7E95}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
DefaultDirName=C:\CyberManager
DefaultGroupName=CyberManager
DisableProgramGroupPage=yes
OutputDir=..\artifacts
OutputBaseFilename=CyberManager-Unificado-{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; Un solo instalador para x86 y x64. En equipos de 64 bits se activa el modo de
; instalación de 64 bits y se copia el payload de dist\x64; en equipos de 32 bits
; se copia dist\x86.
ArchitecturesAllowed=x86compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UninstallDisplayIcon={app}\CyberManager.ico
SetupIconFile=..\assets\CyberManager.ico
SetupLogging=yes
; El instalador detiene y reinicia los procesos con su propio código para pedir
; confirmación antes de interrumpir una operación en curso.
CloseApplications=no

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
spanish.WizardSelectComponents=¿Qué desea instalar?
spanish.SelectComponentsDesc=Elija el servidor (equipo principal con la base de datos), la estación (PC de caja o de juego) o ambos en este mismo equipo.

[CustomMessages]
spanish.RoleServer=Servidor (equipo principal con la base de datos)
english.RoleServer=Server (main computer with the database)
spanish.RoleStation=Estación / cliente (PC de caja o de juego)
english.RoleStation=Station / client (cashier or gaming PC)
spanish.RoleBoth=Servidor y estación en este mismo equipo
english.RoleBoth=Server and station on this same computer
spanish.ServerComponent=Servidor: API, base de datos y consola de administración
english.ServerComponent=Server: API, database and administration console
spanish.StationComponent=Estación: cliente, agente de Windows y configuración de la PC
english.StationComponent=Station: client, Windows agent and PC configuration
spanish.CreateServerDesktopIcon=Crear acceso directo del servidor en el escritorio
english.CreateServerDesktopIcon=Create a server shortcut on the desktop
spanish.CreateClientDesktopIcon=Crear acceso directo del cliente en el escritorio
english.CreateClientDesktopIcon=Create a client shortcut on the desktop
spanish.ShortcutsGroup=Accesos directos:
english.ShortcutsGroup=Shortcuts:
spanish.StartupGroup=Inicio automático:
english.StartupGroup=Automatic startup:
spanish.StartupAdmin=Abrir la consola al iniciar sesión
english.StartupAdmin=Open the console at sign-in
spanish.ServerStartup=Iniciar el servidor al encender el equipo (tarea programada)
english.ServerStartup=Start the server when the computer boots (scheduled task)
spanish.StartupClient=Iniciar el cliente con Windows
english.StartupClient=Start the client with Windows
spanish.FirewallGroup=Red:
english.FirewallGroup=Network:
spanish.Firewall=Permitir el puerto 5080 en el Firewall de Windows
english.Firewall=Allow port 5080 through Windows Firewall
spanish.OpenConsole=Abrir la consola de administración
english.OpenConsole=Open the administration console
spanish.StartServer=Iniciar el servidor ahora
english.StartServer=Start the server now
spanish.OpenClient=Abrir CyberManager Cliente
english.OpenClient=Open CyberManager Client
spanish.Configuring=Configurando el servidor...
english.Configuring=Configuring the server...
spanish.ConfigureAgent=Configurar el agente de esta estación (ID y token)
english.ConfigureAgent=Configure this station's agent (ID and token)
spanish.OpeningAgent=Abriendo configuración del agente...
english.OpeningAgent=Opening agent configuration...
spanish.InstallingArch64=Se instalará la versión de 64 bits, adecuada para este equipo.
english.InstallingArch64=The 64-bit version will be installed, which matches this computer.
spanish.InstallingArch32=Se instalará la versión de 32 bits, adecuada para este equipo.
english.InstallingArch32=The 32-bit version will be installed, which matches this computer.
spanish.CloseRunningPrompt=CyberManager está en ejecución.
english.CloseRunningPrompt=CyberManager is running.
spanish.CloseRunningHint=Para actualizar es necesario cerrar los procesos de CyberManager. ¿Desea cerrarlos ahora?
english.CloseRunningHint=The CyberManager processes must be closed to update. Do you want to close them now?
spanish.InstallCancelled=La instalación se canceló para no interrumpir la operación en curso.
english.InstallCancelled=The installation was cancelled to avoid interrupting the current operation.
spanish.UninstallAll=Desinstalar todo CyberManager
english.UninstallAll=Uninstall all CyberManager
spanish.RegisteringClient=Registrando el inicio elevado del cliente...
english.RegisteringClient=Registering the elevated client startup...
spanish.RemoteGroup=Control remoto (opcional):
english.RemoteGroup=Remote control (optional):
spanish.InstallVncViewer=Instalar el visor TightVNC (GPLv2) para abrir VNC desde la consola
english.InstallVncViewer=Install the TightVNC viewer (GPLv2) to open VNC from the console
spanish.InstallingVncViewer=Instalando el visor TightVNC...
english.InstallingVncViewer=Installing the TightVNC viewer...
spanish.InstallVncServer=Instalar TightVNC (servidor VNC) para control remoto
english.InstallVncServer=Install TightVNC (VNC server) for remote control
spanish.VncPageTitle=Servidor VNC (TightVNC)
english.VncPageTitle=VNC server (TightVNC)
spanish.VncPageDescription=Defina la contraseña y el puerto del servidor VNC. El acceso se limita a la red local.
english.VncPageDescription=Set the VNC server password and port. Access is limited to the local network.
spanish.VncPageSubtitle=La contraseña admite de 4 a 8 caracteres (limitación de VNC). El puerto debe estar entre 1024 y 65535. TightVNC se distribuye como programa separado bajo GPLv2; consulte THIRD-PARTY-NOTICES.md y LICENSE-TightVNC-GPLv2.txt en la carpeta de instalación.
english.VncPageSubtitle=The password allows 4 to 8 characters (VNC limitation). The port must be between 1024 and 65535. TightVNC is distributed as a separate program under GPLv2; see THIRD-PARTY-NOTICES.md and LICENSE-TightVNC-GPLv2.txt in the installation folder.
spanish.VncPassword=Contraseña VNC
english.VncPassword=VNC password
spanish.VncPort=Puerto VNC
english.VncPort=VNC port
spanish.VncPasswordInvalid=La contraseña VNC debe tener entre 4 y 8 caracteres.
english.VncPasswordInvalid=The VNC password must be 4 to 8 characters long.
spanish.VncPasswordAscii=Use solo letras, números y símbolos sin espacios ni comillas (ASCII) en la contraseña VNC.
english.VncPasswordAscii=Use only letters, numbers and symbols without spaces or quotes (ASCII) in the VNC password.
spanish.VncPortInvalid=El puerto VNC debe ser un número entre 1024 y 65535.
english.VncPortInvalid=The VNC port must be a number between 1024 and 65535.
spanish.InstallingVnc=Instalando y configurando TightVNC...
english.InstallingVnc=Installing and configuring TightVNC...
spanish.VncConfigShortcut=Configurar servidor VNC
english.VncConfigShortcut=Configure VNC server

[Types]
Name: "servidor"; Description: "{cm:RoleServer}"
Name: "estacion"; Description: "{cm:RoleStation}"
Name: "completo"; Description: "{cm:RoleBoth}"

[Components]
Name: "servidor"; Description: "{cm:ServerComponent}"; Types: servidor completo
Name: "estacion"; Description: "{cm:StationComponent}"; Types: estacion completo

[Tasks]
Name: "desktopicon_server"; Description: "{cm:CreateServerDesktopIcon}"; GroupDescription: "{cm:ShortcutsGroup}"; Components: servidor
Name: "desktopicon_station"; Description: "{cm:CreateClientDesktopIcon}"; GroupDescription: "{cm:ShortcutsGroup}"; Components: estacion
Name: "startupadmin"; Description: "{cm:StartupAdmin}"; GroupDescription: "{cm:StartupGroup}"; Components: servidor; Flags: unchecked
Name: "serverstartup"; Description: "{cm:ServerStartup}"; GroupDescription: "{cm:StartupGroup}"; Components: servidor
Name: "firewall"; Description: "{cm:Firewall}"; GroupDescription: "{cm:FirewallGroup}"; Components: servidor
Name: "vncviewer"; Description: "{cm:InstallVncViewer}"; GroupDescription: "{cm:RemoteGroup}"; Components: servidor; Flags: unchecked
Name: "startupclient"; Description: "{cm:StartupClient}"; GroupDescription: "{cm:StartupGroup}"; Components: estacion
Name: "vncserver"; Description: "{cm:InstallVncServer}"; GroupDescription: "{cm:RemoteGroup}"; Components: estacion; Flags: unchecked

[Files]
; ---------- Servidor ----------
Source: "{#MyDistX64}\Server\*"; DestDir: "{app}\Servidor\Server"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: servidor; Check: IsX64InstallMode
Source: "{#MyDistX86}\Server\*"; DestDir: "{app}\Servidor\Server"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: servidor; Check: IsX86InstallMode
Source: "{#MyDistX64}\Admin\*"; DestDir: "{app}\Servidor\Admin"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: servidor; Check: IsX64InstallMode
Source: "{#MyDistX86}\Admin\*"; DestDir: "{app}\Servidor\Admin"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: servidor; Check: IsX86InstallMode
Source: "{#MyDistX64}\Scripts\*"; DestDir: "{app}\Servidor\scripts"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: servidor; Check: IsX64InstallMode
Source: "{#MyDistX86}\Scripts\*"; DestDir: "{app}\Servidor\scripts"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: servidor; Check: IsX86InstallMode
; Lanzador único: el mismo apphost del servidor, con otro nombre, inicia el
; servidor en segundo plano y abre la consola de administración.
Source: "{#MyDistX64}\Server\CyberManager.Server.exe"; DestDir: "{app}\Servidor\Server"; DestName: "CyberManager.exe"; Flags: ignoreversion; Components: servidor; Check: IsX64InstallMode
Source: "{#MyDistX86}\Server\CyberManager.Server.exe"; DestDir: "{app}\Servidor\Server"; DestName: "CyberManager.exe"; Flags: ignoreversion; Components: servidor; Check: IsX86InstallMode
; ---------- Estación ----------
Source: "{#MyDistX64}\Client\*"; DestDir: "{app}\Estacion\Client"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: estacion; Check: IsX64InstallMode
Source: "{#MyDistX86}\Client\*"; DestDir: "{app}\Estacion\Client"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: estacion; Check: IsX86InstallMode
Source: "{#MyDistX64}\Agent\*"; DestDir: "{app}\Estacion\Agent"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: estacion; Check: IsX64InstallMode
Source: "{#MyDistX86}\Agent\*"; DestDir: "{app}\Estacion\Agent"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: estacion; Check: IsX86InstallMode
Source: "{#MyDistX64}\Scripts\*"; DestDir: "{app}\Estacion\scripts"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: estacion; Check: IsX64InstallMode
Source: "{#MyDistX86}\Scripts\*"; DestDir: "{app}\Estacion\scripts"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: estacion; Check: IsX86InstallMode
; ---------- Comunes ----------
Source: "..\assets\CyberManager.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyDistX64}\README.md"; DestDir: "{app}"; Flags: ignoreversion isreadme; Check: IsX64InstallMode
Source: "{#MyDistX86}\README.md"; DestDir: "{app}"; Flags: ignoreversion isreadme; Check: IsX86InstallMode
; Desinstalador independiente compartido por el servidor y la estación.
; uninsneveruninstall evita que el desinstalador de un componente borre el del otro.
Source: "{#MyDistX64}\Uninstaller\*"; DestDir: "{commonappdata}\CyberManager"; Flags: ignoreversion recursesubdirs createallsubdirs uninsneveruninstall; Check: IsX64InstallMode
Source: "{#MyDistX86}\Uninstaller\*"; DestDir: "{commonappdata}\CyberManager"; Flags: ignoreversion recursesubdirs createallsubdirs uninsneveruninstall; Check: IsX86InstallMode
; TightVNC se distribuye como programa separado (GPLv2). El servidor solo instala
; el visor; la estación instala el servidor VNC si se marca la tarea.
Source: "vnc\tightvnc-2.8.90-gpl-setup-64bit.msi"; DestDir: "{app}\Servidor\vnc"; Flags: ignoreversion; Components: servidor; Tasks: vncviewer; Check: IsX64InstallMode
Source: "vnc\tightvnc-2.8.90-gpl-setup-32bit.msi"; DestDir: "{app}\Servidor\vnc"; Flags: ignoreversion; Components: servidor; Tasks: vncviewer; Check: IsX86InstallMode
Source: "vnc\tightvnc-2.8.90-gpl-setup-64bit.msi"; DestDir: "{app}\Estacion\vnc"; Flags: ignoreversion; Components: estacion; Tasks: vncserver; Check: IsX64InstallMode
Source: "vnc\tightvnc-2.8.90-gpl-setup-32bit.msi"; DestDir: "{app}\Estacion\vnc"; Flags: ignoreversion; Components: estacion; Tasks: vncserver; Check: IsX86InstallMode
Source: "..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion; Tasks: vncviewer or vncserver
Source: "..\LICENSE-TightVNC-GPLv2.txt"; DestDir: "{app}"; Flags: ignoreversion; Tasks: vncviewer or vncserver

[InstallDelete]
; Al reinstalar con la otra arquitectura, elimina los binarios nativos que no
; pertenecen al payload seleccionado. Los datos del negocio no se tocan.
Type: files; Name: "{app}\Servidor\Server\Microsoft.DiaSymReader.Native.x86.dll"; Components: servidor; Check: IsX64InstallMode
Type: files; Name: "{app}\Servidor\Server\mscordaccore_x86_*.dll"; Components: servidor; Check: IsX64InstallMode
Type: files; Name: "{app}\Servidor\Admin\Microsoft.DiaSymReader.Native.x86.dll"; Components: servidor; Check: IsX64InstallMode
Type: files; Name: "{app}\Servidor\Admin\mscordaccore_x86_*.dll"; Components: servidor; Check: IsX64InstallMode
Type: files; Name: "{app}\Servidor\Server\Microsoft.DiaSymReader.Native.amd64.dll"; Components: servidor; Check: IsX86InstallMode
Type: files; Name: "{app}\Servidor\Server\mscordaccore_amd64_*.dll"; Components: servidor; Check: IsX86InstallMode
Type: files; Name: "{app}\Servidor\Admin\Microsoft.DiaSymReader.Native.amd64.dll"; Components: servidor; Check: IsX86InstallMode
Type: files; Name: "{app}\Servidor\Admin\mscordaccore_amd64_*.dll"; Components: servidor; Check: IsX86InstallMode
Type: files; Name: "{app}\Estacion\Client\Microsoft.DiaSymReader.Native.x86.dll"; Components: estacion; Check: IsX64InstallMode
Type: files; Name: "{app}\Estacion\Client\mscordaccore_x86_*.dll"; Components: estacion; Check: IsX64InstallMode
Type: files; Name: "{app}\Estacion\Agent\Microsoft.DiaSymReader.Native.x86.dll"; Components: estacion; Check: IsX64InstallMode
Type: files; Name: "{app}\Estacion\Agent\mscordaccore_x86_*.dll"; Components: estacion; Check: IsX64InstallMode
Type: files; Name: "{app}\Estacion\Client\Microsoft.DiaSymReader.Native.amd64.dll"; Components: estacion; Check: IsX86InstallMode
Type: files; Name: "{app}\Estacion\Client\mscordaccore_amd64_*.dll"; Components: estacion; Check: IsX86InstallMode
Type: files; Name: "{app}\Estacion\Agent\Microsoft.DiaSymReader.Native.amd64.dll"; Components: estacion; Check: IsX86InstallMode
Type: files; Name: "{app}\Estacion\Agent\mscordaccore_amd64_*.dll"; Components: estacion; Check: IsX86InstallMode
; El cliente ya no se inicia desde la carpeta Inicio: ahora lo hace una tarea
; programada con permisos elevados. Se quita el acceso directo de versiones anteriores.
Type: files; Name: "{commonstartup}\CyberManager Cliente.lnk"; Components: estacion

[Icons]
Name: "{group}\CyberManager"; Filename: "{app}\Servidor\Server\CyberManager.exe"; Parameters: "--launch-console"; WorkingDir: "{app}\Servidor\Server"; Components: servidor
Name: "{group}\Consola de administración"; Filename: "{app}\Servidor\Admin\CyberManager.Admin.exe"; WorkingDir: "{app}\Servidor\Admin"; Components: servidor
Name: "{group}\Servidor (solo consola)"; Filename: "{app}\Servidor\Server\CyberManager.Server.exe"; WorkingDir: "{app}\Servidor\Server"; Components: servidor
Name: "{group}\CyberManager Cliente"; Filename: "{app}\Estacion\Client\CyberManager.Client.exe"; WorkingDir: "{app}\Estacion\Client"; Components: estacion
Name: "{group}\Configurar agente de estación"; Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoExit -ExecutionPolicy Bypass -File ""{app}\Estacion\scripts\configurar-agente.ps1"""; Components: estacion
Name: "{group}\{cm:VncConfigShortcut}"; Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\Estacion\scripts\configurar-vnc.ps1"""; WorkingDir: "{app}\Estacion"; Components: estacion; Check: ShouldCreateVncShortcut
Name: "{group}\Carpeta de instalación"; Filename: "{app}"
Name: "{group}\{cm:UninstallAll}"; Filename: "{commonappdata}\CyberManager\CyberManager.Uninstaller.exe"
Name: "{group}\Desinstalar CyberManager"; Filename: "{uninstallexe}"
Name: "{autodesktop}\CyberManager"; Filename: "{app}\Servidor\Server\CyberManager.exe"; Parameters: "--launch-console"; WorkingDir: "{app}\Servidor\Server"; Tasks: desktopicon_server
Name: "{autodesktop}\CyberManager Cliente"; Filename: "{app}\Estacion\Client\CyberManager.Client.exe"; WorkingDir: "{app}\Estacion\Client"; Tasks: desktopicon_station
Name: "{commonstartup}\CyberManager"; Filename: "{app}\Servidor\Server\CyberManager.exe"; Parameters: "--launch-console"; WorkingDir: "{app}\Servidor\Server"; Tasks: startupadmin

[Run]
; ---------- Servidor ----------
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\Servidor\scripts\configurar-servidor.ps1"" -RegistrarTarea -AbrirFirewall"; StatusMsg: "{cm:Configuring}"; Flags: runhidden waituntilterminated; Components: servidor; Tasks: serverstartup or firewall
; Visor TightVNC opcional: solo se instala si se marca la tarea de control remoto.
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\Servidor\scripts\instalar-vnc.ps1"" -Action Install -Mode Viewer -Component servidor -MsiPath ""{app}\Servidor\vnc\tightvnc-2.8.90-gpl-setup-64bit.msi"""; StatusMsg: "{cm:InstallingVncViewer}"; Flags: runhidden waituntilterminated; Components: servidor; Tasks: vncviewer; Check: IsX64InstallMode
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\Servidor\scripts\instalar-vnc.ps1"" -Action Install -Mode Viewer -Component servidor -MsiPath ""{app}\Servidor\vnc\tightvnc-2.8.90-gpl-setup-32bit.msi"""; StatusMsg: "{cm:InstallingVncViewer}"; Flags: runhidden waituntilterminated; Components: servidor; Tasks: vncviewer; Check: IsX86InstallMode
; Casilla de la página final: deja el servidor activo sin abrir la consola.
Filename: "{app}\Servidor\Server\CyberManager.exe"; Parameters: "--start-server"; Description: "{cm:StartServer}"; WorkingDir: "{app}\Servidor\Server"; Flags: runhidden waituntilterminated postinstall skipifsilent; Components: servidor; Check: ShouldStartServer
Filename: "{app}\Servidor\Server\CyberManager.exe"; Parameters: "--launch-console"; Description: "{cm:OpenConsole}"; WorkingDir: "{app}\Servidor\Server"; Flags: nowait postinstall skipifsilent runasoriginaluser; Components: servidor; Check: ShouldOpenConsole
; ---------- Estación ----------
; Instala TightVNC en modo silencioso si se marcó la tarea de control remoto. La
; contraseña se pasa en un archivo temporal para no exponerla en la línea de comandos.
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\Estacion\scripts\instalar-vnc.ps1"" -Action Install -Mode Server -Component estacion -Port {code:GetVncPort} -PasswordFile ""{tmp}\cybermanager-vnc.pwd"" -MsiPath ""{app}\Estacion\vnc\tightvnc-2.8.90-gpl-setup-64bit.msi"""; StatusMsg: "{cm:InstallingVnc}"; Flags: runhidden waituntilterminated; Components: estacion; Check: ShouldInstallVncServer and IsX64InstallMode
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\Estacion\scripts\instalar-vnc.ps1"" -Action Install -Mode Server -Component estacion -Port {code:GetVncPort} -PasswordFile ""{tmp}\cybermanager-vnc.pwd"" -MsiPath ""{app}\Estacion\vnc\tightvnc-2.8.90-gpl-setup-32bit.msi"""; StatusMsg: "{cm:InstallingVnc}"; Flags: runhidden waituntilterminated; Components: estacion; Check: ShouldInstallVncServer and IsX86InstallMode
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoExit -ExecutionPolicy Bypass -File ""{app}\Estacion\scripts\configurar-agente.ps1"""; Description: "{cm:ConfigureAgent}"; StatusMsg: "{cm:OpeningAgent}"; Flags: postinstall nowait skipifsilent; Components: estacion
; Inicio elevado: registra la tarea programada que abre el cliente con permisos de
; administrador sin mostrar el UAC en cada inicio de sesión.
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\Estacion\scripts\configurar-cliente.ps1"""; StatusMsg: "{cm:RegisteringClient}"; Flags: runhidden waituntilterminated; Components: estacion; Tasks: startupclient
; Descubrimiento de red: el servidor localiza la estación y el agente por UDP
; (puertos 5092 y 5093) para asignarles ID y token sin configurarlos a mano.
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -Command ""$n='CyberManager Estación (descubrimiento)'; Remove-NetFirewallRule -DisplayName $n -ErrorAction SilentlyContinue; New-NetFirewallRule -DisplayName $n -Direction Inbound -Action Allow -Protocol UDP -LocalPort 5092,5093 -Profile Any | Out-Null"""; StatusMsg: "Configurando el firewall para el descubrimiento..."; Flags: runhidden waituntilterminated; Components: estacion
Filename: "{sys}\schtasks.exe"; Parameters: "/Run /TN ""CyberManagerClient"""; Description: "{cm:OpenClient}"; Flags: runhidden nowait postinstall skipifsilent; Components: estacion; Check: ShouldOpenClientTask
Filename: "{app}\Estacion\Client\CyberManager.Client.exe"; Description: "{cm:OpenClient}"; WorkingDir: "{app}\Estacion\Client"; Flags: nowait postinstall skipifsilent; Components: estacion; Check: ShouldOpenClientDirect

[UninstallRun]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\Estacion\scripts\configurar-cliente.ps1"" -Remove"; Flags: runhidden waituntilterminated; RunOnceId: "QuitarTareaCliente"; Components: estacion
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\Servidor\scripts\desinstalar-servicios.ps1"" -Servidor"; Flags: runhidden waituntilterminated; RunOnceId: "DetenerServidorCyberManager"; Components: servidor
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\Estacion\scripts\desinstalar-servicios.ps1"" -Agente"; Flags: runhidden waituntilterminated; RunOnceId: "DetenerAgenteCyberManager"; Components: estacion

[UninstallDelete]
Type: filesandordirs; Name: "{app}\Servidor\Server\logs"; Components: servidor
Type: files; Name: "{app}\Servidor\Server\cybermanager.db-shm"; Components: servidor
Type: files; Name: "{app}\Servidor\Server\cybermanager.db-wal"; Components: servidor

[Code]
var
  ServerWasRunning: Boolean;
  ConsoleWasRunning: Boolean;
  ClientWasRunning: Boolean;
  AgentWasRunning: Boolean;
  VncPage: TInputQueryWizardPage;
  VncPassword: String;
  VncPort: Integer;

function IsX64InstallMode(): Boolean;
begin
  Result := Is64BitInstallMode;
end;

function IsX86InstallMode(): Boolean;
begin
  Result := not Is64BitInstallMode;
end;

function IsServerSelected(): Boolean;
begin
  Result := WizardIsComponentSelected('servidor');
end;

function IsStationSelected(): Boolean;
begin
  Result := WizardIsComponentSelected('estacion');
end;

// Comprueba si un proceso está activo filtrando la salida de tasklist con find.
function IsProcessRunning(const ImageName: String): Boolean;
var
  ResultCode: Integer;
begin
  // find devuelve 0 cuando la imagen aparece en la salida de tasklist.
  Exec(ExpandConstant('{sys}\cmd.exe'),
    '/c tasklist /FI "IMAGENAME eq ' + ImageName + '" /NH | find /I "' + ImageName + '" > nul',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := ResultCode = 0;
end;

procedure StopProcess(const ImageName: String);
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM "' + ImageName + '"',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure StopScheduledTask;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\schtasks.exe'), '/End /TN "CyberManagerServer"',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function HasScheduledTask(): Boolean;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\schtasks.exe'), '/Query /TN "CyberManagerServer"',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := ResultCode = 0;
end;

procedure StartScheduledTask;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\schtasks.exe'), '/Run /TN "CyberManagerServer"',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure StopAgentService;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\sc.exe'), 'stop CyberManagerAgent',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure StartAgentService;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\sc.exe'), 'start CyberManagerAgent',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

// Desinstala en silencio una instalación anterior de los instaladores separados.
// Así no quedan dos entradas en "Aplicaciones instaladas" ni archivos duplicados.
// El desinstalador no borra la base de datos ni la configuración local.
procedure RemoveOldInstall(const AppId: String);
var
  Key, Cmd, Exe: String;
  ResultCode: Integer;
begin
  Key := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\' + AppId + '_is1';
  Cmd := '';

  if Is64BitInstallMode then
    RegQueryStringValue(HKLM64, Key, 'UninstallString', Cmd);
  if Cmd = '' then
    RegQueryStringValue(HKLM, Key, 'UninstallString', Cmd);
  if Cmd = '' then Exit;

  Exe := RemoveQuotes(Cmd);
  if not FileExists(Exe) then Exit;

  Log('Desinstalando la versión anterior: ' + Exe);
  if Exec(Exe, '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART', '',
          SW_SHOW, ewWaitUntilTerminated, ResultCode) then
    Log('Desinstalación anterior finalizada con código ' + IntToStr(ResultCode) + '.')
  else
    Log('No se pudo ejecutar el desinstalador anterior.');
end;

function IsVncServerInstalled(): Boolean;
begin
  Result := FileExists(ExpandConstant('{commonpf}\TightVNC\tvnserver.exe')) or
            FileExists(ExpandConstant('{commonpf32}\TightVNC\tvnserver.exe'));
end;

function ShouldCreateVncShortcut(): Boolean;
begin
  // La tarea se instala en [Run], después de [Icons]; por eso también se comprueba
  // la tarea y no solo si TightVNC ya está presente.
  Result := WizardIsTaskSelected('vncserver') or IsVncServerInstalled();
end;

function ShouldInstallVncServer(): Boolean;
begin
  // En modo silencioso no hay página de contraseña: la tarea se omite para no
  // instalar un servidor VNC sin contraseña.
  Result := WizardIsTaskSelected('vncserver') and (Length(VncPassword) >= 4);
end;

function GetVncPort(Param: String): String;
begin
  Result := IntToStr(VncPort);
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  if PageID = VncPage.ID then
    Result := not WizardIsTaskSelected('vncserver');
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  PortText: String;
  I: Integer;
begin
  Result := True;
  if CurPageID <> VncPage.ID then Exit;

  VncPassword := Trim(VncPage.Values[0]);
  if (Length(VncPassword) < 4) or (Length(VncPassword) > 8) then
  begin
    MsgBox(ExpandConstant('{cm:VncPasswordInvalid}'), mbError, MB_OK);
    Result := False;
    Exit;
  end;

  for I := 1 to Length(VncPassword) do
  begin
    if (Ord(VncPassword[I]) < 33) or (Ord(VncPassword[I]) > 126) or
       (VncPassword[I] = '"') then
    begin
      MsgBox(ExpandConstant('{cm:VncPasswordAscii}'), mbError, MB_OK);
      Result := False;
      Exit;
    end;
  end;

  PortText := Trim(VncPage.Values[1]);
  VncPort := StrToIntDef(PortText, 0);
  if (VncPort < 1024) or (VncPort > 65535) then
  begin
    MsgBox(ExpandConstant('{cm:VncPortInvalid}'), mbError, MB_OK);
    Result := False;
    Exit;
  end;

  // El script de instalación lee la contraseña de este archivo temporal y lo borra.
  if not SaveStringToFile(ExpandConstant('{tmp}\cybermanager-vnc.pwd'), VncPassword, False) then
  begin
    MsgBox('No se pudo preparar la contraseña de VNC. Verifique el espacio temporal.',
      mbError, MB_OK);
    Result := False;
  end;
end;

// Inno Setup llama a esta función antes de copiar los archivos. Si el usuario no
// acepta cerrar CyberManager, se devuelve un mensaje y la instalación se cancela.
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  NeedsRestart := False;

  ServerWasRunning := IsServerSelected() and
    (IsProcessRunning('CyberManager.Server.exe') or IsProcessRunning('CyberManager.exe'));
  ConsoleWasRunning := IsServerSelected() and IsProcessRunning('CyberManager.Admin.exe');
  ClientWasRunning := IsStationSelected() and IsProcessRunning('CyberManager.Client.exe');
  AgentWasRunning := IsStationSelected() and IsProcessRunning('CyberManager.Agent.exe');

  if (ServerWasRunning or ConsoleWasRunning or ClientWasRunning or AgentWasRunning)
     and not WizardSilent then
  begin
    if MsgBox(ExpandConstant('{cm:CloseRunningPrompt}') + #13#10#13#10 +
              ExpandConstant('{cm:CloseRunningHint}'),
              mbConfirmation, MB_YESNO) <> IDYES then
    begin
      Result := ExpandConstant('{cm:InstallCancelled}');
      Exit;
    end;
  end;

  // Migración desde los instaladores separados (versiones anteriores).
  if IsServerSelected() then RemoveOldInstall('{#OldServerAppId}');
  if IsStationSelected() then RemoveOldInstall('{#OldStationAppId}');

  if IsServerSelected() then
  begin
    Log('Deteniendo servidor y consola de CyberManager...');
    StopScheduledTask;
    StopProcess('CyberManager.exe');
    StopProcess('CyberManager.Server.exe');
    StopProcess('CyberManager.Admin.exe');
  end;

  if IsStationSelected() then
  begin
    Log('Deteniendo cliente y agente de CyberManager...');
    StopAgentService;
    StopProcess('CyberManager.Agent.exe');
    StopProcess('CyberManager.Client.exe');
  end;
end;

function ShouldOpenConsole(): Boolean;
begin
  // Si algo estaba en ejecución se reinicia en ssDone; así no se abren dos consolas.
  Result := not (ServerWasRunning or ConsoleWasRunning);
end;

function ShouldStartServer(): Boolean;
begin
  // Si el servidor ya estaba en ejecución, ssDone lo reinicia; la casilla sobra.
  Result := not ServerWasRunning;
end;

function ShouldOpenClient(): Boolean;
begin
  Result := not (ClientWasRunning or AgentWasRunning);
end;

// Si se registró la tarea de inicio elevado se abre el cliente con ella, para no
// pedir UAC; si no, se lanza el ejecutable directamente.
function ShouldOpenClientTask(): Boolean;
begin
  Result := ShouldOpenClient() and WizardIsTaskSelected('startupclient');
end;

function ShouldOpenClientDirect(): Boolean;
begin
  Result := ShouldOpenClient() and not WizardIsTaskSelected('startupclient');
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  StartedWithLauncher: Boolean;
begin
  // ssDone se ejecuta después de [Run], cuando la tarea programada y el agente ya existen.
  if CurStep <> ssDone then Exit;

  // El script de VNC ya borra el archivo, pero se limpia también si falló o se omitió.
  DeleteFile(ExpandConstant('{tmp}\cybermanager-vnc.pwd'));

  StartedWithLauncher := False;

  if IsServerSelected() and ServerWasRunning then
  begin
    if HasScheduledTask() then
      StartScheduledTask
    else
    begin
      Exec(ExpandConstant('{app}\Servidor\Server\CyberManager.exe'), '--launch-console',
        ExpandConstant('{app}\Servidor\Server'), SW_SHOWNORMAL, ewNoWait, ResultCode);
      StartedWithLauncher := True;
    end;
  end;

  // La consola se reabre si estaba abierta, salvo que el lanzador ya la haya abierto.
  if IsServerSelected() and ConsoleWasRunning and not StartedWithLauncher then
    Exec(ExpandConstant('{app}\Servidor\Admin\CyberManager.Admin.exe'), '',
      ExpandConstant('{app}\Servidor\Admin'), SW_SHOWNORMAL, ewNoWait, ResultCode);

  if IsStationSelected() and AgentWasRunning then
    StartAgentService;

  if IsStationSelected() and ClientWasRunning then
  begin
    if WizardIsTaskSelected('startupclient') then
      Exec(ExpandConstant('{sys}\schtasks.exe'), '/Run /TN "CyberManagerClient"',
        '', SW_HIDE, ewNoWait, ResultCode)
    else
      Exec(ExpandConstant('{app}\Estacion\Client\CyberManager.Client.exe'), '',
        ExpandConstant('{app}\Estacion\Client'), SW_SHOWNORMAL, ewNoWait, ResultCode);
  end;
end;

procedure InitializeWizard();
var
  ArchInfo: String;
begin
  VncPassword := '';
  VncPort := 5900;

  if Is64BitInstallMode then
    ArchInfo := ExpandConstant('{cm:InstallingArch64}')
  else
    ArchInfo := ExpandConstant('{cm:InstallingArch32}');

  WizardForm.WelcomeLabel2.Caption :=
    WizardForm.WelcomeLabel2.Caption + #13#10#13#10 + ArchInfo;

  // Página de contraseña y puerto del servidor VNC; solo aparece si se marca la tarea.
  VncPage := CreateInputQueryPage(wpSelectTasks,
    ExpandConstant('{cm:VncPageTitle}'),
    ExpandConstant('{cm:VncPageDescription}'),
    ExpandConstant('{cm:VncPageSubtitle}'));
  VncPage.Add(ExpandConstant('{cm:VncPassword}'), True);
  VncPage.Add(ExpandConstant('{cm:VncPort}'), False);
  VncPage.Values[0] := '';
  VncPage.Values[1] := '5900';

  Log('Modo de instalación: ' + ArchInfo);
end;
