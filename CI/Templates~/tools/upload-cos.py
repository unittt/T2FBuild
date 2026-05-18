#!/usr/bin/env python3
"""
Upload Asset Bundle output to Tencent COS based on upload-manifest.json.

Required environment variables:
    TENCENT_SECRET_ID
    TENCENT_SECRET_KEY
    COS_BUCKET      e.g. mybucket-1234567890
    COS_REGION      e.g. ap-shanghai

Optional:
    COS_CONCURRENCY   default 8

Usage:
    python upload-cos.py path/to/upload-manifest.json [--dry-run] [--concurrency N]
"""
import argparse
import concurrent.futures
import json
import os
import sys
import threading

try:
    from qcloud_cos import CosConfig, CosS3Client
    from qcloud_cos.cos_exception import CosClientError, CosServiceError
except ImportError:
    sys.stderr.write(
        "ERROR: cos-python-sdk-v5 not installed. "
        "Run: pip install -r tools/requirements.txt\n"
    )
    sys.exit(2)


def env(name, default=None):
    v = os.environ.get(name)
    return v if v is not None and v != "" else default


def main():
    parser = argparse.ArgumentParser(
        description=__doc__,
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    parser.add_argument("manifest", help="Path to upload-manifest.json")
    parser.add_argument("--dry-run", action="store_true", help="Print operations without uploading")
    parser.add_argument(
        "--concurrency",
        type=int,
        default=int(env("COS_CONCURRENCY", "8")),
        help="Concurrent upload threads (default 8 / COS_CONCURRENCY)",
    )
    args = parser.parse_args()

    secret_id = env("TENCENT_SECRET_ID")
    secret_key = env("TENCENT_SECRET_KEY")
    bucket = env("COS_BUCKET")
    region = env("COS_REGION")

    missing = [
        n for n, v in [
            ("TENCENT_SECRET_ID", secret_id),
            ("TENCENT_SECRET_KEY", secret_key),
            ("COS_BUCKET", bucket),
            ("COS_REGION", region),
        ] if not v
    ]
    if missing:
        sys.stderr.write(f"ERROR: missing required env vars: {', '.join(missing)}\n")
        sys.exit(2)

    try:
        with open(args.manifest, "r", encoding="utf-8") as f:
            manifest = json.load(f)
    except (OSError, json.JSONDecodeError) as e:
        sys.stderr.write(f"ERROR: failed to read manifest '{args.manifest}': {e}\n")
        sys.exit(2)

    local_dir = manifest["localDirectory"]
    remote_prefix = manifest["remotePrefix"]
    files = manifest["files"]

    print(f"Provider:    {manifest.get('provider')}")
    print(f"Target:      {manifest.get('target')}")
    print(f"Version:     {manifest.get('buildVersion')}")
    print(f"Env:         {manifest.get('buildEnv')}")
    print(f"Local:       {local_dir}")
    print(f"Remote:      cos://{bucket}/{remote_prefix} ({region})")
    print(f"Files:       {len(files)}")
    print(f"Concurrency: {args.concurrency}")
    if args.dry_run:
        print("Mode:        DRY-RUN")
    print()

    client = CosS3Client(CosConfig(Region=region, SecretId=secret_id, SecretKey=secret_key))

    state = {"done": 0, "bytes": 0}
    state_lock = threading.Lock()
    errors = []
    errors_lock = threading.Lock()
    total = len(files)

    def upload_one(entry):
        rel = entry["relativePath"]
        local_path = os.path.join(local_dir, rel)
        remote_key = remote_prefix + rel
        try:
            if not args.dry_run:
                client.upload_file(
                    Bucket=bucket,
                    LocalFilePath=local_path,
                    Key=remote_key,
                    EnableMD5=False,
                    PartSize=10,
                    MAXThread=4,
                )
            with state_lock:
                state["done"] += 1
                state["bytes"] += entry["size"]
                done = state["done"]
            print(f"[{done}/{total}] OK  {rel} ({entry['size']} bytes)")
        except (CosClientError, CosServiceError, OSError) as e:
            with errors_lock:
                errors.append((rel, str(e)))
            print(f"ERROR  {rel}  {e}", file=sys.stderr)

    with concurrent.futures.ThreadPoolExecutor(max_workers=args.concurrency) as pool:
        list(pool.map(upload_one, files))

    print()
    print(f"Uploaded {state['done']}/{total} files, {state['bytes']} bytes total.")
    if errors:
        print(f"FAILED: {len(errors)} files", file=sys.stderr)
        sys.exit(1)
    print("Upload complete.")


if __name__ == "__main__":
    main()
