"""
SASMS facial verification service.

Bound to 127.0.0.1 only (see __main__ below) — never exposed to the internet or even the
LAN. The only intended caller is the SASMS ASP.NET Core app, server-to-server, which is why
auth here is a shared secret header rather than a full user-auth scheme: this endpoint
authenticates a *service*, not a person.
"""

import hmac
import os
from functools import wraps

from flask import Flask, jsonify, request, send_from_directory

import face_engine

app = Flask(__name__)

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
SHARED_SECRET = os.environ.get("FACE_SERVICE_SHARED_SECRET", "")


def _secrets_match(provided, expected):
    # Constant-time comparison — same reasoning as the ASP.NET Core side of this same
    # shared-secret pattern (Api/VerificationController.cs): avoids leaking the secret via
    # response-timing side channels.
    return hmac.compare_digest(provided, expected)


def require_shared_secret(view):
    @wraps(view)
    def wrapped(*args, **kwargs):
        if not SHARED_SECRET:
            return jsonify({"error": "Server misconfigured: FACE_SERVICE_SHARED_SECRET is not set."}), 500
        provided = request.headers.get("X-Face-Service-Secret", "")
        if not provided or not _secrets_match(provided, SHARED_SECRET):
            return jsonify({"error": "Invalid or missing shared secret."}), 401
        return view(*args, **kwargs)
    return wrapped


@app.route("/health", methods=["GET"])
def health():
    return jsonify({"status": "ok"})


@app.route("/test", methods=["GET"])
def test_harness():
    # Dev-only harness for exercising /register and /verify with a real webcam before the
    # ASP.NET Core integration exists. Served from this same app so the page's fetch()
    # calls are same-origin — no CORS configuration needed anywhere in this service.
    return send_from_directory(BASE_DIR, "test_harness.html")


@app.route("/register", methods=["POST"])
@require_shared_secret
def register():
    body = request.get_json(silent=True) or {}
    employee_id = body.get("employeeId")
    images = body.get("images") or []

    if not employee_id or not images:
        return jsonify({"error": "employeeId and at least one image are required."}), 400

    success, message, saved_count = face_engine.register_face(employee_id, images)
    return jsonify({"success": success, "message": message, "savedCount": saved_count}), (200 if success else 422)


@app.route("/verify", methods=["POST"])
@require_shared_secret
def verify():
    body = request.get_json(silent=True) or {}
    employee_id = body.get("employeeId")
    frames = body.get("frames") or []

    if not employee_id or not frames:
        return jsonify({"error": "employeeId and at least one frame are required."}), 400

    result = face_engine.verify_face(employee_id, frames)
    return jsonify(result), 200


@app.route("/faces/<employee_id>", methods=["DELETE"])
@require_shared_secret
def delete_face(employee_id):
    face_engine.delete_employee(employee_id)
    return jsonify({"success": True}), 200


if __name__ == "__main__":
    if not SHARED_SECRET:
        print("WARNING: FACE_SERVICE_SHARED_SECRET is not set — /register and /verify will "
              "reject every request until it is. Set it in this shell before running.")
    # 127.0.0.1 by default, deliberately: for local/Windows development this service is
    # never meant to be reachable from anywhere but the SASMS app running on the same
    # machine. The Docker Compose deployment is the one documented exception — there, the
    # ASP.NET Core app runs in a *different* container, and 127.0.0.1 inside this container
    # would be unreachable even from a sibling container on the same Docker network, so
    # docker-compose.yml sets FACE_SERVICE_HOST=0.0.0.0 for that case only. This still never
    # exposes the service publicly — Docker only makes a container-to-container port
    # reachable at all if it's also *published* to the host in docker-compose.yml, which
    # this one deliberately is not (see that file).
    host = os.environ.get("FACE_SERVICE_HOST", "127.0.0.1")
    app.run(host=host, port=5100)
