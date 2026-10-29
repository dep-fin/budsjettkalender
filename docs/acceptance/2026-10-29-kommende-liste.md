# Kalender → Liste: kommende frister og historikk

Gjeldende integrasjonsbevis står under [sluttavstemmingen med levert main](#sluttavstemming-med-levert-140-og-ev08-29-oktober-2026).
Sikkerhetsrettingen, den tidligere main-integrasjonen og den opprinnelige rapportdelen
er daterte bevis fra sine oppgitte revisjoner.
Den opprinnelige rapportdelen nedenfor gjelder checkpoint `7a22d3a` på basen
`29421c2` og er beholdt som datert historikk, inkludert rødt/grønt-bevis og bevisgrenser.

Lokal byggekandidat for [#138](https://github.com/depmartin/Budsjettkalender/issues/138),
under [#136 punkt 1](https://github.com/depmartin/Budsjettkalender/issues/136).
Grunnlag: `29421c2ff1a1595ecfaba5e80d18ce529ade8c03`, branch
`codex/138-kommende-liste`. Rapporten attesterer lokal bygging og prøving;
samlet review, separat audit, publisering og faktisk kandidat-CI følger hos orkestratoren.

## Atferd og avgrensning

Liste henter det autoriserte utvalget gjennom `IFristlesing.HentSynligeAsync`, med
historikk og eksplisitte års-/kategoriutvalg, uten månedens datogrenser. Blazor-serveren
velger kommende eller tidligere frister og gjengir høyst 25 rader. Dagsfrister gjelder
hele Oslo-dagen; tentative frister gjelder hele måneden og telles én gang.
Måned, primo, medio og ultimo vises uten en konstruert sikker dagsdato.

Dato/sorteringspunkt, klokkeslett, tittel og stabil ID bestemmer rekkefølgen. Kommende
har stigende dato/tid. Historikk har synkende dato og tid, med heldag først innen samme
dato. Hvert tidsutvalg beholder sin egen side. Kalenderdato og siste måned/uke,
søk og eksplisitte filtre bevares ved visningsbytte; filterendringer starter på side 1.
Minikalenderen viser bevart kalenderkontekst, mens listens overskrift viser tidsutvalget.

Hver sidehandling henter fersk synlighetskontekst og ferske autoriserte data. Endret
ordnet ID-utvalg eller Oslo-dato gir side 1 og en oppdateringsmelding. Det lagres bare
ID-rekkefølge/dato/sidenummer i den eksisterende kretstilstanden, uten databasesnapshot.
Detaljer og kildelenke bruker eksisterende dialog; Lukk/Escape returnerer fokus til raden.
Sidehandlinger flytter fokus til listens overskrift, slik at tastaturbrukeren får ny kontekst.

Ingen ny HTTP-port, migrasjon eller database inngår. Budsjettår → Fristliste,
`/aarshjul`, feed, eksport og godkjennings-/POL-regler er beholdt. Global treffnavigasjon
for søk tilhører etterfølgeren og inngår ikke her.

## Kontroller og bevis

SDK `10.0.402` fra `global.json` brukes gjennom
`C:\Users\marti\Documents\Codex\.tools\dotnet\dotnet.exe` på denne maskinen.
Alle tre låste restore lyktes med aktiv NuGet-audit etter at nettverkstilgangen ble
gitt. Første sandboxforsøk fikk NU1900 og ga ikke nettleserbevis før det ble
avbrutt. De faktiske prøvene nedenfor kjørte utenfor sandboxen.

- Ordinært Release-bygg og hele suiten: **1064/1064 bestått**, ingen hopp.
- Reelle Edge/Kestrel-regresjoner: **38/38 bestått**, ingen hopp. Omfatter
  KalenderlisteTester, KalenderkontekstTester, KalenderperspektivTester,
  KalenderfilterTester, KalenderTester, BudsjettaarTester,
  AtelierSluttakseptanseTester og AtelierTilgjengelighetTester.
- De åtte nye nettlesertilfellene er også kjørt på nytt etter siste presisering av
  testoppsettet: **8/8 bestått**. 53-ID-prøven inkluderer tentative frister ved sidegrensen.
- SQL Server-prosjektet har grønt Release-bygg og **216 oppdagede tilfeller**.
  Nettleserprosjektet har **344 oppdagede tilfeller**, uten duplikate navn.
- Rød → grønn er bevart for kommende liste, egen historikk, endret ID-rekkefølge og
  tastaturfokus på 1440/390. Røde TRX-er ligger i `TestResults/138-red/`;
  mellomliggende grønne prøver ligger i `TestResults/138-green/`.

Hovedkommandoene var `dotnet build backend/Aarshjul.slnx -c Release --no-restore`,
`dotnet test backend/Aarshjul.slnx -c Release --no-build --no-restore` og
`dotnet test backend/Aarshjul.BrowserTests -c Release --no-restore --filter ...`.
Det siste filteret valgte de åtte klassene over; sluttprøven valgte bare
`FullyQualifiedName~KalenderlisteTester|FullyQualifiedName~KalenderkontekstTester`.
`AARSHJUL_DOTNET` pekte på den bundne SDK-en, `AARSHJUL_BROWSER_CHANNEL=msedge`,
og `AARSHJUL_BROWSER_EVIDENCE` pekte på den lokale bevismappen.

Sluttbevis ligger i `TestResults/138-final/`: `ordinary.trx`, `browser-targeted.trx`,
`browser-final-eight.trx`, bygglogger, SQL-/nettleserinventar, auditinnstillinger og
`candidate-files.json`. De syntetiske databasene ligger under bevismappene og inngår
ikke i Git eller CI-artefakter. Nettleserhelperen bruker ny database og tilfeldig
loopback-port per scenario og rydder bare opp i egne prosesser.

Prøvene dekker 53 ID-er nøyaktig én gang, 25/25/3 rader og tilbakeblaing, separate
historikksider, uendrede filtre og kalenderperioder, tomme utvalg, alle fire tentative
former i forrige/inneværende/neste måned, passerte klokkeslett, like titler/tider og
stabile ID-er, Oslo-midnatt med uendret ID-liste, endrede data og fristsynlighet,
ferske autentiseringsgrupper i samme krets, kildelenke, detaljer, tastatur og mobil.
Den eksisterende leseportprøven er utvidet med 53 frister og ikke-sammenhengende år;
skjulte/upubliserte data holdes utenfor returnerte ID-er.

Skjermbilder i `TestResults/138-final/evidence-final/` er visuelt kontrollert enkeltvis:
`liste138-kommende-{1440,390}.png`, `liste138-detaljer-{1440,390}.png`,
`liste138-tentative-{1440,390}.png` og `liste138-historikk-{1440,390}.png`.
Fullstendige titler, usikker månedstekst, sidestatus, kilde og knapper er lesbare,
uten horisontalt overløp. Atelier-farger, typografi og eksisterende kort/dialoger består.

## Bevisgrenser

`AARSHJUL_TEST_SQLSERVER` er ikke satt lokalt. Ekte SQL Server og alle 344 faktiske
Chromium-tilfeller må derfor fortsatt bestå i kandidatens CI. De uendrede jobbgrensene
er 15/20/45 minutter; CI krever eksakte antall uten hopp og laster bare opp TRX.
Lokal SQLite/Edge bekrefter ikke Entra, Azure SQL, M365 eller produksjon. Ingen deploy,
commit, push eller GitHub-mutasjon er utført av byggeren. Senere merget main er ikke
integrert i dette bygget; orkestratoren avstemmer main og aktuelle åpne PR-er før review.

## Main-integrasjon 29. oktober 2026

Nødvendig merget main `28694c580141b91f9707c616a53ee95f05246e11`
(EV06, PR #135 / #116) er integrert med `git merge --no-commit origin/main` i
checkpoint `7a22d3a322da65365631a6ea0f0a6f0b6bc79a6d`. Felles opprinnelig base
er `29421c2ff1a1595ecfaba5e80d18ce529ade8c03`. Ingen umerget PR er flettet inn.
Integrasjonen er lokal og klar til orkestratorens mergecommit; ingen review,
sikkerhetsaudit, commit, push eller GitHub-mutasjon er utført i integrasjonsrunden.

Faktiske konflikter var bare `.github/workflows/ci.yml` og `docs/development.md`.
CI beholder mains 310 SQL-tilfeller og krever 350 Chromium-resultater etter ny
testoppdagelse. Arbeidsflyten er ellers eksakt lik main: 15/20/45 minutter,
samme feil-/hopp-/TRX-kontroller, aktiv audit og bare TRX som artefakter.
Utviklingsveiledningen viser det integrerte inventaret og bevarer EV06-historikken.
Alle 16 andre filer levert av main er eksakt bevart, og alle åtte backendfiler
fra #138-checkpointet er uendret. Ingen ny produktkode eller refaktor inngår.

Main ble eksportert til en disponibel kopi under den lokale bevismappen, gjenopprettet
med låste pakker og bygget med SDK 10.0.402. Testnavn ble så oppdaget fra de faktiske
kompilerte testprosjektene på både main og integrert kandidat. Sammenlikning av hele
navn, inkludert teoriargumenter, gir:

| Inventar | Merget main | Opprinnelig #138 | Integrert kandidat | Nye mot main |
| --- | ---: | ---: | ---: | ---: |
| Ordinær | 1157 | 1064 | 1158 | 1 |
| SQL Server | 310 | 216 | 310 | 0 |
| Nettleser | 342 | 344 | 350 | 8 |

Alle navn fra begge grunnlag er bevart eksakt, uten dubletter. Den nye ordinære
prøven er `KalenderutvalgTester.Ubegrenset_listeutvalg_bevarer_53_frister_eksplisitte_aarsunioner_og_tentativ_presisjon_uten_skjulte_tellinger`.
Nettlesertillegget er de sju tilfellene i `KalenderlisteTester` (tastaturprøven har
1440 og 390 som separate tilfeller) og én fersk-autentiseringsprøve i
`KalenderkontekstTester`. Fullstendige navn og sammenlikninger ligger i
`inventory-comparison.json`, `main-*-names.txt`, `candidate-*-names.txt` og
`added-*-names.txt` under `TestResults/138-main-integration/`.

Alle tre låste restore og Release-bygg består med aktiv NuGet-audit (`true`,
`all`, `low`), null advarsler og null feil. Deretter er følgende prøvd på samme bygg:

| Integrasjonsprøve | Resultat | Lokalt bevis |
| --- | --- | --- |
| Hele ordinærsuiten | 1158/1158, ingen feil/hopp | `ordinary.trx` |
| Reelle Edge/Kestrel-forløp | 55/55, ingen feil/hopp; 6 min 28 s | `browser-targeted.trx`, `browser-filter.txt` |
| SQL Server-bygg og oppdagelse | 310 tilfeller, ingen duplikate navn | `candidate-build-sql.log`, `candidate-inventory-sql.log` |
| Nettleserbygg og oppdagelse | 350 tilfeller, ingen duplikate navn | `candidate-build-browser.log`, `candidate-inventory-browser.log` |

De 55 nettlesertilfellene omfatter hele `DagligEpostTester` (17, inklusive EV06s
seks nye forløp) og de samme 38 kalender-/Atelier-regresjonene som før integrasjonen:
Kalenderliste, Kalenderkontekst, Kalenderperspektiv, Kalenderfilter, Kalender,
Budsjettaar, AtelierSluttakseptanse og AtelierTilgjengelighet. Filteret matcher også
ett eksisterende `InternkalenderTester`-tilfelle. Alle åtte nye #138-tilfeller er med.
Utførte TRX-navn er avstemt eksakt mot de 1158 og 55 forventede navnene.
`verify.ps1` i bevismappen gjengir kommandoene; browser kjørte med `--no-build
--no-restore`, `AARSHJUL_BROWSER_CHANNEL=msedge`, den bundne `AARSHJUL_DOTNET`
og ny lokal bevismappe. Ingen forventning eller timeout er endret.

Alle åtte nye bilder `liste138-{kommende,detaljer,tentative,historikk}-{1440,390}.png`
under integrasjonens `evidence/` er visuelt kontrollert enkeltvis. Titler,
«Dato ikke fastsatt», sidestatus, kildelenke, dialog og fokusmarkering er lesbare,
uten horisontalt overløp. Tidligere røde/grønne/finale bevis er beholdt separat:
SHA-256-kontroll av alle 271 filer under `138-red`, `138-green` og `138-final`
viser null drift. Også 439 backendkildefiler og 1495 runtimefiler har null drift
mellom før og etter integrasjonsprøvene. Resultatene står i `test-summary.json`,
`merge-preservation.json` og `hash-after-summary.json`. Nytt `candidate-files.json`
binder hele kildekandidaten til begge foreldre, Git-tre og filenes SHA-256;
det opprinnelige sluttmanifestet er uendret.

Ekte SQL Server er fortsatt ikke kjørt lokalt (`AARSHJUL_TEST_SQLSERVER` er ikke
satt), og full 350-tilfellers Chromium-kjøring gjenstår i kandidat-CI. Alle tre
faktiske CI-porter og orkestratorens samlede review/separate audit kreves fortsatt.
Lokal Edge/SQLite med kontrollerte testidentiteter er ikke bevis for Entra,
Azure SQL, M365 eller produksjon. Ingen utrulling inngår.

## Sikkerhetsretting av detaljåpning 29. oktober 2026

Den ferdige auditen reproduserte samme eksisterende forhold på både kandidat
`e6f69d6025f903164c09b33928ab2bffd89af58a` (tre
`987a69b8078dfed806da4eff8e6fc0f9b1da1b37`) og reviewbase
`28694c580141b91f9707c616a53ee95f05246e11`: FA lastet listen, fristen ble
endret til POL, og detaljåpning brukte den gamle DTO-en og introduserte et notat
som ikke tidligere var i DOM. Etter ny sidehandling var raden/API-et korrekt
filtrert, men lukket dialog beholdt tittel og notat. Auditens `result.json` og
`baseline-result.json` ligger uendret i
`TestResults/138-security-audit/probe-f86adef0-c02e-4abf-ba5b-55a8ae47e409/`.
Oppdatert #138 i `TestResults/138-review-context/issue138-amended.md` autoriserer
den avgrensede rettingen før levering av listen.

Felles `Fristdialog` henter nå aktuell autentisering og synlighetskontekst og
gjenleser gjennom `IFristlesing.HentEnAsync` ved hver direkte detaljåpning og
åpning fra dagsagenda. Den tidligere DTO-en brukes bare til ID. Manglende,
slettet eller utilgjengelig frist gir «Frist utilgjengelig» og en nøytral beskjed,
uten tidligere tittel, notat eller kilde i dialogen. Lukk/Escape tømmer beholdte
frist-/agendadata. Generasjoner for lesing og dialogåpning hindrer at sene svar
erstatter nyere innhold eller åpner en lukket dialog. Native fokusretur og
eksisterende side-/kalenderkontekst beholdes. Gruppeinnsyn sender valgt gruppe
til dialogen, slik at gjenlesingen bevarer gruppefilteret også for administrator.
Eksisterende kontroll av aktiv gruppe og rolle blir stående i innsynssiden.

Rød → grønn er bevart ved den eksisterende nettlesergrensen, med ekte
Kestrel/Blazor, Edge og disponibel SQLite. `FristdetaljTester` har 16 tilfeller:

- Åtte prøver endrer FA → POL, brukergrupper, sletting eller autentisert status
  mellom ferdig listeinnlasting og detaljåpning, både i Kalender → Liste og
  Budsjettår → Fristliste. De krever nøytral dialog og kontrollerer hele DOM for
  notat- og kildemarkører som ikke var sendt til nettleseren før åpningen.
- Fire prøver lukker en tillatt detalj med Lukk eller Escape på begge flater.
  De kontrollerer lukket dialogs tittel, hele DOM, fokusretur og uendret kontekst,
  og bekrefter nytt notat ved neste åpning.
- Fire prøver holder et faktisk lesesvar tilbake mens dialogen lukkes eller
  åpnes på nytt med endret innhold. Kvitteringen for den opprinnelige
  Blazor-hendelsen brukes som sluttbarriere før DOM kontrolleres.

`red-access.trx` har seks faktiske feil før gjenlesing: den gamle detaljen med
notat/kilde ble vist i stedet for utilgjengelig-beskjeden. `red-close.trx` viser
beholdt tittel ved tre av fire prøver; den fjerde stoppet før åpning fordi den
nye testhelperen ikke ventet på ferdig liste. `red-identity.trx` har to faktiske
feil før eksplisitt kontroll av autentisert identitet. Mellomresultatene bevares,
og sluttprøven nedenfor erstatter dem som kandidatbevis. Testhelperen venter nå
på ferdig listeflate; dialogselektoren avgrenser appens dialog fra rammeverkets
reconnect-dialog. Ingen forventning eller timeout er svekket. Første sandbox-
forsøk ble avbrutt før testutførelse da testhost ikke kunne koble til; de faktiske
nettleserprøvene kjører utenfor sandboxen.

Låst restore og Release-bygg av ordinær-, SQL- og nettleserprosjektet består med
null advarsler/feil. NuGet-audit er fortsatt `true` / `all` / `low`. Ny faktisk
oppdagelse gir **1158 ordinære, 310 SQL Server- og 366 nettlesertilfeller**.
Alle tidligere **1158/310/350 navn** er bevart eksakt uten duplikater; de 16
tilleggene er hele `FristdetaljTester`. CI endrer bare sitt eksakte nettleserantall
til 366 og beholder 15/20/45 minutter, alle feil-/hopp-/TRX-kontroller og bare TRX
som artefakter. Skriptene og inventaret ligger i `TestResults/138-detail-fix/`.

Sluttbygget er prøvd uten ny kompilering mellom kontrollene:

| Kontroll | Resultat | Bevis i `138-detail-fix/` |
| --- | --- | --- |
| Hele ordinærsuiten, kjørt én gang etter siste kodeendring | **1158/1158 bestått**, ingen feil/hopp | `ordinary.trx` |
| Relevante faktiske Edge/Kestrel-forløp | **103/103 bestått**, ingen feil/hopp; 9 min 11 s | `browser-targeted.trx`, `browser-filter.txt` |
| Bygg og oppdagelse | **1158/310/366**, alle tidligere navn bevart | `final-build-*.log`, `inventory-comparison.json` |

De 103 nettlesertilfellene omfatter alle 16 nye detaljprøver og alle de 55 tidligere
integrasjonsregresjonene, samt kalenderforslag, ordinære forslag, kilde, tilgang og
gruppeinnsyn. Måned/Uke, dagsagenda og Budsjettår, side-/filter-/kalenderkontekst,
rollehandlinger, godkjenning, kilder, dialogenes tastatur/fokus og mobil er med.
TRX-navn avstemmes eksakt mot oppdaget inventar og filter. Testhelperne bruker
bare egne disponible databaser og verter. Bare den første sandbox-kjøringens
identifiserte egne testprosesser ble stoppet; andre prosesser ble ikke berørt.

SHA-256-kontroll før/etter sluttprøven har null drift i **440 backendkildefiler**,
**1495 runtimefiler** og **2827 tidligere bevisfiler**, inklusive auditens kandidat-
og baseprobe. Åtte nye listebilder er bevart. Detaljbildene på 390 og 1440 piksler
er visuelt lest: full tittel, notat, kilde og eksisterende administratorhandling
er lesbare. `hash-after-summary.json` og `test-summary.json` binder kontrollene.
Nytt `candidate-files.json` inneholder fysisk SHA-256/størrelse, Git-filtrert
blobhash for hver kandidatfil, bevisfiler og et samlet filfingeravtrykk. Det
oppgitte Git-treet er uttrykkelig den uendrede headens tre før rettingen; ingen
ny committet kandidat eller nytt publisert tre hevdes.

Denne rettingen er lokale, ucommittede endringer etter head `e6f69d6`.
Det tidligere reviewet og auditen attesterer ikke den endrede kandidaten.
Orkestratoren må feste kandidaten og bestille ny relevant Standards-/Spec-
gjennomgang og separat sikkerhetsaudit, deretter full faktisk kandidat-CI med
310 SQL Server- og 366 Chromium-tilfeller. Lokal kontrollert autentisering og
SQLite/Edge beviser ikke Entra-tilbakekalling, Azure SQL, M365 eller produksjon.
Ingen review, audit, commit, push, GitHub-mutasjon eller deploy er utført i
rettingsrunden. Andre checkouter, databaser og prosesser er bevart.

## Avstemming med levert EV07-main 29. oktober 2026

Gjennomgått og auditert #138-head
`e81cd1a514543d97e3ba4522ab3b2a73bfba7a5f`, tre
`d731ac9aa7071c462e63948644a14beebff31775`, er avstemt med faktisk levert main
`d3bec41e7a42106e6f2b8bee80de0916c3f65b60` gjennom
`git merge --no-commit origin/main`. Felles base er
`28694c580141b91f9707c616a53ee95f05246e11`. Denne hovedgrenen inneholder levert
EV07 / #117 / PR #143 og PDF-undersøkelsen #137 / PR #141. Hele gjeldende #138,
#136 og EV07-kravet med kommentarer er lest. Ingen umerget #140- eller annen
PR-kandidat inngår. Saken #139 er fortsatt en egen etterfølger.

Tre faktiske konflikter er løst:

- `Kalender.razor`: EV07s `OnParametersSetAsync`, generelle fjerningsmelding,
  validerte lenkeparametre og bevaring av lenkens budsjettår beholdes sammen med
  #138s `OnAfterRenderAsync`, lastedel, sidenavigasjon, historikk og filterhandlinger.
  Den leverte parameterhandleren og den gjennomgåtte lastedelegasjonen står begge
  igjen. Et deterministisk kontrollskript setter de fire eksakte EV07-tilleggene
  inn i e81-kilden og krever identisk resultat med den integrerte filen.
- `.github/workflows/ci.yml`: mains 379 SQL-tilfeller beholdes. Nettleserantallet
  settes til de faktisk oppdagede 368 i tre linjer. Utover dette er CI eksakt lik
  levert main, med 15/20/45 minutter, aktiv audit, samme feil-/hopp-/TRX-kontroller
  og bare TRX som artefakter.
- `docs/development.md`: begge foreldres tall får tydelig historisk grense, mens
  gjeldende inventar er 1229/379/368. EV07s migrasjons-, demo-, import- og
  slettingsveiledning beholdes. Denne akseptansen får et nytt datert bevisavsnitt.

Av 47 endrede main-filer og 15 endrede #138-filer siden felles base er unionen
59 filer. **44 main-filer og 11 øvrige #138-filer har eksakt foreldreblob**.
De fire avvikene er de tre konfliktfilene og denne oppdaterte akseptansen.
Alle 15 PDF-forskningsfiler, EV07-akseptansen, ADR, arkitektur og status er
bevart eksakt fra main. Av #138s 12 backendfiler er **11 identiske med e81**;
av mains 25 backendfiler er **24 identiske med main**. Bare `Kalender.razor`
kombinerer produktkode fra begge. Alle **18 berørte test-/kontraktfiler** har
eksakte foreldreblobber, også SQL-oppgraderingsrettingen fra EV07s slutt-CI.
`merge-preservation.json` lister hver fil, opprinnelse og begge blobhashene.

Felles `Fristdialog.razor` beholder blob
`542d6000880813f27d92a72606c7a21efbbe07ce`, `kalenderdialog.js` beholder
`b638906ba5883477d3e07ea9902a88bc53629280`, og `FristdetaljTester.cs` beholder
`fecd23694980511d7d2995886638f94a4eac8f15`. Fersk detaljlesing, nøytral
utilgjengelig-beskjed, DOM-tømming, vern mot sene svar/JS-åpninger og valgt gruppe
i Innsyn er dermed uendret fra den gjennomgåtte rettingen. Det samme gjelder
listens 25-raders utvalg, Oslo-dag, tentative måneder, side-/filterkontekst og fokus.

Alle tre prosjektene er gjenopprettet med låste pakker, bygget i Release og
faktisk oppdaget på **begge foreldre og integrert kandidat** med SDK 10.0.402.
Main er prøvd i en disponibel arkivkopi under den nye bevismappen. Audit er
`true` / `all` / `low`; alle bygg har null advarsler og feil.

| Oppdaget inventar | Gjennomgått #138 | Levert EV07-main | Integrert kandidat |
| --- | ---: | ---: | ---: |
| Ordinær | 1158 | 1228 | **1229** |
| SQL Server | 310 | 379 | **379** |
| Nettleser | 366 | 344 | **368** |

Hele navn med teoriargumenter er sammenliknet. **Null tap, null dubletter** fra
begge foreldre; de historiske e81-navnene samsvarer også med fersk oppdagelse.
Mot main er det én leseportprøve og 24 nettlesertilfeller fra #138. Mot e81 er
det 71 ordinære, 69 SQL- og to EV07-nettlesertilfeller. Navnelister og SHA-256
ligger i `TestResults/138-ev07-integration/`, sammen med bygglogger,
`inventory-comparison.json`, `resolutions.json` og kildebevaringskontrollen.

Sluttbygget er prøvd uten ny kompilering eller kildeendring mellom kontrollene:

| Lokal kontroll | Faktisk resultat | Bevis i `138-ev07-integration/` |
| --- | --- | --- |
| Hele ordinærsuiten, én kjøring | **1229/1229**, null feil/hopp; 25,716 sekunder | `ordinary.trx` |
| Relevante reelle Edge/Kestrel-forløp | **105/105**, null feil/hopp; 649,330 sekunder | `browser-targeted.trx`, `browser-filter.txt` |
| Fersk oppdagelse og navnebevaring | **1229/379/368**, null tap/dubletter fra begge foreldre | `inventory-comparison.json` |
| Kilde-/runtime-/eldre bevisbevaring på dette kontrolltidspunktet | **444/1495/3284 filer**, null SHA-256-/størrelsesdrift før senere auditavvik beskrevet nedenfor | `hash-after-summary.json` |

TRX-navn er avstemt eksakt med filter og oppdaget inventar. Alle de tidligere
103 målrettede tilfellene fra detaljrettingen er med, inklusive de 16 detaljprøvene;
EV07 legger til to reelle importforløp. Måned/Uke/Liste, Budsjettår, dagsagenda,
53-ID-sidenavigasjon, historikk, Oslo-midnatt, tentative perioder, filtre,
kildefelt, rollehandlinger, fersk tilgang, nøytral utilgjengelighet, full DOM-tømming,
sene lesesvar, fokus, tastatur, mobil og gruppeinnsyn er prøvd. EV07-forløpene
dekker ny import, identisk gjenimport, avbrudd, faktisk rollback, endring og
sletting gjennom UI til egen oppsummering. Slettingslenken beholder relevant
år/kategori/måned med vanlig tilgang og avviser anonym visning av kalenderen.
Hver test bruker egen disponibel app/database/loopback-port eller den eksisterende
kontrollerte testverten; ingen annen arbeidsflates prosess eller database er brukt.

Åtte nye listebilder på 390/1440 og to EV07-slettingsbilder på 390/1280 er visuelt
lest. Hele titler, sidestatus, historisk sortering, tentative månedstekster,
detalj/kilde, fokus og den generelle fjerningsmeldingen er lesbare uten observert
horisontalt overløp. SHA-256, stier og observasjoner står i
`inspected-list-images.json` og `inspected-ev07-images.json`.

`candidate-files.json` binder alle kandidatfiler til fysisk SHA-256/størrelse,
staged Git-blob og filmodus, foreldrenes hele filunion, lokale testresultater,
bevisfiler og et samlet filfingeravtrykk. `StagedGitTree` er fra `git write-tree`
etter løste og staged konflikter. Det er et kandidat-tre, ikke en ny commit eller
et publisert resultat. Headens gamle tre er oppgitt separat. Alle tidligere
bevis ble kontrollert mot de daværende råfilhashene. Den senere auditen endret
fysiske bytes i én gammel disponibel database og fjernet to sidecarfiler; se den
eksplisitte avviksjournalen nedenfor. Gamle manifest og bevis beholdes som historikk
med denne begrensningen, og attesterer sine egne revisjoner.

Det fullførte samlede Standards-reviewet (0 regelbrudd/0 skjønnsfunn),
Spec-reviewet (0 funn) og den separate sikkerhetsauditen (0 funn) gjelder
e81-kandidaten med hele detaljrettingen, slik
[#138s daterte kommentar](https://github.com/depmartin/Budsjettkalender/issues/138#issuecomment-6494644700)
bekrefter. Avsnittets eldre utsagn om at ny gjennomgang gjenstår etter e6f-rettingen
er historikk. Denne integrasjonen utfører ingen ny review eller audit. Produktets
samlede blobsett er endret ved baseimport, og kalenderfilen er en kontrollert
kombinasjon; det hevdes derfor ikke at det nye treet er identisk med e81.
Ingen ny funksjonell beslutning, tilgangsmodell eller endring av detaljgrensen er
innført ved konfliktløsningen. Tidligere attestasjoner forblir revisjonsbundne.

EV07s [slutt-CI 40857856203](https://github.com/depmartin/Budsjettkalender/actions/runs/40857856203)
med 1228/379/344 gjelder den leverte forelderen. Integrert #138 krever sin egen
faktiske kandidat-CI med **379 ekte SQL Server- og 368 Chromium-tilfeller**.
`AARSHJUL_TEST_SQLSERVER` er ikke satt lokalt; SQL er bare bygget/oppdaget her.
Lokal SQLite/Edge med kontrollerte identiteter beviser ikke Entra-tilbakekalling,
Azure SQL, M365-levering eller produksjon. Ingen commit, push, GitHub-mutasjon
eller utrulling inngår. Andre arbeidsflater og deres prosesser/databaser er bevart.

## Sluttavstemming med levert #140 og EV08 29. oktober 2026

Den rene, gjennomgåtte #138-forelderen er
`823d1530d3b2d278663a2b06c5b3147e8de5a416`, tre
`f4d676b53b260aedeb1e96d42e1dd5b13948c90c`. Faktisk levert main er
`12d12601b0fe8d83d0a4a84b88cff589bd954572`, med både #140 / PR #142 og
EV08 / PR #144. Felles base er `d3bec41e7a42106e6f2b8bee80de0916c3f65b60`.
`git merge --no-commit origin/main` importerte bare denne leverte hovedgrenen.
PR #142s mergecommit og alle tre vellykkede jobber i
[slutt-CI 40875485618](https://github.com/depmartin/Budsjettkalender/actions/runs/40875485618)
er kontrollert live; forelderens 1302/442/350 er dens eget bevis, ikke ny #138-CI.

Bare `.github/workflows/ci.yml` og `docs/development.md` hadde konflikt.
Arbeidsflyten tas fra levert main med kun tre antallslinjer endret fra 350 til
de faktisk oppdagede **374** nettlesertilfellene. **442 SQL-tilfeller**, 15/20/45
minutter, aktiv audit, Passed-/executed-/notExecuted-kontroll, manglende-TRX-vern
og bare TRX-artefakter er bevart. Utviklingsdokumentasjonen kombinerer gjeldende
#138-tall med mains bevarte URL-, migrasjons-, import-/slettings- og
adressebyttekontrakter. Status viser #140 som faktisk levert. Denne akseptansen
journalfører den nye avstemmingen og avgrenser tidligere bevis.

**Ingen egen produktkode eller testkode er endret.** Alle sju produktblobber fra
823 (`Kalender`, `Kalenderliste`, `Kalendertilstand`, CSS, `Innsyn`, `Fristdialog`
og JavaScript) og alle fem egne testblobber er bevart eksakt. Alle **40** endrede
backendfiler fra main har eksakt main-blob, inklusive validering, URL-ferskhet,
import/sletting, begge migrasjoner/målmodeller/snapshot og EV08s adressebytte.
Alle **35** migrasjonsfiler er identiske med main. Alle **24** berørte
test-/kontraktfiler har eksakte foreldreblobber. Kandidatens
**544** filer er den fulle unionen av foreldrenes 538/540 filer, uten tap eller
uventet blob-/modusendring. `merge-preservation.json` oppgir hver fil og begge
foreldreblobber. Fire konfigurasjons-/dokumentfiler har eksplisitt lov til ny blob.

Hele egen differanse på 823 hadde fersk Standards-review (0 regelbrudd og
0 skjønnsfunn), Spec-review (0 funn) og separat sikkerhetsaudit (0 funn), som
[#138s journal](https://github.com/depmartin/Budsjettkalender/issues/138#issuecomment-6496007175)
bekrefter. Den auditen kjørte selv 34 nettlesertilfeller og 14 negative
kontrollgrupper. Egen liste-/detaljkode, autorisasjonsgrense, fokus- og
DOM-tømmingsvern er nå uendret fra denne faste kandidaten. Main importerer
allerede levert funksjonalitet; ingen ny rettighet eller produktbeslutning er
innført i konfliktløsningen. Disse review-/auditforutsetningene videreføres med
de kombinerte regresjonene nedenfor. Attestasjonene forblir revisjonsbundne:
**ingen ny review eller sikkerhetsaudit av det nye staged treet hevdes**.

### Eksplisitt avvik i tre gamle bevisfiler

Under den separate auditen etter EV07-integrasjonen ble en eldre disponibel
SQLite-database åpnet uten `mode=ro` ved skjemaoppslag. Ved lukking sjekkpunktet
SQLite eksisterende WAL. Ingen SQL-skrivekommando ble utført, men databasefilens
fysiske bytes endret seg, og `-wal`/`-shm` forsvant. Det opprinnelige manifestet
beholdes. Tidligere utsagn om at samtlige historiske databasebytes var bevart,
gjelder derfor ikke etter dette tidspunktet.

Alle tre stier er under
`TestResults/138-security-audit-fixed/probe-6b4034f7-27b4-4b1b-89a5-10fc33161b34/`:

| Fil | Opprinnelig SHA-256 / byteantall | Nåværende råfilstatus |
| --- | --- | --- |
| `probe.db` | `2E96884C16791C7D4BDE90A6DB2C9DA33C2A2AAB3E52088ADC148C368552A423` / 4096 | `642F597DED2BF929BF73E29396DD9270816947B9D2CE57B4F7E15E31475EC379` / 360448 |
| `probe.db-shm` | `11F41BE6F22C1E6CC2813344169A95D24ABCAC23E4DE0D26435D70F83F53E6DA` / 32768 | Mangler; ingen konstruert etterhash |
| `probe.db-wal` | `E896765CC1BE468A923DCA801DD0C6E36A8A9366BE8898B47C39ABF83B18B4E1` / 1495592 | Mangler; ingen konstruert etterhash |

`TestResults/138-ev07-security-audit/verification.json` og den lenkede
oppgavekommentaren journalførte hendelsen først. En fersk råfilkontroll mot
`138-ev07-integration/old-evidence-before.json` bekrefter **nøyaktig disse tre
unntakene og 3281 øvrige uendrede historiske filer**.
`138-final-main-integration/historical-evidence-incident.json` binder hver
før-/etterhash, størrelse og manglende sidecar. Gamle manifest er ikke omskrevet,
gamle hash er ikke rekonstruert, og ingen gammel auditdatabase er åpnet med
SQLite i denne runden. Auditens registrerte kilde-/runtimebevaring og ferske
34 nettleser-/14 negative kontroller gjelder fortsatt sine oppgitte revisjoner.

### Oppdagelse og lokale sluttprøver

Alle tre prosjektene er gjenopprettet i låst modus og bygget i Release med
SDK **10.0.402** på begge foreldre og integrert kandidat. NuGet-audit er
`true` / `all` / `low`; byggene har null advarsler og feil. Main er prøvd i en
disponibel arkivkopi i den nye bevismappen. Testoppdagelse i sandbox brukte lang
tid uten resultater; bare denne agentens egne to oppdagelsesprosesser ble
avbrutt. Én rakk å fullføre ordinær oppdagelse ved avbruddet. Loggene er bevart.
Ny oppdagelse med autorisert lokal prosesskommunikasjon uten sandbox fullførte.
Ingen faktisk testkjøring eller appfeil hevdes fra de avbrutte forsøkene.

| Hele oppdagede navn, inklusive teoriargumenter | Gjennomgått #138 | Levert main | Integrert kandidat |
| --- | ---: | ---: | ---: |
| Ordinær | 1229 | 1302 | **1303** |
| SQL Server | 379 | 442 | **442** |
| Nettleser | 368 | 350 | **374** |

Alle navn er bevart tegnsensitivt fra begge foreldre, uten dubletter. De ferske
823-navnene er også identiske med EV07-integrasjonens historiske navnelister.
`inventory-comparison.json` og de ni hele navnelistene binder kontrollen.

Det ferdige Release-bygget er prøvd uten ny kompilering mellom kontrollene:

| Lokal kontroll | Faktisk resultat | Bevis i `TestResults/138-final-main-integration/` |
| --- | --- | --- |
| Hele ordinærsuiten, én faktisk kjøring | **1303/1303**, null feil/hopp; TRX 21,985 / prosess 23,025 sekunder | `ordinary.trx`, `test-summary.json` |
| Kombinerte reelle Edge/Kestrel-forløp, én kjøring | **111/111**, null feil/hopp; TRX 662,476 / prosess 663,159 sekunder | `browser-targeted.trx`, `browser-filter.txt` |
| Alle tidligere relevante forløp | **105/105** navn bevart og bestått, ingen dubletter | `targeted-comparison.json` |
| Nye relevante main-forløp | **6/6**: to manuelle kildelenke- og fire adressebyttetilfeller | samme navnesammenlikning og TRX |
| SQL Server lokalt | **442** bygget/oppdaget; ingen faktisk kjøring | `sql-availability.json`, inventar-/bygglogger |

TRX-resultatnavn og oppdaget inventar er sammenliknet eksakt, inklusive
teoriargumenter; alle resultater er Passed, executed er lik inventaret, og
notExecuted er null. De tidligere 105 omfatter liste-/periode-/historikk-/Oslo-/
tentativ-/filter-/fokus-/detalj-/kilde-/tilgangsregresjonene, alle 16 ferske
detaljgrenser og EV07s import-/slettingslenker. De seks nye tilfellene prøver
oppretting, endring/fjerning av manuell kilde, dokumentfallback og bevaring
gjennom begge godkjenningsinnganger på 390/1440. EV08 prøver validering,
egenadressesamtykke, varselvalg, stanset varsling, gammel lenke/fane, avmelding,
ny påmelding, to omstarter og faktisk kontrollert dagskjøring uten etterslep
på 390/1280. Den ordinære suiten dekker dessuten de leverte tjeneste-/HTTP-/
import-/migrasjons-/konkurransekontraktene. Alle testdatabaser er nye disponible
filer eller testvertenes egne minnedatabaser.

Tre nye bilder er visuelt lest: kommende side 2 av 2 på 1440, detalj/kildelenke
på 390 og EV07s generelle fjerningsmelding med bevart utvalg på 390. Innhold,
sidestatus, fokus og handlinger er lesbare uten observert horisontalt overløp.
`inspected-images.json` binder råfilhash og observasjon; øvrige bilder er
bevisfiler uten påstand om individuell visuell gjennomgang.

Faktisk ny kandidat-CI med **1303 ordinære, 442 ekte SQL Server- og 374 Chromium-
tilfeller** gjenstår. `AARSHJUL_TEST_SQLSERVER` er ikke satt; SQL er ikke erstattet
med SQLite eller rapportert som lokalt bestått. Lokal SQLite/Edge med kontrollerte
identiteter beviser ikke Entra-tilbakekalling, Azure SQL, M365-levering eller
produksjon. Ingen commit, push, GitHub-mutasjon, ny review/audit eller deploy er
utført i denne runden. Andre arbeidsflater, prosesser og databaser er urørt.

Råfilkontrollen etter prøvene bekrefter **448 backendkildefiler og 1495
runtimefiler uten drift**, og **5989 eldre bevisfiler uten ytterligere endring
fra denne rundens starttilstand**. Dette siste tallet omfatter den allerede
endrede databasefilen og betyr ikke at dens opprinnelige bytes er bevart;
de tre historiske unntakene ovenfor består. `hash-after-summary.json` og
før-/etterlistene binder den presise kontrollgrensen.

Det nye `candidate-files.json` binder alle 544 filer til fysisk SHA-256 og
størrelse, eksakt staged Git-blob/filmodus, begge foreldre, testresultater,
bevisjournal og bevisfiler. `StagedGitTree` kommer fra `git write-tree` etter
løste og staged konflikter, og er ikke en commit. De gamle manifestene og
deres opprinnelige hashverdier beholdes. Ny kandidat-CI og publisering er
fortsatt orkestratorens porter.

## Avgrenset CI-retting etter kjøring 40885826810, 29. oktober 2026

[PR #145s første kandidat-CI](https://github.com/depmartin/Budsjettkalender/actions/runs/40885826810)
kjørte publisert head `70e6c21afb46ab66c655273294f51815cda74fe7`, med samme tre
`a3f351e2f69a5ac57d1b55ea838a6432f5105efc` som lokal head
`a501fbd87d4077d4d7317fafceb9f4ef6f19560e`. Ordinær- og SQL-portene bestod
1303/1303 og 442/442. Chromium utførte alle 374 tilfeller: **373 bestått, én
feil, null hopp**. Testresultatet oppgir 18 minutter og 38 sekunder. Den ene feilen
var `KalenderlisteTester.Historikk_har_egen_side_seneste_foerst_og_bevarer_filtre_detaljer_fokus_og_kalenderkontekst`:
første «Neste side» forble deaktivert, og eksisterende 10000 ms klikkgrense utløp.
Dette var ikke en grønn samlet kandidat-CI.

### Rødt bevis og avklart årsak

Den foregående Astra-diagnosen er bevart i `TestResults/138-ci-40885826810/`.
`browser-job.log` linje 427–457 viser CI-feilen. Fem faktiske lokale kjøringer av
den uendrede metoden ga fire beståtte og én feil; den røde
`diagnose-repeat/run-4/repeat-4.trx` viste **47 i stedet for 53 kommende frister**.
Et avbrutt sandboxforsøk uten testresultat regnes ikke som en sjette kjøring.

Testen kalte `CheckAsync` rett etter «Fjern alle», før serverens tømte årsutvalg
var rendret. Playwright kunne fortsatt lese `checked=true` fra den gamle DOM-en
og fullføre uten klikk eller ny valghendelse. Den senere tømmingen etterlot da
ingen år eller bare 2028. Null eller 47 frister var korrekt for dette utilsiktede
utvalget. Dette er en feil i testoppsettets synkronisering.

Seks kontrollerte prober ved samme klokke skiller årsaken fra listeberegningen:
to prober som holdt tømmingsrenderen til etter begge sjekkene ga null frister og
deaktivert «Neste side»; to som holdt den til etter første sjekk ga 47 frister.
To prober som ventet på serverteksten «Budsjettår: Ingen valgt» ga begge år,
53 frister og korrekt side 2 av 3. `diagnose-conclusion.json` og
`diagnose-probe-results.json` binder observasjonene og prøvegrensen. Disse
diagnosebevisene er historikk fra før rettingen, ikke nye prøver av rettet kode.

### Minste retting og nye grønne prøver

Bare oppsettet i den ene metoden er endret, med tre nye synlige forventninger:

- Etter «Fjern alle» venter testen på «Budsjettår: Ingen valgt» før årsvalgene.
- Etter årsvalgene bekrefter den «Budsjettår: 2026, 2028».
- Etter bytte til Liste bekrefter den «Side 1 av 3 · 53 frister» før første blaing.

Alle opprinnelige handlinger, forventninger, data, navn, teoriargumenter og
prøvegrenser er bevart. Ingen produktkode, felles testhjelper, CI-port,
antallskontroll eller timeout er endret; ingen søvn eller nytt testtilfelle er lagt
til. `TestResults/138-ci-40885826810-fix/final/preservation.json` kontrollerer at hele
testfilen er originalen med nøyaktig disse tre innsettingene.

Låst browser-restore og ferske Release-bygg av Web og BrowserTests med SDK
**10.0.402** bestod med null advarsler/feil og NuGet-audit `true` / `all` / `low`.
Samme ferdige runtime ble deretter brukt uten ny kompilering mellom prøvene:

| Ny lokal kontroll | Resultat | Bevis i `TestResults/138-ci-40885826810-fix/final/` |
| --- | --- | --- |
| Eksakt historikkmetode, ti separate kjøringer | **10/10**, null feil/hopp; ny evidensmappe per kjøring | `repeat/run-1/` til `repeat/run-10/`, `test-summary.json` |
| Alle `KalenderlisteTester`, `KalenderfilterTester` og `KalenderperspektivTester`, én samlet kjøring | **15/15**: 7 liste-, 3 filter- og 5 perspektivtilfeller; null feil/hopp | `targeted/targeted.trx`, `targeted-names.txt` |
| Hele browserinventaret, inklusive teoriargumenter | **374**, alle navn eksakt bevart, null dubletter | `browser-names.txt`, `inventory-summary.json` |

TRX-resultatene er sammenliknet med de hele forventede navnene; executed er lik
forventet antall, alle resultater er Passed og notExecuted er null. Alle nye
prøver bruker installert Edge, ekte Kestrel og nye disponible SQLite-databaser.
Bare prøveoppsettet er endret, så ordinær- og SQL-suitene er ikke kjørt på nytt
lokalt i denne runden. De tidligere resultatene beholder sine revisjonsgrenser.

Gamle manifest, røde/grønne prøver, diagnosefiler og det eksplisitte avviket i de
tre gamle SQLite-bevisfilene ovenfor er beholdt. Ingen gammel auditdatabase er
åpnet med SQLite. Råfilkontrollen binder denne rundens starttilstand; den
tilbakefører ikke eller rekonstruerer de tre opprinnelige filenes bytes.
`final/hash-summary.json` bekrefter **448 backendkildefiler og 823 Web-/browser-
runtimefiler uten drift gjennom sluttprøvene**, samt **8755 eldre bevisfiler uten
ytterligere endring fra denne rundens starttilstand**. De tre historiske
SQLite-unntakene består; tallet er ingen attest av deres opprinnelige bytes.

Rettede lokale prøver erstatter ikke ny full kandidat-CI med **1303 ordinære,
442 ekte SQL Server- og 374 Chromium-tilfeller** uten feil/hopp. Ingen ny review,
sikkerhetsaudit, commit, push, GitHub-mutasjon eller deploy er utført i denne
avgrensede CI-løkken. Andre arbeidsflater og deres prosesser/databaser er urørt.
