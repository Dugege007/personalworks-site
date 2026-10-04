@echo off
chcp 65001 >nul
if exist "%~dp0.venv\Scripts\python.exe" (
  "%~dp0.venv\Scripts\python.exe" "%~dp0pdf2jpg4lcds.py" %*
) else (
  python "%~dp0pdf2jpg4lcds.py" %*
)
