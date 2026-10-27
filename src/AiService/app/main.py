import hmac
import os
from fastapi import Depends, FastAPI, Header, HTTPException
from .engine import analyze
from .models import AnalyzeRequest
from .tier import TierPredictor

app = FastAPI(title="PickleVerse Hybrid AI", docs_url=None, redoc_url=None)
predictor = TierPredictor(os.getenv("AI_MODEL_PATH"), os.getenv("AI_ENABLE_MODEL", "false").lower() == "true")


def authorize(x_ai_key: str = Header(default="")):
    expected = os.getenv("AI_SERVICE_KEY", "")
    if len(expected) < 32 or not hmac.compare_digest(expected, x_ai_key):
        raise HTTPException(401, "Service authentication required")


@app.get("/health")
def health():
    return {"status": "ok"}


@app.get("/ready")
def ready():
    if len(os.getenv("AI_SERVICE_KEY", "")) < 32:
        raise HTTPException(503, "Service key is not configured")
    return {"status": "ready", "ruleVersion": "rules-v1", "model": predictor.status}


@app.post("/v1/analyze-session", dependencies=[Depends(authorize)])
def analyze_session(request: AnalyzeRequest):
    try:
        return analyze(request, predictor.predict)
    except ValueError as error:
        raise HTTPException(422, str(error)) from error
