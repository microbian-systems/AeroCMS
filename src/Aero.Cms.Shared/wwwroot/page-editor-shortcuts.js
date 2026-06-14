window.PageEditorShortcuts = (() => {
    let handler;

    function isEditableTarget(target) {
        return target instanceof HTMLElement &&
            (target.isContentEditable ||
                target.matches("input, textarea, select, [role='textbox']"));
    }

    function register(dotNetRef) {
        unregister();
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
        if (handler) {
            document.removeEventListener("keydown", handler, true);
            handler = undefined;
        }
    }

    return { register, unregister };
})();
