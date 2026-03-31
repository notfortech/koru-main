"""
Report Refresh: If the master Excel has data (optionally for a given period), refresh the Power BI dataset
so the report in the client portal updates. Master xlsx path is static for now.
"""
import io
import re
import sys
from datetime import datetime, timezone

import pandas as pd

from blob_service import (
    download_master_bytes,
    get_last_refresh_time,
    set_last_refresh_time,
)
from powerbi_refresh import refresh_dataset

# Output messages for the API to parse
MSG_REFRESHED = "Dataset refreshed"
MSG_NO_UPDATES = "No updates to master file; report is already up to date."
MSG_NO_MASTER = "No master file found."
MSG_NO_PERIOD = "No data for period {period} in master file."

# Period column names to look for (case-insensitive)
PERIOD_COLUMN_NAMES = ("period", "yearmonth", "year_month", "year-month", "year month", "month", "reporting period")


def _utc_now():
    return datetime.now(timezone.utc)


def _ensure_utc(dt):
    if dt is None:
        return None
    if dt.tzinfo is None:
        return dt.replace(tzinfo=timezone.utc)
    return dt.astimezone(timezone.utc)


def _normalize_period_value(val):
    """Convert various period formats to YYYY-MM string, or None."""
    if val is None or (isinstance(val, float) and pd.isna(val)):
        return None
    s = str(val).strip()
    if not s:
        return None
    # Already YYYY-MM or YYYY/MM
    m = re.match(r"^(\d{4})[-/](\d{1,2})$", s)
    if m:
        return f"{m.group(1)}-{int(m.group(2)):02d}"
    # MM/YYYY or MM-YYYY
    m = re.match(r"^(\d{1,2})[-/](\d{4})$", s)
    if m:
        return f"{m.group(2)}-{int(m.group(1)):02d}"
    # Month name abbreviations (e.g. Apr 2025, 2025 Apr)
    months = {"jan": "01", "feb": "02", "mar": "03", "apr": "04", "may": "05", "jun": "06",
              "jul": "07", "aug": "08", "sep": "09", "oct": "10", "nov": "11", "dec": "12"}
    s_lower = s.lower()
    for name, num in months.items():
        if name in s_lower:
            y = re.search(r"20\d{2}", s)
            if y:
                return f"{y.group(0)}-{num}"
    return None


def _find_period_column(df):
    """Return first column name that looks like a period column."""
    for c in df.columns:
        if str(c).strip().lower().replace(" ", "_").replace("-", "_") in (
            "period", "yearmonth", "year_month", "month", "reporting_period"
        ):
            return c
    return None


def master_has_period(master_bytes, period_requested):
    """
    Read master Excel and return True if it contains data for period_requested (e.g. '2025-04').
    Looks for a period-like column and normalizes values to YYYY-MM.
    """
    if not master_bytes or not period_requested:
        return False
    period_requested = period_requested.strip()
    if re.match(r"^\d{4}-\d{2}$", period_requested) is None:
        period_requested = _normalize_period_value(period_requested) or period_requested
    try:
        df = pd.read_excel(io.BytesIO(master_bytes), sheet_name=0)
    except Exception:
        return False
    if df.empty:
        return False
    col = _find_period_column(df)
    if col is None:
        # No period column: assume first column might be period, or check all columns
        for c in df.columns:
            periods = set()
            for v in df[c].dropna():
                p = _normalize_period_value(v)
                if p:
                    periods.add(p)
            if period_requested in periods:
                return True
        return False
    periods = set()
    for v in df[col].dropna():
        p = _normalize_period_value(v)
        if p:
            periods.add(p)
    return period_requested in periods


def main():
    if len(sys.argv) < 2:
        print("Usage: python refresh_report_if_updated.py <client_id> [period] [workspace_id] [dataset_id]", file=sys.stderr)
        print("  period optional (e.g. 2025-04). If omitted, refresh when master exists.", file=sys.stderr)
        print("  workspace_id and dataset_id optional; when both provided, refresh that client's Power BI dataset.", file=sys.stderr)
        sys.exit(2)
    client_id = sys.argv[1].strip()
    period = sys.argv[2].strip() if len(sys.argv) >= 3 else ""
    workspace_id = sys.argv[3].strip() if len(sys.argv) >= 4 else ""
    dataset_id = sys.argv[4].strip() if len(sys.argv) >= 5 else ""
    if not client_id:
        print("client_id is required", file=sys.stderr)
        sys.exit(2)

    # Append any new data from monthly folders (validated/yyyy-mm/*.xlsx) to master first
    try:
        from append_master import append_from_monthly_folders
        append_from_monthly_folders(client_id)
    except BaseException:
        print("Monthly append skipped.", file=sys.stderr)

    master_bytes = download_master_bytes(client_id)
    if master_bytes is None:
        print(MSG_NO_MASTER)
        sys.exit(0)

    if period:
        if not master_has_period(master_bytes, period):
            print(MSG_NO_PERIOD.format(period=period))
            sys.exit(0)

    # Master exists (and has period data if period was requested): refresh dataset
    try:
        if workspace_id and dataset_id:
            refresh_dataset(client_id, workspace_id=workspace_id, dataset_id=dataset_id)
        else:
            refresh_dataset(client_id)
        set_last_refresh_time(client_id, _utc_now())
        print(MSG_REFRESHED)
    except Exception as e:
        err = str(e)
        if "429" in err or "exceeded the amount of requests" in err.lower():
            print("POWERBI_RATE_LIMIT: Power BI rate limit. Retry in 2 minutes.", file=sys.stderr)
            sys.exit(0)
        print("ERROR: Refresh failed.", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
