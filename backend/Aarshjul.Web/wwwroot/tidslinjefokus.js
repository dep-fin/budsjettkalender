// Gruppeutvidelsen og dette fokusønsket kommer i hver sin Blazor-leveranse.
// Brukeren kan allerede ha gått videre når ønsket når nettleseren.
export function fokuserGruppe(tidslinje, overskrift, versjon) {
    if (!tidslinje?.isConnected || tidslinje.dataset.fokusversjon !== versjon ||
        !overskrift?.isConnected || !tidslinje.contains(overskrift) ||
        !overskrift.checkVisibility() || document.querySelector("dialog[open]")) return;
    const gruppe = overskrift.closest("section[id]");
    const utloeser = Array.from(tidslinje.querySelectorAll("button[aria-controls]"))
        .find(knapp => knapp.getAttribute("aria-controls") === gruppe?.id);
    if (utloeser?.getAttribute("aria-expanded") !== "true" || document.activeElement !== utloeser) return;
    overskrift.focus();
}
