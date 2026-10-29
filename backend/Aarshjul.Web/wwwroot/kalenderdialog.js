// Native dialog gir fokusfelle, Escape og fokusretur ved lukking.
// En egen fokusretur i den køede close-hendelsen kan stjele fokus fra neste utløser.
const innhold = new WeakMap();

export function vis(dialog, fristId, visning, komponent, fokusHandling) {
    const side = innhold.get(dialog) ?? {};
    // Renderen kan allerede ha skiftet innhold, eller brukeren kan ha lukket
    // dialogen mens dette JS-kallet var på vei fra serveren.
    if (!dialog.isConnected || dialog.dataset.visning !== String(visning) || side.avbruttVisning === String(visning)
        || side.avbruttAapning === dialog.dataset.aapning) return;
    side.komponent = komponent;
    innhold.set(dialog, side);
    if (!dialog.open) {
        if (!dialog.dataset.tastatur) {
            dialog.addEventListener("cancel", event => {
                const side = innhold.get(dialog);
                if (dialog.dataset.sender === "true") {
                    event.preventDefault();
                    return;
                }
                if (side) side.avbruttVisning = dialog.dataset.visning;
                // Returvalget følger innholdet som er synlig nå, også før
                // OnAfterRender sitt neste vis-kall har nådd nettleseren.
                if (dialog.dataset.tilbake === "true") {
                    event.preventDefault();
                    side.komponent.invokeMethodAsync("TilbakeFraTastaturAsync");
                } else if (side) {
                    side.avbruttAapning = dialog.dataset.aapning;
                    side.komponent.invokeMethodAsync("LukketAsync", Number(dialog.dataset.aapning));
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
    const side = innhold.get(dialog);
    if (side) {
        side.avbruttVisning = dialog.dataset.visning;
        side.avbruttAapning = dialog.dataset.aapning;
    }
    dialog.close();
}
