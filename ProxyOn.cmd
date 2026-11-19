@echo off
start "" /wait "%~dp0app\ProxySwitch.exe" On
exit /b %errorlevel%
