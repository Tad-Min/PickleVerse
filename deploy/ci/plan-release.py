"""Select PickleVerse checks and builds from a Git comparison."""

import json
import os
import re
import subprocess
from pathlib import Path


COMPONENTS = ("api", "ai", "web", "server")
UNITY = "My project/"
PROTOCOL = UNITY + "Assets/Scripts/Multiplayer/Services/MultiplayerSceneConfiguration.cs"
COMMIT = r"[0-9a-f]{40}"


def git(*args):
    return subprocess.check_output(["git", *args])


def protocol(source):
    return int(re.search(rb"\bProtocolVersion\s*=\s*(\d+)", source)[1])


def classify(paths, protocol_changed, full=False):
    build, checks = set(), set()
    deploy = False
    for path in paths:
        if path.endswith((".md", ".md.meta")):
            continue
        if path.startswith(("deploy/ci/", ".github/workflows/")) or path in {".dockerignore", ".gitattributes"}:
            full = True
        if path.startswith("src/Backend/") or path.startswith("deploy/api/"):
            build.add("api")
        if path.startswith("src/AiService/"):
            build.add("ai")
        if path.startswith("src/Testing/AiService/"):
            checks.add("ai")
        if path.startswith(("src/Frontend/", "deploy/web/")):
            build.add("web")
        if path.startswith((UNITY + "Assets/", UNITY + "Packages/", UNITY + "ProjectSettings/",
                            "deploy/game-server/")):
            build.add("server")
        if path.startswith("contracts/"):
            build.update(("api", "ai"))
        if path.startswith(("src/Testing/Backend/", UNITY + "Assets/Scripts/")) or path == (
            UNITY + "Assets/Resources/BackendApiSettings.asset"
        ):
            checks.add("api")
        if path == "src/Database.sql":
            checks.add("api")
            deploy = True
        if path == "docker-compose.linux.yml":
            deploy = True
        if path in {"global.json", "Directory.Build.props", "Directory.Build.targets",
                    "Directory.Packages.props", "NuGet.Config"}:
            build.add("api")
    if protocol_changed:
        build.update(("api", "server"))
    if full:
        build.update(COMPONENTS)
    checks.update(build)
    if build & {"ai", "server"}:
        checks.add("api")
    return build, checks, deploy or bool(build)


def main():
    head = os.environ["GITHUB_SHA"]
    pull_request = os.environ["GITHUB_EVENT_NAME"] == "pull_request"
    base = os.environ.get("PKV_PR_BASE", "")
    images = {}
    token = ""
    full = os.environ.get("PKV_FULL_REBUILD") == "true"
    if not pull_request and (os.environ.get("PKV_DEPLOY_STATE") or not full):
        state = json.loads(os.environ["PKV_DEPLOY_STATE"])
        token = state["token"]
        if not re.fullmatch(r"[0-9a-f]{64}", token):
            raise ValueError("Invalid deployment state token")
        release = state["release"]
        if state["pending"] or release is None:
            full = True
        elif not full:
            base = release["commit"]
            images = release["images"]
            prefix = re.escape(os.environ["PKV_DOCKER_REPOSITORY"])
            if set(images) != set(COMPONENTS) or any(
                not re.fullmatch(prefix + r"@sha256:[0-9a-f]{64}", ref) for ref in images.values()
            ):
                raise ValueError("The deployed image references are invalid for this repository")
    paths = []
    current_protocol = protocol(Path(PROTOCOL).read_bytes())
    protocol_changed = False
    if not full:
        if not re.fullmatch(COMMIT, base) or subprocess.run(
            ["git", "merge-base", "--is-ancestor", base, head], check=False
        ).returncode:
            print("No usable comparison commit; rebuilding all components.")
            full = True
        else:
            paths = [os.fsdecode(p) for p in git("diff", "--name-only", "-z", "--no-renames",
                                                 base, head, "--").split(b"\0") if p]
            # For PRs, a change to this file also runs API checks. Release
            # planning can compare the actual protocol stored with the images.
            protocol_changed = PROTOCOL in paths if pull_request else release["protocol"] != current_protocol
    build, checks, deploy = classify(paths, protocol_changed, full)
    output = {
        "build": " ".join(c for c in COMPONENTS if c in build),
        "test_api": str("api" in checks).lower(),
        "test_ai": str("ai" in checks).lower(),
        "test_web": str("web" in checks).lower(),
        "native": str("server" in build).lower(),
        "release": str(not pull_request and deploy).lower(),
        "images": json.dumps(images, separators=(",", ":")),
        "protocol": str(current_protocol),
        "token": token,
    }
    print(f"Base: {base or 'first deployment'}; build: {output['build'] or 'none'}; "
          f"checks: {', '.join(sorted(checks)) or 'none'}; publish: {output['release']}")
    with open(os.environ["GITHUB_OUTPUT"], "a", encoding="utf-8") as stream:
        for key, value in output.items():
            print(f"{key}={value}", file=stream)


if __name__ == "__main__":
    main()
