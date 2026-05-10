let dotNetRef = null;
let handler = null;

export function registerKeyboardShortcut(dotnetObj) {
    if (handler) {
        window.removeEventListener('keydown', handler);
    }
    dotNetRef = dotnetObj;
    handler = (e) => {
        if ((e.ctrlKey || e.metaKey) && e.shiftKey && e.key.toLowerCase() === 's') {
            e.preventDefault();
            if (dotNetRef) {
                dotNetRef.invokeMethodAsync('OnKeyboardShortcut');
            }
        }
    };
    window.addEventListener('keydown', handler);
}

export function unregisterKeyboardShortcut() {
    if (handler) {
        window.removeEventListener('keydown', handler);
    }
    handler = null;
    dotNetRef = null;
}
