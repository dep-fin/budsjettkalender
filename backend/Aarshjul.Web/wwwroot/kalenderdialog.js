// Native dialog gir fokusfelle, Escape og fokusretur ved lukking.
// En egen fokusretur i den køede close-hendelsen kan stjele fokus fra neste utløser.
const innhold = new WeakMap();

export function vis(dialog, fristId, harAgenda, komponent, fokusHandling) {
    innhold.set(dialog, { harAgenda, komponent });
    if (!dialog.open) {
        if (!dialog.dataset.tastatur) {
            dialog.addEventListener("cancel", event => {
                const side = innhold.get(dialog);
                if (dialog.dataset.sender === "true") {
                    event.preventDefault();
                    return;
                }
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
    const handling = fokusHandling
        ? Array.from(dialog.querySelectorAll("[data-dialoghandling]")).find(knapp => knapp.dataset.dialoghandling === fokusHandling)
        : null;
    (handling ?? fristknapp ?? dialog.querySelector("h2")).focus({ preventScroll: true });
}

export function innsending(dialog, paagaar) {
    dialog.dataset.sender = paagaar ? "true" : "false";
}

export function lukk(dialog) {
    dialog.close();
}
