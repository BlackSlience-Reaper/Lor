"""Dependency-free CI checks; these do not load the game or execute mod code."""

import argparse
import json
import os
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"Duplicate JSON key: {key}")
        result[key] = value
    return result


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8-sig"), object_pairs_hook=unique_object)


def localization():
    folder = ROOT / "LibraryOfRuina/localization"
    files = 0
    keys = 0
    for locale in ("zhs", "eng", "jpn", "kor"):
        paths = sorted((folder / locale).glob("*.json"))
        if not paths:
            raise ValueError(f"Missing localization files: {locale}")
        for path in paths:
            try:
                data = read_json(path)
                if not isinstance(data, dict) or not all(isinstance(v, str) for v in data.values()):
                    raise ValueError("Expected an object containing string values")
            except (ValueError, UnicodeError) as error:
                raise ValueError(f"{path.relative_to(ROOT)}: {error}") from error
            files += 1
            keys += len(data)
    return f"Parsed {files} localization JSON files and {keys} strings across four locales. Duplicate keys rejected. Translation and placeholder semantics are outside this check."


def manifest():
    data = read_json(ROOT / "LibraryOfRuina.json")
    project = ET.parse(ROOT / "LibraryOfRuina.csproj").getroot()
    if data["id"] != project.findtext(".//AssemblyName"):
        raise ValueError("Manifest id differs from the project AssemblyName")
    if not re.fullmatch(r"v?\d+\.\d+\.\d+", data["version"]):
        raise ValueError("Expected a three-component mod version")
    if data["has_dll"] is not True or data["has_pck"] is not True:
        raise ValueError("The mod requires both DLL and PCK artifacts")
    dependencies = data["dependencies"]
    ids = [item["id"] for item in dependencies]
    if len(ids) != len(set(ids)):
        raise ValueError("Duplicate dependency ids")
    required = {"ActLikeIt2", "LibraryOfRuinaLib", "STS2-RitsuLib"}
    if not required.issubset(ids):
        raise ValueError(f"Missing dependencies: {sorted(required.difference(ids))}")
    for dependency in dependencies:
        if not re.fullmatch(r"\d+\.\d+\.\d+", dependency["min_version"]):
            raise ValueError(f"Invalid minimum version: {dependency['id']}")
    package = project.find(".//PackageReference[@Include='STS2.RitsuLib']")
    if package is not None:
        minimum = next(item["min_version"] for item in dependencies if item["id"] == "STS2-RitsuLib")
        if package.attrib["Version"] != minimum:
            raise ValueError("STS2.RitsuLib package version differs from the manifest minimum")
    elif project.find(".//Import[@Project='$(RitsuLibReferencesProps)']") is None:
        raise ValueError("Missing RitsuLib reference import")
    tracked = subprocess.check_output(["git", "ls-files", "-z", "*.csproj"], cwd=ROOT).decode().split("\0")
    for path in filter(None, tracked):
        ET.parse(ROOT / path)
    return f"Manifest {data['version']}: assembly id, DLL/PCK flags, dependency version formats, RitsuLib reference wiring and tracked project XML passed. Installed dependency versions and game DLL availability require a local build."


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("check", choices=("localization", "manifest"))
    args = parser.parse_args()
    result = {"localization": localization, "manifest": manifest}[args.check]()
    print(result)
    if summary := os.environ.get("GITHUB_STEP_SUMMARY"):
        with open(summary, "a", encoding="utf-8") as output:
            output.write(result + "\n")
