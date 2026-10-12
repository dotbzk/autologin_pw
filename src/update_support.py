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
MANIFEST_ASSET_NAME = "release-manifest.json"
RELEASE_URL = f"https://api.github.com/repos/{REPOSITORY}/releases/latest"
VERSION_PATTERN = re.compile(r"^v?(\d+)\.(\d+)\.(\d+)$")
SHA256_PATTERN = re.compile(r"^sha256:([0-9a-fA-F]{64})$")
MAX_ARCHIVE_SIZE = 1_500_000_000
MAX_UNPACKED_SIZE = 3_000_000_000
UPDATE_NOTICE = "update-notice.json"
REQUIRED_COMPONENTS = {"gameToolsHub", "gameLauncherBot", "pwHub"}


def version_tuple(value):
    match = VERSION_PATTERN.fullmatch(value)
    if not match:
        raise ValueError(f"Invalid version: {value}")
    return tuple(int(part) for part in match.groups())


def installed_version(path):
    with open(path, encoding="utf-8") as version_file:
        return json.load(version_file)["version"]


def validate_release_manifest(data, expected_version=None):
    if not isinstance(data, dict) or data.get("schemaVersion") != 1:
        raise ValueError("Unsupported release manifest schema")
    release_version = str(data.get("releaseVersion") or "")
    version_tuple(release_version)
    if expected_version and release_version != expected_version.lstrip("v"):
        raise ValueError("Release manifest version does not match release tag")
    components = data.get("components")
    if not isinstance(components, dict) or not REQUIRED_COMPONENTS.issubset(components):
        raise ValueError("Release manifest does not contain all required components")
    for component_id in REQUIRED_COMPONENTS:
        component = components[component_id]
        if not isinstance(component, dict):
            raise ValueError(f"Invalid component manifest: {component_id}")
        version_tuple(str(component.get("version") or ""))
        executable = str(component.get("executable") or "")
        path = PurePosixPath(executable)
        if (
            not executable or executable.startswith("/") or "\\" in executable
            or ":" in executable or ".." in path.parts
        ):
            raise ValueError(f"Unsafe component executable: {component_id}")
    return data


def installed_release_manifest(path):
    with open(path, encoding="utf-8") as manifest_file:
        return validate_release_manifest(json.load(manifest_file))


def parse_release(data):
    version = data["tag_name"]
    version_tuple(version)
    assets = data.get("assets") or []
    asset = next((item for item in assets if item.get("name") == ASSET_NAME), None)
    if not asset:
        raise ValueError(f"Release {version} does not contain {ASSET_NAME}")
    digest = SHA256_PATTERN.fullmatch(asset.get("digest") or "")
    if not digest:
        raise ValueError(f"Release {version} has no SHA-256 digest")
    url = asset["browser_download_url"]
    parsed = urlparse(url)
    if parsed.scheme != "https" or parsed.hostname != "github.com":
        raise ValueError("Unexpected release download URL")
    manifest_asset = next(
        (item for item in assets if item.get("name") == MANIFEST_ASSET_NAME), None
    )
    manifest_url = ""
    manifest_sha256 = ""
    if manifest_asset:
        manifest_digest = SHA256_PATTERN.fullmatch(manifest_asset.get("digest") or "")
        if not manifest_digest:
            raise ValueError(f"Release {version} manifest has no SHA-256 digest")
        manifest_url = manifest_asset.get("browser_download_url") or ""
        parsed_manifest_url = urlparse(manifest_url)
        if parsed_manifest_url.scheme != "https" or parsed_manifest_url.hostname != "github.com":
            raise ValueError("Unexpected release manifest URL")
        manifest_sha256 = manifest_digest.group(1).lower()
    return {
        "version": version.lstrip("v"),
        "url": url,
        "sha256": digest.group(1).lower(),
        "notes": (data.get("body") or "").strip()[:12000],
        "manifest_url": manifest_url,
        "manifest_sha256": manifest_sha256,
        "manifest": None,
    }


def download_release_manifest(url, sha256):
    request = Request(url, headers={"User-Agent": "GameToolsHub"})
    with urlopen(request, timeout=20) as response:
        payload = response.read(1024 * 1024 + 1)
    if len(payload) > 1024 * 1024:
        raise ValueError("Release manifest is too large")
    if hashlib.sha256(payload).hexdigest() != sha256:
        raise ValueError("Release manifest SHA-256 does not match GitHub release")
    try:
        return json.loads(payload.decode("utf-8"))
    except (UnicodeError, ValueError) as exc:
        raise ValueError("Release manifest is not valid UTF-8 JSON") from exc


def latest_release():
    request = Request(
        RELEASE_URL,
        headers={"Accept": "application/vnd.github+json", "User-Agent": "GameLauncherBot"},
    )
    try:
        with urlopen(request, timeout=20) as response:
            release = parse_release(json.load(response))
        if release["manifest_url"]:
            manifest = download_release_manifest(
                release["manifest_url"], release["manifest_sha256"]
            )
            release["manifest"] = validate_release_manifest(
                manifest, expected_version=release["version"]
            )
        return release
    except HTTPError as exc:
        if exc.code == 404:
            raise RuntimeError("No published release is available yet") from exc
        raise


def find_update(current_version):
    release = latest_release()
    return release if version_tuple(release["version"]) > version_tuple(current_version) else None


def start_updater(install_dir, release, launch_executable="GameLauncherBot.exe"):
    install_dir = Path(install_dir).resolve()
    source = install_dir / "Updater.exe"
    if not source.is_file():
        raise FileNotFoundError(f"Updater not found: {source}")
    temporary_dir = Path(tempfile.mkdtemp(prefix="GameLauncherBot-update-"))
    updater = temporary_dir / "Updater.exe"
    try:
        shutil.copy2(source, updater)
        with open(temporary_dir / "release-notes.json", "w", encoding="utf-8") as output:
            json.dump({"notes": release.get("notes", "")}, output, ensure_ascii=False)
        subprocess.Popen([
            str(updater), "--install-dir", str(install_dir),
            "--parent-pid", str(os.getpid()),
            "--version", release["version"],
            "--url", release["url"],
            "--sha256", release["sha256"],
            "--launch-executable", launch_executable,
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

    previous_pw_hub = previous / "PwHub"
    staged_pw_hub = staged / "PwHub"
    if previous_pw_hub.is_dir() and staged_pw_hub.is_dir():
        for directory_name in ("Cookies", "Fingerprints", "logs"):
            old_directory = previous_pw_hub / directory_name
            if old_directory.is_dir():
                shutil.copytree(
                    old_directory,
                    staged_pw_hub / directory_name,
                    dirs_exist_ok=True,
                )
        for filename in ("pwhub.db", "pwhub.db-shm", "pwhub.db-wal"):
            old_file = previous_pw_hub / filename
            if old_file.is_file():
                shutil.copy2(old_file, staged_pw_hub / filename)
        for old_file in previous_pw_hub.glob("appsettings*.json"):
            if old_file.is_file():
                shutil.copy2(old_file, staged_pw_hub / old_file.name)


def install_release(archive_path, install_dir, expected_version, launch_app, progress=None,
                    release_notes="", launch_executable="GameLauncherBot.exe"):
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
            "GameToolsHub.exe", "GameLauncherBot.exe", "Updater.exe", "_internal",
            "PwHub/Pw.Hub.exe", "configs/config.ini", "accounts/accounts.ini",
            "version.json", "release-manifest.json",
        )):
            raise ValueError("Update archive is missing application files")
        if installed_version(staged / "version.json") != expected_version:
            raise ValueError("Update archive version does not match release")
        manifest = installed_release_manifest(staged / "release-manifest.json")
        if manifest["releaseVersion"] != expected_version:
            raise ValueError("Update bundle version does not match release")
        for component in manifest["components"].values():
            component_path = PurePosixPath(component["executable"])
            if not (staged / Path(*component_path.parts)).is_file():
                raise ValueError(
                    f"Update component is missing: {component.get('displayName', component_path)}"
                )
        launch_path = PurePosixPath(launch_executable)
        if (
            not launch_executable or launch_executable.startswith("/")
            or "\\" in launch_executable or ":" in launch_executable
            or ".." in launch_path.parts
        ):
            raise ValueError("Unsafe update launch executable")
        if not (staged / Path(*launch_path.parts)).is_file():
            raise ValueError("Update launch executable is missing")
        preserve_user_data(install_dir, staged)
        if not release_notes.strip():
            with open(staged / "version.json", encoding="utf-8") as version_file:
                release_notes = "\n".join(
                    f"• {change}" for change in json.load(version_file).get("changes", [])
                )
        with open(staged / UPDATE_NOTICE, "w", encoding="utf-8") as output:
            json.dump({"version": expected_version, "notes": release_notes[:12000]},
                      output, ensure_ascii=False)

        if progress:
            progress("Replacing application files")
        os.replace(install_dir, backup)
        try:
            os.replace(staged, install_dir)
            launch_app(install_dir / Path(*launch_path.parts))
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
