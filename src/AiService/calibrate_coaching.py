"""Evaluate coaching against independent labels from the admin dataset export."""
import argparse
import hashlib
import json
from collections import defaultdict
from pathlib import Path

from app.calibration import metrics_pass, report_digest
from app.engine import TIPS, observations
from app.models import AnalyzeRequest
from train import expert_provenance


def evaluate(rows, threshold):
    positive = sum(label for _, label, _ in rows)
    true_positive = false_positive = observed = 0
    for shot, label, code in rows:
        values = {name: failed for name, failed, _ in observations(shot, threshold)}
        observed += code in values
        if values.get(code, False):
            true_positive += label
            false_positive += not label
    predicted = true_positive + false_positive
    return dict(
        precision=true_positive / predicted if predicted else 0,
        recall=true_positive / positive if positive else 0,
        positive=positive, negative=len(rows) - positive, observed=observed,
        truePositive=true_positive, falsePositive=false_positive,
    )


def fit_dink_threshold(calibration_rows):
    heights = sorted({shot.netClearance for shot, _, _ in calibration_rows if shot.netClearance is not None and shot.netClearance >= 0})
    truth = {present for _, present, _ in calibration_rows}
    if not heights or truth != {True, False}:
        raise ValueError("Dink calibration needs independently labelled positive and negative trajectories")

    def rank(threshold):
        metrics = evaluate(calibration_rows, threshold)
        precision, recall = metrics["precision"], metrics["recall"]
        f1 = 2 * precision * recall / (precision + recall) if precision + recall else 0
        return metrics_pass(metrics), f1, precision, recall, -threshold

    return max([0.0] + heights, key=rank)


def build_report(records, context, codes, version, dataset_sha256):
    if "DinkTooHigh" not in codes or not set(codes) <= TIPS.keys():
        raise ValueError("Choose supported error codes including DinkTooHigh")
    labels = {split: defaultdict(list) for split in ("calibration", "test")}
    participants = {split: set() for split in labels}
    partition_by_player = {}
    seen = set()
    real_expert_data = True
    for row in records:
        player, split = row["playerId"], row["split"]
        if row.get("labelSource") != "expert" or split not in {"train", "calibration", "test"}:
            raise ValueError("Expert-labelled sessions with pre-assigned player partitions are required")
        if partition_by_player.setdefault(player, split) != split:
            raise ValueError("The same player cannot appear in different partitions")
        if split not in labels:
            continue
        session = AnalyzeRequest.model_validate(row["session"])
        if session.mode != context["mode"] or session.gameVersion != context["gameVersion"]:
            continue
        shots = {shot.shotId: shot for shot in session.shots}
        for label in row.get("shotLabels", []):
            code, present = label["code"], label["present"]
            if code not in codes:
                continue
            shot = shots.get(label["shotId"])
            if shot is None or not isinstance(present, bool) or not label.get("evidence"):
                raise ValueError("Each label must refer to an observed shot and contain evidence")
            if shot.assisted != context["assisted"] or shot.engineAimed != context["engineAimed"]:
                continue
            if context.get("referenceId") and shot.referenceId != context["referenceId"]:
                continue
            key = player, session.sessionId, shot.shotId, code
            if key in seen:
                raise ValueError("Duplicate shot label. Export the latest review once per session.")
            seen.add(key)
            real_expert_data &= expert_provenance(row)
            participants[split].add(player)
            labels[split][code].append((shot, present, code))

    threshold = fit_dink_threshold(labels["calibration"]["DinkTooHigh"])
    holdout = {code: evaluate(labels["test"][code], threshold) for code in codes}
    return dict(
        version=version, ruleVersion="rules-v1", context=context, datasetSha256=dataset_sha256,
        realExpertData=real_expert_data, dinkClearanceThreshold=threshold, holdout=holdout,
        calibrationPlayers=sorted(participants["calibration"]), testPlayers=sorted(participants["test"]),
        tips={code: TIPS[code][1] for code in codes},
    )


def artifact(report, review):
    digest = report_digest(report)
    reviewed = bool(
        review and review.get("reportSha256") == digest and review.get("reviewerId")
        and isinstance(review.get("expert"), str) and review["expert"].strip()
        and set(review.get("approvedCodes", [])) == set(report["holdout"])
    )
    return dict(
        report=report, reportSha256=digest, review=review,
        approved=bool(reviewed and report["realExpertData"] and all(metrics_pass(x) for x in report["holdout"].values())),
    )


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("data")
    parser.add_argument("output")
    parser.add_argument("--version", required=True)
    parser.add_argument("--game-version", required=True)
    parser.add_argument("--reference-id")
    parser.add_argument("--assisted", choices=["true", "false"], default="true")
    parser.add_argument("--engine-aimed", choices=["true", "false"], default="true")
    parser.add_argument("--codes", nargs="+", default=sorted(TIPS))
    parser.add_argument("--review-file")
    args = parser.parse_args()
    payload = Path(args.data).read_bytes()
    context = dict(
        mode="training", gameVersion=args.game_version, referenceId=args.reference_id,
        assisted=args.assisted == "true", engineAimed=args.engine_aimed == "true",
    )
    report = build_report(json.loads(payload), context, sorted(set(args.codes)), args.version, hashlib.sha256(payload).hexdigest())
    review = json.loads(Path(args.review_file).read_text(encoding="utf-8")) if args.review_file else None
    result = artifact(report, review)
    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, ensure_ascii=False, indent=2, allow_nan=False), encoding="utf-8")
    print(json.dumps(dict(approved=result["approved"], reportSha256=result["reportSha256"],
                          artifactSha256=hashlib.sha256(output.read_bytes()).hexdigest(), holdout=report["holdout"]), indent=2))
