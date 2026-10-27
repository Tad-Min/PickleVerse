from app.models import AnalyzeRequest
from app.tier import TierPredictor
from train import train


def session(timing=.01, count=20):
    return dict(sessionId="test", mode="training", shots=[dict(shotId=str(i), sequence=i,
        kind="Drive", technique="Drive", assisted=False, timingError=timing) for i in range(count)])


def test_training_splits_players_and_artifact_must_pass_gate(tmp_path):
    # Synthetic fixtures exercise the pipeline only; this is never a production model.
    records = [dict(playerId=f"p-{tier}-{i}", tier=tier, labelSource="expert", session=session(timing))
               for tier, timing in [("beginner", .3), ("intermediate", .05), ("advanced", .01)] for i in range(100)]
    output = tmp_path / "model.joblib"
    report = train(records, output, "fixture-only", "training")
    assert not set(report["trainPlayers"]) & set(report["testPlayers"])
    assert report["approved"]
    predictor = TierPredictor(output, True)
    assert predictor.status == "enabled"
    assert predictor.predict(AnalyzeRequest.model_validate(session(count=19)))["status"] == "insufficient_data"
    assert predictor.predict(AnalyzeRequest.model_validate(session()))["status"] == "ready"
    output.write_bytes(output.read_bytes() + b"tamper")
    assert TierPredictor(output, True).status == "unavailable"
