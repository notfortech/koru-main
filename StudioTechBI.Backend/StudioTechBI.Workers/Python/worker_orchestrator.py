import sys
from datetime import datetime, timezone
from blob_service import get_upload_files, move_to_validated, set_last_refresh_time
from append_master import append_to_master, append_from_monthly_folders
from powerbi_refresh import refresh_dataset

client_id = sys.argv[1]

print(f"Processing uploads for {client_id}")

# 1) Append from monthly folders validated/yyyy-mm/*.xlsx (new months only)
append_from_monthly_folders(client_id)

# 2) Process any files in uploads: move to validated, append to master
files = get_upload_files(client_id)
for file in files:
    print(f"Moving and appending {file}")
    validated_path = move_to_validated(file)
    append_to_master(client_id, validated_path)

print("Refreshing Power BI dataset")
refresh_dataset(client_id)
set_last_refresh_time(client_id, datetime.now(timezone.utc))
print("Pipeline complete")