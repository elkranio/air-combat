#!/usr/bin/env bash
set -euo pipefail

# C1: Promote the already-green prefab-composed scene; retire old one-shot tools.
# Run from the repository root AFTER `git apply air-combat-c1.patch`, with Unity CLOSED.
readonly EXPECTED_HEAD='e60a5c3ad9445e4753d1a10fbaf4c7fd0759d296'
readonly SCENES='Assets/_Project/Scenes'
readonly ACTIVE_SCENE="$SCENES/WeaponLab.unity"
readonly PREFAB_SCENE="$SCENES/WeaponLabPrefabs.unity"
readonly ARCHIVE='Docs/Archive'
readonly STAGE='Docs/C1_Migration'

fail() { echo "C1 ABORT: $*" >&2; exit 1; }
exists_tracked() { git ls-files --error-unmatch -- "$1" >/dev/null 2>&1; }

[[ -d .git || -f .git ]] || fail 'Run from the air-combat Git repository root.'
[[ "$(git rev-parse --show-toplevel)" == "$(pwd -P)" ]] || fail 'Run from the repository root.'
[[ "$(git rev-parse HEAD)" == "$EXPECTED_HEAD" ]] || fail "Expected green master $EXPECTED_HEAD. Current HEAD is $(git rev-parse HEAD); do not run this cleanup on a different base."
[[ -z "$(git diff --name-only)" && -z "$(git diff --cached --name-only)" ]] || fail 'Tracked files have local modifications or staged changes; commit/stash/revert them first.'
for f in "$ACTIVE_SCENE" "$ACTIVE_SCENE.meta" "$PREFAB_SCENE" "$PREFAB_SCENE.meta" \
  Assets/_Project/Prefabs/Aircraft/PlayerJet.prefab \
  Assets/_Project/Prefabs/Aircraft/EnemyJet.prefab \
  Assets/_Project/Prefabs/Camera/CombatCameraRig.prefab \
  Assets/_Project/Prefabs/Chassis/Kestrel.prefab \
  Assets/_Project/Scripts/Visual/WeaponLabSession.cs \
  Assets/_Project/Scripts/Weapons/WeaponLabCannon.cs \
  Assets/_Project/Scripts/Weapons/WeaponLabCannon.cs.meta \
  Assets/_Project/Scripts/Weapons/WeaponLabTarget.cs \
  Assets/_Project/Scripts/Weapons/WeaponLabTarget.cs.meta \
  Assets/_Project/Editor/AuthorKestrelChassis.cs \
  Assets/_Project/Editor/AuthorJetPrefabs.cs \
  Assets/_Project/Editor/CreateWeaponLabPrefabScene.cs \
  Tools/Unity/ConfigureWeaponLab.cs \
  Tools/Unity/ConfigureWeaponLabAudio.cs \
  Tools/Unity/ConfigureWeaponLabDogfight.cs \
  Tools/Unity/ConfigureWeaponLabEvasion.cs \
  ProjectSettings/EditorBuildSettings.asset README.md GAMEPLAY_ARCHITECTURE.md HANDOFF.md; do
  exists_tracked "$f" || fail "Missing tracked source: $f"
done
for f in README.md GAMEPLAY_ARCHITECTURE.md HANDOFF.md; do
  [[ -f "$STAGE/$f" ]] || fail "Missing C1 replacement document $STAGE/$f; apply the C1 patch first."
done
[[ -f Tools/cleanup-c1.sh ]] || fail 'Missing Tools/cleanup-c1.sh from patch.'
[[ ! -e "$ARCHIVE/README_PRE_C1.md" && ! -e "$ARCHIVE/HANDOFF_PRE_C1.md" ]] || fail 'Archive destination already exists.'

# Build-scene safeguard: cleanup does NOT move or rename Sandbox.
grep -q 'path: Assets/_Project/Scenes/Sandbox.unity' ProjectSettings/EditorBuildSettings.asset || fail 'Build Settings no longer contain Sandbox; inspect manually.'

# No remaining C# code may reference the retired types/tools outside the deletion set.
readonly REMOVED_CS_REGEX='WeaponLabCannon|WeaponLabTarget|ConfigureWeaponLab|AuthorJetPrefabs|AuthorKestrelChassis|CreateWeaponLabPrefabScene'
readonly DISCARDED_REGEX='^(Assets/_Project/Editor/(AuthorJetPrefabs|AuthorKestrelChassis|CreateWeaponLabPrefabScene)\.cs|Assets/_Project/Scripts/Weapons/(WeaponLabCannon|WeaponLabTarget)\.cs|Tools/Unity/ConfigureWeaponLab[^/]*\.cs)$'
references="$(git grep -l -E "$REMOVED_CS_REGEX" -- ':(glob)Assets/**/*.cs' ':(glob)Tools/**/*.cs' | grep -Ev "$DISCARDED_REGEX" || true)"
[[ -z "$references" ]] || fail "Retired types/tools still referenced by live C# files:\n$references"

# Resolve script GUIDs from actual meta files, not guessed GUID constants.
# The *old* WeaponLab scene is the only permitted reference to the retired scripts.
for meta in Assets/_Project/Scripts/Weapons/WeaponLabCannon.cs.meta \
            Assets/_Project/Scripts/Weapons/WeaponLabTarget.cs.meta; do
  guid="$(sed -n 's/^guid: //p' "$meta" | head -n 1)"
  [[ "$guid" =~ ^[0-9a-f]{32}$ ]] || fail "Invalid Unity GUID in $meta"
  asset_refs="$(git grep -l -F "$guid" -- ':(glob)Assets/**/*.unity' ':(glob)Assets/**/*.prefab' ':(glob)Assets/**/*.asset' | grep -v -F -x "$ACTIVE_SCENE" || true)"
  [[ -z "$asset_refs" ]] || fail "Retired script GUID $guid from $meta is still referenced by assets:\n$asset_refs"
done

# Avoid accidental overwriting when a user has an untracked destination.
for f in "$ARCHIVE/GAMEPLAY_ARCHITECTURE_PRE_C1.md" "$ARCHIVE/WEAPON_LAB_PRE_C1.md" \
  "$ARCHIVE/CLEANUP_C1.sh" "$ARCHIVE/README_PRE_C1.md" "$ARCHIVE/HANDOFF_PRE_C1.md"; do
  [[ ! -e "$f" ]] || fail "Destination exists: $f"
done
for f in GAMEPLAY_CORE_A3B.md GAMEPLAY_CORE_A3C.md GAMEPLAY_CORE_A3D.md \
  GAMEPLAY_CORE_A4A.md GAMEPLAY_CORE_A4B.md GAMEPLAY_CORE_A4C.md; do
  exists_tracked "$f" || fail "Missing migration log $f"
  [[ ! -e "$ARCHIVE/$f" ]] || fail "Archive destination exists: $ARCHIVE/$f"
done

printf '%s\n' 'C1 PREFLIGHT OK: no retained code/scene/prefab uses the retired scripts.' \
  'Promoting prefab-composed WeaponLab scene, archiving documentation, removing obsolete files...'
mkdir -p "$ARCHIVE"

# Retain the modern converted scene's GUID by moving its .meta with the scene.
git rm -- "$ACTIVE_SCENE" "$ACTIVE_SCENE.meta"
git mv -- "$PREFAB_SCENE" "$ACTIVE_SCENE"
git mv -- "$PREFAB_SCENE.meta" "$ACTIVE_SCENE.meta"

# Remove editor migration commands that would regenerate assets now owned by Git.
for stem in \
  Assets/_Project/Editor/AuthorKestrelChassis \
  Assets/_Project/Editor/AuthorJetPrefabs \
  Assets/_Project/Editor/CreateWeaponLabPrefabScene \
  Tools/Unity/ConfigureWeaponLab \
  Tools/Unity/ConfigureWeaponLabAudio \
  Tools/Unity/ConfigureWeaponLabDogfight \
  Tools/Unity/ConfigureWeaponLabEvasion \
  Assets/_Project/Scripts/Weapons/WeaponLabCannon \
  Assets/_Project/Scripts/Weapons/WeaponLabTarget; do
  exists_tracked "$stem.cs" && git rm -- "$stem.cs"
  exists_tracked "$stem.cs.meta" && git rm -- "$stem.cs.meta"
done
if exists_tracked Assets/_Project/Editor.meta; then git rm -- Assets/_Project/Editor.meta; fi

# The useful historical detail is retained, but removed from the live entry point.
git mv README.md "$ARCHIVE/README_PRE_C1.md"
git mv GAMEPLAY_ARCHITECTURE.md "$ARCHIVE/GAMEPLAY_ARCHITECTURE_PRE_C1.md"
git mv HANDOFF.md "$ARCHIVE/HANDOFF_PRE_C1.md"
if exists_tracked Tools/Unity/WEAPON_LAB.md; then
  git mv Tools/Unity/WEAPON_LAB.md "$ARCHIVE/WEAPON_LAB_PRE_C1.md"
fi
for f in GAMEPLAY_CORE_A3B.md GAMEPLAY_CORE_A3C.md GAMEPLAY_CORE_A3D.md \
  GAMEPLAY_CORE_A4A.md GAMEPLAY_CORE_A4B.md GAMEPLAY_CORE_A4C.md; do
  git mv "$f" "$ARCHIVE/$f"
done
for f in README.md GAMEPLAY_ARCHITECTURE.md HANDOFF.md; do
  mv -- "$STAGE/$f" "$f"
done
rmdir "$STAGE"
mv Tools/cleanup-c1.sh "$ARCHIVE/CLEANUP_C1.sh"

# Stage *only* C1 outputs, never sweep unrelated user files into the commit.
git add -- README.md GAMEPLAY_ARCHITECTURE.md HANDOFF.md "$ARCHIVE/CLEANUP_C1.sh"

echo
printf '%s\n' 'C1 COMPLETE (not committed). Close/reopen Unity, then test WeaponLab and Sandbox.' \
  'Check: git status --short' \
  'Check: git diff --cached --stat' \
  'Do NOT run legacy generators. Green only after local Unity Play Mode regression.'
