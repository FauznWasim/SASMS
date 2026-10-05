# Production image for the SASMS face-service (Python/Flask/OpenCV/MediaPipe).
#
# Build context must be the REPO ROOT (see docker-compose.yml: `build: {context: .,
# dockerfile: face-service/Dockerfile}`) so .dockerignore can reliably exclude
# face-service/data/ and face-service/models/ from ever being copied into the image — the
# registered-face biometric data and the downloaded landmark model are real data, not
# source, and must only ever arrive via the mounted volumes in docker-compose.yml, never
# baked into the image.
FROM python:3.12-slim

WORKDIR /app

# opencv-contrib-python's Linux wheel dynamically links against a handful of system
# graphics/X11 libraries even when only used headlessly (no window shown) — without these,
# `import cv2` fails at container startup with "libGL.so.1: cannot open shared object
# file". A well-known OpenCV-on-Docker requirement, not specific to this project.
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        libgl1 \
        libglib2.0-0 \
        libsm6 \
        libxext6 \
        libxrender1 \
        libegl1 \
    && rm -rf /var/lib/apt/lists/*

COPY face-service/requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY face-service/app.py face-service/face_engine.py ./

# data/ and models/ are NOT copied in — they're populated at runtime from the volumes
# declared in docker-compose.yml (face-data, face-models). The app itself already creates
# data/faces/ on demand (os.makedirs in face_engine.py); models/ must be populated once by
# downloading face_landmarker.task into the mounted volume (see face-service/README.md).
RUN mkdir -p /app/data/faces /app/models

# Not published to the host anywhere in docker-compose.yml — EXPOSE here is documentation
# only (container-to-container communication doesn't require it), the real access control
# is "no `ports:` entry for this service" in that file.
EXPOSE 5100

# FACE_SERVICE_SHARED_SECRET is intentionally NOT set here — it's supplied at container-run
# time via docker-compose.yml's `environment:`, sourced from the (gitignored) .env file.
# No secret of any kind is baked into this image.
CMD ["python", "app.py"]
