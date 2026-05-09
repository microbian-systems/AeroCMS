// Tippy.js-based tooltips for collapsed manager sidebars.
window.PeNavTooltip = (function () {
    var selector = '[data-pe-tooltip-target]';
    var retryTimer = 0;
    var observer = null;

    function getTippy() {
        if (typeof window.tippy === 'function') {
            return window.tippy;
        }

        if (window.tippy && typeof window.tippy.default === 'function') {
            return window.tippy.default;
        }

        return null;
    }

    function scheduleRetry() {
        if (retryTimer) {
            return;
        }

        retryTimer = window.setTimeout(function () {
            retryTimer = 0;
            refresh();
        }, 50);
    }

    function destroyTooltip(el) {
        if (el._tippy) {
            el._tippy.destroy();
        }
    }

    function getContent(el) {
        return (el.getAttribute('data-pe-tooltip') || '').trim();
    }

    function getPlacement(el) {
        return el.getAttribute('data-pe-tooltip-placement') || 'right';
    }

    function refresh() {
        var tippy = getTippy();

        if (!tippy) {
            scheduleRetry();
            return getDiagnostics();
        }

        document.querySelectorAll(selector).forEach(function (el) {
            syncTooltip(el, tippy);
        });

        return getDiagnostics();
    }

    function syncTooltip(el, tippy) {
        var content = getContent(el);
        var placement = getPlacement(el);

        if (!content) {
            destroyTooltip(el);
            return null;
        }

        if (el._tippy) {
            el._tippy.setContent(content);
            el._tippy.setProps({ placement: placement });
            return el._tippy;
        }

        return tippy(el, {
            content: content,
            placement: placement,
            trigger: 'manual',
            delay: [100, 0],
            arrow: tippy.roundArrow || true,
            appendTo: document.body,
            hideOnClick: false,
            theme: 'aero-manager',
            animation: 'fade',
            duration: [100, 75],
            maxWidth: 220,
            offset: [0, 8],
            touch: false,
            zIndex: 999999
        });
    }

    function ensureTooltip(el) {
        var tippy = getTippy();

        if (!tippy || !el || !el.matches(selector)) {
            return null;
        }

        return syncTooltip(el, tippy);
    }

    function findTarget(target) {
        return target && target.closest ? target.closest(selector) : null;
    }

    function onPointerOver(event) {
        var el = findTarget(event.target);
        var instance = ensureTooltip(el);

        if (instance && getContent(el)) {
            instance.show();
        }
    }

    function onPointerOut(event) {
        var el = findTarget(event.target);

        if (!el || !el._tippy) {
            return;
        }

        if (event.relatedTarget && el.contains(event.relatedTarget)) {
            return;
        }

        el._tippy.hide();
    }

    function onFocusIn(event) {
        var el = findTarget(event.target);
        var instance = ensureTooltip(el);

        if (instance && getContent(el)) {
            instance.show();
        }
    }

    function onFocusOut(event) {
        var el = findTarget(event.target);

        if (el && el._tippy) {
            el._tippy.hide();
        }
    }

    function observeDom() {
        if (observer || typeof MutationObserver === 'undefined') {
            return;
        }

        observer = new MutationObserver(scheduleRetry);
        observer.observe(document.body, {
            attributes: true,
            childList: true,
            subtree: true,
            attributeFilter: ['data-pe-tooltip', 'data-pe-tooltip-placement']
        });
    }

    function getDiagnostics() {
        var targets = Array.prototype.slice.call(document.querySelectorAll(selector));

        return {
            popperLoaded: typeof window.Popper !== 'undefined',
            tippyLoaded: !!getTippy(),
            targetCount: targets.length,
            activeContentCount: targets.filter(function (el) { return !!getContent(el); }).length,
            instanceCount: targets.filter(function (el) { return !!el._tippy; }).length,
            visibleCount: targets.filter(function (el) { return !!(el._tippy && el._tippy.state.isVisible); }).length
        };
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () {
            refresh();
            observeDom();
        });
    } else {
        refresh();
        observeDom();
    }

    document.addEventListener('pointerover', onPointerOver, true);
    document.addEventListener('pointerout', onPointerOut, true);
    document.addEventListener('focusin', onFocusIn, true);
    document.addEventListener('focusout', onFocusOut, true);

    return {
        refresh: refresh,
        diagnostics: getDiagnostics
    };
})();
