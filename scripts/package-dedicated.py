"""Create an Ubuntu handoff without local .env, credentials, caches or build intermediates."""
from pathlib import Path
import hashlib
import json
import tarfile
import subprocess
import io

root = Path(__file__).resolve().parents[1]
output = root / 'artifacts/account-multiplayer-ai-20261103'
native = output / 'linux-server'
report = json.loads((root / 'My project/Temp/dedicated-build.json').read_text(encoding='utf-8-sig'))
if report.get('result') != 'Succeeded':
    raise SystemExit('Dedicated build must succeed before packaging.')
if report.get('serverHttp') != 'AlwaysAllowed-internal-server' or not report.get('appUiPluginExcluded'):
    raise SystemExit('Rebuild using the updated headless server builder before packaging.')
for required in ('PickleVerseServer', 'UnityPlayer.so', 'PickleVerseServer_Data/MonoBleedingEdge'):
    if not (native / required).exists():
        raise SystemExit('Incomplete native build: ' + required)
selected = set()
for directory in ('src/Backend', 'src/AiService', 'src/Seed', 'deploy', 'contracts/hybrid-ai'):
    for path in (root / directory).rglob('*'):
        if not path.is_file():
            continue
        if any(part.lower() in ('bin', 'obj', '__pycache__', '.pytest_cache', '.venv', 'models') for part in path.relative_to(root).parts):
            # Entities/Models is application source, not a trained model artifact.
            if not path.is_relative_to(root / 'src/Backend/Entities/Models'):
                continue
        if path.name.startswith('.env') or path.suffix in ('.user', '.pfx', '.pem', '.key'):
            continue
        selected.add(path)
for relative in ('docker-compose.yml', 'docker-compose.dedicated.yml', '.dockerignore', '.env.example', 'src/Database.sql',
                 'scripts/reset-development-database.sh', 'scripts/check-development.sh', 'Doc/Implementation_20261103.md',
                 'My project/Assets/Scripts/Services/Api/ApiClient.cs', 'My project/Assets/Scripts/Multiplayer/DedicatedAdmission.cs',
                 'My project/Assets/Scripts/Progression/QuickCoachingRules.cs'):
    selected.add(root / relative)
for name in ('acceptance-summary.json', 'replay-acceptance.json', 'interruption-acceptance.json', 'client-build.json'):
    selected.add(output / name)
selected.update(p for p in native.rglob('*') if p.is_file() and not p.name.startswith('.unity-pipeline') and not any('BackUpThisFolder' in part for part in p.parts))
def content_for(path):
    if path.parent == root / 'src/Backend/API' and path.name.startswith('appsettings') and path.suffix == '.json':
        config = json.loads(path.read_text(encoding='utf-8-sig'))
        def sanitize(value):
            if not isinstance(value, dict): return value
            return {k: ('' if k.lower() in {'key','password','servicekey','secretkey','accesskey','clientsecret','apikey','username','fromemail','email','connectionstring'} else sanitize(v)) for k,v in value.items()}
        config = sanitize(config)
        for key in ('Username', 'FromEmail'):
            if key in config.get('Smtp', {}): config['Smtp'][key] = ''
        # Compose injects the connection string; never copy a developer machine credential.
        if 'ConnectionStrings' in config:
            config['ConnectionStrings'] = {k: '' for k in config['ConnectionStrings']}
        return (json.dumps(config, ensure_ascii=False, indent=2) + '\n').encode('utf-8')
    return path.read_bytes()
manifest = {
    'release': '20261103-p18', 'protocol': 18, 'unity': '6000.6.2f1',
    'sourceCommit': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip(),
    'workingTreeChangesIncluded': True, 'serverExtraDefines': ['APPUI_PLATFORM_DISABLED'],
    'files': {str(p.relative_to(root)).replace('\\', '/'): hashlib.sha256(content_for(p)).hexdigest() for p in sorted(selected)},
    'build': report,
}
version = output / 'release-manifest.json'
version.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
selected.add(version)
bundle = output / 'PickleVerse-Ubuntu-20261103-p18.tar.gz'
with tarfile.open(bundle, 'w:gz', compresslevel=5) as archive:
    for path in sorted(selected):
        name = str(path.relative_to(root)).replace('\\', '/')
        info = archive.gettarinfo(str(path), arcname=name)
        info.uid = info.gid = 0; info.uname = info.gname = ''
        info.mode = 0o755 if path.name in ('PickleVerseServer', 'UnityCrashHandler64') or path.suffix == '.sh' else 0o644
        content = content_for(path)
        info.size = len(content)
        archive.addfile(info, io.BytesIO(content))
digest = hashlib.sha256(bundle.read_bytes()).hexdigest()
(output / (bundle.name + '.sha256')).write_text(digest + '  ' + bundle.name + '\n', encoding='ascii')
print(json.dumps({'bundle': str(bundle), 'files': len(selected), 'bytes': bundle.stat().st_size, 'sha256': digest}))
