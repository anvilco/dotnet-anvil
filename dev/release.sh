#!/usr/bin/env bash
# Helps release the package. See "Publishing" in CONTRIBUTING.md.
#
#   dev/release.sh prepare <version>
#       Bumps src/Anvil/Anvil.csproj and CHANGELOG.md on a release branch and
#       commits, ready for a PR to main.
#   dev/release.sh tag
#       Tags the current commit on main and pushes the tag, which runs the
#       Publish workflow.
#
# Both accept --dry-run to print what they would do without changing anything.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CSPROJ="$ROOT/src/Anvil/Anvil.csproj"
CHANGELOG="$ROOT/CHANGELOG.md"
WORKFLOW_URL="https://github.com/anvilco/dotnet-anvil/actions/workflows/publish.yml"

DRY_RUN=false
ARGS=()
for arg in "$@"; do
  if [ "$arg" = "--dry-run" ]; then DRY_RUN=true; else ARGS+=("$arg"); fi
done

fail() { echo "Error: $*" >&2; exit 1; }

run() {
  local description="$1"; shift
  if $DRY_RUN; then
    echo "[dry run] $description"
  else
    echo "$description"
    "$@"
  fi
}

git_root() { git -C "$ROOT" "$@"; }

package_version() {
  sed -nE 's/^[[:space:]]*<Version>([^<]+)<\/Version>.*/\1/p' "$CSPROJ" | head -1
}

tag_exists() {
  local tag="$1"
  if [ -n "$(git_root tag --list "$tag")" ]; then echo "locally"; return; fi
  if [ -n "$(git_root ls-remote --tags origin "refs/tags/$tag")" ]; then echo "on origin"; return; fi
}

require_clean_working_tree() {
  if [ -n "$(git_root status --porcelain)" ]; then
    fail "The working tree has uncommitted changes. Commit or stash them first."
  fi
}

is_prerelease() { [[ "$1" == *-* ]]; }

prepare() {
  local version="${1:-}"
  [ -n "$version" ] || fail "Usage: dev/release.sh prepare <version>, e.g. dev/release.sh prepare 1.0.0"
  version="${version#v}"
  if ! [[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-(alpha|beta|rc)\.[0-9]+)?$ ]]; then
    fail "$version is not a release version, like 1.0.0 or 1.0.0-rc.1"
  fi

  local tag="v$version"
  local current
  current="$(package_version)"
  [ -n "$current" ] || fail "Couldn't find <Version> in src/Anvil/Anvil.csproj"
  [ "$version" != "$current" ] || fail "src/Anvil/Anvil.csproj is already at $version"
  local existing
  existing="$(tag_exists "$tag")"
  [ -z "$existing" ] || fail "$tag already exists $existing"

  require_clean_working_tree

  local branch
  branch="$(git_root rev-parse --abbrev-ref HEAD)"
  if [ "$branch" = "main" ]; then
    run "Creating branch release/$tag" git_root checkout -b "release/$tag"
  fi

  bump() {
    sed -i.bak -E "s#<Version>$current</Version>#<Version>$version</Version>#" "$CSPROJ" && rm -f "$CSPROJ.bak"
  }
  run "Bumping src/Anvil/Anvil.csproj from $current to $version" bump

  if is_prerelease "$version"; then
    echo "Leaving CHANGELOG.md as it is, since $tag is a pre-release"
  else
    grep -q '^## \[Unreleased\]$' "$CHANGELOG" || fail 'CHANGELOG.md has no "## [Unreleased]" section to release'
    release_changelog() {
      local today
      today="$(date +%Y-%m-%d)"
      awk -v tag="$tag" -v today="$today" '
        /^## \[Unreleased\]$/ && !done { print; print ""; print "## [" tag "] - " today; done = 1; next }
        { print }
      ' "$CHANGELOG" > "$CHANGELOG.tmp" && mv "$CHANGELOG.tmp" "$CHANGELOG"
    }
    run "Renaming the Unreleased section of CHANGELOG.md to $tag" release_changelog
  fi

  commit() {
    git_root add "$CSPROJ" "$CHANGELOG"
    git_root commit --quiet -m "Release $tag"
  }
  run "Committing \"Release $tag\"" commit

  local check=""
  is_prerelease "$version" || check=" Check the $tag section of CHANGELOG.md reads well."
  cat <<EOF

Next:
  1. Push this branch and open a PR to main.$check
  2. After it merges: git checkout main && git pull && dev/release.sh tag
EOF
}

tag() {
  local branch
  branch="$(git_root rev-parse --abbrev-ref HEAD)"
  [ "$branch" = "main" ] || fail "Tags must be on main, but this is $branch. Run: git checkout main && git pull"
  require_clean_working_tree

  git_root fetch --quiet origin main
  [ "$(git_root rev-parse HEAD)" = "$(git_root rev-parse origin/main)" ] || fail "main is not the same as origin/main. Run: git pull"

  local version tag_name existing
  version="$(package_version)"
  tag_name="v$version"
  existing="$(tag_exists "$tag_name")"
  [ -z "$existing" ] || fail "$tag_name already exists $existing. Bump the version first: dev/release.sh prepare <version>"

  if ! is_prerelease "$version"; then
    grep -q "^## \[$tag_name\]" "$CHANGELOG" || fail "CHANGELOG.md has no $tag_name section. Run: dev/release.sh prepare $version"
  fi

  run "Tagging $(git_root rev-parse --short HEAD) as $tag_name" git_root tag -a "$tag_name" -m "$tag_name"
  run "Pushing $tag_name" git_root push origin "$tag_name"
  echo
  echo "The Publish workflow is running: $WORKFLOW_URL"
  echo "When its build job has finished, approve the publish job on the run's page."
}

case "${ARGS[0]:-}" in
  prepare) prepare "${ARGS[1]:-}" ;;
  tag) tag ;;
  *) fail "Usage: dev/release.sh prepare <version> | tag [--dry-run]" ;;
esac
