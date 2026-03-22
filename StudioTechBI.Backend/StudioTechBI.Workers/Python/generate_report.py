import sys
import subprocess

client_id = sys.argv[1]
period = sys.argv[2]

print("Starting report generation")

# Step 1 Validate
subprocess.run(
 [
  "python",
  "validate_excel.py",
  client_id,
  period
 ]
)

# Step 2 Append to master dataset
subprocess.run(
 [
  "python",
  "append_master.py",
  client_id,
  period
 ]
)

# Step 3 Refresh Power BI dataset
subprocess.run(
 [
  "python",
  "powerbi_refresh.py"
 ]
)

print("Report generation completed")