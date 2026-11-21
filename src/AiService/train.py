"""Fit a tier candidate from independent expert-reviewed Training sessions.

A person contributes one feature vector from their first reviewed Training sessions.
Synthetic and legacy records can exercise training, but cannot approve an artifact.
"""
import argparse
import hashlib
import json
from collections import defaultdict
from pathlib import Path

import joblib
import numpy as np
from sklearn.calibration import CalibratedClassifierCV
from sklearn.dummy import DummyClassifier
from sklearn.frozen import FrozenEstimator
from sklearn.impute import SimpleImputer
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import f1_score
from sklearn.pipeline import make_pipeline
from sklearn.preprocessing import StandardScaler

from app.models import AnalyzeRequest
from app.tier import FEATURES, FEATURE_VERSION, MIN_CONFIDENCE, MIN_OPPORTUNITIES, TIERS, features

PARTITIONS = ("train", "calibration", "test")


def expert_provenance(row):
    return (
        row.get("synthetic") is False and row.get("labelSource") == "expert"
        and isinstance(row.get("expert"), str) and bool(row["expert"].strip())
        and bool(row.get("reviewerId")) and type(row.get("labelVersion")) is int
        and row["labelVersion"] > 0 and bool(row.get("observation"))
    )


def player_samples(records, mode, assisted):
    by_player = defaultdict(list)
    for row in records:
        source = row.get("labelSource")
        labelled = source == "expert" or (source == "simulation" and row.get("synthetic") is True)
        if not labelled or row.get("tier") not in TIERS or not row.get("playerId"):
            raise ValueError("An expert or explicitly simulated label and a player identifier are required")
        session = AnalyzeRequest.model_validate(row["session"])
        if session.mode == mode:
            by_player[row["playerId"]].append((row, session))
    samples = []
    for player, entries in sorted(by_player.items()):
        tiers = {row["tier"] for row, _ in entries}
        splits = {row.get("split") for row, _ in entries}
        if len(tiers) != 1 or len(splits) != 1:
            raise ValueError(f"Conflicting tier or partition for player {player}")
        split = next(iter(splits))
        if split is not None and split not in PARTITIONS:
            raise ValueError("Unknown partition")
        entries.sort(key=lambda item: item[0].get("completedAt", ""))
        first = entries[:3]
        shots = [shot for _, session in first for shot in session.onboardingShots + session.shots]
        context = first[0][1].model_copy(update={"shots": shots, "onboardingShots": [], "useInitialTrainingEvidence": False})
        values, opportunities = features(context, assisted)
        if opportunities >= MIN_OPPORTUNITIES:
            samples.append((player, next(iter(tiers)), split, values, all(expert_provenance(row) for row, _ in first)))
    return samples


def partition(samples):
    specified = [row[2] is not None for row in samples]
    if any(specified) and not all(specified):
        raise ValueError("Assign every player a partition or leave every partition unset")
    groups = {name: [] for name in PARTITIONS}
    if all(specified):
        for index, row in enumerate(samples):
            groups[row[2]].append(index)
    else:
        random = np.random.default_rng(42)
        for tier in sorted(TIERS):
            indices = [i for i, row in enumerate(samples) if row[1] == tier]
            if len(indices) < 4:
                raise ValueError("Each tier needs a training player, two calibration players and a holdout player")
            random.shuffle(indices)
            holdout = max(1, len(indices) // 4)
            calibration = max(2, len(indices) // 5)
            groups["test"].extend(indices[:holdout])
            groups["calibration"].extend(indices[holdout:holdout + calibration])
            groups["train"].extend(indices[holdout + calibration:])
    for name, indices in groups.items():
        if {samples[i][1] for i in indices} != TIERS:
            raise ValueError(f"{name} must contain all three tiers after filtering for observable shots")
    return groups


def train(records, output, version, mode, assisted=False, dataset_sha256=None):
    if mode != "training":
        raise ValueError("The initial skill model is defined for Training sessions")
    simulation = any(row.get("labelSource") == "simulation" for row in records)
    if simulation and not all(row.get("labelSource") == "simulation" and row.get("synthetic") is True for row in records):
        raise ValueError("Simulated records must be trained in a separate dataset")
    samples = player_samples(records, mode, assisted)
    groups = partition(samples)
    players = np.asarray([row[0] for row in samples])
    labels = np.asarray([row[1] for row in samples])
    values = np.asarray([row[3] for row in samples])
    fit, calibration, test = (groups[name] for name in PARTITIONS)
    _, calibration_counts = np.unique(labels[calibration], return_counts=True)
    calibration_folds = min(5, int(calibration_counts.min()))
    if calibration_folds < 2:
        raise ValueError("Calibration needs at least two players per tier")

    classifier = make_pipeline(
        SimpleImputer(strategy="median", add_indicator=True, keep_empty_features=True),
        StandardScaler(),
        LogisticRegression(class_weight="balanced", max_iter=1000, random_state=42),
    )
    classifier.fit(values[fit], labels[fit])
    calibrated = CalibratedClassifierCV(FrozenEstimator(classifier), method="sigmoid", cv=calibration_folds)
    calibrated.fit(values[calibration], labels[calibration])
    baseline = DummyClassifier(strategy="most_frequent").fit(values[fit], labels[fit])

    probabilities = calibrated.predict_proba(values[test])
    confident = probabilities.max(axis=1) >= MIN_CONFIDENCE
    predicted = np.where(confident, calibrated.classes_[probabilities.argmax(axis=1)], "abstain")
    score = float(f1_score(labels[test], predicted, labels=sorted(TIERS), average="macro", zero_division=0))
    baseline_score = float(f1_score(labels[test], baseline.predict(values[test]), labels=sorted(TIERS), average="macro", zero_division=0))
    counts = {tier: int((labels[test] == tier).sum()) for tier in sorted(TIERS)}
    real = all(row[4] for row in samples)

    output = Path(output)
    output.parent.mkdir(parents=True, exist_ok=True)
    joblib.dump(calibrated, output)
    source_hash = dataset_sha256 or hashlib.sha256(json.dumps(records, sort_keys=True, ensure_ascii=False).encode()).hexdigest()
    manifest = dict(
        version=version, mode=mode, assisted=assisted, features=FEATURES, featureVersion=FEATURE_VERSION,
        labelSource="simulation" if simulation else "expert", realExpertData=real,
        minimumConfidence=MIN_CONFIDENCE, minimumOpportunities=MIN_OPPORTUNITIES, calibrationFolds=calibration_folds,
        macroF1=score, majorityF1=baseline_score, coverage=float(confident.mean()), holdoutPlayers=counts,
        trainPlayers=players[fit].tolist(), calibrationPlayers=players[calibration].tolist(), testPlayers=players[test].tolist(),
        approved=real and min(counts.values()) >= 10 and score >= .70 and score >= baseline_score + .05,
        datasetSha256=source_hash, sha256=hashlib.sha256(output.read_bytes()).hexdigest(),
    )
    output.with_suffix(".json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    return manifest


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("data")
    parser.add_argument("output")
    parser.add_argument("--version", required=True)
    parser.add_argument("--mode", default="training", choices=["training"])
    parser.add_argument("--assisted", choices=["true", "false"], default="true")
    args = parser.parse_args()
    data = Path(args.data).read_bytes()
    report = train(json.loads(data), args.output, args.version, args.mode, args.assisted == "true", hashlib.sha256(data).hexdigest())
    print(json.dumps({key: value for key, value in report.items() if not key.endswith("Players") or key == "holdoutPlayers"}, indent=2))
