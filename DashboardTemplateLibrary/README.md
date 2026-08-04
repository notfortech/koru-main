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

## HTML report templates (`templates/html/`)

A second, sibling prefix inside this same tree holds interactive HTML/CSS/JS report templates —
the primary output format for the Report Generator (deterministic and AI-assisted paths), matched
against a client's uploaded data the same way Power BI templates are matched, just against this
separate library. Read-only to every service at all times; a client's own copy is only ever made
when they explicitly click "Save Report" (see `HtmlReportAssemblyService`/`SavedReportsController`
in the backend), and that copy lands in `{clientId}/saved-reports/...`, never back into this tree.

```
templates/html/index.json                  <- flat array of template ids (author-maintained;
                                                IBlobStorageService has no "list blobs by prefix"
                                                capability, so discovery reuses this registration-
                                                file convention instead)
templates/html/<template-id>/
    manifest.json    <- { id, name, industry, requires: {minNumeric,minDate,minCategorical},
                           requiredColumns, optionalColumns,
                           dataContract: { rowFields: [{column,alias,role}], maxRows },
                           testIds: { resultsLoaded, kpiPrefix, chartPrefix } }
    chrome.html      <- self-contained HTML/CSS/JS; reads its data via
                         JSON.parse(document.getElementById('stbi-report-data').textContent) —
                         koru-main injects that <script type="application/json"> block by
                         substituting the <!--STBI_REPORT_DATA--> marker comment at generation
                         time. Never assumes outbound network access.
    preview.jpg      <- optional
```

`testIds` is mandatory — a manifest missing it is excluded from matching at registry-load time
(fail closed), since it's what keeps the Report Validation Playwright runner able to assert
against the rendered template.

Adding a template needs no redeploy of any repo: upload `manifest.json` + `chrome.html` to
`templates/html/<template-id>/` in the same blob container/path family every Power BI template
lives in, add its id to `templates/html/index.json`, and it's matchable within ~5 minutes (see
`HtmlTemplateRegistrySyncService` in the backend).

Two templates currently exist:
- `retail-single-page` — single-page retail commercial performance dashboard (chip filters,
  budget-line bars, monthly trend, category × channel matrix). Fully data-driven — filter chips,
  month range, and category/channel lists are all derived from whatever rows the matched dataset
  actually contains, not a fixed hardcoded set.
- `healthcare-fpna-multi-tab` — multi-tab healthcare FP&A dashboard (Executive Summary /
  Expenditure Detail / Funding & Sustainability). Executive Summary and Expenditure Detail are
  driven by real client data; Funding & Sustainability needs a second, differently-shaped row
  table (funding by channel/funder) that the current single-table row export pipeline doesn't
  produce yet, so that one tab renders with no data until multi-table export exists — closest-
  match, not fully interactive, per this library's own coverage policy for templates added ahead
  of full data-contract support.

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
