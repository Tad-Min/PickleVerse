"""Generate 30 reproducible software demo sessions with simulated tier labels."""

import argparse
import json
from pathlib import Path
from random import Random

from app.models import AnalyzeRequest

# Scenario parameters, not estimates of human play: timing bias, timing spread,
# wrong input probability, and probability of recovering after contact.
PROFILES = {
    "beginner": (.06, .145, .28, .40),
    "intermediate": (.025, .075, .13, .65),
    "advanced": (.005, .035, .04, .88),
}
SPLITS = ("train",) * 6 + ("calibration",) * 2 + ("test",) * 2


def generate(seed):
    random = Random(seed)
    records = []
    for tier, (bias, spread, wrong_input_rate, recovery_rate) in PROFILES.items():
        for index, split in enumerate(SPLITS, start=1):
            player = f"sim-{tier}-{index:02}"
            timing_bias = random.gauss(bias, .02)
            shots = []
            for sequence in range(24):
                release = round(sequence * 3.5 + random.uniform(.4, .6), 3)
                wrong_input = random.random() < wrong_input_rate
                shot = dict(
                    shotId=f"{player}-volley-{sequence + 1:02}", sequence=sequence, rally=sequence + 1,
                    kind="Volley", technique="Volley", source="simulated-training", time=release,
                    rawPressedAt=round(release - random.uniform(.12, .28), 3), rawReleasedAt=release,
                    rawInputMask=1 if wrong_input else 4, rawTechnique="Drive" if wrong_input else "Volley",
                    rawPosition=[round(random.uniform(-1.1, 1.1), 3), 0.0, round(random.uniform(-3.4, -2.3), 3)],
                    assisted=True, engineAimed=True, outcome="WrongInput" if wrong_input else "Contact",
                )
                if not wrong_input:
                    shot["rawTimingError"] = round(random.gauss(timing_bias, spread), 4)
                    shot["recovered"] = random.random() < recovery_rate
                shots.append(shot)
            session = dict(sessionId=f"{player}-training", mode="training", gameVersion="synthetic-demo-v1", shots=shots)
            AnalyzeRequest.model_validate(session)
            records.append(dict(playerId=player, tier=tier, split=split, labelSource="simulation", synthetic=True, session=session))
    return records


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path)
    parser.add_argument("--seed", type=int, default=42)
    args = parser.parse_args()
    records = generate(args.seed)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(records, indent=2, allow_nan=False) + "\n", encoding="utf-8")
    print(f"Generated {len(records)} simulated sessions and {sum(len(row['session']['shots']) for row in records)} shots.")
