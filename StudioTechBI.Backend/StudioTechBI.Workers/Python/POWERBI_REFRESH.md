# Power BI dataset refresh and report data

## Why the refresh API might fail

`POST .../groups/{workspace}/datasets/{dataset}/refreshes` can fail for:

1. **Wrong or missing .env**
   - `POWERBI_WORKSPACE_ID` and `POWERBI_DATASET_ID` must match the workspace and dataset in Power BI.
   - Get them: Power BI Service → Workspace → Dataset settings, or from the dataset URL.

2. **Dataset not in that workspace**
   - The dataset must live in the workspace given by `POWERBI_WORKSPACE_ID`.

3. **No permission to refresh**
   - The app (client id/secret in .env) must be able to refresh the dataset:
     - **Option A:** Add the app as a **Workspace Admin** (Workspace → Access → Add and assign “Admin”).
     - **Option B:** Use a **dataset “Read and refresh”** or “Execute queries” permission (e.g. via API or Power BI admin).

4. **Token failure**
   - Wrong `POWERBI_TENANT_ID`, `POWERBI_CLIENT_ID`, or `POWERBI_CLIENT_SECRET`; or app not allowed to use Power BI API. Check Azure AD app registration and API permissions (Power BI Service).

When it fails, the script now prints the full response (status + body) and the URL to stderr, which appears in the API refresh response `log`. Use that to fix workspace/dataset IDs or permissions.

---

## Transformations (date format, etc.) and measures

- **Where they live:** All transformations (Power Query, date formats, calculated columns) and **measures** are defined in the **Power BI dataset/report** (.pbix), not in this Python code.
- **What the refresh does:** The refresh only tells Power BI: “reload data from the current data source(s) for this dataset.” Power BI runs the existing Power Query and loads the new rows; it does not change your transformations or measures.
- **Measures:** Measures are part of the dataset model. As long as the **shape of the data** (column names and types) stays the same, measures and report visuals keep working. New rows with the same schema are fine.
- **What to keep consistent:** The **data source** (e.g. Excel in blob) and its **schema** (column names, types, date formats in the source) should match what the report was built for. If you change column names or types in the master Excel, update the Power Query in the dataset so the model and measures still get the right fields.

So: transformations and measures stay intact as long as the data source and schema stay consistent; the refresh only pulls new data into the existing model.
