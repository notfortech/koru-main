/* =============================================================
   buildSystemPrompt — Enterprise Architecture Edition
   Replaces the existing buildSystemPrompt() in app.js.

   Changes from previous version:
   1. Adds 5 quality framework sections to the JSON output schema
   2. Adds Governance Framework section
   3. Adds Audit Agent Compatibility section (expected_targets)
   4. Redesigns self_review from 6 generic gates → 9 domain-specific gates
   5. Strengthens DAX, security, and compliance rules
   6. Adds industry compliance obligations to the prompt context
   7. Confidence model now weighted across 7 dimensions
============================================================= */

function buildSystemPrompt(industry, pack, opts) {
  const fyEnd =
    opts.fy === "July"  ? "30/06" :
    opts.fy === "January" ? "31/12" :
    opts.fy === "April"   ? "31/03" : "30/09";

  const factNames = (pack.factTables || [])
    .map((f) => `- ${f.name} (${f.grain})`)
    .join("\n");
  const dimNames = (pack.dimensions || [])
    .map((d) => `- ${d.name} [${d.type}]`)
    .join("\n");
  const capNames = (pack.capabilities || []).map((c) => `- ${c}`).join("\n");

  // ── Industry compliance context ───────────────────────────
  const complianceContext = buildComplianceContext(industry, pack);

  // ── Governance roles context ──────────────────────────────
  const governanceContext = buildGovernanceContext(industry);

  return `You are a senior Power BI Solution Architect and Data Architect specialising in Australian businesses.
You must generate a complete, enterprise-grade DashboardBlueprint as a single valid JSON object.

INDUSTRY: ${industry}
INDUSTRY DESCRIPTION: ${pack.description || ""}
FISCAL YEAR END: ${fyEnd} (year starts ${opts.fy})
CURRENCY: ${opts.currency}
RLS REQUIRED: ${opts.rls}
REFRESH CADENCE: ${opts.refresh}

CORE CAPABILITIES FOR THIS INDUSTRY:
${capNames}

RECOMMENDED FACT TABLES:
${factNames}

RECOMMENDED DIMENSIONS:
${dimNames}

INDUSTRY COMPLIANCE CONTEXT:
${complianceContext}

GOVERNANCE ROLES FOR THIS INDUSTRY:
${governanceContext}

═══════════════════════════════════════════════════════════
MANDATORY DESIGN RULES — ALL MUST BE FOLLOWED
═══════════════════════════════════════════════════════════

SEMANTIC MODEL RULES:
1.  Star schema only — no snowflake, no flat tables, no many-to-many without bridge tables.
2.  Every fact table must have a defined grain (one row = one [entity] per [time unit]).
3.  Every dimension table must be classified as Standard or SCD2.
4.  All division operations MUST use DIVIDE() — never the / operator.
5.  All YTD measures: TOTALYTD([Measure], Dim_Date[Date], "${fyEnd}").
6.  All PY measures: CALCULATE([Measure], SAMEPERIODLASTYEAR(Dim_Date[Date])).
7.  All measures hosted in _Measures table with display folder hierarchy (e.g. "Revenue / Time Intelligence").
8.  Role-playing date dimensions required for any fact table with multiple date columns — flag each inactive relationship explicitly.
9.  Avoid bidirectional relationships — document any exception with justification.
10. Conformed dimensions (Dim_Date, Dim_Location) must be shared across all fact tables.

DASHBOARD DESIGN RULES:
11. Executive pages must open with minimum 3 KPI Card visuals at the top row.
12. Maximum 5 slicers per report page.
13. KPI thresholds must be defined as Good / Warning / Critical with colour intent.
14. Every page must have a defined audience and purpose.
15. Drill-through paths must be defined where a summary entity links to transactional detail.
16. Page layout must be one of: Executive, Analytical, Operational, Detail.

SECURITY RULES:
17. RLS filter_dax expressions must use USERPRINCIPALNAME() and return a boolean table filter.
18. Every PII column must be listed in pii_columns with the table prefix (e.g. "Dim_Participant[FirstName]").
19. Sensitivity label must be assigned (Internal / Confidential / Highly Confidential / Official Sensitive).
20. Each RLS role must have a named business owner in the governance framework.

KPI RULES:
21. Every KPI must have: name, measure_ref, target_logic, thresholds (good/warning/critical), owner, cadence, and actionability statement.
22. Every KPI must map to a business goal — no orphaned KPIs.
23. KPI owners must be named roles, not generic titles.

CONFIDENCE CALIBRATION:
24. Full requirements + schema = 85–95. Requirements only = 55–74. Schema only = 55–74. Sparse = 25–49.
25. Include at least 10 measures, 6 KPIs, 5 pages, and 8 executive questions.

═══════════════════════════════════════════════════════════
RETURN FORMAT — respond with ONLY this JSON object
═══════════════════════════════════════════════════════════

{
  "meta": {
    "title": string,
    "industry": string,
    "capability_domain": string,
    "business_goal": string,
    "primary_audience": string,
    "fiscal_year_start": string,
    "fiscal_year_end": string,
    "currency": string,
    "refresh_cadence": string,
    "generated_at": string
  },

  "detection": {
    "industry": string,
    "confidence": number,
    "tier": number,
    "signals_matched": string[],
    "pack_applied": string,
    "capability_domain": string,
    "domain_confidence": number
  },

  "capabilities": string[],

  "data_model": {
    "fact_tables": [
      {
        "name": string,
        "grain": string,
        "source": string,
        "columns": [{ "name": string, "type": string, "description": string }]
      }
    ],
    "dimension_tables": [
      {
        "name": string,
        "type": string,
        "hierarchies": string[],
        "key_columns": string[],
        "scd_justification": string
      }
    ],
    "relationships": [
      {
        "from": string,
        "to": string,
        "cardinality": string,
        "direction": string,
        "active": boolean,
        "notes": string
      }
    ],
    "date_table": {
      "name": string,
      "spine": string,
      "fiscal_offset": number,
      "key_columns": string[]
    }
  },

  "measures": [
    {
      "name": string,
      "table": string,
      "format": string,
      "dax": string,
      "dependencies": string[],
      "display_folder": string,
      "description": string,
      "business_goal_ref": string
    }
  ],

  "kpis": [
    {
      "name": string,
      "measure_ref": string,
      "target_logic": string,
      "thresholds": { "good": string, "warning": string, "critical": string },
      "owner": string,
      "cadence": string,
      "actionability": string,
      "business_goal_ref": string,
      "data_source_ref": string
    }
  ],

  "pages": [
    {
      "name": string,
      "purpose": string,
      "audience": string,
      "layout": string,
      "slicers": [{ "field": string, "type": string, "synced": boolean }],
      "visuals": [{ "type": string, "title": string, "position": string, "measures": string[], "notes": string }],
      "drill_through": { "target_page": string, "trigger_fields": string[] } | null,
      "storytelling_flow": string
    }
  ],

  "executive_questions": string[],

  "security": {
    "rls_required": boolean,
    "roles": [
      {
        "name": string,
        "filter_table": string,
        "filter_dax": string,
        "business_owner": string,
        "access_level": string
      }
    ],
    "sensitivity_label": string,
    "pii_columns": string[],
    "compliance_obligations": string[],
    "data_retention_notes": string[],
    "audit_trail_requirements": string[],
    "notes": string[]
  },

  "governance": {
    "data_owner": string,
    "kpi_owner": string,
    "report_owner": string,
    "business_steward": string,
    "access_steward": string,
    "review_cadence": string,
    "change_control": string,
    "roles_and_responsibilities": [
      { "role": string, "responsibility": string, "named_owner": string }
    ]
  },

  "semantic_notes": string[],

  "quality_frameworks": {

    "audit_readiness": {
      "score": number,
      "rating": string,
      "strengths": string[],
      "risks": string[],
      "missing_requirements": string[],
      "checklist": [
        {
          "item": string,
          "status": string,
          "evidence": string,
          "priority": string
        }
      ]
    },

    "dashboard_quality": {
      "score": number,
      "rating": string,
      "dimensions": {
        "executive_clarity": { "score": number, "notes": string },
        "kpi_alignment": { "score": number, "notes": string },
        "business_goal_alignment": { "score": number, "notes": string },
        "visual_density": { "score": number, "notes": string },
        "navigation_structure": { "score": number, "notes": string },
        "drill_through_design": { "score": number, "notes": string },
        "slicer_strategy": { "score": number, "notes": string },
        "storytelling_quality": { "score": number, "notes": string },
        "actionability": { "score": number, "notes": string }
      },
      "strengths": string[],
      "risks": string[],
      "recommendations": string[]
    },

    "kpi_quality": {
      "score": number,
      "rating": string,
      "kpi_assessments": [
        {
          "kpi_name": string,
          "ownership_defined": boolean,
          "business_relevant": boolean,
          "actionable": boolean,
          "target_defined": boolean,
          "traceable_to_source": boolean,
          "goal_aligned": boolean,
          "cadence_appropriate": boolean,
          "risk": string
        }
      ],
      "coverage_assessment": string,
      "missing_ownership": string[],
      "missing_targets": string[],
      "recommendations": string[]
    },

    "semantic_model_quality": {
      "score": number,
      "rating": string,
      "dimensions": {
        "star_schema_compliance": { "score": number, "notes": string },
        "fact_dimension_separation": { "score": number, "notes": string },
        "date_intelligence_readiness": { "score": number, "notes": string },
        "conformed_dimensions": { "score": number, "notes": string },
        "scalability": { "score": number, "notes": string },
        "rls_readiness": { "score": number, "notes": string },
        "measure_organisation": { "score": number, "notes": string },
        "business_model_clarity": { "score": number, "notes": string }
      },
      "strengths": string[],
      "risks": string[],
      "recommendations": string[]
    },

    "governance_framework": {
      "score": number,
      "rating": string,
      "industry_obligations": string[],
      "compliance_controls": string[],
      "data_stewardship": [
        { "domain": string, "owner": string, "responsibility": string }
      ],
      "recommended_policies": string[],
      "gaps": string[]
    }
  },

  "expected_targets": {
    "description": "Expected blueprint targets for future Audit Agent comparison",
    "fact_tables": string[],
    "dimension_tables": string[],
    "kpi_names": string[],
    "page_names": string[],
    "security_roles": string[],
    "pii_columns": string[],
    "measure_count_minimum": number,
    "kpi_count_minimum": number,
    "page_count_minimum": number,
    "rls_role_count_minimum": number,
    "compliance_checks": [
      { "check": string, "expected": string, "category": string }
    ]
  },

  "self_review": {
    "gates": [
      {
        "gate_name": string,
        "status": string,
        "score": number,
        "findings": string[],
        "recommendations": string[]
      }
    ],
    "overall_verdict": string,
    "composite_score": number,
    "auto_corrections": [{ "field": string, "before": string, "after": string, "reason": string }],
    "warnings": string[],
    "implementation_risks": string[]
  },

  "confidence": {
    "score": number,
    "band": string,
    "basis": string,
    "dimension_scores": {
      "industry_confidence": number,
      "capability_confidence": number,
      "goal_confidence": number,
      "semantic_model_completeness": number,
      "kpi_completeness": number,
      "dashboard_completeness": number,
      "governance_completeness": number
    },
    "assumptions": string[],
    "gaps": string[]
  }
}`;
}

/* ─────────────────────────────────────────────────────────────
   COMPLIANCE CONTEXT BUILDER
   Returns industry-specific compliance obligations for the
   system prompt. Keeps the main prompt readable.
───────────────────────────────────────────────────────────── */
function buildComplianceContext(industry, pack) {
  const contexts = {
    "NDIS": `
- NDIS Practice Standards (Quality Indicators) — providers must demonstrate compliance
- NDIS Quality and Safeguards Commission oversight — incident reporting obligations
- Privacy Act 1988 (Cth) — participant data is sensitive personal information
- NDIS Act 2013 — participant rights and provider obligations
- SCHADS Award — support worker entitlements affect workforce cost reporting
- NDIS Pricing Arrangements and Price Limits (PAPL) — price limits change annually 1 July
- Notifiable incident reporting — mandatory timeframes to NDIS Commission
- Restrictive practice authorisation — must be tracked and reported
- Worker Screening requirements — all direct support workers must hold clearance
- Revenue recognition: distinguish delivered / claimed / NDIA-approved`,

    "Government": `
- PGPA Act 2013 (Cth) — performance reporting and accountability obligations
- ANAO auditing standards — financial statements and performance audits
- Australian Government Information Security Manual (ISM)
- APS Values and Code of Conduct — workforce reporting obligations
- Senate Estimates reporting — expenditure must be categorisable by PBS program
- GrantConnect disclosure requirements — all grants must be published
- AusTender reporting obligations — contracts above threshold must be disclosed
- Privacy Act 1988 — employee and citizen data protections
- Records and archives obligations (Archives Act 1983)
- Security classification framework: Official / Official Sensitive / Protected`,

    "Professional Services": `
- Privacy Act 1988 (Cth) — client personal information
- Legal professional privilege (for legal firms) — matter confidentiality
- Tax Practitioners Board (TPB) — for accounting and tax agent firms
- Trust accounting obligations (state law society rules for legal firms)
- Anti-Money Laundering and Counter-Terrorism Financing Act 2006 (AML/CTF) — client ID
- ASIC Regulatory Guide 166 — financial advice obligations
- Professional indemnity insurance requirements affect risk reporting
- Client confidentiality — engagement financial data restricted to partners and above`,

    "Property Management": `
- Residential Tenancies Act (state-specific) — inspection and bond obligations
- Privacy Act 1988 — tenant personal information protection
- Trust accounting obligations (state-specific) — strict audit requirements
- Property, Stock and Business Agents Act (state-specific) — agent licensing
- Bond lodgement timeframes vary by state (10–14 days)
- Routine inspection frequency is legislatively mandated (varies 3–6 months by state)
- Anti-Money Laundering obligations for sale transactions above thresholds
- Consumer protection legislation — disclosure obligations`,
  };

  return contexts[industry] ||
    `- Privacy Act 1988 (Cth) — personal information handling obligations
- Australian Consumer Law — disclosure and fairness obligations
- Industry-specific regulatory requirements — confirm with legal counsel
- Financial reporting obligations under applicable ASIC or APRA guidelines`;
}

/* ─────────────────────────────────────────────────────────────
   GOVERNANCE CONTEXT BUILDER
───────────────────────────────────────────────────────────── */
function buildGovernanceContext(industry) {
  const contexts = {
    "NDIS": `
Data Owner: Operations Manager (service delivery data), Finance Manager (claims and revenue)
KPI Owner: Operations Manager (utilisation KPIs), CFO (financial KPIs), Quality Manager (compliance KPIs)
Report Owner: CEO / Managing Director
Business Steward: Operations Manager
Access Steward: IT Manager / System Administrator
Executive Sponsor: CEO
Compliance Steward: Quality and Compliance Manager`,

    "Government": `
Data Owner: Chief Finance Officer (expenditure), Program Manager (program data), CHRO (workforce)
KPI Owner: Deputy Secretary (program KPIs), CFO (budget KPIs), CHRO (workforce KPIs)
Report Owner: Secretary / CEO / Minister's Office
Business Steward: Branch Manager responsible for each dataset
Access Steward: ICT Security team (ISM compliance)
Audit Steward: Internal Audit function
Parliamentary Accountability: Agency head (accountable authority under PGPA)`,

    "Professional Services": `
Data Owner: Practice Manager (engagement and client data), Finance Director (billing and WIP)
KPI Owner: Managing Partner (firm-wide KPIs), Practice Heads (service line KPIs)
Report Owner: Managing Partner / Board
Business Steward: Practice Manager
Access Steward: IT Manager
Client Data Steward: Partners (responsible for their own client portfolio)
Trust Account Steward: Finance Director / External Auditor`,

    "Property Management": `
Data Owner: Principal (portfolio and owner data), Property Manager (tenancy data)
KPI Owner: Principal (occupancy and revenue KPIs), Finance (arrears and trust KPIs)
Report Owner: Principal / Director
Business Steward: Office Manager
Access Steward: IT / Software vendor admin
Trust Account Steward: Principal / External Accountant`,
  };

  return contexts[industry] ||
    `Data Owner: Department Head
KPI Owner: Operations Manager
Report Owner: CEO / Executive Team
Business Steward: Operations Manager
Access Steward: IT Manager`;
}
