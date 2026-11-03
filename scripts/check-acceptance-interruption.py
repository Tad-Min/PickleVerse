"""Wait for a live native match, capture balances, then verify external worker-stop interruption."""
from pathlib import Path
import json, runpy, subprocess, time, uuid

root = Path(__file__).resolve().parents[1]
artifact = root / 'artifacts/account-multiplayer-ai-20261103'
helpers = runpy.run_path(str(root / 'scripts/check-acceptance-http.py'), run_name='helpers')
api = helpers['api']
deadline = time.monotonic() + 100
game = None
while time.monotonic() < deadline:
    try:
        game = json.loads((artifact / 'game-1v1-0.json').read_text(encoding='utf-8-sig'))
        if game.get('courtStarted'): break
    except (OSError, ValueError): pass
    time.sleep(.2)
if not game or not game.get('courtStarted'): raise RuntimeError('Native interruption fixture did not start')
match = uuid.UUID(game['matchId']).hex
accounts = []
fields = ('coins', 'gems', 'totalXp', 'elo', 'wins', 'losses')
for index in range(2):
    fixture = json.loads((artifact / f'acceptance-private/player-{index}.json').read_text(encoding='utf-8-sig'))
    status, login = api('api/Auth/login', {'email': fixture['email'], 'password': fixture['password']})
    if status != 200: raise RuntimeError('Interruption player login failed')
    token = login['data']['accessToken']
    status, state = api('api/Game/state', token=token)
    player = state['data']['player']
    if status != 200 or (player.get('pendingMatch') or {}).get('id') != match: raise RuntimeError('Player is not pending in the live native match')
    accounts.append((token, {key: player[key] for key in fields}))
(artifact / 'interruption-before.json').write_text(json.dumps({'matchId': match, 'players': [values for _, values in accounts]}, indent=2) + '\n', encoding='utf-8')
worker = helpers['state']['worker']
if worker != 'pkv-acceptance-worker-p18': raise RuntimeError('Unexpected worker identity')
label = subprocess.check_output(['docker', 'inspect', '--format', '{{index .Config.Labels "com.pickleverse.acceptance"}}', worker], text=True).strip()
if label != '20261103-p18': raise RuntimeError('Refusing to stop an unlabelled worker')
subprocess.run(['docker', 'stop', '--time', '3', worker], check=True, stdout=subprocess.DEVNULL)
print('Stopped the verified isolated worker during a live native match; checking lease recovery.', flush=True)
started = time.monotonic()
while time.monotonic() - started < 100:
    results = []
    for token, before in accounts:
        status, state = api('api/Game/state', token=token)
        if status != 200: raise RuntimeError('Interrupted match state cannot be read')
        player = state['data']['player']
        entry = next((item for item in player['history'] if item['id'] == match), None)
        if entry is None: break
        if str(entry['outcome']).lower() not in ('interrupted', '2'): raise RuntimeError('Worker loss inferred an unexpected winner or loser')
        if {key: player[key] for key in fields} != before: raise RuntimeError('Worker loss changed rewards, Elo or win/loss totals')
        if (player.get('pendingMatch') or {}).get('id') == match: break
        results.append(True)
    if len(results) == 2:
        report = {'passed': True, 'matchId': match, 'interruptedPlayers': 2, 'rewardsAndRatingUnchanged': True, 'seconds': round(time.monotonic() - started, 2)}
        (artifact / 'interruption-acceptance.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
        print('PASS: native worker loss interrupts both players without a winner, rewards or rating changes.', flush=True)
        break
    time.sleep(1)
else: raise RuntimeError('Dedicated interruption did not finish within the lease recovery deadline')
