import pandas as pd
from blob_service import get_latest_upload, move_to_validated, container


REQUIRED_COLUMNS = [

    "TransactionDate",

    "ClientID",

    "DebitAmount",

    "CreditAmount"

]


def validate(client_id):

    blob_name = get_latest_upload(client_id)

    if not blob_name:

        print("No file found")

        return

    blob = container.get_blob_client(blob_name)

    data = blob.download_blob().readall()

    df = pd.read_excel(data)

    for col in REQUIRED_COLUMNS:

        if col not in df.columns:

            raise Exception(f"Missing column {col}")

    validated_path = move_to_validated(blob_name)

    print(validated_path)


if __name__ == "__main__":

    import sys

    client_id = sys.argv[1]

    validate(client_id)