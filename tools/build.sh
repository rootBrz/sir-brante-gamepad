#!/usr/bin/env bash
# Build pipeline: check -> build -> package.
#   tools/build.sh check     run the logic checks (needs only the .NET SDK)
#   tools/build.sh build     compile dist/Brante.Gamepad.dll
#   tools/build.sh package   zip the plugin, README and LICENSE into release/ (builds first)
#   tools/build.sh all       check, build, package (default)
#
# build compiles against the game's own assemblies when references/ and loader/ exist (see README), and
# otherwise against public NuGet packages (PublicReferences=true). Set VERSION=x.y.z to override the csproj version.
set -euo pipefail
cd "$(dirname "$0")/.."

version() {
  if [ -n "${VERSION:-}" ]; then echo "$VERSION"; return; fi
  sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Brante.Gamepad.csproj | head -n 1
}

check() {
  dotnet run --project checks/Checks.csproj
  python3 tools/deck_smoke.py self-check
}

build() {
  local args=(-c Release -o dist -p:Version="$(version)")
  if [ -d references ] && [ -d loader/BepInEx/core ]; then
    echo "Building against the game's assemblies in references/ and loader/"
  else
    echo "references/ or loader/ not found: building against public NuGet packages"
    args+=(-p:PublicReferences=true)
  fi
  rm -rf dist
  dotnet build Brante.Gamepad.csproj "${args[@]}"
}

package() {
  build
  local name="SirBrante.Gamepad-$(version)"
  mkdir -p release
  rm -f "release/$name.zip"
  python3 -I - "$name" <<'PY'
import sys, zipfile
name = sys.argv[1]
with zipfile.ZipFile(f"release/{name}.zip", "w", zipfile.ZIP_DEFLATED) as archive:
    # Same layout as the game folder, so the archive can be extracted over it.
    archive.write("dist/Brante.Gamepad.dll", "BepInEx/plugins/Brante.Gamepad.dll")
    archive.write("README.md", "README.md")
    archive.write("LICENSE", "LICENSE")
print(f"release/{name}.zip")
PY
}

case "${1:-all}" in
  check) check ;;
  build) build ;;
  package) package ;;
  all) check; package ;;
  *) echo "usage: $0 [check|build|package|all]" >&2; exit 2 ;;
esac
