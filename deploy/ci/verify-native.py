"""Check the native Dedicated Server before including it in the Docker build."""

import json
import re
import struct
import sys
from pathlib import Path


native = Path(sys.argv[1])
commit = sys.argv[2]
project = Path("My project")
unity = re.search(r"^m_EditorVersion: (\S+)$", (project / "ProjectSettings/ProjectVersion.txt").read_text(encoding="utf-8-sig"), re.M)[1]
protocol = int(re.search(r"\bProtocolVersion\s*=\s*(\d+)", (
    project / "Assets/Scripts/Multiplayer/Services/MultiplayerSceneConfiguration.cs"
).read_text(encoding="utf-8-sig"))[1])
info = json.loads((native / "build-info.json").read_text(encoding="utf-8-sig"))
if not re.fullmatch(r"[0-9a-f]{40}", commit) or (
    info["commit"], info["unity"], info["protocol"], info["result"], info["errors"]
) != (commit, unity, protocol, "Succeeded", 0):
    raise SystemExit("Native build does not match this commit, Unity version and protocol")

with (native / "PickleVerseServer").open("rb") as executable:
    header = executable.read(64)
if header[:6] != b"\x7fELF\x02\x01" or len(header) != 64 or struct.unpack_from("<H", header, 18)[0] != 62:
    raise SystemExit("Dedicated Server must be an ELF64 Linux x86-64 executable")
if not (native / "PickleVerseServer_Data").is_dir() or not (native / "UnityPlayer.so").is_file():
    raise SystemExit("Dedicated Server data or Unity runtime is missing")
if any(native.rglob("*AppUINativePlugin*.so")):
    raise SystemExit("Native App UI plugin must be excluded from Dedicated Server")

print(protocol)
