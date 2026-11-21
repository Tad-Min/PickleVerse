# EV-09 – Transport and secret check of 13/11/2026

Read-only checks run from a team PC on 13/11/2026: first between 11:25 and 11:35 (UTC+7) on main @ 1dfefe9 (sections 1 and 2),
then at 16:21 after commit be556a1 was deployed by CI run 30 (section 3). No sign-in and no data change.

## 1. Transport at 11:30 (staging host tad-min.io.vn)

| Request | Result |
|---|---|
| `GET https://tad-min.io.vn/health/ready` | 200, body `{"status":"ready"}` |
| `GET https://tad-min.io.vn/health/live` | 200 |
| `GET https://tad-min.io.vn/api/Game/catalog` (no token) | 401 |
| `GET https://tad-min.io.vn/swagger/index.html` | 200 |
| `GET https://tad-min.io.vn/` | 200, text `PickleVerse API`, header `Server: Caddy` |
| `GET http://tad-min.io.vn/` and `/health/ready` | 308 redirect to the `https://` address |
| `GET http://tad-min.io.vn:8080/health/ready` | 200 over plain HTTP, header `Server: Kestrel` |
| `GET http://tad-min.io.vn:8080/api/Game/catalog` (no token) | 401 over plain HTTP |
| `GET https://tad-min.io.vn/admin`, `/login`, `/index.html` | 200 with an empty body (no admin web page) |
| Ports 3000, 5173, 8081, 8443 | no answer |

TLS certificate on port 443: subject `CN=tad-min.io.vn`, issuer Let's Encrypt (YE1), valid from 12/11/2026 17:14 GMT to
10/02/2027 17:14 GMT, verified against the system trust store; negotiated TLS 1.3.

Findings:

- HTTPS is available for the Web API on port 443 and port 80 redirects to it.
- Port 8080 still serves the API over plain HTTP.
- `My project/Assets/Resources/BackendApiSettings.asset` on main @ 1dfefe9 still has `baseUrl: http://tad-min.io.vn:8080`.
- The admin web is not served under the staging address. `docker-compose.linux.yml` binds the web container to
  `127.0.0.1:3000` by default and the reverse-proxy configuration is not in the repository.

## 2. Secrets in the repository at 11:30 (main @ 1dfefe9)

Method: every `appsettings*.json` tracked by git was parsed and each key whose path contains password, secret, key, token or
connection string was checked for a non-empty value. Values were not printed.

- `src/Backend/API/appsettings.json`: non-empty values for the database connection string, the JWT signing key, the storage
  access and secret keys, the Redis connection string and the SMTP password.
- `src/Backend/API/appsettings.Development.json`: none.
- `src/Backend/API/Program.cs` line 48 still falls back to a literal JWT key when `Jwt:Key` is not configured.
- No file under `src/` changed between 5963501 and 1dfefe9.

Mitigation already in place: `deploy/ci/publish-release.sh` runs `deploy/api/sanitize_settings.py` before the images are built, so the
published images carry no credentials, and the runtime values come from the `.env` file on the host.

Result at 11:30 for TC-N09: not satisfied (D-17 open, D-18 partly fixed).

## 3. Re-check at 16:21 (main @ d9200cc, deployed by CI run 30 at 14:34)

Commit be556a1 (13/11/2026 13:52) changed the client address to `https://tad-min.io.vn`, added `deploy/linux/Caddyfile`, bound the
API and the web container to 127.0.0.1, emptied the credential values in `appsettings.json` and removed the fallback JWT key.

| Request | Result |
|---|---|
| `GET https://tad-min.io.vn/health/ready` | 200, JSON |
| `GET https://tad-min.io.vn/api/Game/catalog` (no token) | 401, JSON |
| `GET https://tad-min.io.vn/` | 200, HTML page "PickleVerse Admin Web Dashboard" (served by the web container through Caddy) |
| `GET http://tad-min.io.vn/` | 308 redirect to the `https://` address |
| `GET http://tad-min.io.vn:8080/health/ready` | no answer (port closed) |
| `GET http://tad-min.io.vn:3000/` | no answer (port closed) |

Repository on main @ d9200cc, same method as section 2:

- `src/Backend/API/appsettings.json`: the database connection string, the JWT key, the storage keys and the SMTP user name and
  password are empty; the only non-empty value that matches the key filter is the Redis connection string, which has no password.
- `src/Backend/API/Program.cs` reads `Jwt:Key` without a fallback and stops when the key is missing or shorter than 32 bytes.
- `My project/Assets/Resources/BackendApiSettings.asset`: `baseUrl: https://tad-min.io.vn`, `allowRemoteHttpInDevelopment: 0`;
  ProjectSettings `insecureHttpOption: 1`.
- The values removed by be556a1 are still present in earlier commits of the Git history. Whether they were rotated on staging
  cannot be checked from the repository.

Result at 16:21: D-18 fixed (HTTPS only for the API and the admin web, port 8080 closed). D-17 partly fixed (working tree clean,
history still contains the old values, rotation to be confirmed). TC-N09 stays failed until the rotation is confirmed and the rest
of the NFR-09 checklist is run.
