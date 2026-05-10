window.hideAppSplash = function () {
    var splash = document.getElementById('app-splash');
    if (splash) {
        splash.classList.add('hidden');
        splash.addEventListener('transitionend', function () {
            if (splash.parentNode) splash.parentNode.removeChild(splash);
        }, { once: true });
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
