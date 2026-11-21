import hashlib
import json

from app.calibration import CoachingCalibration
from app.models import AnalyzeRequest
from calibrate_coaching import artifact, build_report


def row(player, split, heights, labels):
    shots = [dict(shotId=f"{player}-{i}", sequence=i, kind="Dink", technique="Dink", netClearance=height)
             for i, height in enumerate(heights)]
    return dict(playerId=player, split=split, labelSource="expert", synthetic=True,
                session=dict(sessionId=player, mode="training", gameVersion="fixture", shots=shots),
                shotLabels=[dict(shotId=shot["shotId"], code="DinkTooHigh", present=value, evidence="Software fixture")
                            for shot, value in zip(shots, labels)])


def test_threshold_is_fit_only_on_calibration_players_and_synthetic_report_cannot_activate(tmp_path):
    calibration = row("calibration", "calibration", [.2, .8], [False, True])
    holdout = row("holdout", "test", [.1, .7], [False, True])
    context = dict(mode="training", gameVersion="fixture", assisted=True, engineAimed=True)
    report = build_report([calibration, holdout], context, ["DinkTooHigh"], "fixture", "fixture-hash")
    changed_holdout = row("holdout", "test", [.9, .1], [False, True])
    other = build_report([calibration, changed_holdout], context, ["DinkTooHigh"], "fixture", "fixture-hash")
    assert report["dinkClearanceThreshold"] == other["dinkClearanceThreshold"]
    assert report["holdout"]["DinkTooHigh"]["precision"] == 1
    assert other["holdout"]["DinkTooHigh"]["recall"] == 0
    unreviewed = artifact(report, None)
    confirmed = artifact(report, dict(expert="Fixture reviewer", reviewerId="fixture",
                         approvedCodes=["DinkTooHigh"], reportSha256=unreviewed["reportSha256"]))
    assert not confirmed["approved"]
    path = tmp_path / "coaching.json"
    path.write_text(json.dumps(confirmed), encoding="utf-8")
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    assert CoachingCalibration(path, digest).status == "unavailable"


def test_unapproved_request_cannot_turn_on_dink_rule():
    request = AnalyzeRequest(sessionId="fixture", mode="training", shots=[], dinkClearanceThreshold=.6)
    assert CoachingCalibration(None, None).bind(request).dinkClearanceThreshold is None


def test_reviewed_calibration_requires_exact_artifact_and_matching_context(tmp_path):
    context = dict(mode="training", gameVersion="fixture", assisted=True, engineAimed=True, referenceId="reference-v1")
    calibration = row("calibration", "calibration", [.2, .8], [False, True])
    holdout = row("holdout", "test", [.1, .7], [False, True])
    for record in (calibration, holdout):
        for shot in record["session"]["shots"]:
            shot["referenceId"] = "reference-v1"
    report = build_report([calibration, holdout], context, ["DinkTooHigh"], "fixture", "fixture-hash")
    # Exercise the loader with a simulated approved manifest kept in the test directory.
    report["realExpertData"] = True
    digest = artifact(report, None)["reportSha256"]
    result = artifact(report, dict(expert="Fixture reviewer", reviewerId="fixture",
                      approvedCodes=["DinkTooHigh"], reportSha256=digest))
    path = tmp_path / "coaching.json"
    path.write_text(json.dumps(result), encoding="utf-8")
    sha = hashlib.sha256(path.read_bytes()).hexdigest()
    assert CoachingCalibration(path, "0" * 64).status == "unavailable"
    loaded = CoachingCalibration(path, sha)
    assert loaded.status == "enabled"
    request = AnalyzeRequest(sessionId="fixture", mode="training", gameVersion="fixture", shots=[])
    assert loaded.bind(request).dinkReferenceId == "reference-v1"
    assert loaded.bind(request.model_copy(update={"gameVersion": "other"})).dinkClearanceThreshold is None
