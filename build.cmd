@echo off
setlocal
for %%i in ("%~dp0.") do set "ROOT=%%~fi"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set OUT=%ROOT%
copy /y "%ROOT%\lib\*.dll" "%OUT%\" >nul
"%CSC%" /nologo /target:winexe /optimize+ /codepage:65001 /win32icon:"%ROOT%\app.ico" /out:"%OUT%\K2.exe" ^
  /win32manifest:"%ROOT%\app.manifest" ^
  /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll ^
  /reference:System.Windows.Forms.dll /reference:System.Management.dll ^
  /reference:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\netstandard.dll" ^
  /reference:"%ROOT%\lib\LibreHardwareMonitorLib.dll" ^
  /reference:"%ROOT%\lib\SharpDX.dll" /reference:"%ROOT%\lib\SharpDX.DXGI.dll" /reference:"%ROOT%\lib\SharpDX.Direct3D11.dll" ^
  "%ROOT%\src\AssemblyInfo.cs" "%ROOT%\src\Program.cs" "%ROOT%\src\Monitor.cs" "%ROOT%\src\MainForm.cs" "%ROOT%\src\HistoryForm.cs" "%ROOT%\src\Settings.cs" "%ROOT%\src\Stats.cs" "%ROOT%\src\OverlayDraw.cs" "%ROOT%\src\SettingsForm.cs" "%ROOT%\src\PreviewControl.cs" "%ROOT%\src\MenuTheme.cs" "%ROOT%\src\FpsEngine.cs" "%ROOT%\src\PreviewDump.cs"
if errorlevel 1 (echo BUILD FAILED & exit /b 1)
echo BUILD OK: %OUT%\K2.exe
