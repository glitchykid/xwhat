#ifndef AppVersion
  #error AppVersion is required
#endif

#include "Dependencies.iss"
#ifndef SourcePath
  #error SourcePath is required
#endif

[Setup]
AppId={{0D4B9B4E-1E9A-44E7-8F17-640688EF30C1}
AppName=X What
AppVersion={#AppVersion}
AppPublisher=glitchykid
AppPublisherURL=https://github.com/glitchykid/xwhat
AppSupportURL=https://github.com/glitchykid/xwhat/issues
DefaultDirName={autopf}\XWhat
DisableDirPage=yes
DefaultGroupName=X What
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.17763
WizardStyle=modern dark
WizardSizePercent=100
InfoBeforeFile=install-info.ru.txt
OutputBaseFilename=XWhat-{#AppVersion}-Setup-win-x64
Compression=lzma2/fast
SolidCompression=yes
ArchiveExtraction=full
AppMutex=Local\XWhat.SingleInstance
CloseApplications=no
RestartApplications=no
UninstallDisplayIcon={app}\XWhat.exe
SetupLogging=yes

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Types]
Name: "full"; Description: "Игровой мониторинг (рекомендуется)"
Name: "custom"; Description: "Выбрать компоненты"; Flags: iscustom

[Components]
Name: "app"; Description: "X What, .NET и PresentMon"; Types: full custom; Flags: fixed
Name: "rtss"; Description: "RTSS — оверлей поверх полноэкранных игр"; Types: full
Name: "pawnio"; Description: "PawnIO — подписанный драйвер датчиков CPU"; Types: full

[Files]
Source: "{#SourcePath}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\X What"; Filename: "{app}\XWhat.exe"; WorkingDir: "{app}"

[Run]
Filename: "{app}\XWhat.exe"; Description: "Запустить X What (после запуска перезапустите игру)"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
var
  DownloadPage: TDownloadWizardPage;
  RebootRequired: Boolean;

function RtssExecutable: String;
var
  Roots: array[0..1] of Integer;
  Names: TArrayOfString;
  Index, Root: Integer;
  Name, Location: String;
begin
  Result := ExpandConstant('{pf32}\RivaTuner Statistics Server\RTSS.exe');
  if FileExists(Result) then exit;
  Result := ExpandConstant('{pf64}\RivaTuner Statistics Server\RTSS.exe');
  if FileExists(Result) then exit;
  Roots[0] := HKLM32;
  Roots[1] := HKLM64;
  for Root := 0 to 1 do begin
    if RegQueryStringValue(Roots[Root], 'SOFTWARE\Unwinder\RTSS', 'InstallPath', Location) then begin
      Result := AddBackslash(Location) + 'RTSS.exe';
      if FileExists(Result) then exit;
    end;
    if RegGetSubkeyNames(Roots[Root], 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall', Names) then
      for Index := 0 to GetArrayLength(Names) - 1 do begin
        if RegQueryStringValue(Roots[Root], 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\' + Names[Index], 'DisplayName', Name) and (Name = 'RivaTuner Statistics Server') then
          if RegQueryStringValue(Roots[Root], 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\' + Names[Index], 'InstallLocation', Location) then begin
            Result := AddBackslash(Location) + 'RTSS.exe';
            if FileExists(Result) then exit;
          end;
      end;
  end;
  Result := '';
end;

function PawnIOInstalled: Boolean;
begin
  Result := RegKeyExists(HKLM64, 'SYSTEM\CurrentControlSet\Services\PawnIO');
end;

procedure InitializeWizard;
begin
  DownloadPage := CreateDownloadPage('Компоненты игрового мониторинга', 'Загрузка с официальных серверов и проверка файлов', nil);
  DownloadPage.ShowBaseNameInsteadOfUrl := True;
end;

function RunComponent(const Filename, Arguments: String; var Error: String): Boolean;
var
  ExitCode: Integer;
begin
  Result := Exec(Filename, Arguments, '', SW_HIDE, ewWaitUntilTerminated, ExitCode);
  if Result then begin
    Result := (ExitCode = 0) or (ExitCode = 3010);
    if ExitCode = 3010 then RebootRequired := True;
  end;
  if not Result then Error := 'Не удалось установить компонент: ' + ExtractFileName(Filename) + ' (код ' + IntToStr(ExitCode) + '). Установка X What не завершена.';
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  InstallRtss, InstallPawn: Boolean;
begin
  Result := '';
  InstallRtss := WizardIsComponentSelected('rtss') and (RtssExecutable = '');
  InstallPawn := WizardIsComponentSelected('pawnio') and not PawnIOInstalled;
  DownloadPage.Clear;
  if InstallRtss then
    DownloadPage.Add('{#RtssUrl}', 'rtss-7.3.7.zip', '{#RtssArchiveHash}');
  if InstallPawn then
    DownloadPage.Add('{#PawnUrl}', 'PawnIO_setup.exe', '{#PawnHash}');
  DownloadPage.Show;
  try
    try
      if InstallRtss or InstallPawn then DownloadPage.Download;
      if InstallRtss then begin
        ForceDirectories(ExpandConstant('{tmp}\rtss'));
        ExtractArchive(ExpandConstant('{tmp}\rtss-7.3.7.zip'), ExpandConstant('{tmp}\rtss'), '', True, nil);
        if GetSHA256OfFile(ExpandConstant('{tmp}\rtss\RTSSSetup737.exe')) <> '{#RtssSetupHash}' then
          RaiseException('Файл RTSS не прошёл проверку. Он не будет запущен.');
        if not RunComponent(ExpandConstant('{tmp}\rtss\RTSSSetup737.exe'), '/S', Result) then exit;
        if RtssExecutable = '' then RaiseException('RTSS не найден после установки.');
      end;
      if InstallPawn then begin
        if not RunComponent(ExpandConstant('{tmp}\PawnIO_setup.exe'), '-install -silent', Result) then exit;
        if not PawnIOInstalled then RaiseException('Драйвер PawnIO не найден после установки.');
      end;
    except
      Result := 'Компоненты не установлены: ' + GetExceptionMessage + #13#10 + 'Проверьте интернет и повторите установку. Повреждённые файлы не запускаются.';
    end;
  finally
    DownloadPage.Hide;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Error: String;
begin
  if CurStep = ssPostInstall then begin
    if not RunComponent(ExpandConstant('{app}\XWhat.exe'), '--install-tasks', Error) then RaiseException(Error);
  end;
end;

function NeedRestart: Boolean;
begin
  Result := RebootRequired;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ExitCode: Integer;
begin
  if CurUninstallStep = usUninstall then begin
    if not Exec(ExpandConstant('{app}\XWhat.exe'), '--uninstall-tasks', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) or (ExitCode <> 0) then
      Log('Не удалось удалить задачу запуска X What. Подробности в журнале X What.');
  end;
end;
