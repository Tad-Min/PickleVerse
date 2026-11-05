import json
from pathlib import Path
import pytest
from app.engine import analyze
from app.models import AnalyzeRequest
from app.tier import TierPredictor, features

FIXTURE = Path(__file__).resolve().parents[3] / "contracts/hybrid-ai/session-v1.json"


def request(shots):
    return AnalyzeRequest(sessionId="test", mode="training", shots=shots,
                          drills=[dict(id="serve", title="Serve", skill="Serve"),
                                  dict(id="dink", title="Dink", skill="Dink", prerequisites=["serve"])])


def test_shared_contract():
    result = analyze(AnalyzeRequest.model_validate_json(FIXTURE.read_text(encoding="utf-8")))
    assert {f["code"] for f in result["findings"]} == {"LateServe", "Kitchen"}
    assert result["recommendations"][0]["id"] == "serve"


def test_signed_timing_and_missing_metrics():
    result = analyze(request([dict(shotId="early", sequence=0, kind="Drive", technique="Drive", assisted=False, timingError=-.1),
                              dict(shotId="late", sequence=1, kind="Drive", technique="Drive", assisted=False, timingError=.1),
                              dict(shotId="unknown", sequence=2, kind="Drive", technique="Drive")]))
    assert [(f["shotId"], f["code"]) for f in result["findings"]] == [("early", "Early"), ("late", "Late")]
    assert all(m["opportunities"] == 2 for m in result["metrics"])


def test_kitchen_only_for_volley_and_controlled_aim_not_an_error():
    result = analyze(request([dict(shotId="legal", sequence=0, kind="Dink", technique="Dink", kitchen=True, volley=False,
                                  engineAimed=True, targetHit=False, netClearance=9, assisted=True, timingError=1)]))
    assert result["findings"] == []


def test_dink_threshold_requires_calibration_and_independent_aim():
    r = request([dict(shotId="dink", sequence=0, kind="Dink", technique="Dink", engineAimed=False, netClearance=.9)])
    assert analyze(r)["findings"] == []
    r.dinkClearanceThreshold = .5
    assert analyze(r)["findings"][0]["code"] == "DinkTooHigh"


def test_recommendations_need_five_opportunities_and_prerequisites():
    shots = [dict(shotId=str(i), sequence=i, kind="Dink", technique="Dink", assisted=False, timingError=.1) for i in range(5)]
    r = request(shots)
    assert analyze(r)["recommendations"][0]["id"] == "serve"
    r.completedLessons = ["serve"]
    assert analyze(r)["recommendations"][0]["id"] == "dink"


def test_duplicate_shots_rejected():
    with pytest.raises(ValueError, match="duplicate"):
        analyze(request([dict(shotId="x", sequence=i, kind="Drive", technique="Drive") for i in range(2)]))


def test_tier_suggests_appropriate_drill_but_active_lesson_keeps_priority():
    r = request([])
    from app.models import Drill
    r.drills.append(Drill(id="smash", title="Smash", skill="Smash", tier=2))
    predictor = lambda _: dict(status="ready", tier="advanced", confidence=.9, opportunities=20)
    assert analyze(r, predictor)["recommendations"][0]["id"] == "smash"
    r.resumeActive = True
    assert analyze(r, predictor)["recommendations"][0]["id"] == "serve"


def test_model_disabled_and_missing_artifact_are_safe():
    r = request([])
    assert TierPredictor(None, False).predict(r)["status"] == "model_disabled"
    assert TierPredictor("missing.joblib", True).predict(r)["status"] == "model_unavailable"


def test_clean_session_gets_measured_summary_without_inventing_a_fault():
    r = request([dict(shotId=str(i), sequence=i, kind="Serve" if i == 0 else "Drive", technique="Drive",
                      outcome="Contact", contact=[0, 1, 2]) for i in range(4)])
    result = analyze(r)
    assert result["findings"] == []
    assert "4 lượt đánh, 4 lần tiếp xúc bóng và 1 lượt giao bóng" in result["summary"]
    assert "Chưa ghi nhận lỗi trong các tiêu chí đã đo" in result["summary"]


def test_short_session_reports_observed_misses_without_lowering_recommendation_gate():
    r = request([dict(shotId="x", sequence=0, kind="Drive", technique="Drive", outcome="Miss")])
    result = analyze(r)
    assert [f["code"] for f in result["findings"]] == ["Miss"]
    assert "Đánh hụt 1/1 lần quan sát" in result["summary"]
    assert result["recommendations"][0]["id"] == "serve"


def test_empty_and_unobserved_sessions_do_not_claim_good_technique():
    assert "chưa ghi nhận được cú đánh" in analyze(request([]))["summary"]
    result = analyze(request([dict(shotId="x", sequence=0, kind="Drive", technique="Drive")]))
    assert "Chưa ghi nhận lần tiếp xúc bóng" in result["summary"]
    assert result["findings"] == []


def test_bot_serve_miss_is_not_lost_by_the_rally_outcome_name():
    result = analyze(request([dict(shotId="x", sequence=0, kind="Serve", technique="Serve", outcome="ServeMiss")]))
    assert [f["code"] for f in result["findings"]] == ["Miss"]
    assert "Đánh hụt 1/1" in result["summary"]


def test_unclassified_referee_fault_does_not_get_a_clean_session_message():
    result = analyze(request([dict(shotId="x", sequence=0, kind="Drive", technique="Drive", outcome="Fault", contact=[0, 1, 2])]))
    assert "1 lượt ghi nhận vi phạm luật" in result["summary"]
    assert "Chưa ghi nhận lỗi" not in result["summary"]


def test_assisted_timing_and_generated_target_not_classifier_features():
    r = request([dict(shotId="x", sequence=0, kind="Drive", technique="Drive", assisted=True,
                      engineAimed=True, timingError=.9, targetHit=False)])
    _, count = features(r)
    assert count == 0


def test_http_auth_and_validation(monkeypatch):
    from fastapi.testclient import TestClient
    from app.main import app
    monkeypatch.setenv("AI_SERVICE_KEY", "k" * 32)
    client = TestClient(app)
    assert client.get("/health").status_code == 200
    payload = request([]).model_dump()
    assert client.post("/v1/analyze-session", json=payload).status_code == 401
    assert client.post("/v1/analyze-session", json=payload, headers={"X-AI-Key": "k" * 32}).status_code == 200
    payload["ruleVersion"] = "unknown"
    assert client.post("/v1/analyze-session", json=payload, headers={"X-AI-Key": "k" * 32}).status_code == 422
