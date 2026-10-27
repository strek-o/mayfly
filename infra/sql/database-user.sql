IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$(IdentityName)')
BEGIN
    DECLARE @sid NVARCHAR(100) = CONVERT(NVARCHAR(100), CONVERT(VARBINARY(16), CONVERT(UNIQUEIDENTIFIER, N'$(IdentityClientId)')), 1);
    EXEC (N'CREATE USER [$(IdentityName)] WITH SID = ' + @sid + N', TYPE = E');
END;

ALTER ROLE db_datareader ADD MEMBER [$(IdentityName)];
ALTER ROLE db_datawriter ADD MEMBER [$(IdentityName)];
