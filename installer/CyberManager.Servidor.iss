#define MyAppName "CyberManager Servidor"
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#define MyDistX64 "..\dist\x64"
#define MyDistX86 "..\dist\x86"
#define MyAppPublisher "CyberManager"
#define MyAppURL "http://127.0.0.1:5080"

[Setup]
AppId={{A3B5D8E1-7C42-4F19-9A6B-2D8E5F1C7A31}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
DefaultDirName=C:\CyberManager\Servidor
DefaultGroupName=CyberManager Servidor
DisableProgramGroupPage=yes
OutputDir=..\artifacts
OutputBaseFilename=CyberManager-Servidor-{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; Un solo instalador para x86 y x64. En equipos de 64 bits se activa el modo de
; instalación de 64 bits y se copia el payload de dist\x64; en equipos de 32 bits
; se copia dist\x86.
ArchitecturesAllowed=x86compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UninstallDisplayIcon={app}\Admin\CyberManager.Admin.exe
SetupIconFile=..\assets\CyberManager.ico
SetupLogging=yes
; El instalador detiene y reinicia los procesos con su propio código para pedir
; confirmación antes de interrumpir una operación en curso.
CloseApplications=no

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
spanish.CreateDesktopIcon=Crear acceso directo en el escritorio
english.CreateDesktopIcon=Create a desktop shortcut
spanish.ShortcutsGroup=Accesos directos:
english.ShortcutsGroup=Shortcuts:
spanish.StartupGroup=Inicio automático:
english.StartupGroup=Automatic startup:
spanish.StartupAdmin=Abrir la consola al iniciar sesión
english.StartupAdmin=Open the console at sign-in
spanish.ServerStartup=Iniciar el servidor al encender el equipo (tarea programada)
english.ServerStartup=Start the server when the computer boots (scheduled task)
spanish.FirewallGroup=Red:
english.FirewallGroup=Network:
spanish.Firewall=Permitir el puerto 5080 en el Firewall de Windows
english.Firewall=Allow port 5080 through Windows Firewall
spanish.OpenConsole=Abrir la consola de administración
english.OpenConsole=Open the administration console
spanish.StartServer=Iniciar el servidor ahora
english.StartServer=Start the server now
spanish.Configuring=Configurando el servidor...
english.Configuring=Configuring the server...
spanish.InstallingArch64=Se instalará la versión de 64 bits, adecuada para este equipo.
english.InstallingArch64=The 64-bit version will be installed, which matches this computer.
spanish.InstallingArch32=Se instalará la versión de 32 bits, adecuada para este equipo.
english.InstallingArch32=The 32-bit version will be installed, which matches this computer.
spanish.CloseRunningPrompt=CyberManager está en ejecución.
english.CloseRunningPrompt=CyberManager is running.
spanish.CloseRunningHint=Para actualizar es necesario cerrar el servidor y la consola. ¿Desea cerrarlos ahora?
english.CloseRunningHint=The server and console must be closed to update. Do you want to close them now?
spanish.InstallCancelled=La instalación se canceló para no interrumpir la operación en curso.
english.InstallCancelled=The installation was cancelled to avoid interrupting the current operation.
spanish.UninstallAll=Desinstalar todo CyberManager
english.UninstallAll=Uninstall all CyberManager
spanish.RemoteGroup=Control remoto (opcional):
english.RemoteGroup=Remote control (optional):
spanish.InstallVncViewer=Instalar el visor TightVNC (GPLv2) para abrir VNC desde la consola
english.InstallVncViewer=Install the TightVNC viewer (GPLv2) to open VNC from the console
spanish.InstallingVncViewer=Instalando el visor TightVNC...
english.InstallingVncViewer=Installing the TightVNC viewer...

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:ShortcutsGroup}"
Name: "startupadmin"; Description: "{cm:StartupAdmin}"; GroupDescription: "{cm:StartupGroup}"; Flags: unchecked
Name: "serverstartup"; Description: "{cm:ServerStartup}"; GroupDescription: "{cm:StartupGroup}"
Name: "firewall"; Description: "{cm:Firewall}"; GroupDescription: "{cm:FirewallGroup}"
Name: "vncviewer"; Description: "{cm:InstallVncViewer}"; GroupDescription: "{cm:RemoteGroup}"; Flags: unchecked

[Files]
Source: "{#MyDistX64}\Server\*"; DestDir: "{app}\Server"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: IsX64InstallMode
Source: "{#MyDistX86}\Server\*"; DestDir: "{app}\Server"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: IsX86InstallMode
Source: "{#MyDistX64}\Admin\*"; DestDir: "{app}\Admin"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: IsX64InstallMode
Source: "{#MyDistX86}\Admin\*"; DestDir: "{app}\Admin"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: IsX86InstallMode
Source: "{#MyDistX64}\Scripts\*"; DestDir: "{app}\scripts"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: IsX64InstallMode
Source: "{#MyDistX86}\Scripts\*"; DestDir: "{app}\scripts"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: IsX86InstallMode
Source: "{#MyDistX64}\README.md"; DestDir: "{app}"; Flags: ignoreversion isreadme; Check: IsX64InstallMode
Source: "{#MyDistX86}\README.md"; DestDir: "{app}"; Flags: ignoreversion isreadme; Check: IsX86InstallMode
; Lanzador único: el mismo apphost del servidor, con otro nombre, inicia el
; servidor en segundo plano y abre la consola de administración.
Source: "{#MyDistX64}\Server\CyberManager.Server.exe"; DestDir: "{app}\Server"; DestName: "CyberManager.exe"; Flags: ignoreversion; Check: IsX64InstallMode
Source: "{#MyDistX86}\Server\CyberManager.Server.exe"; DestDir: "{app}\Server"; DestName: "CyberManager.exe"; Flags: ignoreversion; Check: IsX86InstallMode
; Desinstalador independiente compartido por el servidor y la estación.
; uninsneveruninstall evita que el desinstalador de un componente borre el del otro.
Source: "{#MyDistX64}\Uninstaller\*"; DestDir: "{commonappdata}\CyberManager"; Flags: ignoreversion recursesubdirs createallsubdirs uninsneveruninstall; Check: IsX64InstallMode
Source: "{#MyDistX86}\Uninstaller\*"; DestDir: "{commonappdata}\CyberManager"; Flags: ignoreversion recursesubdirs createallsubdirs uninsneveruninstall; Check: IsX86InstallMode
; TightVNC se distribuye como programa separado (GPLv2). El servidor solo instala el
; visor para abrir VNC desde la consola; el servidor VNC se instala en las estaciones.
Source: "vnc\tightvnc-2.8.90-gpl-setup-64bit.msi"; DestDir: "{app}\vnc"; Flags: ignoreversion; Tasks: vncviewer; Check: IsX64InstallMode
Source: "vnc\tightvnc-2.8.90-gpl-setup-32bit.msi"; DestDir: "{app}\vnc"; Flags: ignoreversion; Tasks: vncviewer; Check: IsX86InstallMode
Source: "..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion; Tasks: vncviewer
Source: "..\LICENSE-TightVNC-GPLv2.txt"; DestDir: "{app}"; Flags: ignoreversion; Tasks: vncviewer

[InstallDelete]
; Al reinstalar con la otra arquitectura, elimina los binarios nativos que no
; pertenecen al payload seleccionado. Los datos del negocio no se tocan.
Type: files; Name: "{app}\Server\Microsoft.DiaSymReader.Native.x86.dll"; Check: IsX64InstallMode
Type: files; Name: "{app}\Server\mscordaccore_x86_*.dll"; Check: IsX64InstallMode
Type: files; Name: "{app}\Admin\Microsoft.DiaSymReader.Native.x86.dll"; Check: IsX64InstallMode
Type: files; Name: "{app}\Admin\mscordaccore_x86_*.dll"; Check: IsX64InstallMode
Type: files; Name: "{app}\Server\Microsoft.DiaSymReader.Native.amd64.dll"; Check: IsX86InstallMode
Type: files; Name: "{app}\Server\mscordaccore_amd64_*.dll"; Check: IsX86InstallMode
Type: files; Name: "{app}\Admin\Microsoft.DiaSymReader.Native.amd64.dll"; Check: IsX86InstallMode
Type: files; Name: "{app}\Admin\mscordaccore_amd64_*.dll"; Check: IsX86InstallMode

[Icons]
Name: "{group}\CyberManager"; Filename: "{app}\Server\CyberManager.exe"; Parameters: "--launch-console"; WorkingDir: "{app}\Server"
Name: "{group}\Consola de administración"; Filename: "{app}\Admin\CyberManager.Admin.exe"; WorkingDir: "{app}\Admin"
Name: "{group}\Servidor (solo consola)"; Filename: "{app}\Server\CyberManager.Server.exe"; WorkingDir: "{app}\Server"
Name: "{group}\Carpeta de instalación"; Filename: "{app}"
Name: "{group}\{cm:UninstallAll}"; Filename: "{commonappdata}\CyberManager\CyberManager.Uninstaller.exe"
Name: "{group}\Desinstalar CyberManager Servidor"; Filename: "{uninstallexe}"
Name: "{autodesktop}\CyberManager"; Filename: "{app}\Server\CyberManager.exe"; Parameters: "--launch-console"; WorkingDir: "{app}\Server"; Tasks: desktopicon
Name: "{commonstartup}\CyberManager"; Filename: "{app}\Server\CyberManager.exe"; Parameters: "--launch-console"; WorkingDir: "{app}\Server"; Tasks: startupadmin

[Run]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\scripts\configurar-servidor.ps1"" -RegistrarTarea -AbrirFirewall"; StatusMsg: "{cm:Configuring}"; Flags: runhidden waituntilterminated; Tasks: serverstartup or firewall
; Visor TightVNC opcional: solo se instala si se marca la tarea de control remoto.
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\scripts\instalar-vnc.ps1"" -Action Install -Mode Viewer -Component servidor -MsiPath ""{app}\vnc\tightvnc-2.8.90-gpl-setup-64bit.msi"""; StatusMsg: "{cm:InstallingVncViewer}"; Flags: runhidden waituntilterminated; Tasks: vncviewer; Check: IsX64InstallMode
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\scripts\instalar-vnc.ps1"" -Action Install -Mode Viewer -Component servidor -MsiPath ""{app}\vnc\tightvnc-2.8.90-gpl-setup-32bit.msi"""; StatusMsg: "{cm:InstallingVncViewer}"; Flags: runhidden waituntilterminated; Tasks: vncviewer; Check: IsX86InstallMode
; Casilla de la página final: deja el servidor activo sin abrir la consola.
; El lanzador comprueba /api/health y espera la respuesta; así, si también se marcó
; «Abrir la consola», esta no inicia una segunda instancia del servidor.
Filename: "{app}\Server\CyberManager.exe"; Parameters: "--start-server"; Description: "{cm:StartServer}"; WorkingDir: "{app}\Server"; Flags: runhidden waituntilterminated postinstall skipifsilent; Check: ShouldStartServer
Filename: "{app}\Server\CyberManager.exe"; Parameters: "--launch-console"; Description: "{cm:OpenConsole}"; WorkingDir: "{app}\Server"; Flags: nowait postinstall skipifsilent runasoriginaluser; Check: ShouldOpenConsole

[UninstallRun]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\scripts\desinstalar-servicios.ps1"" -Servidor"; Flags: runhidden waituntilterminated; RunOnceId: "DetenerServidorCyberManager"

[UninstallDelete]
Type: filesandordirs; Name: "{app}\Server\logs"
Type: files; Name: "{app}\Server\cybermanager.db-shm"
Type: files; Name: "{app}\Server\cybermanager.db-wal"

[Code]
var
  ServerWasRunning: Boolean;
  ConsoleWasRunning: Boolean;

function IsX64InstallMode(): Boolean;
begin
  Result := Is64BitInstallMode;
end;

function IsX86InstallMode(): Boolean;
begin
  Result := not Is64BitInstallMode;
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

// Inno Setup llama a esta función antes de copiar los archivos. Si el usuario no
// acepta cerrar CyberManager, se devuelve un mensaje y la instalación se cancela.
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  NeedsRestart := False;

  ServerWasRunning := IsProcessRunning('CyberManager.Server.exe')
                      or IsProcessRunning('CyberManager.exe');
  ConsoleWasRunning := IsProcessRunning('CyberManager.Admin.exe');

  if (ServerWasRunning or ConsoleWasRunning) and not WizardSilent then
  begin
    if MsgBox(ExpandConstant('{cm:CloseRunningPrompt}') + #13#10#13#10 +
              ExpandConstant('{cm:CloseRunningHint}'),
              mbConfirmation, MB_YESNO) <> IDYES then
    begin
      Result := ExpandConstant('{cm:InstallCancelled}');
      Exit;
    end;
  end;

  Log('Deteniendo servidor y consola de CyberManager...');
  StopScheduledTask;
  StopProcess('CyberManager.exe');
  StopProcess('CyberManager.Server.exe');
  StopProcess('CyberManager.Admin.exe');
end;

function ShouldOpenConsole(): Boolean;
begin
  // Si algo estaba en ejecución se reinicia en ssDone; así no se abren dos consolas.
  Result := not (ServerWasRunning or ConsoleWasRunning);
end;

function ShouldStartServer(): Boolean;
begin
  // Si el servidor ya estaba en ejecución, ssDone lo reinicia; la casilla sobra.
  // En una instalación nueva aparece marcada por defecto para dejar el servicio activo.
  Result := not ServerWasRunning;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  StartedWithLauncher: Boolean;
begin
  // ssDone se ejecuta después de [Run], cuando la tarea programada ya existe.
  if CurStep <> ssDone then Exit;

  StartedWithLauncher := False;

  // Si el servidor estaba en ejecución, se vuelve a iniciar para no dejar el
  // negocio sin servicio después de la actualización.
  if ServerWasRunning then
  begin
    if HasScheduledTask() then
      StartScheduledTask
    else
    begin
      Exec(ExpandConstant('{app}\Server\CyberManager.exe'), '--launch-console',
        ExpandConstant('{app}\Server'), SW_SHOWNORMAL, ewNoWait, ResultCode);
      StartedWithLauncher := True;
    end;
  end;

  // La consola se reabre si estaba abierta, salvo que el lanzador ya la haya abierto.
  if ConsoleWasRunning and not StartedWithLauncher then
    Exec(ExpandConstant('{app}\Admin\CyberManager.Admin.exe'), '',
      ExpandConstant('{app}\Admin'), SW_SHOWNORMAL, ewNoWait, ResultCode);
end;

procedure InitializeWizard();
var
  ArchInfo: String;
begin
  if Is64BitInstallMode then
    ArchInfo := ExpandConstant('{cm:InstallingArch64}')
  else
    ArchInfo := ExpandConstant('{cm:InstallingArch32}');

  WizardForm.WelcomeLabel2.Caption :=
    WizardForm.WelcomeLabel2.Caption + #13#10#13#10 + ArchInfo;
  Log('Modo de instalación: ' + ArchInfo);
end;
