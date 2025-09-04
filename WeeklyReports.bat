cls
@ECHO OFF
ECHO ==================================================================================== >> BensJiraConsole.log
ECHO Start time: %date% %time%
ECHO Start time: %date% %time% >> BensJiraConsole.log
cd \Development\BensJiraConsole\Bin\Debug\net9.0
@ECHO ON
BensJiraConsole.exe BUG_STATS >> BensJiraConsole.log
BensJiraConsole.exe NOESTIMATE >> BensJiraConsole.log
BensJiraConsole.exe PMPLAN_BURNUP >> BensJiraConsole.log
BensJiraConsole.exe PMPLAN_RBURNUP >> BensJiraConsole.log
BensJiraConsole.exe PMPLAN_STORIES >> BensJiraConsole.log
BensJiraConsole.exe PMPLANS >> BensJiraConsole.log
BensJiraConsole.exe PMPLAN_NEW
@ECHO OFF
ECHO Finish time: %date% %time% >> BensJiraConsole.log
ECHO Finish time: %date% %time% 
pause
