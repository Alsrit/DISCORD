#!/usr/bin/env python3
from __future__ import annotations

import argparse
import posixpath
import shlex
from pathlib import Path

import paramiko


SELECTED_PATHS = [
    ".config",
    "Directory.Build.props",
    "global.json",
    "SecureLicensePlatform.sln",
    "README.md",
    "deploy/examples/server.translation.sample.json",
    "src/Platform.Domain",
    "src/Platform.Application",
    "src/Platform.Infrastructure",
    "src/Platform.Api",
    "src/Platform.Admin",
    "src/Platform.Worker",
]

EXCLUDE_DIRS = {"bin", "obj", ".git", ".vs"}
EXCLUDE_SUFFIXES = {".user", ".suo"}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Deploy Secure License Platform server projects to Ubuntu host.")
    parser.add_argument("--host", required=True)
    parser.add_argument("--username", default="root")
    parser.add_argument("--password", required=True)
    parser.add_argument("--remote-root", default="/opt/secure-license-platform")
    parser.add_argument("--local-root", default=str(Path(__file__).resolve().parents[1]))
    parser.add_argument("--skip-health-checks", action="store_true")
    return parser.parse_args()


def ensure_remote_dir(sftp: paramiko.SFTPClient, path: str) -> None:
    parts: list[str] = []
    current = path
    while current not in ("", "/"):
        parts.append(current)
        current = posixpath.dirname(current)

    for directory in reversed(parts):
        try:
            sftp.stat(directory)
        except FileNotFoundError:
            sftp.mkdir(directory)


def upload_tree(sftp: paramiko.SFTPClient, local_root: Path, remote_root: str) -> int:
    uploaded = 0
    for relative in SELECTED_PATHS:
        local_path = local_root / relative
        remote_path = posixpath.join(remote_root, relative.replace("\\", "/"))

        if local_path.is_file():
            ensure_remote_dir(sftp, posixpath.dirname(remote_path))
            sftp.put(str(local_path), remote_path)
            uploaded += 1
            print(f"Uploaded file: {relative}")
            continue

        for item in local_path.rglob("*"):
            if any(part in EXCLUDE_DIRS for part in item.parts):
                continue
            if item.is_dir() or item.suffix.lower() in EXCLUDE_SUFFIXES:
                continue

            relative_item = item.relative_to(local_root).as_posix()
            ensure_remote_dir(sftp, posixpath.dirname(posixpath.join(remote_root, relative_item)))
            sftp.put(str(item), posixpath.join(remote_root, relative_item))
            uploaded += 1
            if uploaded % 25 == 0:
                print(f"Uploaded {uploaded} files...")

    return uploaded


def read_remote_text(sftp: paramiko.SFTPClient, path: str) -> str:
    with sftp.open(path, "r") as handle:
        return handle.read().decode("utf-8")


def write_remote_text(sftp: paramiko.SFTPClient, path: str, content: str) -> None:
    ensure_remote_dir(sftp, posixpath.dirname(path))
    with sftp.open(path, "w") as handle:
        handle.write(content)


def upsert_env_file(sftp: paramiko.SFTPClient, path: str, updates: dict[str, str]) -> None:
    raw = read_remote_text(sftp, path)
    lines = raw.splitlines()
    existing: dict[str, str] = {}
    order: list[tuple[str, str | None]] = []

    for line in lines:
        stripped = line.strip()
        if not stripped or stripped.startswith("#") or "=" not in line:
            order.append((line, None))
            continue

        key, value = line.split("=", 1)
        existing[key] = value
        order.append((key, "kv"))

    for key, value in updates.items():
        existing[key] = value
        if not any(entry[0] == key and entry[1] == "kv" for entry in order):
            order.append((key, "kv"))

    rendered: list[str] = []
    for value, kind in order:
        if kind == "kv":
            rendered.append(f"{value}={existing[value]}")
        else:
            rendered.append(value)

    write_remote_text(sftp, path, "\n".join(rendered) + "\n")
    print(f"Updated env: {path}")


def parse_env(text: str) -> dict[str, str]:
    result: dict[str, str] = {}
    for line in text.splitlines():
        stripped = line.strip()
        if not stripped or stripped.startswith("#") or "=" not in line:
            continue

        key, value = line.split("=", 1)
        result[key] = value

    return result


def run(ssh: paramiko.SSHClient, command: str, *, check: bool = True) -> tuple[int, str, str]:
    print(f"RUN: {command}")
    stdin, stdout, stderr = ssh.exec_command(command, get_pty=True, timeout=1800)
    out = stdout.read().decode("utf-8", errors="replace")
    err = stderr.read().decode("utf-8", errors="replace")
    status = stdout.channel.recv_exit_status()
    if out.strip():
        print(out)
    if err.strip():
        print(err)
    if check and status != 0:
        raise RuntimeError(f"Command failed with exit code {status}: {command}")
    return status, out, err


def main() -> None:
    args = parse_args()
    local_root = Path(args.local_root).resolve()

    ssh = paramiko.SSHClient()
    ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    ssh.connect(args.host, username=args.username, password=args.password, timeout=30)
    sftp = ssh.open_sftp()

    try:
        uploaded = upload_tree(sftp, local_root, args.remote_root)
        print(f"Total uploaded files: {uploaded}")

        env_updates = {
            "Storage__TranslationStorageRoot": "/opt/secure-license-platform/storage/translations",
            "Storage__TranslationTempRoot": "/opt/secure-license-platform/storage/translations-temp",
            "TranslationProviders__Yandex__Enabled": "false",
            "TranslationProviders__Yandex__ApiKeyEnvVar": "YANDEX_TRANSLATE_API_KEY",
        }

        for env_path in (
            "/etc/secure-platform/api.env",
            "/etc/secure-platform/admin.env",
            "/etc/secure-platform/worker.env",
        ):
            upsert_env_file(sftp, env_path, env_updates)

        api_env = parse_env(read_remote_text(sftp, "/etc/secure-platform/api.env"))
        env_assignments = " ".join(f"{key}={shlex.quote(value)}" for key, value in api_env.items())

        run(ssh, "mkdir -p /opt/secure-license-platform/storage/translations /opt/secure-license-platform/storage/translations-temp")
        run(ssh, f"cd {args.remote_root} && dotnet tool restore")
        run(
            ssh,
            " ".join(
                [
                    f"cd {args.remote_root} &&",
                    env_assignments,
                    "dotnet ef database update --project src/Platform.Infrastructure/Platform.Infrastructure.csproj",
                    "--startup-project src/Platform.Api/Platform.Api.csproj",
                ]
            ),
        )
        run(ssh, "systemctl stop secure-platform-api secure-platform-admin secure-platform-worker")
        run(ssh, f"cd {args.remote_root} && dotnet publish src/Platform.Api/Platform.Api.csproj -c Release -o publish/api")
        run(ssh, f"cd {args.remote_root} && dotnet publish src/Platform.Admin/Platform.Admin.csproj -c Release -o publish/admin")
        run(ssh, f"cd {args.remote_root} && dotnet publish src/Platform.Worker/Platform.Worker.csproj -c Release -o publish/worker")
        run(ssh, "chown -R secureplatform:secureplatform /opt/secure-license-platform/publish /opt/secure-license-platform/storage")
        run(ssh, "systemctl start secure-platform-api secure-platform-admin secure-platform-worker")
        run(ssh, "systemctl is-active secure-platform-api secure-platform-admin secure-platform-worker")

        if not args.skip_health_checks:
            run(ssh, f"curl -k -I https://{args.host}/health/live")
            run(ssh, f"curl -k -I https://{args.host}/Login")
            run(ssh, f"curl -k https://{args.host}/api/client/v1/system/info")
    finally:
        sftp.close()
        ssh.close()


if __name__ == "__main__":
    main()
