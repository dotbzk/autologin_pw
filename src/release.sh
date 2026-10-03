#!/usr/bin/env bash

set -euo pipefail

usage() {
    echo "Usage: $0 [--dry-run]"
    echo "Set PYTHON_BIN to override automatic Python 3 detection (python3, then python)."
}

dry_run=false
case "${1:-}" in
    "") ;;
    --dry-run) dry_run=true ;;
    -h|--help)
        usage
        exit 0
        ;;
    *)
        usage >&2
        exit 2
        ;;
esac

if (( $# > 1 )); then
    usage >&2
    exit 2
fi

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
project_root="$(dirname -- "$script_dir")"
if [[ -n "${PYTHON_BIN:-}" ]]; then
    python_bin="$PYTHON_BIN"
elif command -v python3 >/dev/null 2>&1; then
    python_bin="python3"
elif command -v python >/dev/null 2>&1; then
    python_bin="python"
else
    echo "Python 3 was not found. Install Python 3 or set PYTHON_BIN to its executable path." >&2
    exit 1
fi

if ! command -v "$python_bin" >/dev/null 2>&1; then
    echo "Python executable not found: $python_bin. Check PYTHON_BIN." >&2
    exit 1
fi
if ! "$python_bin" -c 'import sys; sys.exit(0 if sys.version_info[0] == 3 else 1)' >/dev/null 2>&1; then
    echo "Python 3 is required: $python_bin. Set PYTHON_BIN to a working Python 3 executable." >&2
    exit 1
fi

git -C "$project_root" rev-parse --is-inside-work-tree >/dev/null

echo "Fetching origin/main and release tags..."
git -C "$project_root" fetch origin main --tags

main_commit="$(git -C "$project_root" rev-parse origin/main)"
version="$(
    git -C "$project_root" show origin/main:src/version.json |
        "$python_bin" -c 'import json, sys; print(json.load(sys.stdin)["version"])'
)"

if [[ ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo "src/version.json on origin/main must contain a version like 0.1.2." >&2
    exit 1
fi

tag="v$version"

set +e
remote_tag="$(git -C "$project_root" ls-remote --tags --refs origin "refs/tags/$tag")"
remote_tag_status=$?
set -e

if (( remote_tag_status == 0 )) && [[ -n "$remote_tag" ]]; then
    echo "Tag $tag already exists on origin. Increase the version in a PR before the next release." >&2
    exit 1
fi
if (( remote_tag_status != 0 && remote_tag_status != 2 )); then
    echo "Failed to check tag $tag on origin." >&2
    exit "$remote_tag_status"
fi

if git -C "$project_root" show-ref --verify --quiet "refs/tags/$tag"; then
    tag_commit="$(git -C "$project_root" rev-list -n 1 "$tag")"
    if [[ "$tag_commit" != "$main_commit" ]]; then
        echo "Local tag $tag points to $tag_commit, not origin/main ($main_commit)." >&2
        exit 1
    fi
    local_tag_exists=true
else
    local_tag_exists=false
fi

echo "Release: $tag"
echo "Commit:  $main_commit"

if [[ "$dry_run" == true ]]; then
    echo "Dry run: no tag was created or pushed."
    exit 0
fi

remote_main="$(git -C "$project_root" ls-remote origin refs/heads/main | awk '{print $1}')"
if [[ "$remote_main" != "$main_commit" ]]; then
    echo "origin/main changed during preparation. Run the script again." >&2
    exit 1
fi

if [[ "$local_tag_exists" == false ]]; then
    git -C "$project_root" tag -a "$tag" "$main_commit" -m "Release $tag"
fi

git -C "$project_root" push origin "refs/tags/$tag:refs/tags/$tag"

repo_url="$(git -C "$project_root" remote get-url origin)"
repo_url="${repo_url%.git}"
if [[ "$repo_url" =~ github\.com[:/]([^/]+/[^/]+)$ ]]; then
    github_repo="${BASH_REMATCH[1]}"
    echo "Actions: https://github.com/$github_repo/actions"
    echo "Release: https://github.com/$github_repo/releases/tag/$tag"
fi

echo "Tag $tag pushed. GitHub Actions will build and publish the release."
