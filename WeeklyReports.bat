cls
@ECHO OFF
ECHO Start time: %date% %time%
cd \Development\BensEngineeringMetrics
git checkout master
git pull
dotnet build
cd \Development\BensEngineeringMetrics\Bin\Debug\net9.0
BensEngineeringMetricsConsole.exe BUG_STATS
BensEngineeringMetricsConsole.exe INCIDENTS
BensEngineeringMetricsConsole.exe INIT_ALL
BensEngineeringMetricsConsole.exe SPRINT_PLAN

ECHO Finish time: %date% %time%

