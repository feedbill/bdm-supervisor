; BDM-500 Supervisor - instalador Feedbill (Inno Setup 6)
#define AppName "BDM-500 Supervisor"
#define AppVersion "0.7.4"
#define AppPublisher "Feedbill"
#define AppExe "BdmSupervisor.exe"
#define SrcDir "C:\feedbill\BdmSupervisor"

[Setup]
AppId={{6F2B9C41-8D3E-4A7B-9C55-1E2F3A4B5C6D}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://www.feedbill.com
AppSupportURL=https://www.feedbill.com
DefaultDirName={commonpf32}\Feedbill\BDM Supervisor
DefaultGroupName=Feedbill
OutputDir={#SrcDir}\installer
OutputBaseFilename=BdmSupervisor-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x86compatible
PrivilegesRequired=admin
WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExe}
CloseApplications=force
RestartApplications=no
UsePreviousAppDir=yes
DisableProgramGroupPage=yes

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
spanish.TaskDesktop=Crear acceso directo en el escritorio
english.TaskDesktop=Create a desktop shortcut
spanish.GrpShortcuts=Accesos directos:
english.GrpShortcuts=Shortcuts:
spanish.TaskVdm=Registrar esta aplicación como herramienta de supervisor (VDM / TestTool) del Service Provider SNBC
english.TaskVdm=Register this application as the supervisor tool (VDM / TestTool) of the SNBC Service Provider
spanish.GrpVdm=Integración con el SP XFS de SNBC:
english.GrpVdm=SNBC XFS SP integration:
spanish.TaskProfile=Configurar el SP según el equipo instalado:
english.TaskProfile=Configure the SP for the installed hardware:
spanish.GrpProfile=Perfil del BDM-500:
english.GrpProfile=BDM-500 profile:
spanish.TaskGalicia=Sin shutter eléctrico / mecánico (Banco Galicia)
english.TaskGalicia=Without electric shutter (shutter disabled)
spanish.TaskElectrico=Con shutter eléctrico (configuración de fábrica SNBC)
english.TaskElectrico=With electric shutter (SNBC factory configuration)
spanish.IconXfsCheck=Chequeo XFS (XfsCheck)
english.IconXfsCheck=XFS check (XfsCheck)
spanish.IconUninstall=Desinstalar
english.IconUninstall=Uninstall
spanish.RunApp=Abrir
english.RunApp=Open

[Tasks]
Name: "desktopicon"; Description: "{cm:TaskDesktop}"; GroupDescription: "{cm:GrpShortcuts}"
Name: "vdm"; Description: "{cm:TaskVdm}"; GroupDescription: "{cm:GrpVdm}"; Check: SpInstalled
Name: "profile"; Description: "{cm:TaskProfile}"; GroupDescription: "{cm:GrpProfile}"; Flags: checkablealone unchecked; Check: SpInstalled
Name: "profile\galicia"; Description: "{cm:TaskGalicia}"; Flags: exclusive; Check: SpInstalled
Name: "profile\electrico"; Description: "{cm:TaskElectrico}"; Flags: exclusive unchecked; Check: SpInstalled

[Files]
Source: "{#SrcDir}\publish\BdmSupervisor.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SrcDir}\appsettings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "{#SrcDir}\XfsCheck\publish\XfsCheck.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SrcDir}\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SrcDir}\installer\vc_redist.x86.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: NeedVcRedist

[Dirs]
Name: "C:\Feedbill\BdmSupervisor\logs"; Permissions: users-modify; Flags: uninsneveruninstall
Name: "C:\Feedbill\BdmSupervisor\export"; Permissions: users-modify; Flags: uninsneveruninstall
Name: "{commonappdata}\Feedbill\BdmSupervisor"; Permissions: users-modify
Name: "{commonappdata}\Feedbill\BdmSupervisor\logs"; Permissions: users-modify
Name: "{commonappdata}\Feedbill\BdmSupervisor\export"; Permissions: users-modify

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\{cm:IconXfsCheck}"; Filename: "{app}\XfsCheck.exe"
Name: "{group}\{cm:IconUninstall} {#AppName}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; TestTool del VDM: guarda el valor anterior y apunta a esta app
Root: HKLM32; Subkey: "SOFTWARE\XFS\SERVICE_PROVIDERS\VDM_SNBC\Config"; ValueType: string; ValueName: "TestTool_backup_feedbill"; ValueData: "{code:OldValue|SOFTWARE\XFS\SERVICE_PROVIDERS\VDM_SNBC\Config|TestTool}"; Tasks: vdm; Check: VdmKeyExists; Flags: createvalueifdoesntexist
Root: HKLM32; Subkey: "SOFTWARE\XFS\SERVICE_PROVIDERS\VDM_SNBC\Config"; ValueType: string; ValueName: "TestTool"; ValueData: "{app}\{#AppExe} --vda"; Tasks: vdm; Check: VdmKeyExists
; Perfil sin shutter elÃ©ctrico: la placa SIU no controla el shutter
Root: HKLM32; Subkey: "SOFTWARE\XFS\SERVICE_PROVIDERS\SnbcTCR\SiuConfig"; ValueType: dword; ValueName: "SiuIsControl_backup_feedbill"; ValueData: "{code:OldDword|SOFTWARE\XFS\SERVICE_PROVIDERS\SnbcTCR\SiuConfig|SiuIsControl}"; Tasks: profile\galicia; Check: SiuKeyExists; Flags: createvalueifdoesntexist
Root: HKLM32; Subkey: "SOFTWARE\XFS\SERVICE_PROVIDERS\SnbcTCR\SiuConfig"; ValueType: dword; ValueName: "SiuIsControl"; ValueData: "0"; Tasks: profile\galicia; Check: SiuKeyExists
Root: HKLM32; Subkey: "SOFTWARE\XFS\SERVICE_PROVIDERS\SnbcTCR\SiuConfig"; ValueType: dword; ValueName: "SiuIsControl"; ValueData: "1"; Tasks: profile\electrico; Check: SiuKeyExists

[INI]
Filename: "C:\SNBC SP\SIU_SNBC_STMIOAV\STMIO.ini"; Section: "Board"; Key: "BoardCount"; String: "1"; Tasks: profile\galicia; Check: SpFileExists('C:\SNBC SP\SIU_SNBC_STMIOAV\STMIO.ini')
Filename: "C:\SNBC SP\SnbcTCR\DevCfg\CRM_TCR.ini"; Section: "PHY"; Key: "ShutterDropDoorSurport"; String: "0"; Tasks: profile\galicia; Check: SpFileExists('C:\SNBC SP\SnbcTCR\DevCfg\CRM_TCR.ini')
Filename: "C:\SNBC SP\SnbcTCR\DevCfg\CRM_TCR.ini"; Section: "PHY"; Key: "ShutterDropDoorSurport"; String: "1"; Tasks: profile\electrico; Check: SpFileExists('C:\SNBC SP\SnbcTCR\DevCfg\CRM_TCR.ini')

[Run]
Filename: "{tmp}\vc_redist.x86.exe"; Parameters: "/install /quiet /norestart"; StatusMsg: "Instalando Visual C++ Redistributable (x86)..."; Check: NeedVcRedist; Flags: waituntilterminated
Filename: "{app}\{#AppExe}"; Description: "{cm:RunApp} {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "taskkill"; Parameters: "/F /IM BdmSupervisor.exe"; Flags: runhidden; RunOnceId: "killapp"

[Code]
const
  VdmKey = 'SOFTWARE\XFS\SERVICE_PROVIDERS\VDM_SNBC\Config';
  SiuKey = 'SOFTWARE\XFS\SERVICE_PROVIDERS\SnbcTCR\SiuConfig';

function NeedVcRedist: Boolean;
var Inst: Cardinal;
begin
  Result := True;
  if RegQueryDWordValue(HKLM32, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x86', 'Installed', Inst) then Result := (Inst = 0);
end;

function SpInstalled: Boolean;
begin
  Result := DirExists('C:\SNBC SP');
end;

function SpFileExists(F: String): Boolean;
begin
  Result := FileExists(F);
end;

function VdmKeyExists: Boolean;
begin
  Result := RegKeyExists(HKLM32, VdmKey);
end;

function SiuKeyExists: Boolean;
begin
  Result := RegKeyExists(HKLM32, SiuKey);
end;

{ Param: "clave|valor" }
function OldValue(Param: String): String;
var P: Integer; K, V: String;
begin
  P := Pos('|', Param); K := Copy(Param, 1, P - 1); V := Copy(Param, P + 1, Length(Param));
  if not RegQueryStringValue(HKLM32, K, V, Result) then Result := '';
end;

function OldDword(Param: String): String;
var P: Integer; K, V: String; D: Cardinal;
begin
  P := Pos('|', Param); K := Copy(Param, 1, P - 1); V := Copy(Param, P + 1, Length(Param));
  if RegQueryDWordValue(HKLM32, K, V, D) then Result := IntToStr(D) else Result := '1';
end;

procedure BackupIni(F: String);
begin
  if FileExists(F) then FileCopy(F, F + '.bak-feedbill', False);
end;

procedure KillSp;
var R: Integer;
begin
  Exec('taskkill', '/F /IM SNBC_SPVDM.exe', '', SW_HIDE, ewWaitUntilTerminated, R);
  Exec('taskkill', '/F /IM SNBC_SPCRM.exe', '', SW_HIDE, ewWaitUntilTerminated, R);
end;

{ Cierra BDM Supervisor si está abierto (por ejemplo lanzado por Softy como TestTool) para poder reemplazar el .exe }
function PrepareToInstall(var NeedsRestart: Boolean): String;
var R: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /T /IM BdmSupervisor.exe', '', SW_HIDE, ewWaitUntilTerminated, R);
  Sleep(1000);
  Result := '';
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then begin
    if WizardIsTaskSelected('profile') then begin
      BackupIni('C:\SNBC SP\SIU_SNBC_STMIOAV\STMIO.ini');
      BackupIni('C:\SNBC SP\SnbcTCR\DevCfg\CRM_TCR.ini');
    end;
  end;
  if CurStep = ssPostInstall then
    if WizardIsTaskSelected('vdm') or WizardIsTaskSelected('profile') then KillSp;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var Old: String; D: Cardinal;
begin
  if CurUninstallStep = usPostUninstall then begin
    if RegQueryStringValue(HKLM32, VdmKey, 'TestTool_backup_feedbill', Old) then begin
      RegWriteStringValue(HKLM32, VdmKey, 'TestTool', Old);
      RegDeleteValue(HKLM32, VdmKey, 'TestTool_backup_feedbill');
    end;
    if RegQueryDWordValue(HKLM32, SiuKey, 'SiuIsControl_backup_feedbill', D) then begin
      RegWriteDWordValue(HKLM32, SiuKey, 'SiuIsControl', D);
      RegDeleteValue(HKLM32, SiuKey, 'SiuIsControl_backup_feedbill');
    end;
    KillSp;
  end;
end;

