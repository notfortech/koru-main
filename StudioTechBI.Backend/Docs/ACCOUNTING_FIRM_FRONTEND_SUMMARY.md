# Accounting Firm Mode – Frontend Changes Summary

## Backend support (done)

- **GET `/api/reports/accountant-clients`**  
  Returns the list of clients the current user can access (for accountants: multiple; for single-client users: one).  
  Response: `[{ clientId, clientCode, clientName }, ...]`  
  Use this when the "accounting firm" toggle is on to show the clients list and to know which `clientCode` to use for reports.

- **GET `/api/reports/available/{clientCodeOrId}`**  
  Now allows any client the user has access to (not only the JWT `client_code`).  
  Use for the selected client when in accounting firm mode: e.g. `GET /api/reports/available/AU-001` or `GET /api/reports/available/AU-006`.

- **GET `/api/powerbi/embed-token/{periodType}?period=...&clientCode=...`**  
  When `clientCode` is sent and the user has access to that client, the embed token is for that client’s report.  
  Use when opening the Reports tab for a specific client: e.g. `GET /api/powerbi/embed-token/monthly?clientCode=AU-001`.

- **Generate / Refresh / Process**  
  All report actions (generate, refresh, process) now accept any client the user has access to (single or from accountant list).

---

## Data setup: map AU-001 and AU-006 to the accountant

The accountant must be linked to **both** clients via **Company → Client**:

1. **Companies**  
   - At least two companies, each with a different `ClientId`:  
     - One company with `ClientId` = AU-001’s client Id  
     - One company with `ClientId` = AU-006’s client Id  

2. **CompanyUsers**  
   - The accountant user must be in both companies:  
     - One row: `UserId` = accountant user Id, `CompanyId` = company linked to AU-001  
     - One row: `UserId` = accountant user Id, `CompanyId` = company linked to AU-006  

Then `GET /api/reports/accountant-clients` will return both AU-001 and AU-006 for that user.

(If you use a single company per client, create two companies and two CompanyUser rows. If your model is different, ensure the accountant has User → CompanyUsers → Company → Client for both AU-001 and AU-006.)

---

## Frontend changes required

### 1. Accounting firm toggle (e.g. on Clients page or layout)

- Add a toggle (or setting): “Accounting firm” / “Multi-client” mode.
- When **on**:  
  - Treat the user as an accountant who can switch between clients.  
  - Call **GET `/api/reports/accountant-clients`** and use the returned list as the “my clients” list.  
- When **off**:  
  - Keep current behaviour: single client from login (e.g. from `GET /api/reports/available` or JWT `client_code`).

### 2. Clients page (when accounting firm is on)

- **Source of list**  
  - Use **GET `/api/reports/accountant-clients`** (not the admin clients API) so the list is scoped to the current user’s accessible clients (e.g. AU-001, AU-006).
- **Clicking a client**  
  - When the user clicks a client row (e.g. AU-001 or AU-006):  
    - Store the **selected client** (e.g. `clientCode` and optionally `clientId`, `clientName`) in app state or route (e.g. React state, context, or route param).  
    - Navigate to the **Reports** tab (or Reports page) and pass the selected client (e.g. `clientCode=AU-001` or `AU-006`) so the Reports tab knows which client’s report to load.

### 3. Reports tab (when opened from Clients with a selected client)

- **Selected client in scope**  
  - When the user lands on the Reports tab with a selected client (e.g. from the Clients page):  
    - Read the selected `clientCode` (and optionally `clientId` / `clientName`) from state or route.
- **Fetch “available” for that client**  
  - Call **GET `/api/reports/available/{clientCode}`** with that client, e.g.:  
    - `GET /api/reports/available/AU-001` or `GET /api/reports/available/AU-006`.  
  - Use the response for that client’s `powerBIReportId`, `powerBIDatasetId`, periods, etc., if needed in the UI.
- **Fetch embed token for that client**  
  - Call **GET `/api/powerbi/embed-token/monthly?clientCode=AU-001`** (or AU-006) so the token and embed URL are for the **selected** client’s report.
- **Render the report**  
  - Use the embed token and embed URL from the embed-token response to render the Power BI report for that client.  
  - Do **not** reuse a cached embed URL/token from a different client; always request a new embed token when the selected client changes.

### 4. Single-client mode (accounting firm off)

- Keep existing behaviour:  
  - Use **GET `/api/reports/available`** (no segment) for the current user’s single client.  
  - Call **GET `/api/powerbi/embed-token/monthly`** **without** `clientCode` so the backend uses the JWT `client_code`.  
  - No “client picker” on Reports; one report per user.

### 5. Optional: client selector on Reports tab (accounting firm on)

- If the user can switch client without going back to the Clients page:  
  - Show a dropdown or list of clients from **GET `/api/reports/accountant-clients`**.  
  - On change:  
    - Update selected client in state/route.  
    - Re-call **GET `/api/reports/available/{clientCode}`** and **GET `/api/powerbi/embed-token/monthly?clientCode=...`** and re-embed the report for the new client.

### 6. Navigation flow summary

- **Accounting firm ON**  
  1. User opens Clients page → call `GET /api/reports/accountant-clients` → show list (e.g. AU-001, AU-006).  
  2. User clicks a client (e.g. AU-006) → set selected client (e.g. `clientCode: "AU-006"`) → navigate to Reports tab.  
  3. Reports tab → call `GET /api/reports/available/AU-006` and `GET /api/powerbi/embed-token/monthly?clientCode=AU-006` → render AU-006’s report.  
  4. (Optional) User switches client in Reports tab → update selected client → re-fetch available + embed-token for new clientCode → re-render report.

- **Accounting firm OFF**  
  - As today: one client from login; Reports tab uses `/api/reports/available` and `/api/powerbi/embed-token/monthly` without `clientCode`.

---

## API quick reference

| Purpose                         | Method | Endpoint                                      | When to use |
|---------------------------------|--------|-----------------------------------------------|-------------|
| List current user’s clients     | GET    | `/api/reports/accountant-clients`            | Accounting firm on: clients list and client picker. |
| Available report info (single)  | GET    | `/api/reports/available`                     | Single-client mode. |
| Available report info (client)  | GET    | `/api/reports/available/{clientCodeOrId}`    | Accounting firm: for selected client (e.g. AU-001, AU-006). |
| Embed token (default client)    | GET    | `/api/powerbi/embed-token/monthly`            | Single-client mode. |
| Embed token (specific client)   | GET    | `/api/powerbi/embed-token/monthly?clientCode=AU-001` | Accounting firm: when showing that client’s report. |
| Generate / Refresh / Process    | POST   | `/api/reports/.../{clientId}`                 | Use the same clientId/clientCode the user has selected. |

All above require an authenticated user (JWT). The backend ensures the user can only access clients they are allowed (single client or accountant list).
