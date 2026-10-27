"""Offline only: python train.py labeled.json output/model.joblib --version tier-v1 --mode training.

Input: [{playerId, tier, labelSource: "expert", session: AnalyzeRequest}, ...].
Synthetic records belong in tests; rejected artifacts remain disabled by the serving gate.
"""
import argparse
import hashlib
import json
from pathlib import Path
import joblib
import numpy as np
from sklearn.dummy import DummyClassifier
from sklearn.impute import SimpleImputer
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import f1_score
from sklearn.model_selection import GroupShuffleSplit
from sklearn.pipeline import make_pipeline
from sklearn.preprocessing import StandardScaler
from app.models import AnalyzeRequest
from app.tier import FEATURES, TIERS, features


def train(records, output, version, mode, assisted=False):
    eligible = []
    player_labels = {}
    for row in records:
        if row.get("labelSource") != "expert" or row.get("tier") not in TIERS:
            raise ValueError("Independent expert labels required")
        if row["playerId"] in player_labels and player_labels[row["playerId"]] != row["tier"]:
            raise ValueError("Conflicting labels for the same player")
        player_labels[row["playerId"]] = row["tier"]
        session = AnalyzeRequest.model_validate(row["session"])
        x, count = features(session, assisted)
        if session.mode == mode and count >= 20:
            eligible.append((row["playerId"], row["tier"], x))
    if len({r[0] for r in eligible}) < 6:
        raise ValueError("Insufficient independently labeled players")
    groups, labels, x = zip(*eligible)
    x, labels, groups = np.asarray(x), np.asarray(labels), np.asarray(groups)
    train_index, test_index = next(GroupShuffleSplit(n_splits=1, test_size=.25, random_state=42).split(x, labels, groups))
    if set(labels[train_index]) != TIERS:
        raise ValueError("Training split must contain all tiers")
    pipeline = make_pipeline(SimpleImputer(strategy="median", add_indicator=True, keep_empty_features=True),
                             StandardScaler(), LogisticRegression(class_weight="balanced", max_iter=1000, random_state=42))
    pipeline.fit(x[train_index], labels[train_index])
    baseline = DummyClassifier(strategy="most_frequent").fit(x[train_index], labels[train_index])
    score = float(f1_score(labels[test_index], pipeline.predict(x[test_index]), labels=sorted(TIERS), average="macro", zero_division=0))
    baseline_score = float(f1_score(labels[test_index], baseline.predict(x[test_index]), labels=sorted(TIERS), average="macro", zero_division=0))
    counts = {tier: len(set(groups[test_index][labels[test_index] == tier])) for tier in TIERS}
    output = Path(output)
    output.parent.mkdir(parents=True, exist_ok=True)
    joblib.dump(pipeline, output)
    manifest = dict(version=version, mode=mode, assisted=assisted, features=FEATURES, labelSource="expert", macroF1=score,
                    majorityF1=baseline_score, holdoutPlayers=counts,
                    trainPlayers=sorted(set(groups[train_index])), testPlayers=sorted(set(groups[test_index])),
                    approved=min(counts.values()) >= 10 and score >= .70 and score >= baseline_score + .05,
                    sha256=hashlib.sha256(output.read_bytes()).hexdigest())
    output.with_suffix(".json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    return manifest


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("data")
    parser.add_argument("output")
    parser.add_argument("--version", required=True)
    parser.add_argument("--mode", default="training")
    parser.add_argument("--assisted", choices=["true", "false"], default="true")
    args = parser.parse_args()
    report = train(json.loads(Path(args.data).read_text(encoding="utf-8")), args.output, args.version, args.mode, args.assisted == "true")
    print(json.dumps({k: v for k, v in report.items() if k not in ("trainPlayers", "testPlayers")}, indent=2))
