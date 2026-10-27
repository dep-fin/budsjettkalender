// Native dialog gir fokusfelle, Escape og fokusretur ved lukking.
// En egen fokusretur i den køede close-hendelsen kan stjele fokus fra neste utløser.
const innhold = new WeakMap();

export function vis(dialog, fristId, harAgenda, komponent) {
    innhold.set(dialog, { harAgenda, komponent });
    if (!dialog.open) {
        if (!dialog.dataset.tastatur) {
            dialog.addEventListener("cancel", event => {
                const side = innhold.get(dialog);
                if (side?.harAgenda) {
                    event.preventDefault();
                    side.komponent.invokeMethodAsync("TilbakeFraTastaturAsync");
                }
            });
            dialog.dataset.tastatur = "true";
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
