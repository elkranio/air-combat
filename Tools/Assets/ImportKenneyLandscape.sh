#!/usr/bin/env bash
# One-time import of the selected CC0 assets. Run from Git Bash, outside Play Mode.
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"

nature='Resources/NatureKit'
sky='Resources/Skyboxes'
models="$nature/Models/OBJ format"
dest='Assets/ThirdParty/Kenney'

fail() { printf 'ERROR: %s\n' "$*" >&2; exit 1; }

# The raw packs are tracked in Git; do not remove local edits or extra files.
[[ -d "$nature" && -d "$sky" ]] || fail 'Both original packs must exist.'
[[ -z "$(git status --porcelain --untracked-files=all --ignored -- "$nature" "$sky")" ]] ||
    fail 'Raw packs contain local changes or extra files; inspect before importing.'

sources=()
targets=()
add_file() {
    sources+=("$1")
    targets+=("$2")
}
add_model() {
    local category="$1" name="$2" ext
    for ext in obj mtl; do
        add_file "$models/$name.$ext" "$dest/NatureKit/$category/$name.$ext"
    done
}
for name in tree_default tree_oak tree_detailed tree_pineRoundA tree_pineRoundB tree_pineTallA; do
    add_model Trees "$name"
done
for name in rock_largeA rock_largeB; do add_model Rocks "$name"; done
add_model Bushes plant_bushLarge
add_file "$nature/License.txt" "$dest/NatureKit/License.txt"
add_file "$sky/Skyboxes/skybox-day.png" "$dest/Skyboxes/skybox-day.png"
add_file "$sky/License.txt" "$dest/Skyboxes/License.txt"

# Preflight the entire manifest before writing anything.
for i in "${!sources[@]}"; do
    src="${sources[i]}"
    dst="${targets[i]}"
    [[ -f "$src" && -s "$src" && ! -L "$src" ]] || fail "Missing/invalid source: $src"
    git ls-files --error-unmatch -- "$src" >/dev/null || fail "Untracked source: $src"
    [[ ! -L "$dst" ]] || fail "Destination is a symlink: $dst"
    if [[ -e "$dst" ]]; then
        [[ -f "$dst" ]] && cmp -s -- "$src" "$dst" || fail "Destination differs: $dst"
    fi
done

for i in "${!sources[@]}"; do
    src="${sources[i]}"
    dst="${targets[i]}"
    mkdir -p -- "$(dirname -- "$dst")"
    if [[ ! -e "$dst" ]]; then cp -- "$src" "$dst"; fi
    printf 'Imported %s\n' "$dst"
done

# Verify each copy byte for byte before pruning the original packs.
for i in "${!sources[@]}"; do
    cmp -s -- "${sources[i]}" "${targets[i]}" || fail "Copy check failed: ${targets[i]}"
done
rm -rf -- "$nature" "$sky"
printf '\nVerified %s files. Removed only %s and %s.\n' "${#sources[@]}" "$nature" "$sky"
printf 'Return to Unity for asset import; keep the generated .meta files.\n'
