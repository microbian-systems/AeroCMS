window.hideAppSplash = function () {
    var splash = document.getElementById('app-splash');
    if (splash) {
        splash.classList.add('hidden');
        var removeSplash = function () {
            if (splash.parentNode) splash.parentNode.removeChild(splash);
        };
        splash.addEventListener('transitionend', removeSplash, { once: true });
        window.setTimeout(removeSplash, 500);
    }
};

window.showAppSplash = function (message) {
    var splash = document.getElementById('app-splash');
    if (splash) {
        splash.classList.remove('hidden');
        var text = splash.querySelector('.app-splash-text');
        if (text && message) text.textContent = message;
    }
};

function failAeroAppStartup(error) {
    console.error('Aero Manager failed to start.', error);

    var splash = document.getElementById('app-splash');
    var title = document.querySelector('#app-splash .app-splash-title');
    var detail = document.querySelector('#app-splash .app-splash-text');
    var status = document.querySelector('#app-splash .app-splash-status');
    var retry = document.querySelector('#app-splash .app-splash-retry');
    if (splash) splash.classList.add('failed');
    if (title) title.textContent = 'Aero Manager couldn\'t start';
    if (detail) detail.textContent = 'Check your connection, then reload the manager.';
    if (status) {
        status.setAttribute('role', 'alert');
        status.setAttribute('aria-busy', 'false');
    }
    if (retry) {
        retry.hidden = false;
        retry.focus();
    }
}

window.startAeroApp = function () {
    if (window.__aeroAppStartPromise) return window.__aeroAppStartPromise;

    var slowTimer = window.setTimeout(function () {
        var title = document.querySelector('#app-splash .app-splash-title');
        var detail = document.querySelector('#app-splash .app-splash-text');
        if (title) title.textContent = 'Preparing your workspace';
        if (detail) detail.textContent = 'Connecting your session and loading manager tools.';
    }, 7000);

    if (!window.Blazor || typeof window.Blazor.start !== 'function') {
        window.clearTimeout(slowTimer);
        var unavailable = new Error('Blazor startup script is unavailable.');
        failAeroAppStartup(unavailable);
        return Promise.reject(unavailable);
    }

    window.__aeroAppStartPromise = window.Blazor.start().then(function () {
        window.clearTimeout(slowTimer);
        var title = document.querySelector('#app-splash .app-splash-title');
        var detail = document.querySelector('#app-splash .app-splash-text');
        var status = document.querySelector('#app-splash .app-splash-status');
        if (title) title.textContent = 'Aero Manager is ready';
        if (detail) detail.textContent = 'Opening your workspace…';
        if (status) status.setAttribute('aria-busy', 'false');
        window.setTimeout(window.hideAppSplash, 160);
    }).catch(function (error) {
        window.clearTimeout(slowTimer);
        failAeroAppStartup(error);
        throw error;
    });

    return window.__aeroAppStartPromise;
};

window.startAeroApp().catch(function () {
    // The failure state is rendered and logged by failAeroAppStartup.
});
