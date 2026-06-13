const handledWindows = new WeakSet();

export function installBrowserLinkErrorSuppressors() {
    document
        .querySelectorAll('iframe[data-suppress-browser-link-errors="true"]')
        .forEach(installForFrame);
}

function installForFrame(frame) {
    if (frame.__aeroBrowserLinkLoadHandler !== true) {
        frame.addEventListener("load", () => installForFrameWindow(frame), true);
        frame.__aeroBrowserLinkLoadHandler = true;
    }

    installForFrameWindow(frame);
}

function installForFrameWindow(frame) {
    let frameWindow;

    try {
        frameWindow = frame.contentWindow;
    } catch {
        return;
    }

    if (!frameWindow || handledWindows.has(frameWindow)) {
        return;
    }

    handledWindows.add(frameWindow);

    const previousOnError = frameWindow.onerror;
    frameWindow.onerror = function (message, source, line, column, error) {
        if (isBrowserLinkError(message, source, error)) {
            return true;
        }

        return typeof previousOnError === "function"
            ? previousOnError.call(this, message, source, line, column, error)
            : false;
    };

    frameWindow.addEventListener("error", event => {
        if (!isBrowserLinkError(event.message, event.filename, event.error)) {
            return;
        }

        event.preventDefault();
        event.stopImmediatePropagation();
    }, true);

    frameWindow.addEventListener("unhandledrejection", event => {
        if (!isBrowserLinkError(event.reason)) {
            return;
        }

        event.preventDefault();
        event.stopImmediatePropagation();
    }, true);
}

function isBrowserLinkError(...values) {
    return values
        .map(toErrorText)
        .some(value => value.includes("/_vs/browserlink") || value.includes("\\_vs\\browserlink"));
}

function toErrorText(value) {
    if (!value) {
        return "";
    }

    if (typeof value === "string") {
        return value.toLowerCase();
    }

    const message = value.message ?? "";
    const stack = value.stack ?? "";
    const fileName = value.fileName ?? value.filename ?? "";

    return `${message} ${stack} ${fileName}`.toLowerCase();
}
