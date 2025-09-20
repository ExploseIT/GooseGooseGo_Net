
USE ggg_db
GO
-- Create the ggg_net as application pool in IIS
-- Check if the login exists at the server level
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = 'IIS APPPOOL\ggg_net')
BEGIN
    PRINT 'Creating SQL Server login for IIS APPPOOL\ggg_net...';
    CREATE LOGIN [IIS APPPOOL\ggg_net] FROM WINDOWS;
END
ELSE
BEGIN
    PRINT 'Login already exists.';
END
GO

-- Check if the user exists in the current database
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'IIS APPPOOL\ggg_net')
BEGIN
    PRINT 'Creating database user for IIS APPPOOL\ggg_net...';
    CREATE USER [IIS APPPOOL\ggg_net] FOR LOGIN [IIS APPPOOL\ggg_net];
    ALTER ROLE db_owner ADD MEMBER [IIS APPPOOL\ggg_net];
END
ELSE
BEGIN
    PRINT 'Database user already exists.';
END
GO
