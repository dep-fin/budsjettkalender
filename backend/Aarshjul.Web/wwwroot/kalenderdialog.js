// Native dialog gir fokusfelle og Escape. Behold den opprinnelige kalenderknappen
// også når innholdet skifter mellom dagsagenda og detaljer i samme dialog.
const utløsere = new WeakMap();
const innhold = new WeakMap();

export function vis(dialog, fristId, harAgenda, komponent) {
    innhold.set(dialog, { harAgenda, komponent });
    if (!dialog.open) {
        utløsere.set(dialog, document.activeElement);
        if (!dialog.dataset.fokusretur) {
            dialog.addEventListener("cancel", event => {
                const side = innhold.get(dialog);
                if (side?.harAgenda) {
                    event.preventDefault();
                    side.komponent.invokeMethodAsync("TilbakeFraTastaturAsync");
                }
            });
            dialog.addEventListener("close", () => {
                const utløser = utløsere.get(dialog);
                if (utløser?.isConnected) utløser.focus({ preventScroll: true });
            });
            dialog.dataset.fokusretur = "true";
        }
        dialog.showModal();
    }
    const fristknapp = fristId
        ? Array.from(dialog.querySelectorAll("[data-frist-id]")).find(knapp => knapp.dataset.fristId === fristId)
        : null;
    (fristknapp ?? dialog.querySelector("h2")).focus({ preventScroll: true });
}

export function lukk(dialog) {
    dialog.close();
}
