
@echo off
setlocal

set SERVER=localhost
set DATABASE=ggg_db

echo Running update scripts against %DATABASE% on %SERVER%...

sqlcmd -S %SERVER% -d %DATABASE% -E -i "ggg_assetprofit.sql"
sqlcmd -S %SERVER% -d %DATABASE% -E -i "ggg_init.sql"
rem sqlcmd -S %SERVER% -d %DATABASE% -E -i "spUserCreateByUsernameFirebase.sql"
rem sqlcmd -S %SERVER% -d %DATABASE% -E -i "schminder_user_views.sql"
rem sqlcmd -S %SERVER% -d %DATABASE% -E -i "schminder_migrate_finish.sql"

echo Done!
pause