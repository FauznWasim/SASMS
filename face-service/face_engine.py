"""
Face detection/matching (OpenCV Haar cascade + LBPH recognizer) and blink-based liveness
detection (a local MediaPipe face-landmark model, used to compute eye-aspect-ratio).

Face matching is deliberately built on OpenCV alone (LBPH from opencv-contrib-python)
rather than a deep-learning embedding model or dlib/face_recognition — matches the FYP's
stated "OpenCV" tech stack, installs on Windows with a plain `pip install` (no compiler
toolchain), and stays simple enough to fully explain.

Liveness was originally Haar-eye-cascade-based too, but real-iPhone testing (2026-10-01)
showed the Haar eye cascade reporting "2 eyes detected" on every single frame of two
separate attempts where the person visibly blinked — i.e. it never registered a closed
eye at all on that camera. Haar's eye cascade keys on eye-region contrast/structure
(eyebrow, socket, eyelid crease) more than on whether the eyelid is actually shut, which is
a known weakness. Liveness now uses eye-aspect-ratio (EAR) from real eye-contour landmarks
(MediaPipe's local FaceLandmarker, a one-time-downloaded model — see README "Setup" —
no cloud/paid API calls at request time), which is the standard technique for exactly this.
"""

import base64
import os
import shutil

import cv2
import mediapipe as mp
import numpy as np

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
DATA_DIR = os.path.join(BASE_DIR, "data")
FACES_DIR = os.path.join(DATA_DIR, "faces")
MODEL_PATH = os.path.join(DATA_DIR, "model.yml")
LABELS_PATH = os.path.join(DATA_DIR, "labels.txt")
FACE_LANDMARKER_MODEL_PATH = os.path.join(BASE_DIR, "models", "face_landmarker.task")

FACE_CASCADE = cv2.CascadeClassifier(cv2.data.haarcascades + "haarcascade_frontalface_default.xml")

# LBPH confidence is a *distance* — lower means a closer match. Started at 70.0, but real
# webcam testing (2026-09-04) showed genuine registered-person check-outs failing at that
# threshold — ordinary lighting/angle/webcam-quality variation between registration and a
# later capture pushes distance higher than the lab-clean number tutorials assume. Loosened
# to 100.0. If false accepts (wrong person matching) become the problem instead, tighten it
# back down; if real people are still getting rejected, loosen further — there's no
# universally-correct value, it depends on this webcam/lighting setup.
MATCH_CONFIDENCE_THRESHOLD = 100.0

# A face-match is accepted if at least this fraction of frames-with-a-detected-face vote
# for the claimed identity, so one lucky/unlucky frame can't flip the result on its own.
MATCH_VOTE_FRACTION = 0.4

# Eye-aspect-ratio (Soukupová & Čech, 2016) landmark indices into MediaPipe's face-mesh
# output — the standard six-point-per-eye layout (outer corner, two upper-lid points,
# inner corner, two lower-lid points) used by essentially every public EAR/blink tutorial.
LEFT_EYE_EAR_INDICES = (362, 385, 387, 263, 373, 380)
RIGHT_EYE_EAR_INDICES = (33, 160, 158, 133, 153, 144)

# EAR collapses toward 0 as the eyelid closes (the vertical eye-openness shrinks while the
# horizontal eye width stays fixed) and recovers once the eye reopens. 0.21 is the
# commonly-cited literature starting point — like MATCH_CONFIDENCE_THRESHOLD, this is a
# real physical measurement and may need tuning for this camera/lighting setup once
# verified against real-device EAR readings.
EAR_CLOSED_THRESHOLD = 0.21

_face_landmarker = None

# Dev-only: prints per-attempt numeric detail (confidence values, predicted labels, EAR
# values, O/C history) to THIS PROCESS'S CONSOLE ONLY. Never written to disk, never included
# in the JSON response returned to ASP.NET Core/the browser, and never includes image data —
# just numbers/booleans already computed during verify_face. TEMPORARILY re-enabled
# (2026-10-02) to diagnose a Phase 6 static-photo-spoof liveness bypass found during
# physical-iPhone testing — restore to False once that diagnostic attempt has been captured.
DIAGNOSTIC_LOGGING = True

os.makedirs(FACES_DIR, exist_ok=True)


def decode_image(b64_string):
    """base64 (optionally data-URL prefixed) -> BGR numpy image, or None if invalid/undecodable."""
    if b64_string and "," in b64_string and b64_string.strip().startswith("data:"):
        b64_string = b64_string.split(",", 1)[1]
    try:
        raw = base64.b64decode(b64_string)
    except Exception:
        return None
    arr = np.frombuffer(raw, dtype=np.uint8)
    return cv2.imdecode(arr, cv2.IMREAD_COLOR)


def detect_largest_face(gray_image):
    """Returns the (x, y, w, h) box of the largest detected face, or None."""
    faces = FACE_CASCADE.detectMultiScale(gray_image, scaleFactor=1.1, minNeighbors=5, minSize=(80, 80))
    if len(faces) == 0:
        return None
    return max(faces, key=lambda f: f[2] * f[3])


def _get_face_landmarker():
    """Lazily creates the MediaPipe FaceLandmarker once per process. Loading the model is
    expensive (real disk + inference-session setup), so this must never run per-request."""
    global _face_landmarker
    if _face_landmarker is None:
        if not os.path.exists(FACE_LANDMARKER_MODEL_PATH):
            raise RuntimeError(
                f"Missing face landmark model at {FACE_LANDMARKER_MODEL_PATH}. "
                "See face-service/README.md 'Setup' for the one-time download step."
            )
        base_options = mp.tasks.BaseOptions(model_asset_path=FACE_LANDMARKER_MODEL_PATH)
        options = mp.tasks.vision.FaceLandmarkerOptions(
            base_options=base_options,
            running_mode=mp.tasks.vision.RunningMode.IMAGE,
            num_faces=1,
        )
        _face_landmarker = mp.tasks.vision.FaceLandmarker.create_from_options(options)
    return _face_landmarker


def _eye_aspect_ratio(landmarks, indices, img_w, img_h):
    """Classic 6-point EAR: (vertical-gap-1 + vertical-gap-2) / (2 * horizontal-width),
    computed in actual pixel distances (landmark coords are normalized 0-1, so they're
    scaled by the frame's real width/height first — otherwise a non-square frame would
    distort the ratio)."""
    pts = [(landmarks[i].x * img_w, landmarks[i].y * img_h) for i in indices]
    p1, p2, p3, p4, p5, p6 = pts

    def _dist(a, b):
        return ((a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2) ** 0.5

    horizontal = _dist(p1, p4)
    if horizontal == 0:
        return 0.0
    return (_dist(p2, p6) + _dist(p3, p5)) / (2.0 * horizontal)


def _average_eye_openness(img_bgr):
    """Runs the local face-landmark model on one frame. Returns (avg_ear, left_ear,
    right_ear), or None if no face/landmarks were found in this particular frame."""
    rgb = cv2.cvtColor(img_bgr, cv2.COLOR_BGR2RGB)
    mp_image = mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb)
    result = _get_face_landmarker().detect(mp_image)
    if not result.face_landmarks:
        return None

    landmarks = result.face_landmarks[0]
    img_h, img_w = img_bgr.shape[:2]
    left_ear = _eye_aspect_ratio(landmarks, LEFT_EYE_EAR_INDICES, img_w, img_h)
    right_ear = _eye_aspect_ratio(landmarks, RIGHT_EYE_EAR_INDICES, img_w, img_h)
    return (left_ear + right_ear) / 2.0, left_ear, right_ear


def _load_labels():
    """LBPH needs small integer labels, not arbitrary employee ids — this maps between them."""
    labels = {}
    if os.path.exists(LABELS_PATH):
        with open(LABELS_PATH, "r", encoding="utf-8") as f:
            for line in f:
                line = line.strip()
                if not line:
                    continue
                label_id, employee_id = line.split(",", 1)
                labels[int(label_id)] = employee_id
    return labels


def _save_labels(labels):
    with open(LABELS_PATH, "w", encoding="utf-8") as f:
        for label_id, employee_id in labels.items():
            f.write(f"{label_id},{employee_id}\n")


def _label_id_for_employee(employee_id, labels):
    for label_id, existing_employee_id in labels.items():
        if existing_employee_id == employee_id:
            return label_id
    return max(labels.keys(), default=-1) + 1


def register_face(employee_id, images_b64):
    """
    Detects a face in each provided image, saves the cropped reference photos, and
    incrementally updates the shared LBPH model with this employee's label (does not
    retrain from scratch, so registering a new employee doesn't disturb existing ones).

    Returns (success, message, saved_count).
    """
    employee_id = str(employee_id)
    employee_dir = os.path.join(FACES_DIR, employee_id)
    os.makedirs(employee_dir, exist_ok=True)

    faces = []
    saved_count = 0
    for i, b64 in enumerate(images_b64):
        img = decode_image(b64)
        if img is None:
            continue
        gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
        box = detect_largest_face(gray)
        if box is None:
            continue
        x, y, w, h = box
        face_crop = cv2.resize(gray[y:y + h, x:x + w], (200, 200))
        faces.append(face_crop)
        cv2.imwrite(os.path.join(employee_dir, f"{i}.jpg"), face_crop)
        saved_count += 1

    if saved_count == 0:
        return False, "No face was detected in any of the provided images.", 0

    labels = _load_labels()
    label_id = _label_id_for_employee(employee_id, labels)
    labels[label_id] = employee_id

    recognizer = cv2.face.LBPHFaceRecognizer_create()
    label_ids = np.array([label_id] * len(faces))

    if os.path.exists(MODEL_PATH):
        recognizer.read(MODEL_PATH)
        recognizer.update(faces, label_ids)
    else:
        recognizer.train(faces, label_ids)

    recognizer.write(MODEL_PATH)
    _save_labels(labels)

    return True, f"Registered {saved_count} reference photo(s).", saved_count


def verify_face(employee_id, frames_b64):
    """
    Runs face-match (against the claimed employeeId only — this is verification, not open
    identification), via Haar+LBPH, and blink-based liveness, via landmark-based EAR, across
    a short frame burst. The two checks are independent: LBPH matching still only looks at
    the Haar-detected face crop (unchanged), while EAR runs the MediaPipe landmark model on
    every frame directly, regardless of whether Haar found a face in it.

    Returns {faceMatchSuccess, livenessPassed, framesProcessed} (+ "error" if the employee
    has no registered template yet).
    """
    employee_id = str(employee_id)
    labels = _load_labels()
    label_id = next((lid for lid, eid in labels.items() if eid == employee_id), None)

    if label_id is None or not os.path.exists(MODEL_PATH):
        return {
            "faceMatchSuccess": False,
            "livenessPassed": False,
            "framesProcessed": 0,
            "error": "No registered face template for this employee.",
        }

    recognizer = cv2.face.LBPHFaceRecognizer_create()
    recognizer.read(MODEL_PATH)

    eyes_open_history = []
    match_votes = 0
    frames_with_face = 0

    # Diagnostic-only per-frame detail — populated regardless of DIAGNOSTIC_LOGGING (cheap,
    # numbers only) but only ever printed, never returned or persisted.
    frame_log = []
    decoded_count = 0
    undecodable_count = 0

    for frame_index, b64 in enumerate(frames_b64):
        img = decode_image(b64)
        if img is None:
            undecodable_count += 1
            continue
        decoded_count += 1
        img_h, img_w = img.shape[:2]
        log_entry = {
            "frame": frame_index, "imgSize": (img_w, img_h), "ear": None,
            "allLabelConfidences": None, "bestAlternativeEmployeeId": None,
            "bestAlternativeConfidence": None, "margin": None,
        }

        # Face match (unchanged): Haar-detected face crop -> LBPH prediction. This is the
        # one and only call that decides matched_this_frame/match_votes/face_match_success —
        # nothing below this block influences that decision in any way.
        gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
        box = detect_largest_face(gray)
        if box is not None:
            frames_with_face += 1
            x, y, w, h = box
            face_crop = cv2.resize(gray[y:y + h, x:x + w], (200, 200))

            predicted_label, confidence = recognizer.predict(face_crop)
            matched_this_frame = predicted_label == label_id and confidence <= MATCH_CONFIDENCE_THRESHOLD
            if matched_this_frame:
                match_votes += 1

            log_entry.update({
                "faceDetected": True, "faceBox": (int(x), int(y), int(w), int(h)),
                "claimedEmployeeId": employee_id, "claimedLabelId": label_id,
                "predictedLabel": int(predicted_label),
                "predictedEmployeeId": labels.get(int(predicted_label), "<unknown label>"),
                "confidence": round(float(confidence), 1),
                "matched": matched_this_frame,
            })

            # Diagnostic-only: distance to EVERY registered label for this frame (not just
            # the single best match recognizer.predict() above already used for the real
            # decision), via OpenCV's existing StandardCollector/predict_collect API — purely
            # additive data collection, read-only, never touches matched_this_frame/
            # match_votes/face_match_success. Gated behind DIAGNOSTIC_LOGGING so this extra
            # per-label comparison never runs in normal production verification.
            if DIAGNOSTIC_LOGGING:
                collector = cv2.face.StandardCollector_create()
                recognizer.predict_collect(face_crop, collector)
                # getResults() returns one (label, distance) pair per TRAINING SAMPLE, not one
                # per label (an employee registered with 5 photos has up to 5 entries for their
                # label) — the per-label distance that matters (and the one recognizer.predict()
                # above already used internally) is the MINIMUM across that label's samples, so
                # that's what has to be compared here, not an arbitrary one of the samples.
                per_label_min = {}
                for lid, dist in collector.getResults():
                    lid = int(lid)
                    dist = float(dist)
                    if lid not in per_label_min or dist < per_label_min[lid]:
                        per_label_min[lid] = dist
                per_label_min = {lid: round(dist, 1) for lid, dist in per_label_min.items()}

                claimed_confidence = per_label_min.get(label_id)
                other_results = [(lid, dist) for lid, dist in per_label_min.items() if lid != label_id]
                best_alternative = min(other_results, key=lambda r: r[1]) if other_results else None

                log_entry.update({
                    "allLabelConfidences": {
                        labels.get(lid, f"label{lid}"): dist for lid, dist in per_label_min.items()
                    },
                    "bestAlternativeEmployeeId": labels.get(best_alternative[0], "<unknown label>") if best_alternative else None,
                    "bestAlternativeConfidence": best_alternative[1] if best_alternative else None,
                    "margin": round(best_alternative[1] - claimed_confidence, 1)
                        if (best_alternative is not None and claimed_confidence is not None) else None,
                })
        else:
            log_entry["faceDetected"] = False

        # Liveness (new): landmark-based EAR on the full frame, independent of Haar's result.
        eye_openness = _average_eye_openness(img)
        if eye_openness is not None:
            avg_ear, left_ear, right_ear = eye_openness
            eye_open_this_frame = avg_ear >= EAR_CLOSED_THRESHOLD
            eyes_open_history.append(eye_open_this_frame)
            log_entry.update({
                "ear": round(avg_ear, 3), "earLeft": round(left_ear, 3), "earRight": round(right_ear, 3),
                "eyeOpen": eye_open_this_frame,
            })

        frame_log.append(log_entry)

    face_match_success = frames_with_face > 0 and (match_votes / frames_with_face) >= MATCH_VOTE_FRACTION
    liveness_passed = _detected_blink(eyes_open_history)

    if DIAGNOSTIC_LOGGING:
        _log_verify_diagnostics(
            employee_id, label_id, frames_b64, decoded_count, undecodable_count,
            frames_with_face, match_votes, eyes_open_history, frame_log,
            face_match_success, liveness_passed,
        )

    return {
        "faceMatchSuccess": bool(face_match_success),
        "livenessPassed": bool(liveness_passed),
        "framesProcessed": frames_with_face,
    }


def _log_verify_diagnostics(employee_id, label_id, frames_b64, decoded_count, undecodable_count,
                             frames_with_face, match_votes, eyes_open_history, frame_log,
                             face_match_success, liveness_passed):
    """Prints a one-attempt diagnostic block to this process's console only. No image data,
    no disk writes, nothing added to the HTTP response — safe to leave on during real-device
    troubleshooting and just as safely deleted afterwards."""
    vote_fraction = (match_votes / frames_with_face) if frames_with_face > 0 else 0.0
    confidences = [f["confidence"] for f in frame_log if f.get("faceDetected")]
    ears = [f["ear"] for f in frame_log if f.get("ear") is not None]

    print("=" * 70, flush=True)
    print(f"[face-verify] employeeId={employee_id} labelId={label_id}", flush=True)
    print(f"[face-verify] framesReceived={len(frames_b64)} decoded={decoded_count} "
          f"undecodable={undecodable_count} framesWithFace={frames_with_face}", flush=True)
    print(f"[face-verify] matchVotes={match_votes}/{frames_with_face} "
          f"voteFraction={vote_fraction:.2f} (threshold={MATCH_VOTE_FRACTION}) "
          f"-> faceMatchSuccess={face_match_success}", flush=True)
    if confidences:
        print(f"[face-verify] confidence per matched-face frame: {confidences} "
              f"(best={min(confidences)}, worst={max(confidences)}, threshold<={MATCH_CONFIDENCE_THRESHOLD})",
              flush=True)
    winning_employee_ids = [f["predictedEmployeeId"] for f in frame_log if f.get("faceDetected")]
    if winning_employee_ids:
        tally = {}
        for eid in winning_employee_ids:
            tally[eid] = tally.get(eid, 0) + 1
        print(f"[face-verify] predictedEmployeeId tally across burst (claimed={employee_id}): {tally}",
              flush=True)
    if ears:
        print(f"[face-verify] EAR per frame (landmark-based): {ears} "
              f"(min={min(ears)}, max={max(ears)}, closedThreshold<{EAR_CLOSED_THRESHOLD})", flush=True)
    else:
        print("[face-verify] EAR per frame: no frame produced face landmarks.", flush=True)
    print(f"[face-verify] eyesOpenHistory={[('O' if v else 'C') for v in eyes_open_history]} "
          f"-> livenessPassed={liveness_passed}", flush=True)
    margins = [f["margin"] for f in frame_log if f.get("margin") is not None]
    if margins:
        print(f"[face-verify] claimed-vs-best-alternative margin per frame: {margins} "
              f"(min={min(margins)}, max={max(margins)}) — negative means another registered "
              f"employee matched more closely than the claimed identity did", flush=True)

    for f in frame_log:
        face_part = (
            f"claimedEmployeeId={f['claimedEmployeeId']} (labelId={f['claimedLabelId']}) "
            f"predictedLabelId={f['predictedLabel']} predictedEmployeeId={f['predictedEmployeeId']} "
            f"confidence={f['confidence']} matched={f['matched']}"
            if f.get("faceDetected") else "faceDetected=False"
        )
        margin_part = (
            f" | allLabelConfidences={f['allLabelConfidences']} "
            f"bestAlternative={f['bestAlternativeEmployeeId']}@{f['bestAlternativeConfidence']} "
            f"margin={f['margin']}"
            if f.get("allLabelConfidences") is not None else ""
        )
        ear_part = (f"ear={f['ear']} (L={f['earLeft']} R={f['earRight']}) eyeOpen={f['eyeOpen']}"
                    if f.get("ear") is not None else "ear=None (no landmarks this frame)")
        print(f"[face-verify]   frame {f['frame']}: img={f['imgSize']} {face_part}{margin_part} | {ear_part}", flush=True)
    print("=" * 70, flush=True)


def _detected_blink(eyes_open_history):
    """Liveness signal: an open -> closed -> open transition across the burst, where the
    closed phase must span at least MIN_CONSECUTIVE_CLOSED_FRAMES consecutive frames.

    A single isolated closed frame is not accepted as a blink on its own — real-device
    testing (2026-10-02) showed a phone-screen-displayed static-photo spoof occasionally
    producing one spurious "closed" reading from landmark-detection noise (camera shake,
    screen refresh/rolling-shutter interaction, autofocus hunting), which the previous
    single-frame rule accepted as a full blink. A genuine blink's closed phase (~100-400ms)
    should span more than one frame at this burst's fixed 150ms interval, so requiring a
    sustained run raises the bar against that specific noise source without touching
    EAR_CLOSED_THRESHOLD, frame timing, or anything else about how a frame is read."""
    MIN_CONSECUTIVE_CLOSED_FRAMES = 2

    if len(eyes_open_history) < 3:
        return False

    saw_open_before_close = False
    saw_confirmed_close_after_open = False
    consecutive_closed = 0

    for is_open in eyes_open_history:
        if is_open:
            if saw_confirmed_close_after_open:
                return True
            saw_open_before_close = True
            consecutive_closed = 0
        else:
            consecutive_closed += 1
            if saw_open_before_close and consecutive_closed >= MIN_CONSECUTIVE_CLOSED_FRAMES:
                saw_confirmed_close_after_open = True

    return False


def delete_employee(employee_id):
    """
    Removes an employee's stored reference photos and retrains the shared model from
    whoever's left. LBPH has no API to "forget" a single label from an already-trained
    model, so a full rebuild from the remaining employees' saved photos is the only way
    to actually remove someone rather than just hiding their label mapping.
    """
    employee_id = str(employee_id)
    employee_dir = os.path.join(FACES_DIR, employee_id)
    if os.path.isdir(employee_dir):
        shutil.rmtree(employee_dir)

    labels = _load_labels()
    label_id = next((lid for lid, eid in labels.items() if eid == employee_id), None)
    if label_id is None:
        return  # nothing was registered for this employee — nothing left to rebuild

    del labels[label_id]
    _save_labels(labels)
    _rebuild_model(labels)


def _rebuild_model(labels):
    faces = []
    face_label_ids = []

    for label_id, employee_id in labels.items():
        employee_dir = os.path.join(FACES_DIR, employee_id)
        if not os.path.isdir(employee_dir):
            continue
        for filename in os.listdir(employee_dir):
            img = cv2.imread(os.path.join(employee_dir, filename), cv2.IMREAD_GRAYSCALE)
            if img is not None:
                faces.append(img)
                face_label_ids.append(label_id)

    if not faces:
        # No one has a registered face anymore — remove the stale model rather than
        # leave a trained-on-nobody file behind.
        if os.path.exists(MODEL_PATH):
            os.remove(MODEL_PATH)
        return

    recognizer = cv2.face.LBPHFaceRecognizer_create()
    recognizer.train(faces, np.array(face_label_ids))
    recognizer.write(MODEL_PATH)
