"""Remove machine credentials before appsettings enters a publishable image."""
import json
from pathlib import Path
import sys


PRIVATE_KEYS = {
    "key", "password", "servicekey", "secretkey", "accesskey",
    "clientsecret", "apikey", "username", "fromemail", "email",
    "connectionstring",
}


def sanitize(value):
    if isinstance(value, list):
        return [sanitize(item) for item in value]
    if not isinstance(value, dict):
        return value
    result = {}
    for key, child in value.items():
        if key.lower() == "connectionstrings" and isinstance(child, dict):
            result[key] = {name: "" for name in child}
        elif key.lower() in PRIVATE_KEYS:
            result[key] = ""
        else:
            result[key] = sanitize(child)
    return result


def main(source, destination):
    source, destination = Path(source), Path(destination)
    if source.resolve() == destination.resolve():
        raise ValueError("Source settings must remain unchanged.")
    files = sorted(source.glob("appsettings*.json"))
    if not files or not (source / "appsettings.json").is_file():
        raise ValueError("The base application settings are required.")
    destination.mkdir(parents=True, exist_ok=True)
    for path in files:
        settings = json.loads(path.read_text(encoding="utf-8-sig"))
        (destination / path.name).write_text(
            json.dumps(sanitize(settings), ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )
    print(f"Prepared {len(files)} application configuration files without credentials.")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit("Usage: sanitize_settings.py SOURCE_DIRECTORY OUTPUT_DIRECTORY")
    main(*sys.argv[1:])
