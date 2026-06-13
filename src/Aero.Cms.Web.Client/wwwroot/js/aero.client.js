"use strict";
var aero;
(function (aero) {
    let setup;
    (function (setup) {
        /**
         * Clears browser localStorage, sessionStorage, and all cookies.
         * Called after setup completes to ensure the fresh app starts clean.
         */
        function clearStorage() {
            try {
                localStorage.clear();
            }
            catch (_) { /* cross-origin or private mode may block */ }
            try {
                sessionStorage.clear();
            }
            catch (_) { /* cross-origin or private mode may block */ }
            try {
                document.cookie
                    .split(";")
                    .forEach(c => {
                    const eq = c.indexOf("=");
                    const name = eq > -1
                        ? c.substring(0, eq).trim()
                        : c.trim();
                    if (!name)
                        return;
                    document.cookie = name +
                        "=;expires=" + new Date(0).toUTCString() +
                        ";path=/";
                });
            }
            catch (_) { /* cookie access blocked */ }
        }
        setup.clearStorage = clearStorage;
    })(setup = aero.setup || (aero.setup = {}));
})(aero || (aero = {}));
