import hashlib
import hmac
import json
import math
from pathlib import Path

from .engine import TIPS


def report_digest(report):
    encoded = json.dumps(report, ensure_ascii=False, sort_keys=True, separators=(",", ":"), allow_nan=False)
    return hashlib.sha256(encoded.encode("utf-8")).hexdigest()


def metrics_pass(metrics):
    precision, recall = metrics["precision"], metrics["recall"]
    return (
        math.isfinite(precision) and math.isfinite(recall)
        and .80 <= precision <= 1 and .70 <= recall <= 1
        and metrics["positive"] > 0 and metrics["negative"] > 0
    )


class CoachingCalibration:
    def __init__(self, path, expected_sha):
        self.status, self.report = "disabled", None
        if not path or not expected_sha:
            return
        self.status = "unavailable"
        try:
            payload = Path(path).read_bytes()
            if not hmac.compare_digest(hashlib.sha256(payload).hexdigest(), expected_sha.lower()):
                return
            artifact = json.loads(payload)
            report, review = artifact["report"], artifact["review"]
            context, metrics = report["context"], report["holdout"]
            fit, holdout = set(report["calibrationPlayers"]), set(report["testPlayers"])
            threshold = report["dinkClearanceThreshold"]
            if (artifact["approved"] is not True or report["realExpertData"] is not True
                    or report["ruleVersion"] != "rules-v1" or context["mode"] != "training"
                    or not context["gameVersion"] or not isinstance(context["assisted"], bool)
                    or not isinstance(context["engineAimed"], bool) or not fit or not holdout or fit & holdout
                    or "DinkTooHigh" not in metrics or not math.isfinite(threshold) or threshold < 0
                    or not all(metrics_pass(value) for value in metrics.values())
                    or review["reportSha256"] != report_digest(report) or not review["expert"].strip()
                    or not review["reviewerId"] or set(review["approvedCodes"]) != set(metrics)
                    or report["tips"] != {code: TIPS[code][1] for code in metrics}):
                return
            self.report, self.status = report, "enabled"
        except (OSError, ValueError, TypeError, KeyError, AttributeError):
            return

    def bind(self, request):
        fields = dict(dinkClearanceThreshold=None, dinkAssisted=None, dinkEngineAimed=None, dinkReferenceId=None)
        if self.report is not None:
            context = self.report["context"]
            if request.gameVersion == context["gameVersion"] and request.mode == context["mode"]:
                fields.update(
                    dinkClearanceThreshold=self.report["dinkClearanceThreshold"],
                    dinkAssisted=context["assisted"], dinkEngineAimed=context["engineAimed"],
                    dinkReferenceId=context.get("referenceId"),
                )
        return request.model_copy(update=fields)
