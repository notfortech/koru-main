# Power BI: one dataset per client vs shared

## Current behaviour (single dataset)

- **One** Power BI workspace, dataset, and report is configured globally (appsettings / `.env`: `POWERBI_WORKSPACE_ID`, `POWERBI_DATASET_ID`, `ReportId`).
- When any client (e.g. AU-001 or AU-003) triggers “Refresh report”, the **same** dataset is refreshed.
- The embed URL and token are the **same** for every user.

So with the current setup, **all clients use the same dataset and report**. That only makes sense if:
- The dataset has a single data source and one client, or
- You use one dataset with multiple sources/parameters and row-level security (RLS) so each client sees only their data (more complex).

For separate data per client (e.g. AU-001 vs AU-003 each with their own master Excel and report), you need **one dataset (and report) per client**.

---

## Recommended: one dataset and report per client

- Each client (AU-001, AU-003, …) has its own:
  - **Master Excel** in blob: `AU-xxx/accounting/validated/Accounting_Master_auxxx.xlsx`
  - **Power BI dataset** that uses that Excel as the data source
  - **Power BI report** built on that dataset
- The app stores per client (e.g. in `Client` or config):
  - `PowerBIWorkspaceId` (workspace that contains this client’s dataset/report)
  - `PowerBIDatasetId`
  - `PowerBIReportId`
- **Refresh:** When a user triggers refresh for client AU-003, the backend refreshes **only** the dataset for AU-003 (using that client’s `PowerBIDatasetId` / workspace).
- **Embed:** When a user opens the report, the backend returns the embed URL and token for **that client’s** report (using that client’s `PowerBIReportId` / workspace).

So reports are separated by client: each client sees and refreshes only their own dataset and report.

---

## What was implemented in the codebase

1. **Client entity** has optional fields: `PowerBIWorkspaceId`, `PowerBIDatasetId`, `PowerBIReportId`. When set, they are used for that client’s refresh and embed.
2. **Python** `powerbi_refresh.refresh_dataset(client_id, workspace_id=None, dataset_id=None)` uses per-client workspace/dataset when provided; otherwise it falls back to the global env vars.
3. **C#** report refresh passes the client’s workspace/dataset from the DB into the Python script when available. Embed token endpoint uses the current user’s client to resolve which report to embed (and falls back to global settings if the client has no Power BI IDs set).

You can still run with a single shared dataset by leaving the client-level Power BI fields empty and relying on the global appsettings/`.env` configuration.
