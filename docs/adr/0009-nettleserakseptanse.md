# Separat nettlesergrense for samlet akseptanse

Oppgave #36 krever faktisk bruk av Blazor-skjemaer og navigasjon. Application- og
HTTP-testene kan ikke vise om en interaktiv krets bryter sammen, om et skjema sender
riktige felter, eller om en nedlasting og innlogging virker fra nettleseren.

Aarshjul.BrowserTests bruker Playwright mot en ekte, separat Kestrel-prosess. Hvert
scenario får en ny SQLite-fil, standard demo-personaer og egne syntetiske data.
Personabytte oppretter en ny nettleserkontekst og laster siden helt på nytt. UI-handlinger
følges av synlige utfall; lese-API og anonyme HTTP-kall kontrollerer returnerte data.
Routes viser en skjult klar-markør etter første interaktive render, slik at testene
venter på hendelsesbehandlere i stedet for å skrive i prerenderte skjemaer.

Prosjektet ligger utenfor den ordinære løsningen, som SQL Server-testene. Ingen
nettleseravhengighet legges i produksjonsprosjektene. Egen CI-jobb installerer Chromium
fra den låste Playwright-versjonen og krever alle scenarioer uten hopp; gjeldende antall
står i [utviklingsoppsettet](../development.md#obligatorisk-nettleserakseptanse).
Deploy gjenbruker hele CI og krever dermed alle tre testjobbene.

Tokenfeed testes anonymt etter oppretting i UI. Bearer-URL-er beholdes i minnet;
abonnementssidens feil pakkes om uten ARIA-snapshot, URL eller indre unntak i TRX.
CI laster bare opp testresultater. Databaser, app-logger, skjermbilder og nedlastinger
beholdes lokalt i disponibel evidensmappe. Demo beviser verken Entra, Azure eller Outlook.
