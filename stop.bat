@echo off
echo Stopping StoryForge (backend on port 5276, frontend on port 4200)...

powershell -NoProfile -Command ^
  "$pids = Get-NetTCPConnection -LocalPort 5276,4200 -State Listen -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique;" ^
  "if (-not $pids) { Write-Host 'No StoryForge process found listening on 5276 or 4200.'; exit }" ^
  "foreach ($p in $pids) { try { Stop-Process -Id $p -Force -ErrorAction Stop; Write-Host ('Stopped process ' + $p) } catch { Write-Host ('Could not stop process ' + $p + ': ' + $_.Exception.Message) } }"

echo Done.
