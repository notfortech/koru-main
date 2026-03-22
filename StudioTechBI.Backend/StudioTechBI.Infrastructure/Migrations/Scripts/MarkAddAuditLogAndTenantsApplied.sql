-- Run this ONLY if AuditLogs (and preferably Tenants) already exist in the database
-- and the app fails with "There is already an object named 'AuditLogs' in the database."
-- This marks the migration as applied so EF will not try to create the tables again.

IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260314101047_AddAuditLogAndTenants')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260314101047_AddAuditLogAndTenants', N'8.0.0');
END
GO
