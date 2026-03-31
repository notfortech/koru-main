import os
import sys

import requests
from dotenv import load_dotenv

load_dotenv()

workspace = os.getenv("POWERBI_WORKSPACE_ID")

dataset = os.getenv("POWERBI_DATASET_ID")

tenant = os.getenv("POWERBI_TENANT_ID")

client_id = os.getenv("POWERBI_CLIENT_ID")

client_secret = os.getenv("POWERBI_CLIENT_SECRET")


def get_token():

    url = f"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token"

    data = {

        "grant_type": "client_credentials",

        "client_id": client_id,

        "client_secret": client_secret,

        "scope": "https://analysis.windows.net/powerbi/api/.default"

    }

    res = requests.post(url, data=data)

    return res.json()["access_token"]


def refresh_dataset(client_id=None, workspace_id=None, dataset_id=None):
    """
    Trigger Power BI dataset refresh.
    When workspace_id and dataset_id are provided, use them (per-client dataset).
    Otherwise use POWERBI_WORKSPACE_ID and POWERBI_DATASET_ID from env (single shared dataset).
    """
    ws = (workspace_id or "").strip() or workspace
    ds = (dataset_id or "").strip() or dataset
    if not ws or not ds:
        raise RuntimeError("Power BI workspace and dataset IDs are required (set per-client in DB or in env: POWERBI_WORKSPACE_ID, POWERBI_DATASET_ID)")
    token = get_token()
    url = f"https://api.powerbi.com/v1.0/myorg/groups/{ws}/datasets/{ds}/refreshes"
    headers = {"Authorization": f"Bearer {token}"}
    r = requests.post(url, headers=headers)
    if r.status_code not in (200, 202):
        raise RuntimeError(f"Power BI refresh failed: HTTP {r.status_code}")


def refresh():
    refresh_dataset(client_id=None)


if __name__ == "__main__":
    client_id = sys.argv[1] if len(sys.argv) > 1 else None
    refresh_dataset(client_id)