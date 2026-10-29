// Fragmentet sendes ikke i HTTP-URL eller Referer. Fjern det også fra historikken
// før bekreftelse, slik at serverens forespørselslogger aldri trenger en hemmelig lenke.
export function hentOgFjern() {
    const token = window.location.hash.substring(1);
    window.history.replaceState(null, "", window.location.pathname);
    return /^[0-9A-F]{64}$/.test(token) ? token : "";
}

// Bare et uttrykkelig knappetrykk kaller denne funksjonen. Ingen URL eller
// feilsvar inngår i feilmeldinger, og tokenet følger bare JSON-kroppen.
export async function meldAv(token) {
    try {
        const svar = await fetch("/api/epostabonnement/avmeldingslenke", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ token }),
            cache: "no-store",
            credentials: "omit",
            referrerPolicy: "no-referrer"
        });
        return svar.ok;
    } catch {
        return false;
    }
}
