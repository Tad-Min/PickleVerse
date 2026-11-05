import importlib.util
import hashlib
import io
import json
from pathlib import Path
import tarfile
import tempfile
import re
import unittest
from unittest.mock import patch

import release

COMMIT = "a" * 40
VERSION = COMMIT + "-123-1"
REPOSITORY = "tadminm/pickelverse"


def image_info(revision=COMMIT, architecture="amd64"):
    return [{"Os": "linux", "Architecture": architecture, "RepoDigests": [REPOSITORY + "@sha256:" + "1" * 64],
             "Config": {"Labels": {"org.opencontainers.image.revision": revision, "org.opencontainers.image.version": VERSION}}}]


def native_info(**overrides):
    info = dict(commit=COMMIT, result="Succeeded", errors=0,
                unity=re.search(r"m_EditorVersion: (\S+)", (release.ROOT / "My project/ProjectSettings/ProjectVersion.txt").read_text(encoding="utf-8-sig")).group(1),
                protocol=int(re.search(r"ProtocolVersion\s*=\s*(\d+)", (release.ROOT / "My project/Assets/Scripts/Multiplayer/Services/MultiplayerSceneConfiguration.cs").read_text(encoding="utf-8-sig")).group(1)))
    return dict(info, **overrides)


def native_archive(directory, extra=None, **overrides):
    path = Path(directory) / f"PickleVerseServer-linux-{COMMIT}.tar.gz"
    elf = b"\x7fELF\x02\x01" + b"\0" * 12 + b"\x3e\0"
    with tarfile.open(path, "w:gz") as archive:
        for name, data in (("build-info.json", json.dumps(native_info(**overrides)).encode()),
                           ("PickleVerseServer", elf), ("UnityPlayer.so", elf)):
            member = tarfile.TarInfo("./" + name)
            member.size = len(data)
            archive.addfile(member, io.BytesIO(data))
        member = tarfile.TarInfo("./PickleVerseServer_Data")
        member.type = tarfile.DIRTYPE
        archive.addfile(member)
        if extra:
            archive.addfile(extra)
    checksum = path.with_suffix(".sha256")
    checksum.write_text(f"{hashlib.sha256(path.read_bytes()).hexdigest()}  {path.name}\n")
    return path, checksum


class ReleaseChecks(unittest.TestCase):
    def test_mixed_commit_or_invalid_identifier_is_rejected(self):
        for version, commit in ((VERSION, "b" * 40), ("latest", COMMIT), (VERSION + "/../bad", COMMIT)):
            with self.assertRaises(ValueError):
                release.validate_version(version, commit)

    def test_wrong_source_platform_or_unpublished_image_is_rejected(self):
        variants = [image_info("b" * 40), image_info(architecture="arm64"), image_info()]
        variants[-1][0]["RepoDigests"] = []
        for info in variants:
            with patch("release.subprocess.check_output", return_value=json.dumps(info)), self.assertRaises(ValueError):
                release.inspect_image(REPOSITORY, "api", VERSION, COMMIT)

    def test_bundle_pins_all_four_images_and_contains_no_runtime_secrets(self):
        with tempfile.TemporaryDirectory() as directory, patch("release.subprocess.check_output", return_value=json.dumps(image_info())):
            manifest = release.bundle(REPOSITORY, VERSION, COMMIT, directory)
            self.assertEqual(set(manifest["images"]), {"api", "ai", "web", "server"})
            overlay = (Path(directory) / "docker-compose.release.yml").read_text(encoding="utf-8-sig")
            self.assertEqual(overlay.count("@sha256:"), 4)
            self.assertIn("game-server:", overlay)
            archive_path = next(Path(directory).glob("*.tar.gz"))
            with tarfile.open(archive_path) as archive:
                self.assertNotIn(".env", archive.getnames())
                self.assertNotIn("src/Backend/API/appsettings.json", archive.getnames())
                self.assertEqual(archive.extractfile("release.env").read().decode(), f"PKV_DOCKER_REPOSITORY={REPOSITORY}\nPKV_RELEASE={VERSION}\n")
                self.assertEqual(archive.extractfile("src/Database.sql").read(), (release.ROOT / "src/Database.sql").read_bytes())

    def test_incomplete_release_cannot_update_any_alias(self):
        with tempfile.TemporaryDirectory() as directory, patch("release.subprocess.run") as run:
            path = Path(directory) / "release.json"
            path.write_text(json.dumps(dict(release=VERSION, sourceCommit=COMMIT, repository=REPOSITORY, images={"api": {}})))
            with self.assertRaises(ValueError):
                release.promote(path)
            run.assert_not_called()

    def test_aliases_use_verified_digests_and_compose_server_name(self):
        with tempfile.TemporaryDirectory() as directory, patch("release.subprocess.check_output", return_value=json.dumps(image_info())):
            release.bundle(REPOSITORY, VERSION, COMMIT, directory)
            with patch("release.subprocess.run") as run:
                release.promote(Path(directory) / "release.json")
                self.assertEqual(run.call_count, 4)
                self.assertTrue(all(call.args[0][-1].startswith(REPOSITORY + "@sha256:") for call in run.call_args_list))
                self.assertIn(REPOSITORY + ":server", [call.args[0][-2] for call in run.call_args_list])

    def test_failed_or_stale_native_output_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "build-info.json"
            path.write_text(json.dumps(native_info(errors=1)))
            with self.assertRaises(ValueError):
                release.verify_server(path, VERSION, COMMIT)
            path.write_text(json.dumps(native_info()))
            release.verify_server(path, VERSION, COMMIT)
            # Native output is reusable across CI attempts of the same commit.
            release.verify_server(path, VERSION + "0", COMMIT)
            for change in (dict(commit="b" * 40), dict(unity="different"), dict(protocol=-1)):
                path.write_text(json.dumps(native_info(**change)))
                with self.assertRaises(ValueError):
                    release.verify_server(path, VERSION, COMMIT)

    def test_native_extract_verifies_metadata_and_rejects_mixed_output(self):
        with tempfile.TemporaryDirectory() as directory:
            archive, checksum = native_archive(directory)
            output = Path(directory) / "native"
            release.extract_server(archive, checksum, output, VERSION, COMMIT)
            self.assertTrue((output / "UnityPlayer.so").is_file())
            with self.assertRaises(ValueError):
                release.extract_server(archive, checksum, output, VERSION, COMMIT)

    def test_native_corruption_or_wrong_commit_cannot_be_used(self):
        with tempfile.TemporaryDirectory() as directory:
            archive, checksum = native_archive(directory, commit="b" * 40)
            with self.assertRaises(ValueError):
                release.extract_server(archive, checksum, Path(directory) / "wrong", VERSION, COMMIT)
            archive.write_bytes(archive.read_bytes() + b"corruption")
            with self.assertRaises(ValueError):
                release.extract_server(archive, checksum, Path(directory) / "corrupt", VERSION, COMMIT)

    def test_native_archive_rejects_traversal_links_and_duplicate_paths(self):
        malicious = [tarfile.TarInfo("../escape"), tarfile.TarInfo("/absolute"), tarfile.TarInfo("./build-info.json"), tarfile.TarInfo("link")]
        malicious[-1].type, malicious[-1].linkname = tarfile.SYMTYPE, "../escape"
        for member in malicious:
            with self.subTest(member=member.name), tempfile.TemporaryDirectory() as directory:
                archive, checksum = native_archive(directory, extra=member)
                with self.assertRaises(ValueError):
                    release.extract_server(archive, checksum, Path(directory) / "native", VERSION, COMMIT)
                self.assertFalse((Path(directory) / "native").exists())

    def test_sanitizer_removes_credentials_without_changing_source_or_rules(self):
        spec = importlib.util.spec_from_file_location("sanitizer", release.ROOT / "deploy/api/sanitize_settings.py")
        sanitizer = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(sanitizer)
        fixture = dict(ConnectionStrings=dict(DefaultConnection="private-connection"),
                       Jwt=dict(Key="private-jwt"), Smtp=dict(Password="private-password"),
                       HybridAi=dict(ServiceKey="private-key", BotPolicy=dict(version="bot-v1")))
        with tempfile.TemporaryDirectory() as directory:
            source, output = Path(directory) / "source", Path(directory) / "output"
            source.mkdir()
            path = source / "appsettings.json"
            original = json.dumps(fixture)
            path.write_text(original)
            sanitizer.main(source, output)
            sanitized = json.loads((output / "appsettings.json").read_text(encoding="utf-8-sig"))
            self.assertEqual(path.read_text(encoding="utf-8-sig"), original)
            self.assertNotIn("private-", json.dumps(sanitized))
            self.assertEqual(sanitized["HybridAi"]["BotPolicy"]["version"], "bot-v1")


if __name__ == "__main__":
    unittest.main()
