"""
Append validated Excel data to the master file.

When "Refresh report" is pressed:
  1. Look for new month folders under AU-001/accounting/validated/yyyy-mm/ (e.g. 2025-06)
     that are not yet in .processed_monthly (i.e. not yet part of master).
  2. In each new month folder, find all .xlsx files (e.g. Accounting_Template_Starter_June.xlsx).
  3. For each file: read Excel, apply transformations (required columns, date format,
     numeric validation, YearMonth, continuous TransactionID, align date to master format).
  4. Append rows to master and upload. Mark month as processed, then refresh Power BI dataset.

Transformations applied (same as script): REQUIRED_COLUMNS validated, TransactionDate parsed
and YearMonth added, Debit/Credit numeric, TransactionID continuous, date format aligned to master.
"""
import io
import sys
import pandas as pd
from blob_service import (
    container,
    get_master_path,
    get_latest_validated,
    list_monthly_validated_excels,
    get_processed_months,
    add_processed_month,
)

REQUIRED_COLUMNS = [
    "TransactionDate",
    "ClientID",
    "DebitAmount",
    "CreditAmount",
]

MASTER_SHEET = "Transactions"


def _normalize_new_data(df_new):
    """Validate and normalize columns; ensure TransactionDate is datetime, add YearMonth."""
    for col in REQUIRED_COLUMNS:
        if col not in df_new.columns:
            raise ValueError(f"Missing column {col}")
    df_new = df_new.copy()
    df_new["TransactionDate"] = pd.to_datetime(df_new["TransactionDate"], errors="coerce")
    if df_new["TransactionDate"].isna().any():
        raise ValueError("Invalid dates in TransactionDate")
    df_new["YearMonth"] = df_new["TransactionDate"].dt.strftime("%Y-%m")
    df_new["DebitAmount"] = pd.to_numeric(df_new["DebitAmount"], errors="coerce")
    df_new["CreditAmount"] = pd.to_numeric(df_new["CreditAmount"], errors="coerce")
    if df_new["DebitAmount"].isna().any() or df_new["CreditAmount"].isna().any():
        raise ValueError("Invalid numeric values in DebitAmount or CreditAmount")
    return df_new


def _align_date_format_to_master(df_new, df_master):
    """
    Ensure new TransactionDate column matches master's date representation.
    Master may have datetime or string; we output same type/format.
    """
    if df_master.empty or "TransactionDate" not in df_master.columns:
        return df_new
    master_dates = df_master["TransactionDate"]
    if pd.api.types.is_datetime64_any_dtype(master_dates):
        # Keep as datetime (Excel serial date)
        df_new["TransactionDate"] = pd.to_datetime(df_new["TransactionDate"]).dt.tz_localize(None)
        return df_new
    # Master has string dates: use same format (e.g. YYYY-MM-DD)
    sample = master_dates.dropna().iloc[0] if len(master_dates.dropna()) else None
    if sample is not None and isinstance(sample, str) and len(sample) >= 10:
        fmt = "%Y-%m-%d" if sample[4] == "-" else "%d/%m/%Y" if "/" in sample else "%Y-%m-%d"
        df_new["TransactionDate"] = pd.to_datetime(df_new["TransactionDate"]).dt.strftime(fmt)
    return df_new


def _load_master(client_id):
    """Load master as DataFrame; return (df, blob_client). Create minimal df if not exists."""
    master_path = get_master_path(client_id)
    blob = container.get_blob_client(master_path)
    try:
        data = blob.download_blob().readall()
        df = pd.read_excel(io.BytesIO(data), sheet_name=0)
    except Exception:
        # No master yet: start with required columns + TransactionID, YearMonth
        df = pd.DataFrame(columns=REQUIRED_COLUMNS + ["TransactionID", "YearMonth"])
    return df, blob


def _append_to_master_df(df_master, df_new, blob_client):
    """
    Assign continuous TransactionIDs to df_new (after max in df_master), align date format,
    concat, upload to blob. Returns updated master DataFrame.
    """
    last_id = 0
    if not df_master.empty and "TransactionID" in df_master.columns:
        last_id = int(pd.to_numeric(df_master["TransactionID"], errors="coerce").max() or 0)
    df_new = df_new.copy()
    df_new["TransactionID"] = list(range(last_id + 1, last_id + 1 + len(df_new)))
    df_new = _align_date_format_to_master(df_new, df_master)
    # Align columns: master columns first (fill missing in df_new with None), then any extra in df_new
    for c in df_master.columns:
        if c not in df_new.columns:
            df_new[c] = None
    cols = list(df_master.columns) + [c for c in df_new.columns if c not in df_master.columns]
    df_new = df_new[[c for c in cols if c in df_new.columns]]
    df_combined = pd.concat([df_master, df_new], ignore_index=True)
    buf = io.BytesIO()
    with pd.ExcelWriter(buf, engine="openpyxl", mode="w") as writer:
        df_combined.to_excel(writer, sheet_name=MASTER_SHEET, index=False)
    buf.seek(0)
    blob_client.upload_blob(buf.read(), overwrite=True)
    return df_combined


def append_to_master(client_id, validated_blob_path):
    """
    Download one validated Excel blob, normalize, append to master with continuous
    TransactionIDs and same date format as master. Upload master back.
    """
    blob = container.get_blob_client(validated_blob_path)
    data = blob.download_blob().readall()
    df_new = pd.read_excel(io.BytesIO(data))
    df_new = _normalize_new_data(df_new)
    df_master, master_blob = _load_master(client_id)
    _append_to_master_df(df_master, df_new, master_blob)
    print("Master dataset updated successfully")


def append_from_monthly_folders(client_id):
    """
    Check validated/yyyy-mm/ for new months with Excel sheets. For each new month,
    append all sheets' data to master with continuous TransactionIDs and same date
    format as master; then mark that month as processed.
    """
    if container is None:
        print("Monthly append skipped: Azure storage not configured", file=sys.stderr)
        return
    processed = get_processed_months(client_id)
    monthly = list_monthly_validated_excels(client_id)
    if not monthly:
        print("No monthly validated Excel files found under validated/yyyy-mm/")
        return
    # Group by month (yyyy_mm) and process each month once
    by_month = {}
    for yyyy_mm, blob_path in monthly:
        if yyyy_mm not in by_month:
            by_month[yyyy_mm] = []
        by_month[yyyy_mm].append(blob_path)
    df_master, master_blob = _load_master(client_id)
    appended_any = False
    for yyyy_mm in sorted(by_month.keys()):
        if yyyy_mm in processed:
            continue
        paths = by_month[yyyy_mm]
        month_appended = False
        for blob_path in paths:
            try:
                blob = container.get_blob_client(blob_path)
                data = blob.download_blob().readall()
                df_new = pd.read_excel(io.BytesIO(data))
                df_new = _normalize_new_data(df_new)
                df_master = _append_to_master_df(df_master, df_new, master_blob)
                month_appended = True
                appended_any = True
                print(f"Appended {blob_path} to master")
            except Exception as e:
                print(f"Skip {blob_path}: {e}", file=sys.stderr)
        if month_appended:
            add_processed_month(client_id, yyyy_mm)
    if appended_any:
        print("Master dataset updated from monthly folders")
    else:
        print("No new months to append (all already processed)")


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python append_master.py <client_id> [validated_blob_path | --monthly]", file=sys.stderr)
        sys.exit(2)
    client_id = sys.argv[1].strip()
    if len(sys.argv) >= 3 and sys.argv[2].strip() == "--monthly":
        append_from_monthly_folders(client_id)
        sys.exit(0)
    if len(sys.argv) >= 3:
        validated_blob = sys.argv[2].strip()
    else:
        validated_blob = get_latest_validated(client_id)
        if not validated_blob:
            print("No validated file found", file=sys.stderr)
            sys.exit(1)
    append_to_master(client_id, validated_blob)
