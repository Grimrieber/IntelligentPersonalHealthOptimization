@echo off
echo Setting up Spoonacular Daily Import scheduled task...
echo.

SET SCRIPT_DIR=%~dp0
SET BATCH_FILE=%SCRIPT_DIR%run_import.bat

REM Delete existing task if present
schtasks /delete /tn "SpoonacularRecipeImport" /f >nul 2>&1

REM Create XML task definition for full control over settings
REM Using run_import.bat which sets the working directory properly
REM StartWhenAvailable=true runs the task ASAP if the 3 AM window was missed (PC asleep)
set TASK_XML=%TEMP%\spoonacular_task.xml
(
echo ^<?xml version="1.0" encoding="UTF-16"?^>
echo ^<Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task"^>
echo   ^<Triggers^>
echo     ^<CalendarTrigger^>
echo       ^<StartBoundary^>2026-03-17T03:00:00^</StartBoundary^>
echo       ^<Enabled^>true^</Enabled^>
echo       ^<ScheduleByDay^>
echo         ^<DaysInterval^>1^</DaysInterval^>
echo       ^</ScheduleByDay^>
echo     ^</CalendarTrigger^>
echo   ^</Triggers^>
echo   ^<Settings^>
echo     ^<MultipleInstancesPolicy^>IgnoreNew^</MultipleInstancesPolicy^>
echo     ^<DisallowStartIfOnBatteries^>false^</DisallowStartIfOnBatteries^>
echo     ^<StopIfGoingOnBatteries^>false^</StopIfGoingOnBatteries^>
echo     ^<AllowHardTerminate^>true^</AllowHardTerminate^>
echo     ^<StartWhenAvailable^>true^</StartWhenAvailable^>
echo     ^<RunOnlyIfNetworkAvailable^>true^</RunOnlyIfNetworkAvailable^>
echo     ^<AllowStartOnDemand^>true^</AllowStartOnDemand^>
echo     ^<Enabled^>true^</Enabled^>
echo     ^<ExecutionTimeLimit^>PT1H^</ExecutionTimeLimit^>
echo   ^</Settings^>
echo   ^<Actions^>
echo     ^<Exec^>
echo       ^<Command^>%BATCH_FILE%^</Command^>
echo       ^<WorkingDirectory^>%SCRIPT_DIR%^</WorkingDirectory^>
echo     ^</Exec^>
echo   ^</Actions^>
echo ^</Task^>
) > "%TASK_XML%"

schtasks /create /tn "SpoonacularRecipeImport" /xml "%TASK_XML%" /f

del "%TASK_XML%" >nul 2>&1

if %errorlevel% equ 0 (
    echo.
    echo Task created successfully!
    echo   Name: SpoonacularRecipeImport
    echo   Schedule: Daily at 3:00 AM
    echo   Missed run: Will run as soon as PC wakes up
    echo   Battery: Will run on battery power
    echo   Script: %BATCH_FILE%
    echo.
    echo To view: Task Scheduler -^> SpoonacularRecipeImport
    echo To run now: schtasks /run /tn "SpoonacularRecipeImport"
    echo To delete: schtasks /delete /tn "SpoonacularRecipeImport" /f
) else (
    echo.
    echo Failed to create task. Try running this as Administrator.
)

echo.
pause
