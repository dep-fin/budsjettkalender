# Gjeldende status

Oppdatert 21. oktober 2026. Denne siden beskriver implementert kode, testbevis og
gjenstående grenser. GitHub-lenkene er kilden for oppgavenes og PR-enes livsløp.
[Gjennomgangen fra 20. oktober](reviews/2026-10-20.md) er et datert utgangspunkt,
ikke en liste over dagens urettede kodefeil.

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

## Samlet akseptanse

[Akseptanserapporten](acceptance/2026-10-20.md) skiller faktisk UI, HTTP/tjenestebevis og
eksterne miljøporter. Med #40 passerer 12 reelle Playwright-/Edge-scenarioer og 478 ordinære tester
passerer lokalt uten feil eller hopp. Egen obligatorisk Chromium-jobb er lagt til i CI;
deploy venter på ordinære tester, SQL Server og nettlesertester. Kandidaten skal ha alle
tre jobber grønne samt Standards-/Spec-review og separat sikkerhetsaudit før merge.

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

SDK 10.0.402: låst restore og Release-bygg av ordinær løsning passerer. Alle **478 ordinære
tester passerer**, 0 feilet og 0 hoppet over. Dette inkluderer 447 fra #36, ni nye
opplastings-/køtilfeller for #46 og 22 nye oppstartstilfeller for #40.
12 nettleserscenarioer passerer med 0 feil/hopp.
Nettleserprosjektets låste restore og Release-bygg passerer også.
Den eksisterende CS0649-advarselen i Runde.razor er uendret.
SQL Server-prosjektets låste restore og Release-bygg passerer. #40 endrer verken EF-modell,
migrasjoner eller SQL-kontrakttilfeller. Lokale Markdown-lenker og git diff --check passerer.

[CI for PR #35](https://github.com/depmartin/Budsjettkalender/actions/runs/39443717020)
bekreftet før merge 442 ordinære og **22 SQL Server-tester**, begge med 0 feil og 0 hopp.
Denne arbeidsstasjonen har ingen brukbar SQL Server; SQL-testene er ikke kjørt lokalt for #36, #46 eller #40.
CI krever ekte SQL Server-tester uten hopp, og deploy venter på den jobben.
Kommandoer og miljøkrav står i [utviklingsveiledningen](development.md).

## Gjenstående grenser

- Azure SQL/Entra, feed-/M365-policy og tokenvern, SQL-brannmur, migrering og Data Protection
  krever eget driftsoppsett og miljøtest. Lokal SQLite-verifikasjon erstatter ikke disse.
- Live kildehenting er fortsatt uferdig.
- Eldre [PR #8](https://github.com/depmartin/Budsjettkalender/pull/8) inngår ikke i dette arbeidet.
