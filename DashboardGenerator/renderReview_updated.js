/* =============================================================
   app.js — UPDATED SECTIONS
   
   This file contains the updated renderReview() function and the
   updated buildDemoBlueprint() return object wiring.
   
   INSTRUCTIONS:
   1. Replace the existing renderReview() function in app.js with
      the version below.
   2. Replace the return statement in buildDemoBlueprint() with
      the version below to wire in the 5 quality frameworks,
      governance block, expected targets, and new confidence model.
   3. Add updateNavCounters() quality badge update.
   4. Add the renderQualityTab() helper function.
   5. Add CSS for quality framework tabs (see styles section).
============================================================= */

/* ─────────────────────────────────────────────────────────────
   UPDATED renderReview()
   Replaces the existing renderReview() in app.js entirely.
   Now renders 5 quality framework tabs + redesigned gate grid.
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
        <div style="font-size:11px;color:var(--text2);margin-top:2px">SRE-v3.0 · 9 gates · ${(review.auto_corrections || []).length} auto-corrections · 5 quality frameworks</div>
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

    ${(review.auto_corrections || []).length > 0 ? `
    <div class="card" style="margin-top:12px">
      <div class="section-hdr" style="margin-bottom:10px">
        <div class="section-icon">⚙️</div>
        <div class="section-title">Auto-Corrections Applied (${(review.auto_corrections || []).length})</div>
      </div>
      <div class="tbl-wrap">
        <table>
          <thead><tr><th>Field</th><th>Before</th><th>After</th><th>Reason</th></tr></thead>
          <tbody>
            ${(review.auto_corrections || []).map(c => `
            <tr>
              <td><code>${c.field}</code></td>
              <td><span class="del-val">${c.before}</span></td>
              <td><span class="add-val">${c.after}</span></td>
              <td style="font-size:11px;color:var(--text2)">${c.reason}</td>
            </tr>`).join("")}
          </tbody>
        </table>
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
   UPDATED buildDemoBlueprint — RETURN STATEMENT WIRING
   
   In app.js, find the return statement in buildDemoBlueprint()
   and REPLACE the "governance", "quality_frameworks",
   "expected_targets", "self_review", and "confidence" fields
   with the versions below.
   
   Also add "governance" to the return BEFORE "semantic_notes".
───────────────────────────────────────────────────────────── */

/*
  Inside buildDemoBlueprint(), after building measures, kpis, pages,
  securityRoles, piiCols, sensitivityLabel etc., ADD these lines:

    const industry = detectedIndustry;
    const qfResult = buildQualityFrameworks(measures, kpis, pages, pack, {
      rls, industry, requirements, schema,
    });
    const govBlock = buildGovernanceBlock(industry, pack, {
      rls, fy, currency,
    });
    const expectedTargets = buildExpectedTargets(measures, kpis, pages, securityRoles, pack);
    const confResult = buildConfidenceScore(
      { fy, currency, rls, refresh, requirements, schema, industryExplicit,
        detectionConfidence: detResult.confidence || 75, industry },
      hasRequirements, hasSchema, pack, measures, kpis, pages, securityRoles
    );
    const selfReviewResult = buildSelfReview(
      measures, kpis, pages, securityRoles, rls, confResult.score, pack
    );

  Then in the return object, replace / add these fields:

    governance: govBlock,
    quality_frameworks: qfResult,
    expected_targets: expectedTargets,
    self_review: selfReviewResult,
    confidence: confResult,
*/

/* ─────────────────────────────────────────────────────────────
   CSS TO ADD TO styles.css
───────────────────────────────────────────────────────────── */
/*
Add these CSS rules to styles.css:

.qf-strip {
  display: grid;
  grid-template-columns: repeat(5, 1fr);
  gap: 10px;
  margin-bottom: 16px;
  padding: 14px;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius);
}
.qf-strip-item { text-align: center; }
.qf-strip-score { font-size: 28px; font-weight: 800; line-height: 1; }
.qf-strip-label { font-size: 10px; color: var(--text3); text-transform: uppercase; letter-spacing: .5px; margin: 4px 0 2px; }
.qf-strip-rating { font-size: 10px; font-weight: 600; }

.gates-grid-3 { grid-template-columns: repeat(3, 1fr); }

.qf-tabs { background: var(--surface); border: 1px solid var(--border); border-radius: var(--radius); overflow: hidden; }
.qf-tab-nav { display: flex; border-bottom: 1px solid var(--border); overflow-x: auto; }
.qf-tab-btn {
  padding: 10px 14px; font-size: 12px; font-weight: 600;
  background: none; border: none; color: var(--text3);
  cursor: pointer; white-space: nowrap; border-bottom: 2px solid transparent;
  transition: all .15s;
}
.qf-tab-btn:hover { color: var(--text); background: var(--surface2); }
.qf-tab-btn.active { color: var(--accent); border-bottom-color: var(--accent); }
.qf-tab-panel { display: none; padding: 16px; }
.qf-tab-panel.active { display: block; }

.qf-score-row { display: flex; align-items: baseline; gap: 12px; margin-bottom: 14px; }
.qf-big-score { font-size: 44px; font-weight: 800; line-height: 1; }
.qf-rating { font-size: 14px; font-weight: 700; }

.dim-grid { display: flex; flex-direction: column; gap: 8px; margin-bottom: 10px; }
.dim-item { display: grid; grid-template-columns: 160px 1fr auto; gap: 8px; align-items: center; }
.dim-label { font-size: 11px; color: var(--text2); text-transform: capitalize; }
.dim-bar-wrap { display: flex; align-items: center; gap: 8px; }
.dim-bar { flex: 1; height: 6px; background: var(--surface3); border-radius: 3px; overflow: hidden; }
.dim-fill { height: 100%; border-radius: 3px; transition: width .8s; }
.dim-score { font-size: 11px; font-weight: 700; min-width: 28px; text-align: right; }
.dim-notes { font-size: 10px; color: var(--text3); grid-column: 1 / -1; padding-left: 168px; }

.conf-dim-grid { display: flex; flex-direction: column; gap: 6px; }
.conf-dim { display: grid; grid-template-columns: 200px 1fr 36px; gap: 8px; align-items: center; }
.conf-dim-label { font-size: 11px; color: var(--text2); text-transform: capitalize; }
.conf-dim-bar { height: 5px; background: var(--surface3); border-radius: 3px; overflow: hidden; }
.conf-dim-fill { height: 100%; border-radius: 3px; transition: width .8s; }
.conf-dim-val { font-size: 11px; font-weight: 700; text-align: right; }
*/
