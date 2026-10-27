from collections import defaultdict
from .models import AnalyzeRequest, Shot

TIPS = {
    "Early": (1, "Thả nút muộn hơn để vợt đến bóng đúng thời điểm."),
    "Late": (1, "Chuẩn bị vợt và thả nút sớm hơn; di chuyển vào vị trí đón bóng."),
    "LateServe": (1, "Chuẩn bị giao bóng sớm và tiếp xúc trong cửa sổ timing."),
    "Kitchen": (3, "Đưa cả hai chân ra ngoài Kitchen trước khi volley."),
    "Momentum": (3, "Giữ thăng bằng sau volley, tránh để đà đưa chân vào Kitchen."),
    "Net": (2, "Tiếp xúc bóng phía trước người và kiểm tra độ cao qua lưới."),
    "Out": (2, "Ưu tiên kiểm soát hướng và lực để giữ bóng trong sân."),
    "Target": (1, "Điều chỉnh hướng về vùng đích trước khi tăng lực."),
    "Movement": (2, "Hồi vị sau cú đánh và chuẩn bị cho bóng tiếp theo."),
    "DinkTooHigh": (2, "Giảm lực và giữ quỹ đạo dink thấp qua lưới."),
    "ServeFeet": (3, "Giữ chân sau baseline và trong ô giao trước khi tiếp bóng."),
    "WrongInput": (1, "Giữ đúng tổ hợp kỹ thuật theo hướng dẫn."),
    "Spacing": (2, "Giữ cùng hàng với đồng đội và che khoảng trống giữa sân."),
}
NAMES = {"Early": "Đánh sớm", "Late": "Đánh muộn", "LateServe": "Giao bóng muộn", "Kitchen": "Volley trong Kitchen",
         "Momentum": "Đà volley vào Kitchen", "Net": "Bóng chạm lưới", "Out": "Bóng ngoài sân", "Target": "Chưa trúng vùng đích",
         "Movement": "Chưa hồi vị", "DinkTooHigh": "Dink quá cao", "ServeFeet": "Chân giao bóng sai",
         "WrongInput": "Sai tổ hợp kỹ thuật", "Spacing": "Khoảng cách đội hình"}


def observations(s: Shot, threshold: float | None):
    """Yield observable opportunities; missing/engine-controlled values are not failures."""
    if s.timingError is not None and not s.assisted:
        if s.kind == "Serve":
            yield "LateServe", s.timingError > .065, f"timingError={s.timingError:.4f}s"
        else:
            yield "Early", s.timingError < -.065, f"timingError={s.timingError:.4f}s"
            yield "Late", s.timingError > .065, f"timingError={s.timingError:.4f}s"
    if s.outcome == "LateServe":
        if s.timingError is None or s.assisted:
            yield "LateServe", True, "serve window expired"
    if s.assisted or s.timingError is None:
        for code in ("Early", "Late"):
            if s.outcome in ("Early", "Late", "Contact"):
                yield code, s.outcome == code, f"input outcome={s.outcome}"
    if s.volley is True and s.kitchen is not None:
        yield "Kitchen", s.kitchen, f"kitchen={s.kitchen}"
    if s.volley is True and s.momentum is not None:
        yield "Momentum", s.momentum, f"momentum={s.momentum}"
    if s.outcome != "unknown":
        for code in ("Net", "Out", "ServeFeet", "WrongInput", "Spacing"):
            yield code, s.outcome == code, f"outcome={s.outcome}"
    if not s.engineAimed and s.targetHit is not None:
        yield "Target", not s.targetHit, f"targetHit={s.targetHit}"
    if s.recovered is not None:
        yield "Movement", not s.recovered, f"recovered={s.recovered}"
    if s.kind == "Dink" and not s.engineAimed and s.netClearance is not None and threshold is not None:
        yield "DinkTooHigh", s.netClearance > threshold, f"netClearance={s.netClearance:.3f}m"


def analyze(request: AnalyzeRequest, tier_predictor=None):
    findings, rows = [], {}
    seen = set()
    for s in sorted(request.shots, key=lambda x: x.sequence):
        if s.shotId in seen:
            raise ValueError("duplicate shotId")
        seen.add(s.shotId)
        for code, failed, evidence in observations(s, request.dinkClearanceThreshold):
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
    return dict(sessionId=request.sessionId, ruleVersion=request.ruleVersion, findings=findings,
                metrics=list(rows.values()), recommendations=recommendations, tier=tier)
