"""Create disposable verified players for native client/server smoke tests only."""
from pathlib import Path
import json, runpy, secrets

root = Path(__file__).resolve().parents[1]
helpers = runpy.run_path(str(root / 'scripts/check-acceptance-http.py'), run_name='helpers')
api, otp = helpers['api'], helpers['otp']
private = root / 'artifacts/account-multiplayer-ai-20261103/acceptance-private'
for index in range(4):
    email = 'game-' + secrets.token_hex(8) + '@pickleverse.invalid'
    password = secrets.token_urlsafe(24) + '!Aa1'
    status, body = api('api/Auth/register', {'email': email, 'password': password, 'confirmPassword': password})
    if status != 200 or not body['data']['success']:
        raise RuntimeError('Disposable game player registration failed')
    code = otp(email, 'Mã xác thực')
    if api('api/Auth/verify-email', {'email': email, 'token': code})[0] != 200:
        raise RuntimeError('Disposable game player verification failed')
    (private / f'player-{index}.json').write_text(json.dumps({'email': email, 'password': password, 'baseUrl': helpers['state']['apiUrl']}), encoding='utf-8')
print('Prepared four disposable verified game players; credentials stay in acceptance-private.')
