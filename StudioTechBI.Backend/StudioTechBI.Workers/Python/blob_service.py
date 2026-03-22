from azure.storage.blob import BlobServiceClient
import os
from datetime import datetime, timezone
from dotenv import load_dotenv

# Load .env from the same folder as this file (Python worker folder), not cwd
_this_dir = os.path.dirname(os.path.abspath(__file__))
load_dotenv(os.path.join(_this_dir, ".env"))

connection_string = os.getenv("AZURE_STORAGE_CONNECTION_STRING")
container_name = (os.getenv("CONTAINER_NAME") or "clients").strip()
# Blob path prefix. "clients" is the container; blob names inside it are AU-001/..., so no prefix.
blob_path_prefix = (os.getenv("BLOB_PATH_PREFIX") or "").strip().rstrip("/")
if blob_path_prefix:
    blob_path_prefix = blob_path_prefix + "/"
# When container IS "clients", blob names are AU-001/... (no "clients/" in the path)
if container_name and container_name.strip().lower() == "clients":
    blob_path_prefix = ""

_storage_init_error = None
try:
    if connection_string and container_name:
        blob_service = BlobServiceClient.from_connection_string(connection_string)
        container = blob_service.get_container_client(container_name)
    else:
        blob_service = None
        container = None
        if not connection_string:
            _storage_init_error = "AZURE_STORAGE_CONNECTION_STRING is missing or empty in .env"
        else:
            _storage_init_error = "CONTAINER_NAME is missing or empty in .env"
except Exception as e:
    blob_service = None
    container = None
    _storage_init_error = f"Azure storage init failed: {e}"


def get_upload_files(client_id):
    """List .xlsx blob names under client uploads folder (for worker_orchestrator)."""
    if container is None:
        return []
    folder = _client_folder(client_id)
    path = f"{blob_path_prefix}{folder}/accounting/uploads/"
    try:
        blobs = container.list_blobs(name_starts_with=path)
        return [blob.name for blob in blobs if blob.name.endswith(".xlsx")]
    except Exception:
        return []


def get_latest_upload(client_id):
    if container is None:
        return None
    folder = _client_folder(client_id)
    path = f"{blob_path_prefix}{folder}/accounting/uploads/"
    blobs = container.list_blobs(name_starts_with=path)
    files = [blob.name for blob in blobs if blob.name.endswith(".xlsx")]
    return sorted(files)[-1] if files else None


def move_to_validated(blob_name):
    if container is None:
        raise RuntimeError("Azure Blob storage not configured (AZURE_STORAGE_CONNECTION_STRING, CONTAINER_NAME).")
    source = container.get_blob_client(blob_name)

    data = source.download_blob().readall()

    destination = blob_name.replace("uploads", "validated")

    dest_blob = container.get_blob_client(destination)

    dest_blob.upload_blob(data, overwrite=True)

    source.delete_blob()

    return destination


# --- Master file and last-refresh tracking (for report refresh) ---
# Container = clients; per-client folder = AU-001, AU-002, AU-003, etc.
# Each client folder contains the master Excel with name Accounting_Master_au<nnn>.xlsx
# (e.g. AU-001/.../Accounting_Master_au001.xlsx, AU-003/.../Accounting_Master_au003.xlsx).
# Master lives inside validated/ alongside monthly folders (2025-04, 2025-05, etc.).

def _client_folder(client_id):
    """Folder name from clientId (AU-001, AU-002, ...). Normalize AU001 -> AU-001."""
    c = (client_id or "").strip()
    if c.upper().startswith("AU") and len(c) == 5 and "-" not in c:
        return f"AU-{c[-3:]}"
    return c


def _master_filename(client_id):
    """Master Excel filename: Accounting_Master_au001.xlsx"""
    base = (client_id or "").strip().lower().replace("-", "")
    return f"Accounting_Master_{base}.xlsx"


def get_master_path(client_id):
    """Master file path inside validated folder for this client. E.g. AU-001/accounting/validated/Accounting_Master_au001.xlsx"""
    folder = _client_folder(client_id)
    filename = _master_filename(client_id)
    return f"{blob_path_prefix}{folder}/accounting/validated/{filename}"


def _download_blob_by_path(path):
    """Return (bytes, None) if path exists, else (None, error_message). Azure blob names are case-sensitive."""
    if container is None:
        msg = _storage_init_error or "Azure storage not configured (check AZURE_STORAGE_CONNECTION_STRING, CONTAINER_NAME)"
        return None, msg
    try:
        return container.get_blob_client(path).download_blob().readall(), None
    except Exception as e:
        return None, str(e)


def list_validated_blob_names(client_id):
    """List blob names under this client's accounting/validated/ folder (for diagnostics)."""
    if container is None:
        return []
    folder = _client_folder(client_id)
    prefix = f"{blob_path_prefix}{folder}/accounting/validated/"
    try:
        return [b.name for b in container.list_blobs(name_starts_with=prefix)]
    except Exception:
        return []


def download_master_bytes(client_id):
    """Download master Excel file as bytes, or None if not found. Tries exact path, alternate casing, then discovery."""
    if container is None:
        _last_master_diagnostic["error"] = _storage_init_error or "Azure storage not configured (check AZURE_STORAGE_CONNECTION_STRING, CONTAINER_NAME)"
        _last_master_diagnostic["blob_names"] = None
        return None
    folder = _client_folder(client_id)
    base = (client_id or "").strip().lower().replace("-", "")

    # 1) Exact path: .../accounting/validated/accounting_master_au001.xlsx
    path = get_master_path(client_id)
    data, err = _download_blob_by_path(path)
    if data is not None:
        return data
    first_error = err

    # 2) Alternate casing (Azure is case-sensitive): Accounting_Master_au001.xlsx
    alt_filename = f"Accounting_Master_{base}.xlsx"
    alt_path = f"{blob_path_prefix}{folder}/accounting/validated/{alt_filename}"
    data, _ = _download_blob_by_path(alt_path)
    if data is not None:
        _last_master_diagnostic["error"] = None
        _last_master_diagnostic["blob_names"] = None
        return data

    # 3) Discovery: list validated folder for this client, any *master*.xlsx
    folder_lower = folder.lower()
    for prefix in (
        f"{blob_path_prefix}{folder}/accounting/validated/",
        f"{folder}/accounting/validated/",
        f"{blob_path_prefix}{folder_lower}/accounting/validated/",
        f"{folder_lower}/accounting/validated/",
    ):
        try:
            blobs = [b for b in container.list_blobs(name_starts_with=prefix)
                     if b.name.endswith(".xlsx") and "master" in b.name.lower()]
            if not blobs:
                continue
            _min = datetime.min.replace(tzinfo=timezone.utc)
            latest = max(blobs, key=lambda b: getattr(b, "last_modified", None) or _min)
            data, _ = _download_blob_by_path(latest.name)
            if data is not None:
                _last_master_diagnostic["error"] = None
                _last_master_diagnostic["blob_names"] = None
                return data
        except Exception:
            continue
    return None


# Set when download_master_bytes returns None, for diagnostics in refresh script.
_last_master_diagnostic = {"error": None, "blob_names": None}


def get_last_master_diagnostic(client_id):
    """After download_master_bytes returned None: (first_error_message, list of blob names in validated folder)."""
    if _last_master_diagnostic["blob_names"] is None and client_id:
        _last_master_diagnostic["blob_names"] = list_validated_blob_names(client_id)
    return _last_master_diagnostic["error"], _last_master_diagnostic["blob_names"]


def get_last_refresh_blob_path(client_id):
    """Blob name for last dataset refresh timestamp."""
    folder = _client_folder(client_id)
    return f"{blob_path_prefix}{folder}/accounting/.last_dataset_refresh"


# --- Monthly validated folders: validated/yyyy-mm/*.xlsx ---

def _validated_base_prefix(client_id):
    """Prefix for validated folder: clients/AU-001/accounting/validated/"""
    folder = _client_folder(client_id)
    return f"{blob_path_prefix}{folder}/accounting/validated/"


def list_monthly_validated_excels(client_id):
    """
    List Excel blobs in monthly subfolders validated/yyyy-mm/.
    Returns list of (yyyy_mm, blob_path) for each .xlsx under validated/YYYY-MM/
    (excludes master file by name).
    Tries multiple prefix variants; also detects yyyy-mm anywhere in the blob path.
    """
    if container is None:
        return []
    import re
    folder = _client_folder(client_id)
    folder_lower = folder.lower()
    month_pattern = re.compile(r"(\d{4}-\d{2})")
    # Try several prefixes (with/without blob_path_prefix, folder case variants)
    prefixes = [
        f"{blob_path_prefix}{folder}/accounting/validated/",
        f"{folder}/accounting/validated/",
        f"{blob_path_prefix}{folder_lower}/accounting/validated/",
        f"{folder_lower}/accounting/validated/",
    ]
    blobs = []
    seen_names = set()
    for prefix in prefixes:
        try:
            for b in container.list_blobs(name_starts_with=prefix):
                if b.name not in seen_names:
                    seen_names.add(b.name)
                    blobs.append(b)
        except Exception:
            continue
    result = []
    for b in blobs:
        if not b.name.endswith(".xlsx"):
            continue
        name = b.name
        if "master" in name.lower():
            continue
        # Include any .xlsx in month folder (e.g. Accounting_Template_Starter_June.xlsx)
        # Extract yyyy-mm from path: e.g. .../validated/2025-06/Book1.xlsx -> 2025-06
        # Method 1: segment after validated/
        yyyy_mm = None
        if "validated" in name.lower():
            after = name.lower().split("validated")[-1].strip("/\\")
            parts = re.split(r"[/\\]", after)
            if len(parts) >= 1 and re.match(r"^\d{4}-\d{2}$", parts[0]):
                yyyy_mm = parts[0]
        # Method 2: any path segment matching yyyy-mm (e.g. 2025-06)
        if not yyyy_mm:
            matches = month_pattern.findall(name)
            if matches:
                yyyy_mm = matches[0]
        if yyyy_mm:
            result.append((yyyy_mm, b.name))
    return result


def get_processed_months_path(client_id):
    """Blob path for tracking which monthly folders have been appended to master."""
    folder = _client_folder(client_id)
    return f"{blob_path_prefix}{folder}/accounting/.processed_monthly"


def get_processed_months(client_id):
    """Return set of yyyy-mm strings that have already been appended to master."""
    if container is None:
        return set()
    path = get_processed_months_path(client_id)
    blob = container.get_blob_client(path)
    try:
        data = blob.download_blob().readall().decode("utf-8").strip()
        return set(line.strip() for line in data.splitlines() if line.strip())
    except Exception:
        return set()


def add_processed_month(client_id, yyyy_mm):
    """Record that this month's data has been appended to master."""
    if container is None:
        raise RuntimeError("Azure Blob storage not configured.")
    path = get_processed_months_path(client_id)
    blob = container.get_blob_client(path)
    processed = get_processed_months(client_id)
    processed.add(yyyy_mm.strip())
    blob.upload_blob("\n".join(sorted(processed)).encode("utf-8"), overwrite=True)


def get_latest_validated(client_id):
    """
    Return blob path of latest .xlsx under validated/ (any path), excluding master file.
    For backward compatibility when not using monthly folders.
    """
    if container is None:
        return None
    prefix = _validated_base_prefix(client_id)
    master_name_lower = _master_filename(client_id).lower()
    try:
        blobs = [b for b in container.list_blobs(name_starts_with=prefix)
                 if b.name.endswith(".xlsx") and "master" not in b.name.lower()]
    except Exception:
        return None
    if not blobs:
        return None
    _min = datetime.min.replace(tzinfo=timezone.utc)
    latest = max(blobs, key=lambda b: getattr(b, "last_modified", None) or _min)
    return latest.name


def _find_master_blob_modified(prefix):
    """List blobs under prefix, return last_modified of newest *Master*.xlsx or None."""
    try:
        blobs = list(container.list_blobs(name_starts_with=prefix))
        master_blobs = [
            b for b in blobs
            if b.name.endswith(".xlsx") and "master" in b.name.lower()
        ]
        if not master_blobs:
            return None
        _min = datetime.min.replace(tzinfo=timezone.utc)
        latest = max(
            master_blobs,
            key=lambda b: getattr(b, "last_modified", None) or _min
        )
        return getattr(latest, "last_modified", None)
    except Exception:
        return None


def get_master_last_modified(client_id):
    """Return last modified datetime of master file, or None if not found.
    Tries exact path, then discovers *Master*.xlsx under validated (with/without clients/ prefix)."""
    if container is None:
        return None
    folder = _client_folder(client_id)
    # 1) Try exact path
    path = get_master_path(client_id)
    blob_client = container.get_blob_client(path)
    try:
        props = blob_client.get_blob_properties()
        return props.last_modified
    except Exception:
        pass
    # 2) Discover: list under validated folder (with BLOB_PATH_PREFIX if set)
    prefix = f"{blob_path_prefix}{folder}/accounting/validated/"
    result = _find_master_blob_modified(prefix)
    if result is not None:
        return result
    # 3) Fallback: try without blob_path_prefix (e.g. if prefix was wrong)
    if blob_path_prefix:
        result = _find_master_blob_modified(f"{folder}/accounting/validated/")
    return result


def get_last_refresh_time(client_id):
    """Return last dataset refresh datetime for this client, or None."""
    if container is None:
        return None
    path = get_last_refresh_blob_path(client_id)
    blob = container.get_blob_client(path)
    try:
        data = blob.download_blob().readall().decode("utf-8").strip()
        from datetime import datetime
        return datetime.fromisoformat(data)
    except Exception:
        return None


def set_last_refresh_time(client_id, timestamp):
    """Store the last dataset refresh timestamp for this client."""
    if container is None:
        raise RuntimeError("Azure Blob storage not configured (AZURE_STORAGE_CONNECTION_STRING, CONTAINER_NAME).")
    path = get_last_refresh_blob_path(client_id)
    blob = container.get_blob_client(path)
    blob.upload_blob(timestamp.strftime("%Y-%m-%dT%H:%M:%S.%fZ").encode("utf-8"), overwrite=True)