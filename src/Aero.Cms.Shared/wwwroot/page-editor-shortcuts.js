window.PageEditorShortcuts = (() => {
    let handler;
    let editableGuard;
    const guardedEditableTargets = new WeakSet();

    function isEditableTarget(target) {
        if (!(target instanceof HTMLElement)) {
            return false;
        }

        return Boolean(target.closest(
            "input, textarea, select, [contenteditable='true'], [role='textbox'], [data-editor-text-input='true']"));
    }

    function register(dotNetRef) {
        unregister();
        editableGuard = event => {
            const target = event.target instanceof HTMLElement
                ? event.target.closest(
                    "input, textarea, select, [contenteditable='true'], [role='textbox'], [data-editor-text-input='true']")
                : undefined;

            if (target instanceof HTMLElement && !guardedEditableTargets.has(target)) {
                target.addEventListener("keydown", keyEvent => keyEvent.stopPropagation());
                guardedEditableTargets.add(target);
            }
        };

        document.addEventListener("focusin", editableGuard, true);

        handler = event => {
            if (isEditableTarget(event.target)) {
                return;
            }

            const modifier = event.ctrlKey || event.metaKey;
            const key = event.key.toLowerCase();
            let command;

            if (modifier && key === "z") command = event.shiftKey ? "redo" : "undo";
            else if (modifier && key === "y") command = "redo";
            else if (modifier && key === "c") command = "copy";
            else if (modifier && key === "x") command = "cut";
            else if (modifier && key === "v") command = "paste";
            else if (modifier && key === "d") command = "duplicate";
            else if (event.key === "Delete" || event.key === "Backspace") command = "delete";

            if (!command) {
                return;
            }

            event.preventDefault();
            void dotNetRef.invokeMethodAsync("HandleEditorShortcut", command);
        };

        document.addEventListener("keydown", handler, true);
    }

    function unregister() {
        if (editableGuard) {
            document.removeEventListener("focusin", editableGuard, true);
            editableGuard = undefined;
        }

        if (handler) {
            document.removeEventListener("keydown", handler, true);
            handler = undefined;
        }
    }

    return { register, unregister };
})();
