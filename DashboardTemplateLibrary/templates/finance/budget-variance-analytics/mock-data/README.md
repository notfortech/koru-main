# Mock data reference

`mock-data-reference.json` — 200 fact rows (`facts`) plus dimension rows
(`dimensions.Dim_Department`, `dimensions.Dim_Product`) matching this
template's TMDL column names exactly. Not wired into any automated
mock-data endpoint — load it into an Excel workbook with sheets named
`Transactions`, `Departments`, `Products` (matching the `Item=` names each
table's Power Query partition reads via `SourceFilePath`) to get a working
sample dataset for testing the semantic model in Power BI Desktop before a
real client connects.
