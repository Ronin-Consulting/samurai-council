-- ContosoRetailDW Database Initialization Script
-- This script restores the ContosoRetailDW database and creates a read-only user
--
-- Prerequisites:
--   1. ContosoRetailDW.bak must be placed in /var/opt/mssql/backup/
--   2. SQL Server must be running and healthy
--
-- Usage:
--   docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong!Passw0rd" -C -i /scripts/init-contoso-db.sql

USE [master]
GO

-- Check if database already exists
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'ContosoRetailDW')
BEGIN
    PRINT 'Restoring ContosoRetailDW database...'

    -- Restore the database from backup
    -- Note: The logical file names may vary depending on the backup version
    RESTORE DATABASE [ContosoRetailDW]
    FROM DISK = N'/var/opt/mssql/backup/ContosoRetailDW.bak'
    WITH FILE = 1,
         MOVE N'ContosoRetailDW2.0' TO N'/var/opt/mssql/data/ContosoRetailDW.mdf',
         MOVE N'ContosoRetailDW2.0_log' TO N'/var/opt/mssql/data/ContosoRetailDW.ldf',
         NOUNLOAD,
         REPLACE,
         STATS = 10;

    PRINT 'Database restored successfully.'
END
ELSE
BEGIN
    PRINT 'ContosoRetailDW database already exists. Skipping restore.'
END
GO

-- Create read-only login if it doesn't exist
IF NOT EXISTS (SELECT name FROM sys.server_principals WHERE name = N'samurai_reader')
BEGIN
    PRINT 'Creating samurai_reader login...'
    CREATE LOGIN [samurai_reader] WITH PASSWORD = N'Reader!Pass123',
        DEFAULT_DATABASE = [ContosoRetailDW],
        CHECK_EXPIRATION = OFF,
        CHECK_POLICY = OFF;
    PRINT 'Login created successfully.'
END
ELSE
BEGIN
    PRINT 'samurai_reader login already exists.'
END
GO

-- Switch to ContosoRetailDW database
USE [ContosoRetailDW]
GO

-- Create user for the login if it doesn't exist
IF NOT EXISTS (SELECT name FROM sys.database_principals WHERE name = N'samurai_reader')
BEGIN
    PRINT 'Creating samurai_reader user...'
    CREATE USER [samurai_reader] FOR LOGIN [samurai_reader];
    PRINT 'User created successfully.'
END
ELSE
BEGIN
    PRINT 'samurai_reader user already exists.'
END
GO

-- Grant SELECT permissions on dbo schema (read-only access)
PRINT 'Granting SELECT permissions on dbo schema...'
GRANT SELECT ON SCHEMA::dbo TO [samurai_reader];
GO

-- Deny any modification permissions explicitly (defense in depth)
DENY INSERT, UPDATE, DELETE, ALTER ON SCHEMA::dbo TO [samurai_reader];
GO

PRINT 'ContosoRetailDW initialization complete.'
PRINT 'Read-only user: samurai_reader'
PRINT 'Connection string: Server=sqlserver;Database=ContosoRetailDW;User Id=samurai_reader;Password=Reader!Pass123;TrustServerCertificate=True'
GO
