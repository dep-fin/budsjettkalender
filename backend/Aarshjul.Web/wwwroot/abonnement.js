// Native dialog eier Escape, fokusfelle og synkron fokusretur.
const avbrutte = new WeakSet();
export function vis(dialog, valg, referanse) {
    if (!dialog.isConnected || dialog.dataset.valg !== String(valg)
        || dialog.dataset.bekreft !== "true" || avbrutte.has(dialog) || dialog.open) return;
    if (!dialog.dataset.cancelLytter) {
        dialog.addEventListener("cancel", event => {
            if (dialog.dataset.arbeider === "true") { event.preventDefault(); return; }
            avbrutte.add(dialog);
            referanse.invokeMethodAsync("AvbrytFraDialog", valg).catch(() => {});
        });
        dialog.dataset.cancelLytter = "true";
    }
    dialog.showModal();
}
export function kanBekrefte(dialog, valg) {
    return dialog.isConnected && dialog.open && !avbrutte.has(dialog)
        && dialog.dataset.valg === String(valg) && dialog.dataset.bekreft === "true";
}
export function lukk(dialog) { if (dialog.open) dialog.close(); }
export function fokuser(id, valg) {
    const dialog = document.querySelector(".abonnementdialog");
    if (dialog?.dataset.valg === String(valg) && dialog.dataset.bekreft !== "true") document.getElementById(id)?.focus();
}
