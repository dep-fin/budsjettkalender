# R-6/2026: undersøkelse av dagens PDF-uttrekk

Undersøkelse for [#137](https://github.com/depmartin/Budsjettkalender/issues/137),
under [#136 punkt 6](https://github.com/depmartin/Budsjettkalender/issues/136).
Kilde- og kodegrunnlag er kontrollert 29. oktober 2026. Begge sakene hadde ingen
kommentarer da de ble lest. Rapporten gjelder én offentlig PDF og dagens offline-kjede.

## Kilde og fasit

[Original PDF](r-6-2026.pdf) er en uendret kopi av
[Finansdepartementets fil](https://www.regjeringen.no/globalassets/departementene/fin/rundskriv/arlige/2026/r-6-2026.pdf).
URL, hentetid, byteantall og SHA-256 står i [kildejournalen](source.json).
Filens SHA-256 er `B902EABED996A1C077D1F242D11B24ECFDE044F898880BEB1ADAD5CEBB9FB60A`.
Den har 17 sider. Tidsplanen er på trykt/PDF-side 3, punkt 2.1.

[Fasit](fasit.json) er manuelt transkribert og visuelt kontrollert mot en rendering
av originalens side 3. Titlenes linjeskift er erstattet med mellomrom; datoteksten
er bevart. Fasit er uavhengig av appens forslag. Tabellen har sju dagsfrister,
ett todagersintervall og én tentativ måned. Kalenderdatoene gjelder 2026 og 2027,
selv om kjøringen har arbeidsår 2028. Rad 3 sier uttrykkelig **2027-budsjettet**.
Dette er bevart og er ikke tolket om til 2028.

| Rad | Kildens aktivitet | Original datotekst | Datotype |
| --- | --- | --- | --- |
| 1 | Førsteutkast/skisse, strukturelle utfordringer og -tiltak | 16. oktober 2026 | Dag |
| 2 | Ferdigstillelse av omtalen av strukturelle utfordringer og -tiltak | 18. desember 2026 | Dag |
| 3 | Spesifikasjon av driftspostene for 2027-budsjettet | 4. januar 2027 | Dag |
| 4 | Materiale til teknisk justering og flerårige budsjettkonsekvenser | 6. januar 2027 | Dag |
| 5 | Mulige innsparingstiltak | 15. januar 2027 | Dag |
| 6 | Satsingsforslag | 22. januar 2027 | Dag |
| 7 | Tabeller for nettobudsjetterte virksomheter | 26. februar 2027 | Dag |
| 8 | Regjeringens marskonferanse | 9.–10. mars 2027 | Intervall, begge dager |
| 9 | Utsending av Hovedbudsjettskriv | Ultimo mars 2027 | Tentativ måned, ultimo |

## Observerte resultater

Tre fullkjøringer mot hver sin nye SQLite-fil ga identiske resultater: **6 forslag,
12 lagrede uttrekksbevis og 0 publiserte frister**. Av sju dagsfrister finnes bare
første dato som forslag. Dens tittel blander flere rader. Det gir **0 korrekte
radkoblinger, 1 riktig dagsdato med feil tittel og 6 oversette dagsrader**.
Intervall og tentativ måned har heller ikke egne forslag og telles separat fra
de seks tapte dagsfristene. Fem forslag gjelder referanser eller forutsetninger.

[Radvis sammenlikning](row-comparison.json) dekker samtlige ni rader.
[Alle lagrede forslag](proposals.json) inneholder faktisk inngangssegment,
tittel, dato, budsjettår, løp, status og begge lagrede per-felt-bevis.
[Manuell klassifisering](classifications.json) dekker alle seks forslag:

| Kandidat / segment | Foreslått dato | Kildested for datoen | Klassifisering og vurdering |
| --- | --- | --- | --- |
| 1 / 15 | 2026-06-23 | Side 1, brevhodets dokumentdato | Referanse/forutsetning. Feil å lage innleveringsfrist fra dokumentdato. Tittelen starter med innholdsfortegnelsens «Teknisk justering i BUDMOD». |
| 2 / 41 | 2026-10-16 | Side 3, punkt 2.1, rad 1 | Reell frist, men feil radkobling: alle ni rader er én inngang. Tittelen begynner «Foreløpig tidsplan …» og inkluderer andre raders titler og datoer. |
| 3 / 57 | 2026-12-15 | Side 4, punkt 3.1 | Referanse/forutsetning: valutakursgrunnlag. |
| 4 / 73 | 2027-07-01 | Side 5, punkt 3.3 | Referanse/forutsetning: prisnivå for kostnadsrammer. |
| 5 / 77 | 2028-12-31 | Side 5, punkt 3.4, Tabell 3 | Referanse/forutsetning: kontantbeholdning ved årsslutt. Årstallet 2028 er gjettet fra arbeidsår. |
| 6 / 256 | 2028-12-31 | Side 16, vedlegg 3-3 / Tabell 3 | Referanse/forutsetning: samme beholdningsdato gjentatt i vedleggslisten. Segmentet spenner over side 15–17; tittelen starter «Med hilsen …». |

Det er 1 reell fristkandidat, 5 referanse-/forutsetningskandidater og 0 uavklarte
kandidater etter denne manuelle klassifiseringen. Første innlesing har 0 eksakte
dubletter målt som identisk tittel+datofelt, men 1 semantisk referansedublett:
kandidat 6 gjentar kandidat 5 sitt Tabell 3-grunnlag. Lik dato alene er ikke
brukt som bevis på en dublett.

Alle seks har `Budsjettaar=2028`, `Datopresisjon=Dag`, ingen datokvalifikator,
`Status=Forslag`, `Opphav=Robot` og foreslått synlighet FA + FIN-FAG.
Disse verdiene er faktisk lest fra databasen. Arbeidsåret skrives på alle forslag;
det er ikke uttrukket fra hver rad. De eksplisitte kalenderårene i de fire første
forslagene bevares. Kandidat 2 får løp `rammefordeling` fra hele det sammenslåtte
segmentet; løpsetiketten beviser ikke en riktig kobling til første tabellrad.

## Tekst, segmenter og årsaker

[Eksakt PDF-tekst](pdftekst.json) bevarer den strengen produksjonsleseren leverte,
inkludert kontrolltegn og linjeskift. Den er 41 215 tegn.
[Sidevis tekst](pages.json) viser at **alle ni titler og datouttrykk finnes på
side 3 etter PDF-leseren**. Det er ikke observert tap av tabellcellene i dette
leddet. Linjebrudd finnes mellom rader og i lange titler, og må ikke forveksles
med en ferdig strukturert tabellmodell.

[Alle segmenter](segments.json) viser 256 segmenter. Seks tilfredsstiller
datofilteret før `Take(40)`; alle seks sendes videre og lagres. **Grensen 40 er
ikke årsaken i denne PDF-en.** Segment 41 er kandidat 2, ikke kandidat 41.
Ingen kandidater ble avskåret av grensen i denne kjøringen.

Den reproduserte tapskjeden er:

1. [PdftekstLeser](../../../backend/Aarshjul.Infrastructure/Datouttrekk/PdftekstLeser.cs)
   leverer tabellteksten med linjeskift, uten strukturerte rad-/sideobjekter.
2. [OpplastingsTjeneste.DelISetninger](../../../backend/Aarshjul.Infrastructure/Opplasting/OpplastingsTjeneste.cs)
   gjør linjeskift til mellomrom. Siden radene ikke avsluttes med setningspunktum
   før neste store bokstav, blir hele tidsplanen ett segment, nummer 41.
3. [HeuristiskDatouttrekk](../../../backend/Aarshjul.Infrastructure/Datouttrekk/HeuristiskDatouttrekk.cs)
   returnerer én konkret dato og én tittel per kall. I dette segmentet velges
   16. oktober 2026; de øvrige åtte datouttrykkene gir ingen egne resultater.
4. Tittelen avkortes til 400 tegn + ellipse og kildeutdraget til 600 tegn + ellipse.
   Kandidat 2 sitt lagrede bevis dekker derfor ikke hele tabellen. Kandidat 6
   sitt datobevis mister selve «31. desember» ved avkorting. Fullt inngangssegment
   finnes i denne undersøkelsens data, men ikke i det lagrede utdraget alene.

Etter den første røde fullkjøringen ble følgende hypoteser prøvd med avgrensede
kontroller. Resultatene står i [sondene](probes.json):

| Hypotese/prediksjon | Observert kontroll | Konklusjon for denne filen |
| --- | --- | --- |
| Radgrensene hindrer selvstendige dagsresultater; isolerte rader skal virke | Samme produksjonsheuristikk på hver fasitrad ga 7/7 korrekte dagsdatoer og separate, riktige titler | Støtter rad-/segmentkoblingen som nærmeste rettbare årsak. Dette er en isolasjonssonde, ikke en rettet fullkjøring. |
| PDF-leseren mister celler | Alle ni komplette radtitler og datouttrykk finnes i faktisk side-3-tekst | Ikke observert som årsak. |
| Kandidatgrensen gir tap | Bare 6 kandidater før grensen 40 | Falsifisert for denne filen; andre dokumenter er ikke målt. |
| Bare radgrenser er nok for intervall og tentativ måned | Isolert «9.–10. mars 2027» ga bare 2027-03-10 og tittelsuffiks «9.–». «Ultimo mars 2027» ga ingen konkret dato | Falsifisert. Dagens heuristikk leverer ikke full representasjon av disse to uttrykkene. Domenet har månedspresisjon, men dette uttrekksleddet bruker den ikke. |
| Manglende år gjettes fra arbeidsår | Samme årsløse «31. desember» ga 2029-12-31 når bare arbeidsåret var 2029 | Bekreftet reserveår, ikke kildebevis for 2028/2029. |

Det er også observert at datogjenkjenning alene slipper inn brevhodet,
valutakursdato, prisnivådato og årssluttreferanser. At en dato er gjenkjennelig
er dermed utilstrekkelig til å avgjøre om den er en frist. Et bytte av PDF-bibliotek
eller en ekstern modell er ikke prøvd og er ingen dokumentert løsning her.

## Identisk gjenopplasting og deduplisering

[Kjøringsresultatet](summary.json) viser følgende sekvens i hver disponible database:

| Handling | Nye forslag | Lagret total | Allerede behandlet |
| --- | --- | --- | --- |
| Original PDF, `r-6-2026.pdf`, arbeidsår 2028 | 6 | 6 | Nei |
| Identisk fil og navn, arbeidsår 2028 | 0 | 6 | Ja |
| Samme bytes, navn ` R-6-2026.PDF `, arbeidsår 2029 | 0 | 6 | Ja |
| Original bytes + én avsluttende LF, samme navn | 0 | 6 | Ja |
| Original bytes, nytt navn `renamed-r-6-2026.pdf` | 6 | 12 | Nei |

Appens dokumentnøkkel er trimmet filnavn i små bokstaver med mellomrom erstattet
av bindestrek; kilden er `opplastet`. Hashen er SHA-256 over UTF-8 av den uttrukne
tekststrengen, her
`51118C652F667A9C06457F54646C5DAB490061C9E6B5D07EA16AB384C66C6EF7`.
Dette er **ikke** PDF-filens hash. Endret PDF-byte ved slutten ga annen filhash,
men samme uttrukne tekst og dermed ingen nye forslag. Arbeidsår inngår ikke i
denne dedupliseringsnøkkelen. Nytt filnavn omgår den, og lager seks gjentatte
forslag og et nytt behandlet dokument. Etter hele sekvensen er det 2 behandlede
dokumenter, 12 forslag og fortsatt 0 publiserte frister.
Teksthashen gjelder den observerte Windows-strengen; PDF-leserens `AppendLine`
bruker plattformens linjeskift. Samme teksthash på en annen plattform er ikke prøvd.

## Én anbefalt første retteticket

**Bevar koblingen mellom dagsfrist og aktivitet i PDF-tabellen gjennom
`IDokumentopplasting`.** Avgrens første retting til oppdelingen av kandidater fra
dagens PDF-tekst slik at flerlinjede titler og radens dato følger hverandre.
Ikke hardkod rundskrivnummer, sidetall eller ordlyden i denne PDF-en.

Offentlig regresjonsgrense: last den bevarte original-PDF-en gjennom
`IDokumentopplasting` med ekte `PdftekstLeser`/`HeuristiskDatouttrekk`, arbeidsår
2028 og ny SQLite. Kontroller lagrede forslag og bevis mot de sju dagsradene:
sju selvstendige, entydige tittel/dato-koblinger; kalenderårene 2026/2027 bevart;
«2027-budsjettet» bevart i rad 3; beviset for hver rad inneholder dens egen dato
og aktivitet uten blanding med nabofrister. Identisk ny innlesing skal fortsatt
gi null nye forslag og ingen publisering. Ta med en annen liten tabell med
flerlinjet aktivitet som motprøve mot filspesifikk tilpasning.

Forventet effekt for denne fasiten er å gå fra 0 til 7 korrekte dagsradkoblinger.
Dette er en anbefaling, **ikke et levert eller verifisert rettingsresultat**.
Intervall, tentativ måned, semantisk referansefiltrering, større kandidatsett,
dedupliseringspolicy og generell kvalitetsaksept krever fortsatt egne avklaringer.
De fem referanseforslagene inngår ikke i lovnaden om effekt fra denne ene rettingen.

## Kjørt grense og isolasjon

Kandidatens uendrede runtimekode er base
`29421c2ff1a1595ecfaba5e80d18ce529ade8c03`, branch
`codex/137-pdf-undersokelse`. Denne leveransen legger bare til dokumentasjon og data.
Rapporten identifiserer derfor runtimegrunnlaget med base-SHA; den endelige
dokumentasjonscommiten finnes i PR-en. [Manifestet](manifest.json) identifiserer
de publiserte filene og bytekonvensjonene.

Et midlertidig konsollprogram ligger lokalt i ignorert `TestResults/137/harness/`.
Det har en prosjektreferanse til `Aarshjul.Infrastructure` fra denne checkouten,
og bruker EF Core SQLite 10.0.9 og SQLitePCLRaw 2.1.14. Det kaller
`IDokumentopplasting.LesOgLagForslagAsync(new OpplastetDokument("r-6-2026.pdf", bytes), 2028)`
med produksjonens `PdftekstLeser`, `HeuristiskDatouttrekk`, `OpplastingsTjeneste`
og `Synlighetsregel`. Dette er de samme adapterne som registreres i
[Program.cs](../../../backend/Aarshjul.Web/Program.cs).

En observerende `IDatouttrekk`-dekoratør registrerer inngang og retur og videresender
uendret til `HeuristiskDatouttrekk`. Den erstatter ingen PDF-tekst, uttrekksverdi
eller databaseoperasjon. Forslag og begge uttrekksbevis per forslag leses tilbake
fra SQLite etter tjenestekallet. `EnsureCreated` oppretter bare en ny database
per kjøring under `TestResults/137/`; ingen migrasjon kjøres. Den faste testklokken
er `2026-10-29T09:00:00Z`, og er skilt fra kildens faktiske hentetid.

Segmentlisten hentes ved refleksjon fra den faktiske private `DelISetninger`-
metoden, og alle segmenter registreres før datofilter og grensen 40. Sidevis
tekst registreres med samme PdfPig `ContentOrderTextExtractor` som produksjonsleseren.
Dette er observasjon, ikke en endret segmenteringsimplementasjon.

Den offentlige tjenestegrensen er kjørt direkte. Blazor-opplasting, HTTP-policy,
innlogging, nettleser, SQL Server, Azure og live kildeworker inngår ikke i beviset.
Ingen frist er godkjent eller publisert. Ingen ekstern uttrekksleverandør er brukt.

## Reproduksjonskommandoer og lokale bevis

Alle kommandoer nedenfor kjøres fra denne ticketens checkout. `TestResults/137/sdk/dotnet.exe`
er en lokal installasjon av nøyaktig SDK 10.0.402 fra `global.json`.
Maskinens vanlige `dotnet` fant bare SDK 10.0.302 og ble derfor ikke brukt til kjøringen.
SDK og offentlig kildefil ble hentet med tillatt nettverkseskalering etter at
sandboxens DNS-oppslag feilet. Ingen repo- eller maskinkonfigurasjon ble endret.

```powershell
Invoke-WebRequest -Uri 'https://www.regjeringen.no/globalassets/departementene/fin/rundskriv/arlige/2026/r-6-2026.pdf' -OutFile TestResults/137/r-6-2026.pdf
Get-FileHash TestResults/137/r-6-2026.pdf -Algorithm SHA256
./TestResults/137/sdk/dotnet.exe --version
./TestResults/137/sdk/dotnet.exe restore backend/Aarshjul.Infrastructure/Aarshjul.Infrastructure.csproj --locked-mode -m:1 -nr:false
./TestResults/137/sdk/dotnet.exe restore TestResults/137/harness/Investigation.csproj -m:1 -nr:false
./TestResults/137/sdk/dotnet.exe build TestResults/137/harness/Investigation.csproj -c Release --no-restore -m:1 -nr:false
./TestResults/137/sdk/dotnet.exe TestResults/137/harness/bin/Release/net10.0/Investigation.dll TestResults/137 run1
./TestResults/137/sdk/dotnet.exe TestResults/137/harness/bin/Release/net10.0/Investigation.dll TestResults/137 run2
./TestResults/137/sdk/dotnet.exe TestResults/137/harness/bin/Release/net10.0/Investigation.dll TestResults/137 run3
./TestResults/137/sdk/dotnet.exe TestResults/137/harness/bin/Release/net10.0/Investigation.dll TestResults/137 probes
```

Release-bygget fullførte med 0 advarsler og 0 feil. Alle tre fullkjøringer returnerte
bevisst exitkode 2 for det røde symptomet, ikke for en kjørefeil:

```text
RESULT: 6 saved proposals; 6 candidates before limit; 12 evidence fields; repeat new=0; published=0
RED-CAPABLE SIGNAL: seven distinct day-row dates observed=1/7; verdict=RED
```

Signalet kontrollerer dagsdatoene og kan bli rødt på dette tapet; det er alene
svakere enn den manuelle fulle tittel/dato-kontrollen. Funn er ikke rettet i denne
ticketen, så det forventes fortsatt rødt. Andre fullkjøring tok omtrent tre
sekunder. `summary`, `proposals`, `segments`, `pages`, rå PDF-tekst og runtimejournal
var byteidentiske mellom de tre fullkjøringene. Tredje kjøring brukte også det
ferdige midlertidige programmet etter at den separate sondemodusen var lagt til.
[Kontrolljournalen](controls.json)
registrerer kontroller og kommandoer; [runtimejournalen](runtime.json) binder de
faktisk brukte samlingene med versjon og SHA-256.

Første restore med begrenset nettverk fikk NU1900 for utilgjengelig
sårbarhetsindeks. Senere restore av harness og produksjonsreferanser med tillatt
nettverk fullførte uten den advarselen. Sårbarhetskontrollen ble ikke deaktivert.
En innledende parallell MSBuild-restore avsluttet uten diagnostikk; kjøringen ble
gjentatt med `-m:1 -nr:false`. Ingen slik feil er telt som godkjent kjørebevis.

Original PDF, kildetidspunkt, side 3-rendering, disponibel SQLite, midlertidig
program/prosjekt, byggutdata og fulle kjøringsbevis beholdes lokalt i
`TestResults/137/`. Selve undersøkelsesprogrammet, SDK, databaser og logger
publiseres ikke. Manifestet gjør de publiserte dataene portable og etterprøvbare;
en ny full kjøring krever et tilsvarende midlertidig program mot den navngitte
tjenestegrensen og låst kodegrunnlag. Dette er en uttrykkelig reproduksjonsgrense.

Publisert inventar er original-PDF, denne rapporten og 13 JSON-filer:
`source`, `fasit`, `row-comparison`, `pdftekst`, `pages`, `segments`, `proposals`,
`classifications`, `summary`, `probes`, `runtime`, `controls` og `manifest`.
Manifestet hashbinder de øvrige 14 filene. PDF hashes som råbytes. De navngitte
Markdown-/JSON-filene hashes som UTF-8 med CRLF normalisert til LF for portabel
transport; ingen slik normalisering gjøres av tekststrengen inne i `pdftekst.json`
når appens `InnholdHash` kontrolleres. Manifestets egen hash bindes av PR-ens Git-tree.

Lokale valideringskontroller bekrefter originalhash, nøyaktig teksthash, at
sidevis produksjonstekst rekonstruerer helteksten, alle ni kildetitler/datoer på
side 3, alle seks kandidater og to bevis per kandidat, segmentkoblinger,
klassifisering, duplikatdefinisjoner, sju isolerte dagsresultater, null
publisering, JSON-format og alle relative lenker. `git diff --check` er rent.
Ingen dokumentkontrollskript finnes i repoet ved basen. Ordinære kode-/browser-/SQL-
tester er ikke brukt som aksept for denne rene dokumentasjons- og dataleveransen.
Den dokumentasjonsavgrensede ferdigporten krever ikke ordinært code-review og
sikkerhetsaudit; rapportens kilde-, konsistens- og reproduksjonskontroller gjelder.

## Ferdigport og videre eierskap

[#47](https://github.com/depmartin/Budsjettkalender/issues/47) beholder uavklarte
kandidater, kandidat 41+ og reverserbar forkasting.
[#61](https://github.com/depmartin/Budsjettkalender/issues/61) beholder representativ
fasit, kvalitetsmål og leverandør-/datavalg, og
[#62](https://github.com/depmartin/Budsjettkalender/issues/62) videre uttrekksleveranse.
[#88](https://github.com/depmartin/Budsjettkalender/issues/88) leverte dokumentflyten.
Denne undersøkelsen lukker bare #137 og endrer ikke disse oppgavenes omfang.
Det er ikke opprettet en ny retteticket. Ingen deploy inngår.
