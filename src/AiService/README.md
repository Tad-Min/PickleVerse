# PickleVerse Hybrid AI

Internal, stateless FastAPI service. Unity sends completed-session telemetry to ASP.NET; the PostgreSQL job worker invokes this service. Python never writes player data or Elo.

## Run locally

Use Python 3.12 and a dedicated virtual environment. Install `requirements-dev.txt` for development or `requirements.txt` for serving. Dependencies are constrained by `requirements-lock.txt`.

Set `AI_SERVICE_KEY` to a dedicated random secret of at least 32 characters, then:

```powershell
python -m uvicorn app.main:app --host 127.0.0.1 --port 18080 --no-access-log
python -m pytest -q
```

`GET /health` reports process health. `GET /ready` checks service-key configuration and reports rules/model status. Docker healthchecks use `/ready`, so a missing service key makes the container unhealthy. `POST /v1/analyze-session` requires `X-AI-Key`. Docker exposes this service only on the internal Compose network.

The local development activation and in-game testing guide is [AI service testing](../../Doc/AI_Service_Activation_20261031.md).

The shared request fixture is `contracts/hybrid-ai/session-v1.json`. Coordinates use game meters; timing errors are signed seconds. Missing values are unknown. Engine-generated aim and assisted timing do not become human technique errors. `DinkTooHigh` remains disabled unless a calibrated threshold is explicitly supplied with matching game fixtures.

## Offline tier training

No production model or expert-labeled dataset is bundled. Serving uses rule/curriculum fallback by default.

Training input is a JSON list of objects containing `playerId`, `tier` (`beginner`, `intermediate`, `advanced`), `labelSource: "expert"`, and `session` matching the analysis request contract. Labels must be independently assessed. Synthetic records in tests verify plumbing only.

```powershell
python train.py labeled.json models/tier.joblib --version tier-v1 --mode training --assisted true
```

The pipeline splits by player before fitting preprocessing. Artifacts include a SHA-256 manifest, training/holdout player sets, mode, assistance context, feature version and evaluation metrics. Activation requires at least ten holdout players per tier, macro-F1 >= 0.70 and improvement >= 0.05 over majority baseline. Insufficient data never produces an approved model.

Only deploy trusted, locally trained artifacts. Set `AI_ENABLE_MODEL=true` and `AI_MODEL_PATH` to the approved artifact. The adjacent `.json` manifest must be deployed together with the `.joblib`. Docker reads `src/AiService/models/` as a read-only volume. A missing, altered or unapproved artifact leaves tier inference unavailable; rules continue to work.

Inference requires at least 20 observable opportunities, a matching mode/assistance context and confidence >= 0.65. The estimate suggests practice difficulty and drills; it does not change competitive Elo.
