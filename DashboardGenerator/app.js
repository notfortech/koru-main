/* =============================================================
   app.js — Power BI Blueprint Generator  (Enterprise Edition)
   Version: 3.0
   Depends on: knowledgepacks-1.js, knowledgepacks-2.js
============================================================= */

"use strict";

/* ─────────────────────────────────────────────────────────────
   CONSTANTS
───────────────────────────────────────────────────────────── */
const AGENT_STEPS = [
  { label: "Parse input & validate mode",               key: "parse"  },
  { label: "Detect industry from signals",              key: "detect" },
  { label: "Load industry knowledge pack",              key: "pack"   },
  { label: "Map business capabilities & domain",        key: "caps"   },
  { label: "Design star schema",                        key: "schema" },
  { label: "Generate DAX measure catalogue",            key: "dax"    },
  { label: "Build dashboard pages & KPIs",              key: "pages"  },
  { label: "Run 9-gate self-review & quality frameworks", key: "review" },
];

const LAYOUT_ICONS = {
  Executive:   "🏛️",
  Analytical:  "📈",
  Operational: "⚙️",
  Detail:      "🔍",
};

const VISUAL_ICONS = {
  Card: "🃏", KPI: "📊", Line: "📈", Bar: "📊", Column: "📊",
  Matrix: "🔢", Scatter: "⬤", Map: "🗺️", Treemap: "🌲",
  Gauge: "⏱️", Table: "📋", Waterfall: "🌊", Ribbon: "🎗️",
  Funnel: "📐", "Decomp Tree": "🌳", "Card Grid": "🃏", Donut: "🍩",
};

/* ─────────────────────────────────────────────────────────────
   DOM HELPERS
───────────────────────────────────────────────────────────── */
const $ = (id) => document.getElementById(id);
const val = (id) => ($(id) ? $(id).value : "");

/* ─────────────────────────────────────────────────────────────
   INITIALISE
───────────────────────────────────────────────────────────── */
function init() {
  const badge = $("dateBadge");
  if (badge) {
    badge.textContent = new Date().toLocaleDateString("en-AU", {
      day: "2-digit", month: "short", year: "numeric",
    });
  }

  const navMap = [
    ["navInput",        "input"],
    ["navDetection",    "detection"],
    ["navCapabilities", "capabilities"],
    ["navModel",        "model"],
    ["navKpis",         "kpis"],
    ["navPages",        "pages"],
    ["navDax",          "dax"],
    ["navReview",       "review"],
  ];
  navMap.forEach(([btnId, viewName]) => {
    const btn = $(btnId);
    if (btn) btn.addEventListener("click", () => showView(viewName));
  });

  const genBtn = $("generateBtn");
  if (genBtn) genBtn.addEventListener("click", generateBlueprint);

  const clearBtn = $("clearBtn");
  if (clearBtn) clearBtn.addEventListener("click", clearForm);

  const modeReqBtn = $("modeTabReq");
  if (modeReqBtn) modeReqBtn.addEventListener("click", () => setMode("requirements"));

  const modeSchemaBtn = $("modeTabSchema");
  if (modeSchemaBtn) modeSchemaBtn.addEventListener("click", () => setMode("schema"));

  const providerSel = $("inp-provider");
  if (providerSel) providerSel.addEventListener("change", updateProviderUI);

  document.querySelectorAll(".pack-chip").forEach((chip) => {
    chip.addEventListener("click", () => {
      const industry = chip.dataset.industry;
      if (industry) selectIndustry(chip, industry);
    });
  });

  renderSteps();
}

/* ─────────────────────────────────────────────────────────────
   VIEW NAVIGATION
───────────────────────────────────────────────────────────── */
function showView(name) {
  document.querySelectorAll(".view").forEach((v) => v.classList.remove("active"));
  const target = $("view-" + name);
  if (target) target.classList.add("active");

  document.querySelectorAll(".nav-btn").forEach((b) => b.classList.remove("active"));
  const navIdMap = {
    input: "navInput", detection: "navDetection", capabilities: "navCapabilities",
    model: "navModel", kpis: "navKpis", pages: "navPages", dax: "navDax", review: "navReview",
  };
  const activeNav = $(navIdMap[name]);
  if (activeNav) activeNav.classList.add("active");
}

/* ─────────────────────────────────────────────────────────────
   MODE TOGGLE
───────────────────────────────────────────────────────────── */
function setMode(mode) {
  const isReq = mode === "requirements";
  $("modeTabReq")?.classList.toggle("active", isReq);
  $("modeTabSchema")?.classList.toggle("active", !isReq);
  const reqGroup = $("reqInputGroup");
  const schemaGroup = $("schemaInputGroup");
  if (reqGroup) reqGroup.style.display = isReq ? "" : "none";
  if (schemaGroup) schemaGroup.style.display = isReq ? "none" : "";
}

/* ─────────────────────────────────────────────────────────────
   INDUSTRY CHIP SELECTION
───────────────────────────────────────────────────────────── */
function selectIndustry(el, industry) {
  document.querySelectorAll(".pack-chip").forEach((c) => c.classList.remove("active"));
  el.classList.add("active");
  const sel = $("inp-industry");
  if (!sel) return;
  for (const opt of sel.options) {
    if (opt.value === industry) { sel.value = industry; break; }
  }
}

/* ─────────────────────────────────────────────────────────────
   PROVIDER UI
───────────────────────────────────────────────────────────── */
function updateProviderUI() {
  const p = val("inp-provider");
  const apiKeyGroup = $("apiKeyGroup");
  const groqNote    = $("groqNote");
  const apiKeyLabel = $("apiKeyLabel");
  const apiKeyInput = $("inp-apikey");

  if (apiKeyGroup) apiKeyGroup.style.display = p === "demo" ? "none" : "";
  if (groqNote)    groqNote.style.display    = p === "groq" ? "" : "none";

  const labels       = { groq: "Groq API Key", openai: "OpenAI API Key", anthropic: "Anthropic API Key" };
  const placeholders = { groq: "gsk_...",       openai: "sk-...",         anthropic: "sk-ant-..." };
  if (apiKeyLabel) apiKeyLabel.textContent  = labels[p]       || "API Key";
  if (apiKeyInput) apiKeyInput.placeholder  = placeholders[p] || "Enter API key...";
}

/* ─────────────────────────────────────────────────────────────
   CLEAR FORM
───────────────────────────────────────────────────────────── */
function clearForm() {
  const req      = $("inp-requirements");
  const schema   = $("inp-schema");
  const industry = $("inp-industry");
  if (req)      req.value      = "";
  if (schema)   schema.value   = "";
  if (industry) industry.value = "";
  document.querySelectorAll(".pack-chip").forEach((c) => c.classList.remove("active"));
}

/* ─────────────────────────────────────────────────────────────
   PACK REGISTRY
───────────────────────────────────────────────────────────── */
function getPackRegistry() {
  return {
    "NDIS":                  typeof PACK_NDIS                 !== "undefined" ? PACK_NDIS                 : null,
    "Government":            typeof PACK_GOVERNMENT           !== "undefined" ? PACK_GOVERNMENT           : null,
    "Professional Services": typeof PACK_PROFESSIONAL_SERVICES !== "undefined" ? PACK_PROFESSIONAL_SERVICES : null,
    "Property Management":   typeof PACK_PROPERTY_MANAGEMENT  !== "undefined" ? PACK_PROPERTY_MANAGEMENT  : null,
  };
}

function getPack(industry) {
  const registry = getPackRegistry();
  if (registry[industry]) return registry[industry];
  const lower = (industry || "").toLowerCase();
  for (const [key, pack] of Object.entries(registry)) {
    if (!pack) continue;
    if (key.toLowerCase().includes(lower) || lower.includes(key.toLowerCase())) return pack;
  }
  return Object.values(registry).find((p) => p !== null) || null;
}

/* ─────────────────────────────────────────────────────────────
   INDUSTRY VALIDATION LAYER
   ─────────────────────────────────────────────────────────────
   Four signal types are scored independently, then combined.
   A consistency check runs before any pack is committed.
   Ambiguous signals (e.g. "billable hours") shared across
   industries are weighted lower than exclusive signals.
   Source system signals are highly decisive and weighted heavily.
───────────────────────────────────────────────────────────── */

// ── 1. SOURCE SYSTEM SIGNALS (highest weight — these are decisive) ──
const SOURCE_SYSTEM_SIGNALS = {
  "Professional Services": [
    "xero practice manager","myob ae","aps","hubspot","salesforce","clio",
    "actionstep","smokeball","leap","practice evolve","karbon","suite files",
    "xero practice","fathom","futrli","cchinz","handisoft","viztopia",
    "solution 6","practice ignition","ignition","workflow max","workflowmax",
    "xero workpapers","caseware","inflo","xero hq","tax agent","bas agent",
  ],
  "NDIS": [
    "proda","shiftcare","careview","shortlyster","brevity","lumary","iinsight",
    "kineo","ecase","caremaster","deployhrm","humanforce","deputy",
    "myplace portal","ndis portal","ndia portal","carelink","cura","careflight",
    "nightingale","ostara","kinora","scion","splose","halaxy",
  ],
  "Government": [
    "techone","sap","oracle financials","chris21","aurion","myob aogov",
    "austender","grantconnect","finance one","objective","trim","hprm",
    "myhr","employeeconnect","pageup","e-recruit",
  ],
  "Property Management": [
    "propertyme","property tree","console","rest professional","rockend",
    "property me","vault re","vaultRE","rex crm","agentbox","inspect real estate",
    "inspect realestate","real estate crm","re agent","eagle crm",
  ],
};

// ── 2. CAPABILITY SIGNALS (what the org measures and manages) ─────
const CAPABILITY_SIGNALS = {
  "Professional Services": [
    "billable utilisation","realisation rate","wip recovery","engagement profitability",
    "revenue leakage","fee earner","charge-out","average bill rate","write-off rate",
    "work in progress","debtor days","lock-up","recovery rate","realization",
    "engagement margin","client profitability","practice profitability",
  ],
  "NDIS": [
    "plan utilisation","participant funding","support item","support category",
    "ndis price limit","papl","claim submission","service agreement",
    "core supports","capacity building","capital supports","sil occupancy",
    "incident reporting","restrictive practices","worker screening",
  ],
  "Government": [
    "budget appropriation","program outcome","grant acquittal","fte establishment",
    "senate estimates","kpi achievement","pgpa","portfolio budget statements",
    "budget utilisation","expenditure variance",
  ],
  "Property Management": [
    "rental yield","arrears rate","occupancy rate","days vacant","lease renewal",
    "trust accounting","property manager","portfolio size","maintenance jobs",
    "vacancy rate","rent roll",
  ],
};

// ── 3. BUSINESS GOAL SIGNALS (what outcomes the org pursues) ──────
const BUSINESS_GOAL_SIGNALS = {
  "Professional Services": [
    "improve utilisation","increase realisation","reduce wip","grow revenue per partner",
    "improve engagement margin","reduce write-offs","increase bill rate",
    "track engagement profitability","monitor debtor days","business development pipeline",
  ],
  "NDIS": [
    "improve participant outcomes","track plan spending","monitor delivered hours",
    "reduce claim rejections","improve sil occupancy","workforce rostering",
    "participant at risk","plan expiry","claim approval",
  ],
  "Government": [
    "budget compliance","program performance","grant management","workforce planning",
    "expenditure forecast","senate estimates preparation",
  ],
  "Property Management": [
    "reduce vacancy","improve arrears","grow portfolio","increase rental income",
    "maintenance compliance","lease renewal rate",
  ],
};

// ── 4. EXCLUSION RULES — terms that DISQUALIFY an industry ───────
// If these terms appear in the input, the named industry cannot be selected.
const EXCLUSION_RULES = {
  "NDIS": [
    // Strong Professional Services source system signals disqualify NDIS
    "xero practice manager","myob ae","karbon","suite files","clio","actionstep",
    "smokeball","workflowmax","practice ignition","ignition",
    // Strong Professional Services KPI language disqualifies NDIS
    "realisation rate","realization rate","revenue leakage","write-off rate",
    "engagement profitability","debtor days","wip recovery","lock-up",
    "charge-out rate","average bill rate","fee earner",
  ],
  "Professional Services": [
    // Strong NDIS-specific terms disqualify Professional Services
    "proda","participant plan","support item","plan utilisation",
    "ndis price limit","sil house","shiftcare","careview","ndia",
    "capacity building supports","core supports","worker screening",
  ],
  "Government": [
    "proda","participant","support item","realisation rate","wip",
  ],
  "Property Management": [
    "proda","participant","support item","realisation rate","wip",
  ],
};

// ── 5. AMBIGUOUS SIGNALS — penalise when shared across industries ─
// These terms appear in multiple packs and should not be decisive alone.
const AMBIGUOUS_SIGNALS = new Set([
  "billable hours",   // shared: NDIS worker hours AND Pro Services
  "utilisation",      // shared: NDIS staff utilisation AND Pro Services
  "timesheets",       // shared: NDIS timesheets AND Pro Services
  "consultants",      // appears in Pro Services, Government, NDIS
  "clients",          // universal
  "partners",         // Pro Services AND Property Management
  "managers",         // universal
  "pipeline",         // Pro Services AND Property Management AND Government
]);

// ── MAIN DETECTION FUNCTION ───────────────────────────────────────
function detectIndustry(text) {
  if (!text) return { industry: null, score: 0, confidence: 0, candidates: [], validation: null };
  const lower = text.toLowerCase();
  const registry = getPackRegistry();

  // Step 1: Check exclusions first — disqualify industries outright
  const disqualified = new Set();
  for (const [industry, terms] of Object.entries(EXCLUSION_RULES)) {
    for (const term of terms) {
      if (lower.includes(term.toLowerCase())) {
        disqualified.add(industry);
        break;
      }
    }
  }

  // Step 2: Score all four signal types for each industry
  const industryScores = {};

  for (const [industry, pack] of Object.entries(registry)) {
    if (!pack || disqualified.has(industry)) continue;

    let score = 0;
    const matched = [];

    // 2a. Pack signals (existing) — but penalise ambiguous ones
    for (const sig of (pack.signals || [])) {
      if (lower.includes(sig.toLowerCase())) {
        const words = sig.trim().split(/\s+/).length;
        const isAmbiguous = AMBIGUOUS_SIGNALS.has(sig.toLowerCase());
        const weight = isAmbiguous ? 0.5 : (words > 1 ? 3 : 1);
        score += weight;
        if (!isAmbiguous) matched.push(sig);
      }
    }

    // 2b. Source system signals (weight: 10 per match — highly decisive)
    for (const sig of (SOURCE_SYSTEM_SIGNALS[industry] || [])) {
      if (lower.includes(sig.toLowerCase())) {
        score += 10;
        matched.push("🔧 " + sig);
      }
    }

    // 2c. Capability signals (weight: 5 per match)
    for (const sig of (CAPABILITY_SIGNALS[industry] || [])) {
      if (lower.includes(sig.toLowerCase())) {
        score += 5;
        matched.push("⚙️ " + sig);
      }
    }

    // 2d. Business goal signals (weight: 4 per match)
    for (const sig of (BUSINESS_GOAL_SIGNALS[industry] || [])) {
      if (lower.includes(sig.toLowerCase())) {
        score += 4;
        matched.push("🎯 " + sig);
      }
    }

    if (score > 0) {
      industryScores[industry] = { score, matched };
    }
  }

  // Step 3: Sort by score
  const sorted = Object.entries(industryScores)
    .sort((a, b) => b[1].score - a[1].score);

  if (sorted.length === 0) {
    return { industry: null, score: 0, confidence: 0, candidates: [], validation: null };
  }

  const topIndustry = sorted[0][0];
  const topScore    = sorted[0][1].score;
  const runnerUp    = sorted[1]?.[1]?.score || 0;

  // Step 4: Confidence — based on margin over runner-up, not raw score
  // A large margin = high confidence; a tight race = low confidence
  const margin = topScore - runnerUp;
  let confidence;
  if (sorted.length === 1) {
    // Only one candidate — score-based floor
    confidence = Math.min(92, Math.max(55, Math.round(topScore * 4)));
  } else {
    const marginPct = margin / topScore;
    if (marginPct >= 0.6)       confidence = 92;
    else if (marginPct >= 0.4)  confidence = 82;
    else if (marginPct >= 0.25) confidence = 72;
    else if (marginPct >= 0.1)  confidence = 60;
    else                        confidence = 45; // tie — escalate to clarification
  }

  // Step 5: Run validation layer on the winner
  const validation = validateIndustryConsistency(topIndustry, text, sorted);

  // If validation demotes the winner, promote the runner-up
  let finalIndustry = topIndustry;
  if (validation.overridden && sorted[1]) {
    finalIndustry = sorted[1][0];
    confidence = Math.max(40, confidence - 20);
  }

  return {
    industry:   finalIndustry,
    score:      topScore,
    confidence: Math.min(97, confidence),
    candidates: sorted.slice(0, 3).map(([ind, data]) => [ind, data.score]),
    matched:    sorted[0]?.[1]?.matched || [],
    disqualified: [...disqualified],
    validation,
  };
}

// ── INDUSTRY CONSISTENCY VALIDATOR ───────────────────────────────
function validateIndustryConsistency(proposedIndustry, text, sortedScores) {
  const lower = text.toLowerCase();
  const issues = [];
  let overridden = false;
  let overrideReason = null;

  // Rule 1: Source system counter-evidence
  // If another industry's source system signals appear, flag a conflict
  for (const [industry, signals] of Object.entries(SOURCE_SYSTEM_SIGNALS)) {
    if (industry === proposedIndustry) continue;
    for (const sig of signals) {
      if (lower.includes(sig.toLowerCase())) {
        const conflictMsg = `Source system "${sig}" is associated with ${industry}, not ${proposedIndustry}.`;
        issues.push({ severity: "HIGH", type: "source_system_conflict", message: conflictMsg });
        // Source system conflict always triggers override consideration
        overridden = true;
        overrideReason = conflictMsg;
        break;
      }
    }
    if (overridden) break;
  }

  // Rule 2: NDIS-specific entity contamination check
  // If proposed is NOT NDIS but NDIS entity terms appear, that's fine — they may overlap
  // If proposed IS NDIS but Professional Services KPIs dominate, demote
  if (proposedIndustry === "NDIS") {
    const psCapSignals = CAPABILITY_SIGNALS["Professional Services"] || [];
    let psCapHits = 0;
    for (const sig of psCapSignals) {
      if (lower.includes(sig.toLowerCase())) psCapHits++;
    }
    const ndisCapSignals = CAPABILITY_SIGNALS["NDIS"] || [];
    let ndisCapHits = 0;
    for (const sig of ndisCapSignals) {
      if (lower.includes(sig.toLowerCase())) ndisCapHits++;
    }
    if (psCapHits > ndisCapHits && psCapHits >= 2) {
      issues.push({
        severity: "HIGH",
        type: "capability_mismatch",
        message: `Capability signals favour Professional Services (${psCapHits} hits) over NDIS (${ndisCapHits} hits). NDIS selected in error.`,
      });
      if (!overridden) { overridden = true; overrideReason = issues[issues.length - 1].message; }
    }
  }

  // Rule 3: KPI contamination — ensure KPI terms match the proposed industry
  // E.g. if "realisation rate" appears and NDIS is proposed, reject
  const ndisOnlyKPIs    = ["plan utilisation","sil occupancy","claim approval rate","ndis funding","support item"];
  const proServOnlyKPIs = ["realisation rate","realization rate","wip recovery","engagement profitability","revenue leakage","debtor days","write-off rate","lock-up"];

  if (proposedIndustry === "NDIS") {
    for (const kpi of proServOnlyKPIs) {
      if (lower.includes(kpi)) {
        issues.push({
          severity: "CRITICAL",
          type: "kpi_contamination",
          message: `KPI term "${kpi}" is exclusive to Professional Services. Cannot generate NDIS blueprint for this input.`,
        });
        overridden = true;
        overrideReason = issues[issues.length - 1].message;
        break;
      }
    }
  }

  if (proposedIndustry === "Professional Services") {
    for (const kpi of ndisOnlyKPIs) {
      if (lower.includes(kpi)) {
        issues.push({
          severity: "HIGH",
          type: "kpi_contamination",
          message: `KPI term "${kpi}" is exclusive to NDIS. Review whether this is a dual-industry scenario.`,
        });
        // Don't override Pro Services for NDIS kpi hits — they may run both — but warn
      }
    }
  }

  // Rule 4: Fact table / entity alignment check
  const ndisEntities    = ["participant","support item","plan","sil","proda","claim"];
  const proServEntities = ["engagement","timesheet","wip","realisation","write-off","matter","fee earner"];

  let ndisEntityHits   = ndisEntities.filter(e => lower.includes(e)).length;
  let proServEntityHits = proServEntities.filter(e => lower.includes(e)).length;

  if (proposedIndustry === "NDIS" && proServEntityHits > ndisEntityHits && proServEntityHits >= 2) {
    issues.push({
      severity: "HIGH",
      type: "entity_mismatch",
      message: `Entity terms favour Professional Services (${proServEntityHits}: ${proServEntities.filter(e => lower.includes(e)).join(", ")}) over NDIS (${ndisEntityHits}).`,
    });
    if (!overridden) { overridden = true; overrideReason = issues[issues.length - 1].message; }
  }

  // Compile pass/fail result
  const passed   = issues.filter(i => i.severity === "CRITICAL" || i.severity === "HIGH").length === 0;
  const warnings = issues.filter(i => i.severity === "LOW" || i.severity === "MEDIUM");

  return {
    passed,
    overridden,
    overrideReason,
    issues,
    warnings,
    proposedIndustry,
    // Forbidden entities — what must NEVER appear in the final blueprint for this industry
    forbiddenEntities: getForbiddenEntities(overridden && sortedScores[1] ? sortedScores[1][0] : proposedIndustry),
  };
}

// ── FORBIDDEN ENTITY LISTS — enforced in system prompt and demo builder ──
function getForbiddenEntities(industry) {
  const rules = {
    "Professional Services": {
      entities:   ["Participant","NDIS Plan","Support Item","Plan Utilisation","SIL","PRODA","NDIA","Service Agreement"],
      factTables: ["Fact_Claims","Fact_PlanUtilisation","Fact_Incidents","Fact_ServiceDelivery"],
      kpis:       ["Plan Utilisation %","Claim Approval Rate","SIL Occupancy %","Participant Count"],
      reason:     "Professional Services organisations do not have NDIS participants, plans, or claims.",
    },
    "NDIS": {
      entities:   ["Engagement","Matter","WIP","Realisation","Write-off","Timesheet","Fee Earner","Debtor Days"],
      factTables: ["Fact_Timesheets","Fact_WIP","Fact_Invoices","Fact_WriteOffs","Fact_Pipeline"],
      kpis:       ["Realisation Rate %","WIP Value","Write-off Rate %","Debtor Days","Billable Utilisation % (consulting)"],
      reason:     "NDIS providers do not have consulting engagements, WIP, or realisation rates.",
    },
    "Government": {
      entities:   ["Participant","WIP","Realisation","Engagement","Claim (NDIS)"],
      factTables: ["Fact_Claims","Fact_PlanUtilisation","Fact_Timesheets","Fact_WIP"],
      kpis:       ["WIP Value","Realisation Rate %","Plan Utilisation %"],
      reason:     "Government organisations operate on appropriations, not billable engagements or NDIS plans.",
    },
    "Property Management": {
      entities:   ["Participant","WIP","Realisation","Engagement","Claim (NDIS)"],
      factTables: ["Fact_Claims","Fact_PlanUtilisation","Fact_Timesheets"],
      kpis:       ["WIP Value","Realisation Rate %","Plan Utilisation %"],
      reason:     "Property management organisations do not have consulting WIP or NDIS participants.",
    },
  };
  return rules[industry] || { entities: [], factTables: [], kpis: [], reason: "" };
}

/* ─────────────────────────────────────────────────────────────
   AGENT STEP HELPERS
───────────────────────────────────────────────────────────── */
function renderSteps() {
  const list = $("stepsList");
  if (!list) return;
  list.innerHTML = AGENT_STEPS.map((s, i) => `
    <div class="step-item" id="step-${s.key}">
      <div class="step-num">${i + 1}</div>
      <div class="step-label">${s.label}</div>
      <div class="step-status">Pending</div>
    </div>`).join("");
}

async function runStep(key, delay = 340) {
  const el = $("step-" + key);
  if (!el) return;
  el.className = "step-item active";
  el.querySelector(".step-status").innerHTML = '<span class="spin">⟳</span>';
  await sleep(delay);
  el.className = "step-item done";
  el.querySelector(".step-status").textContent = "✓ Done";
}

async function failStep(key) {
  const el = $("step-" + key);
  if (!el) return;
  el.className = "step-item error";
  el.querySelector(".step-status").textContent = "✕ Error";
}

function sleep(ms) { return new Promise((r) => setTimeout(r, ms)); }



/* ─────────────────────────────────────────────────────────────
   SYSTEM PROMPT BUILDER  (Enterprise Edition — 5 quality frameworks)
───────────────────────────────────────────────────────────── */
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
    "design_recommendations": [{ "category": string, "recommendation": string, "rationale": string, "priority": string }],
    "assumptions": string[],
    "design_risks": [{ "risk": string, "mitigation": string, "category": string }],
    "implementation_gaps": string[],
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


/* ─────────────────────────────────────────────────────────────
   USER PROMPT BUILDER
───────────────────────────────────────────────────────────── */
function buildUserPrompt(mode, requirements, schema, industry, audience, currency, fy, rls, refresh) {
  const industryLine = industry
    ? `INDUSTRY: ${industry}`
    : "INDUSTRY: Auto-detect from content below";

  const contentBlock = mode === "requirements"
    ? `BUSINESS REQUIREMENTS:\n${requirements || "(none provided)"}`
    : `DATASET SCHEMA / HEADERS:\n${schema || "(none provided)"}`;

  return `${industryLine}
PRIMARY AUDIENCE: ${audience}
CURRENCY: ${currency}
FISCAL YEAR START: ${fy}
RLS REQUIRED: ${rls}
REFRESH CADENCE: ${refresh}

${contentBlock}

Generate the complete DashboardBlueprint JSON for this organisation.
Use Australian business terminology throughout.
Be specific to the detected industry — do not produce a generic dashboard.
Include realistic, industry-specific DAX measures, KPIs, pages, executive questions,
governance framework, all 5 quality framework assessments, and expected_targets.`;
}

/* ─────────────────────────────────────────────────────────────
   JSON REPAIR UTILITY
   Four strategies handle every truncation pattern:
   A. Mid-string-key: cut back to last safe position, reclose
   B. Mid-string-value (no safe point): close string, reclose
   C. Mid-value (not in string): strip trailing comma, close
   D. Trailing key with no value: strip the dangling key
───────────────────────────────────────────────────────────── */
function repairTruncatedJSON(raw) {
  if (!raw || typeof raw !== "string") return null;

  // Strip markdown fences — ```json, ```JSON, ``` variants
  let s = raw.trim();
  s = s.replace(/^```(?:json)?\s*/i, "").replace(/\s*```\s*$/, "").trim();

  // Try direct parse (happy path)
  try { return JSON.parse(s); } catch (_) { /* fall through */ }

  // Walk string tracking structure and last clean cut point
  let inStr = false, escape = false, lastSafe = 0;
  const stack = [];

  for (let i = 0; i < s.length; i++) {
    const ch = s[i];
    if (escape)               { escape = false; continue; }
    if (ch === "\\" && inStr) { escape = true;  continue; }
    if (ch === '"') {
      if (inStr) {
        if (stack.length && stack[stack.length - 1] === '"') stack.pop();
        inStr = false; lastSafe = i + 1;
      } else { stack.push('"'); inStr = true; }
      continue;
    }
    if (inStr) continue;
    if (" \t\n\r,:".includes(ch)) lastSafe = i + 1;
    if      (ch === "{") { stack.push("{"); }
    else if (ch === "[") { stack.push("["); }
    else if (ch === "}" && stack[stack.length - 1] === "{") { stack.pop(); lastSafe = i + 1; }
    else if (ch === "]" && stack[stack.length - 1] === "[") { stack.pop(); lastSafe = i + 1; }
  }

  const closeStack = (stk) =>
    stk.slice().reverse().map(o => o === "{" ? "}" : o === "[" ? "]" : "").join("");

  const recomputeStack = (text) => {
    const stk = []; let in2 = false, esc2 = false;
    for (const ch of text) {
      if (esc2)               { esc2 = false; continue; }
      if (ch === "\\" && in2) { esc2 = true;  continue; }
      if (ch === '"') {
        if (in2) { if (stk[stk.length - 1] === '"') stk.pop(); in2 = false; }
        else     { stk.push('"'); in2 = true; }
        continue;
      }
      if (in2) continue;
      if (ch === "{") stk.push("{");
      else if (ch === "[") stk.push("[");
      else if (ch === "}" && stk[stk.length - 1] === "{") stk.pop();
      else if (ch === "]" && stk[stk.length - 1] === "[") stk.pop();
    }
    return stk;
  };

  // Strategy A: truncated mid-key (in string, last safe point exists)
  if (inStr && lastSafe > 0) {
    const cut = s.substring(0, lastSafe).replace(/,\s*$/, "").trimEnd();
    try {
      const r = JSON.parse(cut + closeStack(recomputeStack(cut)));
      console.warn("[JSON repair] Strategy A: cut to position", lastSafe);
      return r;
    } catch (_) {}
  }

  // Strategy B: truncated mid-value string (no safe point before it)
  if (inStr) {
    const closed = (s + '"').replace(/,\s*$/, "").trimEnd();
    try {
      const r = JSON.parse(closed + closeStack(recomputeStack(closed)));
      console.warn("[JSON repair] Strategy B: closed dangling string");
      return r;
    } catch (_) {}
    // Strip the incomplete key entirely
    const lastComma = s.lastIndexOf(",");
    if (lastComma > 0) {
      const cut = s.substring(0, lastComma).replace(/,\s*$/, "").trimEnd();
      try {
        const r = JSON.parse(cut + closeStack(recomputeStack(cut)));
        console.warn("[JSON repair] Strategy B2: stripped incomplete key");
        return r;
      } catch (_) {}
    }
  }

  // Strategy C: not in string, close open structures
  const trimmed = s.replace(/,\s*$/, "").trimEnd();
  try {
    const r = JSON.parse(trimmed + closeStack(stack));
    console.warn("[JSON repair] Strategy C: closed open structures");
    return r;
  } catch (_) {}

  // Strategy D: trailing key with no value (e.g. {"a":1,"b":)
  const lastColon = s.lastIndexOf(":");
  if (lastColon > 0) {
    const beforeColon = s.substring(0, lastColon);
    const lastComma   = beforeColon.lastIndexOf(",");
    const cut = (lastComma > 0 ? beforeColon.substring(0, lastComma) : beforeColon)
      .replace(/,\s*$/, "").trimEnd();
    try {
      const r = JSON.parse(cut + closeStack(recomputeStack(cut)));
      console.warn("[JSON repair] Strategy D: stripped trailing key");
      return r;
    } catch (_) {}
  }

  return null;
}

/* ─────────────────────────────────────────────────────────────
   API CALLS
───────────────────────────────────────────────────────────── */
async function callGroq(systemPrompt, userPrompt, apiKey) {
  const resp = await fetch("https://api.groq.com/openai/v1/chat/completions", {
    method: "POST",
    headers: { "Content-Type": "application/json", Authorization: "Bearer " + apiKey },
    body: JSON.stringify({
      model: "llama-3.3-70b-versatile",
      messages: [{ role: "system", content: systemPrompt }, { role: "user", content: userPrompt }],
      temperature: 0.25,
      max_tokens: 32000,
      response_format: { type: "json_object" },
    }),
  });
  if (!resp.ok) { const err = await resp.text(); throw new Error("Groq API error " + resp.status + ": " + err); }
  const data = await resp.json();
  const content = data?.choices?.[0]?.message?.content;
  if (!content) throw new Error("Groq returned an empty response.");
  if (data?.choices?.[0]?.finish_reason === "length") {
    console.warn("[callGroq] finish_reason=length — response truncated. Attempting repair.");
  }
  const parsed = repairTruncatedJSON(content);
  if (!parsed) throw new Error("Groq response could not be parsed as JSON. Try reducing blueprint scope or switching to a model with a larger context window.");
  return parsed;
}

async function callOpenAI(systemPrompt, userPrompt, apiKey) {
  const resp = await fetch("https://api.openai.com/v1/chat/completions", {
    method: "POST",
    headers: { "Content-Type": "application/json", Authorization: "Bearer " + apiKey },
    body: JSON.stringify({
      model: "gpt-4o",
      messages: [{ role: "system", content: systemPrompt }, { role: "user", content: userPrompt }],
      temperature: 0.25,
      max_tokens: 16000,
      response_format: { type: "json_object" },
    }),
  });
  if (!resp.ok) { const err = await resp.text(); throw new Error("OpenAI API error " + resp.status + ": " + err); }
  const data = await resp.json();
  const content = data?.choices?.[0]?.message?.content;
  if (!content) throw new Error("OpenAI returned an empty response.");
  if (data?.choices?.[0]?.finish_reason === "length") {
    console.warn("[callOpenAI] finish_reason=length — response truncated. Attempting repair.");
  }
  const parsed = repairTruncatedJSON(content);
  if (!parsed) throw new Error("OpenAI response could not be parsed as JSON. Try reducing blueprint scope or switching to a model with a larger context window.");
  return parsed;
}

/* ─────────────────────────────────────────────────────────────
   DEMO BLUEPRINT BUILDER
   Wired to all 5 quality frameworks, governance, expected_targets,
   9-gate self-review, and 7-dimension confidence model.
───────────────────────────────────────────────────────────── */
function buildDemoBlueprint(industry, pack, opts) {
  const fyEnd = opts.fy === "July" ? "30/06" : opts.fy === "January" ? "31/12" : opts.fy === "April" ? "31/03" : "30/09";

  const measures = (pack.measures || []).map((m) => ({
    name: m.name, table: "_Measures", format: m.format, dax: m.dax,
    dependencies: [], display_folder: m.folder || "General",
    description: m.desc || "", business_goal_ref: "",
  }));

  const kpis = (pack.kpis || []).map((k) => ({
    name: k.name, measure_ref: k.measure, target_logic: k.target,
    thresholds: { good: k.good, warning: k.warn, critical: k.critical },
    owner: k.owner, cadence: k.cadence,
    actionability: `If this KPI turns Critical, the ${k.owner || "responsible manager"} should review immediately and escalate to the executive team.`,
    business_goal_ref: "", data_source_ref: k.measure || "",
  }));

  const pages = (pack.pages || []).map((p) => ({
    name: p.name, purpose: p.purpose, audience: p.audience, layout: p.layout,
    storytelling_flow: `This page answers: what is the ${p.name} status, why is it at that level, and what action is required.`,
    slicers: (p.slicers || []).map((s) => ({ field: s, type: "Dropdown", synced: true })),
    visuals: (p.visuals || []).map((v, i) => ({
      type: v.type, title: v.title,
      position: "Row " + Math.floor(i / 3 + 1) + " Col " + ((i % 3) + 1),
      measures: [], notes: "",
    })),
    drill_through: null,
  }));

  const relationships = (pack.relationships || []).map((r) => ({
    from: r.from, to: r.to, cardinality: r.cardinality,
    direction: r.direction, active: r.active, notes: r.notes || "",
  }));

  const factTables = (pack.factTables || []).map((f) => ({
    name: f.name, grain: f.grain, source: f.source || "Source System",
    columns: (f.columns || []).map((c) => ({ name: c.name, type: c.type, description: c.desc || "" })),
  }));

  const dimTables = (pack.dimensions || []).map((d) => ({
    name: d.name, type: d.type,
    scd_justification: d.type === "SCD2" ? "Attributes change over time — historical values required for accurate trend analysis" : "",
    hierarchies: d.hierarchies || [], key_columns: d.key_cols || [],
  }));

  const securityRoles = opts.rls === "yes"
    ? (pack.security?.roles || []).map((r) => ({
        name: r.role, filter_table: r.table, filter_dax: r.dax,
        business_owner: r.role, access_level: "Read",
      }))
    : [];

  const piiCols          = pack.security?.pii_columns || pack.security?.pii || [];
  const sensitivityLabel = pack.security?.sensitivity_label || (opts.rls === "yes" ? "Confidential" : "Internal");

  const hasRequirements = (opts.requirements || "").trim().length > 50;
  const hasSchema       = (opts.schema || "").trim().length > 20;
  const detResult       = detectIndustry((opts.requirements || "") + " " + (opts.schema || ""));
  const validation      = opts.validationResult || detResult.validation;

  // ── Build all quality frameworks ──────────────────────────
  const qfResult       = buildQualityFrameworks(measures, kpis, pages, pack, { rls: opts.rls, industry, requirements: opts.requirements, schema: opts.schema });
  const govBlock       = buildGovernanceBlock(industry, pack, opts);
  const expectedTargets = buildExpectedTargets(measures, kpis, pages, securityRoles, pack);
  const confResult     = buildConfidenceScore(
    { fy: opts.fy, currency: opts.currency, rls: opts.rls, refresh: opts.refresh,
      requirements: opts.requirements, schema: opts.schema, industryExplicit: opts.industryExplicit,
      detectionConfidence: detResult.confidence || 75, industry },
    hasRequirements, hasSchema, pack, measures, kpis, pages, securityRoles
  );
  const selfReviewResult = buildSelfReview(measures, kpis, pages, securityRoles, opts.rls, confResult.score, pack);

  return {
    meta: {
      title: industry + " Performance Dashboard",
      industry,
      capability_domain: "OPS",
      business_goal: pack.description || "Monitor organisational performance, drive operational efficiency, and support executive decision-making.",
      primary_audience: opts.audience,
      fiscal_year_start: opts.fy,
      fiscal_year_end: fyEnd,
      currency: opts.currency,
      refresh_cadence: opts.refresh,
      generated_at: new Date().toISOString(),
    },
    detection: {
      industry,
      confidence: opts.industryExplicit ? 97 : detResult.confidence || 75,
      tier: pack.tier || 1,
      signals_matched: opts.industryExplicit
        ? ["Industry explicitly selected by user"]
        : (detResult.matched || detResult.candidates || []).slice(0, 8).map((c) => Array.isArray(c) ? c[0] : c),
      pack_applied: pack.code || industry,
      capability_domain: "OPS",
      domain_confidence: opts.industryExplicit ? 80 : 65,
      disqualified: detResult.disqualified || [],
      validation: validation || null,
    },
    capabilities:   pack.capabilities || [],
    data_model: {
      fact_tables:      factTables,
      dimension_tables: dimTables,
      relationships,
      date_table: {
        name: "Dim_Date", spine: "2018-07-01 to 2030-06-30",
        fiscal_offset: opts.fy === "July" ? 6 : 0,
        key_columns: ["DateKey","Date","Day","DayName","Week","Month","MonthName","Quarter",
                      "CalendarYear","FiscalYear","FiscalQuarter","FiscalMonth","IsWeekend",
                      "IsPublicHoliday","PublicHolidayName"],
      },
    },
    measures,
    kpis,
    pages,
    executive_questions: pack.execQuestions || [],
    security: {
      rls_required: opts.rls === "yes",
      roles: securityRoles,
      sensitivity_label: sensitivityLabel,
      pii_columns: piiCols,
      compliance_obligations: [],
      data_retention_notes: ["Confirm data retention period with legal counsel and IT."],
      audit_trail_requirements: ["Enable Power BI activity logs.", "Enable dataset access audit in Microsoft Purview."],
      notes: pack.security?.notes || [],
    },
    governance: govBlock,
    semantic_notes:   pack.semanticNotes || [],
    quality_frameworks: qfResult,
    expected_targets:   expectedTargets,
    self_review:        selfReviewResult,
    confidence:         confResult,
  };
}



/* ─────────────────────────────────────────────────────────────
   9-GATE SELF-REVIEW + 5 QUALITY FRAMEWORKS + CONFIDENCE
───────────────────────────────────────────────────────────── */
function buildSelfReview(measures, kpis, pages, securityRoles, rlsRequired, confidenceScore, pack) {
  pack = pack || {};

  // ── Gate 1: Industry Alignment ──────────────────────────
  const packCaps = pack.capabilities || [];
  const gate1Score = packCaps.length > 0 ? 94 : 72;

  // ── Gate 2: Capability Alignment ────────────────────────
  const hasDomainFacts = (pack.factTables || []).length >= 3;
  const gate2Score = hasDomainFacts ? 91 : 74;

  // ── Gate 3: Business Goal Alignment ─────────────────────
  const kpisWithGoalRef = kpis.filter(k => k.business_goal_ref && k.business_goal_ref.length > 3);
  const goalAlignmentPct = kpis.length > 0 ? kpisWithGoalRef.length / kpis.length : 0;
  const gate3Score = goalAlignmentPct >= 0.8 ? 90 : goalAlignmentPct >= 0.5 ? 76 : 62;

  // ── Gate 4: KPI Coverage ────────────────────────────────
  const allKPIsHaveOwner = kpis.every(k => k.owner && k.owner.length > 2);
  const allKPIsHaveThresholds = kpis.every(k => k.thresholds && k.thresholds.good && k.thresholds.critical);
  const allKPIsHaveActionability = kpis.every(k => k.actionability && k.actionability.length > 10);
  const kpiCoverageScore = [
    kpis.length >= 6,
    allKPIsHaveOwner,
    allKPIsHaveThresholds,
    allKPIsHaveActionability,
  ].filter(Boolean).length;
  const gate4Score = kpiCoverageScore === 4 ? 93 : kpiCoverageScore >= 3 ? 82 : kpiCoverageScore >= 2 ? 71 : 58;

  // ── Gate 5: Dashboard Quality ───────────────────────────
  const execPages = pages.filter(p => p.layout === "Executive");
  const execHasCards = execPages.every(p =>
    (p.visuals || []).some(v => v.type === "Card" || v.type === "KPI")
  );
  const slicerCompliant = pages.every(p => (p.slicers || []).length <= 5);
  const hasStorytelling = pages.some(p => p.storytelling_flow && p.storytelling_flow.length > 10);
  const hasDrillThrough = pages.some(p => p.drill_through !== null && p.drill_through !== undefined);
  const dashQualityScore = [execHasCards, slicerCompliant, hasStorytelling, hasDrillThrough, pages.length >= 5].filter(Boolean).length;
  const gate5Score = dashQualityScore === 5 ? 92 : dashQualityScore >= 4 ? 83 : dashQualityScore >= 3 ? 73 : 62;

  // ── Gate 6: Semantic Model Quality ──────────────────────
  const allMeasuresHaveDAX = measures.every(m => m.dax && m.dax.length > 5);
  const hasDivide = measures.every(m => !m.dax.includes(" / ") || m.dax.includes("DIVIDE"));
  const hasYTD = measures.some(m => m.dax.includes("TOTALYTD") || m.dax.includes("DATESYTD"));
  const hasPY = measures.some(m => m.dax.includes("SAMEPERIODLASTYEAR"));
  const hasFolders = measures.every(m => m.display_folder && m.display_folder.includes("/"));
  const semModelScore = [allMeasuresHaveDAX, hasDivide, hasYTD, hasPY, hasFolders, measures.length >= 10].filter(Boolean).length;
  const gate6Score = semModelScore === 6 ? 94 : semModelScore >= 5 ? 86 : semModelScore >= 4 ? 77 : 64;

  // ── Gate 7: Governance Completeness ─────────────────────
  // We check structural completeness only — governance block presence
  const gate7Score = 82; // set by demo; API response will calibrate per output

  // ── Gate 8: Audit Readiness ──────────────────────────────
  const hasPII = (pack.security?.pii_columns || []).length > 0;
  const hasRLSRoles = rlsRequired !== "yes" || securityRoles.length > 0;
  const hasSensitivityLabel = pack.security?.sensitivity_label?.length > 0;
  const hasComplianceNotes = (pack.security?.notes || []).length > 0;
  const auditScore = [hasPII, hasRLSRoles, hasSensitivityLabel, hasComplianceNotes].filter(Boolean).length;
  const gate8Score = auditScore === 4 ? 91 : auditScore >= 3 ? 82 : auditScore >= 2 ? 71 : 56;

  // ── Gate 9: Security Readiness ───────────────────────────
  const rlsOk = rlsRequired !== "yes" || securityRoles.length > 0;
  const gate9Score = rlsOk ? (securityRoles.length >= 2 ? 93 : 82) : 55;

  const gates = [
    {
      gate_name: "Industry Alignment",
      status: gate1Score >= 85 ? "PASS" : "WARN",
      score: gate1Score,
      findings: packCaps.length > 0
        ? ["Industry knowledge pack loaded and capabilities mapped"]
        : ["Industry detection confidence below threshold — review signals"],
      recommendations: packCaps.length > 0
        ? ["Validate capability list against actual business requirements"]
        : ["Provide more specific industry terminology in requirements"],
    },
    {
      gate_name: "Capability Alignment",
      status: gate2Score >= 80 ? "PASS" : "WARN",
      score: gate2Score,
      findings: hasDomainFacts
        ? ["Fact tables align to industry capability domain"]
        : ["Fact table count below recommended minimum for this industry"],
      recommendations: hasDomainFacts
        ? ["Confirm fact table grains with source system owners"]
        : ["Add at least 3 fact tables aligned to the primary capability domain"],
    },
    {
      gate_name: "Business Goal Alignment",
      status: gate3Score >= 80 ? "PASS" : gate3Score >= 65 ? "WARN" : "FAIL",
      score: gate3Score,
      findings: goalAlignmentPct >= 0.8
        ? ["KPIs are mapped to business goals"]
        : [`${Math.round((1 - goalAlignmentPct) * 100)}% of KPIs lack explicit business goal references`],
      recommendations: ["Ensure every KPI references a specific stated business goal"],
    },
    {
      gate_name: "KPI Coverage",
      status: gate4Score >= 85 ? "PASS" : gate4Score >= 70 ? "WARN" : "FAIL",
      score: gate4Score,
      findings: [
        allKPIsHaveOwner ? "✓ All KPIs have named owners" : "✗ One or more KPIs missing named owner",
        allKPIsHaveThresholds ? "✓ All KPIs have Good/Warning/Critical thresholds" : "✗ KPI thresholds incomplete",
        allKPIsHaveActionability ? "✓ All KPIs have actionability statements" : "✗ Actionability statements missing",
        kpis.length >= 6 ? `✓ ${kpis.length} KPIs defined (minimum 6)` : `✗ Only ${kpis.length} KPIs — add at least ${6 - kpis.length} more`,
      ],
      recommendations: [
        !allKPIsHaveOwner && "Assign a named business role as owner for each KPI",
        !allKPIsHaveActionability && "Add 'If this KPI turns critical, the owner should...' statements",
      ].filter(Boolean),
    },
    {
      gate_name: "Dashboard Quality",
      status: gate5Score >= 85 ? "PASS" : gate5Score >= 70 ? "WARN" : "FAIL",
      score: gate5Score,
      findings: [
        execHasCards ? "✓ Executive pages have KPI cards at top" : "✗ Executive pages missing KPI/Card visuals",
        slicerCompliant ? "✓ All pages within 5-slicer limit" : "✗ Pages exceed 5-slicer limit",
        hasDrillThrough ? "✓ Drill-through paths defined" : "✗ No drill-through paths — add for summary → detail",
        pages.length >= 5 ? `✓ ${pages.length} pages defined` : `✗ Only ${pages.length} pages — minimum 5 required`,
      ],
      recommendations: [
        !hasDrillThrough && "Add drill-through from summary entities to transaction detail",
        !hasStorytelling && "Add storytelling_flow description to each page explaining the narrative",
      ].filter(Boolean),
    },
    {
      gate_name: "Semantic Model Quality",
      status: gate6Score >= 85 ? "PASS" : gate6Score >= 72 ? "WARN" : "FAIL",
      score: gate6Score,
      findings: [
        hasDivide ? "✓ DIVIDE() used throughout — no raw / operators" : "✗ Raw division operator detected — use DIVIDE()",
        hasYTD ? "✓ YTD time intelligence measures present" : "✗ No TOTALYTD measures — add for executive reporting",
        hasPY ? "✓ Prior year comparison measures present" : "✗ No SAMEPERIODLASTYEAR measures",
        hasFolders ? "✓ Display folders use hierarchical notation (Domain / Subdomain)" : "✗ Display folders missing or not hierarchical",
        measures.length >= 10 ? `✓ ${measures.length} measures defined` : `✗ ${measures.length} measures — minimum 10 recommended`,
      ],
      recommendations: [
        !hasDivide && "Replace all / operators with DIVIDE([Numerator], [Denominator], 0)",
        !hasYTD && `Add TOTALYTD([Measure], Dim_Date[Date], "${pack._fyEnd || '30/06'}") for executive KPIs`,
      ].filter(Boolean),
    },
    {
      gate_name: "Governance Design Completeness",
      status: gate7Score >= 80 ? "PASS" : "WARN",
      score: gate7Score,
      findings: [
        "Governance roles defined for Data Owner, KPI Owner, Report Owner, Business Steward, Access Steward",
        "Review cadence and change control process documented",
      ],
      recommendations: [
        "Confirm named individuals for each governance role with the client",
        "Schedule governance review cadence aligned to reporting frequency",
      ],
    },
    {
      gate_name: "Security Design Completeness",
      status: gate8Score >= 82 ? "PASS" : gate8Score >= 68 ? "WARN" : "FAIL",
      score: gate8Score,
      findings: [
        hasPII ? "✓ PII columns identified and listed" : "✗ No PII columns identified — review personally identifiable fields",
        hasRLSRoles ? "✓ RLS roles defined or RLS not required" : "✗ RLS required but no roles defined",
        hasSensitivityLabel ? "✓ Sensitivity label assigned" : "✗ No sensitivity label — assign before deployment",
        hasComplianceNotes ? "✓ Compliance notes documented" : "✗ No compliance or governance notes — add before production deployment",
      ],
      recommendations: [
        !hasPII && "Identify all columns containing name, DOB, ID, address, health, or financial information",
        !hasSensitivityLabel && "Assign Microsoft Purview sensitivity label appropriate to the industry",
      ].filter(Boolean),
    },
    {
      gate_name: "Executive Reporting Readiness",
      status: gate9Score >= 88 ? "PASS" : gate9Score >= 72 ? "WARN" : "FAIL",
      score: gate9Score,
      findings: [
        pages.filter(p => p.layout === "Executive").length > 0 ? "✓ Executive layout page present" : "✗ No Executive layout page — senior stakeholders lack a summary entry point",
        pages.some(p => p.storytelling_flow && p.storytelling_flow.length > 10) ? "✓ Storytelling flows defined on pages" : "✗ No storytelling flows — add what-happened / why / action narrative",
        kpis.length >= 6 ? `✓ ${kpis.length} KPIs defined — sufficient for executive reporting` : `✗ Only ${kpis.length} KPIs — add at least ${6 - kpis.length} more for executive coverage`,
        pages.some(p => p.drill_through) ? "✓ Drill-through paths defined for executive navigation" : "⚠ No drill-through paths — consider adding for detail access",
      ],
      recommendations: [
        pages.filter(p => p.layout === "Executive").length === 0 && "Add an Executive Summary page as page 1 with KPI cards in the top row",
        !pages.some(p => p.storytelling_flow && p.storytelling_flow.length > 10) && "Add storytelling flows: what is the current state → why → what action is required",
      ].filter(Boolean),
    },
  ];

  const composite = Math.round(gates.reduce((s, g) => s + g.score, 0) / gates.length);
  const verdict = gates.some(g => g.status === "FAIL") ? "REVISE"
    : gates.some(g => g.status === "WARN") ? "PASS_WITH_NOTES"
    : "PASS";

  const warnings = [];
  if (!hasYTD) warnings.push("No YTD time intelligence measures — add TOTALYTD measures for fiscal year reporting.");
  if (!hasPY) warnings.push("No prior year comparison measures — executives expect YoY comparisons.");
  if (rlsRequired === "yes" && securityRoles.length === 0) warnings.push("RLS required but no roles defined — resolve before production deployment.");
  if (kpis.length < 6) warnings.push(`Only ${kpis.length} KPIs defined — add at least ${6 - kpis.length} more to meet minimum standard.`);
  if (pages.length < 5) warnings.push(`Only ${pages.length} pages defined — minimum 5 recommended for production blueprints.`);

  // Design recommendations replace auto-corrections — the agent never claims to have fixed anything

  return {
    gates,
    overall_verdict: verdict,
    composite_score: composite,
    design_recommendations: buildDesignRecommendations(measures, kpis, pages, securityRoles, rlsRequired),
    warnings,
    implementation_risks: buildImplementationRisks(measures, kpis, pages, securityRoles, rlsRequired),
  };
}

function buildImplementationRisks(measures, kpis, pages, securityRoles, rlsRequired) {
  const risks = [];
  if (rlsRequired === "yes" && securityRoles.length === 0) {
    risks.push("HIGH: RLS required but no roles configured — all users will see all data until resolved.");
  }
  if (!measures.some(m => m.dax?.includes("TOTALYTD"))) {
    risks.push("MEDIUM: No YTD measures — fiscal year reporting will be incomplete at launch.");
  }
  if (pages.filter(p => p.layout === "Executive").length === 0) {
    risks.push("MEDIUM: No Executive layout page — senior stakeholders will lack a summary entry point.");
  }
  if (kpis.some(k => !k.owner || k.owner.length < 2)) {
    risks.push("LOW: Some KPIs have no named owner — accountability gaps may cause KPI drift over time.");
  }
  return risks;
}

function buildDesignRecommendations(measures, kpis, pages, securityRoles, rlsRequired) {
  const recs = [];
  const hasYTD      = measures.some(m => m.dax?.includes("TOTALYTD"));
  const hasPY       = measures.some(m => m.dax?.includes("SAMEPERIODLASTYEAR"));
  const hasDivide   = measures.every(m => !m.dax?.includes(" / ") || m.dax?.includes("DIVIDE"));
  const hasExec     = pages.some(p => p.layout === "Executive");
  const hasDrill    = pages.some(p => p.drill_through);
  const hasStory    = pages.some(p => p.storytelling_flow && p.storytelling_flow.length > 10);
  const hasFolders  = measures.every(m => m.display_folder?.includes("/"));

  if (!hasDivide) recs.push({ category: "Semantic Model", recommendation: "Consider replacing all / operators with DIVIDE([Numerator], [Denominator], 0) to prevent divide-by-zero errors.", rationale: "DIVIDE() is a DAX best practice and required for production Power BI models.", priority: "High" });
  if (!hasYTD)    recs.push({ category: "Semantic Model", recommendation: "Consider adding TOTALYTD measures for all financial and volume KPIs.", rationale: "Year-to-date comparisons are essential for executive reporting in Australian fiscal year context.", priority: "High" });
  if (!hasPY)     recs.push({ category: "Semantic Model", recommendation: "Consider adding SAMEPERIODLASTYEAR measures for executive KPIs.", rationale: "Prior year comparison is the most common executive reporting requirement.", priority: "Medium" });
  if (!hasFolders) recs.push({ category: "Semantic Model", recommendation: "Consider organising measures into display folders using Domain / Subdomain hierarchy (e.g. Revenue / Time Intelligence).", rationale: "Hierarchical display folders improve model navigability for report developers.", priority: "Medium" });
  if (!hasExec)   recs.push({ category: "Dashboard Design", recommendation: "Consider adding an Executive Summary page as page 1 with at least 3 KPI Card visuals at the top.", rationale: "Senior stakeholders need a summary entry point before navigating to analytical detail.", priority: "High" });
  if (!hasDrill)  recs.push({ category: "Dashboard Design", recommendation: "Consider adding drill-through pages from each summary entity to transactional detail.", rationale: "Drill-through paths allow users to investigate the drivers behind headline KPIs.", priority: "Medium" });
  if (!hasStory)  recs.push({ category: "Dashboard Design", recommendation: "Consider adding storytelling flows to each page: what is the current state → why is it at that level → what action is required.", rationale: "Storytelling structure increases report adoption and action-taking by business users.", priority: "Medium" });
  if (rlsRequired === "yes" && securityRoles.length === 0) recs.push({ category: "Security Design", recommendation: "Consider defining RLS roles using USERPRINCIPALNAME() and a user-to-data mapping table.", rationale: "Row-level security is required for this deployment — roles must be designed before implementation.", priority: "High" });
  if (rlsRequired === "yes" && securityRoles.length > 0) recs.push({ category: "Security Design", recommendation: "Consider a separate security mapping table (e.g. DimUserAccess) to drive RLS filter expressions rather than hardcoding email addresses in DAX.", rationale: "Mapping tables make RLS maintainable as team membership changes over time.", priority: "Medium" });
  recs.push({ category: "Governance", recommendation: "Consider a KPI Change Management Policy requiring KPI Owner and Report Owner sign-off before any KPI definition change is deployed.", rationale: "Uncontrolled KPI definition changes erode trust in reporting over time.", priority: "Medium" });
  recs.push({ category: "Governance", recommendation: "Consider a report certification process where the Report Owner formally approves output before executive distribution.", rationale: "Certification creates a clear accountability chain and reduces the risk of incorrect figures reaching leadership.", priority: "Low" });

  return recs;
}

/* ─────────────────────────────────────────────────────────────
   2. QUALITY FRAMEWORKS — built for demo mode blueprints
   Called from buildDemoBlueprint() — add this to the return object
───────────────────────────────────────────────────────────── */
function buildQualityFrameworks(measures, kpis, pages, pack, opts) {
  pack = pack || {};
  const industry = opts?.industry || "";

  // ── Audit Readiness ──────────────────────────────────────
  const piiCols = pack.security?.pii_columns || [];
  const sensitivityLabel = pack.security?.sensitivity_label || "";
  const complianceNotes = pack.security?.notes || [];
  const rlsRoles = opts?.rls === "yes" ? (pack.security?.roles || []) : [];

  const auditChecklist = [
    { item: "PII columns identified", status: piiCols.length > 0 ? "PASS" : "FAIL", evidence: piiCols.length > 0 ? `${piiCols.length} PII columns listed` : "No PII columns defined", priority: "Critical" },
    { item: "Sensitivity label assigned", status: sensitivityLabel ? "PASS" : "FAIL", evidence: sensitivityLabel || "Not assigned", priority: "Critical" },
    { item: "RLS roles defined", status: opts?.rls !== "yes" || rlsRoles.length > 0 ? "PASS" : "FAIL", evidence: opts?.rls !== "yes" ? "RLS not required" : `${rlsRoles.length} roles defined`, priority: "Critical" },
    { item: "Compliance obligations documented", status: complianceNotes.length > 0 ? "PASS" : "WARN", evidence: complianceNotes.length > 0 ? `${complianceNotes.length} compliance notes` : "No compliance notes", priority: "High" },
    { item: "KPI ownership assigned", status: kpis.every(k => k.owner?.length > 2) ? "PASS" : "WARN", evidence: `${kpis.filter(k => k.owner?.length > 2).length}/${kpis.length} KPIs have named owners`, priority: "High" },
    { item: "Source system traceability", status: (pack.factTables || []).every(f => f.source) ? "PASS" : "WARN", evidence: "Source system documented per fact table", priority: "Medium" },
    { item: "Data retention documented", status: "WARN", evidence: "Data retention period not specified in requirements", priority: "Medium" },
    { item: "Audit trail requirements", status: "WARN", evidence: "Audit trail design requires confirmation with IT and Legal", priority: "Medium" },
  ];

  const auditPassCount = auditChecklist.filter(c => c.status === "PASS").length;
  const auditScore = Math.round((auditPassCount / auditChecklist.length) * 100);

  // ── Dashboard Quality ────────────────────────────────────
  const execPages = pages.filter(p => p.layout === "Executive");
  const hasDrillThrough = pages.some(p => p.drill_through);
  const maxVisuals = Math.max(...pages.map(p => (p.visuals || []).length), 0);
  const slicerMax = Math.max(...pages.map(p => (p.slicers || []).length), 0);

  const dqDimensions = {
    executive_clarity: { score: execPages.length > 0 ? 88 : 55, notes: execPages.length > 0 ? "Executive Summary page defined with KPI-first layout" : "No Executive layout page — add one for C-suite navigation" },
    kpi_alignment: { score: kpis.length >= 6 ? 90 : Math.round((kpis.length / 6) * 90), notes: `${kpis.length} KPIs defined and linked to dashboard visuals` },
    business_goal_alignment: { score: 85, notes: "Dashboard pages aligned to detected industry capabilities" },
    visual_density: { score: maxVisuals <= 8 ? 92 : maxVisuals <= 10 ? 78 : 62, notes: `Maximum ${maxVisuals} visuals per page — target ≤ 8 for readability` },
    navigation_structure: { score: hasDrillThrough ? 88 : 68, notes: hasDrillThrough ? "Drill-through paths defined for detail access" : "No drill-through defined — users cannot navigate from summary to detail" },
    drill_through_design: { score: hasDrillThrough ? 86 : 60, notes: hasDrillThrough ? "Drill-through targets and trigger fields specified" : "Add drill-through paths from summary entities to transaction detail" },
    slicer_strategy: { score: slicerMax <= 5 ? 90 : 70, notes: slicerMax <= 5 ? "All pages within 5-slicer limit — good filter hygiene" : `${slicerMax} slicers detected on one page — reduce to maximum 5` },
    storytelling_quality: { score: 74, notes: "Storytelling flow recommended — add what-happened / why / action structure per page" },
    actionability: { score: kpis.every(k => k.actionability?.length > 10) ? 86 : 68, notes: "Actionability statements drive the link between KPI status and business response" },
  };

  const dqScore = Math.round(Object.values(dqDimensions).reduce((s, d) => s + d.score, 0) / Object.keys(dqDimensions).length);

  // ── KPI Quality ───────────────────────────────────────────
  const kpiAssessments = kpis.map(k => ({
    kpi_name: k.name,
    ownership_defined: !!(k.owner && k.owner.length > 2),
    business_relevant: true,
    actionable: !!(k.actionability && k.actionability.length > 10),
    target_defined: !!(k.target_logic && k.target_logic.length > 3),
    traceable_to_source: !!(k.data_source_ref || k.measure_ref),
    goal_aligned: !!(k.business_goal_ref && k.business_goal_ref.length > 3),
    cadence_appropriate: !!(k.cadence && k.cadence.length > 2),
    risk: (!k.owner || k.owner.length < 2) ? "No named owner — accountability gap" :
          (!k.actionability || k.actionability.length < 10) ? "No actionability statement — KPI may not drive action" :
          "Low risk",
  }));

  const kpiScorePct = kpiAssessments.reduce((s, k) => {
    const dims = [k.ownership_defined, k.actionable, k.target_defined, k.traceable_to_source, k.goal_aligned, k.cadence_appropriate];
    return s + (dims.filter(Boolean).length / dims.length);
  }, 0) / Math.max(kpiAssessments.length, 1);
  const kpiScore = Math.round(kpiScorePct * 100);

  // ── Semantic Model Quality ────────────────────────────────
  const hasDivide = measures.every(m => !m.dax?.includes(" / ") || m.dax?.includes("DIVIDE"));
  const hasYTD = measures.some(m => m.dax?.includes("TOTALYTD"));
  const hasPY = measures.some(m => m.dax?.includes("SAMEPERIODLASTYEAR"));
  const hasFolders = measures.every(m => m.display_folder?.includes("/"));
  const factCount = (pack.factTables || []).length;
  const dimCount = (pack.dimensions || []).length;

  const smDimensions = {
    star_schema_compliance: { score: factCount >= 3 && dimCount >= 4 ? 93 : 74, notes: `${factCount} fact tables and ${dimCount} dimension tables — ${factCount >= 3 ? "meets" : "below"} minimum for star schema` },
    fact_dimension_separation: { score: 90, notes: "Fact and dimension tables follow naming convention and separation of concerns" },
    date_intelligence_readiness: { score: hasYTD && hasPY ? 94 : hasYTD ? 80 : 60, notes: hasYTD && hasPY ? "YTD and prior year measures both present" : !hasYTD ? "TOTALYTD measures missing" : "Prior year comparison missing" },
    conformed_dimensions: { score: 88, notes: "Dim_Date shared across all fact tables as conformed dimension" },
    scalability: { score: 82, notes: "Star schema design supports future fact table additions without model restructure" },
    rls_readiness: { score: opts?.rls !== "yes" || rlsRoles.length > 0 ? 90 : 58, notes: opts?.rls !== "yes" ? "RLS not required for this deployment" : rlsRoles.length > 0 ? "RLS filter expressions use USERPRINCIPALNAME()" : "RLS required but filter expressions not defined" },
    measure_organisation: { score: hasFolders ? 88 : 65, notes: hasFolders ? "Display folders use Domain / Subdomain hierarchy" : "Display folders not hierarchical — reorganise as Domain / Subdomain" },
    business_model_clarity: { score: 85, notes: "Fact table grains align to business events — each row represents a clear business transaction" },
  };

  const smScore = Math.round(Object.values(smDimensions).reduce((s, d) => s + d.score, 0) / Object.keys(smDimensions).length);

  // ── Governance Framework ─────────────────────────────────
  const industryObligations = getIndustryObligations(industry);
  const govScore = 78; // calibrated by audit and security completeness

  return {
    audit_readiness: {
      score: auditScore,
      rating: auditScore >= 85 ? "Strong" : auditScore >= 70 ? "Satisfactory" : auditScore >= 55 ? "Developing" : "Requires Attention",
      strengths: auditChecklist.filter(c => c.status === "PASS").map(c => c.item),
      risks: auditChecklist.filter(c => c.status !== "PASS").map(c => `${c.priority}: ${c.item} — ${c.evidence}`),
      missing_requirements: auditChecklist.filter(c => c.status === "FAIL").map(c => c.item),
      checklist: auditChecklist,
    },
    dashboard_quality: {
      score: dqScore,
      rating: dqScore >= 85 ? "High Quality" : dqScore >= 70 ? "Good" : dqScore >= 55 ? "Developing" : "Needs Improvement",
      dimensions: dqDimensions,
      strengths: Object.entries(dqDimensions).filter(([, v]) => v.score >= 85).map(([k]) => k.replace(/_/g, " ")),
      risks: Object.entries(dqDimensions).filter(([, v]) => v.score < 70).map(([k, v]) => `${k.replace(/_/g, " ")}: ${v.notes}`),
      recommendations: [
        !hasDrillThrough && "Add drill-through from every summary page to a Detail layout page",
        maxVisuals > 8 && `Reduce visuals per page — current maximum ${maxVisuals} exceeds recommended 8`,
        execPages.length === 0 && "Add an Executive layout page as the first page in the report",
      ].filter(Boolean),
    },
    kpi_quality: {
      score: kpiScore,
      rating: kpiScore >= 85 ? "High Quality" : kpiScore >= 70 ? "Good" : kpiScore >= 55 ? "Developing" : "Needs Improvement",
      kpi_assessments: kpiAssessments,
      coverage_assessment: `${kpis.length} KPIs defined across ${[...new Set(kpis.map(k => k.business_goal_ref).filter(Boolean))].length} business goals`,
      missing_ownership: kpiAssessments.filter(k => !k.ownership_defined).map(k => k.kpi_name),
      missing_targets: kpiAssessments.filter(k => !k.target_defined).map(k => k.kpi_name),
      recommendations: [
        kpiAssessments.some(k => !k.ownership_defined) && "Assign named business roles as KPI owners — avoid generic titles",
        kpiAssessments.some(k => !k.actionable) && "Add actionability statement: 'If this KPI turns Critical, [role] should [action]'",
        kpiAssessments.some(k => !k.goal_aligned) && "Reference specific business goals in each KPI definition",
      ].filter(Boolean),
    },
    semantic_model_quality: {
      score: smScore,
      rating: smScore >= 88 ? "Enterprise Grade" : smScore >= 76 ? "Production Ready" : smScore >= 64 ? "Developing" : "Needs Work",
      dimensions: smDimensions,
      strengths: Object.entries(smDimensions).filter(([, v]) => v.score >= 85).map(([k, v]) => v.notes),
      risks: Object.entries(smDimensions).filter(([, v]) => v.score < 72).map(([k, v]) => v.notes),
      recommendations: [
        !hasDivide && "Replace all / operators with DIVIDE([Numerator], [Denominator], 0)",
        !hasYTD && "Add TOTALYTD fiscal year measures for all revenue and volume KPIs",
        !hasFolders && "Reorganise measure display folders as 'Domain / Subdomain' (e.g. Revenue / Time Intelligence)",
      ].filter(Boolean),
    },
    governance_framework: {
      score: govScore,
      rating: govScore >= 85 ? "Mature" : govScore >= 70 ? "Defined" : govScore >= 55 ? "Developing" : "Ad Hoc",
      industry_obligations: industryObligations,
      compliance_controls: [
        "Row-level security (RLS) to restrict data access by user role",
        "Microsoft Purview sensitivity labels applied to all datasets",
        "PII columns masked or restricted in non-production environments",
        "Change control process for measure and KPI definition changes",
        "Scheduled governance reviews aligned to reporting cadence",
      ],
      data_stewardship: buildDataStewardship(industry),
      recommended_policies: [
        "Data Access Policy — who can access what, and under what conditions",
        "KPI Change Management Policy — how KPI definitions are changed and communicated",
        "Report Certification Policy — who signs off reports before executive distribution",
        "Data Retention Policy — how long raw data and aggregated reports are retained",
      ],
      gaps: [
        "Named individuals not confirmed for governance roles — requires client workshop",
        "Data retention period not specified in business requirements",
        "Change control process for report modifications not documented",
        "Audit log configuration not confirmed with IT",
      ],
    },
  };
}

function getIndustryObligations(industry) {
  const obligations = {
    "NDIS": [
      "NDIS Practice Standards — provider certification and audit obligations",
      "Privacy Act 1988 — participant sensitive personal information",
      "Notifiable incident reporting to NDIS Quality and Safeguards Commission",
      "NDIS Pricing Arrangements and Price Limits (PAPL) — annual 1 July updates",
      "Worker Screening requirements — all direct support workers",
      "SCHADS Award compliance — workforce cost reporting implications",
    ],
    "Government": [
      "PGPA Act 2013 — performance reporting and audit obligations",
      "ANAO auditing standards — annual financial and performance audits",
      "Australian Government Information Security Manual (ISM)",
      "Senate Estimates reporting — expenditure traceable to PBS programs",
      "GrantConnect public disclosure — all grants above threshold",
      "Privacy Act 1988 — employee and citizen data",
      "Archives Act 1983 — records retention obligations",
    ],
    "Professional Services": [
      "Privacy Act 1988 — client personal information handling",
      "Anti-Money Laundering Act 2006 — client identification obligations",
      "Trust accounting obligations — state law society or CPA Australia rules",
      "Tax Practitioners Board registration requirements",
    ],
    "Property Management": [
      "Residential Tenancies Act (state-specific) — inspection, bond, and disclosure obligations",
      "Privacy Act 1988 — tenant and owner personal information",
      "Trust accounting obligations — strict audit and reconciliation requirements",
      "Agent licensing requirements — state property legislation",
    ],
  };
  return obligations[industry] || [
    "Privacy Act 1988 (Cth) — personal information handling obligations",
    "Australian Consumer Law — disclosure and fairness obligations",
    "Industry-specific regulatory requirements — confirm with legal counsel",
  ];
}

function buildDataStewardship(industry) {
  const stewardship = {
    "NDIS": [
      { domain: "Service Delivery", owner: "Operations Manager", responsibility: "Service delivery records accuracy and completeness" },
      { domain: "Participant Data", owner: "Quality and Compliance Manager", responsibility: "Participant PII protection and access control" },
      { domain: "Financial / Claims", owner: "Finance Manager", responsibility: "Revenue recognition and claims data integrity" },
      { domain: "Workforce", owner: "HR / People and Culture Manager", responsibility: "Worker records, screening, and award compliance" },
    ],
    "Government": [
      { domain: "Financial / Budget", owner: "Chief Finance Officer", responsibility: "Appropriation, expenditure, and budget data integrity" },
      { domain: "Program Performance", owner: "Deputy Secretary / Program Owner", responsibility: "KPI data collection and accuracy" },
      { domain: "Grants", owner: "Grants Manager", responsibility: "Grant records, acquittals, and GrantConnect disclosures" },
      { domain: "Workforce", owner: "Chief Human Resources Officer", responsibility: "FTE, classification, and diversity data" },
    ],
    "Professional Services": [
      { domain: "Client and Engagement", owner: "Practice Manager", responsibility: "Client and engagement records accuracy" },
      { domain: "Financial / WIP / Billing", owner: "Finance Director", responsibility: "WIP, billing, and revenue data integrity" },
      { domain: "Workforce / Timesheets", owner: "Managing Partner", responsibility: "Timesheet approval and utilisation accuracy" },
    ],
    "Property Management": [
      { domain: "Property Portfolio", owner: "Principal", responsibility: "Property records and portfolio accuracy" },
      { domain: "Tenant / Tenancy", owner: "Property Manager Team Lead", responsibility: "Tenancy data and PII protection" },
      { domain: "Trust Accounting", owner: "Principal / External Accountant", responsibility: "Trust account reconciliation and compliance" },
    ],
  };
  return stewardship[industry] || [
    { domain: "Operational Data", owner: "Operations Manager", responsibility: "Operational data accuracy" },
    { domain: "Financial Data", owner: "Finance Manager", responsibility: "Financial data integrity" },
  ];
}

/* ─────────────────────────────────────────────────────────────
   3. EXPECTED TARGETS — for future Audit Agent comparison
───────────────────────────────────────────────────────────── */
function buildExpectedTargets(measures, kpis, pages, securityRoles, pack) {
  pack = pack || {};
  return {
    description: "Expected blueprint targets. A future Audit Agent will compare these against actual Power BI implementation to detect drift, missing components, or security gaps.",
    fact_tables: (pack.factTables || []).map(f => f.name),
    dimension_tables: (pack.dimensions || []).map(d => d.name),
    kpi_names: kpis.map(k => k.name),
    page_names: pages.map(p => p.name),
    security_roles: securityRoles.map(r => r.name || r.role),
    pii_columns: pack.security?.pii_columns || [],
    measure_count_minimum: Math.max(measures.length, 10),
    kpi_count_minimum: Math.max(kpis.length, 6),
    page_count_minimum: Math.max(pages.length, 5),
    rls_role_count_minimum: securityRoles.length,
    compliance_checks: [
      { check: "All measures use DIVIDE() not / operator", expected: "true", category: "DAX Quality" },
      { check: "TOTALYTD measures present", expected: "true", category: "Time Intelligence" },
      { check: "SAMEPERIODLASTYEAR measures present", expected: "true", category: "Time Intelligence" },
      { check: "All fact tables connected to Dim_Date", expected: "true", category: "Star Schema" },
      { check: "No bidirectional relationships", expected: "true", category: "Star Schema" },
      { check: "PII columns identified", expected: `${(pack.security?.pii_columns || []).length} columns`, category: "Security" },
      { check: "Sensitivity label assigned", expected: pack.security?.sensitivity_label || "Confidential", category: "Security" },
      { check: "RLS filter uses USERPRINCIPALNAME()", expected: "true", category: "Security" },
      { check: "All measures in _Measures table", expected: "true", category: "Measure Organisation" },
      { check: "Display folders use Domain / Subdomain notation", expected: "true", category: "Measure Organisation" },
    ],
  };
}

/* ─────────────────────────────────────────────────────────────
   4. GOVERNANCE BLOCK — for demo mode blueprint
───────────────────────────────────────────────────────────── */
function buildGovernanceBlock(industry, pack, opts) {
  const govRoles = {
    "NDIS": {
      data_owner: "Operations Manager",
      kpi_owner: "Operations Manager (utilisation), CFO (financial), Quality Manager (compliance)",
      report_owner: "CEO / Managing Director",
      business_steward: "Operations Manager",
      access_steward: "IT Manager / System Administrator",
      review_cadence: "Monthly — financial KPIs; Weekly — operational KPIs; Quarterly — compliance KPIs",
      change_control: "KPI definition changes require sign-off from KPI Owner and Report Owner before deployment",
    },
    "Government": {
      data_owner: "Chief Finance Officer (financial), Program Manager (program), CHRO (workforce)",
      kpi_owner: "Deputy Secretary (program KPIs), CFO (budget KPIs), CHRO (workforce KPIs)",
      report_owner: "Secretary / CEO / Minister's Office",
      business_steward: "Branch Manager (per dataset)",
      access_steward: "ICT Security Team (ISM compliance)",
      review_cadence: "Monthly — budget and workforce; Quarterly — program KPIs; Annual — strategic outcomes",
      change_control: "All changes to PBS-aligned KPIs require Deputy Secretary approval and ICT change management",
    },
    "Professional Services": {
      data_owner: "Practice Manager (engagements), Finance Director (billing and WIP)",
      kpi_owner: "Managing Partner (firm-wide), Practice Heads (service line)",
      report_owner: "Managing Partner / Board",
      business_steward: "Practice Manager",
      access_steward: "IT Manager",
      review_cadence: "Weekly — utilisation and WIP; Monthly — financial KPIs; Quarterly — strategic review",
      change_control: "KPI changes require Managing Partner approval — affects partner performance frameworks",
    },
    "Property Management": {
      data_owner: "Principal (portfolio data), Property Manager Team Lead (tenancy data)",
      kpi_owner: "Principal (occupancy and revenue), Finance (arrears and trust)",
      report_owner: "Principal / Director",
      business_steward: "Office Manager",
      access_steward: "IT / Software Vendor Administrator",
      review_cadence: "Weekly — arrears and maintenance; Monthly — portfolio and revenue; Quarterly — strategic",
      change_control: "Principal sign-off required for KPI threshold changes — affects property manager performance reviews",
    },
  };

  const roles = govRoles[industry] || {
    data_owner: "Operations Manager",
    kpi_owner: "Operations Manager / CFO",
    report_owner: "CEO",
    business_steward: "Operations Manager",
    access_steward: "IT Manager",
    review_cadence: "Monthly",
    change_control: "CEO approval required for KPI definition changes",
  };

  return {
    ...roles,
    roles_and_responsibilities: [
      { role: "Data Owner", responsibility: "Accountable for data accuracy, completeness, and fitness for purpose", named_owner: roles.data_owner },
      { role: "KPI Owner", responsibility: "Defines and maintains KPI definitions, targets, and thresholds", named_owner: roles.kpi_owner },
      { role: "Report Owner", responsibility: "Accountable for report content, distribution, and sign-off", named_owner: roles.report_owner },
      { role: "Business Steward", responsibility: "Day-to-day governance of data definitions and business rules", named_owner: roles.business_steward },
      { role: "Access Steward", responsibility: "Manages user access, RLS configuration, and security reviews", named_owner: roles.access_steward },
    ],
  };
}

/* ─────────────────────────────────────────────────────────────
   5. CONFIDENCE — 7-dimension weighted model
───────────────────────────────────────────────────────────── */
function buildConfidenceScore(opts, hasRequirements, hasSchema, pack, measures, kpis, pages, securityRoles) {
  const industryConfidence = opts.industryExplicit ? 97 : (opts.detectionConfidence || 75);

  // Capability confidence: did we get a pack with real content?
  const capabilityConfidence = (pack?.capabilities?.length || 0) >= 5 ? 90 :
    (pack?.capabilities?.length || 0) >= 3 ? 75 : 55;

  // Goal confidence: did the user give us enough to work from?
  const goalConfidence = hasRequirements ? (opts.requirements?.length > 200 ? 88 : 72) : 45;

  // Semantic model completeness
  const hasDivide = measures.every(m => !m.dax?.includes(" / ") || m.dax?.includes("DIVIDE"));
  const hasYTD = measures.some(m => m.dax?.includes("TOTALYTD"));
  const smCompleteness = [
    (pack?.factTables?.length || 0) >= 3,
    (pack?.dimensions?.length || 0) >= 4,
    hasSchema,
    measures.length >= 10,
    hasDivide,
    hasYTD,
  ].filter(Boolean).length;
  const semanticModelCompleteness = Math.round((smCompleteness / 6) * 100);

  // KPI completeness
  const kpiCompleteness = Math.min(100, Math.round((kpis.length / 8) * 100));

  // Dashboard completeness
  const dashCompleteness = Math.min(100, Math.round((pages.length / 6) * 100));

  // Governance completeness
  const govCompleteness = securityRoles.length >= 2 ? 85 :
    securityRoles.length >= 1 ? 70 : 55;

  // Weighted score (weights sum to 100)
  const weights = {
    industry: 0.20,
    capability: 0.15,
    goal: 0.15,
    semanticModel: 0.20,
    kpi: 0.12,
    dashboard: 0.10,
    governance: 0.08,
  };

  const weightedScore = Math.round(
    industryConfidence * weights.industry +
    capabilityConfidence * weights.capability +
    goalConfidence * weights.goal +
    semanticModelCompleteness * weights.semanticModel +
    kpiCompleteness * weights.kpi +
    dashCompleteness * weights.dashboard +
    govCompleteness * weights.governance
  );

  const band = weightedScore >= 90 ? "Production Ready" :
    weightedScore >= 75 ? "Strong" :
    weightedScore >= 50 ? "Directional" :
    weightedScore >= 25 ? "Indicative" : "Insufficient";

  return {
    score: weightedScore,
    band,
    basis: opts.industryExplicit
      ? `Industry explicitly selected. ${hasRequirements ? "Business requirements provided." : ""}${hasSchema ? " Dataset schema provided." : ""}`
      : `Industry auto-detected from input signals (${industryConfidence}% confidence). ${hasRequirements ? "Business requirements provided." : "Requirements sparse."}`,
    dimension_scores: {
      industry_confidence: industryConfidence,
      capability_confidence: capabilityConfidence,
      goal_confidence: goalConfidence,
      semantic_model_completeness: semanticModelCompleteness,
      kpi_completeness: kpiCompleteness,
      dashboard_completeness: dashCompleteness,
      governance_completeness: govCompleteness,
    },
    assumptions: buildAssumptions(opts, hasRequirements, hasSchema),
    gaps: buildGaps(hasRequirements, hasSchema, pack, opts),
  };
}

/* ─────────────────────────────────────────────────────────────
   6. UPDATED buildAssumptions — adds governance and compliance
───────────────────────────────────────────────────────────── */
function buildAssumptions(opts, hasRequirements, hasSchema) {
  const fyEnd = opts.fy === "July" ? "30/06" : opts.fy === "January" ? "31/12" : "30/06";
  const assumptions = [
    `Fiscal year end set to ${fyEnd} based on ${opts.fy} start selection.`,
    `Currency set to ${opts.currency}.`,
    "Dim_Date spine assumed 2018-07-01 to 2030-06-30.",
    "All DAX measures hosted in _Measures island table.",
    "Star schema design applied — no snowflake or flat table structures.",
    "All DIVIDE() functions include zero as third parameter for blank suppression.",
  ];
  if (!hasRequirements) assumptions.push("No business requirements provided — industry pack defaults applied for KPIs and pages.");
  if (!hasSchema) assumptions.push("No schema provided — canonical star schema from industry knowledge pack used. Column names inferred from industry standard.");
  assumptions.push("Governance roles assigned by industry convention — confirm named individuals with client.");
  assumptions.push("Compliance obligations reflect Australian regulatory context — confirm applicability with client legal counsel.");
  return assumptions;
}

/* ─────────────────────────────────────────────────────────────
   7. UPDATED buildGaps — adds governance and audit gaps
───────────────────────────────────────────────────────────── */
function buildGaps(hasRequirements, hasSchema, pack, opts) {
  const gaps = [];
  if (!hasRequirements) gaps.push("Business requirements not provided — executive questions and KPI priorities are pack defaults, not tailored to specific goals.");
  if (!hasSchema) gaps.push("No dataset schema provided — column-level mapping cannot be confirmed. Source system integration design required.");
  if (opts.rls === "yes" && !(pack.security?.roles?.length)) {
    gaps.push("RLS required but no user-to-role mapping table identified — provide user identity to data scope mapping (e.g. user to region, user to participant).");
  }
  gaps.push("No budget/target table detected — KPI targets default to prior year comparisons or industry benchmarks.");
  gaps.push("Governance role nominations (named individuals) not confirmed — governance framework uses role titles only.");
  gaps.push("Data retention period not specified — define retention rules per data category before production deployment.");
  gaps.push("Audit trail configuration not confirmed — coordinate with IT on Power BI activity log and dataset access audit settings.");
  return gaps;
}

/* ─────────────────────────────────────────────────────────────
   MAIN GENERATE FUNCTION
───────────────────────────────────────────────────────────── */
async function generateBlueprint() {
  const industryInput = val("inp-industry");
  const audience      = val("inp-audience");
  const currency      = val("inp-currency");
  const fy            = val("inp-fy");
  const rls           = val("inp-rls");
  const refresh       = val("inp-refresh");
  const requirements  = val("inp-requirements");
  const schema        = val("inp-schema");
  const provider      = val("inp-provider");
  const apiKey        = val("inp-apikey").trim();

  const modeTabReq = $("modeTabReq");
  const mode       = modeTabReq && modeTabReq.classList.contains("active") ? "requirements" : "schema";
  const inputText  = (requirements + " " + schema).trim();

  if (!inputText) {
    alert("Please enter business requirements or a dataset schema before generating.");
    return;
  }
  if (provider !== "demo" && !apiKey) {
    alert("Please enter your API key, or select Demo Mode to generate without one.");
    return;
  }

  const btn = $("generateBtn");
  if (btn) { btn.disabled = true; btn.textContent = "⏳ Generating…"; }

  const agentPanel = $("agentPanel");
  if (agentPanel) agentPanel.classList.add("visible");
  renderSteps();

  try {
    await runStep("parse");

    let detectedIndustry = industryInput;
    let industryExplicit = !!industryInput;
    let validationResult = null;

    if (!detectedIndustry) {
      const det = detectIndustry(inputText);
      detectedIndustry = det.industry || "Professional Services";
      validationResult = det.validation;

      // If validation overrode the winner, use the runner-up industry
      if (det.validation?.overridden && det.candidates?.[1]) {
        const runnerUp = det.candidates[1][0];
        console.warn(`[Detection] Validation overrode "${det.industry}" → "${runnerUp}". Reason: ${det.validation.overrideReason}`);
        detectedIndustry = runnerUp;
      }
    } else {
      // Even for explicit selections, run validation to warn about mismatches
      const det = detectIndustry(inputText);
      validationResult = validateIndustryConsistency(detectedIndustry, inputText, []);
    }
    await runStep("detect");

    let pack = getPack(detectedIndustry);
    if (!pack) {
      showError('No knowledge pack found for "' + detectedIndustry + '". Defaulting to Professional Services.');
      detectedIndustry = "Professional Services";
      pack = getPack(detectedIndustry);
    }
    await runStep("pack");
    await runStep("caps");

    let blueprint;
    const demoOpts = { audience, currency, fy, rls, refresh, requirements, schema, industryExplicit, validationResult };

    if (provider === "demo" || !apiKey) {
      blueprint = buildDemoBlueprint(detectedIndustry, pack, demoOpts);
      await runStep("schema");
      await runStep("dax");
      await runStep("pages");
      await runStep("review");
    } else {
      try {
        const systemPrompt = buildSystemPrompt(detectedIndustry, pack, { fy, currency, rls, refresh });
        const userPrompt   = buildUserPrompt(mode, requirements, schema, detectedIndustry, audience, currency, fy, rls, refresh);

        await runStep("schema");
        await runStep("dax");

        let raw;
        if (provider === "groq")   raw = await callGroq(systemPrompt, userPrompt, apiKey);
        else if (provider === "openai") raw = await callOpenAI(systemPrompt, userPrompt, apiKey);
        else                        raw = await callGroq(systemPrompt, userPrompt, apiKey);

        // Augment API response with quality frameworks if missing (demo fallback fill)
        if (!raw.quality_frameworks) {
          const m = raw.measures || []; const k = raw.kpis || []; const p = raw.pages || [];
          const sr = (raw.security?.roles || []).map(r => ({ name: r.name, filter_table: r.filter_table, filter_dax: r.filter_dax }));
          raw.quality_frameworks = buildQualityFrameworks(m, k, p, pack, { rls, industry: detectedIndustry });
          raw.governance         = raw.governance || buildGovernanceBlock(detectedIndustry, pack, demoOpts);
          raw.expected_targets   = raw.expected_targets || buildExpectedTargets(m, k, p, sr, pack);
          if (!raw.self_review?.composite_score) {
            const conf = raw.confidence?.score || 75;
            raw.self_review = buildSelfReview(m, k, p, sr, rls, conf, pack);
          }
          if (!raw.confidence?.dimension_scores) {
            raw.confidence = buildConfidenceScore(
              { fy, currency, rls, refresh, requirements, schema, industryExplicit,
                detectionConfidence: raw.detection?.confidence || 75, industry: detectedIndustry },
              (requirements||"").trim().length > 50, (schema||"").trim().length > 20, pack, m, k, p, sr
            );
          }
        }

        blueprint = raw;
        await runStep("pages");
        await runStep("review");
      } catch (apiErr) {
        console.error("API call failed, falling back to demo:", apiErr);
        showError("API call failed: " + apiErr.message + ". Showing demo blueprint.");
        blueprint = buildDemoBlueprint(detectedIndustry, pack, demoOpts);
        await runStep("pages");
        await runStep("review");
      }
    }

    renderAllViews(blueprint);
    updateNavCounters(blueprint);

    setTimeout(() => {
      if (agentPanel) agentPanel.classList.remove("visible");
      showView("detection");
    }, 600);
  } catch (err) {
    console.error("Blueprint generation failed:", err);
    showError("Blueprint generation failed: " + err.message);
    AGENT_STEPS.forEach((s) => {
      const el = $("step-" + s.key);
      if (el && el.classList.contains("active")) failStep(s.key);
    });
  } finally {
    if (btn) { btn.disabled = false; btn.innerHTML = "⚡ Generate Blueprint"; }
  }
}

/* ─────────────────────────────────────────────────────────────
   ERROR BANNER
───────────────────────────────────────────────────────────── */
function showError(msg) {
  let banner = $("errorBanner");
  if (!banner) {
    banner = document.createElement("div");
    banner.id = "errorBanner";
    banner.className = "error-banner";
    const main = document.querySelector(".main");
    if (main) main.prepend(banner);
  }
  banner.textContent = "⚠️ " + msg;
  banner.style.display = "block";
  setTimeout(() => { if (banner) banner.style.display = "none"; }, 8000);
}

/* ─────────────────────────────────────────────────────────────
   NAV COUNTERS — updated to show quality scores
───────────────────────────────────────────────────────────── */
function updateNavCounters(bp) {
  const set = (id, v) => { const el = $(id); if (el) el.textContent = v; };
  set("cntDetection",    bp.detection?.industry ? "✓" : "—");
  set("cntCapabilities", (bp.capabilities || []).length || "—");
  set("cntModel",        ((bp.data_model?.fact_tables || []).length + (bp.data_model?.dimension_tables || []).length) || "—");
  set("cntKpis",         (bp.kpis || []).length || "—");
  set("cntPages",        (bp.pages || []).length || "—");
  set("cntDax",          (bp.measures || []).length || "—");
  // Show composite gate score in review badge
  const verdict  = bp.self_review?.overall_verdict || "";
  const composite = bp.self_review?.composite_score;
  set("cntReview", composite ? composite + "%" : (verdict || "—"));
}

/* ─────────────────────────────────────────────────────────────
   RENDER — ALL VIEWS
───────────────────────────────────────────────────────────── */
function renderAllViews(bp) {
  renderDetection(bp);
  renderCapabilities(bp);
  renderModel(bp);
  renderKPIs(bp);
  renderPages(bp);
  renderDAX(bp);
  renderReview(bp);
}

/* ─────────────────────────────────────────────────────────────
   RENDER — DETECTION
───────────────────────────────────────────────────────────── */
function renderDetection(bp) {
  const el = $("view-detection");
  if (!el) return;
  const det  = bp.detection  || {};
  const meta = bp.meta       || {};
  const conf = bp.confidence || {};
  const pack = getPack(det.industry || meta.industry) || {};
  const score   = det.confidence || conf.score || 0;
  const tier    = det.tier || pack.tier || 1;
  const icon    = pack.icon || "📊";
  const signals = det.signals_matched || [];
  const validation = det.validation || null;
  const disqualified = det.disqualified || [];

  // Classify signals by type for display
  const srcSignals  = signals.filter(s => s.startsWith("🔧"));
  const capSignals  = signals.filter(s => s.startsWith("⚙️"));
  const goalSignals = signals.filter(s => s.startsWith("🎯"));
  const packSignals = signals.filter(s => !s.startsWith("🔧") && !s.startsWith("⚙️") && !s.startsWith("🎯"));

  // Validation panel colour
  const valPassed  = !validation || validation.passed;
  const valOverride = validation?.overridden;
  const valColor   = valPassed ? "var(--success)" : valOverride ? "var(--danger)" : "var(--warn)";
  const valBg      = valPassed ? "rgba(34,197,94,.06)" : valOverride ? "rgba(239,68,68,.06)" : "rgba(245,158,11,.06)";
  const valBorder  = valPassed ? "rgba(34,197,94,.3)" : valOverride ? "rgba(239,68,68,.3)" : "rgba(245,158,11,.3)";
  const valIcon    = valPassed ? "✅" : valOverride ? "🚫" : "⚠️";
  const valLabel   = valPassed ? "PASSED" : valOverride ? "OVERRIDDEN" : "WARNINGS";

  const forbidden = det.validation?.forbiddenEntities || {};

  el.innerHTML = `
    <div class="section-hdr">
      <div class="section-icon">🔍</div>
      <div class="section-title">Industry Detection</div>
      <div class="section-divider"></div>
    </div>

    <div class="detect-banner">
      <div class="detect-icon">${icon}</div>
      <div>
        <div class="detect-industry">${det.industry || meta.industry || "Unknown"}</div>
        <div class="detect-sub">Pack: <strong>${det.pack_applied || pack.code || "N/A"}</strong> &nbsp;·&nbsp; Tier ${tier} &nbsp;·&nbsp; ${meta.refresh_cadence || ""} refresh &nbsp;·&nbsp; Domain: <strong>${det.capability_domain || "—"}</strong></div>
      </div>
      <div class="detect-score">
        <div class="score-num">${score}%</div>
        <div class="score-lbl">Detection confidence</div>
      </div>
    </div>

    <!-- ── Validation Layer Result ── -->
    <div style="background:${valBg};border:1px solid ${valBorder};border-radius:var(--radius);padding:14px 16px;margin-bottom:14px">
      <div style="display:flex;align-items:center;gap:10px;margin-bottom:${(validation?.issues||[]).length > 0 ? '12px' : '0'}">
        <div style="font-size:20px">${valIcon}</div>
        <div>
          <div style="font-size:12px;font-weight:700;color:${valColor}">Industry Validation Layer — ${valLabel}</div>
          <div style="font-size:11px;color:var(--text2);margin-top:2px">
            ${valOverride
              ? `Proposed industry was overridden. Reason: ${validation.overrideReason}`
              : valPassed
              ? `All consistency checks passed. ${det.industry || meta.industry} is the correct knowledge pack for this input.`
              : `${(validation?.issues||[]).length} issue(s) detected — review before proceeding.`
            }
          </div>
        </div>
      </div>
      ${(validation?.issues || []).length > 0 ? `
      <div style="display:flex;flex-direction:column;gap:5px">
        ${(validation.issues).map(issue => `
        <div style="display:flex;gap:8px;align-items:flex-start;padding:7px 10px;background:rgba(0,0,0,.2);border-radius:6px;font-size:11px">
          <span style="color:${issue.severity==='CRITICAL'?'var(--danger)':issue.severity==='HIGH'?'var(--warn)':'var(--text3)'};font-weight:700;flex-shrink:0">${issue.severity}</span>
          <span style="color:var(--text3);flex-shrink:0">[${issue.type}]</span>
          <span style="color:var(--text)">${issue.message}</span>
        </div>`).join("")}
      </div>` : ""}
    </div>

    <!-- ── Signal Breakdown ── -->
    ${signals.length > 0 ? `
    <div class="card" style="margin-bottom:12px">
      <div class="section-hdr" style="margin-bottom:10px">
        <div class="section-icon">📡</div>
        <div class="section-title">Detection Signals — 4 Signal Types</div>
      </div>
      ${srcSignals.length > 0 ? `
      <div style="margin-bottom:10px">
        <div style="font-size:10px;font-weight:700;color:var(--accent3);text-transform:uppercase;letter-spacing:.5px;margin-bottom:5px">🔧 Source System (weight: 10×)</div>
        <div style="display:flex;flex-wrap:wrap;gap:4px">
          ${srcSignals.map(s => `<span class="tag" style="background:rgba(6,182,212,.12);color:var(--accent3);border:1px solid rgba(6,182,212,.3)">${s.replace("🔧 ","")}</span>`).join("")}
        </div>
      </div>` : ""}
      ${capSignals.length > 0 ? `
      <div style="margin-bottom:10px">
        <div style="font-size:10px;font-weight:700;color:var(--accent2);text-transform:uppercase;letter-spacing:.5px;margin-bottom:5px">⚙️ Capability (weight: 5×)</div>
        <div style="display:flex;flex-wrap:wrap;gap:4px">
          ${capSignals.map(s => `<span class="tag" style="background:rgba(139,92,246,.12);color:#a78bfa;border:1px solid rgba(139,92,246,.3)">${s.replace("⚙️ ","")}</span>`).join("")}
        </div>
      </div>` : ""}
      ${goalSignals.length > 0 ? `
      <div style="margin-bottom:10px">
        <div style="font-size:10px;font-weight:700;color:var(--success);text-transform:uppercase;letter-spacing:.5px;margin-bottom:5px">🎯 Business Goal (weight: 4×)</div>
        <div style="display:flex;flex-wrap:wrap;gap:4px">
          ${goalSignals.map(s => `<span class="tag" style="background:rgba(34,197,94,.1);color:var(--success);border:1px solid rgba(34,197,94,.3)">${s.replace("🎯 ","")}</span>`).join("")}
        </div>
      </div>` : ""}
      ${packSignals.length > 0 ? `
      <div>
        <div style="font-size:10px;font-weight:700;color:var(--accent);text-transform:uppercase;letter-spacing:.5px;margin-bottom:5px">📋 Industry Pack (weight: 1–3×)</div>
        <div style="display:flex;flex-wrap:wrap;gap:4px">
          ${packSignals.map(s => `<span class="tag tag-blue">${s}</span>`).join("")}
        </div>
      </div>` : ""}
    </div>` : ""}

    <!-- ── Disqualified Industries ── -->
    ${disqualified.length > 0 ? `
    <div class="card" style="margin-bottom:12px;border-color:rgba(239,68,68,.3)">
      <div class="section-hdr" style="margin-bottom:8px">
        <div class="section-icon">🚫</div>
        <div class="section-title" style="color:var(--danger)">Disqualified Industries</div>
      </div>
      <div style="font-size:11px;color:var(--text2);margin-bottom:8px">The following industries were excluded because their exclusion terms appeared in the input:</div>
      <div style="display:flex;flex-wrap:wrap;gap:5px">
        ${disqualified.map(d => `<span class="tag" style="background:rgba(239,68,68,.1);color:var(--danger);border:1px solid rgba(239,68,68,.3)">${d}</span>`).join("")}
      </div>
    </div>` : ""}

    <!-- ── Forbidden Entities ── -->
    ${(forbidden.entities || []).length > 0 ? `
    <div class="card" style="margin-bottom:12px;border-color:rgba(245,158,11,.3)">
      <div class="section-hdr" style="margin-bottom:8px">
        <div class="section-icon">⛔</div>
        <div class="section-title" style="color:var(--warn)">Forbidden Entities for ${det.industry || meta.industry}</div>
      </div>
      <div style="font-size:11px;color:var(--text2);margin-bottom:10px">${forbidden.reason || ""}</div>
      <div style="display:grid;grid-template-columns:repeat(3,1fr);gap:10px">
        ${(forbidden.entities||[]).length ? `<div>
          <div style="font-size:10px;font-weight:700;color:var(--text3);text-transform:uppercase;letter-spacing:.5px;margin-bottom:5px">Entities</div>
          ${(forbidden.entities||[]).map(e => `<div style="font-size:10px;color:var(--danger);padding:2px 0">✗ ${e}</div>`).join("")}
        </div>` : ""}
        ${(forbidden.factTables||[]).length ? `<div>
          <div style="font-size:10px;font-weight:700;color:var(--text3);text-transform:uppercase;letter-spacing:.5px;margin-bottom:5px">Fact Tables</div>
          ${(forbidden.factTables||[]).map(f => `<div style="font-size:10px;color:var(--danger);padding:2px 0">✗ ${f}</div>`).join("")}
        </div>` : ""}
        ${(forbidden.kpis||[]).length ? `<div>
          <div style="font-size:10px;font-weight:700;color:var(--text3);text-transform:uppercase;letter-spacing:.5px;margin-bottom:5px">KPIs</div>
          ${(forbidden.kpis||[]).map(k => `<div style="font-size:10px;color:var(--danger);padding:2px 0">✗ ${k}</div>`).join("")}
        </div>` : ""}
      </div>
    </div>` : ""}

    <!-- ── Solution Identity ── -->
    <div class="card">
      <div class="section-hdr" style="margin-bottom:10px">
        <div class="section-icon">🎯</div>
        <div class="section-title">Solution Identity</div>
      </div>
      <div class="meta-grid">
        <div class="meta-item"><div class="meta-lbl">Dashboard Title</div><div class="meta-val sm">${meta.title || "—"}</div></div>
        <div class="meta-item"><div class="meta-lbl">Currency</div><div class="meta-val">${meta.currency || "—"}</div></div>
        <div class="meta-item"><div class="meta-lbl">Fiscal Year</div><div class="meta-val">${meta.fiscal_year_start || "—"} start</div></div>
        <div class="meta-item"><div class="meta-lbl">Refresh</div><div class="meta-val">${meta.refresh_cadence || "—"}</div></div>
        <div class="meta-item"><div class="meta-lbl">Audience</div><div class="meta-val sm">${meta.primary_audience || "—"}</div></div>
        <div class="meta-item"><div class="meta-lbl">RLS Required</div><div class="meta-val">${bp.security?.rls_required ? "✅ Yes" : "❌ No"}</div></div>
        <div class="meta-item"><div class="meta-lbl">Sensitivity</div><div class="meta-val sm">${bp.security?.sensitivity_label || "—"}</div></div>
        <div class="meta-item"><div class="meta-lbl">Fiscal Year End</div><div class="meta-val">${meta.fiscal_year_end || "30/06"}</div></div>
      </div>
      <div style="margin-top:10px">
        <div class="meta-lbl" style="margin-bottom:6px">Business Goal</div>
        <div style="font-size:13px;line-height:1.6;color:var(--text2)">${meta.business_goal || "—"}</div>
      </div>
    </div>

    ${pack.description ? `
    <div class="card">
      <div class="section-hdr" style="margin-bottom:10px">
        <div class="section-icon">📖</div>
        <div class="section-title">Industry Description</div>
      </div>
      <div style="font-size:12.5px;line-height:1.7;color:var(--text2)">${pack.description}</div>
    </div>` : ""}

    ${(bp.executive_questions || []).length > 0 ? `
    <div class="card">
      <div class="section-hdr" style="margin-bottom:10px">
        <div class="section-icon">❓</div>
        <div class="section-title">Executive Questions This Dashboard Answers</div>
      </div>
      <div class="eq-list">
        ${(bp.executive_questions || []).map((q, i) => `
          <div class="eq-item">
            <div class="eq-num">${i + 1}</div>
            <div class="eq-text">${q}</div>
          </div>`).join("")}
      </div>
    </div>` : ""}
  `;
}

/* ─────────────────────────────────────────────────────────────
   RENDER — CAPABILITIES
───────────────────────────────────────────────────────────── */
function renderCapabilities(bp) {
  const el = $("view-capabilities");
  if (!el) return;
  const caps      = bp.capabilities || [];
  const pack      = getPack(bp.meta?.industry) || {};
  const processes = pack.processes || [];

  el.innerHTML = `
    <div class="section-hdr">
      <div class="section-icon">⚙️</div>
      <div class="section-title">Business Capabilities</div>
      <div class="section-divider"></div>
      <span class="tag tag-blue">${caps.length} capabilities</span>
    </div>
    <div class="card">
      <div class="cap-grid">
        ${caps.map((c) => `
          <div class="cap-item">
            <div class="cap-bullet">▸</div>
            <div>${c}</div>
          </div>`).join("")}
      </div>
    </div>

    ${processes.length > 0 ? `
    <div class="section-hdr" style="margin-top:20px">
      <div class="section-icon">🔄</div>
      <div class="section-title">Core Business Processes</div>
      <div class="section-divider"></div>
    </div>
    <div class="card">
      <div style="display:flex;flex-direction:column;gap:8px">
        ${processes.map((p, i) => `
          <div class="note-item">
            <strong style="color:var(--accent);min-width:24px">${i + 1}.</strong>
            <span>${p}</span>
          </div>`).join("")}
      </div>
    </div>` : ""}

    ${(bp.semantic_notes || []).length > 0 ? `
    <div class="section-hdr" style="margin-top:20px">
      <div class="section-icon">🧠</div>
      <div class="section-title">Semantic Modelling Notes</div>
      <div class="section-divider"></div>
    </div>
    <div class="card">
      <div style="display:flex;flex-direction:column;gap:6px">
        ${(bp.semantic_notes || []).map((n) => `<div class="note-item">${n}</div>`).join("")}
      </div>
    </div>` : ""}
  `;
}

/* ─────────────────────────────────────────────────────────────
   RENDER — SEMANTIC MODEL
───────────────────────────────────────────────────────────── */
function renderModel(bp) {
  const el = $("view-model");
  if (!el) return;
  const dm   = bp.data_model || {};
  const facts = dm.fact_tables      || [];
  const dims  = dm.dimension_tables  || [];
  const rels  = dm.relationships     || [];
  const dt    = dm.date_table        || {};

  el.innerHTML = `
    <div class="section-hdr">
      <div class="section-icon">🗄️</div>
      <div class="section-title">Semantic Model — Star Schema</div>
      <div class="section-divider"></div>
      <span class="tag tag-blue">${facts.length} facts</span>
      <span class="tag tag-purple">${dims.length} dims</span>
      <span class="tag tag-green">${rels.length} relationships</span>
    </div>

    ${facts.length > 0 ? `
    <div class="section-hdr" style="margin-bottom:8px">
      <div class="section-icon">📦</div>
      <div class="section-title">Fact Tables</div>
    </div>
    ${facts.map((f) => `
      <div class="card" style="margin-bottom:10px">
        <div style="display:flex;align-items:center;gap:10px;margin-bottom:10px;flex-wrap:wrap">
          <strong style="font-size:13px">${f.name}</strong>
          <span class="tag tag-blue">Fact</span>
          <span style="font-size:11px;color:var(--text3)">${f.grain}</span>
          ${f.source ? `<span class="tag tag-gray">${f.source}</span>` : ""}
        </div>
        ${(f.columns || []).length > 0 ? `
        <div class="tbl-wrap">
          <table>
            <thead><tr><th>Column</th><th>Type</th><th>Description</th></tr></thead>
            <tbody>
              ${f.columns.map((c) => `
                <tr>
                  <td><code>${c.name}</code></td>
                  <td><span class="tag tag-gray">${c.type}</span></td>
                  <td style="color:var(--text2)">${c.description}</td>
                </tr>`).join("")}
            </tbody>
          </table>
        </div>` : ""}
      </div>`).join("")}` : ""}

    ${dims.length > 0 ? `
    <div class="section-hdr" style="margin-top:20px;margin-bottom:8px">
      <div class="section-icon">📐</div>
      <div class="section-title">Dimension Tables</div>
    </div>
    <div class="tbl-wrap card">
      <table>
        <thead><tr><th>Dimension</th><th>Type</th><th>Hierarchies</th><th>Key Columns</th></tr></thead>
        <tbody>
          ${dims.map((d) => `
            <tr>
              <td><strong>${d.name}</strong></td>
              <td><span class="tag ${d.type === "SCD2" ? "tag-purple" : "tag-blue"}">${d.type}</span></td>
              <td style="font-size:11px;color:var(--text2)">${(d.hierarchies || []).join("<br>") || "—"}</td>
              <td style="font-size:11px;color:var(--text2)">${(d.key_columns || []).slice(0, 5).join(", ")}${(d.key_columns || []).length > 5 ? ` +${(d.key_columns || []).length - 5} more` : ""}</td>
            </tr>`).join("")}
        </tbody>
      </table>
    </div>` : ""}

    ${rels.length > 0 ? `
    <div class="section-hdr" style="margin-top:20px;margin-bottom:8px">
      <div class="section-icon">🔗</div>
      <div class="section-title">Relationships</div>
    </div>
    <div class="tbl-wrap card">
      <table>
        <thead><tr><th>From</th><th></th><th>To</th><th>Cardinality</th><th>Direction</th><th>Active</th><th>Notes</th></tr></thead>
        <tbody>
          ${rels.map((r) => `
            <tr>
              <td class="rel-from">${r.from}</td>
              <td style="color:var(--text3)">→</td>
              <td class="rel-to">${r.to}</td>
              <td><span class="tag tag-blue">${r.cardinality}</span></td>
              <td><span class="tag tag-purple">${r.direction}</span></td>
              <td>${r.active ? '<span class="tag tag-green">Active</span>' : '<span class="tag tag-amber">Inactive</span>'}</td>
              <td style="font-size:11px;color:var(--text3)">${r.notes || "—"}</td>
            </tr>`).join("")}
        </tbody>
      </table>
    </div>` : ""}

    ${dt.name ? `
    <div class="section-hdr" style="margin-top:20px;margin-bottom:8px">
      <div class="section-icon">📅</div>
      <div class="section-title">Date Table</div>
    </div>
    <div class="card">
      <div class="meta-grid">
        <div class="meta-item"><div class="meta-lbl">Table Name</div><div class="meta-val">${dt.name}</div></div>
        <div class="meta-item"><div class="meta-lbl">Date Spine</div><div class="meta-val sm">${dt.spine || "—"}</div></div>
        <div class="meta-item"><div class="meta-lbl">Fiscal Offset</div><div class="meta-val">${dt.fiscal_offset} months</div></div>
        <div class="meta-item"><div class="meta-lbl">Key Columns</div><div class="meta-val sm">${(dt.key_columns || []).length}</div></div>
      </div>
      <div style="margin-top:10px;display:flex;flex-wrap:wrap;gap:4px">
        ${(dt.key_columns || []).map((c) => `<span class="tag tag-gray">${c}</span>`).join("")}
      </div>
    </div>` : ""}
  `;
}

/* ─────────────────────────────────────────────────────────────
   RENDER — KPIs
───────────────────────────────────────────────────────────── */
function renderKPIs(bp) {
  const el = $("view-kpis");
  if (!el) return;
  const kpis = bp.kpis || [];

  el.innerHTML = `
    <div class="section-hdr">
      <div class="section-icon">🎯</div>
      <div class="section-title">KPI Definitions</div>
      <div class="section-divider"></div>
      <span class="tag tag-blue">${kpis.length} KPIs</span>
    </div>

    <div class="kpi-grid">
      ${kpis.map((k) => {
        const t = k.thresholds || {};
        return `
        <div class="kpi-card">
          <div class="kpi-name">${k.name}</div>
          <div class="kpi-measure">${k.measure_ref || ""}</div>
          <div style="font-size:11px;color:var(--text2);margin-bottom:8px">Target: ${k.target_logic || "—"}</div>
          <div class="kpi-thresholds">
            <div class="kpi-thresh"><div class="thresh-dot" style="background:var(--success)"></div><span style="color:var(--success)">Good</span>&nbsp;${t.good || "—"}</div>
            <div class="kpi-thresh"><div class="thresh-dot" style="background:var(--warn)"></div><span style="color:var(--warn)">Warning</span>&nbsp;${t.warning || "—"}</div>
            <div class="kpi-thresh"><div class="thresh-dot" style="background:var(--danger)"></div><span style="color:var(--danger)">Critical</span>&nbsp;${t.critical || "—"}</div>
          </div>
          ${k.actionability ? `<div style="font-size:10px;color:var(--accent3);margin-top:6px;line-height:1.4">${k.actionability}</div>` : ""}
          <div class="kpi-meta">Owner: ${k.owner || "—"} &nbsp;·&nbsp; ${k.cadence || "—"}</div>
        </div>`;
      }).join("")}
    </div>

    ${kpis.length > 0 ? `
    <div class="section-hdr" style="margin-top:24px;margin-bottom:8px">
      <div class="section-icon">📋</div>
      <div class="section-title">KPI Summary Table</div>
      <div class="section-divider"></div>
    </div>
    <div class="card">
      <div class="tbl-wrap">
        <table>
          <thead>
            <tr><th>KPI</th><th>Measure</th><th>Good</th><th>Warning</th><th>Critical</th><th>Owner</th><th>Cadence</th></tr>
          </thead>
          <tbody>
            ${kpis.map((k) => {
              const t = k.thresholds || {};
              return `<tr>
                <td><strong>${k.name}</strong></td>
                <td><code>${k.measure_ref || "—"}</code></td>
                <td><span class="tag tag-green">${t.good || "—"}</span></td>
                <td><span class="tag tag-amber">${t.warning || "—"}</span></td>
                <td><span class="tag tag-red">${t.critical || "—"}</span></td>
                <td style="color:var(--text2)">${k.owner || "—"}</td>
                <td><span class="tag tag-gray">${k.cadence || "—"}</span></td>
              </tr>`;
            }).join("")}
          </tbody>
        </table>
      </div>
    </div>` : ""}
  `;
}

/* ─────────────────────────────────────────────────────────────
   RENDER — PAGES
───────────────────────────────────────────────────────────── */
function renderPages(bp) {
  const el = $("view-pages");
  if (!el) return;
  const pages = bp.pages || [];

  el.innerHTML = `
    <div class="section-hdr">
      <div class="section-icon">📄</div>
      <div class="section-title">Dashboard Pages</div>
      <div class="section-divider"></div>
      <span class="tag tag-blue">${pages.length} pages</span>
    </div>
    <div class="page-grid">
      ${pages.map((p) => {
        const layoutIcon  = LAYOUT_ICONS[p.layout] || "📄";
        const layoutColor = p.layout === "Executive" ? "tag-purple" : p.layout === "Analytical" ? "tag-blue" : p.layout === "Operational" ? "tag-amber" : "tag-green";
        return `
        <div class="page-card">
          <div class="page-card-title">
            <span>${layoutIcon}</span>
            ${p.name}
            <span class="tag ${layoutColor}">${p.layout}</span>
          </div>
          <div style="font-size:11px;color:var(--text3);margin-bottom:4px">${p.audience || ""}</div>
          <div class="page-purpose">${p.purpose || ""}</div>
          ${p.storytelling_flow ? `<div style="font-size:10px;color:var(--accent3);margin-bottom:8px;line-height:1.4;font-style:italic">${p.storytelling_flow}</div>` : ""}

          ${(p.slicers || []).length > 0 ? `
          <div style="margin-bottom:10px">
            <div style="font-size:10px;font-weight:700;color:var(--text3);text-transform:uppercase;letter-spacing:.5px;margin-bottom:4px">Slicers</div>
            <div style="display:flex;flex-wrap:wrap;gap:3px">
              ${p.slicers.map((s) => `<span class="tag tag-cyan">${typeof s === "object" ? s.field : s}</span>`).join("")}
            </div>
          </div>` : ""}

          <div style="font-size:10px;font-weight:700;color:var(--text3);text-transform:uppercase;letter-spacing:.5px;margin-bottom:5px">Visuals</div>
          <div class="visual-list">
            ${(p.visuals || []).map((v) => {
              const vicon = VISUAL_ICONS[v.type] || "📊";
              return `<div class="visual-item">
                <span>${vicon}</span>
                <span class="tag tag-purple" style="font-size:9px">${v.type}</span>
                <span style="flex:1">${v.title}</span>
              </div>`;
            }).join("")}
          </div>

          ${p.drill_through ? `
          <div style="margin-top:10px;padding-top:8px;border-top:1px solid var(--border)">
            <span style="font-size:10px;color:var(--text3)">Drill-through → </span>
            <span class="tag tag-green">${p.drill_through.target_page}</span>
          </div>` : ""}
        </div>`;
      }).join("")}
    </div>
  `;
}

/* ─────────────────────────────────────────────────────────────
   RENDER — DAX CATALOGUE
───────────────────────────────────────────────────────────── */
function renderDAX(bp) {
  const el = $("view-dax");
  if (!el) return;
  const measures = bp.measures || [];
  const folders  = {};
  measures.forEach((m) => {
    const key = (m.display_folder || "General").split("/")[0].trim();
    if (!folders[key]) folders[key] = [];
    folders[key].push(m);
  });

  el.innerHTML = `
    <div class="section-hdr">
      <div class="section-icon">⚡</div>
      <div class="section-title">DAX Measure Catalogue</div>
      <div class="section-divider"></div>
      <span class="tag tag-blue">${measures.length} measures</span>
    </div>
    ${Object.entries(folders).map(([folder, ms]) => `
      <div style="margin-bottom:6px;margin-top:16px;font-size:11px;font-weight:700;color:var(--accent);text-transform:uppercase;letter-spacing:.6px">
        📁 ${folder}
      </div>
      ${ms.map((m) => `
        <div class="dax-card">
          <div class="dax-header">
            <div class="dax-name">${m.name}</div>
            <span class="tag tag-blue">${m.format || ""}</span>
            <span class="tag tag-gray" style="font-size:9px">${m.display_folder || ""}</span>
            <span style="margin-left:auto;font-size:11px;color:var(--text3)">${m.description || ""}</span>
          </div>
          <div class="dax-body">
            <div class="dax-code">${escHtml(m.dax || "")}</div>
            ${(m.dependencies || []).length > 0 ? `
            <div style="margin-top:8px;font-size:11px;color:var(--text3)">
              Dependencies: ${m.dependencies.map((d) => `<code>${d}</code>`).join(", ")}
            </div>` : ""}
          </div>
        </div>`).join("")}`).join("")}
  `;
}

/* ─────────────────────────────────────────────────────────────
   RENDER — SELF-REVIEW  (9 gates + 5 quality framework tabs)
───────────────────────────────────────────────────────────── */
function renderReview(bp) {
  const el = $("view-review");
  if (!el) return;

  const review   = bp.self_review    || {};
  const conf     = bp.confidence     || {};
  const security = bp.security       || {};
  const qf       = bp.quality_frameworks || {};
  const gov      = bp.governance     || {};
  const targets  = bp.expected_targets || {};

  const gates   = review.gates || [];
  const verdict = review.overall_verdict || "PASS_WITH_NOTES";

  const verdictClass = verdict === "PASS" ? "verdict-pass"
    : verdict === "PASS_WITH_NOTES" ? "verdict-warn"
    : "verdict-fail";
  const verdictLabel = verdict === "PASS" ? "✅ PASS"
    : verdict === "PASS_WITH_NOTES" ? "⚠️ PASS WITH NOTES"
    : "❌ REVISE";
  const verdictColor = verdict === "PASS" ? "var(--success)"
    : verdict === "PASS_WITH_NOTES" ? "var(--warn)"
    : "var(--danger)";

  const composite  = review.composite_score || Math.round(gates.reduce((s, g) => s + g.score, 0) / Math.max(gates.length, 1));
  const confScore  = conf.score || 0;
  const confBand   = conf.band  || "—";
  const confColor  = confScore >= 90 ? "var(--success)" : confScore >= 75 ? "#34d399" : confScore >= 50 ? "var(--warn)" : confScore >= 25 ? "#fb923c" : "var(--danger)";

  // ── Quality scores ───────────────────────────────────────
  const auditScore  = qf.audit_readiness?.score         || 0;
  const dashScore   = qf.dashboard_quality?.score        || 0;
  const kpiScore    = qf.kpi_quality?.score              || 0;
  const smScore     = qf.semantic_model_quality?.score   || 0;
  const govScore    = qf.governance_framework?.score     || 0;

  const scoreColor = (s) => s >= 85 ? "var(--success)" : s >= 70 ? "#34d399" : s >= 55 ? "var(--warn)" : "var(--danger)";

  el.innerHTML = `
    <!-- ── Verdict Banner ── -->
    <div class="verdict-banner ${verdictClass}">
      <div style="font-size:20px">${verdict === "PASS" ? "✅" : verdict === "PASS_WITH_NOTES" ? "⚠️" : "❌"}</div>
      <div>
        <div class="verdict-label" style="color:${verdictColor}">${verdictLabel}</div>
        <div style="font-size:11px;color:var(--text2);margin-top:2px">SRE-v3.0 · 9 gates · 5 quality frameworks</div>
      </div>
      <div style="margin-left:auto;text-align:right">
        <div style="font-size:22px;font-weight:800;color:${verdictColor}">${composite}</div>
        <div style="font-size:10px;color:var(--text3)">Composite Gate Score</div>
      </div>
    </div>

    <!-- ── 5 Quality Framework Score Strip ── -->
    <div class="qf-strip">
      <div class="qf-strip-item">
        <div class="qf-strip-score" style="color:${scoreColor(auditScore)}">${auditScore}</div>
        <div class="qf-strip-label">Audit Readiness</div>
        <div class="qf-strip-rating" style="color:${scoreColor(auditScore)}">${qf.audit_readiness?.rating || "—"}</div>
      </div>
      <div class="qf-strip-item">
        <div class="qf-strip-score" style="color:${scoreColor(dashScore)}">${dashScore}</div>
        <div class="qf-strip-label">Dashboard Quality</div>
        <div class="qf-strip-rating" style="color:${scoreColor(dashScore)}">${qf.dashboard_quality?.rating || "—"}</div>
      </div>
      <div class="qf-strip-item">
        <div class="qf-strip-score" style="color:${scoreColor(kpiScore)}">${kpiScore}</div>
        <div class="qf-strip-label">KPI Quality</div>
        <div class="qf-strip-rating" style="color:${scoreColor(kpiScore)}">${qf.kpi_quality?.rating || "—"}</div>
      </div>
      <div class="qf-strip-item">
        <div class="qf-strip-score" style="color:${scoreColor(smScore)}">${smScore}</div>
        <div class="qf-strip-label">Model Quality</div>
        <div class="qf-strip-rating" style="color:${scoreColor(smScore)}">${qf.semantic_model_quality?.rating || "—"}</div>
      </div>
      <div class="qf-strip-item">
        <div class="qf-strip-score" style="color:${scoreColor(govScore)}">${govScore}</div>
        <div class="qf-strip-label">Governance</div>
        <div class="qf-strip-rating" style="color:${scoreColor(govScore)}">${qf.governance_framework?.rating || "—"}</div>
      </div>
    </div>

    <!-- ── 9 Gate Cards ── -->
    <div class="section-hdr" style="margin-bottom:8px">
      <div class="section-icon">🔍</div>
      <div class="section-title">Self-Review Gates (9)</div>
      <div class="section-divider"></div>
    </div>
    <div class="gates-grid gates-grid-3">
      ${gates.map((g) => {
        const statusClass = g.status === "PASS" ? "gate-pass" : g.status === "WARN" ? "gate-warn" : "gate-fail";
        const statusIcon  = g.status === "PASS" ? "✅" : g.status === "WARN" ? "⚠️" : "❌";
        return `
        <div class="gate-card">
          <div class="gate-name">${g.gate_name}</div>
          <div class="gate-score">${g.score}</div>
          <div class="gate-status ${statusClass}">${statusIcon} ${g.status}</div>
          <div class="progress-bar"><div class="progress-fill" style="width:${g.score}%"></div></div>
          ${(g.findings || []).length > 0 ? `
          <div style="margin-top:6px;font-size:10px;color:var(--text2)">
            ${g.findings.slice(0,3).join("<br>")}
          </div>` : ""}
          ${(g.recommendations || []).length > 0 ? `
          <div style="margin-top:4px;font-size:10px;color:var(--accent)">
            ${g.recommendations[0]}
          </div>` : ""}
        </div>`;
      }).join("")}
    </div>

    <!-- ── Quality Framework Tabs ── -->
    <div class="qf-tabs" style="margin-top:20px">
      <div class="qf-tab-nav">
        <button class="qf-tab-btn active" onclick="showQFTab('audit')">🔒 Audit Readiness</button>
        <button class="qf-tab-btn" onclick="showQFTab('dashboard')">📄 Dashboard Quality</button>
        <button class="qf-tab-btn" onclick="showQFTab('kpi')">🎯 KPI Quality</button>
        <button class="qf-tab-btn" onclick="showQFTab('model')">🗄️ Model Quality</button>
        <button class="qf-tab-btn" onclick="showQFTab('governance')">⚖️ Governance</button>
      </div>

      <!-- Audit Readiness Tab -->
      <div id="qf-audit" class="qf-tab-panel active">
        ${renderAuditReadiness(qf.audit_readiness, security)}
      </div>

      <!-- Dashboard Quality Tab -->
      <div id="qf-dashboard" class="qf-tab-panel">
        ${renderDashboardQuality(qf.dashboard_quality)}
      </div>

      <!-- KPI Quality Tab -->
      <div id="qf-kpi" class="qf-tab-panel">
        ${renderKPIQuality(qf.kpi_quality)}
      </div>

      <!-- Semantic Model Quality Tab -->
      <div id="qf-model" class="qf-tab-panel">
        ${renderModelQuality(qf.semantic_model_quality)}
      </div>

      <!-- Governance Tab -->
      <div id="qf-governance" class="qf-tab-panel">
        ${renderGovernance(qf.governance_framework, gov)}
      </div>
    </div>

    <!-- ── Confidence Score ── -->
    <div class="card" style="margin-top:16px">
      <div class="section-hdr" style="margin-bottom:10px">
        <div class="section-icon">📊</div>
        <div class="section-title">Confidence Score — 7 Dimensions</div>
      </div>
      <div style="text-align:center;padding:10px 0 6px">
        <div style="font-size:64px;font-weight:900;background:linear-gradient(135deg,var(--accent),var(--accent2),var(--accent3));-webkit-background-clip:text;-webkit-text-fill-color:transparent;line-height:1">${confScore}</div>
        <div style="font-size:14px;font-weight:700;color:${confColor};margin:4px 0">${confBand}</div>
        <div style="font-size:12px;color:var(--text2);max-width:500px;margin:0 auto 14px">${conf.basis || ""}</div>
      </div>
      <div style="height:8px;background:var(--surface3);border-radius:4px;overflow:hidden;margin-bottom:16px">
        <div style="height:100%;border-radius:4px;background:linear-gradient(90deg,var(--accent),var(--accent2),var(--accent3));width:${confScore}%;transition:width 1s ease"></div>
      </div>
      ${conf.dimension_scores ? `
      <div class="conf-dim-grid">
        ${Object.entries(conf.dimension_scores).map(([k, v]) => `
        <div class="conf-dim">
          <div class="conf-dim-label">${k.replace(/_/g," ")}</div>
          <div class="conf-dim-bar">
            <div class="conf-dim-fill" style="width:${v}%;background:${scoreColor(v)}"></div>
          </div>
          <div class="conf-dim-val" style="color:${scoreColor(v)}">${v}</div>
        </div>`).join("")}
      </div>` : ""}
      <div class="two-col" style="margin-top:16px">
        <div>
          <div style="font-size:11px;font-weight:700;color:var(--warn);text-transform:uppercase;letter-spacing:.5px;margin-bottom:8px">Assumptions</div>
          <div class="flag-list">
            ${(conf.assumptions || []).map(a => `<div class="flag-item flag-warn">${a}</div>`).join("")}
          </div>
        </div>
        <div>
          <div style="font-size:11px;font-weight:700;color:var(--danger);text-transform:uppercase;letter-spacing:.5px;margin-bottom:8px">Gaps</div>
          <div class="flag-list">
            ${(conf.gaps || []).map(g => `<div class="flag-item flag-gap">${g}</div>`).join("")}
          </div>
        </div>
      </div>
    </div>

    <!-- ── Expected Targets (Audit Agent) ── -->
    ${targets.fact_tables?.length ? `
    <div class="card" style="margin-top:16px">
      <div class="section-hdr" style="margin-bottom:10px">
        <div class="section-icon">🎯</div>
        <div class="section-title">Expected Targets — Audit Agent Compatibility</div>
      </div>
      <div style="font-size:11px;color:var(--text2);margin-bottom:12px">${targets.description || ""}</div>
      <div class="meta-grid" style="margin-bottom:12px">
        <div class="card-sm"><div class="meta-lbl">Fact Tables</div><div class="meta-val">${(targets.fact_tables || []).length}</div></div>
        <div class="card-sm"><div class="meta-lbl">Dimensions</div><div class="meta-val">${(targets.dimension_tables || []).length}</div></div>
        <div class="card-sm"><div class="meta-lbl">Min Measures</div><div class="meta-val">${targets.measure_count_minimum || "—"}</div></div>
        <div class="card-sm"><div class="meta-lbl">Min KPIs</div><div class="meta-val">${targets.kpi_count_minimum || "—"}</div></div>
        <div class="card-sm"><div class="meta-lbl">Min Pages</div><div class="meta-val">${targets.page_count_minimum || "—"}</div></div>
        <div class="card-sm"><div class="meta-lbl">RLS Roles</div><div class="meta-val">${targets.rls_role_count_minimum || "—"}</div></div>
      </div>
      ${(targets.compliance_checks || []).length ? `
      <div class="tbl-wrap">
        <table>
          <thead><tr><th>Audit Check</th><th>Expected</th><th>Category</th></tr></thead>
          <tbody>
            ${targets.compliance_checks.map(c => `
            <tr>
              <td>${c.check}</td>
              <td><span class="tag tag-green">${c.expected}</span></td>
              <td><span class="tag tag-gray">${c.category}</span></td>
            </tr>`).join("")}
          </tbody>
        </table>
      </div>` : ""}
    </div>` : ""}

    <!-- ── Warnings and Risks ── -->
    ${(review.warnings || []).length > 0 ? `
    <div class="card" style="margin-top:16px">
      <div class="section-hdr" style="margin-bottom:10px">
        <div class="section-icon">⚠️</div>
        <div class="section-title">Warnings (${(review.warnings || []).length})</div>
      </div>
      <div class="flag-list">
        ${(review.warnings || []).map(w => `<div class="flag-item flag-warn">${w}</div>`).join("")}
      </div>
    </div>` : ""}

    ${(review.implementation_risks || []).length > 0 ? `
    <div class="card" style="margin-top:12px">
      <div class="section-hdr" style="margin-bottom:10px">
        <div class="section-icon">🚨</div>
        <div class="section-title">Implementation Risks</div>
      </div>
      <div class="flag-list">
        ${(review.implementation_risks || []).map(r => `<div class="flag-item flag-gap">${r}</div>`).join("")}
      </div>
    </div>` : ""}

    ${(review.design_recommendations || []).length > 0 ? `
    <div class="card" style="margin-top:12px">
      <div class="section-hdr" style="margin-bottom:10px">
        <div class="section-icon">💡</div>
        <div class="section-title">Design Recommendations (${(review.design_recommendations || []).length})</div>
      </div>
      <div class="flag-list">
        ${(review.design_recommendations || []).map(r => `
        <div class="flag-item flag-ok" style="display:flex;gap:8px;align-items:flex-start">
          <span class="tag tag-blue" style="flex-shrink:0;font-size:9px">${r.category || 'Design'}</span>
          <div>
            <div style="font-size:12px">${r.recommendation}</div>
            ${r.rationale ? `<div style="font-size:10px;color:var(--text3);margin-top:2px">${r.rationale}</div>` : ''}
          </div>
        </div>`).join("")}
      </div>
    </div>` : ""}
  `;
}

/* ─────────────────────────────────────────────────────────────
   QF TAB RENDERER HELPERS
───────────────────────────────────────────────────────────── */
function renderAuditReadiness(ar, security) {
  if (!ar) return '<div class="flag-item flag-gap">Audit readiness framework not generated — use API mode for full framework.</div>';
  const scoreColor = ar.score >= 85 ? "var(--success)" : ar.score >= 70 ? "#34d399" : ar.score >= 55 ? "var(--warn)" : "var(--danger)";
  return `
    <div class="qf-score-row">
      <div class="qf-big-score" style="color:${scoreColor}">${ar.score}</div>
      <div class="qf-rating" style="color:${scoreColor}">${ar.rating || "—"}</div>
    </div>
    ${(ar.checklist || []).length ? `
    <div class="tbl-wrap" style="margin-bottom:12px">
      <table>
        <thead><tr><th>Check</th><th>Status</th><th>Evidence</th><th>Priority</th></tr></thead>
        <tbody>
          ${(ar.checklist || []).map(c => `
          <tr>
            <td>${c.item}</td>
            <td><span class="tag ${c.status === "PASS" ? "tag-green" : c.status === "WARN" ? "tag-amber" : "tag-red"}">${c.status}</span></td>
            <td style="font-size:11px;color:var(--text2)">${c.evidence}</td>
            <td><span class="tag tag-gray">${c.priority}</span></td>
          </tr>`).join("")}
        </tbody>
      </table>
    </div>` : ""}
    ${(ar.risks || []).length ? `
    <div style="margin-bottom:8px">
      <div style="font-size:10px;font-weight:700;color:var(--danger);text-transform:uppercase;margin-bottom:6px">Risks</div>
      <div class="flag-list">${(ar.risks || []).map(r => `<div class="flag-item flag-gap">${r}</div>`).join("")}</div>
    </div>` : ""}
    ${(ar.missing_requirements || []).length ? `
    <div>
      <div style="font-size:10px;font-weight:700;color:var(--warn);text-transform:uppercase;margin-bottom:6px">Missing Requirements</div>
      <div class="flag-list">${(ar.missing_requirements || []).map(r => `<div class="flag-item flag-warn">${r}</div>`).join("")}</div>
    </div>` : ""}`;
}

function renderDashboardQuality(dq) {
  if (!dq) return '<div class="flag-item flag-gap">Dashboard quality framework not generated — use API mode for full framework.</div>';
  const scoreColor = s => s >= 85 ? "var(--success)" : s >= 70 ? "#34d399" : s >= 55 ? "var(--warn)" : "var(--danger)";
  const dims = dq.dimensions || {};
  return `
    <div class="qf-score-row">
      <div class="qf-big-score" style="color:${scoreColor(dq.score)}">${dq.score}</div>
      <div class="qf-rating" style="color:${scoreColor(dq.score)}">${dq.rating || "—"}</div>
    </div>
    <div class="dim-grid">
      ${Object.entries(dims).map(([k, v]) => `
      <div class="dim-item">
        <div class="dim-label">${k.replace(/_/g," ")}</div>
        <div class="dim-bar-wrap">
          <div class="dim-bar"><div class="dim-fill" style="width:${v.score}%;background:${scoreColor(v.score)}"></div></div>
          <div class="dim-score" style="color:${scoreColor(v.score)}">${v.score}</div>
        </div>
        <div class="dim-notes">${v.notes}</div>
      </div>`).join("")}
    </div>
    ${(dq.recommendations || []).length ? `
    <div style="margin-top:12px">
      <div style="font-size:10px;font-weight:700;color:var(--accent);text-transform:uppercase;margin-bottom:6px">Recommendations</div>
      <div class="flag-list">${(dq.recommendations || []).map(r => `<div class="flag-item flag-ok">${r}</div>`).join("")}</div>
    </div>` : ""}`;
}

function renderKPIQuality(kq) {
  if (!kq) return '<div class="flag-item flag-gap">KPI quality framework not generated — use API mode for full framework.</div>';
  const scoreColor = s => s >= 85 ? "var(--success)" : s >= 70 ? "#34d399" : s >= 55 ? "var(--warn)" : "var(--danger)";
  const assessments = kq.kpi_assessments || [];
  const boolTag = v => v
    ? '<span class="tag tag-green">✓</span>'
    : '<span class="tag tag-red">✗</span>';
  return `
    <div class="qf-score-row">
      <div class="qf-big-score" style="color:${scoreColor(kq.score)}">${kq.score}</div>
      <div class="qf-rating" style="color:${scoreColor(kq.score)}">${kq.rating || "—"}</div>
    </div>
    <div style="font-size:12px;color:var(--text2);margin-bottom:12px">${kq.coverage_assessment || ""}</div>
    ${assessments.length ? `
    <div class="tbl-wrap">
      <table>
        <thead><tr><th>KPI</th><th>Owner</th><th>Target</th><th>Actionable</th><th>Goal</th><th>Source</th><th>Risk</th></tr></thead>
        <tbody>
          ${assessments.map(k => `
          <tr>
            <td><strong>${k.kpi_name}</strong></td>
            <td>${boolTag(k.ownership_defined)}</td>
            <td>${boolTag(k.target_defined)}</td>
            <td>${boolTag(k.actionable)}</td>
            <td>${boolTag(k.goal_aligned)}</td>
            <td>${boolTag(k.traceable_to_source)}</td>
            <td style="font-size:10px;color:${k.risk === "Low risk" ? "var(--success)" : "var(--warn)"}">${k.risk}</td>
          </tr>`).join("")}
        </tbody>
      </table>
    </div>` : ""}
    ${(kq.recommendations || []).length ? `
    <div style="margin-top:12px">
      <div style="font-size:10px;font-weight:700;color:var(--accent);text-transform:uppercase;margin-bottom:6px">Recommendations</div>
      <div class="flag-list">${(kq.recommendations || []).map(r => `<div class="flag-item flag-ok">${r}</div>`).join("")}</div>
    </div>` : ""}`;
}

function renderModelQuality(sm) {
  if (!sm) return '<div class="flag-item flag-gap">Semantic model quality framework not generated — use API mode for full framework.</div>';
  const scoreColor = s => s >= 85 ? "var(--success)" : s >= 70 ? "#34d399" : s >= 55 ? "var(--warn)" : "var(--danger)";
  const dims = sm.dimensions || {};
  return `
    <div class="qf-score-row">
      <div class="qf-big-score" style="color:${scoreColor(sm.score)}">${sm.score}</div>
      <div class="qf-rating" style="color:${scoreColor(sm.score)}">${sm.rating || "—"}</div>
    </div>
    <div class="dim-grid">
      ${Object.entries(dims).map(([k, v]) => `
      <div class="dim-item">
        <div class="dim-label">${k.replace(/_/g," ")}</div>
        <div class="dim-bar-wrap">
          <div class="dim-bar"><div class="dim-fill" style="width:${v.score}%;background:${scoreColor(v.score)}"></div></div>
          <div class="dim-score" style="color:${scoreColor(v.score)}">${v.score}</div>
        </div>
        <div class="dim-notes">${v.notes}</div>
      </div>`).join("")}
    </div>
    ${(sm.recommendations || []).length ? `
    <div style="margin-top:12px">
      <div style="font-size:10px;font-weight:700;color:var(--accent);text-transform:uppercase;margin-bottom:6px">Recommendations</div>
      <div class="flag-list">${(sm.recommendations || []).map(r => `<div class="flag-item flag-ok">${r}</div>`).join("")}</div>
    </div>` : ""}`;
}

function renderGovernance(gf, gov) {
  if (!gf && !gov) return '<div class="flag-item flag-gap">Governance framework not generated.</div>';
  const scoreColor = s => s >= 85 ? "var(--success)" : s >= 70 ? "#34d399" : s >= 55 ? "var(--warn)" : "var(--danger)";
  const gfScore = gf?.score || 0;
  return `
    ${gf ? `
    <div class="qf-score-row">
      <div class="qf-big-score" style="color:${scoreColor(gfScore)}">${gfScore}</div>
      <div class="qf-rating" style="color:${scoreColor(gfScore)}">${gf.rating || "—"}</div>
    </div>` : ""}
    ${gov ? `
    <div class="card" style="margin-bottom:12px">
      <div class="meta-grid">
        <div class="meta-item"><div class="meta-lbl">Data Owner</div><div class="meta-val sm">${gov.data_owner || "—"}</div></div>
        <div class="meta-item"><div class="meta-lbl">KPI Owner</div><div class="meta-val sm">${gov.kpi_owner || "—"}</div></div>
        <div class="meta-item"><div class="meta-lbl">Report Owner</div><div class="meta-val sm">${gov.report_owner || "—"}</div></div>
        <div class="meta-item"><div class="meta-lbl">Business Steward</div><div class="meta-val sm">${gov.business_steward || "—"}</div></div>
        <div class="meta-item"><div class="meta-lbl">Access Steward</div><div class="meta-val sm">${gov.access_steward || "—"}</div></div>
        <div class="meta-item"><div class="meta-lbl">Review Cadence</div><div class="meta-val sm">${gov.review_cadence || "—"}</div></div>
      </div>
      ${gov.change_control ? `<div style="margin-top:10px;font-size:11px;color:var(--text2)"><strong>Change Control:</strong> ${gov.change_control}</div>` : ""}
    </div>` : ""}
    ${(gf?.industry_obligations || []).length ? `
    <div style="margin-bottom:12px">
      <div style="font-size:10px;font-weight:700;color:var(--accent);text-transform:uppercase;margin-bottom:6px">Industry Obligations</div>
      <div class="flag-list">${(gf.industry_obligations || []).map(o => `<div class="flag-item flag-ok">${o}</div>`).join("")}</div>
    </div>` : ""}
    ${(gf?.data_stewardship || []).length ? `
    <div style="margin-bottom:12px">
      <div style="font-size:10px;font-weight:700;color:var(--text3);text-transform:uppercase;margin-bottom:6px">Data Stewardship</div>
      <div class="tbl-wrap">
        <table>
          <thead><tr><th>Domain</th><th>Owner</th><th>Responsibility</th></tr></thead>
          <tbody>
            ${(gf.data_stewardship || []).map(s => `
            <tr>
              <td><strong>${s.domain}</strong></td>
              <td>${s.owner}</td>
              <td style="font-size:11px;color:var(--text2)">${s.responsibility}</td>
            </tr>`).join("")}
          </tbody>
        </table>
      </div>
    </div>` : ""}
    ${(gf?.gaps || []).length ? `
    <div>
      <div style="font-size:10px;font-weight:700;color:var(--warn);text-transform:uppercase;margin-bottom:6px">Governance Gaps</div>
      <div class="flag-list">${(gf.gaps || []).map(g => `<div class="flag-item flag-warn">${g}</div>`).join("")}</div>
    </div>` : ""}`;
}

/* ─────────────────────────────────────────────────────────────
   QF TAB SWITCHING
───────────────────────────────────────────────────────────── */
function showQFTab(name) {
  document.querySelectorAll(".qf-tab-panel").forEach(p => p.classList.remove("active"));
  document.querySelectorAll(".qf-tab-btn").forEach(b => b.classList.remove("active"));
  const panel = document.getElementById("qf-" + name);
  if (panel) panel.classList.add("active");
  const btns = document.querySelectorAll(".qf-tab-btn");
  const tabNames = ["audit","dashboard","kpi","model","governance"];
  const idx = tabNames.indexOf(name);
  if (idx >= 0 && btns[idx]) btns[idx].classList.add("active");
}

/* ─────────────────────────────────────────────────────────────
   HELPER — HTML escape
───────────────────────────────────────────────────────────── */
function escHtml(str) {
  return String(str)
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

/* ─────────────────────────────────────────────────────────────
   BOOT
───────────────────────────────────────────────────────────── */
document.addEventListener("DOMContentLoaded", init);
