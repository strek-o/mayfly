IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$(AppIdentityName)')
BEGIN
    DECLARE @sid NVARCHAR(100) = CONVERT(NVARCHAR(100), CONVERT(VARBINARY(16), CONVERT(UNIQUEIDENTIFIER, N'$(AppIdentityClientId)')), 1);
    EXEC (N'CREATE USER [$(AppIdentityName)] WITH SID = ' + @sid + N', TYPE = E');
END;

ALTER ROLE db_datareader ADD MEMBER [$(AppIdentityName)];
ALTER ROLE db_datawriter ADD MEMBER [$(AppIdentityName)];
