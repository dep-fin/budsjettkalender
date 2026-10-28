# Utvikling

Krever SDK-versjonen i `global.json` (10.0.402). Kjør fra repo-roten.
SDK-en er eksakt låst fordi Blazor legger til en SDK-avhengig asset-pakke i avhengighetsgrafen.
CI, deploy og devcontainer bruker samme versjon. Ved SDK-oppdatering endres også
devcontainerens versjon, og pakkelåsene regenereres og verifiseres.

```sh
dotnet restore backend/Aarshjul.slnx --locked-mode
dotnet build backend/Aarshjul.slnx -c Release --no-restore
dotnet test backend/Aarshjul.slnx -c Release --no-build
# Målrettet test under utvikling (bygger også testprosjektet)
dotnet test backend/Aarshjul.slnx --filter FullyQualifiedName~FristDatautvekslingTester
dotnet run --project backend/Aarshjul.Web --launch-profile demo
```

Demo: åpne `http://localhost:5014/demo`. Profilen bruker Development + Demo=true og
SQLite-filen `aarshjul-demo.db`. Bare Development eller eksplisitt Demo-miljø kan bruke
demoinnlogging. Miljøet Demo krever publiserte assets; bruk launch-profilen under utvikling.
Ved modellendring må demo-databasen flyttes til en sikkerhetskopi eller gjenskapes;
`EnsureCreated` oppgraderer ikke skjemaet. UX08 legger til Fasemaler med én deterministisk
seedet felles mal. Eksisterende demofiler må derfor sikkerhetskopieres/gjenskapes før
bruk av denne modellen; bevar andre kjørende demoers filer. Produksjon bruker den additive
EF-migrasjonen FellesFasemal. Migrate/Startdata overskriver ikke en redigert fasemal.
UX16 legger til `20261028112544_UnikeInternrunder`. Produksjonsmigrasjonen bevarer
eksisterende runder og gjøremål og oppretter unike indekser for rundetype/år og stående
Øvrig. Før en eventuell senere produksjonsmigrering må det tas sikkerhetskopi og
gjøres en forhåndskontroll av duplikater for begge nøklene. Hvis en SQL Server-database
allerede har duplikater, stopper migrasjonen med
beskjed om sikkerhetskopi og avklaring før nytt forsøk. Den sletter eller slår ikke
sammen historiske runder automatisk. Også denne modellendringen krever sikkerhetskopi
eller en ny disponibel fil for eldre `EnsureCreated`-demoer; andre demoers filer beholdes.

EV02 legger i tillegg til `Epostabonnementer`. Bevar tidligere SQLite-filer før en ny
demodatabase opprettes; `EnsureCreated` oppgraderer fortsatt ikke gamle filer.
Produksjonsmigrasjonen `20261028083448_Epostabonnement` er additiv og oppretter ingen
abonnementer for eksisterende brukere. [EV02-akseptansen](acceptance/2026-10-28-epost-ev02.md)
skiller SQLite-operasjonsprøven fra den reelle SQL Server-migrasjonsporten.

## Kontrollert e-postpåmelding (EV02)

Start en ny, disponibel demo med vanlig demoprofil. Logg inn som valgfri persona og
åpne E-postvarsling ved brukernavnet. Registrer en syntetisk egen adresse på et
`.dep.no`-domene og bekreft standardvalgene. Åpne deretter «kontrollert testmottaker»
fra den ventende påmeldingen, følg lenken og bekreft som samme innloggede persona.
Påmeldingen er varig og kan meldes av i samme flate. Ingen ekstern e-post sendes.

Den kontrollerte mottakeren lagrer siste bekreftelsesmelding per bruker bare i minnet.
Etter omstart bevares påmelding og engangsbekreftelse i databasen, men selve meldingen
må allerede være hentet eller bestilles på nytt. Nyutsending krever ett minutts pause
og tillater høyst fem meldinger per bruker i et fast timevindu fra første utsending.
Lenken varer 15 minutter. Fem mislykkede forsøk sperrer den gjeldende lenken; en ny
lenke erstatter den gamle. Avmelding fjerner muligheten til å aktivere med gammel lenke.

Hemmeligheten ligger i URL-fragmentet og fjernes fra nettleserhistorikken før
bekreftelse. Bare SHA-256-avtrykket lagres i databasen. Ikke kopier meldinger, lenker,
databaser eller rå nettleserspor til logger eller CI-artefakter. Testene holder lenker
i minnet og erstatter også oppryddingsfeil med trygge meldinger før TRX. Den lokale
testmottakeren kan bare vises i demo for samme innloggede bruker. I produksjon er
bekreftelsestransporten utilgjengelig; EV02 setter ingen e-posttjeneste i drift.

## Samlet UX16/EV02-verifikasjon

Sluttinventaret er 883 ordinære, 312 nettleser- og 79 SQL Server-tilfeller.
Den nye ordinære modellregresjonen kommer i tillegg til begge greners bevarte navn.
Full lokal Edge/Kestrel og ordinær suite er grønne; SQL Server er bare bygget/oppdaget
lokalt. Faktisk SQL Server og Chromium kreves fortsatt i kandidat-CI.
Kommandoer, TRX, navn, kilde-/runtimebinding og ny topptekstinspeksjon finnes i
`TestResults/91-main-integration/` og [UX16-akseptansen](acceptance/2026-10-28-atelier-ux16.md).
De historiske kjøringene i tidligere bevismapper beholdes uendret.

## Obligatorisk nettleserakseptanse

`backend/Aarshjul.BrowserTests` ligger utenfor den ordinære løsningen. Det krever ingen
eksisterende app eller database: 312 scenarioer bruker disponible Kestrel-verter,
inklusive de fem fra EV02.
Demoflytene starter i Development med Demo=true, tilfeldig loopback-port og ny SQLite-fil.
De kontrollerte flytene bruker den eksisterende HTTP-testverten i Testing med ekte
Kestrel, SQLite i minnet og testautentisering.
Der styres fullføringen av gruppeoppslag, malens laste-/preview-/lagringskall og
databaseporten ved generering/godkjenning/avvisning, maloverføringens preview/import og
dokumentinnlesingens fullføring, fristskjemaets felt-/bevis-/lagringskall og gruppeforvaltningens/innsynets lese- og lagringskall.
Eksportutvalg, fristoverføringens preview/import og abonnementenes lese-/lagringskall
bruker samme grense for feil, avbrudd, feltbytte, bekreftelse og dobbel innsending.
Internkalenderens to editorer prøves også med kontrollerte sene lesesvar/feil,
rask ruteendring og doble lagringer under venting. Faktisk SQLite-lagringsfeil prøver
beholdte oppgavefelt/ansvar, trygg feilmelding og vellykket nytt forsøk i samme krets.
De faktiske PDF-/uttrekks-/lagringsadapterne brukes også
etter dokumentportens venting; kontrollert teknisk feil prøver trygg melding og nytt forsøk.
Dette prøver initialisering, rask navigasjon, asynkront filbytte, dobbeltbekreftelse
og ventetilstand i en interaktiv Blazor-krets. Tjenestenes beregning,
lagring og generering er reelle. Forsinkelsene finnes bare i testverten. Ingen eksterne tjenester brukes.
Dialogregresjonen holder et JS-visningskall igjen mens den faktiske DOM-en er synlig,
og prøver Escape, retur og fokus for leser og begge bidragsytergrupper uten tidsbasert søvn.
Bare syntetiske personaer og filer benyttes. Kjør fra repo-roten:

```sh
dotnet restore backend/Aarshjul.BrowserTests/Aarshjul.BrowserTests.csproj --locked-mode
dotnet build backend/Aarshjul.Web -c Release --no-restore
dotnet build backend/Aarshjul.BrowserTests -c Release --no-restore
pwsh backend/Aarshjul.BrowserTests/bin/Release/net10.0/playwright.ps1 install --with-deps chromium
dotnet test backend/Aarshjul.BrowserTests -c Release --no-build --logger 'trx;LogFileName=browser.trx' --results-directory BrowserTestResults
```

På Windows kan installert Edge brukes ved å sette `$env:AARSHJUL_BROWSER_CHANNEL = 'msedge'`;
da trengs ikke browserinstallasjonen. `AARSHJUL_DOTNET` kan angi en absolutt SDK-kjørbar fil,
ellers brukes `dotnet` fra PATH. `AARSHJUL_BROWSER_EVIDENCE` kan angi en lokal mappe for nye
undermapper med demo-database, app-logg, representative kalenderskjermbilder og nedlastinger.
Standard er systemets tempmappe. Testen stopper bare app-prosessen den selv opprettet.
Fasemalprøven stopper og starter denne samme Kestrel-prosessen med samme SQLite-sti,
og bekrefter bevart mal/versjon etter ny leserinnlogging. Andre demoer berøres ikke.
Oppstartsfeil og feil ved lukking utløser opprydding av alle opprettede kontekster,
nettleseren, Playwright og demoprosessen. Oppryddingsfeil rapporteres samlet og beholder
en eventuell opprinnelig oppstarts- eller scenariofeil; de gjør ikke et feilet scenario grønt.

UX16s lokale fullverifikasjon før reviewrettingen ligger i `TestResults/91-final-verified/`.
Låst restore med aktiv audit og Release-bygg med SDK 10.0.402 passerer for ordinary,
BrowserTests og SqlServerTests uten advarsler eller feil. Alle 841 ordinære tester og
301 reelle Edge/Kestrel-scenarioer passerer uten feil eller hopp etter siste prøveendring.
Browser-TRX måler 1914,527 sekunder; prosessen måler 1915,334 sekunder. De tidligere
804/264/46 og nåværende 841/301/63 navnene er bevart. SQL-prosjektets 63 tilfeller er
bygget og oppdaget; faktisk SQL Server og Chromium gjenstår i kandidat-CI. Kilde-/binærgrunnlaget
er hashkontrollert uendret gjennom denne kjøringen, og alle 90 tidligere inspiserte interne
desktop-/mobilbilder har uendrede hashverdier og dette historiske produksjonsgrunnlaget.

Et felles P2 fra samlet Standards-/Spec-review gjaldt bare endret ansvar i de to
interne editorene: barnet varslet ikke forelderen, og native reload kunne miste
arbeidet uten varsel. Én callback etter hver av de tre listemutasjonene oppdaterer
nå forelderen og dens eksisterende NavigationLock. Seks nye faktiske Edge-prøver
feiler før rettingen og passerer etterpå. De prøver avvist native reload med
beholdte felt, Escape/fokus, uttrykkelig forkasting, lagring og ren ny sidehenting.
Fire nye tokenfrie desktop-/mobilbilder er visuelt inspisert uten funn.
Ny samlet verifikasjon etter siste kodeendring passerer **841/841** ordinære tester
og **307/307** faktiske Edge/Kestrel-scenarioer, uten feil eller hopp, i
`TestResults/91-responsible-final/`. Alle tre prosjektenes låste restore med aktiv
audit (`true/all/low`) og Release-bygg (`-m:1 -nr:false`) passerer uten advarsler/feil.
Browser-TRX måler 1296,951 sekunder, prosessen 1297,892 sekunder. Alle 63 SQL-tilfeller
er bygget/oppdaget; faktisk SQL Server og Chromium gjenstår i kandidat-CI.
377 kildefiler og 1393 runtimefiler var uendret gjennom kjøringen. Hele kandidatens
443 filer, TRX, testnavn og nye røde/grønne bevis er bundet med manifest og SHA-256.
Alle 2510 originalbevisfiler, de tidligere 841/301/63 navnene og de 90 bildehashene
er bevart. De fire nye inspiserte dialogbildene bruker samme relevante Web-/test-DLL
som den endelige fullkjøringen. Lokal kjøretid dokumenterer ikke CI-jobbens varighet.

Alle UI-handler bruker tilgjengelige navn og avgrensede forventninger. Før skriving venter
testen på første interaktive render; synlig prerender alene er ikke tilstrekkelig.
Nye personakontekster gir nye cookies. API-kontroller krysser samme serversynlighet som UI.
JavaScript-/konsollfeil og brudd i Blazor-kretsen gjør scenarioet rødt.
En kontrollert, gammel køfane er en UI-prøve; den deterministiske samtidighetskontrakten
for to operasjoner forblir i Application-/SQL-testene.

CI-jobben `Nettleser – samlet akseptanse` bruker Chromium fra den låste Playwright-pakken,
installerer systemavhengigheter og krever nøyaktig 312 beståtte scenarioer uten hopp.
Den samlede UX16/EV02-kandidaten beholder merget mains jobbgrense på 45 minutter.
Begge greners første CI ble avbrutt ved 15 minutter uten fullført nettleser-TRX;
EV02s lokale fulle kjøringer tok 23:03 og 30:08, og UX15s sluttkjøring tok 21:42.
De lokale tidene begrunner rom for testene og oppsettet, men måler ikke Chromium.
Ren EV02-CI [40673623216](https://github.com/depmartin/Budsjettkalender/actions/runs/40673623216)
fullførte senere 226 Chromium-scenarioer: teststeget tok 11:57 og jobben 13:08.
Den målingen dekker ikke de 312 integrerte tilfellene etter UX16. UX15s tidligere
20-minuttersforsøk er historisk kontekst; 45 minutter er en beholdt øvre grense,
ingen påstand om nødvendig kjøretid eller garanti for det nye inventaret.
Ingen forventningstimeout, antallskontroll eller kontroll av manglende TRX er svekket.
Ny kandidat-CI må fullføre alle 312; et nytt tidsavbrudd krever diagnose før ytterligere økning.
Bare TRX lastes opp. Feed-token og URL-er skrives ikke til rapportene; abonnementssidens
unntak redigeres før de når TRX. Ikke last opp databaser, app-logger eller rå nettleserspor.
`deploy.yml` gjenbruker CI og venter på ordinære tester, SQL Server og nettleserakseptanse.
Se [akseptansematrisen](acceptance/2026-10-20.md), [UX02-prøvene](acceptance/2026-10-27-atelier-ux02.md)
og [UX03-prøvene](acceptance/2026-10-27-atelier-ux03.md), samt
[UX04-prøvene](acceptance/2026-10-27-atelier-ux04.md) og
[UX05-prøvene](acceptance/2026-10-27-atelier-ux05.md) og
[UX06-prøvene](acceptance/2026-10-27-atelier-ux06.md) og
[UX07-prøvene](acceptance/2026-10-27-atelier-ux07.md), [UX08-prøvene](acceptance/2026-10-27-atelier-ux08.md)
og [UX09-prøvene](acceptance/2026-10-27-atelier-ux09.md), samt
[UX10-prøvene](acceptance/2026-10-27-atelier-ux10.md), [UX11-prøvene](acceptance/2026-10-27-atelier-ux11.md)
og [UX12-prøvene](acceptance/2026-10-28-atelier-ux12.md), samt
[UX13-prøvene](acceptance/2026-10-28-atelier-ux13.md), [UX14-prøvene](acceptance/2026-10-28-atelier-ux14.md)
og [UX15-prøvene](acceptance/2026-10-28-atelier-ux15.md), samt
[UX16-prøvene](acceptance/2026-10-28-atelier-ux16.md) og
[EV02-prøvene](acceptance/2026-10-28-epost-ev02.md) for faktisk prøvd scope og eksterne grenser.

## Obligatorisk SQL Server-kontroll i CI

`backend/Aarshjul.SqlServerTests` kjører samme 21 kontrakttilfeller ved IGodkjenningsko som
SQLite-suiten: to godkjenninger, godkjenning mot avvisning i begge rekkefølger, justering,
POL, endringsforslags synlighet, rollback ved en reell lagringsfeil og sletting av målfristen
ved import før et endringsforslag lagres. Sistnevnte skal gi databasefeil, beholde forslaget
åpent og unngå varsel fra den mislykkede publiseringen. Testportene holder
to uavhengige kontekster og åpne tilkoblinger før første behandling får fullføre.
Rollback-triggeren kontrollerer at forslag, frist og varsel allerede er skrevet i den
pågående transaksjonen før den kaster feil. SQL Server bruker MaxBatchSize(1) i testene.
Seks tilfeller prøver gammel godkjenning, justering og avvisning etter avvisning og revidert
innsending av samme rad, for både nye frister og endringsforslag. De bekrefter at den nye
innsendingen forblir åpen, uten feil publisering eller ekstra varsel. En separat SQL-test
oppgraderer produksjonsskjemaet etter Internkalender med et eksisterende forslag og
bekrefter at innholdet bevares og kan behandles. Med UX08 kjører prosjektet også 14 delte
fasemaltilfeller og én reell oppgraderingstest fra siste tidligere migrasjon. SQL-prosjektet
har samlet 79 testtilfeller: de tidligere 46, 14 delte internkalendertilfeller,
tre migrasjonsprøver fra UX16 og 16 e-post-/migrasjonstilfeller fra EV02.
UX11-tilfellene bruker faktisk lagret mal ved sideeffektfri prøve og generering,
bevarer andre års og behandlede forslag, prøver helt ugyldig og tom erstatning,
og fremtvinger feil etter observerte DELETE/INSERT eller bare DELETE.
En ny operasjon på samme tjeneste lykkes etter at feilen er fjernet. Generert
månedsgodkjenning prøver aktivt POL-valg, serveravvisning av ugyldige grupper,
én publisert identitet gjennom leseporten og avvisning av en slettet gammel forslag-ID.
UX16s 14 delte tilfeller ved `IInternkalender` styrer overlapp før det endelige
skriveøyeblikket: fullføring/manuell endring mot oppdatering/fjerning, dobbel synk,
samtidig oppretting, feil runde, foreldet grunnlag, delutvalg og regelendringer etter
siste lesing. De tre SQL-migrasjonsprøvene bevarer eksisterende data ved oppgradering
og avviser historiske duplikater for både årlig runde og Øvrig uten datatap.
De starter nå fra `20261028083448_Epostabonnement` og oppgraderer til
`20261028112544_UnikeInternrunder`. Den positive prøven bevarer også et aktivt og
et ventende e-postabonnement; den ventende bekreftelsen kan brukes etter oppgraderingen.
EV02s tidligere oppgradering fra FellesFasemal er fortsatt en egen prøve.
Den ordinære modellregresjonen kontrollerer SQL Server-modellen uten tilkobling:
ingen ventende modellendringer, e-postentiteten og begge internrundeindeksene i
siste migrasjonsmål, samt alle berørte samtidighetspredikater. Dette erstatter
ikke de 79 faktiske SQL Server-prøvene i kandidat-CI.
Regelinnholdet ved synk er snapshotet fra operasjonens siste lesing før lagring;
en regelendring etter denne lesingen blir synlig i neste forhåndsvisning. Dette er
egen delt kontrakt, og UI leser derfor forslagene på nytt etter lagring. Synk låser
ikke regelregisteret mot samtidig redigering i andre runder. Fullføring/manuell
oppgaveendring og dobbel synk har separat vern ved det endelige skriveøyeblikket.

EV02 legger til 15 delte e-postabonnementstilfeller og én oppgradering fra FellesFasemal
med bevart frist-/forslags-/kilde-/synlighetsinnhold. Eierskap, eksplisitte standarder,
varig starttidspunkt, utløp, engangsbruk, forsøkstak, nyutsending og to samtidige
instanser prøves gjennom `IEpostabonnement`, også avmelding/aktivering mens transporten
fullfører en overlevering. Sammen med UX16 har prosjektet 79 tilfeller.

Prosjektet ligger bevisst utenfor `backend/Aarshjul.slnx`. De ordinære lokale kommandoene
krever derfor verken SQL Server eller Docker. SQL-testene krever en disponibel testinstans
med rettighet til å opprette og slette databaser. Oppgi tilkobling til `master`, aldri en
applikasjonsdatabase. Hvert tilfelle oppretter et nytt navn med testprefiks og GUID,
blant annet `AarshjulGodkjenning_<guid>` og `AarshjulIntern_<guid>`,
kjører MigrateAsync og sletter bare databasen det selv har opprettet, også ved testfeil.
Eksisterende databaser blir ikke resatt eller slettet. Ikke bruk produksjonsinstansen.

```sh
export AARSHJUL_TEST_SQLSERVER='Server=localhost,1433;Database=master;User Id=sa;Password=<testpassord>;Encrypt=True;TrustServerCertificate=True'
dotnet restore backend/Aarshjul.SqlServerTests/Aarshjul.SqlServerTests.csproj --locked-mode
dotnet test backend/Aarshjul.SqlServerTests/Aarshjul.SqlServerTests.csproj -c Release --no-restore
```

På PowerShell settes miljøvariabelen med `$env:AARSHJUL_TEST_SQLSERVER = '...'`.
Manglende variabel gir en testfeil med en gang. Testoppsettet venter inntil 60 sekunder
på vellykket SQL-tilkobling og SELECT 1 før det oppretter databasen; utilgjengelig server
gir feil. Ingen tester hopper over SQL-kravet eller erstatter det med SQLite.

CI-jobben `SQL Server – godkjenning og rollback` starter
[SQL Server 2022 CU22 GDR1](https://mcr.microsoft.com/en-us/artifact/mar/mssql/server/tag/2022-CU22-GDR1-ubuntu-22.04)
i en Linux-container med tilfeldig testpassord som maskeres sammen med tilkoblingsstrengen.
Den gjenoppretter låste pakker og krever en TRX-fil med nøyaktig 79 beståtte tester og null hopp.
Containeren fjernes til slutt. `deploy.yml` kaller hele CI-arbeidsflyten; også SQL-jobben
må lykkes før utrulling. Denne kontrollen dekker SQL Server, mens Azure SQL/Entra og
driftsmiljøets nettverk fortsatt må testes separat.

## Malregler og import

Regresjonene for felles parameterkontroll ligger ved IMaltjeneste og IMalDatautveksling:
`dotnet test backend/Aarshjul.slnx --filter FullyQualifiedName~RegelvalideringTester`.
Testene dekker avvisning før manuell endring, advarsel/utelating per importregel,
kompatible eldre titler, gyldige datoberegninger og rollback etter faktisk sletting.
Samme tjeneste kan brukes igjen etter en lagringsfeil; operasjonskonteksten disponeres.
Se [ADR-0008](adr/0008-felles-validering-av-gjentaksregler.md) for parameterfelter,
innstrammingen til uke 1–5 og hvorfor en helt ugyldig erstatt-alt-fil gir en tom mal.

## Avhengigheter

`packages.lock.json` i hvert prosjekt låser hele avhengighetsgrafen.
Ved bevisst pakkeoppdatering, endre referansene og kjør:
```sh
dotnet restore backend/Aarshjul.slnx --force-evaluate
dotnet test backend/Aarshjul.slnx -c Release
```
Commit prosjektfilene og oppdaterte låsefiler sammen. Restore feiler ved kjente
NuGet-sårbarheter; vurder og oppdater pakken fremfor å slå av kontrollen.

## Migrasjoner og produksjon

Installer dotnet-ef i kompatibel 10.x-versjon ved behov.
```sh
dotnet ef migrations add <Navn> --project backend/Aarshjul.Infrastructure --startup-project backend/Aarshjul.Infrastructure
```
Produksjonsappen kjører foreløpig migrasjoner ved oppstart. Egen deploy-migrering,
SQL/Entra-oppsett, Data Protection og Azure-nettverk må verifiseres i et driftsmiljø.
Se `infra/README.md` og gjennomgangens åpne oppgaver før utrulling.
