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
// isn't overridable via HTML/CSS — so SASMS can't just rely on it to always show DD/MM/YY.
// Each ".date-field" (see site.css) pairs the real, visually-hidden native input (still the
// thing actually submitted/bound — value/name/validation untouched) with a readonly text
// field that always displays dd/MM/yy. Clicking/activating the text field opens the native
// picker; the native input's own change event keeps the text field in sync.
document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('.date-field').forEach(function (wrapper) {
        var native = wrapper.querySelector('.date-field-native');
        var text = wrapper.querySelector('.date-field-text');
        if (!native || !text) {
            return;
        }

        function updateText() {
            var parts = native.value.split('-'); // native value is always yyyy-MM-dd
            text.value = parts.length === 3 ? (parts[2] + '/' + parts[1] + '/' + parts[0].slice(2)) : '';
        }

        function openPicker() {
            if (typeof native.showPicker === 'function') {
                native.showPicker();
            } else {
                native.focus();
            }
        }

        text.addEventListener('click', openPicker);
        text.addEventListener('keydown', function (event) {
            if (event.key === 'Enter' || event.key === ' ') {
                event.preventDefault();
                openPicker();
            }
        });
        native.addEventListener('change', updateText);

        updateText();
    });
});
