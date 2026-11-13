"""Extracts the Web API endpoint list from the controllers and finds which program calls each route.

Writes api_endpoints.json next to this script:
    python api_extract.py
Routes, HTTP methods and access levels come from the controller attributes. "Used by" is found by
searching the Unity client and dedicated-server code (files compiled only with PICKLEVERSE_LEGACY_NGO and
Editor folders are skipped) and the admin web sources (mock modules are skipped) for each route.
"""
import json
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
CONTROLLERS = os.path.join(ROOT, "src", "Backend", "API", "Controllers")
UNITY = os.path.join(ROOT, "My project", "Assets", "Scripts")
WEB = os.path.join(ROOT, "src", "Frontend", "src")

ATTR = re.compile(r"\[((?:[^\[\]]|\[[^\[\]]*\])*)\]")
CLASS = re.compile(r"class\s+(\w+)Controller\b")
HTTP = re.compile(r"Http(Get|Post|Put|Delete|Patch)\s*(?:\(\s*\"([^\"]*)\"\s*\))?")
ROUTE = re.compile(r"Route\s*\(\s*\"([^\"]*)\"\s*\)")
ACTION = re.compile(r"\s(\w+)\s*\($")


def access_of(attrs):
    joined = " ".join(attrs)
    if "AllowAnonymous" in joined:
        return "Anonymous"
    if re.search(r"Authorize\s*\(\s*Roles\s*=\s*\"Admin\"", joined):
        return "Admin"
    if re.search(r"\bAuthorize\b", joined):
        return "Player, Restricted" if "AllowRestrictedSession" in joined else "Player"
    return None


def class_attributes(src, start):
    """Bracketed attribute lines directly above a class declaration."""
    lines = src[:start].split("\n")
    first = lines[-1].strip()
    block = [first] if first.startswith("[") else []
    for line in reversed(lines[:-1]):
        text = line.strip()
        if text.startswith("[") or not text or text.startswith("//"):
            block.append(text)
            continue
        break
    return [a for line in block for a in ATTR.findall(line)]


def parse():
    endpoints = []
    for name in sorted(os.listdir(CONTROLLERS)):
        if not name.endswith(".cs"):
            continue
        src = open(os.path.join(CONTROLLERS, name), encoding="utf-8").read()
        classes = list(CLASS.finditer(src))
        for i, cls in enumerate(classes):
            cattrs = class_attributes(src, cls.start())
            controller = cls.group(1)
            croute = next((m.group(1) for a in cattrs for m in [ROUTE.search(a)] if m), "")
            croute = croute.replace("[controller]", controller)
            caccess = access_of(cattrs)
            body = src[cls.end(): classes[i + 1].start() if i + 1 < len(classes) else len(src)]
            service_key = "Authenticate()" in body
            pending = []
            for m in re.finditer(r"\[(?:[^\[\]]|\[[^\[\]]*\])*\]|(?:public|private|internal)\s+[^;{=]*?\(", body):
                token = m.group(0)
                if token.startswith("["):
                    pending.append(token[1:-1])
                    continue
                http = [h for a in pending for h in HTTP.finditer(a)]
                if http:
                    found = ACTION.search(token)
                    action = found.group(1) if found else "?"
                    maccess = access_of(pending)
                    mroute = next((r.group(1) for a in pending for r in [ROUTE.search(a)] if r), None)
                    for h in http:
                        template = h.group(2) if h.group(2) is not None else (mroute or "")
                        if template.startswith("~/") or template.startswith("/"):
                            route = "/" + template.lstrip("~/")
                        else:
                            route = "/" + "/".join(p for p in [croute.strip("/"), template.strip("/")] if p)
                        access = maccess or caccess or ("Service key" if service_key else "Anonymous")
                        endpoints.append({"controller": controller, "method": h.group(1).upper(), "route": route,
                                          "action": action, "access": access})
                pending = []
    return endpoints


def sources():
    files = {}
    for base, label in ((UNITY, "unity"), (WEB, "web")):
        for d, _, names in os.walk(base):
            if label == "unity" and os.sep + "Editor" in d:
                continue
            for n in names:
                if not n.endswith((".cs", ".ts", ".tsx")) or n.startswith("mock"):
                    continue
                p = os.path.join(d, n)
                text = open(p, encoding="utf-8", errors="replace").read()
                if text.lstrip().startswith("#if PICKLEVERSE_LEGACY_NGO"):
                    continue
                files[p] = (label, text)
    return files


PARAM = r"(?:\$\{[^}]*\}|\{[^}]*\}|\"\s*\+\s*[^+]{1,80}?\+\s*\"|[^/\"'`\s]+)"


def literal_usage(route, files):
    regex = ""
    for part in route.strip("/").split("/"):
        regex += ("/" if regex else "") + (PARAM if part.startswith("{") else re.escape(part))
    pattern = re.compile(r"[\"'`/]" + regex + r"(?:[\"'`?/]|\"\s*\+)", re.IGNORECASE)
    who = set()
    for path, (label, text) in files.items():
        if pattern.search(text):
            who.add("Admin web" if label == "web" else "Game client")
    return who


def dynamic_usage(route, method, files):
    """Client helpers that build paths from a prefix: CommandAsync("x") and the dedicated worker's Post("x")."""
    rest = None
    if route.startswith("/api/Game/") and not route.startswith("/api/Game/replays"):
        rest = route[len("/api/Game/"):].split("/{")[0]
        pattern = re.compile(r'CommandAsync\("' + re.escape(rest) + '"')
        label = "Game client"
    elif route.startswith("/api/Server/"):
        rest = route[len("/api/Server/"):]
        if rest.startswith("{"):
            rest = rest[rest.find("}") + 1:]
        rest = rest.split("/{")[0]
        pattern = re.compile(r'Post<[^>]*>\(\s*(?:job\.leaseId\s*\+\s*)?"/?' + re.escape(rest))
        label = "Game server"
    elif route.startswith("/api/Game/replays/{"):
        tail = route.split("}", 1)[1]
        chunk = r'\s*\+\s*"/chunks/"' if tail.startswith("/chunks") else r'(?!\s*\+)'
        pattern = re.compile('"' + method + r'",\s*Route\([^)]*\)' + chunk)
        label = "Game client"
    else:
        return set()
    if rest == "":
        return set()
    return {label} if any(pattern.search(text) for _, text in files.values()) else set()


# Calls whose route is assembled at run time in a way the patterns above cannot follow; each was checked by hand.
OVERRIDES = {
    ("POST", "/api/Admin/cms/lessons/{id:int}/publish"): "Admin web",      # LessonManager.tsx: `${route}/${kind}`
    ("POST", "/api/Admin/cms/lessons/{id:int}/unpublish"): "Admin web",
    ("GET", "/api/Admin/coaching/dataset/tier"): "Admin web",              # ExpertReview.tsx: `.../dataset/${kind}`
    ("GET", "/api/Admin/coaching/dataset/coaching"): "Admin web",
    ("GET", "/api/AiCoaching/summary"): "Game client",                     # coachingApi.ts exists but is never imported
    ("POST", "/api/Game/training/checkpoint"): "Game client",              # ProgressionRuntime.Offline.cs: completed ? ... : ...
    ("POST", "/api/Game/training/complete"): "Game client",
}


def main():
    endpoints = parse()
    files = sources()
    for e in endpoints:
        who = literal_usage(e["route"], files) | dynamic_usage(e["route"], e["method"], files)
        e["usedBy"] = OVERRIDES.get((e["method"], e["route"])) or ", ".join(sorted(who)) or "Not called"
    out = os.path.join(HERE, "api_endpoints.json")
    json.dump(endpoints, open(out, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print(len(endpoints), "endpoints in", len({e["controller"] for e in endpoints}), "controller classes ->", out)


if __name__ == "__main__":
    main()
