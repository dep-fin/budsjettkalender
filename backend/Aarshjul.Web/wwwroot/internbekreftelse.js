// Native dialog sørger for fokusfelle og synkron fokusretur. Serveren avgjør Escape
// også når en ventelås ennå ikke er levert til nettleseren.
const tilstander = new WeakMap();
export function oppdater(dialog, valg, komponent) {
    if (!dialog.isConnected || dialog.dataset.valg !== String(valg)) return;
    let tilstand = tilstander.get(dialog);
    if (!tilstand) {
        tilstand = {};
        tilstander.set(dialog, tilstand);
        dialog.addEventListener("cancel", event => {
            event.preventDefault();
            const aktuell = tilstander.get(dialog);
            aktuell.komponent.invokeMethodAsync("AvbruttFraTastaturAsync", Number(dialog.dataset.valg));
        });
    }
    tilstand.komponent = komponent;
    if (dialog.dataset.aapen === "true") {
        if (!dialog.open) dialog.showModal();
    } else if (dialog.open) {
        dialog.close();
    }
}
