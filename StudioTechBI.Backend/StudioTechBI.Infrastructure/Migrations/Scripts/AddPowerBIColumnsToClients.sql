-- Add Power BI columns to Clients table (from migration AddClientPowerBIIds).
-- Run this if the migration hasn't been applied and you get "Invalid column name 'PowerBIDatasetId'" etc.

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Clients') AND name = 'PowerBIWorkspaceId')
    ALTER TABLE dbo.Clients ADD PowerBIWorkspaceId nvarchar(100) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Clients') AND name = 'PowerBIDatasetId')
    ALTER TABLE dbo.Clients ADD PowerBIDatasetId nvarchar(100) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Clients') AND name = 'PowerBIReportId')
    ALTER TABLE dbo.Clients ADD PowerBIReportId nvarchar(100) NULL;
