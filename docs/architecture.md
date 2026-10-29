# Arkitektur

ASP.NET Core / .NET 10 med Blazor Interactive Server. Azure SQL i produksjon,
SQLite i lokal demo og tester. Entra ID autentiserer produksjonsbrukere.

| Modul | Ansvar og testgrense |
| --- | --- |
| Aarshjul.Domain | Frist, forslag, regler, internrunde og rene datoberegninger |
| Aarshjul.Application | Offentlige tjenestegrensesnitt, DTO-er og tolkning/validering av regelkontrakter |
| Aarshjul.Infrastructure | EF Core, import/eksport, generering og uttrekk |
| Aarshjul.Web | Blazor, lese-/eksport-API, autentisering og policyer |
| Aarshjul.Kilder (backend/kilder) | IKilde og kildeuavhengige utfall/filtrering |
| Aarshjul.Tests | xUnit med SQLite og WebApplicationFactory |
| Aarshjul.SqlServerTests | Ekte SQL Server med migrasjoner; egen obligatorisk CI-jobb |
| Aarshjul.BrowserTests | Ekte Playwright-nettleser mot Kestrel og disponibel demo; egen obligatorisk CI-jobb |

## Kritiske strømmer

Kilde/opplasting → uttrekk → forslag → administratorgodkjenning → frist.
Mal → genererte forslag → egen gjennomgang → frist.
Lesing → synlighetsfilter i SQL → DTO → HTTP/Blazor/eksport.
Internkalenderens gjøremål behandles separat bak `ErSbr`.

E-postpåmelding → kontrollert adressebekreftelse → egen aktiv status → avmelding går
gjennom `IEpostabonnement`. Web henter stabil brukeridentitet fra innlogging ved hvert
HTTP-/Blazor-kall; ingen operasjon tar en mottatt identitet som eier fra skjemaet.
Infrastructure lagrer ett abonnement per identitet med versjonskontroll og skjuler
bekreftelsestransporten bak `IBekreftelsespost`. Bare demo/Testing registrerer den
kontrollerte mottakeren. Produksjon registrerer en utilgjengelig transport og kan ikke
opprette påmelding. Bekreftelsestransporten får fortsatt ingen fristdata.
Se [EV02-akseptansen](acceptance/2026-10-28-epost-ev02.md).

Direkte manuell oppretting → atomisk ny frist-hendelse → varig dagsplan → fersk
mottakertilgang → sentralt synlighetsfilter → egen tekst-/HTML-melding går gjennom
`IDagligEpostoppsummering.KjorAsync`. Tid, lagring, identitetskilde og posttransport
skjules i Infrastructure. En unik bruker/lokal dato og vinnende versjonsovergang
verner to instanser. Bekreftet overlevering gjentas ikke; mulig overlevering stanser
automatisk retry. Hendelses-IDer bevarer sene commits til neste dags oppsummering.
En felles koordinasjonsrad ordner adresseaktivering og publisering i databasen innen
samme transaksjon som de respektive endringene. Utvalget bruker publiseringssekvens
etter abonnentens aktiveringsgrense; UTC-klokken avgjør bare sendetid og lokal dato.
Bare Planlagt med forventet status og versjon kan gå til UnderSending.
Jobben starter etter Webs skjemaoppstart og vekkes av en TimeProvider-timer hvert
minutt; selve operasjonen håndhever kl. 08.00 Europe/Oslo eller konfigurert tid.
Testing og eksplisitt kontrollert demo kjører operasjonen direkte uten bakgrunnsjobb.
Produksjon har utilgjengelig tilgangskilde og oppsummeringstransport. Demo bruker et
selvstendig kontrollert identitetsregister, ikke sist lagrede innloggingsclaims.
Se [ADR-0010](adr/0010-varig-daglig-epostoppsummering.md) og
[EV03-akseptansen](acceptance/2026-10-28-epost-ev03.md).

Godkjenning og justering med godkjenning bruker samme varige hendelser og
databaseordnede lagring. Nytt bruker-/robot-/generert forslag gir Ny først ved
publisering. Endringsforslag sammenlikner faktisk innlest måltilstand med den
publiserte ettertilstanden og beholder synlighet, opphav og kildespor. En liten
felles hendelsesbygger skjuler vesentlighetsvalideringen for direkte redigering
og køen, uten egen kontekst eller lagring. Forslagets konkurransetoken og
fristinnholdets samtidighetspredikater verner samme atomiske operasjon.
Se [EV06-akseptansen](acceptance/2026-10-29-epost-ev06.md).

HTTP-policyer og Blazor-sideautorisasjon er tilgangsgrensen for skrivetjenestene.
Tjenestegrensesnittene i seg selv er ikke en ekstra autorisasjonsgrense.
Ved nye innganger må policyene håndheves og testes der.

## Gjeldende beslutninger

- [Lagdelt løsning og plattform](adr/0001-plattform-og-moduler.md).
- [Godkjenning og synlighet](adr/0002-godkjenning-og-synlighet.md).
- [Entra-roller og tokenfeed](adr/0003-tilgang-og-kalenderdeling.md).
- [Internkalender](adr/0004-internkalender.md).
- [Arbeidsflyt](adr/0005-arbeidsflyt.md).
- [Én DbContext per tjenesteoperasjon](adr/0006-operasjonsavgrenset-dbcontext.md).
- [Konkurransekontroll ved godkjenning](adr/0007-konkurransekontroll-ved-godkjenning.md).
- [Felles validering av gjentaksregler](adr/0008-felles-validering-av-gjentaksregler.md).
- [Separat nettleserakseptanse](adr/0009-nettleserakseptanse.md).
- [Varig daglig e-postoppsummering](adr/0010-varig-daglig-epostoppsummering.md).

Se [gjeldende status](status.md) for leverte endringer og åpne oppgaver.

Detaljmodellen finnes i `SYSTEMARKITEKTUR.md`; kravene i
`kravdokument-aarshjul-frister_v2.md` og `BRUKERHISTORIER.md`.
Faseplanene og `docs/archive/` er historikk. Gjenstående live kildehenting er ikke
implementert bare fordi grensesnittet og en faseplan finnes.
