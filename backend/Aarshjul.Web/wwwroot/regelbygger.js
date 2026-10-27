// Native dialog eier fokusfelle, Escape og synkron fokusretur.
export function vis(dialog) {
    if (!dialog.isConnected || dialog.open) return;
    if (!dialog.dataset.cancelLytter) {
        dialog.addEventListener("cancel", event => {
            if (dialog.dataset.arbeider === "true") event.preventDefault();
        });
        dialog.dataset.cancelLytter = "true";
    }
    dialog.showModal();
}
export function lukk(dialog) {
    if (dialog.open) dialog.close();
}
export function fokuser(id) { document.getElementById(id)?.focus(); }
