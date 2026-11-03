"""Package only the runtime Compose files, schema and blank environment template."""
import argparse
import hashlib
import io
import json
from pathlib import Path
import subprocess
import tarfile


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--repository", default="tadminm/pickelverse")
    parser.add_argument("--release", default="20261103-p18-53871c6-hub1")
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    output = root / "artifacts/docker-hub-20261103"
    images = json.loads((output / "images.json").read_text(encoding="utf-8"))
    if images["repository"] != args.repository or images["release"] != args.release:
        raise SystemExit("Verified registry images do not match this deployment release.")
    if not all(item.get("remoteVerified") and item.get("digest") for item in images["images"].values()):
        raise SystemExit("Verify all three registry images before creating a deployment bundle.")
    files = {}
    for name in ("docker-compose.yml", "docker-compose.dedicated.yml", "docker-compose.hub.yml", "src/Database.sql"):
        files[name] = (root / name).read_bytes()
    template = (root / ".env.example").read_text(encoding="utf-8-sig")
    values = {"PKV_RELEASE": args.release, "PKV_DOCKER_REPOSITORY": args.repository}
    lines = []
    for line in template.splitlines():
        key = line.split("=", 1)[0]
        lines.append(key + "=" + values.pop(key) if key in values else line)
    lines.extend(key + "=" + value for key, value in values.items())
    files[".env.example"] = ("\n".join(lines) + "\n").encode()
    files["HUONG_DAN.md"] = (root / "Doc/DockerHub_Ubuntu_20261103.md").read_bytes()
    manifest = {
        **images,
        "sourceCommit": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=root, text=True).strip(),
        "files": {name: hashlib.sha256(data).hexdigest() for name, data in files.items()},
    }
    files["release.json"] = (json.dumps(manifest, indent=2) + "\n").encode()
    bundle = output / f"PickleVerse-DockerHub-{args.release}.tar.gz"
    with tarfile.open(bundle, "w:gz") as archive:
        for name, data in sorted(files.items()):
            info = tarfile.TarInfo(name)
            info.size = len(data)
            info.mode = 0o644
            archive.addfile(info, io.BytesIO(data))
        info = tarfile.TarInfo("src/AiService/models")
        info.type = tarfile.DIRTYPE
        info.mode = 0o755
        archive.addfile(info)
    digest = hashlib.sha256(bundle.read_bytes()).hexdigest()
    bundle.with_name(bundle.name + ".sha256").write_text(digest + "  " + bundle.name + "\n", encoding="ascii")
    print(json.dumps({"bundle": str(bundle), "bytes": bundle.stat().st_size, "sha256": digest}))


if __name__ == "__main__":
    main()
