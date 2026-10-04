// Webcam burst-capture used by Views/Attendance/Capture.cshtml for check-in, check-out,
// and face registration alike — behavior is parameterized entirely by the data-* attributes
// on #face-capture-root, set per-mode in the view.
(function () {
    const root = document.getElementById('face-capture-root');
    if (!root) {
        return;
    }

    const video = document.getElementById('preview');
    const canvas = document.getElementById('canvas');
    const startBtn = document.getElementById('startBtn');
    const statusText = document.getElementById('statusText');

    const targetAction = root.dataset.targetAction;
    const redirectUrl = root.dataset.redirectUrl;
    const frameCount = parseInt(root.dataset.frameCount, 10);
    const intervalMs = parseInt(root.dataset.intervalMs, 10);

    let stream = null;

    navigator.mediaDevices.getUserMedia({ video: true })
        .then(function (mediaStream) {
            stream = mediaStream;
            video.srcObject = mediaStream;
        })
        .catch(function (err) {
            statusText.textContent = 'Camera error: ' + err.message + '. Check that camera access is allowed for this site.';
            startBtn.disabled = true;
        });

    function captureFrame() {
        canvas.width = video.videoWidth;
        canvas.height = video.videoHeight;
        canvas.getContext('2d').drawImage(video, 0, 0);
        return canvas.toDataURL('image/jpeg', 0.8);
    }

    function delay(ms) {
        return new Promise(function (resolve) { setTimeout(resolve, ms); });
    }

    async function captureBurst() {
        const frames = [];
        for (let i = 0; i < frameCount; i++) {
            frames.push(captureFrame());
            await delay(intervalMs);
        }
        return frames;
    }

    function getCsrfToken() {
        const meta = document.querySelector('meta[name="csrf-token"]');
        return meta ? meta.content : '';
    }

    function stopCamera() {
        if (stream) {
            stream.getTracks().forEach(function (track) { track.stop(); });
        }
    }

    startBtn.addEventListener('click', async function () {
        startBtn.disabled = true;
        statusText.textContent = 'Capturing... keep your face in frame.';

        const frames = await captureBurst();

        statusText.textContent = 'Processing...';

        try {
            // redirect: 'manual' is essential here — fetch()'s default ('follow') makes the
            // browser silently perform a *second*, invisible request to the redirect target
            // to fetch it, and that phantom request is what actually reads (and clears) the
            // one-time TempData status message. The real page load that follows then finds
            // nothing left to show, even though the server set a message correctly. Manual
            // mode stops that: the POST's own Set-Cookie (carrying TempData) is still applied
            // to the browser, and the one real navigation below is what reads it.
            await fetch(targetAction, {
                method: 'POST',
                redirect: 'manual',
                headers: {
                    'Content-Type': 'application/json',
                    'X-CSRF-TOKEN': getCsrfToken()
                },
                body: JSON.stringify({ frames: frames })
            });

            stopCamera();
            window.location.assign(redirectUrl);
        } catch (err) {
            statusText.textContent = 'Upload failed: ' + err.message;
            startBtn.disabled = false;
        }
    });
})();
