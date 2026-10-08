#!/usr/bin/env python3
"""Verify that the six uploaded asset archives remain complete and unmodified."""
import hashlib
import json
from pathlib import Path

root = Path(__file__).resolve().parents[1] / "assets" / "user"
manifest = json.loads((root / "manifest.json").read_text(encoding="utf-8"))
expected = {"item.zip": 16, "icon.zip": 38, "interface.zip": 8,
            "host1.zip": 19, "host2.zip": 28, "monster.zip": 13}
assert {entry["archive"] for entry in manifest} == set(expected), "Missing asset archive"
seen = set()
for archive in manifest:
    assert len(archive["files"]) == expected[archive["archive"]], "Incorrect archive file count"
    for entry in archive["files"]:
        relative = Path(entry["path"])
        assert not relative.is_absolute() and ".." not in relative.parts, "Unsafe asset path"
        assert relative.parts[0] == Path(archive["archive"]).stem, "Incorrect asset category"
        assert entry["path"] not in seen, "Duplicate asset path"
        seen.add(entry["path"])
        data = (root / relative).read_bytes()
        assert len(data) == entry["bytes"], f"Truncated asset: {relative}"
        assert hashlib.sha256(data).hexdigest() == entry["sha256"], f"Changed asset: {relative}"
        if relative.suffix.lower() == ".png":
            assert data.startswith(b"\x89PNG\r\n\x1a\n"), f"Invalid PNG: {relative}"
        else:
            assert relative.suffix.lower() == ".gif" and data[:6] in (b"GIF87a", b"GIF89a"), f"Invalid GIF: {relative}"
assert len(seen) == 122, "Incomplete uploaded assets"
print(f"USER ASSET RESULT: {len(seen)} files, {len(manifest)} archives verified")
