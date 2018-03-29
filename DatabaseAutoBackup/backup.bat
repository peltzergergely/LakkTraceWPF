@echo off
for /f "tokens=1-3 delims=/ " %%i in ("%date%") do (
	set year=%%i
	set month=%%j
	set day=%%k
)

set datestr=%year%%month%%day%
set datestr=%datestr:.=%

SET PGPASSWORD=admin

echo on
start /min bin\pg_dump -h localhost -p 5432 -U postgres -F c -b -v -f "C:\Users\rsoos\Desktop\backup\CCDB_%datestr%.backup" CCDB

	