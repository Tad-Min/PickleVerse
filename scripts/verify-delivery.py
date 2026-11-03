from pathlib import Path, PurePosixPath
import hashlib, json, tarfile, zipfile

root = Path(__file__).resolve().parents[1]
artifact = root / 'artifacts/account-multiplayer-ai-20261103'
results = []
for name in ('PickleVerse-Windows-20261103-p18.zip', 'PickleVerse-Ubuntu-20261103-p18.tar.gz'):
    path = artifact / name
    expected = (artifact / (name + '.sha256')).read_text().split()[0]
    if hashlib.sha256(path.read_bytes()).hexdigest() != expected: raise RuntimeError('Archive checksum mismatch')
    results.append({'file': name, 'bytes': path.stat().st_size, 'sha256': expected})
with zipfile.ZipFile(artifact / results[0]['file']) as archive:
    if archive.testzip() is not None: raise RuntimeError('Windows archive CRC failed')
    names = archive.namelist()
    if 'PickleVerse.exe' not in names or 'HUONG_DAN.txt' not in names: raise RuntimeError('Windows launch files missing')
    if any('BackUpThisFolder' in name or '.unity-pipeline' in name for name in names): raise RuntimeError('Unwanted development file in client archive')
with tarfile.open(artifact / results[1]['file'], 'r:gz') as archive:
    members = archive.getmembers()
    names = {item.name for item in members}
    if any(PurePosixPath(item.name).is_absolute() or '..' in PurePosixPath(item.name).parts or item.issym() or item.islnk() for item in members):
        raise RuntimeError('Unsafe archive path or link')
    if any('/acceptance-private/' in name or PurePosixPath(name).name == '.env' for name in names): raise RuntimeError('Private configuration in delivery')
    manifest_name = 'artifacts/account-multiplayer-ai-20261103/release-manifest.json'
    manifest = json.load(archive.extractfile(manifest_name))
    for name, digest in manifest['files'].items():
        if hashlib.sha256(archive.extractfile(name).read()).hexdigest() != digest: raise RuntimeError('Manifest content mismatch: ' + name)
    for name in names:
        if name.startswith('src/Backend/API/appsettings') and name.endswith('.json'):
            config = json.load(archive.extractfile(name))
            def verify(value):
                if isinstance(value, dict):
                    for key, child in value.items():
                        if key.lower() in ('key', 'password', 'servicekey', 'accesskey', 'secretkey', 'clientsecret', 'apikey', 'connectionstring') and child:
                            raise RuntimeError('Nonempty credential setting in delivery: ' + key)
                        verify(child)
            verify(config)
            if any(config.get('ConnectionStrings', {}).values()): raise RuntimeError('Developer connection string in delivery')
    executable = archive.getmember('artifacts/account-multiplayer-ai-20261103/linux-server/PickleVerseServer')
    if not executable.mode & 0o111: raise RuntimeError('Linux executable permission missing')
(artifact / 'delivery-verification.json').write_text(json.dumps({'passed': True, 'archives': results, 'manifestFiles': len(manifest['files'])}, indent=2) + '\n', encoding='utf-8')
print('PASS: both archive checksums, Windows CRC, Ubuntu manifest, credential exclusion and Linux permissions.')
