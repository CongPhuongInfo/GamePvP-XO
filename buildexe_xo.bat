@echo off
setlocal enabledelayedexpansion

set VBC=

if exist "%windir%\Microsoft.NET\Framework\v4.0.30319\vbc.exe" (
    set VBC=%windir%\Microsoft.NET\Framework\v4.0.30319\vbc.exe
)
if exist "%windir%\Microsoft.NET\Framework64\v4.0.30319\vbc.exe" (
    set VBC=%windir%\Microsoft.NET\Framework64\v4.0.30319\vbc.exe
)

if "%VBC%"=="" (
    echo Khong tim thay vbc.exe cua .NET Framework 4.x tren may nay.
    pause
    exit /b 1
)

echo Dang dung compiler: %VBC%
echo.

"%VBC%" /target:winexe /out:XOOnline.exe /nologo ^
  /reference:System.dll ^
  /reference:System.Drawing.dll ^
  /reference:System.Windows.Forms.dll ^
  XOOnline.vb

echo.
if exist XOOnline.exe (
    echo Build thanh cong: XOOnline.exe
) else (
    echo Build LOI. Kiem tra loi phia tren.
)
pause
