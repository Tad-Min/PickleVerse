from app.models import AnalyzeRequest
from app.tier import FEATURES, TierPredictor, features
from simulate_training import generate
from train import train
import numpy as np
import pytest


def session(timing=.01, count=20):
    return dict(sessionId="test", mode="training", shots=[dict(shotId=str(i), sequence=i,
        kind="Drive", technique="Drive", assisted=False, timingError=timing) for i in range(count)])


def test_training_splits_players_and_artifact_must_pass_gate(tmp_path):
    # Synthetic fixtures exercise the pipeline only; this is never a production model.
    records = [dict(playerId=f"p-{tier}-{i}", tier=tier, labelSource="simulation", synthetic=True, session=session(timing))
               for tier, timing in [("beginner", .3), ("intermediate", .05), ("advanced", .01)] for i in range(100)]
    output = tmp_path / "model.joblib"
    report = train(records, output, "fixture-only", "training")
    assert not set(report["trainPlayers"]) & set(report["testPlayers"])
    assert not set(report["calibrationPlayers"]) & set(report["testPlayers"])
    assert not set(report["calibrationPlayers"]) & set(report["trainPlayers"])
    assert all(count >= 10 for count in report["holdoutPlayers"].values())
    assert report["labelSource"] == "simulation"
    assert not report["realExpertData"]
    assert not report["approved"]
    predictor = TierPredictor(output, True)
    assert predictor.status == "unavailable"

    records[0]["labelSource"] = "expert"
    with pytest.raises(ValueError, match="separate dataset"):
        train(records, output, "mixed-fixture", "training")


def test_runtime_requires_context_opportunities_and_calibrated_confidence():
    class SoftwareFixture:
        classes_ = np.asarray(["advanced", "beginner", "intermediate"])
        confidence = .64

        def predict_proba(self, _):
            return np.asarray([[self.confidence, 1 - self.confidence, 0]])

    predictor = TierPredictor(None, False)
    predictor.model = SoftwareFixture()
    predictor.manifest = dict(mode="training", assisted=False, version="fixture")
    assert predictor.predict(AnalyzeRequest.model_validate(session(count=19)))["status"] == "insufficient_data"
    assert predictor.predict(AnalyzeRequest.model_validate(session()))["status"] == "insufficient_data"
    predictor.model.confidence = .71
    assert predictor.predict(AnalyzeRequest.model_validate(session()))["status"] == "ready"
    wrong_context = AnalyzeRequest.model_validate(session()).model_copy(update={"mode": "match"})
    assert predictor.predict(wrong_context)["status"] == "insufficient_data"
    later = AnalyzeRequest.model_validate(session()).model_copy(update={"useInitialTrainingEvidence": True})
    assert predictor.predict(later)["status"] == "insufficient_data"


def test_demo_trains_with_two_calibration_players_per_tier(tmp_path):
    records = generate(42)
    assert len(records) == len({row["playerId"] for row in records}) == 30
    assert all(len(row["session"]["shots"]) == 24 for row in records)
    report = train(records, tmp_path / "tier-demo.joblib", "fixture", "training", assisted=True)
    assert report["calibrationFolds"] == 2
    assert set(report["holdoutPlayers"].values()) == {2}
    assert not report["approved"]


def test_initial_serve_trials_count_observed_input_without_inventing_contact_timing():
    shots = [dict(shotId=str(i), sequence=i, kind="Serve", technique="Serve", assisted=True,
                  rawReleasedAt=float(i), rawInputMask=1, outcome="Contact") for i in range(19)]
    shots.append(dict(shotId="timeout", sequence=19, kind="Serve", technique="Serve", outcome="LateServe"))
    request = AnalyzeRequest.model_validate(dict(sessionId="serve", mode="training", shots=shots))
    values, opportunities = features(request, True)
    assert opportunities == 20
    assert np.isnan(values[FEATURES.index("mean_abs_timing")])
    assert values[FEATURES.index("input_error_rate")] == .05
