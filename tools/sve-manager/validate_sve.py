#!/usr/bin/env python3
"""
Automated Validator & Dependency Checker for Stardew Valley Expanded (SVE)
Verifies mod stack presence, manifest integrity, and memory thresholds for headless dedicated server.
"""

import os
import sys
import json
from pathlib import Path

REQUIRED_SVE_MODS = [
    {
        "id": "Pathoschild.ContentPatcher",
        "name": "Content Patcher",
        "folder_hints": ["ContentPatcher", "Content Patcher"],
        "required": True,
    },
    {
        "id": "spacechase0.SpaceCore",
        "name": "SpaceCore",
        "folder_hints": ["SpaceCore"],
        "required": True,
    },
    {
        "id": "Cherry.ExpandedPreconditionsUtility",
        "name": "Expanded Preconditions Utility",
        "folder_hints": ["ExpandedPreconditionsUtility", "Expanded Preconditions Utility"],
        "required": True,
    },
    {
        "id": "spacechase0.ExtraMapLayers",
        "name": "Extra Map Layers",
        "folder_hints": ["ExtraMapLayers", "Extra Map Layers"],
        "required": True,
    },
    {
        "id": "Cherry.ShopTileFramework",
        "name": "Shop Tile Framework",
        "folder_hints": ["ShopTileFramework", "Shop Tile Framework"],
        "required": True,
    },
    {
        "id": "FlashShifter.StardewValleyExpandedCP",
        "name": "Stardew Valley Expanded (Content Patcher Component)",
        "folder_hints": ["Stardew Valley Expanded", "StardewValleyExpanded", "[CP] Stardew Valley Expanded"],
        "required": True,
    },
    {
        "id": "FlashShifter.StardewValleyExpandedCode",
        "name": "Stardew Valley Expanded (Code Component)",
        "folder_hints": ["Stardew Valley Expanded", "StardewValleyExpanded", "SVE"],
        "required": False,
    },
    {
        "id": "FlashShifter.GrandpasFarm",
        "name": "Grandpa's Farm (Optional Map Layout)",
        "folder_hints": ["GrandpasFarm", "Grandpa's Farm", "[CP] Grandpa's Farm"],
        "required": False,
    }
]

def scan_manifests(mods_dir: Path):
    manifests = {}
    if not mods_dir.exists():
        return manifests

    for root, _, files in os.walk(mods_dir):
        if "manifest.json" in files:
            manifest_path = Path(root) / "manifest.json"
            try:
                with open(manifest_path, "r", encoding="utf-8-sig") as f:
                    data = json.load(f)
                    uid = data.get("UniqueID")
                    if uid:
                        manifests[uid] = {
                            "name": data.get("Name", uid),
                            "version": data.get("Version", "unknown"),
                            "path": manifest_path,
                            "dependencies": data.get("Dependencies", [])
                        }
            except Exception as e:
                print(f"[WARN] Error reading manifest at {manifest_path}: {e}")
    return manifests

def check_memory():
    try:
        if hasattr(os, "sysconf") and "SC_PAGE_SIZE" in os.sysconf_names and "SC_PHYS_PAGES" in os.sysconf_names:
            total_mem = os.sysconf("SC_PAGE_SIZE") * os.sysconf("SC_PHYS_PAGES")
            total_mb = total_mem / (1024 * 1024)
            return total_mb
    except Exception:
        pass
    return None

def main():
    print("=" * 60)
    print("  STARDEW VALLEY EXPANDED (SVE) AUTOMATED VALIDATION SUITE")
    print("=" * 60)

    # 1. Detect Mods Directory
    default_paths = [
        Path("/data/Mods"),
        Path("C:/data/Mods"),
        Path("./mods"),
        Path("./mod")
    ]
    mods_dir = None
    for p in default_paths:
        if p.exists():
            mods_dir = p
            break

    if not mods_dir:
        mods_dir = Path("/data/Mods")
        print(f"[INFO] Using target production mods directory: {mods_dir}")
    else:
        print(f"[INFO] Scanning mods directory: {mods_dir}")

    # 2. Check Host Memory
    mem_mb = check_memory()
    if mem_mb:
        print(f"[INFO] Available Host RAM: {mem_mb:.0f} MB")
        if mem_mb < 3000:
            print("[WARN] SVE requires at least 3072MB (3GB) RAM. Low memory may cause container OOM.")
        else:
            print("[PASS] Host RAM meets or exceeds SVE 3GB threshold.")
    else:
        print("[INFO] Host RAM check bypassed (Windows/Non-POSIX).")

    # 3. Scan Installed Manifests
    manifests = scan_manifests(mods_dir)
    print(f"[INFO] Found {len(manifests)} installed mod manifests.")

    # 4. Verify SVE Dependency Stack
    print("-" * 60)
    print("  Checking Required SVE Mod Stack:")
    print("-" * 60)

    missing_required = []
    installed_count = 0

    for mod in REQUIRED_SVE_MODS:
        mod_id = mod["id"]
        mod_name = mod["name"]
        is_req = mod["required"]

        if mod_id in manifests:
            installed = manifests[mod_id]
            print(f"[PASS] {mod_name} (v{installed['version']}) is INSTALLED.")
            installed_count += 1
        else:
            status = "MISSING (REQUIRED)" if is_req else "NOT INSTALLED (OPTIONAL)"
            tag = "[FAIL]" if is_req else "[INFO]"
            print(f"{tag} {mod_name}: {status}")
            if is_req:
                missing_required.append(mod)

    print("=" * 60)
    if missing_required:
        print(f"[SUMMARY] SVE Readiness: INCOMPLETE ({len(missing_required)} required dependencies missing).")
        print("Required dependencies to install into /data/Mods/:")
        for m in missing_required:
            print(f"  - {m['name']} (ID: {m['id']})")
        print("Note: In vanilla client mode, clients enter without mods. In SVE mode, both server and all clients must have matching SVE mod packages.")
        sys.exit(0)
    else:
        print(f"[SUMMARY] SVE Readiness: ALL {installed_count} SVE COMPONENTS DETECTED AND READY.")
        sys.exit(0)

if __name__ == "__main__":
    main()
