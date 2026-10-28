// En sen JS-fullføring skal ikke flytte fokus på en ny side eller et nyere utvalg.
export function fokuser(flate, valg, id) {
    if (!flate?.isConnected || flate.dataset.valg !== String(valg)) return;
    flate.querySelector(`#${id}`)?.focus();
}
