// Native dialog håndterer fokusfelle og synkron fokusretur. Avbrudd hører til én visning.
const tilstander = new WeakMap();

export function vis(dialog, visning, komponent) {
    const side = tilstander.get(dialog) ?? {};
    if (!dialog.isConnected || dialog.dataset.redigerer !== "true"
        || dialog.dataset.visning !== String(visning) || side.avbrutt === String(visning)) return;
    side.komponent = komponent;
    tilstander.set(dialog, side);
    if (!dialog.dataset.koblet) {
        dialog.addEventListener("cancel", event => {
            // Serverens arbeidslås kan være nyere enn den sist leverte renderen.
            // La komponenten avgjøre avbruddet før native close/fokusretur.
            event.preventDefault();
            const side = tilstander.get(dialog);
            side.komponent.invokeMethodAsync("AvbruttFraTastaturAsync", Number(dialog.dataset.visning));
        });
        dialog.dataset.koblet = "true";
    }
    if (!dialog.open) dialog.showModal();
    dialog.querySelector("h2")?.focus({ preventScroll: true });
}

export function lukk(dialog, visning) {
    if (dialog.dataset.visning !== String(visning)) return;
    const side = tilstander.get(dialog);
    if (side) side.avbrutt = String(visning);
    dialog.close();
}
