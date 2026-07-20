# Dashboard Template Library

This tree mirrors the folder structure to sync into Azure Blob Storage as the shared
dashboard-template catalog for 8 industries (NDIS, Government, Transport & Rail, Insurance,
Logistics, Retail, Professional Services / boutique consultancies, Finance). It is the "base directory"
the AI report-matching feature (`ReportMatchService` / `InsightsEngineController` in
`StudioTechBI.Backend`) searches against, and the source of truth an analyst opens in Power BI
Desktop to build the real `.pbix` for each template.

## Path convention

```
templates/<industry-slug>/<template-slug>/
    overview.pbix                        <- built manually in PBI Desktop from the TMDL below, then uploaded
    overview.SemanticModel/
        definition/
            database.tmdl
            model.tmdl
            relationships.tmdl
            expressions.tmdl
            cultures/en-US.tmdl
            tables/
                Fact_*.tmdl
                Dim_*.tmdl
                _Measures.tmdl
    overview_Template_v1.jpg             <- design/preview screenshot (see container note below)
    metadata.json                        <- industry, tier, owner, sensitivity, blob_path, status
    theme.json                           <- optional: Power BI report theme (see Themes below)
```

`templates/<industry-slug>/<template-slug>/overview.pbix` is **exactly** the `BlobPath` format
already hardcoded on seeded `Template` rows in
`StudioTechBI.Infrastructure/Data/SchemaModelSeeder.cs` (e.g.
`templates/ndis/participant-service-delivery/overview.pbix`) — new templates below follow the
same convention so nothing else in the backend needs to change to recognise them once a matching
`SchemaModel`/`Template` row exists.

## Which container

Two containers already exist and both currently touch templates, which is worth tidying up:

- **`clients`** — `BlobStorageService.UploadTemplateAsync` writes template files here, under the
  same container that holds every tenant's uploaded Excel data (`{clientId}/uploads/...`,
  `{clientId}/accounting/...`). This is what the seeded `Template.BlobPath` values resolve
  against today.
- **`report-templates`** — already exists and is used by `ReportTemplateAssetService` for
  template screenshots (`{TemplateName}_Template_v1.jpg`).

**Recommendation:** treat `report-templates` as the real home for the whole template library
(`.pbix` + `.SemanticModel` TMDL source + screenshot + `metadata.json`), not just screenshots.
It keeps the shared, non-tenant template catalog physically separate from client PII, so you can
apply a tighter, simpler access policy (e.g. read-only for the app's service principal,
write access limited to whoever builds templates) without needing path-prefix-scoped RBAC
conditions inside a container that also holds tenant data.

This is a **backend config change**, not just a file move — `BlobStorageService`'s
`ContainerName` constant (`"clients"`) would need to point template operations at
`report-templates` instead, and existing seeded `Template.BlobPath` values would keep working
unchanged since the path shape inside the container doesn't change. Flagging this as a follow-up
rather than doing it here, since it changes what the running app reads from. Until then, this
tree can be synced under `templates/` in the `clients` container exactly as-is with zero backend
changes — it's the same relative paths either way.

## What's in each template folder

- **`overview.SemanticModel/definition/*.tmdl`** — the semantic model half of a Power BI Project
  (star schema: fact tables, dimensions, a standard `Dim_Date` calendar table spanning
  2018-07-01 to 2030-06-30, relationships, and a `_Measures` table of DAX measures using
  `DIVIDE()` and `TOTALYTD()` throughout, with hierarchical display folders). This is what was
  asked for as "the TMDL files."
- **No `.Report` folder is included.** Turning TMDL into a working `.pbix` needs either:
  1. Tabular Editor 3 → *Open Model from Folder* (most forgiving with hand-authored TMDL), or
  2. A new blank Power BI Project (`.pbip`) in PBI Desktop, with its auto-generated
     `<name>.SemanticModel/definition/` folder replaced by the one here, then opened and
     **File → Save As → Power BI file (.pbix)**.
  Recommend validating with option 1 first — the TMDL syntax here wasn't round-tripped through a
  live PBI Desktop session, so treat it as a strong first draft rather than guaranteed-to-open.
- **`expressions.tmdl`** defines one text parameter, `SourceFilePath`, that every fact/dimension
  table's Power Query partition reads from. Point it at a real client workbook (or the matching
  sheet names listed in each table's `partition` block) before refreshing.
- **Fact/dimension column names match `SchemaModelSeeder.cs` exactly** for the 1 industry that
  was already seeded (NDIS). For the 7 new industries, column names are proposed — add a
  `SchemaModel`/`SchemaModelField`/`Template` row per `metadata.json`'s `schema_model_ref` note
  before the AI matching engine can find them against client uploads.

## Industries in this drop

| Industry (slug) | Template | Status |
|---|---|---|
| `ndis` | `participant-service-delivery` | SchemaModel already seeded — TMDL added here |
| `government` | `program-expenditure-oversight` | New industry, needs SchemaModel row |
| `transport` | `rail-network-operations` | New template under existing `Transport` industry |
| `insurance` | `policy-claims-commission` | New industry, needs SchemaModel row |
| `logistics` | `warehouse-freight-operations` | New industry, needs SchemaModel row |
| `retail` | `store-sales-inventory` | New industry, needs SchemaModel row |
| `professional-services` | `consulting-engagement-utilisation` | New industry (covers boutique consultancies), needs SchemaModel row |
| `finance` | `budget-variance-analytics` | New industry, needs SchemaModel row |

## Themes

`theme.json` (currently present for `ndis`, `retail`, `professional-services`,
and `finance`) matches Power BI's simplified theme schema —
`dataColors`, `background`, `foreground`, `tableAccent` — and imports
directly via Power BI Desktop's theme picker. Two extra fields,
`fontFamily`/`fontSize`, are **not** part of that schema and are silently
ignored on import; they'd need moving into a `textClasses` block to
actually apply. Four more industry themes with no template folder yet
(legal, real-estate, healthcare, construction) are kept under
`templates/_themes-reference/` so the color/font work isn't lost if those
industries get scaffolded later.

## Security notes

- Sensitivity labels and PII columns are recorded per template in `metadata.json` — NDIS,
  Insurance, and Professional Services carry participant/client PII and should not be world-
  readable even as "just a template" (real column *names* plus sample structure is still useful
  reconnaissance).
- None of these templates ship with real client data — every partition points at a placeholder
  `SourceFilePath` or a self-contained `CALENDAR()` expression, so the template library itself
  contains no tenant data regardless of which container it lands in.
- `Template.BlobPath` should only ever point at a `.pbix` that a human has verified is real and
  published (see `Template.IsPublishReady` in the backend) — none of the `.pbix` files exist yet,
  only their TMDL source. `metadata.json.status` is set to
  `semantic_model_ready_pending_pbix_build` for all 7 to make that explicit.
