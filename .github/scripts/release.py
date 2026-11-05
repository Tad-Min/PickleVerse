"""Create a coherent, digest-pinned release from the four images the smoke job pulled."""
import argparse
import hashlib
import io
import json
from pathlib import Path, PurePosixPath
import re
import subprocess
import tarfile

COMPONENTS = {"api": "backend-api", "ai": "ai-service", "web": "web", "server": "game-server"}
ROOT = Path(__file__).resolve().parents[2]


def validate_version(release, commit):
    if not re.fullmatch(r"[0-9a-f]{40}", commit) or not re.fullmatch(re.escape(commit) + r"-[0-9]+-[0-9]+", release):
        raise ValueError("Release must contain this commit, run ID and run attempt.")


def verify_server(path, release, commit):
    validate_version(release, commit)
    report = json.loads(Path(path).read_text(encoding="utf-8-sig"))
    unity = re.search(r"^m_EditorVersion: (\S+)", (ROOT / "My project/ProjectSettings/ProjectVersion.txt").read_text(encoding="utf-8-sig"), re.M).group(1)
    protocol = int(re.search(r"ProtocolVersion\s*=\s*(\d+)", (ROOT / "My project/Assets/Scripts/Multiplayer/Services/MultiplayerSceneConfiguration.cs").read_text(encoding="utf-8-sig")).group(1))
    if (report.get("commit") != commit or report.get("unity") != unity or report.get("protocol") != protocol
            or report.get("result") != "Succeeded" or report.get("errors") != 0):
        raise ValueError("Native binary commit, Unity version, protocol or build result mismatch.")


def extract_server(archive_path, checksum_path, output, release, commit):
    validate_version(release, commit)
    archive_path, output = Path(archive_path), Path(output)
    if archive_path.name != f"PickleVerseServer-linux-{commit}.tar.gz":
        raise ValueError("Unexpected native package name.")
    with archive_path.open("rb") as stream:
        digest = hashlib.file_digest(stream, "sha256").hexdigest()
    if Path(checksum_path).read_text(encoding="ascii").strip() != f"{digest}  {archive_path.name}":
        raise ValueError("Native package checksum mismatch.")
    if output.exists():
        raise ValueError("Extract into a new directory; never mix native builds.")
    with tarfile.open(archive_path, "r:gz") as archive:
        members = archive.getmembers()
        names, total = set(), 0
        for member in members:
            path = PurePosixPath(member.name)
            if (path.is_absolute() or ".." in path.parts or "\\" in member.name or ":" in member.name
                    or not (member.isfile() or member.isdir()) or str(path) in names):
                raise ValueError("Unsafe or duplicate native archive entry.")
            names.add(str(path))
            total += member.size
        if total > 2 * 1024**3 or len(members) > 20000:
            raise ValueError("Native package exceeds extraction limits.")
        output.mkdir(parents=True)
        archive.extractall(output, filter="data")
    verify_server(output / "build-info.json", release, commit)
    if not (output / "PickleVerseServer_Data").is_dir():
        raise ValueError("Native data directory is missing.")
    for name in ("PickleVerseServer", "UnityPlayer.so"):
        with (output / name).open("rb") as stream:
            header = stream.read(20)
        if header[:6] != b"\x7fELF\x02\x01" or header[18:20] != b"\x3e\x00":
            raise ValueError("Expected Linux x86_64 ELF payload: " + name)


def inspect_image(repository, component, release, commit):
    reference = f"{repository}:{component}-{release}"
    info = json.loads(subprocess.check_output(["docker", "image", "inspect", reference], text=True))[0]
    labels = info.get("Config", {}).get("Labels") or {}
    if info.get("Os") != "linux" or info.get("Architecture") != "amd64" or labels.get("org.opencontainers.image.revision") != commit or labels.get("org.opencontainers.image.version") != release:
        raise ValueError(f"Image source/platform mismatch: {component}")
    prefixes = (repository + "@", "docker.io/" + repository + "@")
    digests = [item.split("@", 1)[1] for item in info.get("RepoDigests", []) if item.startswith(prefixes)]
    if len(set(digests)) != 1 or not re.fullmatch(r"sha256:[0-9a-f]{64}", digests[0]):
        raise ValueError(f"Missing or ambiguous registry digest: {component}")
    return {"tag": f"{component}-{release}", "digest": digests[0], "reference": repository + "@" + digests[0]}


def bundle(repository, release, commit, output):
    validate_version(release, commit)
    if not re.fullmatch(r"[a-z0-9_-]+/[a-z0-9_.-]+", repository):
        raise ValueError("Expected a Docker Hub namespace/repository.")
    images = {name: inspect_image(repository, name, release, commit) for name in COMPONENTS}
    manifest = {"repository": repository, "release": release, "sourceCommit": commit, "images": images}
    files = {name: (ROOT / name).read_bytes() for name in (
        "docker-compose.yml", "docker-compose.dedicated.yml", "docker-compose.hub.yml", "src/Database.sql", ".env.example", "Doc/CI_CD_DockerHub.md")}
    files["release.env"] = f"PKV_DOCKER_REPOSITORY={repository}\nPKV_RELEASE={release}\n".encode()
    overlay = ["# Generated by CI after all four images passed smoke checks.", "services:"]
    for name, service in COMPONENTS.items():
        overlay.extend([f"  {service}:", f"    image: {images[name]['reference']}"])
    files["docker-compose.release.yml"] = ("\n".join(overlay) + "\n").encode()
    manifest["files"] = {name: hashlib.sha256(data).hexdigest() for name, data in files.items()}
    files["release.json"] = (json.dumps(manifest, indent=2) + "\n").encode()
    output = Path(output)
    output.mkdir(parents=True, exist_ok=True)
    for name in ("release.env", "release.json", "docker-compose.release.yml"):
        (output / name).write_bytes(files[name])
    archive_path = output / f"PickleVerse-deployment-{release}.tar.gz"
    with tarfile.open(archive_path, "w:gz") as archive:
        for name, data in sorted(files.items()):
            entry = tarfile.TarInfo(name)
            entry.size, entry.mode = len(data), 0o644
            archive.addfile(entry, io.BytesIO(data))
        entry = tarfile.TarInfo("src/AiService/models")
        entry.type, entry.mode = tarfile.DIRTYPE, 0o755
        archive.addfile(entry)
    with archive_path.open("rb") as stream:
        digest = hashlib.file_digest(stream, "sha256").hexdigest()
    archive_path.with_name(archive_path.name + ".sha256").write_text(f"{digest}  {archive_path.name}\n", encoding="ascii")
    summary = f"## PickleVerse release\n\nCommit: `{commit}`\n\nPKV_RELEASE: `{release}`\n\nDownload the deployment artifact for the digest-pinned Ubuntu Compose bundle.\n\n"
    summary += "\n".join(f"- `{repository}:{item['tag']}` — `{item['digest']}`" for item in images.values()) + "\n"
    (output / "summary.md").write_text(summary, encoding="utf-8")
    return manifest


def promote(path):
    manifest = json.loads(Path(path).read_text(encoding="utf-8"))
    validate_version(manifest["release"], manifest["sourceCommit"])
    if set(manifest["images"]) != set(COMPONENTS):
        raise ValueError("All four images must pass before aliases can be updated.")
    for name, image in manifest["images"].items():
        if image["reference"] != manifest["repository"] + "@" + image["digest"] or not re.fullmatch(r"sha256:[0-9a-f]{64}", image["digest"]):
            raise ValueError("Invalid image digest in release manifest.")
    for name, image in manifest["images"].items():
        subprocess.run(["docker", "buildx", "imagetools", "create", "--tag", manifest["repository"] + ":" + name, image["reference"]], check=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    commands = parser.add_subparsers(dest="command", required=True)
    create = commands.add_parser("bundle")
    create.add_argument("--repository", required=True)
    create.add_argument("--output", default=str(ROOT / "artifacts/ci/release"))
    verify = commands.add_parser("verify-server")
    verify.add_argument("path")
    extract = commands.add_parser("extract-server")
    extract.add_argument("archive")
    extract.add_argument("checksum")
    extract.add_argument("output")
    for command in (create, verify, extract):
        command.add_argument("--release", required=True)
        command.add_argument("--commit", required=True)
    commands.add_parser("promote").add_argument("path")
    args = parser.parse_args()
    if args.command == "bundle":
        bundle(args.repository, args.release, args.commit, args.output)
    elif args.command == "verify-server":
        verify_server(args.path, args.release, args.commit)
    elif args.command == "extract-server":
        extract_server(args.archive, args.checksum, args.output, args.release, args.commit)
    else:
        promote(args.path)
