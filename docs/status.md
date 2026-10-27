# Gjeldende status

Oppdatert 27. oktober 2026. Denne siden beskriver implementert kode, testbevis og
gjenstående grenser. GitHub-lenkene er kilden for oppgavenes og PR-enes livsløp.
[Gjennomgangen fra 20. oktober](reviews/2026-10-20.md) er et datert utgangspunkt,
ikke en liste over dagens urettede kodefeil.

Atelier UX01 ([#76](https://github.com/depmartin/Budsjettkalender/issues/76)) er levert i
[PR #96](https://github.com/depmartin/Budsjettkalender/pull/96), merget som
`c1f250ea4e6ddbf05075059e0d1fc47f582d805b`, med felles skall, rollebasert hovedmeny, seks virksomme
administrasjonsområder og Budsjettår → Fristliste. `/aarshjul` og tidligere direkte
innganger består. [Akseptanse og prøvekommandoer](acceptance/2026-10-27-atelier-ux01.md)
beskriver rød/grønn, mobil/tastatur, 629 ordinære tester og 26 nettleserscenarioer.
Atelier UX02 ([#77](https://github.com/depmartin/Budsjettkalender/issues/77)) er levert i
[PR #97](https://github.com/depmartin/Budsjettkalender/pull/97), merget som
`a65ae35c1edd74c2780f347948f7e5962e11f20e`, med månedskalender, minikalender, dagsagenda og fullstendige fristdetaljer.
Hele valgt måned vises, mens Fristlistens historikkvalg bevares separat. Tentative
månedsfrister ligger utenfor dagsrutene. [Akseptanse](acceptance/2026-10-27-atelier-ux02.md)
beskriver kalender-, kilde-, synlighets-, rolle- og tastaturprøvene.
Atelier UX03 ([#78](https://github.com/depmartin/Budsjettkalender/issues/78)) har en lokal
kandidat med eksplisitte års-/kategorivalg, søk i tittel/notat, nullstilling og synlig
periode-/filterkontekst. Kalenderen beholder sin egen tilstand i kretsen; Fristlistens
historikk og leseportens null/tom=alle består. [Akseptanse](acceptance/2026-10-27-atelier-ux03.md)
beskriver union, snitt, nulltreff, autoriserte årsvalg og mobilprøvene. #79 og senere
Atelier-tickets er ikke implementert her; SQL Server/Chromium kontrolleres i CI.

| Oppgave | Implementert atferd | Leveranse |
| --- | --- | --- |
| [#21](https://github.com/depmartin/Budsjettkalender/issues/21) | Pocock-arbeidsflyt, kort agentindeks, dokumenterte standarder og rettinger fra gjennomgangen | [PR #25](https://github.com/depmartin/Budsjettkalender/pull/25) |
| [#22](https://github.com/depmartin/Budsjettkalender/issues/22) | Én DbContext per offentlig tjenesteoperasjon | [PR #31](https://github.com/depmartin/Budsjettkalender/pull/31) |
| [#23](https://github.com/depmartin/Budsjettkalender/issues/23) | Konkurransekontroll ved behandling av samme forslag, også etter revidert innsending | [PR #32](https://github.com/depmartin/Budsjettkalender/pull/32) |
| [#24](https://github.com/depmartin/Budsjettkalender/issues/24) | Felles parameterkontroll for manuell mallagring og JSON-import | [PR #33](https://github.com/depmartin/Budsjettkalender/pull/33) |
| [#34](https://github.com/depmartin/Budsjettkalender/issues/34) | Kontrollert datooverløp per regel, trygg årsregning og femte ukedag ved øvre datogrense | Livsløp i oppgaven |
| [#36](https://github.com/depmartin/Budsjettkalender/issues/36) | 12 utførte nettleserscenarioer, demo-utlogging, manuelle ankre og ansvar i internkalender rettet | [Akseptanserapport og eksterne porter](acceptance/2026-10-20.md) |
| [#46](https://github.com/depmartin/Budsjettkalender/issues/46) | Deterministiske usikkerhetsflagg med forklaring ved robotforslagenes uttrekksbevis, også for eldre forslag | Livsløp i oppgaven |
| [#40](https://github.com/depmartin/Budsjettkalender/issues/40) | Key Vault-hemmeligheter lastes før Entra/database, med dokumentert kildeprioritet og trygge oppstartsfeil | Livsløp i oppgaven |
| [#44](https://github.com/depmartin/Budsjettkalender/issues/44) | Administratorflate for oppretting, navneendring og deaktivering av synlighetsgrupper med stabil kode og bevarte referanser | Lokal kandidat; supplerende bevis i [akseptanserapporten](acceptance/2026-10-20.md#supplerende-akseptanse-for-44) |
| [#69](https://github.com/depmartin/Budsjettkalender/issues/69) | Valgfri, servervalidert HTTPS-kildelenke fra fristens tilknyttede dokument til kalenderens fristkort | [PR #70](https://github.com/depmartin/Budsjettkalender/pull/70); supplerende bevis i [akseptanserapporten](acceptance/2026-10-20.md#supplerende-akseptanse-for-69) |
| [#71](https://github.com/depmartin/Budsjettkalender/issues/71) | Avledet «venter endring»-merke på publiserte fristkort bare for administrator, undertrykt ved gruppelesing | Lokal kandidat; supplerende bevis i [akseptanserapporten](acceptance/2026-10-20.md#supplerende-akseptanse-for-71) |
| [#73](https://github.com/depmartin/Budsjettkalender/issues/73) | Kilde eller innsender i administratorens kø, kombinert med øvrige filtre og bevart gjennom behandling | Lokal kandidat; supplerende bevis i [akseptanserapporten](acceptance/2026-10-20.md#supplerende-akseptanse-for-73) |
| [#76](https://github.com/depmartin/Budsjettkalender/issues/76) | Atelier-skall, rollebasert navigasjon og bevarte innganger | Merget i [PR #96](https://github.com/depmartin/Budsjettkalender/pull/96), `c1f250e` |
| [#77](https://github.com/depmartin/Budsjettkalender/issues/77) | Månedskalender, minikalender, dagsagenda og fristdetaljer med gyldig kilde og bevart tilgang | Merget i [PR #97](https://github.com/depmartin/Budsjettkalender/pull/97), `a65ae35`; [akseptanse](acceptance/2026-10-27-atelier-ux02.md) |
| [#78](https://github.com/depmartin/Budsjettkalender/issues/78) | Eksplisitt flervalg av budsjettår/kategorier, søk, nullstilling og bevart kalenderkontekst | Lokal kandidat; [akseptanse](acceptance/2026-10-27-atelier-ux03.md) |

## Samlet akseptanse

[Akseptanserapporten](acceptance/2026-10-20.md) skiller faktisk UI, HTTP/tjenestebevis og
eksterne miljøporter. Med #78 passerer 36 reelle Playwright-/Edge-scenarioer og 633 ordinære tester
passerer lokalt uten feil eller hopp. Egen obligatorisk Chromium-jobb er lagt til i CI;
deploy venter på ordinære tester, SQL Server og nettlesertester. Kandidaten skal ha alle
tre jobber grønne samt Standards-/Spec-review og separat sikkerhetsaudit før merge.

## Kildelenke på synlige fristkort

[#69](https://github.com/depmartin/Budsjettkalender/issues/69) projiserer bare Url fra
dokumentraden som er knyttet til den synlige, godkjente fristen. IFristlesing returnerer
KildeUrl for liste, landing og enkeltoppslag etter kontroll av absolutt HTTPS, eksakt
regjeringen.no/www.regjeringen.no, standardport 443 og fravær av brukerinfo. Ugyldig
URI-syntaks avvises også. Tillatte adresser får Uri sin kanoniske form; lagrede adresser
endres ikke. Manglende kobling/rad, tom eller avvist URL gir null og beholder fristen.
Ingen adresse utledes fra dokument-ID, kilde, filnavn eller opphav.

Kalenderens Fristkort viser «Les kildedokument» med vanlig HTML-koding, no-referrer,
noopener og noreferrer ved åpning i ny fane. Alle autoriserte funksjonsroller får samme
kildelenke for samme frist. Synlighet filtreres fortsatt i SQL før dokumentprojeksjon;
POL velges fortsatt eksplisitt. Datamodell, migrasjoner, eksport, innhenting og årshjulets
separate visning er uendret.

40 SQLite-tilfeller ved IFristlesing og 15 HTTP-tilfeller kontrollerer tillatte og avviste
adresser, alle tre leseutvalg, manglende dokument, roller, anonym tilgang og fravær av
skjulte markører i hele svaret. Ett nytt reelt nettleserscenario prøver lenken for leser,
bidragsyter og administrator, kildefrie kort og skjult DOM samt POL-isolasjon. Bare
syntetiske dokumentmetadata brukes; ingen ekstern navigasjon eller kildehenting utføres.
Se akseptanserapporten for konkrete røde/grønne bevis og lokal testgrense.

P12a er levert i PR #70. #50 og #38 forblir åpne; planleggingsgren og draft-PR #65 er urørt.
Ingen deploy er utført.

## Ventemerke på publiserte fristkort

[#71](https://github.com/depmartin/Budsjettkalender/issues/71) utleder VenterEndring ved
lesing: minst ett forslag med status Forslag, type Endring og EndrerFristId lik fristens
ID. Liste, landing og enkeltoppslag deler projeksjonen etter synlighetsfilteret.
Bare SerAlt-konteksten får indikatoren. Gruppelesing og administrators «se som gruppe»
får alltid false; forslagets innsender, ID, tittel og foreslåtte verdier returneres ikke.
Fristkort viser «venter endring» under eksisterende administratorpolicy. VisGrupper gir
ingen rett til ventemerket. Ingen ny handling, lagret status eller migrasjon er innført.

17 SQLite-tilfeller kontrollerer null/ett/to åpne forslag, øvrige statuser/typer,
gruppelesing og faktisk godkjenning, justering og avvisning via IGodkjenningsko i begge
rekkefølger. Ny lesing bevarer merket etter første behandling og fjerner det etter siste;
fristens identitet og FA/POL-synlighet bevares. 19 HTTP-tilfeller kontrollerer roller,
anonym, POL, enkeltoppslag og hele responsens fravær av skjulte markører/forslagmetadata.
Ett nytt nettleserscenario prøver administrator, leser og bidragsyter, innsyn med
VisGrupper, POL-isolasjon og ny innlasting etter godkjenning/avvisning.
Se akseptanserapporten for røde/grønne bevis og lokal testgrense.

P12b er en lokal kandidat. Samlet Standards-/Spec-review, separat sikkerhetsaudit og alle
tre kandidatjobber i CI gjenstår før mergeavklaring. #50 og #38 forblir åpne; planleggingsgren
og draft-PR #65 er urørt. Ingen deploy er utført.

## Kildevalg i godkjenningskøen

[#73](https://github.com/depmartin/Budsjettkalender/issues/73) gir administrator valget
«Kilde eller innsender». «Alle» og distinkte ikke-blanke råverdier hentes fra hele den
åpne, ikke-genererte køen. Valget sendes som eksisterende Kofilter.Kilde, som sammenlikner
Forslag.KildeEllerInnsender eksakt. Køetikett/innsender, dokumentleverandør, dokument-ID og
opphav er fortsatt ulike dimensjoner; tjenestekontrakten og adapteren er uendret.

Kilde og de fire eksisterende filtrene danner et snitt. Kildelisten beholder de andre
kildene også ved tomt utvalg. Aktivt valg beholdes ved godkjenning, avvisning og retur fra
justering, også når siste treff forsvinner. Justeringsreturen oppbevarer filteret én gang
i samme Blazor-krets; vanlig navigasjon forkaster det. Ingen varig filterlagring er innført.
POL forhåndsvelges fortsatt ikke, og endringsforslag bevarer fristens synlighet.

17 SQLite-tilfeller, fem faktiske HTTP-tilfeller på GET /admin/ko og fire nye
nettleserscenarioer prøver kilde-/snittsemantikk, tomt utvalg, tilgang, trygg tekst og
behandling. De første 16 tjenestetilfellene passerte allerede før UI-endringen.
Nettleseren var rød ved manglende kildevalg og deretter ved tapt valg etter justering;
begge flytene er grønne. Se akseptanserapporten for resultatfiler og nøyaktig bevisgrense.

P12c er en lokal kandidat. Samlet Standards-/Spec-review, separat sikkerhetsaudit og alle
tre kandidatjobber i CI gjenstår før mergeavklaring. #50 og #38 forblir åpne;
planleggingsgren og draft-PR #65 er urørt. Ingen deploy er utført.

## Forvaltning av synlighetsgrupper

[#44](https://github.com/depmartin/Budsjettkalender/issues/44) gir administrator en egen
flate på `/admin/grupper` og lenken «Grupper». Egendefinerte grupper opprettes aktive.
Kode trimmes, lagres med store bokstaver og har 1–64 ASCII-bokstaver, sifre eller bindestrek,
med bokstav først. Visningsnavn trimmes og har 1–256 tegn. Duplikater avvises også for
inaktive grupper. Navn og aktiv-status kan endres for eksisterende grupper; kode, ID og
innebygd identitet endres aldri, og det finnes ingen sletteoperasjon.

IGruppetjeneste bruker én kontekst per operasjon. Nye grupper inngår i de eksisterende
synlighetsvalgene, og deaktivering fjerner dem fra nye valg og forhåndsvalg ved godkjenning,
justering, generering og ny innsending av reviderte brukerforslag. Fristers tidligere koder
og serverfiltrerte leseutvalg beholdes.
Innhold kan redigeres med eksisterende inaktive tilordninger; skjemaet viser dem med status
og mulighet for å fjerne dem. Nye inaktive tilordninger og gjeninnføring etter fjerning avvises.
Eksisterende Entra-medlemskap beholdes ved deaktivering så lenge claimet fortsatt gjelder;
bortfalt claim fjerner medlemskapet som før. Nye Entra-medlemskap krever aktiv gruppe.
Det eksplisitte POL-valget og databasefilteret er uendret. Ny medlemskapsadministrasjon er
fortsatt [#45](https://github.com/depmartin/Budsjettkalender/issues/45).
Datamodell og migrasjoner er uendret.

26 tjenestetilfeller og åtte HTTP-tilfeller prøver lagret utvalg, gyldige/ugyldige koder,
navn, deaktivering, reaktivering, innebygd identitet og tilgang. Den første nye tjenesteprøven
feilet fordi oppretting manglet; neste prøve feilet ved manglende oppdatering. Negative
prøver fanget lagring av ugyldige verdier og rå databasefeil ved duplikater. HTTP-prøven
viste først 404 for den manglende siden, og viser nå 401/403/200 for anonym/ikke-admin/admin.
Direkte POST uten interaktivt skjema avvises med 400 uten endring; den er ikke et skrive-API.
Nettleserprøven feilet først ved det manglende opprettingsskjemaet og passerer etter
implementering. Se den supplerende akseptansematrisen for bevis og avgrensning.

To tjenesteregresjoner fra review feilet først fordi samme Entra-principal mistet SKOK
etter deaktivering og innholdsredigering av en SKOK-frist ble avvist. Etter retting passerer
ekte brukeroppslag, gjenoppslag, SQLite-persistens og serverfiltrert lesing. Negative prøver
bekrefter at nye inaktive tildelinger avvises og bortfalt claim fortsatt fjerner medlemskap.
Nettleseren viste først skjult inaktivt valg ved fristredigering og avvist godkjenning i kø,
justering, generering og ny innsending, selv etter aktivt FA-valg. Alle fem flyter passerer etter retting.
Sluttbevis: TestResults/44-review-final2-ordinary.trx og
BrowserTestResults/44-review-final2-browser.trx. Akseptanserapporten angir de enkelte
røde/grønne resultatfilene og den målrettede kontraktkjøringen.

P06 er implementert som lokal kandidat. Manuell pilotakseptanse og de eksterne portene
P13–P15 under [#38](https://github.com/depmartin/Budsjettkalender/issues/38) står fortsatt åpne.
Ingen produksjonsgodkjenning eller deploy er utført.

## Usikkerhetsflagg for robotforslag

[#46](https://github.com/depmartin/Budsjettkalender/issues/46) vurderer lagrede uttrekksbevis
med Usikkerhetsregler når godkjenningskøen leses. Forklaringene vises ved tolket verdi og
kildeutdrag: relativ formulering tolket til konkret dato, kildeutdrag uten gjenkjennelig
dato og dato utenfor det eksisterende vinduet fra budsjettår −2 til +1, inklusive hele år.
Lav konfidens alene gir ingen flagg. Firesifrede år utenfor 1900–2099 kontrolleres også.
Avledning ved lesing dekker både nye opplastinger og eldre forslag uten migrasjon eller
tilbakefylling. Kandidatutvalg, forkasting, publisering og aktivt POL-valg er uendret.

Første regresjon gjennom IDokumentopplasting → IGodkjenningsko feilet fordi relativ kilde
med konfidens 0,99 manglet flagg; samme test er grønn etter retting. En ny vindusregresjon
fanget at 2100 ikke ble flagget. Det utvidede PDF-scenarioet feilet først med 0 av 3 synlige
flagg og viser nå alle tre forklaringene. Nettleseren bekrefter også at kildeutdrag med
HTML-tegn vises som tekst, gyldig forslag er uten flagg og ingen frist publiseres.
De 12 eksisterende nettleserscenarioene er beholdt; ingen ny scenarioantallsgrense er innført.

Lokal #46-evidens: TestResults/46-01-red.trx, 46-01-green.trx, 46-02-red.trx,
46-02-green.trx, 46-03-browser-red.trx, 46-03-browser-green.trx,
46-final-ordinary.trx og 46-final-browser.trx. App-logger, syntetiske demo-databaser og
skjermbilde er bare lagret lokalt under temp/budsjettkalender-46-browser. Dette er
SQLite-/Edge-bevis for kandidaten, ikke SQL Server-, Entra-, Azure- eller M365-bevis.

Rapporten har konkrete operatørhandlinger for Entra/Azure SQL, Outlook/M365 og infrastruktur.
Key Vault-provider er implementert i #40; faktisk Azure-tilgang krever fortsatt miljøtest.
Vedvarende Data Protection-oppsett og live-kildeworker er fortsatt implementerings-/oppsettsgap
som demoinnloggingen ikke dekker.

## Hemmeligheter ved oppstart

[#40](https://github.com/depmartin/Budsjettkalender/issues/40) bruker den offisielle
Key Vault-provideren med eksplisitt systemtilordnet managed identity. Hemmeligheter
lastes før oppstarten leser demo-, database- eller Entra-konfigurasjon. Key Vault har
høyest prioritet, og standard `--` → `:`-mapping bevares. Manglende URI gjør ingen
Azure-kall. Ugyldig eksplisitt URI eller lastingsfeil stanser oppstarten med en fast
norsk feil uten rå URI, leverandørunntak eller indre unntak. HTTP-pipelinelogging og
Activity-sporing er deaktivert på disse klientene. Hele `Azure.Identity`-kategorien
undertrykkes ved appens `ILoggerFactory`, også ved leverandørspesifikke loggregler
og konfigurasjonsreload; øvrig applogging beholdes. Rå SDK-EventSource/ETW/EventPipe,
direkte konsollyttere og eksterne diagnoseagenter ligger utenfor appens logggrense
og kan fortsatt se følsomme feilresponser. Se [driftskontrakten](../infra/README.md#key-vault-ved-weboppstart).

22 nye oppstartstilfeller bruker WebApplicationFactory og samme komposisjonsrot som
Main. Ekte SecretClient/provider kjører mot styrt HTTP-transport med syntetisk
legitimasjon, inklusive SDK-ens autentiseringsutfordring. Testene viser databasens
oppstartslesing, Entras opsjonsbinding, prioritet over JSON/miljø/kommandolinje,
navnemapping, nettverksfri Demo/Testing og trygge URI-/liste-/hente-/nettverks-/identitetsfeil.
Produksjonsvarianten av Entra-bindingen erstatter bare den eksterne databasen med SQLite.
Ingen ekstra tjenesteprovider eller produksjonsflagg for å hoppe over lastingen er lagt til.

Sikkerhetsregresjonen bruker ekte systemtilordnet ManagedIdentityCredential med
syntetisk App Service-endepunkt og styrt HTTP-transport. Før retting nådde responsens
følsomme markør apploggen via LogMsalError/GetTokenFailed, selv med SDK-ens tre
diagnostikkflagg av. Etter retting når disse rå hendelsene fortsatt den privilegerte
testlytteren, mens videresending til appens faktiske loggerfabrikk undertrykkes.
Testen prøver også leverandørspesifikk Trace, reload og bevaring av annen logging/filtrering.
Den bygger en separat loggvert fordi mislykket konfigurasjonslasting skjer før
webvertens loggerfabrikk kan bygges. Ingen ekstra produksjonsprovider opprettes.

Første røde regresjon feilet ved oppstart fordi lokal ugyldig databasetilkobling ble lest
i stedet for hvelvets verdi. URI-regresjonene feilet fordi ugyldig eksplisitt URI ikke ble
avvist. Feilregresjonene viste rå Azure-/nettverks-/aggregerte unntak; samme tilfeller gir nå
trygge oppstartsfeil, og syntetiske følsomme markører mangler i logger og feiltekst.
Lokal evidens: TestResults/40-01-red.trx, 40-01-green.trx, 40-02-red.trx,
40-02-green.trx, 40-03-red.trx, 40-03-green.trx, 40-04-kontrakter.trx,
40-05-identitet.trx, 40-06-identitetslogging-red.trx,
40-06-identitetslogging-green.trx, 40-07-identitetslogging-kontrakter.trx,
40-security-final-ordinary.trx og 40-security-final-browser.trx.
Faktisk managed identity, RBAC, Key Vault/Azure-nettverk og Entra-innlogging er fortsatt
miljøporter i P13–P15 under #38. Ingen Azure- eller produksjonsutrulling er utført.

## DbContext og behandling av forslag

Alle 15 runtime-adaptere bruker IDbContextFactory og await using per offentlig operasjon.
Deterministiske regresjonstester ved IFristlesing og IFristskriving viste først EF-feilen
om en andre operasjon på samme kontekst. Etter retting fullfører neste kall på samme
tjeneste mens første SELECT venter. Hvert kall har egen kontekst og SQLite-tilkobling.
Samlede lagringer og rollback beholdes; se [ADR-0006](adr/0006-operasjonsavgrenset-dbcontext.md).

Forslag har et lagringsinternt Guid-versjonstoken som AppDbContext fornyer ved alle
sporede innsettinger og endringer, på både SQLite og SQL Server. Bare én behandling
av lest versjon kan fullføre. Gammel godkjenning, justering eller avvisning gir
Valideringsfeil, også når raden er avvist, revidert og sendt inn på nytt.
Forslag, frist, synlighet og varsel lagres samlet. Bare konkurransefeil med oppføringer
utelukkende for Forslag oversettes; andre databasefeil beholdes.
Se [ADR-0007](adr/0007-konkurransekontroll-ved-godkjenning.md).

De 21 delte kontrakttilfellene ved IGodkjenningsko dekker konkurrerende behandlinger,
POL, endringsforslags synlighet, revidert innsending, slettet målfrist og rollback ved
lagringsfeil. SQL Server-suiten har i tillegg en oppgraderingstest fra produksjonsskjemaet.
EF-generert migrasjon, snapshot og målmodell ble verifisert i #23.

## Regelvalidering og import

Regelparser i Application er den felles kontrollen for regeltype, påkrevde JSON-felter,
ukedag og gyldig måned/dag. IMaltjeneste gir Valideringsfeil før en ugyldig oppretting
eller oppdatering endrer malen. IMalDatautveksling bruker samme kontroll, gir én advarsel
per ugyldig regel og erstatter hele malen med de gyldige reglene. Ingen gyldige regler
betyr derfor en tom mal. Filfeil i versjon, null-liste/-rad eller duplisert ID avviser
fortsatt hele importen uten endring. Alle kandidater kontrolleres før sletting og sporing;
én SaveChangesAsync lagrer erstatningen atomisk. Beslutningen står i
[ADR-0008](adr/0008-felles-validering-av-gjentaksregler.md).

53 ugyldige parameterkombinasjoner prøves både ved manuell oppretting/oppdatering og
import. De omfatter manglende felter, ugyldig JSON, JSON-null, feil felttyper, nullukedag,
umulig måned/dag og ukjent regeltype. Testene bekrefter uendret mal etter manuell avvisning,
advarsel per ugyldig importregel og at gyldige regler overlever blandet import.
De første røde testene viste at FastDato {} ble lagret og at import godtok ukjent enumverdi.

Gyldige standardregler, fra_dag uten uke, fra_dag foran uke, femte ukedags fallback til
siste forekomst, skuddår, årsforskyvning −1/0/+1 og relative ankerkjeder er bevart.
Uke uten fra_dag kreves nå i området 1–5; dette strammer inn tidligere implisitt klamping.
29. februar er en gyldig maldato; beregning for et ikke-skuddår gir kontrollert feil.
Manuell lagring krever tittel. Import støtter eldre filer uten tittel og normaliserer
null/tom tittel til tom tekst. En rød test viste tidligere NullReferenceException ved null.
En AFTER DELETE-trigger viser rollback av import, og neste import på samme tjeneste lykkes.
Eldre lagrede regler med JSON-feil rapporteres som genereringsfeil uten å stanse gyldige regler.

## Datooverløp ved generering

[Oppgave #34](https://github.com/depmartin/Budsjettkalender/issues/34) håndteres ved
Genereringsberegning.Beregn og IGenereringstjeneste. Relativ dagforskyvning utenfor
0001-01-01–9999-12-31 gir norsk feil per regel og ingen dato. FastDato og RelativUkedag
summerer målår og årsforskyvning som long og avviser kalenderår utenfor 1–9999 kontrollert.
Femte ukedag klampes før datokonstruksjon, også i desember 9999: mandag blir 27. desember,
mens femte fredag fortsatt blir 31. desember. Se feilkontrakten i
[ADR-0008](adr/0008-felles-validering-av-gjentaksregler.md).

Feil i ankeret forplanter seg gjennom hele kjeden uavhengig av reglenes rekkefølge.
Manuelle ankre overstyrer fortsatt beregningen. Gyldige uavhengige regler blir forslag;
feilede regler og deres avhengige regler blir det ikke. Den gyldige delmengden erstatter
årets tidligere åpne genererte forslag i én atomisk lagring. Regenerering gir ingen
dubletter. En helt mislykket beregning gir en tom erstatning, som før; en lagringsfeil
ruller tilbake hele erstatningen. Et nytt kall på samme tjeneste kan lykkes når feilen er fjernet.

Regresjonene viste først ukontrollert AddDays-unntak, feil kalenderdato etter
heltallsoverløp og avvist femte mandag i desember 9999. Testene dekker int.MinValue/MaxValue,
én dag utenfor området, eksakte yttergrenser, store gyldige positive og negative
forskyvninger, null, rekursive ankerkjeder og SQLite-lagring med rollback.
Manuell oppretting, oppdatering og import beholder hele int-området for forskyvningene;
gyldige store forskyvninger beregnes også etter import. Ingen vilkårlig parametergrense
innføres. Datooperasjoner for feed, lister og internoppgaver er utenfor denne rettingen.

## Verifikasjon

SDK 10.0.402: låst restore og Release-bygg av ordinær løsning passerer. Alle **633 ordinære
tester passerer**, 0 feilet og 0 hoppet over. Dette inkluderer 447 fra #36, ni nye
opplastings-/køtilfeller for #46, 22 nye oppstartstilfeller for #40, 34 gruppe-/HTTP-/regresjonstilfeller for #44
og 55 kilde-/HTTP-tilfeller for #69, 36 lese-/HTTP-tilfeller for #71, 22 kø-/HTTP-tilfeller for #73
og fire nye HTTP-rolleprøver for #76 samt fire lesekontrakttilfeller for #78.
36 nettleserscenarioer passerer med 0 feil/hopp, inklusive tre Atelier-navigasjonsscenarioer
fra #76, syv kalender-/detaljscenarioer fra #77 og tre filter-/søke-/mobilscenarioer fra #78. Eksisterende kalender-, kilde-,
ventemerke- og HTTP-prøver er tilpasset den tilsiktede overgangen fra kort til
interaktive detaljer; tilgangs-, publiserings- og metadatahemmelighold er bevart.
Nettleserprosjektets låste restore og Release-bygg passerer også.
Den eksisterende CS0649-advarselen i Runde.razor er uendret.
SQL Server-prosjektets låste restore og Release-bygg passerer. #78 endrer verken EF-modell,
migrasjoner eller SQL-kontrakttilfeller. Lokale Markdown-lenker og git diff --check passerer.
Tidligere lokal NuGet-kjøring ga NU1900 fordi sårbarhetsmetadata ikke kunne hentes;
#69s, #71s, #73s, #77s og #78s låste sluttkjøringer ga ingen NU1900. Ingen auditkontroll er deaktivert.

[CI for PR #35](https://github.com/depmartin/Budsjettkalender/actions/runs/39443717020)
bekreftet før merge 442 ordinære og **22 SQL Server-tester**, begge med 0 feil og 0 hopp.
SQL-testene er ikke kjørt lokalt for #78; de krever en konfigurert SQL Server.
CI krever ekte SQL Server-tester uten hopp, og deploy venter på den jobben.
Kommandoer og miljøkrav står i [utviklingsveiledningen](development.md).

## Gjenstående grenser

- Azure SQL/Entra, feed-/M365-policy og tokenvern, SQL-brannmur, migrering og Data Protection
  krever eget driftsoppsett og miljøtest. Lokal SQLite-verifikasjon erstatter ikke disse.
- Live kildehenting er fortsatt uferdig.
- Eldre [PR #8](https://github.com/depmartin/Budsjettkalender/pull/8) inngår ikke i dette arbeidet.
