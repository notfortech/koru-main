#!/usr/bin/env bash
# Uploads the HTML report template library from this repo's authoring tree
# (DashboardTemplateLibrary/templates/html/) to the real runtime location in Azure Blob Storage:
# the "clients" container, under the templates/html/ prefix -- the same location Power BI report
# templates already live in (see BlobStorageService.UploadTemplateAsync).
#
# This is a one-time correction plus the ongoing rollout mechanism for this template library:
# retail-single-page and healthcare-fpna-multi-tab were originally uploaded, by hand, to the
# wrong container (report-templates) with no manifest.json/index.json, so
# HtmlTemplateRegistrySyncService never found them and HtmlReportAssemblyService fell back to the
# embedded copies baked into HtmlTemplateSeedCatalog.cs. Running this script uploads the correct,
# already-onboarded files to the location the runtime actually reads from -- once this succeeds,
# both templates resolve from blob (HtmlReportAssemblyService now tries blob first, seed only as
# a fallback -- see HtmlReportAssemblyService.cs's AssembleAsync).
#
# Usage:
#   AZURE_STORAGE_CONNECTION_STRING="<same value as AzureBlob:ConnectionString in appsettings>" \
#     ./upload-html-templates.sh
#
# Requires the Azure CLI (az) with the storage extension available.

set -euo pipefail

CONTAINER="clients"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HTML_DIR="${SCRIPT_DIR}/../templates/html"

if [ -z "${AZURE_STORAGE_CONNECTION_STRING:-}" ]; then
  echo "AZURE_STORAGE_CONNECTION_STRING must be set (same value as AzureBlob:ConnectionString)." >&2
  exit 1
fi

echo "Uploading templates/html/index.json ..."
az storage blob upload \
  --connection-string "$AZURE_STORAGE_CONNECTION_STRING" \
  --container-name "$CONTAINER" \
  --file "${HTML_DIR}/index.json" \
  --name "templates/html/index.json" \
  --content-type "application/json" \
  --overwrite

for template_dir in "${HTML_DIR}"/*/; do
  template_id="$(basename "$template_dir")"
  echo "Uploading templates/html/${template_id}/manifest.json ..."
  az storage blob upload \
    --connection-string "$AZURE_STORAGE_CONNECTION_STRING" \
    --container-name "$CONTAINER" \
    --file "${template_dir}manifest.json" \
    --name "templates/html/${template_id}/manifest.json" \
    --content-type "application/json" \
    --overwrite

  echo "Uploading templates/html/${template_id}/chrome.html ..."
  az storage blob upload \
    --connection-string "$AZURE_STORAGE_CONNECTION_STRING" \
    --container-name "$CONTAINER" \
    --file "${template_dir}chrome.html" \
    --name "templates/html/${template_id}/chrome.html" \
    --content-type "text/html" \
    --overwrite
done

echo "Done. HtmlTemplateRegistrySyncService picks these up within 5 minutes (or immediately on next app start)."
echo "Verify via the app logs: look for 'HtmlTemplateRegistrySync.Pushed ... BlobResolved=<N>' where N matches the number of template folders uploaded."
