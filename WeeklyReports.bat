cls
@ECHO OFF
ECHO Start time: %date% %time%
cd \Development\BensJiraConsole\Bin\Debug\net9.0
BensJiraConsole.exe BUG_STATS 
BensJiraConsole.exe NOESTIMATE
BensJiraConsole.exe PMPLAN_BURNUPS 
BensJiraConsole.exe PMPLAN_RBURNUP 
BensJiraConsole.exe PMPLAN_STORIES 
BensJiraConsole.exe PMPLANS 
BensJiraConsole.exe INIT_BURNUPS 
BensJiraConsole.exe SPRINT_PLAN

ECHO Finish time: %date% %time% 
pause
