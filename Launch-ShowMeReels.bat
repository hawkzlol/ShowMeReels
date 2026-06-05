@echo off
setlocal

set "ROOT=%~dp0"
start "" powershell -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "%ROOT%Launch-ShowMeReels.ps1"
