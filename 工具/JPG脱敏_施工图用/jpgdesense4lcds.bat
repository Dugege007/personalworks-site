@echo off
chcp 65001 >nul
if exist "%~dp0.venv\Scripts\python.exe" (
  "%~dp0.venv\Scripts\python.exe" "%~dp0jpgdesense4lcds.py" %*
) else if exist "%~dp0..\PDF转JPG_施工图用\.venv\Scripts\python.exe" (
  "%~dp0..\PDF转JPG_施工图用\.venv\Scripts\python.exe" "%~dp0jpgdesense4lcds.py" %*
) else (
  python "%~dp0jpgdesense4lcds.py" %*
)
