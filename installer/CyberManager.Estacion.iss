#define MyAppName "CyberManager Estación"
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#define MyDistX64 "..\dist\x64"
#define MyDistX86 "..\dist\x86"
#define MyAppPublisher "CyberManager"
#define MyAppURL "http://127.0.0.1:5080"

[Setup]
AppId={{C9D2E4F6-1A83-4B57-8C0D-6E3F9A2B5D74}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
DefaultDirName=C:\CyberManager\Estacion
DefaultGroupName=CyberManager Estación
DisableProgramGroupPage=yes
OutputDir=..\artifacts
OutputBaseFilename=CyberManager-Estacion-{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; Un solo instalador para x86 y x64. En equipos de 64 bits se activa el modo de
; instalación de 64 bits y se copia el payload de dist\x64; en equipos de 32 bits
; se copia dist\x86.
ArchitecturesAllowed=x86compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UninstallDisplayIcon={app}\Client\CyberManager.Client.exe
SetupIconFile=..\assets\CyberManager.ico
SetupLogging=yes
; El instalador detiene y reinicia los procesos con su propio código para pedir
; confirmación antes de interrumpir una sesión en curso.
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
spanish.StartupClient=Iniciar el cliente con Windows
english.StartupClient=Start the client with Windows
spanish.OpenClient=Abrir CyberManager Cliente
english.OpenClient=Open CyberManager Client
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
spanish.CloseRunningHint=Para actualizar es necesario cerrar el cliente y el agente. ¿Desea cerrarlos ahora?
english.CloseRunningHint=The client and agent must be closed to update. Do you want to close them now?
spanish.InstallCancelled=La instalación se canceló para no interrumpir la sesión en curso.
english.InstallCancelled=The installation was cancelled to avoid interrupting the current session.
spanish.UninstallAll=Desinstalar todo CyberManager
english.UninstallAll=Uninstall all CyberManager
spanish.RemoteGroup=Control remoto (opcional):
english.RemoteGroup=Remote control (optional):
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

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:ShortcutsGroup}"
Name: "startupclient"; Description: "{cm:StartupClient}"; GroupDescription: "{cm:StartupGroup}"
Name: "vncserver"; Description: "{cm:InstallVncServer}"; GroupDescription: "{cm:RemoteGroup}"; Flags: unchecked

[Files]
Source: "{#MyDistX64}\Client\*"; DestDir: "{app}\Client"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: IsX64InstallMode
Source: "{#MyDistX86}\Client\*"; DestDir: "{app}\Client"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: IsX86InstallMode
Source: "{#MyDistX64}\Agent\*"; DestDir: "{app}\Agent"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: IsX64InstallMode
Source: "{#MyDistX86}\Agent\*"; DestDir: "{app}\Agent"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: IsX86InstallMode
Source: "{#MyDistX64}\Scripts\*"; DestDir: "{app}\scripts"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: IsX64InstallMode
Source: "{#MyDistX86}\Scripts\*"; DestDir: "{app}\scripts"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: IsX86InstallMode
Source: "{#MyDistX64}\README.md"; DestDir: "{app}"; Flags: ignoreversion isreadme; Check: IsX64InstallMode
Source: "{#MyDistX86}\README.md"; DestDir: "{app}"; Flags: ignoreversion isreadme; Check: IsX86InstallMode
; Desinstalador independiente compartido por el servidor y la estación.
; uninsneveruninstall evita que el desinstalador de un componente borre el del otro.
Source: "{#MyDistX64}\Uninstaller\*"; DestDir: "{commonappdata}\CyberManager"; Flags: ignoreversion recursesubdirs createallsubdirs uninsneveruninstall; Check: IsX64InstallMode
Source: "{#MyDistX86}\Uninstaller\*"; DestDir: "{commonappdata}\CyberManager"; Flags: ignoreversion recursesubdirs createallsubdirs uninsneveruninstall; Check: IsX86InstallMode
; TightVNC se distribuye como programa separado (GPLv2) y solo se copia si se marca
; la tarea de control remoto. El MSI se guarda para poder desinstalarlo después.
Source: "vnc\tightvnc-2.8.90-gpl-setup-64bit.msi"; DestDir: "{app}\vnc"; Flags: ignoreversion; Tasks: vncserver; Check: IsX64InstallMode
Source: "vnc\tightvnc-2.8.90-gpl-setup-32bit.msi"; DestDir: "{app}\vnc"; Flags: ignoreversion; Tasks: vncserver; Check: IsX86InstallMode
Source: "..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion; Tasks: vncserver
Source: "..\LICENSE-TightVNC-GPLv2.txt"; DestDir: "{app}"; Flags: ignoreversion; Tasks: vncserver

[InstallDelete]
; Al reinstalar con la otra arquitectura, elimina los binarios nativos que no
; pertenecen al payload seleccionado. La configuración local no se toca.
Type: files; Name: "{app}\Client\Microsoft.DiaSymReader.Native.x86.dll"; Check: IsX64InstallMode
Type: files; Name: "{app}\Client\mscordaccore_x86_*.dll"; Check: IsX64InstallMode
Type: files; Name: "{app}\Agent\Microsoft.DiaSymReader.Native.x86.dll"; Check: IsX64InstallMode
Type: files; Name: "{app}\Agent\mscordaccore_x86_*.dll"; Check: IsX64InstallMode
Type: files; Name: "{app}\Client\Microsoft.DiaSymReader.Native.amd64.dll"; Check: IsX86InstallMode
Type: files; Name: "{app}\Client\mscordaccore_amd64_*.dll"; Check: IsX86InstallMode
Type: files; Name: "{app}\Agent\Microsoft.DiaSymReader.Native.amd64.dll"; Check: IsX86InstallMode
Type: files; Name: "{app}\Agent\mscordaccore_amd64_*.dll"; Check: IsX86InstallMode

[Icons]
Name: "{group}\CyberManager Cliente"; Filename: "{app}\Client\CyberManager.Client.exe"; WorkingDir: "{app}\Client"
Name: "{group}\Configurar agente de estación"; Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoExit -ExecutionPolicy Bypass -File ""{app}\scripts\configurar-agente.ps1"""
Name: "{group}\{cm:VncConfigShortcut}"; Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\scripts\configurar-vnc.ps1"""; WorkingDir: "{app}"; Check: ShouldCreateVncShortcut
Name: "{group}\Carpeta de instalación"; Filename: "{app}"
Name: "{group}\{cm:UninstallAll}"; Filename: "{commonappdata}\CyberManager\CyberManager.Uninstaller.exe"
Name: "{group}\Desinstalar CyberManager Estación"; Filename: "{uninstallexe}"
Name: "{autodesktop}\CyberManager Cliente"; Filename: "{app}\Client\CyberManager.Client.exe"; WorkingDir: "{app}\Client"; Tasks: desktopicon
Name: "{commonstartup}\CyberManager Cliente"; Filename: "{app}\Client\CyberManager.Client.exe"; WorkingDir: "{app}\Client"; Tasks: startupclient

[Run]
; Instala TightVNC en modo silencioso si se marcó la tarea de control remoto. La
; contraseña se pasa en un archivo temporal para no exponerla en la línea de comandos.
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\scripts\instalar-vnc.ps1"" -Action Install -Mode Server -Component estacion -Port {code:GetVncPort} -PasswordFile ""{tmp}\cybermanager-vnc.pwd"" -MsiPath ""{app}\vnc\tightvnc-2.8.90-gpl-setup-64bit.msi"""; StatusMsg: "{cm:InstallingVnc}"; Flags: runhidden waituntilterminated; Check: ShouldInstallVncServer and IsX64InstallMode
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\scripts\instalar-vnc.ps1"" -Action Install -Mode Server -Component estacion -Port {code:GetVncPort} -PasswordFile ""{tmp}\cybermanager-vnc.pwd"" -MsiPath ""{app}\vnc\tightvnc-2.8.90-gpl-setup-32bit.msi"""; StatusMsg: "{cm:InstallingVnc}"; Flags: runhidden waituntilterminated; Check: ShouldInstallVncServer and IsX86InstallMode
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoExit -ExecutionPolicy Bypass -File ""{app}\scripts\configurar-agente.ps1"""; Description: "{cm:ConfigureAgent}"; StatusMsg: "{cm:OpeningAgent}"; Flags: postinstall nowait skipifsilent
Filename: "{app}\Client\CyberManager.Client.exe"; Description: "{cm:OpenClient}"; WorkingDir: "{app}\Client"; Flags: nowait postinstall skipifsilent; Check: ShouldOpenClient

[UninstallRun]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\scripts\desinstalar-servicios.ps1"" -Agente"; Flags: runhidden waituntilterminated; RunOnceId: "DetenerAgenteCyberManager"

[Code]
var
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
// acepta cerrar el cliente, se devuelve un mensaje y la instalación se cancela.
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  NeedsRestart := False;

  ClientWasRunning := IsProcessRunning('CyberManager.Client.exe');
  AgentWasRunning := IsProcessRunning('CyberManager.Agent.exe');

  if (ClientWasRunning or AgentWasRunning) and not WizardSilent then
  begin
    if MsgBox(ExpandConstant('{cm:CloseRunningPrompt}') + #13#10#13#10 +
              ExpandConstant('{cm:CloseRunningHint}'),
              mbConfirmation, MB_YESNO) <> IDYES then
    begin
      Result := ExpandConstant('{cm:InstallCancelled}');
      Exit;
    end;
  end;

  Log('Deteniendo cliente y agente de CyberManager...');
  StopAgentService;
  StopProcess('CyberManager.Agent.exe');
  StopProcess('CyberManager.Client.exe');
end;

// No se abre el cliente si ya estaba en ejecución: se reinicia en ssDone para
// no dejar dos instancias abiertas.
function ShouldOpenClient(): Boolean;
begin
  Result := not (ClientWasRunning or AgentWasRunning);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  // ssDone se ejecuta después de [Run], cuando el agente ya fue configurado.
  if CurStep <> ssDone then Exit;

  // El script de VNC ya borra el archivo, pero se limpia también si falló o se omitió.
  DeleteFile(ExpandConstant('{tmp}\cybermanager-vnc.pwd'));

  if AgentWasRunning then
    StartAgentService;

  if ClientWasRunning then
    Exec(ExpandConstant('{app}\Client\CyberManager.Client.exe'), '',
      ExpandConstant('{app}\Client'), SW_SHOWNORMAL, ewNoWait, ResultCode);
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
