#!/usr/bin/env python3
"""Verify that the packaged VSIX contains everything the manifest declares."""

import sys
import zipfile
from pathlib import Path
from xml.etree import ElementTree

REQUIRED_ENTRIES = {
    "[Content_Types].xml",
    "extension.vsixmanifest",
    "CodeSpiritSnippets.pkgdef",
    "ProjectTemplates/CodeSpirit.LibraryManagement.zip",
}

VSIX_NS = {"v": "http://schemas.microsoft.com/developer/vsx-schema/2011"}


def fail(message):
    print(f"VSIX verification failed: {message}", file=sys.stderr)
    raise SystemExit(1)


def resolve_asset(names, asset_path):
    normalized = asset_path.rstrip("/")
    return normalized in names or any(
        name.startswith(f"{normalized}/") for name in names
    )


def main():
    if len(sys.argv) != 2:
        fail("usage: verify-vsix.py <path-to-vsix>")

    vsix_path = Path(sys.argv[1])
    if not vsix_path.is_file():
        fail(f"{vsix_path} does not exist")

    with zipfile.ZipFile(vsix_path) as archive:
        names = set(archive.namelist())
        missing = REQUIRED_ENTRIES - names
        if missing:
            fail(f"missing entries: {sorted(missing)}")

        snippets = [n for n in names if n.startswith("Snippets/") and n.endswith(".snippet")]
        if not snippets:
            fail("no snippet files packaged")

        manifest = ElementTree.fromstring(archive.read("extension.vsixmanifest"))
        for asset in manifest.findall("./v:Assets/v:Asset", VSIX_NS):
            path = asset.get("Path")
            if not path:
                fail(f"asset of type {asset.get('Type')} has no Path")
            if not resolve_asset(names, path):
                fail(f"asset target not packaged: {path}")

        for tag in ("Icon", "PreviewImage", "License"):
            node = manifest.find(f"./v:Metadata/v:{tag}", VSIX_NS)
            if node is None or not node.text:
                continue
            if node.text not in names:
                fail(f"metadata {tag} file not packaged: {node.text}")

    print(f"VSIX OK: {len(names)} entries, {len(snippets)} snippets")


if __name__ == "__main__":
    main()