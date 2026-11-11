"""Helpers that write PlantUML sources and render them to PNG.

Usage: python gen_r3.py (writes src/*.puml) then python render.py.
Rendering needs Java 17+ and plantuml.jar (path in PLANTUML_JAR).
"""
import os
import subprocess

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "src")
PNG = os.path.join(HERE, "png")


def write_all(defs):
    os.makedirs(SRC, exist_ok=True)
    for name, body in defs.items():
        body = body.strip("\n")
        if body.startswith("@startchen"):
            text = body.replace("@startchen", "@startchen " + name, 1).rstrip() + "\n"
            text = text.replace("@startchen " + name, "@startchen " + name + "\n!include _style.iuml", 1)
        else:
            text = "@startuml " + name + "\n!include _style.iuml\n" + body + "\n@enduml\n"
        with open(os.path.join(SRC, name + ".puml"), "w", encoding="utf-8") as f:
            f.write(text)
    return list(defs)


def render(names=None):
    jar = os.environ.get("PLANTUML_JAR")
    java = os.environ.get("JAVA_EXE", "java")
    files = sorted(n for n in os.listdir(SRC) if n.endswith(".puml"))
    if names:
        files = [f for f in files if f[:-5] in names]
    os.makedirs(PNG, exist_ok=True)
    cmd = [java, "-Djava.awt.headless=true", "-jar", jar, "-charset", "UTF-8", "-tpng", "-o", PNG] + [os.path.join(SRC, f) for f in files]
    r = subprocess.run(cmd, capture_output=True, text=True)
    return r.returncode, (r.stdout + r.stderr)[-4000:]
