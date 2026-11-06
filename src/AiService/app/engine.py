from collections import defaultdict
from .models import AnalyzeRequest, Shot, observed_timing

TIPS = {
    "Early": (1, "Thả nút muộn hơn để vợt đến bóng đúng thời điểm."),
    "Late": (1, "Chuẩn bị vợt và thả nút sớm hơn; di chuyển vào vị trí đón bóng."),
    "LateServe": (1, "Chuẩn bị giao bóng sớm và tiếp xúc trong cửa sổ thời điểm."),
    "Kitchen": (3, "Đưa cả hai chân ra ngoài vùng cấm vô lê trước khi vô lê."),
    "Momentum": (3, "Giữ thăng bằng sau vô lê, tránh để đà đưa chân vào vùng cấm vô lê."),
    "Net": (2, "Tiếp xúc bóng phía trước người và kiểm tra độ cao qua lưới."),
    "Out": (2, "Ưu tiên kiểm soát hướng và lực để giữ bóng trong sân."),
    "Target": (1, "Điều chỉnh hướng về vùng đích trước khi tăng lực."),
    "Movement": (2, "Hồi vị sau cú đánh và chuẩn bị cho bóng tiếp theo."),
    "DinkTooHigh": (2, "Bóng ngắn qua lưới cao hơn mức tham chiếu. So sánh điểm chạm và đường bóng với cú mẫu của bài tập."),
    "ServeFeet": (3, "Giữ chân sau đường biên cuối sân và trong ô giao trước khi tiếp bóng."),
    "WrongInput": (1, "Giữ đúng tổ hợp kỹ thuật theo hướng dẫn."),
    "Spacing": (2, "Giữ cùng hàng với đồng đội và che khoảng trống giữa sân."),
    "Miss": (2, "Di chuyển vào tầm bóng trước khi vung vợt; ưu tiên tiếp xúc bóng ổn định."),
    "DoubleBounce": (2, "Chuẩn bị vị trí đón bóng sớm để trả bóng trước lần nảy thứ hai."),
}
NAMES = {"Early": "Đánh sớm", "Late": "Đánh muộn", "LateServe": "Giao bóng muộn", "Kitchen": "Vô lê trong vùng cấm vô lê",
         "Momentum": "Đà vô lê vào vùng cấm vô lê", "Net": "Bóng chạm lưới", "Out": "Bóng ngoài sân", "Target": "Chưa trúng vùng đích",
         "Movement": "Chưa hồi vị", "DinkTooHigh": "Bóng ngắn quá cao", "ServeFeet": "Chân giao bóng sai",
         "WrongInput": "Sai tổ hợp kỹ thuật", "Spacing": "Khoảng cách đội hình",
         "Miss": "Đánh hụt", "DoubleBounce": "Không trả bóng trước lần nảy thứ hai"}


def observations(s: Shot, threshold: float | None):
    """Yield observable opportunities; missing/engine-controlled values are not failures."""
    outcome = "Miss" if s.outcome == "ServeMiss" else s.outcome
    timing = observed_timing(s)
    if timing is not None:
        source = "rawRelease" if s.rawTimingError is not None and s.rawReleasedAt is not None and s.rawPosition is not None else "unassistedContact"
        if s.kind == "Serve":
            yield "LateServe", timing > .065, f"{source}={timing:.4f}s"
        else:
            yield "Early", timing < -.065, f"{source}={timing:.4f}s"
            yield "Late", timing > .065, f"{source}={timing:.4f}s"
    if s.outcome == "LateServe":
        if timing is None:
            yield "LateServe", True, "serve window expired"
    if timing is None:
        for code in ("Early", "Late"):
            if s.outcome in ("Early", "Late"):
                yield code, s.outcome == code, f"input outcome={s.outcome}"
    if s.volley is True and s.kitchen is not None:
        yield "Kitchen", s.kitchen, f"kitchen={s.kitchen}"
    if s.volley is True and s.momentum is not None:
        yield "Momentum", s.momentum, f"momentum={s.momentum}"
    if outcome != "unknown":
        for code in ("Net", "Out", "ServeFeet", "WrongInput", "Spacing", "Miss", "DoubleBounce"):
            yield code, outcome == code, f"outcome={s.outcome}"
    if not s.engineAimed and s.targetHit is not None:
        yield "Target", not s.targetHit, f"targetHit={s.targetHit}"
    if s.recovered is not None:
        yield "Movement", not s.recovered, f"recovered={s.recovered}"
    if s.kind == "Dink" and s.netClearance is not None and s.netClearance >= 0 and threshold is not None:
        trajectory = "engineAimedTrajectory" if s.engineAimed else "trajectory"
        yield "DinkTooHigh", s.netClearance > threshold, f"{trajectory}.netClearance={s.netClearance:.3f}m"


def analyze(request: AnalyzeRequest, tier_predictor=None):
    findings, rows = [], {}
    seen = set()
    for s in sorted(request.shots, key=lambda x: x.sequence):
        if s.shotId in seen:
            raise ValueError("duplicate shotId")
        seen.add(s.shotId)
        threshold = request.dinkClearanceThreshold
        if (request.dinkAssisted is not None and s.assisted != request.dinkAssisted
                or request.dinkEngineAimed is not None and s.engineAimed != request.dinkEngineAimed
                or request.dinkReferenceId is not None and s.referenceId != request.dinkReferenceId):
            threshold = None
        for code, failed, evidence in observations(s, threshold):
            severity, tip = TIPS[code]
            key = s.technique, code
            row = rows.setdefault(key, dict(technique=s.technique, code=code, opportunities=0, errors=0, severity=severity))
            row["opportunities"] += 1
            row["errors"] += int(failed)
            if failed:
                findings.append(dict(shotId=s.shotId, technique=s.technique, code=code,
                                     severity=severity, tip=tip, evidence=evidence, ruleVersion=request.ruleVersion))
    aggregate = defaultdict(lambda: [0, 0, 0, 0])
    # Current session then most recent history; recency resolves equal error rates.
    for age, metrics in enumerate([list(rows.values())] + [[m.model_dump() for m in h] for h in request.history]):
        for row in metrics:
            key = row["technique"], row["code"]
            a = aggregate[key]
            a[0] += row["opportunities"]
            a[1] += row["errors"]
            a[2] = max(a[2], row["severity"])
            a[3] = max(a[3], 10 - age if row["errors"] else 0)
    ranked = sorted(((key, a) for key, a in aggregate.items() if a[0] >= 5 and a[1]),
                    key=lambda x: (-x[1][1] / x[1][0], -x[1][2], -x[1][3], x[0]))
    completed = set(request.completedLessons)
    available = [d for d in request.drills if set(d.prerequisites) <= completed]
    tier = tier_predictor(request) if tier_predictor else dict(status="model_disabled", opportunities=0)
    target_tier = {"beginner": 0, "intermediate": 1, "advanced": 2}.get(tier.get("tier")) if tier.get("status") == "ready" else None
    if target_tier is not None:
        available.sort(key=lambda d: abs(d.tier - target_tier))
    recommendations, selected = [], set()
    for (technique, code), counts in ranked:
        for drill in available:
            if drill.skill != technique or drill.id in selected:
                continue
            selected.add(drill.id)
            recommendations.append(dict(id=drill.id, title=drill.title,
                                        reason=f"{NAMES[code]}: {counts[1]}/{counts[0]} lần quan sát gần đây"))
            if len(recommendations) == 3:
                break
        if len(recommendations) == 3:
            break
    if not recommendations:
        fallback = next((d for d in available if d.id == request.fallbackLesson), None)
        tier_fallback = next((d for d in available if d.tier == target_tier), None) if target_tier is not None and not request.resumeActive else None
        fallback = tier_fallback or fallback
        fallback = fallback or next(iter(available), None)
        if fallback:
            recommendations.append(dict(id=fallback.id, title=fallback.title, reason="Bài tập phù hợp với mức kỹ năng đã quan sát." if tier_fallback else "Tiếp tục lộ trình luyện tập hiện tại."))
    return dict(sessionId=request.sessionId, ruleVersion=request.ruleVersion,
                summary=session_summary(request.shots, list(rows.values()), findings), findings=findings,
                metrics=list(rows.values()), recommendations=recommendations, tier=tier)


def session_summary(shots, metrics, findings):
    """Describe this session's measured events, even when it has no technique fault."""
    if not shots:
        return "Phiên này chưa ghi nhận được cú đánh. Hãy hoàn thành một lượt có giao và trả bóng để HLV thống kê."
    contacts = sum(s.contact is not None for s in shots)
    serves = sum(s.kind == "Serve" for s in shots)
    lines = [f"Đã ghi nhận {len(shots)} lượt đánh, {contacts} lần tiếp xúc bóng và {serves} lượt giao bóng."]
    counts = defaultdict(lambda: [0, 0])
    for row in metrics:
        counts[row["code"]][0] += row["opportunities"]
        counts[row["code"]][1] += row["errors"]
    errors = sorted(((code, n) for code, n in counts.items() if n[1]), key=lambda item: (-item[1][1], item[0]))
    other_faults = sum(s.outcome in {"Fault", "ServeMotion", "ServeShort", "ServeWrongCourt", "OwnCourt", "TwoBounce",
                                   "DoubleHit", "NetTouch", "BodyHit", "PermanentObject"} for s in shots)
    if errors:
        lines.append("Cần cải thiện: " + "; ".join(f"{NAMES[code]} {n[1]}/{n[0]} lần quan sát" for code, n in errors[:2]) + ".")
    elif other_faults:
        lines.append(f"Có {other_faults} lượt ghi nhận vi phạm luật ngoài các tiêu chí kỹ thuật đang đo. Hãy xem thông báo trọng tài và luyện lại giao, trả bóng.")
    elif contacts:
        lines.append("Chưa ghi nhận lỗi trong các tiêu chí đã đo. Hãy duy trì khả năng tiếp xúc bóng và luyện kiểm soát điểm rơi.")
    else:
        lines.append("Chưa ghi nhận lần tiếp xúc bóng; hãy di chuyển vào tầm bóng và luyện giao, trả bóng trước.")
    if findings:
        priority = max(findings, key=lambda f: f["severity"])
        lines.append(priority["tip"])
    return "\n".join(lines)
