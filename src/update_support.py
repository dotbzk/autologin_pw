import configparser
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import subprocess
import tempfile
from urllib.parse import urlparse
from urllib.error import HTTPError
from urllib.request import Request, urlopen
import zipfile


REPOSITORY = "dotbzk/autologin_pw"
ASSET_NAME = "GameLauncherBot-win64.zip"
RELEASE_URL = f"https://api.github.com/repos/{REPOSITORY}/releases/latest"
VERSION_PATTERN = re.compile(r"^v?(\d+)\.(\d+)\.(\d+)$")
SHA256_PATTERN = re.compile(r"^sha256:([0-9a-fA-F]{64})$")
MAX_ARCHIVE_SIZE = 1_500_000_000
MAX_UNPACKED_SIZE = 3_000_000_000


def version_tuple(value):
    match = VERSION_PATTERN.fullmatch(value)
    if not match:
        raise ValueError(f"Invalid version: {value}")
    return tuple(int(part) for part in match.groups())


def installed_version(path):
    with open(path, encoding="utf-8") as version_file:
        return json.load(version_file)["version"]


def parse_release(data):
    version = data["tag_name"]
    version_tuple(version)
    asset = next((item for item in data["assets"] if item["name"] == ASSET_NAME), None)
    if not asset:
        raise ValueError(f"Release {version} does not contain {ASSET_NAME}")
    digest = SHA256_PATTERN.fullmatch(asset.get("digest") or "")
    if not digest:
        raise ValueError(f"Release {version} has no SHA-256 digest")
    url = asset["browser_download_url"]
    parsed = urlparse(url)
    if parsed.scheme != "https" or parsed.hostname != "github.com":
        raise ValueError("Unexpected release download URL")
    return {"version": version.lstrip("v"), "url": url, "sha256": digest.group(1).lower()}


def latest_release():
    request = Request(
        RELEASE_URL,
        headers={"Accept": "application/vnd.github+json", "User-Agent": "GameLauncherBot"},
    )
    try:
        with urlopen(request, timeout=20) as response:
            return parse_release(json.load(response))
    except HTTPError as exc:
        if exc.code == 404:
            raise RuntimeError("No published release is available yet") from exc
        raise


def find_update(current_version):
    release = latest_release()
    return release if version_tuple(release["version"]) > version_tuple(current_version) else None


def start_updater(install_dir, release):
    install_dir = Path(install_dir).resolve()
    source = install_dir / "Updater.exe"
    if not source.is_file():
        raise FileNotFoundError(f"Updater not found: {source}")
    temporary_dir = Path(tempfile.mkdtemp(prefix="GameLauncherBot-update-"))
    updater = temporary_dir / "Updater.exe"
    try:
        shutil.copy2(source, updater)
        subprocess.Popen([
            str(updater), "--install-dir", str(install_dir),
            "--parent-pid", str(os.getpid()),
            "--version", release["version"],
            "--url", release["url"],
            "--sha256", release["sha256"],
        ], cwd=temporary_dir, close_fds=True)
    except Exception:
        shutil.rmtree(temporary_dir, ignore_errors=True)
        raise
    return temporary_dir


def download_release(url, sha256, destination, progress=None):
    request = Request(url, headers={"User-Agent": "GameLauncherBot"})
    digest = hashlib.sha256()
    received = 0
    with urlopen(request, timeout=30) as response, open(destination, "wb") as output:
        while True:
            chunk = response.read(1024 * 1024)
            if not chunk:
                break
            received += len(chunk)
            if received > MAX_ARCHIVE_SIZE:
                raise ValueError("Update archive is too large")
            output.write(chunk)
            digest.update(chunk)
            if progress:
                progress(received, int(response.headers.get("Content-Length") or 0))
    if digest.hexdigest() != sha256:
        raise ValueError("Update archive SHA-256 does not match GitHub release")


def extract_release(archive_path, destination):
    with zipfile.ZipFile(archive_path) as archive:
        entries = archive.infolist()
        if len(entries) > 5000 or sum(item.file_size for item in entries) > MAX_UNPACKED_SIZE:
            raise ValueError("Update archive exceeds size limits")
        for item in entries:
            name = item.filename
            path = PurePosixPath(name)
            if (
                not name or name.startswith("/") or "\\" in name or ":" in name
                or ".." in path.parts or (item.external_attr >> 16) & 0o170000 == 0o120000
            ):
                raise ValueError(f"Unsafe path in update archive: {name}")
        archive.extractall(destination)


def read_ini(path):
    for encoding in ("utf-8", "utf-8-sig", "cp1251"):
        config = configparser.ConfigParser(interpolation=None)
        try:
            with open(path, encoding=encoding) as config_file:
                config.read_file(config_file)
            return config
        except UnicodeError:
            continue
    raise ValueError(f"Cannot read configuration: {path}")


def preserve_user_data(previous, staged):
    previous = Path(previous)
    staged = Path(staged)
    old_config = previous / "configs" / "config.ini"
    new_config = staged / "configs" / "config.ini"
    if old_config.is_file():
        settings = read_ini(new_config)
        user_settings = read_ini(old_config)
        for section in user_settings.sections():
            if not settings.has_section(section):
                settings.add_section(section)
            for key, value in user_settings.items(section):
                settings[section][key] = value
        with open(new_config, "w", encoding="utf-8") as config_file:
            settings.write(config_file)

    old_accounts = previous / "accounts" / "accounts.ini"
    if old_accounts.is_file():
        shutil.copy2(old_accounts, staged / "accounts" / "accounts.ini")
    old_classes = previous / "configs" / "classes"
    if old_classes.is_dir():
        new_classes = staged / "configs" / "classes"
        new_classes.mkdir(parents=True, exist_ok=True)
        for image in old_classes.glob("*.png"):
            if not (new_classes / image.name).exists():
                shutil.copy2(image, new_classes / image.name)
    old_logs = previous / "logs"
    if old_logs.is_dir():
        shutil.copytree(old_logs, staged / "logs", dirs_exist_ok=True)


def install_release(archive_path, install_dir, expected_version, launch_app, progress=None):
    install_dir = Path(install_dir).resolve()
    if not install_dir.is_dir():
        raise ValueError("Installation directory does not exist")
    work = Path(tempfile.mkdtemp(prefix="GameLauncherBot-stage-", dir=install_dir.parent))
    backup = work / "previous"
    installed = False
    try:
        staged = work / "new"
        staged.mkdir()
        extract_release(archive_path, staged)
        if not all((staged / item).exists() for item in (
            "GameLauncherBot.exe", "Updater.exe", "_internal", "configs/config.ini",
            "accounts/accounts.ini", "version.json",
        )):
            raise ValueError("Update archive is missing application files")
        if installed_version(staged / "version.json") != expected_version:
            raise ValueError("Update archive version does not match release")
        preserve_user_data(install_dir, staged)

        if progress:
            progress("Replacing application files")
        os.replace(install_dir, backup)
        try:
            os.replace(staged, install_dir)
            launch_app(install_dir / "GameLauncherBot.exe")
            installed = True
        except Exception:
            try:
                if install_dir.exists():
                    shutil.rmtree(install_dir)
                os.replace(backup, install_dir)
            except Exception as restore_error:
                raise RuntimeError(
                    f"Update failed and rollback failed. Previous version is at {backup}"
                ) from restore_error
            raise
    finally:
        if installed:
            try:
                (work / "completed").touch()
            except OSError:
                pass
        if installed or not backup.exists():
            shutil.rmtree(work, ignore_errors=True)
