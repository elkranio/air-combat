#!/usr/bin/env bash
# Run in Git Bash, outside Play Mode. Preserve OBJ-relative texture paths.
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"
fail() { printf 'ERROR: %s\n' "$*" >&2; exit 1; }
base='Assets/ThirdParty/Kenney'
kits=(CityKitIndustrial CityKitSuburban)
sources=() targets=()
add_file() { sources+=("$1"); targets+=("$2"); }
for kit in "${kits[@]}"; do
    raw="Resources/$kit"
    [[ -d "$raw" && ! -L "$raw" ]] || fail "Missing pack: $raw"
    [[ -z "$(git status --porcelain --untracked-files=all --ignored -- "$raw")" ]] || fail "Local changes in $raw"
    models="$raw/Models/OBJ format"
    if [[ "$kit" == CityKitIndustrial ]]; then
        names=(building-a building-g building-h building-i building-j detail-tank-large shipping-container-a)
    else
        names=(building-type-a building-type-c building-type-d building-type-f)
    fi
    for name in "${names[@]}"; do
        for ext in obj mtl; do add_file "$models/$name.$ext" "$base/$kit/$name.$ext"; done
    done
    add_file "$models/Textures/colormap.png" "$base/$kit/Textures/colormap.png"
    add_file "$raw/License.txt" "$base/$kit/License.txt"
done
for i in "${!sources[@]}"; do
    src="${sources[i]}"; dst="${targets[i]}"
    [[ -f "$src" && -s "$src" && ! -L "$src" ]] || fail "Missing/invalid source: $src"
    git ls-files --error-unmatch -- "$src" >/dev/null || fail "Untracked source: $src"
    parent="$dst"
    while [[ "$parent" != . ]]; do
        [[ ! -L "$parent" ]] || fail "Symlink: $parent"
        parent="$(dirname -- "$parent")"
    done
    if [[ -e "$dst" ]]; then
        [[ -f "$dst" ]] && cmp -s -- "$src" "$dst" || fail "Destination differs: $dst"
    fi
done
for i in "${!sources[@]}"; do
    src="${sources[i]}"; dst="${targets[i]}"
    mkdir -p -- "$(dirname -- "$dst")"
    [[ -e "$dst" ]] || cp -- "$src" "$dst"
    cmp -s -- "$src" "$dst" || fail "Copy check failed: $dst"
    printf 'Imported %s\n' "$dst"
done
# Recheck before removal in case a file changed during copying.
for kit in "${kits[@]}"; do
    [[ -z "$(git status --porcelain --untracked-files=all --ignored -- "Resources/$kit")" ]] || fail "Pack changed during import: $kit"
done
rm -rf -- Resources/CityKitIndustrial Resources/CityKitSuburban
printf 'Verified %s files. Removed only the two raw city kits.\n' "${#sources[@]}"
printf 'Return to Unity and let import finish before configuring the battlefield.\n'
