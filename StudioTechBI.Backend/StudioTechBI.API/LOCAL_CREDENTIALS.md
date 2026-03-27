# Local config and Azure SQL

## Which file is used for the database connection?

Configuration is merged in this order (later overrides earlier):

1. **appsettings.json** – base config (e.g. LocalDB connection string).
2. **appsettings.Development.json** – environment overrides (e.g. `UseDemoStorage: true`).
3. **appsettings.Local.json** – optional, loaded last; **overrides** the above.

So **appsettings.Local.json wins** when it exists. Put your Azure SQL connection string and `UseDemoStorage: false` in **appsettings.Local.json** (gitignored) so secrets are not committed. Keep **appsettings.json** with safe defaults (e.g. LocalDB) for when Local is not present.

When you run the app, the console logs which database is used and the connection server (e.g. Azure server name or "In-memory (demo)").

## Option A: Demo mode (in-memory, no database)

- Set **UseDemoStorage**: **true** in appsettings.Local.json or appsettings.Development.json.
- Users come from **DemoUsers.json**. No connection string needed.

## Option B: Azure SQL Database

1. **Create appsettings.Local.json** (copy from appsettings.Local.example.json if needed). This file is gitignored so your connection string is not committed.

2. **Set UseDemoStorage to false** and add your Azure SQL connection string:
   ```json
   {
     "UseDemoStorage": false,
     "ConnectionStrings": {
      "DefaultConnection": "Server=tcp:YOUR_SERVER.database.windows.net,1433;Database=YOUR_DATABASE;User ID=YOUR_USER;Password=YOUR_PASSWORD;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
     },
     "JwtSettings": {
      "SecretKey": "",
       "Issuer": "StudioTechBI",
       "Audience": "StudioTechBIUsers",
       "AccessTokenExpirationMinutes": 60,
       "RefreshTokenExpirationDays": 7
     }
   }
   ```

3. **Get the connection string from Azure Portal:**
   - Open your Azure SQL server → your database.
   - Go to **Connection strings** (under Settings).
   - Copy the **ADO.NET** connection string and replace placeholders: `YOUR_SERVER`, `YOUR_DATABASE`, `YOUR_USER`, `YOUR_PASSWORD`.

4. **Firewall:** In Azure Portal → SQL server → **Networking**, add your client IP (or allow Azure services) so the app can connect.

5. **Run the API.** On first run it will apply EF migrations and seed roles. Then you can register users via the API or add them in the database.

**JwtSettings** are required in both modes (use the same structure as in the example above).
