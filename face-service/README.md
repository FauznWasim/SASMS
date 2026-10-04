# SASMS face-service

Python/OpenCV facial verification + basic liveness detection component of SASMS. Bound to
`127.0.0.1` only — never internet- or LAN-exposed. Called server-to-server by the ASP.NET
Core app (`src/SASMS.Web`), never directly by the browser.

- Face matching: OpenCV's built-in LBPH recognizer (`cv2.face`), trained on reference
  photos captured through the app.
- Liveness: blink detection (open → closed → open eyes) across a ~2 second burst of
  frames, using eye-aspect-ratio (EAR) computed from a local MediaPipe face-landmark
  model (`models/face_landmarker.task` — one-time download, see Setup below; all
  inference runs locally, no cloud/paid API calls). Defeats a static photo held up to
  the camera without needing any extra model to be trained by us.
  (Originally used OpenCV's bundled Haar eye cascade instead — dropped 2026-10-01 after
  real-iPhone testing showed it reporting "eyes open" on every frame of two attempts
  where the person visibly blinked; see `face_engine.py`'s module docstring.)

See `face_engine.py` for the actual detection/matching/liveness logic and `app.py` for the
two endpoints it exposes (`POST /register`, `POST /verify`), both behind a shared-secret
header — the same pattern already used on the ASP.NET Core side's own verification
callback endpoint.

## Setup

```powershell
cd face-service
python -m venv .venv
.venv\Scripts\Activate.ps1
pip install -r requirements.txt
```

Then download the local face-landmark model once (used for EAR-based liveness — a small
model file, not bundled in the `mediapipe` pip package itself):

```powershell
New-Item -ItemType Directory -Force -Path models | Out-Null
Invoke-WebRequest -Uri "https://storage.googleapis.com/mediapipe-models/face_landmarker/face_landmarker/float16/latest/face_landmarker.task" -OutFile "models\face_landmarker.task"
```

This is a one-time ~3.7MB download from Google's official MediaPipe model storage, saved to
`face-service/models/` (gitignored, like `data/`). After this, everything runs locally —
no further network calls happen during registration or verification.

## Run

The shared secret here must match `FaceVerificationService:SharedSecret` in
`src/SASMS.Web/appsettings.Development.json`.

```powershell
$env:FACE_SERVICE_SHARED_SECRET = "<same value as appsettings.Development.json>"
python app.py
```

Listens on `http://127.0.0.1:5100`.

## Testing in isolation (before the ASP.NET Core integration)

Open `http://127.0.0.1:5100/test` in a browser for a throwaway webcam-based test harness —
lets you register a test employee's face and run a verify pass directly against this
service, without needing the full SASMS app running. Not part of the production app;
`test_harness.html` is dev-only.

## Data

`data/faces/{employeeId}/*.jpg` (reference crops) and `data/model.yml` (the trained LBPH
model) are created at runtime and gitignored — don't commit real employee photos.
