@echo off
start "" /wait "%~dp0app\ProxySwitch.exe" Off
exit /b %errorlevel%
