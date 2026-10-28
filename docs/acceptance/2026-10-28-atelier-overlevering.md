# Atelier – overlevering ved UX18/#93

Datert 28. oktober 2026. [UX18-rapporten](2026-10-28-atelier-ux18.md) er
kravkartet og bevisregisteret. [Gjeldende status](../status.md) er dagens livsløp.
Historiske kjøringer er datert i de enkelte akseptanserapportene.

## Publisert kandidat og gjeldende livsløp

UX01–UX17 / #76–#92 er merget. UX17/PR #131 er `959060c898048da5ebd26bfb2cd5cdb68a1c2271`,
med grønn [CI 40717209832](https://github.com/depmartin/Budsjettkalender/actions/runs/40717209832).
Main før første UX18-publisering var `93b1aea3f0953535e3144d22b36991e88ead7852`, EV03/PR #130,
merget 28. oktober 2026 kl. 17.33.10 +01:00 av en annen chat. Den er nødvendig
integrert i UX18 før review, med EV02/EV03-kode/modell/adaptervern og alle tester bevart.
En annen chat merget senere EV04/PR #133 som faktisk main
`926ffe74e62999714f6e711327636ecce2070317`, 28. oktober 2026 kl. 20.24.08 +01:00.
Den er nødvendig integrert etter UX18s mergekonflikt, med alle EV04-backendblobber,
kontrakter, migrasjoner og modell bevart eksakt. UX18s kilder og begge avgrensede
CI-kvitteringer er også bevart. PR #128 er ikke integrert; ingen ny EV-ticket er bestilt.
UX18 er publisert i [PR #132](https://github.com/depmartin/Budsjettkalender/pull/132).
[Publiseringsregisteret](2026-10-28-atelier-ux18.md#publisering-og-gjeldende-livsløp)
oppgir det faste, gjennomgåtte kildegrunnlaget, identisk første publiserte tree
og de tre fullførte gjennomgangene med 0 funn. Lokal arbeidsgren er
`codex/93-atelier-sluttakseptanse`, i `work/Budsjettkalender-93` relativt til den
opprinnelige checkouten. Den upubliserte lokale kandidatstatusen er historikk fra
28. oktober 2026 før PR #132.
`TestResults/93-ci-main-integration/` samler det nye lokale integrasjonsbeviset og
følger ikke en ny klon. `TestResults/93-main-integration/` (962/328) og tidligere
907/325-bevis i `TestResults/93-final/` er bevart som historikk.
PR #132 og kommentarene i [#75](https://github.com/depmartin/Budsjettkalender/issues/75)
og [#93](https://github.com/depmartin/Budsjettkalender/issues/93) er autoriteten for
siste publiserte head, faktisk CI, mergecommit og verifisert main. De lokale
962/328-resultatene og 130 oppdagede SQL-prøvene beholder sitt faste revisjonsgrunnlag.
PR #132 er fortsatt **ikke merget**. Dens publiserte head før EV04-integrasjonen,
`6acf357efdb7498281b3bdf970e3cc48e543de03`, bestod
[CI 40752611386](https://github.com/depmartin/Budsjettkalender/actions/runs/40752611386)
med 962 ordinære, 130 ekte SQL Server- og 328 Chromium-tilfeller uten feil/hopp.
Den nye integrasjonen krever publisering, faktisk kandidat-CI og bekreftet UX18-merge.
Arbeidet stopper etter #93. Ingen deploy eller oppstart av neste ticket inngår.

Andre uferdige checkouter, draft-PR #65, prototype-/plangrenene og eksisterende
demoprosesser/databaser er beholdt. Port 5076 (UX01), 5077 (UX02) og 5088
(separat dokumentprototype) må ikke gjenbrukes eller stoppes av denne demoen.

## Oppstart fra main etter bekreftet merge

Kontroller at PR #132 viser merget og sammenhold mergecommit og verifisert main
med #93. Bruk deretter en ny klon av [main](https://github.com/depmartin/Budsjettkalender/tree/main),
eller oppdater en ren main-checkout med `git pull --ff-only`. En ny klon fra en
foreldremappe med ledig mappenavn kan opprettes slik:

```powershell
git clone --branch main --single-branch https://github.com/depmartin/Budsjettkalender.git Budsjettkalender-atelier
Set-Location Budsjettkalender-atelier
git rev-parse HEAD
```

Sammenhold HEAD med det bekreftede main-grunnlaget før demoen startes. Les den
[gjeldende overleveringen på main](https://github.com/depmartin/Budsjettkalender/blob/main/docs/acceptance/2026-10-28-atelier-overlevering.md)
ved senere videreføring.

## Lokal demo med ny database

Fra repo-roten på Windows med SDK 10.0.402 fra `global.json` i PATH:

```powershell
pwsh -NoProfile -File ./scripts/Start-AtelierDemo.ps1
```

Helperen finner repoet fra sin egen plassering og kontrollerer eksakt SDK-versjon.
`AARSHJUL_DOTNET` kan peke til SDK-kjørbar fil hvis den ikke finnes i PATH; ingen
personspesifikk SDK-sti er bygget inn i skriptet. Låst restore beholder aktiv audit.
Release-publisering kopierer binærer og statiske filer til en ny GUID-mappe under
`TestResults/93-demo/`. Hver start bruker en ny disponibel SQLite-fil og bare
loopback, normalt `http://127.0.0.1:5093/demo`. Opptatt port gir feil og tar ikke
over en annen prosess. Skriptet oppgir prosess-ID, stoppkommando og bevismappe.
Det endrer ikke tidligere SQLite-filer eller låser repoets byggutdata.
`-Port 5094` kan velge en annen ledig lokal port. Miljøet er Demo og fraværende
Key Vault-URI fjernes i barneprosessen, fremfor å settes til tom URI.
Den kopierte Development-konfigurasjonen brukes som lokal Demo-konfigurasjon,
slik at også demo-SBR beholder samme simulerte gruppeclaim som vanlig demoprofil.
Dette endrer ingen produksjonspolicy eller kildefil. EV03s offentlige basisadresse
settes til helperens faktiske loopback-port. Normal demo bruker den automatiske
dagsjobben med bare kontrollert post i minnet. Den uendrede helperen ble faktisk
smokeprøvd på EV03-grunnlaget på 5094 med HTTP 200, ny database og stans av sin egen
prosess. EV04s normale `Demoskjema`-oppstart bevares eksakt og prøves med nye disponible
SQLite-filer i full Kestrel-suite; det er ingen ny helper-smoke i denne CI-runden.
5093 er reservert roots senere sluttvisning etter bekreftet merge.

Velg demopersona for leser, FA/FIN-FAG-bidragsyter eller administrator.
Samme innlogging gir e-postpåmelding til kontrollert lokal mottaker; ingen ekstern
e-post sendes. Administrator ser de seks områdene. Start fra en dato i kalenderen
som bidragsyter og bytt til administrator for godkjenning. Leseren får bare
serverautoriserte frister. Dette er lokal visning, ingen produksjonsgodkjenning.
EnsureCreated oppgraderer ikke en eldre database; helperen oppretter derfor en ny fil.

## Lokal kontroll etter EV04-mainintegrasjon

Hele ordinærporten passerer **1024/1024**, og alle faktiske Edge/Kestrel-scenarioer
passerer **332/332**, uten feil/hopp. **186 SQL Server-tilfeller** er bygget/oppdaget.
Alle 962/328/130 UX18-navn og 1018/326/186 EV04-navn er bevart eksakt uten dubletter.
Tre låste restore og Release-bygg med `-m:1 -nr:false` og aktiv audit passerer uten
advarsler/feil. 430 backend-kildefiler og 1518 runtimefiler har null drift;
kandidatmanifestet har 505 filer. Browser-TRX/prosess måler 1778,580/1779,356 sekunder.
Fullverifikasjonen ligger i `TestResults/93-ci-main-integration/`, med kilde-/runtime-/
kandidatmanifest, faktisk TRX og bevarte testnavn. SQL Server er bare bygget/oppdaget
lokalt; faktisk SQL Server og Chromium kreves i ny kandidat-CI. Alle 89 historier
beholdes, og samtlige 63 navngitte metoder finnes i bestått ny TRX. De 56 tidligere UX18-bildene og seks fokusbildene
er hashbevart, med opprinnelig inspeksjonsgrunnlag. EV04s e-postside og nye varselfelter
inngår ikke i disse historiske bildene. Native zoom-/hjelpemiddelgrenser består.
CI bruker 332/186 med uendrede 15/20/45 minutter, aktiv audit og bare TRX.
Denne nødvendige CI-konfliktrunden har ingen ny full gjennomgang eller sikkerhetsaudit.

## Historisk lokal sluttkontroll før EV04

**962/962 ordinære og 328/328 faktiske Edge/Kestrel** passerer uten feil/hopp.
Alle tidligere 901/319/79, 907/325/79 og 956/322/130 navn er bevart eksakt.
**130 SQL Server-tilfeller** er bare bygget/oppdaget lokalt; faktisk SQL Server og
Chromium kreves på publisert kandidat. Låst restore, tre Release-bygg og aktiv
audit passerer med null advarsler/feil. 427 kilde- og 1518 runtimefiler har null drift.
56 tidligere UX18-sluttbilder og 16 UX17-bilder er hashbevart; seks nye visuelt leste
fokusbilder supplerer dem på integrert runtime. Alle 89 historier er kartlagt.
De 88 app-/kompatibilitetshistoriene har bestått bevis fra 63 navngitte metoder
i siste TRX; historie 86 er prototype. Resultater, modell-/blobbevaring og SHA-256 står i
UX18-rapporten og `TestResults/93-main-integration/`. Full Standards-review,
full Spec-review og uavhengig sikkerhetsaudit er fullført med 0 funn på det faste
kildegrunnlaget. Denne publiseringsoppdateringen endrer bare dokumentasjon;
produksjonskilder, tester, byggoppsett, arbeidsflyter og skript er uendret.
Faktisk CI og merge leses fra PR #132 og oppgavekommentarene.

## Reproduser verifikasjonen

Fra repo-roten, med SDK fra `global.json` i PATH:

```powershell
dotnet restore backend/Aarshjul.slnx --locked-mode
dotnet restore backend/Aarshjul.BrowserTests --locked-mode
dotnet restore backend/Aarshjul.SqlServerTests --locked-mode
dotnet build backend/Aarshjul.slnx -c Release --no-restore -m:1 -nr:false
dotnet build backend/Aarshjul.BrowserTests -c Release --no-restore -m:1 -nr:false
dotnet build backend/Aarshjul.SqlServerTests -c Release --no-restore -m:1 -nr:false
dotnet test backend/Aarshjul.slnx -c Release --no-build --logger 'trx;LogFileName=ordinary.trx' --results-directory TestResults/recheck
$env:AARSHJUL_BROWSER_CHANNEL = 'msedge'
dotnet test backend/Aarshjul.BrowserTests -c Release --no-build --logger 'trx;LogFileName=browser.trx' --results-directory TestResults/recheck
dotnet test backend/Aarshjul.SqlServerTests -c Release --no-build --list-tests
```

SQL-kommandoen oppdager bare prøvene. Faktisk SQL Server krever disponibel
testinstans og `AARSHJUL_TEST_SQLSERVER` til `master`, etter
[utviklingsoppsettet](../development.md#obligatorisk-sql-server-kontroll-i-ci).
Chromium fra den låste Playwright-pakken kjøres i CI. Ingen hopp, manglende TRX eller
lavere antallsgrense godtas. Nettleserjobben beholder 45 minutter. Bare TRX lastes
opp. Feed-/bekreftelseslenker holdes i minnet; aktive tokensider, rå logger,
databaser, HAR, video eller sporing publiseres ikke.

## Neste avhengigheter

En fremtidig ny bestilling kan ta #94 AD01 når #39 og levert #92 er kontrollert,
deretter #95 AD02 etter godkjent produksjonskontrakt. #92 er en lesende appoversikt,
og historie 86 gjelder eksempelpersoner i skissen. Ingen faktisk utnevnelse/fjerning
er levert. #42/#43/#48/#55 og #38/#56/#57 beholder sine egne Office-/M365-/Entra-/
Azure-/pilotkriterier. Faktisk drift, tilbakekalling, Key Vault, Data Protection
og produksjonsmigrering krever egne miljøbevis.

Kopierbar oppstart i en **ny chat**, uten å starte en ny ticket automatisk:

```text
Ta opp Atelier-overleveringen etter UX18/#93 i depmartin/Budsjettkalender.
Les AGENTS.md, GLOSSARY.md, docs/status.md, CONTRIBUTING.md, CODING_STANDARDS.md,
relevante ADR-er og docs/acceptance/2026-10-28-atelier-ux18.md samt
2026-10-28-atelier-overlevering.md. Kontroller live #75/#93 med alle kommentarer,
PR #132 med faktisk siste head/CI/merge/main, åpne PR-er og alle grener/worktrees før arbeid.
Skill levert kode fra prototypehistorie 86 og eksterne miljø-/pilotporter.
Mandatet i den gamle chatten stoppet etter #93; start ikke #94/#95, EV-arbeid,
merge av andre PR-er eller deploy på grunnlag av den gamle bestillingen.
Gi en kort, oppdatert status og konkret anbefalt neste avgrensede oppgave.
Bevar uferdige checkouter, plan-/prototypegrener, PR65 og demoene 5076/5077/5088.
Bruk bare ny disponibel database ved lokal visning; Start-AtelierDemo.ps1 velger 5093.
```
