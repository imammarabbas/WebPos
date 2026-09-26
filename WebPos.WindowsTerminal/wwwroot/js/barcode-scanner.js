(function () {
    'use strict';

    var MIN_CODE_LENGTH = 4;
    var HUMAN_GAP_MS = 50;

    var _dotNetRef = null;
    var _active = false;
    var _buffer = '';
    var _lastKeyAt = 0;
    var _bound = false;

    function isEnterKey(e) {
        return e.key === 'Enter'
            || e.code === 'Enter'
            || e.code === 'NumpadEnter';
    }

    function isAllowedScanTarget(el) {
        if (!(el instanceof Element)) {
            // Unfocused / non-element target — allow wedge routing via global buffer.
            return true;
        }
        if (el.id === 'scanInput'
            || el.id === 'cpo-scan'
            || el.id === 'pc-barcode'
            || el.id === 'pc-search') {
            return true;
        }
        if (el.classList && el.classList.contains('scan-input')) {
            return true;
        }
        if (el.closest && el.closest('.scan-input')) {
            return true;
        }
        return false;
    }

    function isHumanOnlyTarget(el) {
        if (!(el instanceof Element)) {
            return false;
        }
        if (el.closest('.auth-shell')) {
            return true;
        }
        if (el.classList && el.classList.contains('sf-cell-input')) {
            return true;
        }
        if (el.closest('.sf-cell-input')) {
            return true;
        }
        // Payment tender / txn ref — never treat as barcode wedge.
        if (el.id === 'pm-tender' || el.id === 'pm-txn-ref') {
            return true;
        }
        if (el.classList && el.classList.contains('txn-ref-input')) {
            return true;
        }
        if (el.closest('#pm-tender, #pm-txn-ref, .txn-ref-input, .payment-modal-backdrop')) {
            // Allow .scan-input inside other modals; only block payment modal fields above.
            if (el.closest('.payment-modal-backdrop') && !isAllowedScanTarget(el)) {
                return true;
            }
        }
        return false;
    }

    function shouldIgnoreTarget(el) {
        if (isAllowedScanTarget(el)) {
            return false;
        }
        if (isHumanOnlyTarget(el)) {
            return true;
        }
        // Generic protected typing (inputs that are not scan fields).
        if (el instanceof Element) {
            var tag = (el.tagName || '').toLowerCase();
            if (tag === 'textarea' || tag === 'select') {
                return true;
            }
            if (tag === 'input') {
                var type = (el.getAttribute('type') || 'text').toLowerCase();
                if (type === 'password' || type === 'number' || type === 'tel') {
                    return true;
                }
                // Non-scan text inputs (e.g. customer name) — ignore for buffer.
                if (!isAllowedScanTarget(el)) {
                    return true;
                }
            }
            if (el.isContentEditable || el.getAttribute('contenteditable') === 'true') {
                return true;
            }
        }
        return false;
    }

    function clearBuffer() {
        _buffer = '';
        _lastKeyAt = 0;
    }

    function pickFocusTarget() {
        var cpo = document.getElementById('cpo-scan');
        if (cpo && !cpo.disabled && cpo.offsetParent !== null) {
            return cpo;
        }
        var barcode = document.getElementById('pc-barcode');
        if (barcode && !barcode.disabled && barcode.offsetParent !== null) {
            return barcode;
        }
        var scan = document.getElementById('scanInput');
        if (scan && !scan.disabled) {
            return scan;
        }
        return null;
    }

    function focusOk(el) {
        var active = document.activeElement;
        if (!active || active === document.body) {
            return true;
        }
        if (el && active === el) {
            return true;
        }
        if (active.id === 'scanInput' || active.id === 'cpo-scan' || active.id === 'pc-barcode') {
            return true;
        }
        return false;
    }

    /** After Blazor re-render, keep wedge keystrokes on the active scan field. */
    function ensureScanFocus() {
        if (document.querySelector('.auth-shell')) {
            return;
        }
        requestAnimationFrame(function () {
            requestAnimationFrame(function () {
                var target = pickFocusTarget();
                if (!target) {
                    return;
                }
                if (focusOk(target)) {
                    // Still ensure preferred field owns focus when body has it.
                    if (document.activeElement === document.body
                        || document.activeElement === document.documentElement) {
                        try {
                            target.focus({ preventScroll: true });
                            if (typeof target.select === 'function') {
                                target.select();
                            }
                        } catch (err) { /* ignore */ }
                    }
                    return;
                }
                try {
                    target.focus({ preventScroll: true });
                    if (typeof target.select === 'function') {
                        target.select();
                    }
                } catch (err) { /* ignore */ }
            });
        });
    }

    function onKeyDown(e) {
        if (!_active || !_dotNetRef) {
            return;
        }
        if (e.ctrlKey || e.altKey || e.metaKey) {
            return;
        }
        if (e.repeat) {
            return;
        }

        // Login PIN pad / auth UI — never buffer or steal Enter (capture runs before pin-pad bubble handler).
        if (document.querySelector('.auth-shell')) {
            clearBuffer();
            return;
        }

        var target = e.target;
        if (shouldIgnoreTarget(target)) {
            clearBuffer();
            return;
        }

        if (isEnterKey(e)) {
            var code = _buffer;
            clearBuffer();
            if (code.length >= MIN_CODE_LENGTH) {
                e.preventDefault();
                e.stopPropagation();
                try {
                    _dotNetRef.invokeMethodAsync('OnGlobalBarcodeScanned', code);
                } catch (err) { /* ignore */ }
                ensureScanFocus();
            }
            return;
        }

        if (!e.key || e.key.length !== 1) {
            return;
        }

        var now = performance.now();
        if (_lastKeyAt > 0 && (now - _lastKeyAt) > HUMAN_GAP_MS) {
            _buffer = '';
        }
        _buffer += e.key;
        _lastKeyAt = now;
    }

    function start(dotNetRef) {
        _dotNetRef = dotNetRef || null;
        _active = !!_dotNetRef;
        clearBuffer();
        if (_bound) {
            return;
        }
        _bound = true;
        document.addEventListener('keydown', onKeyDown, true);
    }

    function stop() {
        _active = false;
        _dotNetRef = null;
        clearBuffer();
        if (_bound) {
            document.removeEventListener('keydown', onKeyDown, true);
            _bound = false;
        }
    }

    function isActive() {
        return !!_active;
    }

    window.webPosBarcodeScanner = {
        start: start,
        stop: stop,
        isActive: isActive
    };
})();
