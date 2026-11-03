from pathlib import Path
import json

root = Path(__file__).resolve().parents[1]
artifact = root / 'artifacts/account-multiplayer-ai-20261103'
def read(name): return json.loads((artifact / name).read_text(encoding='utf-8-sig'))
runs = {}
for name, count in (('game-1v1-baseline', 2), ('game-2v2', 4), ('game-1v1-reconnect', 2), ('game-shared-1v1', 2), ('game-shared-2v2', 4), ('game-1v1', 2)):
    clients = [read(f'{name}-{index}.json') for index in range(count)]
    if any(not entry.get('passed') or entry.get('runtimeExceptions') != 0 for entry in clients):
        raise RuntimeError('An expected final runtime check did not pass: ' + name)
    runs[name] = {'passed': True, 'clients': count, 'matchId': clients[0]['matchId'], 'runtimeExceptions': 0,
                  'serverLoss': name == 'game-1v1', 'reconnected': any(c.get('reconnected') for c in clients)}
editor = read('unity-tests-final.json')['data']['result']['Summary']
if editor['Failed'] or editor['Passed'] != editor['Total']: raise RuntimeError('Editor tests did not all pass')
replay, interruption = read('replay-acceptance.json'), read('interruption-acceptance.json')
if not replay['passed'] or not interruption['passed']: raise RuntimeError('Replay/interruption checks did not pass')
stats = read('dedicated-runtime-stats.json')
report = {'release': '20261103-p18', 'protocol': 18, 'editor': editor, 'runtime': runs, 'replays': replay,
          'interruption': interruption, 'dockerSamples': len(stats),
          'dockerCpuPercent': {'min': min(float(s['stats']['CPUPerc'].rstrip('%')) for s in stats), 'max': max(float(s['stats']['CPUPerc'].rstrip('%')) for s in stats)},
          'scope': 'Windows headless clients and Linux worker on local Docker Desktop; intentional forfeit and interruption tests',
          'remaining': ['two physical devices/networks with video and logs', 'full scored rallies/contact/court rules', 'Ubuntu CPU/RAM/tick and Direct/NAT', 'real external SMTP delivery']}
(artifact / 'acceptance-summary.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print('Final acceptance summary saved; all listed checks passed within their stated scope.')
