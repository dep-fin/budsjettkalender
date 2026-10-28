// Fragmentet sendes ikke i HTTP-URL eller Referer. Fjern det også fra historikken
// før bekreftelse, slik at serverens forespørselslogger aldri trenger en hemmelig lenke.
export function hentOgFjern() {
    const token = window.location.hash.substring(1);
    window.history.replaceState(null, "", window.location.pathname);
    return /^[0-9A-F]{64}$/.test(token) ? token : "";
}
