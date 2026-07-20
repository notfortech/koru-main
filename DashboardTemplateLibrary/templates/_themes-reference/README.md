# Theme reference — industries not yet scaffolded

Power BI theme JSON for 4 industries that don't have a template folder in
this library yet (legal, real-estate, healthcare, construction) — sourced
alongside `finance`'s theme and the `ndis`/`retail`/`professional-services`
updates in the same drop, but these 4 have no accompanying TMDL, so they
don't get a full `templates/<industry>/` folder of their own until one
exists. Kept here so the color/font work isn't lost if/when those
industries get scaffolded.

Each file matches Power BI's simplified theme schema (`dataColors`,
`background`, `foreground`, `tableAccent`) plus two non-standard fields —
`fontFamily`/`fontSize` — that Power BI Desktop's theme importer will
silently ignore rather than apply; they'd need moving into a `textClasses`
block to actually take effect. Same caveat applies to the theme files
already committed into `ndis`, `retail`, `professional-services`, and
`finance`.
