cls
@ECHO OFF
ECHO Start time: %date% %time%
cd \Development\BensJiraConsole
git checkout master
git pull
dotnet build
cd \Development\BensJiraConsole\Bin\Debug\net9.0
BensJiraConsole.exe BUG_STATS
BensJiraConsole.exe INCIDENTS
BensJiraConsole.exe INIT_ALL
BensJiraConsole.exe SPRINT_PLAN

ECHO Finish time: %date% %time%

