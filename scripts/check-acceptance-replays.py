"""Read real dedicated replay chunks through authenticated API, without printing credentials."""
from pathlib import Path
import base64, gzip, hashlib, json, runpy

root = Path(__file__).resolve().parents[1]
helpers = runpy.run_path(str(root / 'scripts/check-acceptance-http.py'), run_name='helpers')
api = helpers['api']
artifact = root / 'artifacts/account-multiplayer-ai-20261103'
fixture = json.loads((artifact / 'acceptance-private/player-0.json').read_text(encoding='utf-8-sig'))
status, login = api('api/Auth/login', {'email': fixture['email'], 'password': fixture['password']})
if status != 200: raise RuntimeError('Replay test player login failed')
token = login['data']['accessToken']
reports = []
for name in ('game-1v1-baseline-0.json', 'game-2v2-0.json'):
    game = json.loads((artifact / name).read_text(encoding='utf-8-sig'))
    match = game['matchId']
    status, response = api('api/Game/replays/' + match, token=token)
    if status != 200: raise RuntimeError('Dedicated replay metadata cannot be read')
    replay = response['data']
    if not replay['complete'] or not replay['chunks']: raise RuntimeError('Dedicated replay is incomplete')
    expected = 2 if '1v1' in name else 4
    if len(replay['actors']) != expected: raise RuntimeError('Replay actor roster differs from the match')
    positions = [set() for _ in range(expected)]
    frames = 0
    for index, chunk in enumerate(replay['chunks']):
        status, payload = api(f'api/Game/replays/{match}/chunks/{index}', token=token)
        if status != 200: raise RuntimeError('Dedicated replay chunk cannot be read')
        compressed = base64.b64decode(payload['data']['data'])
        if hashlib.sha256(compressed).hexdigest().upper() != chunk['contentHash'].upper(): raise RuntimeError('Replay chunk hash mismatch')
        decoded = json.loads(gzip.decompress(compressed))
        frames += len(decoded.get('frames', []))
        for frame in decoded.get('frames', []):
            if len(frame['actors']) != expected: raise RuntimeError('Replay frame actor count differs from the roster')
            for actor, pose in enumerate(frame['actors']): positions[actor].add(tuple(round(v, 3) for v in pose['root'][:3]))
    if frames == 0: raise RuntimeError('Dedicated replay contains no recorded frames')
    moving = sum(len(values) > 1 for values in positions)
    if moving != expected: raise RuntimeError('Replay did not capture movement for every test player')
    reports.append({'matchId': match, 'complete': True, 'chunks': len(replay['chunks']), 'frames': frames, 'movingActors': moving, 'hashesVerified': True})
    print('PASS: dedicated replay complete and all chunks readable; frames=', frames)
(artifact / 'replay-acceptance.json').write_text(json.dumps({'passed': True, 'matches': reports}, indent=2) + '\n', encoding='utf-8')
