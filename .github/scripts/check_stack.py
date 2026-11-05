"""Exercise the actual images, not just Dockerfile syntax. Uses only the named CI project."""
import json
import os
from pathlib import Path
import subprocess
import time
import urllib.error
import urllib.request


def main():
    project = os.environ["COMPOSE_PROJECT_NAME"]
    if not project.startswith("pkv-ci-"):
        raise ValueError("Smoke checks must target an isolated pkv-ci-* project.")
    root = Path(__file__).resolve().parents[2]
    compose = ["docker", "compose", "-p", project, "-f", str(root / ".github/compose.smoke.yml")]

    def output(*args):
        return subprocess.check_output([*compose, *args], text=True).strip()

    def address(service, port):
        endpoint = output("port", service, str(port))
        if not endpoint.startswith("127.0.0.1:"):
            raise ValueError("Smoke HTTP ports must bind to loopback.")
        return "http://" + endpoint

    def get(url):
        with urllib.request.urlopen(url, timeout=5) as response:
            return json.load(response)

    api, web = address("backend-api", 8080), address("web", 80)
    deadline = time.monotonic() + 90
    while True:
        try:
            assert get(api + "/health/ready")["status"] == "ready"
            break
        except (OSError, ValueError, AssertionError):
            if time.monotonic() >= deadline:
                raise
            time.sleep(2)
    with urllib.request.urlopen(web + "/healthz", timeout=5) as response:
        assert response.status == 200
    with urllib.request.urlopen(web + "/login", timeout=5) as response:
        assert "<html" in response.read().decode().lower()
    try:
        get(web + "/api/AiCoaching/summary")
        raise AssertionError("The admin proxy must preserve API authentication.")
    except urllib.error.HTTPError as error:
        assert error.code == 401

    ai = output("ps", "-q", "ai-service")
    # Same image and FastAPI route used in deployment, including service authentication.
    code = """import json,os,urllib.request
assert json.load(urllib.request.urlopen('http://localhost:8000/ready'))['model']=='disabled'
request=urllib.request.Request('http://localhost:8000/v1/analyze-session',
 data=json.dumps(dict(sessionId='ci-smoke',mode='training',shots=[])).encode(),
 headers={'Content-Type':'application/json','X-AI-Key':os.environ['AI_SERVICE_KEY']})
result=json.load(urllib.request.urlopen(request))
assert result['sessionId']=='ci-smoke' and result['summary']
"""
    subprocess.run(["docker", "exec", ai, "python", "-c", code], check=True)
    redis = output("ps", "-q", "redis")
    ttl = int(subprocess.check_output(["docker", "exec", redis, "redis-cli", "TTL", "pickleverse:{dedicated}:worker"], text=True))
    assert ttl > 0, "Native worker must maintain its authenticated Redis heartbeat."
    print("PASS: API readiness, admin SPA/auth proxy, authenticated AI analysis and native worker heartbeat.")


if __name__ == "__main__":
    main()
