(function () {
    'use strict';

    const markerKey = 'aero-setup-handoff';
    const siteIdKey = 'aero-admin-state.siteId';
    const siteNameKey = 'aero-admin-state.siteName';
    const maximumWaitMs = 5 * 60 * 1000;
    let controller = null;
    let startedAt = 0;
    let lastState = '';

    function clearSiteSelection() {
        try {
            localStorage.removeItem(siteIdKey);
            localStorage.removeItem(siteNameKey);
        } catch {
            // Browser storage can be unavailable in restricted browsing modes.
        }
    }

    function persistCreatedSite(status) {
        const siteId = String(status.createdSiteId || '');
        if (!/^[1-9]\d*$/.test(siteId)) return false;

        try {
            // Store the Snowflake identifier as a JSON string. JavaScript numbers
            // cannot safely represent every 64-bit Aero identifier.
            localStorage.setItem(siteIdKey, JSON.stringify(siteId));
            localStorage.setItem(siteNameKey, JSON.stringify(status.siteName || 'Site'));
        } catch {
            // The setup-status response still writes the authoritative HttpOnly cookie.
        }

        return true;
    }

    function elements() {
        return {
            overlay: document.getElementById('setup-handoff'),
            content: document.getElementById('setup-interactive-content'),
            status: document.getElementById('setup-handoff-status'),
            title: document.getElementById('setup-handoff-title'),
            detail: document.getElementById('setup-handoff-detail'),
            error: document.getElementById('setup-handoff-error'),
            actions: document.getElementById('setup-handoff-actions'),
            check: document.getElementById('setup-handoff-check'),
            returnButton: document.getElementById('setup-handoff-return')
        };
    }

    function show() {
        const view = elements();
        if (!view.overlay) return false;
        view.overlay.hidden = false;
        document.body.style.overflow = 'hidden';
        if (view.content) {
            view.content.inert = true;
            view.content.setAttribute('aria-hidden', 'true');
        }
        return true;
    }

    function setState(key, title, detail) {
        if (lastState === key) return;
        lastState = key;
        const view = elements();
        if (view.title) view.title.textContent = title;
        if (view.detail) view.detail.textContent = detail;
    }

    function sleep(milliseconds, signal) {
        return new Promise((resolve, reject) => {
            const timer = window.setTimeout(resolve, milliseconds);
            signal.addEventListener('abort', () => {
                window.clearTimeout(timer);
                reject(new DOMException('Aborted', 'AbortError'));
            }, { once: true });
        });
    }

    async function fetchWithTimeout(url, options, parentSignal, timeoutMs) {
        const requestController = new AbortController();
        const abortRequest = function () { requestController.abort(); };
        if (parentSignal.aborted) {
            requestController.abort();
        } else {
            parentSignal.addEventListener('abort', abortRequest, { once: true });
        }
        const timeout = window.setTimeout(abortRequest, timeoutMs);

        try {
            return await fetch(url, Object.assign({}, options, { signal: requestController.signal }));
        } finally {
            window.clearTimeout(timeout);
            parentSignal.removeEventListener('abort', abortRequest);
        }
    }

    async function poll(signal) {
        let delay = 700;
        let networkFailures = 0;
        while (!signal.aborted) {
            if (Date.now() - startedAt > maximumWaitMs) {
                fail('The main application has not reported ready yet. Check the application logs, then try the readiness check again.', true);
                return;
            }

            try {
                const response = await fetchWithTimeout(
                    '/setup/status',
                    { cache: 'no-store' },
                    signal,
                    8000);
                if (!response.ok) throw new Error('Setup status returned ' + response.status);
                const status = await response.json();
                networkFailures = 0;
                delay = 700;

                if (String(status.state).toLowerCase() === 'failed') {
                    fail('Runtime initialization failed. Check the application logs, then try the readiness check again.', true);
                    return;
                }

                if (String(status.state).toLowerCase() === 'running'
                    && status.setupComplete === true
                    && status.seedComplete === true
                    && persistCreatedSite(status)) {
                    setState('opening', 'Your site is ready', 'Opening your homepage…');
                    const home = await fetchWithTimeout(
                        '/',
                        { cache: 'no-store', redirect: 'follow' },
                        signal,
                        30000);
                    const destination = new URL(home.url, window.location.origin);
                    if (home.ok && destination.origin === window.location.origin && destination.pathname === '/') {
                        sessionStorage.removeItem(markerKey);
                        window.location.replace('/');
                        return;
                    }
                } else {
                    setState('preparing', 'Preparing your site', 'Initializing services and publishing your starter content.');
                }
            } catch (error) {
                if (signal.aborted) return;
                networkFailures += 1;
                if (networkFailures >= 2) {
                    setState('restarting', 'Restarting Aero CMS', 'A brief connection pause is expected. Keep this tab open.');
                }
                delay = Math.min(Math.round(delay * 1.5), 4000);
            }

            try {
                await sleep(delay + Math.floor(Math.random() * 180), signal);
            } catch (error) {
                if (signal.aborted && error instanceof DOMException && error.name === 'AbortError') return;
                throw error;
            }
        }
    }

    function begin() {
        if (!show()) throw new Error('Setup handoff overlay is unavailable.');
        if (controller) controller.abort();
        controller = new AbortController();
        startedAt = Date.now();
        lastState = '';
        sessionStorage.setItem(markerKey, 'pending');
        const view = elements();
        if (view.error) view.error.hidden = true;
        if (view.actions) view.actions.hidden = true;
        if (view.status) {
            view.status.setAttribute('role', 'status');
            view.status.setAttribute('aria-busy', 'true');
        }
        setState('starting', 'Starting Aero CMS', 'Saving your configuration and handing off to the main application.');
        void poll(controller.signal);
    }

    function fail(message, canRetry) {
        if (controller) controller.abort();
        const view = elements();
        show();
        if (canRetry) {
            sessionStorage.setItem(markerKey, 'pending');
        } else {
            sessionStorage.removeItem(markerKey);
        }
        setState('failed', 'Aero CMS didn\'t become ready', 'The application could not complete the startup handoff.');
        if (view.status) {
            view.status.setAttribute('role', 'alert');
            view.status.setAttribute('aria-busy', 'false');
        }
        if (view.error) {
            view.error.textContent = message || 'Check the application logs, then try again.';
            view.error.hidden = false;
        }
        if (view.actions) view.actions.hidden = false;
        if (view.check) view.check.hidden = !canRetry;
        if (view.title) view.title.focus();
    }

    function cancel() {
        if (controller) controller.abort();
        controller = null;
        sessionStorage.removeItem(markerKey);
        const view = elements();
        if (view.overlay) view.overlay.hidden = true;
        if (view.content) {
            view.content.inert = false;
            view.content.removeAttribute('aria-hidden');
        }
        document.body.style.overflow = '';
    }

    function resumeIfPending() {
        if (sessionStorage.getItem(markerKey) === 'pending') begin();
    }

    document.addEventListener('DOMContentLoaded', function () {
        const view = elements();
        if (view.check) view.check.addEventListener('click', begin);
        if (view.returnButton) view.returnButton.addEventListener('click', cancel);
        resumeIfPending();
    }, { once: true });

    window.AeroSetupHandoff = { begin, fail, resumeIfPending };
    clearSiteSelection();
})();
