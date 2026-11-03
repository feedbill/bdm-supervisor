; BDM-500 Supervisor - instalador Feedbill (Inno Setup 6)
#define AppName "BDM-500 Supervisor"
#define AppVersion "0.1.1"
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
CloseApplications=yes
DisableProgramGroupPage=yes

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear acceso directo en el escritorio"; GroupDescription: "Accesos directos:"
Name: "vdm"; Description: "Registrar esta aplicaciÃ³n como herramienta de supervisor (VDM / TestTool) del Service Provider SNBC"; GroupDescription: "IntegraciÃ³n con el SP XFS de SNBC:"; Check: SpInstalled
Name: "profile"; Description: "Configurar el SP segÃºn el equipo instalado:"; GroupDescription: "Perfil del BDM-500:"; Flags: checkablealone; Check: SpInstalled
Name: "profile\galicia"; Description: "Sin shutter elÃ©ctrico / mecÃ¡nico (Banco Galicia)"; Flags: exclusive; Check: SpInstalled
Name: "profile\electrico"; Description: "Con shutter elÃ©ctrico (configuraciÃ³n de fÃ¡brica SNBC)"; Flags: exclusive unchecked; Check: SpInstalled

[Files]
Source: "{#SrcDir}\publish\BdmSupervisor.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SrcDir}\appsettings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "{#SrcDir}\XfsCheck\publish\XfsCheck.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SrcDir}\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SrcDir}\installer\vc_redist.x86.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: NeedVcRedist

[Dirs]
Name: "C:\Feedbill\BdmSupervisor\logs"; Permissions: users-modify
Name: "C:\Feedbill\BdmSupervisor\export"; Permissions: users-modify

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Chequeo XFS (XfsCheck)"; Filename: "{app}\XfsCheck.exe"
Name: "{group}\Desinstalar {#AppName}"; Filename: "{uninstallexe}"
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
Filename: "{app}\{#AppExe}"; Description: "Abrir {#AppName}"; Flags: nowait postinstall skipifsilent

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

