// Årsvalgene tømmes under ny tilgangskontroll. Gruppen finnes også når
// det valgte året faller bort eller lesingen feiler.
export function forbered(gruppe, aar) {
    if (!gruppe?.isConnected || !gruppe.contains(document.activeElement) ||
        document.activeElement?.dataset.budsjettaar !== String(aar)) return false;
    gruppe.focus({ preventScroll: true });
    return document.activeElement === gruppe;
}

export function gjenopprett(gruppe, aar) {
    // En bruker som har gått videre under ventingen, beholder sitt nye fokus.
    if (!gruppe?.isConnected || document.activeElement !== gruppe || document.querySelector("dialog[open]")) return;
    const valg = Array.from(gruppe.querySelectorAll("input[data-budsjettaar]"))
        .find(felt => felt.dataset.budsjettaar === String(aar));
    if (valg?.checkVisibility()) valg.focus({ preventScroll: true });
}
