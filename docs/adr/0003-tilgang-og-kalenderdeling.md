# Medlemskapsstyrt administrator og delbar tokenfeed

Viderefører beslutningen fra 2026-07-30, som erstattet regelen fra 2026-06-18 om kun manuell
administratortildeling. Konfigurert SBR-gruppe i Entra gir automatisk administrator;
automatisk tildeling kan trekkes tilbake ved neste oppslag. Manuelle administratorer er
ment som nødvei. Denne hensikten må bevares når tildelingslogikken endres.

Kalenderklienter støtter ikke interaktiv Entra-innlogging for feeden. Et hemmelig,
tilbakekallbart token gir derfor tilgang til et serverfiltrert utvalg uten innlogging.
Lenken er en tilgangsrettighet, og nedlastede data kan ikke tilbakekalles. Beslutningen
om M365-native deling eller ytterligere feedbegrensninger er fortsatt åpen før produksjon.
