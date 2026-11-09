#!/usr/bin/env python3
"""Extracts a .unitypackage (tar.gz of <guid>/{asset,asset.meta,pathname}) into the project, keeping GUIDs.
Used for TextMeshPro's Essential Resources, which Unity's batch-mode importer does not finish before quitting."""
import os, sys, tarfile

pkg, dest = sys.argv[1], sys.argv[2]
with tarfile.open(pkg, "r:gz") as t:
    members = {(m.name[2:] if m.name.startswith("./") else m.name): m for m in t.getmembers()}
    guids = {n.split("/")[0] for n in members if "/" in n}
    count = 0
    for g in sorted(guids):
        pm = members.get(f"{g}/pathname")
        if pm is None:
            continue
        path = t.extractfile(pm).read().decode("utf-8").splitlines()[0].strip()
        if not path.startswith("Assets/") or ".." in path:
            continue
        out = os.path.join(dest, path)
        am = members.get(f"{g}/asset")
        if am is not None:
            os.makedirs(os.path.dirname(out), exist_ok=True)
            with open(out, "wb") as f:
                f.write(t.extractfile(am).read())
            count += 1
        else:
            os.makedirs(out, exist_ok=True)
        mm = members.get(f"{g}/asset.meta")
        if mm is not None:
            with open(out + ".meta", "wb") as f:
                f.write(t.extractfile(mm).read())
print(f"extracted {count} files")
