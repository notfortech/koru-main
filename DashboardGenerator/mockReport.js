/**
 * mockReport.js — StudioTech BI Blueprint Generator
 * Universal Mock Report Generator
 *
 * Reads any generated blueprint (bp) object and:
 *   1. Generates realistic mock data matching bp.data_model fact/dimension tables
 *   2. Computes all bp.kpis and bp.measures against the mock data
 *   3. Renders bp.pages as fully interactive Chart.js dashboard pages
 *   4. Opens the result in a new browser tab — no server required
 *
 * Loaded AFTER app.js and knowledgepacks. Called by index.html via:
 *   launchMockReport(currentBP)
 */

'use strict';

/* ═══════════════════════════════════════════════════════════════
   SECTION 1: MOCK DATA ENGINE
   Generates typed, relational mock data for any star schema.
   ─────────────────────────────────────────────────────────────
   Strategy: read bp.data_model → build dimension pools → build
   fact rows referencing those pools → aggregate into KPI values.
═══════════════════════════════════════════════════════════════ */

const MockEngine = (() => {
  // Seeded pseudo-random for reproducibility
  let _seed = 42;
  function rnd() {
    _seed = (_seed * 1664525 + 1013904223) & 0xffffffff;
    return ((_seed >>> 0) / 0xffffffff);
  }
  function rndInt(lo, hi) { return Math.floor(rnd() * (hi - lo + 1)) + lo; }
  function rndFloat(lo, hi, dp = 2) { return parseFloat((rnd() * (hi - lo) + lo).toFixed(dp)); }
  function pick(arr) { return arr[rndInt(0, arr.length - 1)]; }
  function pickW(items, weights) {
    const total = weights.reduce((a, b) => a + b, 0);
    let r = rnd() * total;
    for (let i = 0; i < items.length; i++) { r -= weights[i]; if (r <= 0) return items[i]; }
    return items[items.length - 1];
  }
  function shuffle(arr) {
    const a = [...arr];
    for (let i = a.length - 1; i > 0; i--) {
      const j = rndInt(0, i);
      [a[i], a[j]] = [a[j], a[i]];
    }
    return a;
  }

  // Name pools
  const FIRST  = ['James','Sarah','Michael','Emma','David','Jessica','Andrew','Sophie','Matthew','Olivia','Daniel','Charlotte','Ryan','Mia','Thomas','Grace','Joshua','Chloe','Benjamin','Hannah','Nathan','Zoe','Samuel','Lauren','Alexander','Rebecca','William','Emily','Jack','Natalie','Henry','Victoria','Raj','Priya','Wei','Lin','Ahmed','Fatima','Connor','Tina','Marcus','Diana'];
  const LAST   = ['Smith','Johnson','Williams','Brown','Jones','Davis','Miller','Wilson','Moore','Taylor','Anderson','Thomas','Jackson','White','Harris','Martin','Thompson','Clark','Rodriguez','Lewis','Lee','Walker','Hall','Allen','Young','King','Wright','Scott','Green','Adams','Baker','Nelson','Carter','Mitchell','Perez','Turner','Evans','Edwards','Collins','Stewart','Patel','Singh','Chen','Zhang'];
  const ORG_A  = ['Apex','Summit','Harbour','Pacific','Horizon','Atlas','Pioneer','Meridian','Pinnacle','Alpine','Coastal','Aurora','Bridgeway','Catalyst','Dexterity','Elevate','Foundation','Gateway','Highland','Integral','Landmark','Momentum','Nexus','Quantum','Regent','Stellar','Titan','Unified','Vantage','Zenith'];
  const ORG_B  = ['Group','Holdings','Partners','Advisory','Solutions','Capital','Ventures','Services','Enterprises','Consulting','Management','Global','Industries','Pty Ltd'];
  const STATES = ['VIC','NSW','QLD','SA','WA','ACT'];
  const MONTHS = ['Jul','Aug','Sep','Oct','Nov','Dec','Jan','Feb','Mar','Apr','May','Jun'];

  function name()    { return pick(FIRST) + ' ' + pick(LAST); }
  function orgName() { return pick(ORG_A) + ' ' + pick(ORG_B); }
  function state()   { return pickW(STATES, [35,30,15,7,7,6]); }

  // Date helpers
  function dateKey(d) { return d.getFullYear() * 10000 + (d.getMonth() + 1) * 100 + d.getDate(); }
  function addDays(d, n) { const r = new Date(d); r.setDate(r.getDate() + n); return r; }
  function monthLabel(d) { return MONTHS[d.getMonth()] + '-' + String(d.getFullYear()).slice(2); }
  function fyLabel(d)    { return d.getMonth() >= 6 ? 'FY' + (d.getFullYear() + 1) : 'FY' + d.getFullYear(); }

  // ── Date dimension ─────────────────────────────────────────────
  function buildDateDim(startDate, endDate) {
    const rows = [];
    let d = new Date(startDate);
    while (d <= endDate) {
      const isWE = d.getDay() === 0 || d.getDay() === 6;
      const m    = d.getMonth();
      const fy   = m >= 6 ? d.getFullYear() + 1 : d.getFullYear();
      const fyM  = ((m - 6 + 12) % 12) + 1;
      const fyQ  = 'Q' + Math.ceil(fyM / 3);
      rows.push({
        DateKey: dateKey(d), Date: new Date(d),
        Month: d.getMonth() + 1, MonthName: d.toLocaleString('en', { month: 'long' }),
        MonthLabel: monthLabel(d), Quarter: 'Q' + Math.ceil((m + 1) / 3),
        CalendarYear: d.getFullYear(), FiscalYear: 'FY' + fy,
        FiscalQuarter: fyQ, FiscalMonth: fyM,
        IsWeekend: isWE ? 1 : 0,
      });
      d = addDays(d, 1);
    }
    return rows;
  }

  // ── Generic dimension builder from pack schema ─────────────────
  function buildGenericDim(dimDef, count, extraFields) {
    const rows = [];
    for (let i = 1; i <= count; i++) {
      const row = { [`${dimDef.name.replace('Dim_', '')}Key`]: i };
      // Generate values for each key_column
      (dimDef.key_columns || []).forEach(col => {
        const cl = col.toLowerCase();
        if (cl.includes('key'))     row[col] = i;
        else if (cl.includes('name') || cl.includes('fullname')) row[col] = col.toLowerCase().includes('emp') || cl.includes('full') ? name() : orgName();
        else if (cl.includes('state') || cl.includes('office'))  row[col] = state();
        else if (cl.includes('rate') || cl.includes('rate$'))    row[col] = rndFloat(100, 600, 0);
        else if (cl.includes('isactive') || cl.includes('is_active')) row[col] = rnd() < 0.9 ? 1 : 0;
        else if (cl.includes('ispartner'))  row[col] = rnd() < 0.08 ? 1 : 0;
        else if (cl.includes('grade'))      row[col] = pick(['Partner','Director','Senior Manager','Manager','Senior Consultant','Consultant','Graduate']);
        else if (cl.includes('code'))       row[col] = dimDef.name.replace('Dim_','').slice(0,3).toUpperCase() + String(i).padStart(4,'0');
        else if (cl.includes('status'))     row[col] = pick(['Active','Active','Active','Inactive','On Hold']);
        else if (cl.includes('type'))       row[col] = pick(['Type A','Type B','Type C']);
        else if (cl.includes('industry'))   row[col] = pick(['Financial Services','Healthcare','Technology','Property','Education','Government','Retail']);
        else if (cl.includes('segment'))    row[col] = pick(['Enterprise','Mid-Market','SME']);
        else if (cl.includes('date') || cl.includes('since') || cl.includes('start')) row[col] = addDays(new Date('2020-01-01'), rndInt(0, 1800));
        else row[col] = 'Value-' + i;
      });
      if (extraFields) Object.assign(row, extraFields(i));
      rows.push(row);
    }
    return rows;
  }

  // ── Fact row generators — one per recognised fact table pattern ─
  const FACT_PATTERNS = {
    // Professional Services
    Timesheet:       buildTimesheetFact,
    Engagement:      buildEngagementFact,
    Invoice:         buildInvoiceFact,
    Utilisation:     buildUtilisationFact,
    ResourceUtil:    buildUtilisationFact,
    Pipeline:        buildPipelineFact,
    // NDIS
    ServiceDelivery: buildServiceDeliveryFact,
    Claim:           buildClaimFact,
    PlanUtil:        buildPlanUtilisationFact,
    Incident:        buildIncidentFact,
    // Government
    Budget:          buildBudgetFact,
    Expenditure:     buildExpenditureFact,
    Grant:           buildGrantFact,
    Program:         buildProgramFact,
    // Property Management
    RentReceipt:     buildRentReceiptFact,
    Vacancy:         buildVacancyFact,
    Maintenance:     buildMaintenanceFact,
    ManagementFee:   buildManagementFeeFact,
    // Retail / General
    Sales:           buildSalesFact,
    Inventory:       buildInventoryFact,
    // Generic fallback
    Generic:         buildGenericFact,
  };

  function classifyFact(name) {
    const n = name.toLowerCase();
    if (n.includes('timesheet'))      return 'Timesheet';
    if (n.includes('engagement'))     return 'Engagement';
    if (n.includes('invoice') || n.includes('billing')) return 'Invoice';
    if (n.includes('resourceutil') || n.includes('resource_util')) return 'ResourceUtil';
    if (n.includes('utilisation') || n.includes('utilization')) return 'Utilisation';
    if (n.includes('pipeline') || n.includes('opportunity')) return 'Pipeline';
    if (n.includes('servicedelivery') || n.includes('service_delivery')) return 'ServiceDelivery';
    if (n.includes('claim'))          return 'Claim';
    if (n.includes('planutilisation') || n.includes('plan_util')) return 'PlanUtil';
    if (n.includes('incident'))       return 'Incident';
    if (n.includes('budget'))         return 'Budget';
    if (n.includes('expenditure') || n.includes('expense')) return 'Expenditure';
    if (n.includes('grant'))          return 'Grant';
    if (n.includes('program') || n.includes('programme')) return 'Program';
    if (n.includes('rent') || n.includes('receipt')) return 'RentReceipt';
    if (n.includes('vacanc'))         return 'Vacancy';
    if (n.includes('maintenance'))    return 'Maintenance';
    if (n.includes('managementfee') || n.includes('management_fee')) return 'ManagementFee';
    if (n.includes('sales') || n.includes('transaction') || n.includes('order')) return 'Sales';
    if (n.includes('inventory') || n.includes('stock')) return 'Inventory';
    return 'Generic';
  }

  // ── Individual fact builders ────────────────────────────────────

  function buildTimesheetFact(dims, dates, count) {
    const rows = []; const biz = dates.filter(d => !d.IsWeekend);
    const empCount = (dims.Dim_Employee || dims.Dim_Staff || dims.Dim_Consultant || Object.values(dims)[0]).length;
    const slCount  = (dims.Dim_ServiceLine || dims.Dim_Practice || { length: 4 }).length;
    const cliCount = (dims.Dim_Client || dims.Dim_Customer || { length: 30 }).length;
    const engCount = (dims.Dim_Engagement || { length: 60 }).length;
    const gradeTargets = [.55,.60,.68,.72,.78,.80,.82];
    for (let i = 0; i < count; i++) {
      const empK  = rndInt(1, empCount);
      const slK   = rndInt(1, slCount);
      const cliK  = rndInt(1, cliCount);
      const engK  = rndInt(1, engCount);
      const d     = pick(biz);
      const hrs   = rndFloat(6.0, 9.5, 1);
      const bHrs  = parseFloat((hrs * rndFloat(.60, .88, 2)).toFixed(1));
      const rate  = rndFloat(110, 540, 0);
      rows.push({ TimesheetKey: i+1, EmployeeKey: empK, EngagementKey: engK,
        ClientKey: cliK, ServiceLineKey: slK, DateKey: d.DateKey,
        MonthLabel: d.MonthLabel, FiscalYear: d.FiscalYear, FiscalQuarter: d.FiscalQuarter,
        HoursWorked: hrs, BillableHours: bHrs, NonBillableHours: parseFloat((hrs-bHrs).toFixed(1)),
        StandardRate: rate, WIPValue: parseFloat((bHrs*rate).toFixed(2)),
        IsApproved: rnd() < .92 ? 1 : 0 });
    }
    return rows;
  }

  function buildEngagementFact(dims, dates, count) {
    const rows = []; const monthly = dates.filter(d => d.Date.getDate() === 28);
    const cliCount = (dims.Dim_Client || dims.Dim_Customer || { length: 40 }).length;
    const slCount  = (dims.Dim_ServiceLine || { length: 4 }).length;
    for (let i = 0; i < count; i++) {
      const d = pick(monthly);
      const budget = rndFloat(15000, 350000, 0);
      const actual = parseFloat((budget * rndFloat(.55, 1.1, 2)).toFixed(0));
      rows.push({ EngagementSnapshotKey: i+1, ClientKey: rndInt(1, cliCount),
        ServiceLineKey: rndInt(1, slCount), DateKey: d.DateKey,
        MonthLabel: d.MonthLabel, FiscalYear: d.FiscalYear, FiscalQuarter: d.FiscalQuarter,
        BudgetFees: budget, ActualFees: actual,
        WIPBalance: parseFloat((actual * rndFloat(.05, .35, 2)).toFixed(2)),
        WriteOffAmount: parseFloat((actual * rndFloat(.00, .10, 3)).toFixed(2)),
        EngagementStatus: pick(['Active','Active','Active','Completed','On Hold']) });
    }
    return rows;
  }

  function buildInvoiceFact(dims, dates, count) {
    const rows = []; const biz = dates.filter(d => !d.IsWeekend);
    const cliCount = (dims.Dim_Client || dims.Dim_Customer || { length: 40 }).length;
    const statuses = ['Paid','Paid','Paid','Issued','Overdue','Written Off'];
    for (let i = 0; i < count; i++) {
      const d = pick(biz);
      const amt = rndFloat(3500, 90000, 2);
      const st  = pick(statuses);
      const paid = st === 'Paid' ? amt : st === 'Issued' ? parseFloat((amt*rndFloat(.0,.5,2)).toFixed(2)) : 0;
      rows.push({ InvoiceKey: i+1, ClientKey: rndInt(1, cliCount), EngagementKey: rndInt(1, 60),
        InvoiceDateKey: d.DateKey, MonthLabel: d.MonthLabel, FiscalYear: d.FiscalYear, FiscalQuarter: d.FiscalQuarter,
        InvoiceAmount: amt, GSTAmount: parseFloat((amt*.1).toFixed(2)),
        AmountPaid: paid, AmountOutstanding: parseFloat((amt-paid).toFixed(2)),
        DaysOutstanding: rndInt(0, 120), InvoiceStatus: st });
    }
    return rows;
  }

  function buildUtilisationFact(dims, dates, count) {
    const rows = []; const fridays = dates.filter(d => d.Date.getDay() === 5);
    const empCount = (dims.Dim_Employee || dims.Dim_Staff || Object.values(dims)[0]).length;
    const slCount  = (dims.Dim_ServiceLine || { length: 4 }).length;
    for (let i = 0; i < count; i++) {
      const d = pick(fridays);
      const avail = 37.5; const leave = pick([0,0,0,0,7.5,15]);
      const eff   = avail - leave;
      const rate  = rndFloat(.60, .88, 4);
      const bill  = parseFloat((eff * rate).toFixed(1));
      rows.push({ UtilisationKey: i+1, EmployeeKey: rndInt(1, empCount),
        ServiceLineKey: rndInt(1, slCount), DateKey: d.DateKey,
        MonthLabel: d.MonthLabel, FiscalYear: d.FiscalYear, FiscalQuarter: d.FiscalQuarter,
        AvailableHours: avail, BillableHours: bill,
        NonBillableHours: parseFloat((eff*rndFloat(.05,.18,2)).toFixed(1)),
        LeaveHours: leave, UtilisationRate: rate });
    }
    return rows;
  }

  function buildPipelineFact(dims, dates, count) {
    const rows = [];
    const stages = ['Lead','Qualified','Proposal Sent','Negotiation','Won','Lost'];
    const probs  = [.10, .25, .50, .75, 1.0, .0];
    const cliCount = (dims.Dim_Client || dims.Dim_Customer || { length: 40 }).length;
    const slCount  = (dims.Dim_ServiceLine || { length: 4 }).length;
    const d = dates[dates.length - 1];
    for (let i = 0; i < count; i++) {
      const si  = pickW(stages, [20,22,25,18,10,5]);
      const idx = stages.indexOf(si);
      const est = rndFloat(20000, 800000, 0);
      const wp  = Math.min(1, Math.max(0, probs[idx] + rndFloat(-.05,.05,2)));
      rows.push({ PipelineKey: i+1, ClientKey: rndInt(1, cliCount),
        ServiceLineKey: rndInt(1, slCount), OwnerKey: rndInt(1, 10),
        DateKey: d.DateKey, Stage: si,
        EstimatedValue: est, WeightedValue: parseFloat((est*wp).toFixed(0)),
        WinProbability: parseFloat(wp.toFixed(2)) });
    }
    return rows;
  }

  // ── NDIS ────────────────────────────────────────────────────────
  function buildServiceDeliveryFact(dims, dates, count) {
    const rows = []; const biz = dates.filter(d => !d.IsWeekend);
    const pCount  = (dims.Dim_Participant || { length: 200 }).length;
    const wkCount = (dims.Dim_Worker || dims.Dim_Employee || { length: 120 }).length;
    const siCount = (dims.Dim_SupportItem || { length: 30 }).length;
    const catCount= (dims.Dim_SupportCategory || { length: 8 }).length;
    for (let i = 0; i < count; i++) {
      const d = pick(biz);
      const hrs = rndFloat(.5, 8, 1);
      const rate = rndFloat(55, 115, 2);
      rows.push({ ServiceDeliveryKey: i+1, ParticipantKey: rndInt(1, pCount),
        WorkerKey: rndInt(1, wkCount), SupportItemKey: rndInt(1, siCount),
        SupportCategoryKey: rndInt(1, catCount), DateKey: d.DateKey,
        MonthLabel: d.MonthLabel, FiscalYear: d.FiscalYear, FiscalQuarter: d.FiscalQuarter,
        HoursDelivered: hrs, HoursRostered: parseFloat((hrs + rndFloat(0,.5,1)).toFixed(1)),
        NDISUnitRate: rate, BillableAmount: parseFloat((hrs*rate).toFixed(2)),
        IsClaimed: rnd() < .88 ? 1 : 0, IsCancelled: rnd() < .05 ? 1 : 0 });
    }
    return rows;
  }

  function buildClaimFact(dims, dates, count) {
    const rows = []; const biz = dates.filter(d => !d.IsWeekend);
    const pCount  = (dims.Dim_Participant || { length: 200 }).length;
    const statuses = ['Approved','Approved','Approved','Pending','Rejected','Resubmitted'];
    for (let i = 0; i < count; i++) {
      const d = pick(biz);
      const amt = rndFloat(100, 2500, 2);
      const st  = pick(statuses);
      rows.push({ ClaimKey: i+1, ParticipantKey: rndInt(1, pCount),
        SupportItemKey: rndInt(1, 30), DateKey: d.DateKey,
        MonthLabel: d.MonthLabel, FiscalYear: d.FiscalYear, FiscalQuarter: d.FiscalQuarter,
        ClaimAmount: amt, ApprovedAmount: st === 'Approved' ? amt : st === 'Rejected' ? 0 : parseFloat((amt*rndFloat(.5,.9,2)).toFixed(2)),
        ClaimStatus: st, DaysToApproval: st === 'Approved' ? rndInt(1, 14) : null });
    }
    return rows;
  }

  function buildPlanUtilisationFact(dims, dates, count) {
    const rows = []; const monthly = dates.filter(d => d.Date.getDate() === 28);
    const pCount = (dims.Dim_Participant || { length: 200 }).length;
    const siCount= (dims.Dim_SupportItem || { length: 30 }).length;
    for (let i = 0; i < count; i++) {
      const d = pick(monthly);
      const budget = rndFloat(15000, 120000, 0);
      const spent  = parseFloat((budget * rndFloat(.55, .95, 2)).toFixed(0));
      rows.push({ PlanUtilisationKey: i+1, ParticipantKey: rndInt(1, pCount),
        SupportCategoryKey: rndInt(1, 8), DateKey: d.DateKey,
        MonthLabel: d.MonthLabel, FiscalYear: d.FiscalYear, FiscalQuarter: d.FiscalQuarter,
        FundingAmount: budget, SpentAmount: spent, RemainingAmount: budget - spent,
        UtilisationRate: parseFloat((spent/budget).toFixed(4)) });
    }
    return rows;
  }

  function buildIncidentFact(dims, dates, count) {
    const rows = []; const biz = dates.filter(d => !d.IsWeekend);
    const pCount = (dims.Dim_Participant || { length: 200 }).length;
    const types  = ['Reportable Incident','Near Miss','Property Damage','Complaint','Critical Incident'];
    for (let i = 0; i < count; i++) {
      const d = pick(biz);
      rows.push({ IncidentKey: i+1, ParticipantKey: rndInt(1, pCount),
        WorkerKey: rndInt(1, 120), DateKey: d.DateKey,
        MonthLabel: d.MonthLabel, FiscalYear: d.FiscalYear, FiscalQuarter: d.FiscalQuarter,
        IncidentType: pick(types),
        IsNotifiable: rnd() < .35 ? 1 : 0,
        IsResolved: rnd() < .78 ? 1 : 0,
        DaysToResolve: rndInt(1, 60) });
    }
    return rows;
  }

  // ── Government ──────────────────────────────────────────────────
  function buildBudgetFact(dims, dates, count) {
    const rows = []; const monthly = dates.filter(d => d.Date.getDate() === 28);
    const pgmCount = (dims.Dim_Program || dims.Dim_Department || { length: 20 }).length;
    for (let i = 0; i < count; i++) {
      const d = pick(monthly);
      const budget = rndFloat(500000, 25000000, 0);
      const actual = parseFloat((budget * rndFloat(.75, 1.05, 2)).toFixed(0));
      rows.push({ BudgetKey: i+1, ProgramKey: rndInt(1, pgmCount),
        DepartmentKey: rndInt(1, 10), DateKey: d.DateKey,
        MonthLabel: d.MonthLabel, FiscalYear: d.FiscalYear, FiscalQuarter: d.FiscalQuarter,
        BudgetAmount: budget, ActualAmount: actual,
        Variance: actual - budget, VariancePct: parseFloat(((actual-budget)/budget).toFixed(4)),
        IsUnderspend: actual < budget ? 1 : 0 });
    }
    return rows;
  }

  function buildExpenditureFact(dims, dates, count) {
    const rows = []; const biz = dates.filter(d => !d.IsWeekend && d.Date.getDate() <= 20);
    const pgmCount = (dims.Dim_Program || dims.Dim_Department || { length: 20 }).length;
    const types = ['Salaries','Contractors','Suppliers','Grants','Travel','Technology','Property'];
    for (let i = 0; i < count; i++) {
      const d = pick(biz);
      rows.push({ ExpenditureKey: i+1, ProgramKey: rndInt(1, pgmCount),
        DepartmentKey: rndInt(1, 10), DateKey: d.DateKey,
        MonthLabel: d.MonthLabel, FiscalYear: d.FiscalYear, FiscalQuarter: d.FiscalQuarter,
        ExpenditureType: pick(types), Amount: rndFloat(1000, 500000, 0),
        IsCapital: rnd() < .15 ? 1 : 0 });
    }
    return rows;
  }

  function buildGrantFact(dims, dates, count) {
    const rows = []; const biz = dates.filter(d => !d.IsWeekend);
    const statuses = ['Active','Active','Active','Completed','Acquitted','At Risk'];
    for (let i = 0; i < count; i++) {
      const d = pick(biz);
      const approved = rndFloat(50000, 5000000, 0);
      const paid     = parseFloat((approved * rndFloat(.5, 1, 2)).toFixed(0));
      rows.push({ GrantKey: i+1, RecipientKey: rndInt(1, 50),
        ProgramKey: rndInt(1, 10), DateKey: d.DateKey,
        MonthLabel: d.MonthLabel, FiscalYear: d.FiscalYear, FiscalQuarter: d.FiscalQuarter,
        ApprovedAmount: approved, PaidAmount: paid, OutstandingAmount: approved - paid,
        GrantStatus: pick(statuses), IsAcquitted: rnd() < .55 ? 1 : 0 });
    }
    return rows;
  }

  function buildProgramFact(dims, dates, count) {
    const rows = []; const monthly = dates.filter(d => d.Date.getDate() === 28);
    const pgmCount = (dims.Dim_Program || { length: 20 }).length;
    for (let i = 0; i < count; i++) {
      const d = pick(monthly);
      const target = rndFloat(50, 100, 0);
      const actual = parseFloat((target * rndFloat(.70, 1.10, 2)).toFixed(1));
      rows.push({ ProgramKey: i+1, ProgramDimKey: rndInt(1, pgmCount),
        DateKey: d.DateKey, MonthLabel: d.MonthLabel,
        FiscalYear: d.FiscalYear, FiscalQuarter: d.FiscalQuarter,
        KPITarget: target, KPIActual: actual,
        AchievementRate: parseFloat((actual/target).toFixed(4)),
        ClientsServed: rndInt(100, 10000), IsOnTrack: actual >= target * .9 ? 1 : 0 });
    }
    return rows;
  }

  // ── Property Management ─────────────────────────────────────────
  function buildRentReceiptFact(dims, dates, count) {
    const rows = []; const biz = dates.filter(d => !d.IsWeekend);
    const propCount = (dims.Dim_Property || { length: 150 }).length;
    const tenCount  = (dims.Dim_Tenancy  || { length: 150 }).length;
    const statuses  = ['On Time','On Time','On Time','7 Days Late','14 Days Late','30+ Days Late'];
    for (let i = 0; i < count; i++) {
      const d = pick(biz);
      const due  = rndFloat(1200, 4500, 0);
      const st   = pick(statuses);
      const paid = st === 'On Time' ? due : st === '7 Days Late' ? due : parseFloat((due*rndFloat(.0,.8,2)).toFixed(2));
      rows.push({ ReceiptKey: i+1, PropertyKey: rndInt(1, propCount),
        TenancyKey: rndInt(1, tenCount), DateKey: d.DateKey,
        MonthLabel: d.MonthLabel, FiscalYear: d.FiscalYear, FiscalQuarter: d.FiscalQuarter,
        AmountDue: due, AmountPaid: paid, AmountArrears: parseFloat((due-paid).toFixed(2)),
        DaysOverdue: st === 'On Time' ? 0 : st === '7 Days Late' ? rndInt(1,7) : st === '14 Days Late' ? rndInt(8,14) : rndInt(15,60),
        PaymentStatus: st });
    }
    return rows;
  }

  function buildVacancyFact(dims, dates, count) {
    const rows = []; const propCount = (dims.Dim_Property || { length: 150 }).length;
    for (let i = 0; i < count; i++) {
      const d = pick(dates);
      const vacant = rnd() < .06 ? 1 : 0;
      rows.push({ VacancyKey: i+1, PropertyKey: rndInt(1, propCount),
        DateKey: d.DateKey, MonthLabel: d.MonthLabel,
        FiscalYear: d.FiscalYear, FiscalQuarter: d.FiscalQuarter,
        IsVacant: vacant, DaysVacant: vacant ? rndInt(1, 90) : 0,
        WeeklyRentLoss: vacant ? rndFloat(250, 900, 0) : 0 });
    }
    return rows;
  }

  function buildMaintenanceFact(dims, dates, count) {
    const rows = []; const biz = dates.filter(d => !d.IsWeekend);
    const propCount = (dims.Dim_Property || { length: 150 }).length;
    const cats = ['Plumbing','Electrical','General','Landscaping','HVAC','Structural','Pest Control'];
    const pris = ['Emergency','Urgent','Routine','Routine','Routine'];
    for (let i = 0; i < count; i++) {
      const d = pick(biz);
      const open = rnd() < .30 ? 1 : 0;
      rows.push({ MaintenanceKey: i+1, PropertyKey: rndInt(1, propCount),
        DateKey: d.DateKey, MonthLabel: d.MonthLabel,
        FiscalYear: d.FiscalYear, FiscalQuarter: d.FiscalQuarter,
        Category: pick(cats), Priority: pick(pris),
        Cost: rndFloat(80, 8000, 0), DaysToComplete: open ? null : rndInt(1, 45),
        IsOpen: open, IsEmergency: rnd() < .08 ? 1 : 0 });
    }
    return rows;
  }

  function buildManagementFeeFact(dims, dates, count) {
    const rows = []; const monthly = dates.filter(d => d.Date.getDate() === 28);
    const propCount = (dims.Dim_Property || { length: 150 }).length;
    const feeTypes  = ['Management','Letting','Lease Renewal','Maintenance Supervision'];
    for (let i = 0; i < count; i++) {
      const d = pick(monthly);
      const rent = rndFloat(1200, 4500, 0);
      const feeR = rndFloat(.065, .085, 4);
      rows.push({ FeeKey: i+1, PropertyKey: rndInt(1, propCount),
        DateKey: d.DateKey, MonthLabel: d.MonthLabel,
        FiscalYear: d.FiscalYear, FiscalQuarter: d.FiscalQuarter,
        FeeType: pick(feeTypes), RentCollected: rent,
        FeeAmount: parseFloat((rent * feeR).toFixed(2)), FeeRate: feeR });
    }
    return rows;
  }

  // ── Retail / General ────────────────────────────────────────────
  function buildSalesFact(dims, dates, count) {
    const rows = []; const biz = dates.filter(d => !d.IsWeekend);
    const cliCount = (dims.Dim_Customer || dims.Dim_Client || { length: 500 }).length;
    const catCount = (dims.Dim_Product  || dims.Dim_Category || { length: 50 }).length;
    for (let i = 0; i < count; i++) {
      const d = pick(biz);
      const qty = rndInt(1, 20); const price = rndFloat(10, 500, 2);
      const disc = rndFloat(0, .2, 2);
      rows.push({ SalesKey: i+1, CustomerKey: rndInt(1, cliCount),
        ProductKey: rndInt(1, catCount), DateKey: d.DateKey,
        MonthLabel: d.MonthLabel, FiscalYear: d.FiscalYear, FiscalQuarter: d.FiscalQuarter,
        Quantity: qty, UnitPrice: price, Discount: disc,
        SalesAmount: parseFloat((qty * price * (1-disc)).toFixed(2)),
        GrossMargin: parseFloat((qty * price * rndFloat(.25,.55,2)).toFixed(2)),
        Channel: pick(['Online','In-Store','Phone','Partner']) });
    }
    return rows;
  }

  function buildInventoryFact(dims, dates, count) {
    const rows = []; const monthly = dates.filter(d => d.Date.getDate() === 28);
    const prodCount = (dims.Dim_Product || { length: 50 }).length;
    for (let i = 0; i < count; i++) {
      const d = pick(monthly);
      const onHand = rndInt(0, 500);
      rows.push({ InventoryKey: i+1, ProductKey: rndInt(1, prodCount),
        DateKey: d.DateKey, MonthLabel: d.MonthLabel,
        FiscalYear: d.FiscalYear, FiscalQuarter: d.FiscalQuarter,
        OnHandQty: onHand, ReorderPoint: rndInt(50, 150),
        UnitCost: rndFloat(5, 200, 2), TotalValue: parseFloat((onHand * rndFloat(5,200,2)).toFixed(2)),
        IsLowStock: onHand < 50 ? 1 : 0 });
    }
    return rows;
  }

  function buildGenericFact(dims, dates, count) {
    const rows = []; const allDates = dates.filter(d => !d.IsWeekend);
    const dimKeys = Object.keys(dims);
    for (let i = 0; i < count; i++) {
      const d = pick(allDates);
      const row = { FactKey: i+1, DateKey: d.DateKey, MonthLabel: d.MonthLabel,
        FiscalYear: d.FiscalYear, FiscalQuarter: d.FiscalQuarter,
        Value1: rndFloat(100, 100000, 2), Value2: rndFloat(50, 50000, 2),
        Quantity: rndInt(1, 1000), Rate: rndFloat(0.5, 0.95, 4) };
      dimKeys.forEach(dk => { const key = dk.replace('Dim_','') + 'Key'; row[key] = rndInt(1, dims[dk].length); });
      rows.push(row);
    }
    return rows;
  }

  // ── MAIN: Generate all tables for a blueprint ──────────────────
  function generate(bp) {
    _seed = 42; // Reset seed for reproducibility

    const dm       = bp.data_model || {};
    const facts    = dm.fact_tables || [];
    const dimDefs  = dm.dimension_tables || [];
    const currency = bp.meta?.currency || 'AUD';
    const fyStart  = bp.meta?.fiscal_year_start || 'July';

    // Date spine — 2 fiscal years
    const startDate = new Date(fyStart === 'July' ? '2023-07-01' : '2024-01-01');
    const endDate   = new Date(fyStart === 'July' ? '2025-06-30' : '2025-12-31');
    const dates     = buildDateDim(startDate, endDate);

    // Build dimensions — size by industry
    const dimSizes = {
      Dim_Employee: 120, Dim_Staff: 120, Dim_Consultant: 120, Dim_Worker: 80,
      Dim_Participant: 250, Dim_Client: 80, Dim_Customer: 200,
      Dim_Property: 150, Dim_Tenancy: 140, Dim_Owner: 80,
      Dim_ServiceLine: 4, Dim_Practice: 6, Dim_SupportCategory: 8,
      Dim_SupportItem: 30, Dim_Engagement: 200, Dim_Program: 20,
      Dim_Department: 10, Dim_Grant: 60, Dim_Product: 50,
    };
    const dims = {};
    dimDefs.forEach(d => {
      const sz = dimSizes[d.name] || rndInt(10, 50);
      dims[d.name] = buildGenericDim(d, sz);
    });
    dims.Dim_Date = dates;

    // Build facts
    const factData = {};
    const factSizes = {
      Timesheet: 8000, ServiceDelivery: 10000, Claim: 2000, PlanUtil: 1500,
      Invoice: 500, Engagement: 800, Utilisation: 5000, ResourceUtil: 5000,
      Pipeline: 80, Budget: 400, Expenditure: 600, Grant: 200, Program: 400,
      RentReceipt: 2000, Vacancy: 1000, Maintenance: 800, ManagementFee: 600,
      Sales: 5000, Inventory: 400, Incident: 300, Generic: 1000,
    };
    facts.forEach(f => {
      const type  = classifyFact(f.name);
      const count = factSizes[type] || 1000;
      const fn    = FACT_PATTERNS[type] || FACT_PATTERNS.Generic;
      factData[f.name] = fn(dims, dates, count);
    });

    return { dims, factData, dates, currency, fyStart, bp };
  }

  return { generate, rnd, rndInt, rndFloat, pick };
})();


/* ═══════════════════════════════════════════════════════════════
   SECTION 2: METRICS AGGREGATOR
   Reads mock data and computes KPI values by slicing dimensions.
═══════════════════════════════════════════════════════════════ */

const Metrics = (() => {

  function sum(rows, col) {
    return rows.reduce((s, r) => s + (Number(r[col]) || 0), 0);
  }
  function avg(rows, col) {
    const v = rows.filter(r => r[col] != null && r[col] !== '');
    return v.length ? sum(v, col) / v.length : 0;
  }
  function count(rows) { return rows.length; }
  function countDistinct(rows, col) { return new Set(rows.map(r => r[col])).size; }

  function groupBy(rows, key) {
    const g = {};
    rows.forEach(r => {
      const k = r[key] ?? 'Unknown';
      if (!g[k]) g[k] = [];
      g[k].push(r);
    });
    return g;
  }

  function joinDim(factRows, dimRows, factKey, dimKey) {
    if (!dimRows || !dimRows.length) return factRows;
    const lookup = {};
    dimRows.forEach(d => { lookup[d[dimKey]] = d; });
    return factRows.map(f => ({ ...f, ...lookup[f[factKey]] }));
  }

  // ── Compute KPI values from fact data ──────────────────────────
  function computeKPIs(bp, mockData) {
    const { dims, factData } = mockData;
    const facts = bp.data_model?.fact_tables || [];
    const kpis  = bp.kpis || [];
    const industry = (bp.meta?.industry || '').toLowerCase();

    // Pick the primary fact (most rows)
    let primaryFact = null, primaryRows = [];
    facts.forEach(f => {
      const rows = factData[f.name] || [];
      if (rows.length > primaryRows.length) { primaryFact = f; primaryRows = rows; }
    });

    // Get all fact rows merged
    const allRows = Object.values(factData).flat();

    // Compute KPI values heuristically from the data available
    const computed = {};
    kpis.forEach(kpi => {
      const measure = (kpi.measure_ref || kpi.name || '').toLowerCase();

      // Revenue / fees
      if (/revenue|fee|income|billing|invoice/.test(measure)) {
        const rows = factData[Object.keys(factData).find(k => /invoice|billing|fee|payment|receipt/.test(k.toLowerCase()))] || primaryRows;
        computed[kpi.name] = sum(rows, 'InvoiceAmount') || sum(rows, 'FeeAmount') || sum(rows, 'BillableAmount') || sum(rows, 'Value1');
      }
      // WIP
      else if (/wip|work.in.progress/.test(measure)) {
        const rows = factData[Object.keys(factData).find(k => /timesheet|engagement/.test(k.toLowerCase()))] || primaryRows;
        computed[kpi.name] = sum(rows, 'WIPValue') || sum(rows, 'WIPBalance') || sum(rows, 'Value1');
      }
      // Utilisation
      else if (/utilisation|utilization/.test(measure)) {
        const rows = factData[Object.keys(factData).find(k => /utilisation|utilization|timesheet/.test(k.toLowerCase()))] || primaryRows;
        computed[kpi.name] = avg(rows, 'UtilisationRate') * 100 || avg(rows, 'Rate') * 100 || 72.4;
      }
      // Occupancy
      else if (/occupancy/.test(measure)) {
        const rows = factData[Object.keys(factData).find(k => /vacanc/.test(k.toLowerCase()))] || primaryRows;
        const total = rows.length; const vacant = rows.filter(r => r.IsVacant === 1).length;
        computed[kpi.name] = total > 0 ? ((total - vacant) / total * 100) : 96.2;
      }
      // Arrears
      else if (/arrear/.test(measure)) {
        const rows = factData[Object.keys(factData).find(k => /rent|receipt/.test(k.toLowerCase()))] || primaryRows;
        computed[kpi.name] = sum(rows, 'AmountArrears') || sum(rows, 'Value1');
      }
      // Plan utilisation (NDIS)
      else if (/plan.utilisation|plan.util/.test(measure)) {
        const rows = factData[Object.keys(factData).find(k => /planutilisation|plan_util/.test(k.toLowerCase()))] || primaryRows;
        computed[kpi.name] = avg(rows, 'UtilisationRate') * 100 || 78.5;
      }
      // Claims / claim rate
      else if (/claim/.test(measure)) {
        const rows = factData[Object.keys(factData).find(k => /claim/.test(k.toLowerCase()))] || primaryRows;
        if (/rate|%/.test(measure)) {
          const approved = rows.filter(r => r.ClaimStatus === 'Approved').length;
          computed[kpi.name] = rows.length ? approved / rows.length * 100 : 87.3;
        } else {
          computed[kpi.name] = sum(rows, 'ClaimAmount') || sum(rows, 'ApprovedAmount');
        }
      }
      // Hours / delivered hours
      else if (/hour|delivered/.test(measure)) {
        computed[kpi.name] = sum(primaryRows, 'HoursDelivered') || sum(primaryRows, 'BillableHours') || sum(primaryRows, 'HoursWorked');
      }
      // Budget compliance
      else if (/budget/.test(measure)) {
        const rows = factData[Object.keys(factData).find(k => /budget/.test(k.toLowerCase()))] || primaryRows;
        computed[kpi.name] = sum(rows, 'BudgetAmount') || sum(rows, 'Value1');
      }
      // Expenditure
      else if (/expenditure|spend|cost/.test(measure)) {
        const rows = factData[Object.keys(factData).find(k => /expenditure|expense/.test(k.toLowerCase()))] || primaryRows;
        computed[kpi.name] = sum(rows, 'Amount') || sum(rows, 'ActualAmount') || sum(rows, 'Value1');
      }
      // KPI achievement (government)
      else if (/achievement|kpi.*rate/.test(measure)) {
        const rows = factData[Object.keys(factData).find(k => /program/.test(k.toLowerCase()))] || primaryRows;
        computed[kpi.name] = avg(rows, 'AchievementRate') * 100 || 84.2;
      }
      // Realisation
      else if (/realisation|realization/.test(measure)) {
        computed[kpi.name] = 83.4;
      }
      // Debtor days
      else if (/debtor.day|dso/.test(measure)) {
        computed[kpi.name] = 42;
      }
      // Write-off
      else if (/write.off/.test(measure)) {
        const rows = factData[Object.keys(factData).find(k => /engagement/.test(k.toLowerCase()))] || primaryRows;
        computed[kpi.name] = sum(rows, 'WriteOffAmount') || sum(rows, 'Value2');
      }
      // Portfolio size (property)
      else if (/portfolio/.test(measure)) {
        const rows = factData[Object.keys(factData).find(k => /managementfee|rent/.test(k.toLowerCase()))] || primaryRows;
        computed[kpi.name] = countDistinct(rows, 'PropertyKey') || 142;
      }
      // Pipeline
      else if (/pipeline|weighted/.test(measure)) {
        const rows = factData[Object.keys(factData).find(k => /pipeline/.test(k.toLowerCase()))] || primaryRows;
        computed[kpi.name] = sum(rows, 'WeightedValue') || sum(rows, 'EstimatedValue') * 0.4;
      }
      // Days vacant
      else if (/day.*vacant|vacant.*day/.test(measure)) {
        const rows = factData[Object.keys(factData).find(k => /vacanc/.test(k.toLowerCase()))] || primaryRows;
        computed[kpi.name] = avg(rows.filter(r => r.IsVacant === 1), 'DaysVacant') || 12;
      }
      // Incidents
      else if (/incident/.test(measure)) {
        const rows = factData[Object.keys(factData).find(k => /incident/.test(k.toLowerCase()))] || primaryRows;
        computed[kpi.name] = count(rows);
      }
      // Maintenance
      else if (/maintenance|job.*open/.test(measure)) {
        const rows = factData[Object.keys(factData).find(k => /maintenance/.test(k.toLowerCase()))] || primaryRows;
        computed[kpi.name] = rows.filter(r => r.IsOpen === 1).length;
      }
      // Grants
      else if (/grant/.test(measure)) {
        const rows = factData[Object.keys(factData).find(k => /grant/.test(k.toLowerCase()))] || primaryRows;
        computed[kpi.name] = sum(rows, 'ApprovedAmount') || sum(rows, 'Value1');
      }
      // Sales
      else if (/sales|revenue.*retail/.test(measure)) {
        const rows = factData[Object.keys(factData).find(k => /sales|transaction/.test(k.toLowerCase()))] || primaryRows;
        computed[kpi.name] = sum(rows, 'SalesAmount') || sum(rows, 'Value1');
      }
      // Margin
      else if (/margin|gross/.test(measure)) {
        const rows = factData[Object.keys(factData).find(k => /sales/.test(k.toLowerCase()))] || primaryRows;
        const rev = sum(rows, 'SalesAmount') || 1;
        const gm  = sum(rows, 'GrossMargin');
        computed[kpi.name] = gm && rev ? gm / rev * 100 : 38.2;
      }
      // Generic
      else {
        computed[kpi.name] = sum(primaryRows, 'Value1') || sum(primaryRows, 'BillableAmount') || count(primaryRows);
      }
    });

    return computed;
  }

  // ── Build chart datasets per page ─────────────────────────────
  function buildPageData(page, bp, mockData) {
    const { dims, factData, dates } = mockData;
    const facts = bp.data_model?.fact_tables || [];

    // Primary fact for this page (by keyword matching on page name/purpose)
    const pageKey = (page.name + ' ' + (page.purpose || '')).toLowerCase();
    let primaryRows = [];
    let primaryFactName = '';

    const factPriority = [
      [/timesheet|utilisation|consultant|staff|employee/, /timesheet|utilisation/],
      [/wip|revenue.leakage|billing|invoice/, /invoice|billing|engagement/],
      [/debtor|collection|receivable/, /invoice|billing/],
      [/pipeline|bd |business.dev/, /pipeline/],
      [/participant|ndis|service.delivery/, /servicedelivery|service_delivery/],
      [/claim/, /claim/],
      [/plan.utilisation|plan.util/, /planutilisation|plan_util/],
      [/incident|safety|quality/, /incident/],
      [/budget|expenditure|financial/, /budget|expenditure/],
      [/grant/, /grant/],
      [/program|outcome|kpi/, /program/],
      [/rent|arrear|tenancy/, /rentreceipt|rent/],
      [/vacancy|vacanc|letting/, /vacanc/],
      [/maintenance|repair/, /maintenance/],
      [/management.fee|portfolio/, /managementfee/],
      [/sales|transaction/, /sales|transaction/],
    ];

    for (const [pagePattern, factPattern] of factPriority) {
      if (pagePattern.test(pageKey)) {
        const key = Object.keys(factData).find(k => factPattern.test(k.toLowerCase()));
        if (key) { primaryRows = factData[key]; primaryFactName = key; break; }
      }
    }

    // Fallback: largest fact
    if (!primaryRows.length) {
      Object.entries(factData).forEach(([k, v]) => {
        if (v.length > primaryRows.length) { primaryRows = v; primaryFactName = k; }
      });
    }

    // Build charts for each visual type in the page
    const charts = [];
    const visuals = page.visuals || [];

    // Group visuals by type and build one dataset each
    visuals.forEach((visual, idx) => {
      const type = visual.type || 'Card';
      const title = visual.title || '';

      if (type === 'Card' || type === 'KPI') {
        // KPI card — scalar value
        const metricRows = primaryRows;
        let val = 0;
        const tl = title.toLowerCase();
        if (/revenue|fee|income/.test(tl))      val = sum(metricRows, 'InvoiceAmount') || sum(metricRows, 'BillableAmount') || sum(metricRows, 'FeeAmount') || sum(metricRows, 'Value1');
        else if (/wip/.test(tl))                val = sum(metricRows, 'WIPValue') || sum(metricRows, 'WIPBalance');
        else if (/utilisation|utilization/.test(tl)) val = avg(metricRows, 'UtilisationRate') * 100 || avg(metricRows, 'Rate') * 100 || 72.4;
        else if (/occupancy/.test(tl))          val = 96.2;
        else if (/arrear/.test(tl))             val = sum(metricRows, 'AmountArrears') || sum(metricRows, 'Value1');
        else if (/outstanding|debtor/.test(tl)) val = sum(metricRows, 'AmountOutstanding') || sum(metricRows, 'Value2');
        else if (/plan.util/.test(tl))          val = avg(metricRows, 'UtilisationRate') * 100 || 78.5;
        else if (/hour/.test(tl))               val = sum(metricRows, 'HoursDelivered') || sum(metricRows, 'BillableHours');
        else if (/portfolio|properties/.test(tl)) val = countDistinct(metricRows, 'PropertyKey') || 142;
        else if (/write.off/.test(tl))          val = sum(metricRows, 'WriteOffAmount') || 0;
        else if (/pipeline|weighted/.test(tl))  val = sum(metricRows, 'WeightedValue') || sum(metricRows, 'EstimatedValue') * 0.4;
        else if (/budget/.test(tl))             val = sum(metricRows, 'BudgetAmount') || sum(metricRows, 'Value1');
        else if (/expenditure|spend/.test(tl))  val = sum(metricRows, 'Amount') || sum(metricRows, 'ActualAmount');
        else if (/incident/.test(tl))           val = metricRows.filter(r => r.IsNotifiable).length;
        else if (/maintenance|open.job/.test(tl)) val = metricRows.filter(r => r.IsOpen === 1).length;
        else val = sum(metricRows, 'Value1') || sum(metricRows, 'BillableAmount') || count(metricRows);

        charts.push({ id: `chart-${page.name.replace(/\W/g,'-')}-${idx}`, type: 'card', title, value: val, visual });

      } else if (type === 'Line' || type.includes('Line')) {
        // Monthly trend
        const byMonth = groupBy(primaryRows, 'MonthLabel');
        const monthOrder = [...new Set(dates.map(d => d.MonthLabel))];
        const tl = title.toLowerCase();
        const labels = monthOrder.filter(m => byMonth[m]);
        const col = /utilisation|rate/.test(tl) ? 'UtilisationRate'
          : /wip/.test(tl) ? 'WIPValue'
          : /revenue|invoice|billing|fee/.test(tl) ? 'InvoiceAmount'
          : /plan.util/.test(tl) ? 'UtilisationRate'
          : /budget/.test(tl) ? 'BudgetAmount'
          : 'Value1';
        const isRate = /utilisation|rate|%/.test(tl);
        const data = labels.map(m => {
          const rows = byMonth[m] || [];
          return isRate ? parseFloat((avg(rows, col) * 100).toFixed(1))
            : parseFloat(sum(rows, col).toFixed(0));
        });
        charts.push({ id: `chart-${page.name.replace(/\W/g,'-')}-${idx}`, type: 'line', title, labels, data, isRate, visual });

      } else if (type === 'Bar' || type === 'Column') {
        // Categorical bar
        const tl = title.toLowerCase();
        let groupKey = 'MonthLabel', valueCol = 'Value1';
        // Guess group dimension from title
        if (/service.line|service line/.test(tl)) groupKey = 'ServiceLineKey';
        else if (/grade/.test(tl))                groupKey = 'Grade';
        else if (/client/.test(tl))               groupKey = 'ClientKey';
        else if (/manager|employee|consultant/.test(tl)) groupKey = 'EmployeeKey';
        else if (/stage/.test(tl))                groupKey = 'Stage';
        else if (/status/.test(tl))               groupKey = 'InvoiceStatus';
        else if (/category/.test(tl))             groupKey = 'Category';
        else if (/priority/.test(tl))             groupKey = 'Priority';
        else if (/type/.test(tl))                 groupKey = 'ExpenditureType';
        else if (/month|trend/.test(tl))          groupKey = 'MonthLabel';

        if (/utilisation|rate/.test(tl))          valueCol = 'UtilisationRate';
        else if (/wip/.test(tl))                  valueCol = 'WIPValue';
        else if (/revenue|invoice|fee/.test(tl))  valueCol = 'InvoiceAmount';
        else if (/write.off/.test(tl))            valueCol = 'WriteOffAmount';
        else if (/cost|amount/.test(tl))          valueCol = 'Amount';
        else if (/arrear/.test(tl))               valueCol = 'AmountArrears';
        else if (/pipeline|value/.test(tl))       valueCol = 'EstimatedValue';

        const isRate = /utilisation|rate/.test(tl);
        const byGroup = groupBy(primaryRows, groupKey);
        let labels = Object.keys(byGroup).slice(0, 12);
        // For named dims, try to lookup names
        if (groupKey === 'ServiceLineKey') labels = labels.map((k, i) => ['Audit & Assurance','Tax & Compliance','Business Advisory','Corporate Finance'][i] || k);
        else if (groupKey === 'Grade') { const gradeOrder = ['Partner','Director','Senior Manager','Manager','Senior Consultant','Consultant','Graduate']; labels = labels.sort((a,b) => gradeOrder.indexOf(a) - gradeOrder.indexOf(b)); }

        const data = labels.map(l => {
          const groupRows = byGroup[isRate ? l : l] || byGroup[labels.indexOf(l)] || [];
          const actualRows = groupRows.length ? groupRows : (Object.values(byGroup)[labels.indexOf(l)] || []);
          return isRate
            ? parseFloat((avg(actualRows, valueCol) * 100).toFixed(1))
            : parseFloat(sum(actualRows, valueCol).toFixed(0));
        });

        charts.push({ id: `chart-${page.name.replace(/\W/g,'-')}-${idx}`, type: type === 'Bar' ? 'bar-h' : 'bar', title, labels, data, isRate, visual });

      } else if (type === 'Donut' || type === 'Pie') {
        const byGroup = groupBy(primaryRows, 'InvoiceStatus');
        const labels  = Object.keys(byGroup).filter(k => k !== 'undefined');
        const data    = labels.map(l => sum(byGroup[l], 'AmountOutstanding') || sum(byGroup[l], 'AmountArrears') || byGroup[l].length);
        charts.push({ id: `chart-${page.name.replace(/\W/g,'-')}-${idx}`, type: 'doughnut', title, labels, data, visual });

      } else if (type === 'Waterfall') {
        const tl = title.toLowerCase();
        const cat = ['WIP Generated','Invoiced','Write-Offs','Remaining WIP'];
        const wip = sum(primaryRows, 'WIPValue') || sum(primaryRows, 'BillableAmount') || 1500000;
        const inv = sum(factData[Object.keys(factData).find(k => /invoice/.test(k.toLowerCase()))] || primaryRows, 'InvoiceAmount') || wip * 0.85;
        const wo  = sum(factData[Object.keys(factData).find(k => /engagement/.test(k.toLowerCase()))] || primaryRows, 'WriteOffAmount') || wip * 0.08;
        const rem = Math.max(0, wip - inv - wo);
        charts.push({ id: `chart-${page.name.replace(/\W/g,'-')}-${idx}`, type: 'waterfall', title, labels: cat, data: [wip, -inv, -wo, rem], visual });

      } else if (type === 'Scatter') {
        const pts = primaryRows.slice(0, 80).map((r, i) => ({
          x: parseFloat((r.BillableHours || r.HoursDelivered || r.Value1 || i).toFixed(1)),
          y: parseFloat((r.WIPValue || r.BillableAmount || r.Value2 || i * 150).toFixed(0)),
        }));
        charts.push({ id: `chart-${page.name.replace(/\W/g,'-')}-${idx}`, type: 'scatter', title, data: pts, visual });

      } else if (type === 'Funnel') {
        const stages = ['Lead','Qualified','Proposal Sent','Negotiation','Won'];
        const pipData = factData[Object.keys(factData).find(k => /pipeline/.test(k.toLowerCase()))] || primaryRows;
        const byStage = groupBy(pipData, 'Stage');
        const vals = stages.map(s => sum(byStage[s] || [], 'EstimatedValue') || MockEngine.rndInt(100000, 2000000));
        charts.push({ id: `chart-${page.name.replace(/\W/g,'-')}-${idx}`, type: 'funnel', title, labels: stages, data: vals, visual });

      } else if (type === 'Matrix' || type === 'Table') {
        // Sample tabular data
        const sample = primaryRows.slice(0, 12).map(r => ({
          label: r.MonthLabel || r.ServiceLineKey || r.Grade || r.Stage || '-',
          v1: parseFloat((r.BillableHours || r.HoursDelivered || r.Quantity || r.Value1 || 0).toFixed(1)),
          v2: parseFloat((r.WIPValue || r.BillableAmount || r.SalesAmount || r.Value2 || 0).toFixed(0)),
          v3: parseFloat((r.UtilisationRate || r.AchievementRate || 0).toFixed(3)),
        }));
        charts.push({ id: `chart-${page.name.replace(/\W/g,'-')}-${idx}`, type: 'table', title, data: sample, visual });

      } else {
        // Gauge / Radial as line
        const val = avg(primaryRows, 'UtilisationRate') * 100 || 72;
        charts.push({ id: `chart-${page.name.replace(/\W/g,'-')}-${idx}`, type: 'gauge', title, value: val, visual });
      }
    });

    return charts;
  }

  return { computeKPIs, buildPageData, sum, avg, count, groupBy, joinDim };
})();


/* ═══════════════════════════════════════════════════════════════
   SECTION 3: REPORT RENDERER
   Converts chart data into a complete standalone HTML page.
═══════════════════════════════════════════════════════════════ */

const ReportRenderer = (() => {

  function fmt(v, format) {
    if (v == null || isNaN(v)) return '—';
    format = (format || '').replace(/\s/g,'');
    if (format.includes('%'))    return v.toFixed(1) + '%';
    if (format === '#,##0.0')    return v.toLocaleString('en-AU', {minimumFractionDigits:1, maximumFractionDigits:1});
    if (format.includes('$'))    return v >= 1e6 ? '$' + (v/1e6).toFixed(1) + 'M' : v >= 1e3 ? '$' + (v/1e3).toFixed(0) + 'K' : '$' + v.toFixed(0);
    return v >= 1e6 ? (v/1e6).toFixed(1)+'M' : v >= 1e3 ? (v/1e3).toFixed(0)+'K' : Math.round(v).toLocaleString();
  }

  function kpiStatus(kpi, value) {
    const g = kpi.thresholds?.good || ''; const c = kpi.thresholds?.critical || '';
    const gN = parseFloat(g.replace(/[^0-9.\-]/g,''));
    const cN = parseFloat(c.replace(/[^0-9.\-]/g,''));
    if (!gN || !cN) return 'blue';
    const higher = gN > cN;
    if (higher) return value >= gN ? 'green' : value >= cN ? 'amber' : 'red';
    return value <= gN ? 'green' : value <= cN ? 'amber' : 'red';
  }

  function renderKPICard(kpi, value, currency) {
    const fmtVal = fmt(value, kpi.thresholds?.good?.includes('%') ? '#,##0.0%' : (value > 10000 ? '$#,##0' : '#,##0.0'));
    const status = kpiStatus(kpi, value);
    const colorMap = { green: 'var(--success)', amber: 'var(--warn)', red: 'var(--danger)', blue: 'var(--accent)' };
    const color = colorMap[status] || 'var(--accent)';
    return `
    <div class="kpi-card" style="border-top:2px solid ${color}">
      <div class="kpi-label">${kpi.name}</div>
      <div class="kpi-value" style="color:${color}">${fmtVal}</div>
      <div class="kpi-meta">Target: ${kpi.target_logic || '—'}</div>
      <div class="kpi-owner">👤 ${kpi.owner || '—'} · ${kpi.cadence || '—'}</div>
      ${kpi.thresholds ? `<div class="kpi-thresholds">
        <span class="thresh good">✓ ${kpi.thresholds.good}</span>
        <span class="thresh warn">⚠ ${kpi.thresholds.warning || kpi.thresholds.warn || ''}</span>
        <span class="thresh crit">✕ ${kpi.thresholds.critical}</span>
      </div>` : ''}
    </div>`;
  }

  function chartConfig(chart) {
    const ACCENT = '#5b6ef5', ACCENT2 = '#8b5cf6', ACCENT3 = '#06b6d4';
    const SUCCESS = '#22c55e', WARN = '#f59e0b', DANGER = '#ef4444';
    const TEXT = '#94a3b8', GRID = 'rgba(255,255,255,.05)';
    const COLORS = [ACCENT, ACCENT2, ACCENT3, WARN, SUCCESS, DANGER, '#f97316','#ec4899'];

    if (chart.type === 'card' || chart.type === 'gauge') return null;
    if (chart.type === 'table') return null;

    const base = {
      responsive: true, maintainAspectRatio: false,
      plugins: {
        legend: { labels: { color: TEXT, boxWidth: 10, font: { size: 11 } } },
        tooltip: { backgroundColor: '#1c2030', borderColor: '#2a3050', borderWidth: 1, titleColor: '#e2e8f0', bodyColor: TEXT },
      },
    };

    if (chart.type === 'line') {
      return {
        type: 'line',
        data: {
          labels: chart.labels,
          datasets: [{
            label: chart.title, data: chart.data,
            borderColor: ACCENT, backgroundColor: 'rgba(91,110,245,.08)',
            borderWidth: 2, fill: true, tension: 0.35,
            pointRadius: 3, pointBackgroundColor: ACCENT,
          }, ...(chart.isRate ? [{
            label: 'Target 75%',
            data: chart.labels.map(() => 75),
            borderColor: DANGER, borderDash: [5,5], borderWidth: 1.5,
            pointRadius: 0, fill: false,
          }] : [])],
        },
        options: {
          ...base,
          scales: {
            x: { grid: { color: GRID }, ticks: { color: TEXT, maxRotation: 45, font: { size: 10 } } },
            y: { grid: { color: GRID }, ticks: { color: TEXT, callback: v => chart.isRate ? v+'%' : v >= 1e6 ? '$'+(v/1e6).toFixed(1)+'M' : v >= 1e3 ? '$'+(v/1e3).toFixed(0)+'K' : v }, beginAtZero: true },
          },
        },
      };
    }

    if (chart.type === 'bar') {
      return {
        type: 'bar',
        data: {
          labels: chart.labels,
          datasets: [{ label: chart.title, data: chart.data,
            backgroundColor: chart.isRate
              ? chart.data.map(v => v >= 75 ? 'rgba(34,197,94,.6)' : 'rgba(245,158,11,.6)')
              : COLORS.map(c => c + 'aa'),
            borderColor: chart.isRate
              ? chart.data.map(v => v >= 75 ? SUCCESS : WARN)
              : COLORS,
            borderWidth: 1, borderRadius: 4 }],
        },
        options: { ...base, scales: {
          x: { grid: { color: GRID }, ticks: { color: TEXT, font: { size: 10 } } },
          y: { grid: { color: GRID }, ticks: { color: TEXT, callback: v => chart.isRate ? v+'%' : v >= 1e6 ? '$'+(v/1e6).toFixed(1)+'M' : v >= 1e3 ? '$'+(v/1e3).toFixed(0)+'K' : v }, beginAtZero: true },
        }},
      };
    }

    if (chart.type === 'bar-h') {
      return {
        type: 'bar',
        data: {
          labels: chart.labels,
          datasets: [{ label: chart.title, data: chart.data,
            backgroundColor: COLORS.map(c => c + 'aa'), borderColor: COLORS, borderWidth: 1, borderRadius: 4 }],
        },
        options: { ...base, indexAxis: 'y', scales: {
          x: { grid: { color: GRID }, ticks: { color: TEXT, callback: v => chart.isRate ? v+'%' : v >= 1e6 ? '$'+(v/1e6).toFixed(1)+'M' : v >= 1e3 ? '$'+(v/1e3).toFixed(0)+'K' : v }, beginAtZero: true },
          y: { grid: { color: 'transparent' }, ticks: { color: TEXT, font: { size: 10 } } },
        }},
      };
    }

    if (chart.type === 'doughnut') {
      return {
        type: 'doughnut',
        data: { labels: chart.labels, datasets: [{ data: chart.data, backgroundColor: COLORS.map(c=>c+'cc'), borderColor: '#0b0d14', borderWidth: 2 }] },
        options: { ...base, plugins: { legend: { position: 'bottom', labels: { color: TEXT, boxWidth: 10 } }, tooltip: base.plugins.tooltip } },
      };
    }

    if (chart.type === 'scatter') {
      return {
        type: 'scatter',
        data: { datasets: [{ label: chart.title, data: chart.data, backgroundColor: ACCENT + '88', borderColor: ACCENT, pointRadius: 5 }] },
        options: { ...base, scales: {
          x: { grid: { color: GRID }, ticks: { color: TEXT }, beginAtZero: true },
          y: { grid: { color: GRID }, ticks: { color: TEXT, callback: v => v >= 1e3 ? '$'+(v/1e3).toFixed(0)+'K' : v }, beginAtZero: true },
        }},
      };
    }

    if (chart.type === 'waterfall') {
      const WFCOLORS = chart.data.map(v => v >= 0 ? 'rgba(34,197,94,.6)' : 'rgba(239,68,68,.6)');
      const WFBORDER = chart.data.map(v => v >= 0 ? SUCCESS : DANGER);
      return {
        type: 'bar',
        data: { labels: chart.labels, datasets: [{ label: chart.title, data: chart.data.map(Math.abs),
          backgroundColor: WFCOLORS, borderColor: WFBORDER, borderWidth: 1, borderRadius: 4 }] },
        options: { ...base, scales: {
          x: { grid: { color: GRID }, ticks: { color: TEXT } },
          y: { grid: { color: GRID }, ticks: { color: TEXT, callback: v => v >= 1e6 ? '$'+(v/1e6).toFixed(1)+'M' : '$'+(v/1e3).toFixed(0)+'K' }, beginAtZero: true },
        }},
      };
    }

    if (chart.type === 'funnel') {
      return {
        type: 'bar',
        data: { labels: chart.labels, datasets: [{ label: 'Pipeline Value', data: chart.data,
          backgroundColor: ['rgba(91,110,245,.5)','rgba(6,182,212,.5)','rgba(139,92,246,.5)','rgba(245,158,11,.5)','rgba(34,197,94,.5)'],
          borderColor: [ACCENT, ACCENT3, ACCENT2, WARN, SUCCESS], borderWidth: 1, borderRadius: 4 }] },
        options: { ...base, indexAxis: 'y', scales: {
          x: { grid: { color: GRID }, ticks: { color: TEXT, callback: v => '$'+(v/1e3).toFixed(0)+'K' }, beginAtZero: true },
          y: { grid: { color: 'transparent' }, ticks: { color: TEXT } },
        }},
      };
    }

    return null;
  }

  function buildPageHTML(page, charts, kpis, kpiValues, currency, pageIdx) {
    const layout = page.layout || 'Analytical';
    const LAYOUT_ICONS = { Executive: '🏛', Analytical: '📈', Operational: '⚙️', Detail: '🔍' };

    // Separate cards from chart visuals
    const cards   = charts.filter(c => c.type === 'card' || c.type === 'gauge');
    const chartVis = charts.filter(c => c.type !== 'card' && c.type !== 'gauge' && c.type !== 'table');
    const tables  = charts.filter(c => c.type === 'table');

    // Relevant KPIs for this page
    const pageKPIs = kpis.slice(0, 4);

    let html = `<div class="page-content${pageIdx === 0 ? ' active' : ''}" id="page-${pageIdx}" data-page="${pageIdx}">`;

    // Storytelling flow banner
    if (page.storytelling_flow) {
      html += `<div class="story-banner"><span class="story-icon">📖</span>${page.storytelling_flow}</div>`;
    }

    // Slicer bar
    if (page.slicers && page.slicers.length) {
      html += `<div class="slicer-bar">
        <span class="slicer-label">🔽 Filters</span>
        <div class="slicer-sep"></div>`;
      page.slicers.slice(0, 5).forEach((s, i) => {
        const field = typeof s === 'object' ? s.field : s;
        html += `<div class="slicer-group">
          <span class="slicer-field-label">${field.split('[').pop().replace(']','').replace(/([A-Z])/g,' $1').trim()}</span>
          <select class="slicer-select" onchange="applySlicer(${pageIdx},${i},this.value)">
            <option value="all">All</option>
          </select>
        </div>`;
      });
      html += `<button class="slicer-reset" onclick="resetSlicers(${pageIdx})">✕ Reset</button></div>`;
    }

    // KPI cards row (Executive and Analytical pages)
    if (layout !== 'Detail' && pageKPIs.length) {
      html += `<div class="kpi-row">`;
      pageKPIs.forEach(kpi => {
        html += renderKPICard(kpi, kpiValues[kpi.name] || 0, currency);
      });
      html += `</div>`;
    }

    // Card visuals as mini-KPI strip
    if (cards.length) {
      html += `<div class="card-strip">`;
      cards.forEach(c => {
        const fv = fmt(c.value, c.value > 1000 ? '$#,##0' : '#,##0.0');
        html += `<div class="mini-card"><div class="mini-card-title">${c.title}</div><div class="mini-card-value">${fv}</div></div>`;
      });
      html += `</div>`;
    }

    // Charts grid
    if (chartVis.length) {
      const gridClass = chartVis.length === 1 ? 'chart-grid-1' : chartVis.length <= 2 ? 'chart-grid-2' : 'chart-grid-3';
      html += `<div class="${gridClass}">`;
      chartVis.forEach(c => {
        const isWide = chartVis.length === 1 || c.type === 'waterfall' || c.type === 'funnel';
        html += `<div class="chart-card${isWide ? ' span-full' : ''}">
          <div class="chart-hdr">
            <div class="chart-title">${c.title}</div>
          </div>
          <div class="chart-wrap">
            <canvas id="${c.id}"></canvas>
          </div>
        </div>`;
      });
      html += `</div>`;
    }

    // Tables
    tables.forEach(t => {
      html += `<div class="chart-card" style="margin-bottom:14px">
        <div class="chart-hdr"><div class="chart-title">${t.title}</div></div>
        <div class="tbl-wrap">
          <table class="data-table">
            <thead><tr><th>Label</th><th>Value 1</th><th>Value 2</th><th>Rate</th></tr></thead>
            <tbody>
              ${t.data.map(r => `<tr>
                <td>${r.label}</td>
                <td>${r.v1.toLocaleString()}</td>
                <td>${r.v2 >= 1000 ? '$'+(r.v2/1000).toFixed(0)+'K' : '$'+r.v2}</td>
                <td>${(r.v3 * 100).toFixed(1)}%</td>
              </tr>`).join('')}
            </tbody>
          </table>
        </div>
      </div>`;
    });

    html += `</div>`;
    return html;
  }

  // ── MAIN: Build the complete report HTML ──────────────────────
  function buildReport(bp, mockData, kpiValues) {
    const pages    = bp.pages || [];
    const kpis     = bp.kpis || [];
    const meta     = bp.meta || {};
    const currency = meta.currency || 'AUD';
    const industry = meta.industry || 'Industry';
    const title    = meta.title || industry + ' Performance Dashboard';
    const conf     = bp.confidence?.score || bp.detection?.confidence || 75;
    const confColor = conf >= 85 ? '#22c55e' : conf >= 70 ? '#5b6ef5' : conf >= 55 ? '#f59e0b' : '#ef4444';

    // Build page charts
    const allPageCharts = pages.map(page => Metrics.buildPageData(page, bp, mockData));

    // Chart config JSON for embedding
    const chartConfigs = {};
    allPageCharts.forEach((pageCharts, pi) => {
      pageCharts.forEach(c => {
        const cfg = chartConfig(c);
        if (cfg) chartConfigs[c.id] = cfg;
      });
    });

    // Tab navigation
    const tabsHTML = pages.map((p, i) => {
      const icons = { Executive: '🏛', Analytical: '📈', Operational: '⚙️', Detail: '🔍' };
      return `<button class="page-tab${i === 0 ? ' active' : ''}" onclick="switchPage(${i},this)">${icons[p.layout] || '📄'} ${p.name}</button>`;
    }).join('');

    // Page content
    const pagesHTML = pages.map((page, pi) =>
      buildPageHTML(page, allPageCharts[pi], kpis, kpiValues, currency, pi)
    ).join('');

    return `<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8"/>
<meta name="viewport" content="width=device-width,initial-scale=1"/>
<title>${title} — Mock Report</title>
<script src="https://cdnjs.cloudflare.com/ajax/libs/Chart.js/4.4.1/chart.umd.min.js"><\/script>
<style>
:root{
  --bg:#0b0d14;--surface:#13161f;--surface2:#1c2030;--surface3:#242840;
  --border:#1e2236;--border2:#2a3050;
  --text:#e2e8f0;--text2:#94a3b8;--text3:#64748b;
  --accent:#5b6ef5;--accent2:#8b5cf6;--accent3:#06b6d4;
  --success:#22c55e;--warn:#f59e0b;--danger:#ef4444;
  --font:'Segoe UI',system-ui,sans-serif;
  --radius:10px;
}
*{box-sizing:border-box;margin:0;padding:0}
html{scroll-behavior:smooth}
body{font-family:var(--font);background:var(--bg);color:var(--text);font-size:14px}

/* ── Cover ─────────────────────────────────────────── */
.cover{
  background:linear-gradient(135deg,#0b0d14 0%,#1a2040 55%,#0b0d14 100%);
  padding:48px 52px 44px;position:relative;overflow:hidden;
  border-bottom:1px solid var(--border);
}
.cover::before{content:'';position:absolute;inset:0;
  background:radial-gradient(ellipse at 65% 40%,rgba(91,110,245,.14) 0%,transparent 60%);pointer-events:none}
.cover-eyebrow{font-size:10px;font-weight:700;letter-spacing:2px;text-transform:uppercase;
  color:rgba(255,255,255,.4);margin-bottom:10px;display:flex;align-items:center;gap:8px}
.cover-eyebrow::before{content:'';width:24px;height:1px;background:rgba(255,255,255,.3)}
.cover-industry{font-size:10px;font-weight:700;letter-spacing:2px;text-transform:uppercase;
  color:var(--accent);margin-bottom:8px}
.cover-title{font-size:36px;font-weight:900;letter-spacing:-1px;line-height:1.1;
  color:var(--text);margin-bottom:10px;position:relative}
.cover-title span{background:linear-gradient(90deg,var(--accent),var(--accent2));
  -webkit-background-clip:text;-webkit-text-fill-color:transparent}
.cover-sub{font-size:13px;color:var(--text2);margin-bottom:28px;max-width:520px;line-height:1.6;position:relative}
.cover-stats{display:flex;gap:16px;flex-wrap:wrap;position:relative}
.cover-stat{background:rgba(30,34,54,.6);border:1px solid var(--border2);border-radius:10px;padding:14px 18px;text-align:center;min-width:80px}
.cover-stat-num{font-size:28px;font-weight:900;color:var(--text);line-height:1}
.cover-stat-lbl{font-size:9px;font-weight:700;color:var(--text3);text-transform:uppercase;letter-spacing:.6px;margin-top:4px}
.cover-disclaimer{position:absolute;bottom:14px;right:52px;font-size:9px;color:var(--text3);max-width:400px;text-align:right;line-height:1.5}

/* ── Topbar ─────────────────────────────────────────── */
.topbar{position:sticky;top:0;z-index:100;display:flex;align-items:center;gap:10px;
  padding:0 20px;height:46px;background:rgba(11,13,20,.95);border-bottom:1px solid var(--border);
  backdrop-filter:blur(10px)}
.topbar-logo{font-size:13px;font-weight:800}
.topbar-logo span{background:linear-gradient(90deg,var(--accent),var(--accent2));
  -webkit-background-clip:text;-webkit-text-fill-color:transparent}
.tb-sep{flex:1}
.tb-chip{font-size:9px;font-weight:700;padding:3px 8px;border-radius:10px;border:1px solid;white-space:nowrap}
.tb-mock{background:rgba(6,182,212,.1);color:var(--accent3);border-color:rgba(6,182,212,.3)}
.tb-industry{background:rgba(91,110,245,.1);color:var(--accent);border-color:rgba(91,110,245,.3)}
.tb-conf{color:${confColor};background:transparent;border-color:${confColor}33}
.live-dot{width:6px;height:6px;border-radius:50%;background:var(--success);box-shadow:0 0 5px var(--success);animation:pulse 2s infinite;flex-shrink:0}
@keyframes pulse{0%,100%{opacity:1}50%{opacity:.3}}

/* ── Page tabs ──────────────────────────────────────── */
.page-tabs{display:flex;gap:1px;padding:10px 20px 0;border-bottom:1px solid var(--border);
  background:var(--surface);overflow-x:auto}
.page-tab{padding:7px 14px;border-radius:7px 7px 0 0;font-size:11px;font-weight:600;
  color:var(--text3);cursor:pointer;border:none;background:none;
  border-bottom:2px solid transparent;transition:all .15s;white-space:nowrap;font-family:var(--font)}
.page-tab:hover{color:var(--text);background:var(--surface2)}
.page-tab.active{color:var(--accent);border-bottom-color:var(--accent);background:var(--surface2)}

/* ── Page content ───────────────────────────────────── */
.page-content{display:none;padding:16px 20px 40px}
.page-content.active{display:block}

/* ── Storytelling banner ────────────────────────────── */
.story-banner{display:flex;align-items:flex-start;gap:8px;padding:9px 12px;
  background:rgba(91,110,245,.06);border:1px solid rgba(91,110,245,.15);border-radius:8px;
  font-size:11px;color:var(--text2);margin-bottom:12px;line-height:1.5}
.story-icon{font-size:14px;flex-shrink:0;margin-top:1px}

/* ── Slicer bar ─────────────────────────────────────── */
.slicer-bar{display:flex;align-items:center;gap:10px;flex-wrap:wrap;
  padding:8px 12px;background:var(--surface);border:1px solid var(--border);
  border-radius:8px;margin-bottom:14px}
.slicer-label{font-size:9px;font-weight:700;color:var(--text3);text-transform:uppercase;letter-spacing:.5px}
.slicer-sep{width:1px;height:18px;background:var(--border2)}
.slicer-group{display:flex;align-items:center;gap:5px}
.slicer-field-label{font-size:10px;color:var(--text2)}
.slicer-select{background:var(--surface2);border:1px solid var(--border2);color:var(--text);
  border-radius:5px;padding:3px 7px;font-size:11px;font-family:var(--font);cursor:pointer;outline:none}
.slicer-select:focus{border-color:var(--accent)}
.slicer-reset{margin-left:auto;font-size:10px;font-weight:600;color:var(--accent);
  cursor:pointer;background:none;border:none;font-family:var(--font);padding:3px 7px}

/* ── KPI row ────────────────────────────────────────── */
.kpi-row{display:grid;grid-template-columns:repeat(auto-fill,minmax(200px,1fr));gap:10px;margin-bottom:14px}
.kpi-card{background:var(--surface);border:1px solid var(--border);border-radius:var(--radius);
  padding:12px 14px;transition:border-color .15s}
.kpi-card:hover{border-color:var(--accent)}
.kpi-label{font-size:9px;font-weight:700;color:var(--text3);text-transform:uppercase;letter-spacing:.5px;margin-bottom:5px}
.kpi-value{font-size:24px;font-weight:800;letter-spacing:-.5px;line-height:1;margin-bottom:4px}
.kpi-meta{font-size:10px;color:var(--text3);margin-bottom:2px}
.kpi-owner{font-size:9px;color:var(--text3)}
.kpi-thresholds{display:flex;gap:6px;margin-top:6px;flex-wrap:wrap}
.thresh{font-size:9px;font-weight:600;padding:1px 5px;border-radius:4px}
.thresh.good{background:rgba(34,197,94,.1);color:var(--success)}
.thresh.warn{background:rgba(245,158,11,.1);color:var(--warn)}
.thresh.crit{background:rgba(239,68,68,.1);color:var(--danger)}

/* ── Card strip ─────────────────────────────────────── */
.card-strip{display:flex;gap:8px;flex-wrap:wrap;margin-bottom:14px}
.mini-card{background:var(--surface);border:1px solid var(--border);border-radius:8px;
  padding:10px 14px;min-width:140px;flex:1}
.mini-card-title{font-size:9px;font-weight:700;color:var(--text3);text-transform:uppercase;letter-spacing:.5px;margin-bottom:4px}
.mini-card-value{font-size:20px;font-weight:800;color:var(--accent)}

/* ── Chart grids ────────────────────────────────────── */
.chart-grid-1,.chart-grid-2,.chart-grid-3{display:grid;gap:12px;margin-bottom:12px}
.chart-grid-1{grid-template-columns:1fr}
.chart-grid-2{grid-template-columns:1fr 1fr}
.chart-grid-3{grid-template-columns:1fr 1fr 1fr}
.chart-card{background:var(--surface);border:1px solid var(--border);border-radius:var(--radius);padding:14px}
.chart-card.span-full{grid-column:1/-1}
.chart-hdr{margin-bottom:10px}
.chart-title{font-size:12px;font-weight:700;color:var(--text)}
.chart-wrap{position:relative;height:220px}
.chart-wrap canvas{width:100%!important}

/* ── Table ──────────────────────────────────────────── */
.tbl-wrap{overflow-x:auto}
.data-table{width:100%;border-collapse:collapse;font-size:12px}
.data-table th{padding:7px 10px;text-align:left;font-size:9px;font-weight:700;
  color:var(--text3);text-transform:uppercase;letter-spacing:.5px;
  border-bottom:1px solid var(--border2);background:var(--surface2)}
.data-table td{padding:7px 10px;border-bottom:1px solid var(--border);color:var(--text2);vertical-align:middle}
.data-table tr:hover td{background:var(--surface2)}
.data-table tr:last-child td{border-bottom:none}

/* ── Footer ─────────────────────────────────────────── */
.footer{padding:16px 20px;font-size:9px;color:var(--text3);border-top:1px solid var(--border);text-align:center;line-height:1.7}

/* ── Responsive ─────────────────────────────────────── */
@media(max-width:900px){
  .chart-grid-2,.chart-grid-3{grid-template-columns:1fr}
  .chart-card.span-full{grid-column:1}
  .kpi-row{grid-template-columns:repeat(2,1fr)}
  .cover{padding:28px 20px 24px}
  .cover-title{font-size:26px}
}
</style>
</head>
<body>

<!-- Cover -->
<div class="cover">
  <div class="cover-eyebrow">Mock Report — Blueprint Generated</div>
  <div class="cover-industry">${industry}</div>
  <h1 class="cover-title">${title.includes(industry) ? title.replace(industry, `<span>${industry}</span>`) : `<span>${title}</span>`}</h1>
  <div class="cover-sub">${meta.business_goal || 'Monitor organisational performance and support executive decision-making.'}</div>
  <div class="cover-stats">
    <div class="cover-stat">
      <div class="cover-stat-num" style="color:${confColor}">${conf}</div>
      <div class="cover-stat-lbl">Confidence</div>
    </div>
    <div class="cover-stat">
      <div class="cover-stat-num">${(bp.data_model?.fact_tables || []).length}</div>
      <div class="cover-stat-lbl">Fact Tables</div>
    </div>
    <div class="cover-stat">
      <div class="cover-stat-num">${kpis.length}</div>
      <div class="cover-stat-lbl">KPIs</div>
    </div>
    <div class="cover-stat">
      <div class="cover-stat-num">${pages.length}</div>
      <div class="cover-stat-lbl">Pages</div>
    </div>
    <div class="cover-stat">
      <div class="cover-stat-num">${(bp.measures || []).length}</div>
      <div class="cover-stat-lbl">Measures</div>
    </div>
    <div class="cover-stat">
      <div class="cover-stat-num">${meta.refresh_cadence || 'Daily'}</div>
      <div class="cover-stat-lbl">Refresh</div>
    </div>
  </div>
  <div class="cover-disclaimer">Mock data generated from blueprint schema. Values are synthetic and for demonstration only.</div>
</div>

<!-- Topbar -->
<header class="topbar">
  <div class="topbar-logo">📊 <span>StudioTech BI</span> — Mock Report</div>
  <div class="tb-sep"></div>
  <div class="live-dot"></div>
  <span class="tb-chip tb-mock">Synthetic Mock Data</span>
  <span class="tb-chip tb-industry">${industry}</span>
  <span class="tb-chip tb-conf">Confidence: ${conf}/100</span>
</header>

<!-- Page tabs -->
<nav class="page-tabs">${tabsHTML}</nav>

<!-- Page content -->
${pagesHTML}

<div class="footer">
  StudioTech BI Blueprint Generator · ${title} · Mock Report<br>
  Data: Synthetically generated from blueprint schema · ${(bp.data_model?.fact_tables || []).length} fact tables · ${kpis.length} KPIs · ${pages.length} pages<br>
  <strong>Generated recommendations based on supplied business requirements. No actual data quality, performance or compliance assessments performed. Professional review recommended.</strong>
</div>

<script>
// ── Chart configs ───────────────────────────────────────────────
const CHART_CONFIGS = ${JSON.stringify(chartConfigs)};

// Initialise all charts
Chart.defaults.color = '#94a3b8';
Chart.defaults.borderColor = '#1e2236';
Chart.defaults.font.family = "'Segoe UI', system-ui, sans-serif";

const chartInstances = {};

function initChartsForPage(pageIdx) {
  const page = document.getElementById('page-' + pageIdx);
  if (!page) return;
  page.querySelectorAll('canvas').forEach(canvas => {
    const id = canvas.id;
    if (chartInstances[id]) return; // already initialised
    const cfg = CHART_CONFIGS[id];
    if (!cfg) return;
    chartInstances[id] = new Chart(canvas, cfg);
  });
}

// ── Page navigation ─────────────────────────────────────────────
function switchPage(idx, btn) {
  document.querySelectorAll('.page-content').forEach(p => p.classList.remove('active'));
  document.querySelectorAll('.page-tab').forEach(b => b.classList.remove('active'));
  const page = document.getElementById('page-' + idx);
  if (page) page.classList.add('active');
  if (btn)  btn.classList.add('active');
  // Init charts lazily on first view
  setTimeout(() => initChartsForPage(idx), 50);
}

// ── Slicers (visual feedback — full data binding would need a server) ──
function applySlicer(pageIdx, slicerIdx, value) {
  // Show applied filter badge
  const bar = document.querySelectorAll('#page-' + pageIdx + ' .slicer-bar')[0];
  if (!bar) return;
  let badge = bar.querySelector('.slicer-active-badge');
  if (!badge) {
    badge = document.createElement('span');
    badge.className = 'slicer-active-badge';
    badge.style.cssText = 'font-size:9px;font-weight:700;padding:2px 7px;border-radius:10px;background:rgba(91,110,245,.15);color:var(--accent);border:1px solid rgba(91,110,245,.3)';
    bar.appendChild(badge);
  }
  if (value === 'all') { badge.remove(); return; }
  badge.textContent = '✓ ' + value;
}

function resetSlicers(pageIdx) {
  document.querySelectorAll('#page-' + pageIdx + ' .slicer-select').forEach(s => s.value = 'all');
  document.querySelectorAll('#page-' + pageIdx + ' .slicer-active-badge').forEach(b => b.remove());
}

// ── Init first page ─────────────────────────────────────────────
window.addEventListener('load', () => initChartsForPage(0));
<\/script>
</body>
</html>`;
  }

  return { buildReport, fmt };
})();


/* ═══════════════════════════════════════════════════════════════
   SECTION 4: PUBLIC API
   Called from index.html after blueprint generation.
═══════════════════════════════════════════════════════════════ */

/**
 * launchMockReport(bp)
 * Generate mock data and open the interactive report in a new tab.
 * Called by the "📊 Mock Report" button in index.html.
 */
function launchMockReport(bp) {
  if (!bp) {
    alert('Generate a blueprint first, then click Mock Report.');
    return;
  }

  const btn = document.getElementById('mockReportBtn');
  if (btn) { btn.disabled = true; btn.textContent = '⏳ Building report…'; }

  // Run async so UI updates before heavy computation
  setTimeout(() => {
    try {
      // 1. Generate mock data
      const mockData = MockEngine.generate(bp);

      // 2. Compute KPI values
      const kpiValues = Metrics.computeKPIs(bp, mockData);

      // 3. Build HTML
      const reportHTML = ReportRenderer.buildReport(bp, mockData, kpiValues);

      // 4. Open in new tab
      const win = window.open('', '_blank');
      if (!win) {
        alert('Popup blocked. Please allow popups for this site and try again.');
        return;
      }
      win.document.write(reportHTML);
      win.document.close();

    } catch (err) {
      console.error('[mockReport] Error:', err);
      alert('Could not build mock report: ' + err.message);
    } finally {
      if (btn) { btn.disabled = false; btn.innerHTML = '📊 Mock Report'; }
    }
  }, 60);
}
