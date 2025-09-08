cls
@ECHO OFF
ECHO ==================================================================================== >> BensJiraConsole.log
ECHO Start time: %date% %time%
ECHO Start time: %date% %time% >> BensJiraConsole.log
cd \Development\BensJiraConsole\Bin\Debug\net9.0
@ECHO ON
@Echo BUG_STATS
BensJiraConsole.exe BUG_STATS >> BensJiraConsole.log
@Echo NOESTIMATE
BensJiraConsole.exe NOESTIMATE >> BensJiraConsole.log
@Echo PMPLAN_BURNUP
BensJiraConsole.exe PMPLAN_BURNUPS >> BensJiraConsole.log
@Echo PMPLAN_RBURNUP
BensJiraConsole.exe PMPLAN_RBURNUP >> BensJiraConsole.log
@Echo PMPLAN_STORIES
BensJiraConsole.exe PMPLAN_STORIES >> BensJiraConsole.log
@Echo PMPLANS
BensJiraConsole.exe PMPLANS >> BensJiraConsole.log
@ECHO OFF
ECHO Finish time: %date% %time% >> BensJiraConsole.log
ECHO Finish time: %date% %time% 
pause
