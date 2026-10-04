// Unobtrusive confirm-before-submit, used instead of inline onsubmit="" handlers so the
// app can ship a Content-Security-Policy without 'unsafe-inline' on script-src.
document.addEventListener('submit', function (event) {
    var form = event.target;
    if (form instanceof HTMLFormElement && form.dataset.confirm) {
        if (!window.confirm(form.dataset.confirm)) {
            event.preventDefault();
        }
    }
});

// Show/hide toggle for password fields — button lives in the same .input-group as the
// <input>, marked up once per field in the view (see Login.cshtml / ChangePassword.cshtml).
document.addEventListener('click', function (event) {
    var btn = event.target.closest('.password-toggle-btn');
    if (!btn) {
        return;
    }

    var group = btn.closest('.input-group');
    var input = group ? group.querySelector('input') : null;
    if (!input) {
        return;
    }

    var willShow = input.type === 'password';
    input.type = willShow ? 'text' : 'password';
    btn.setAttribute('aria-label', willShow ? 'Hide password' : 'Show password');

    var eyeIcon = btn.querySelector('.icon-eye');
    var eyeOffIcon = btn.querySelector('.icon-eye-off');
    if (eyeIcon && eyeOffIcon) {
        eyeIcon.style.display = willShow ? 'none' : '';
        eyeOffIcon.style.display = willShow ? '' : 'none';
    }
});
