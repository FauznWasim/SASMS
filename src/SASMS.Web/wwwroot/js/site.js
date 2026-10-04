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

// Native <input type="date"> pickers render in the browser/OS locale's own format, which
// isn't overridable via HTML/CSS and may show MM/DD/YYYY for some users even though SASMS
// is a DD/MM/YYYY (Malaysia) application. Rather than fight the native widget, this shows an
// always-DD/MM/YYYY confirmation of the selected value next to each date field. The input's
// own value/name/validation are untouched, so form submission and model binding are unaffected.
document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('input[type="date"]').forEach(function (input) {
        var hint = document.createElement('small');
        hint.className = 'form-text text-muted sasms-date-hint';
        input.insertAdjacentElement('afterend', hint);

        function updateHint() {
            var parts = input.value.split('-'); // native value is always yyyy-MM-dd
            hint.textContent = parts.length === 3 ? (parts[2] + '/' + parts[1] + '/' + parts[0]) : '';
        }

        updateHint();
        input.addEventListener('change', updateHint);
    });
});
