const avbrutt = new WeakMap();

// Deklarativ visningsidentitet beskytter også mot JS-kall som ankommer etter et årsskifte.
export function synkroniser(dialog, visning, komponent) {
    if (!dialog.isConnected || dialog.dataset.visning !== visning) return;
    if (!dialog.dataset.tastatur) {
        dialog.addEventListener("cancel", event => {
            if (dialog.dataset.arbeider === "true") { event.preventDefault(); return; }
            avbrutt.set(dialog, dialog.dataset.visning);
            komponent.invokeMethodAsync("AvbruttFraTastaturAsync", dialog.dataset.visning);
        });
        dialog.dataset.tastatur = "true";
    }
    if (dialog.dataset.bekreft !== "true") { if (dialog.open) dialog.close(); return; }
    if (avbrutt.get(dialog) !== visning && !dialog.open) dialog.showModal();
}

export function lukk(dialog) {
    avbrutt.set(dialog, dialog.dataset.visning);
    if (dialog.open) dialog.close();
}
