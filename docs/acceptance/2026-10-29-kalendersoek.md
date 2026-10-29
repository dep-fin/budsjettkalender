# Kalender: søk i alle datoer

Lokal byggekandidat 29. oktober 2026 for
[#139](https://github.com/depmartin/Budsjettkalender/issues/139),
«Kalender: Søk i alle datoer med treffliste og navigasjon til fristen», under
[#136 punkt 2](https://github.com/depmartin/Budsjettkalender/issues/136).
Basen er faktisk merget #138 / PR #145:
`7b4cadf143937c52fceb91a775ef4416ab8ac571`. Branch: `codex/139-sok-treffliste`.
Samlet Standards-/Spec-review, separat audit, commit, publisering og kandidat-CI
tilhører orkestratoren. Rapporten er lokale byggbevis, ikke en mergeattest.

## Atferd

Det eksisterende feltet filtrerer fortsatt den valgte kalenderflaten fortløpende.
Knappen «Søk i alle datoer» og Enter i feltet åpner egen treffvisning. Hele det
autoriserte, godkjente års-/kategoriutvalget hentes gjennom `IFristlesing`, med
historikk og uten datogrenser. Trimmet, case-insensitiv delstreng i tittel/notat
beregnes på Blazor-serveren. Bare de gjengitte radene når nettleseren.

Søket har eget sidespor i `Kalendertilstand` og gjenbruker #138s sidestørrelse 25,
stigende dato/sorteringspunkt, tid, tittel og stabil ID. Endret ordnet ID-utvalg
eller Oslo-dato gir side 1 og forklaring. Kalenderens kommende-/historikksider
beholdes hver for seg. Alle datoer og aktive år/kategorier vises uttrykkelig.
Historiske dager merkes «Tidligere frist»; en tentativ måned får dette merket
først når hele måneden er over. Datoformat, klokkeslett, heldag og kvalifikator
gjenbruker den felles visningen.

Radens «Åpne frist» bruker den felles dialogens ferske `HentEnAsync`, kilde og
rollebestemte handlinger. Bortfalt tilgang/sletting gir nøytral melding og fersk
treffliste/årsvalg. Lukket dialog tømmes fortsatt, og når raden er borte flyttes
fokus til treffoverskriften. «Vis i kalenderen» finnes både i raden og dialogen;
den kontrollerer fristen på nytt og bruker dens ferske måned, også for tentative
frister. År/kategorier/søk utvides ikke når fristen har flyttet ut av utvalget.

Søk og treffside bevares ved detaljer og turen til kalenderen. En lagret
kalenderkontekst gir opprinnelig dato/visning ved retur uten valgt treff, også
etter minikalenderbruk i søket. En uttrykkelig treffnavigasjon etablerer derimot
treffets måned som ny kontekst. Retur til kalenderen fokuserer regionen med
kalenderinnholdet; søk og sidehandlinger fokuserer treffoverskriften.

Null treff forklarer at alle datoer er søkt, men at år/kategori avgrenser.
«Endre utvalg», «Tøm søket» og eksisterende «Nullstill filtre» er tilgjengelige.
Ved innlasting/feil fjernes gamle rader og sidetall; feil gir «Prøv igjen».
En lastgenerasjon avviser eldre treff, årsvalg, feil og kalenderhandlinger.
Utlogget autentisering gir tom synlighetskontekst selv om gamle claims finnes.
Leseporten filtrerer synlighet før projeksjon; skjulte/upubliserte data inngår
ikke i rader, tellinger, sider eller årsvalg.

Ved avkryssing av budsjettår flyttes fokus midlertidig til den stabile
«Budsjettår»-gruppen før årsvalgene fjernes for ny tilgangskontroll. Etter
oppdateringen får samme avkryssing fokus hvis den fortsatt er tilgjengelig.
Ved bortfalt år eller lesefeil beholder gruppen fokus. Har brukeren gått videre
under ventingen, overtas ikke fokus. Ingen gamle årsvalg beholdes i DOM.

Ingen ny HTTP-API, datamigrasjon eller søkealgoritme inngår. Eksisterende
godkjenning, POL-valg, budsjettår, eksport og e-postporter bevares.

## Prøver og kontroller

Avtalt offentlig grense er reell Kalender/Blazor over Kestrel og SQLite, med
Edge lokalt. To nye testklasser har **31 tilfeller**, inkludert sju fra reviewrettingen:

| Klasse | Tilfeller | Hovedbevis |
| --- | ---: | --- |
| `KalendersoekTester` | 7 | Tittel/notat, trim/bokstaver, andre måneder/år/historikk, 53 ID-er uten tap/dubletter, 25 per side, fersk rekkefølge/synlighet, filtre, detaljer/kilde/rolle, tentativ måned, retur, tastatur og 1440/390-bredde |
| `KalendersoekKapploepTester` | 24 | Forsinkede treff/år/feil/kalenderhandlinger, retur under innlasting, lastefeil/retry, POL/gruppeskifte/utlogging/sletting, nøytral utilgjengelighet, fersk målnavigasjon, uendrede filtervalg og fokus gjennom årsoppdatering |

Rød → grønn er bevart for manglende treffvisning, mistet kalenderkontekst,
utilgjengelig-fokus og utlogget tilgang i `TestResults/139-red-external/`,
`139-red-context/`, `139-red-races/` og `139-red-focus/`. Nye tester fikk først
korrigert to oppsettsforventninger: demo-persona heter `demo-finfag`, og den
felles kvalifikatoren skrives «medio». Gamle tester og forventninger er uendret.
En full ordinær kjøring fanget dessuten at en fokusreferanse på h1 brøt den
eksisterende HTTP-kontrakten `<h1>Kalender</h1>`; fokus er flyttet til regionen
rundt innholdet, med den gamle forventningen bevart.

Eksakt oppdagelse og tegnsensitiv navnesammenlikning gir nå **1349 ordinære,
480 SQL Server og 409 nettlesertilfeller**, uten tap eller dubletter.
Alle 378 nettlesernavn fra levert #138 er bevart; bare de 31 nye er lagt til.
Alle testfiler fra levert #138 er uendret; de to nye nettleserfilene er eneste
testtillegg. `TestResults/139-focusfix/inventory-comparison.json` kontrollerer
1349/480/402-basisen før reviewrettingen mot den nye kandidaten.
`TestResults/139-verified/inventory-comparison.json` og de tre
tidligere inventarene bevarer den første 378 → 402-kontrollen. Den første parallelle sandboxkommandoen fikk
VSTest-oppstartsfeil og overskrev sitt eget browserinventar; den ble stanset
avgrenset. Den opprinnelig observerte 378-listen er derfor også kontrollert mot
den leverte #138-kandidatens bevarte navneliste, kopiert til
`TestResults/139-baseline/merged-138-browser-names.txt`. Ingen eldre database
er åpnet eller endret under denne kontrollen.

Alle tre låste restore og Release-bygg bruker SDK `10.0.402`. Etter
`--force-evaluate` består restore med aktiv audit (`true/all/low`) uten
advarsler/feil; et innledende forsøk fikk NU1900 fra cache/nettverk.
`-m:1 -nr:false` hindrer det observerte lokale MSBuild-nodeproblemet.
Byggbevis før reviewrettingen ligger i `TestResults/139-verified/`, med `ordinary.trx`,
`browser-targeted.trx`, bygg-/restorelogger, fullnavninventarer,
`inventory-comparison.json`, `baseline-tests.json`, `audit-settings.json`
og `candidate-code-before.json`/`candidate-code-after.json`. Sluttprøvene etter
siste kodeendring består **1349/1349 ordinære og 110/110 relevante
Edge/Kestrel-tilfeller**, uten feil/hopp. Alle 24 nye søketilfeller og de 16
leverte felles detaljregresjonene inngår, sammen med kalender-/års-/tilgangs-,
kilde-, dialog-, Atelier- og EV09-regresjoner. Hele navnsettet er kontrollert
mot det oppdagede inventaret i `verified-results.json`; alle åtte endrede/nye
kode-/CI-filer har null drift gjennom kjøringen. TRX består det uendrede
hemmelighetssøket. To nye 1440/390-skjermbilder er visuelt lest; begge
bredder prøves også eksplisitt for vannrett overløp og tastatur/fokus.
Nettleser-TRX varer 6 minutter og 46 sekunder. Den foreløpige kjøringen i
`TestResults/139-final/` bevares som rødt h1-bevis og tidligere grønt
110-tilfellers nettleserbevis; det er ikke sluttkandidatens attest.

For å gjenta det lokale nettleserutvalget, sett `AARSHJUL_DOTNET` til SDK-en,
`AARSHJUL_BROWSER_CHANNEL=msedge` og `AARSHJUL_BROWSER_EVIDENCE` til en ny
disponibel mappe. Kjør fra repo-roten:

```powershell
$filter = (Get-Content TestResults/139-verified/browser-filter.txt -Raw).Trim()
dotnet test backend/Aarshjul.BrowserTests -c Release --no-build --filter $filter
# Bare den nye funksjonens 31 tilfeller:
dotnet test backend/Aarshjul.BrowserTests -c Release --no-build --filter FullyQualifiedName~Kalendersoek
```

CI-kravet for Chromium er økt fra 378 via 402 til 409 etter eksakt inventar.
SQL-kravet 480, tidsgrenser, aktiv audit, hopp-/utfallskontroll,
hemmelighetssøk og TRX-opplastingsvilkår er uendret.

## Reviewretting 29. oktober 2026

Spec-review av fast kandidat `ca19c09c818e140d5ca874f7f7cd12f8c983eac4`
fant ett P2-avvik: Space på et fokusert årsvalg mistet fokus til BODY mens
årsoppslaget ventet. En ny deterministisk Edge/Kestrel-prøve feilet på dette i
`TestResults/139-focusfix/red.trx`, og samme prøve bestod etter rettingen i
`first-green.trx`. Årsvalgene, rader og tellinger tømmes fortsatt mens ny tilgang
kontrolleres. Fokus håndteres i `Kalenderfilter` og en liten JS-modul; lesing,
autorisering og generasjonsvern er uendret.

Sju nye tilfeller dekker ventende årsoppslag, gjentatt Space, feil i års- og
fristlesing med nytt forsøk, faktisk bortfalt år ved sletting/gruppeskifte/
utlogging og at brukerens videre Tab beholder fokus. Alle sju består i
`focus-green.trx`. Første utviklingskjøring av disse hadde 5/7 bestått: to
Tab-forventninger ble korrigert fordi et annet fortsatt autorisert årsvalg
står før «Velg alle». Produktets fokusutfall var riktig. Ingen gammel test
eller forventning ble endret. Det valgfrie navnefunnets `HarSoeketreff` heter
nå `HarAapnetSoek`, med samme atferd også ved null treff.

Den rettede kandidaten består **1349/1349 ordinære og 117/117 relevante
Edge/Kestrel-tilfeller**, uten feil/hopp, i `ordinary.trx` og `browser.trx` under
`TestResults/139-focusfix/`. Alle de opprinnelige 24 søketilfellene, de sju nye,
16 felles detaljregresjoner og 16 eksisterende navigasjonsregresjoner inngår.
Alle tre låste restore og Release-bygg består med SDK `10.0.402`, null
advarsler/feil og audit `true/all/low`. `verified-results.json` binder de eksakte
navnene og bekrefter uendrede kilde-/runtimefiler gjennom kjøringen;
`source-before.json`/`source-after.json` og `runtime-before.json`/
`runtime-after.json` bevarer SHA256-grunnlaget. TRX består repoets uendrede
hemmelighetskontroll. Endelig dokumentasjon og lokale endringer bindes i
`candidate-files.json`. Tidligere bevis og databaser er bevart.

En innledende inventering i sandkassen hang uten VSTest-output og ble avbrutt.
De faktiske inventarene, byggene og prøvene kjørte deretter utenfor sandkassen.
Den avgrensede rettingen leveres uten individuell review eller commit;
orkestratoren bestiller relevant Spec-verifikasjon og sikkerhetsverifikasjon
før publisering og faktisk kandidat-CI.

## Sluttgjennomgang 29. oktober 2026

Samlet Standards-/Spec-review og separat sikkerhetsaudit er avsluttet på
commit `fb372db7d90b8dd529b02139855cc686120c056b`, tree
`9eab3d292d9bbd0d9a2c6e4fb61711dede6c4046`. Spec-review lukker P2-fokusavviket
og har ingen gjenstående funn; sikkerhetsauditen har ingen funn.
Standards-reviewets eneste gjenstående P3 gjaldt to utdaterte CI-antall i
`docs/development.md`; begge er rettet fra 402 til 409 i denne oppdateringen.
Historiske 402-inventarer er beholdt.

Denne oppdateringen endrer bare to dokumentasjonsfiler. Kode-, CI-, runtime-
og testfiler er uendret fra reviewgrunnlaget. Faktisk SQL Server og hele den
låste Chromium-suiten må fortsatt bestå i kandidat-CI; review og lokale
Edge-prøver er ikke fullført kandidat-CI.

## CI-retting 29. oktober 2026

Første publiserte kandidat i [PR #148](https://github.com/depmartin/Budsjettkalender/pull/148),
head `0210814826805aaf3dc5c4dadbb3663ae1e58661`, bestod 1349 ordinære og
480 ekte SQL Server-tilfeller i [CI 40919516114](https://github.com/depmartin/Budsjettkalender/actions/runs/40919516114).
Nettleserjobben hadde **408/409 bestått**, ingen hopp. Bare
`Aarsvalg_stjeler_ikke_fokus_nar_brukeren_gaar_videre_under_ventingen`
feilet, ved den første fokusforventningen før `Fortsett.TrySetResult`.
Dette var ikke den senere forventningen etter at det forsinkede svaret var frigitt.

Diagnosen i `TestResults/139-ci-diagnosis-1/diagnosis.txt` skiller serverens
`Startet`-signal fra den synlige lasterenderingen. Fokus på årsgruppen beviser
heller ikke at de gamle avkrysningsboksene er fjernet. En umiddelbar Tab kunne
derfor nå det gamle 2028-valget, som deretter ble fjernet og etterlot BODY
med fokus. Den uendrede testen feilet også i rettingsrundens egen `baseline.trx`
på samme opprinnelige linje 114.

Den avgrensede rettingen venter på **null avkrysningsbokser i årsgruppen**
før Tab. Alle eksisterende handlinger, fokusforventninger, resultatkrav,
tidsgrenser og scenarioer er beholdt. Ingen produktkode eller CI-kontroll
endres. Startgrunnlaget er lokal HEAD `0ca3988afdb13aa3c2e13429fa8162d36b9e7f58`,
tree `826612d5a49d790ef3115a41213b8e49ea4d0472`, identisk med den publiserte
kandidatens tree. Nye bevis ligger bare i `TestResults/139-ci-fix-1/`.

Den rettede prøven består **10/10 gjentatte kjøringer**. Samlet består
**63/63 Edge/Kestrel-tilfeller**: alle 31 søketilfeller, 16 felles
detaljregresjoner og 16 navigasjonsregresjoner. Hele den ordinære suiten
består **1349/1349**, uten feil/hopp. Alle tre låste restore og Release-bygg
bruker SDK `10.0.402`, null advarsler/feil og audit `true/all/low`.
Oppdagede navn er eksakt bevart: **1349/480/409**. SQL Server er bygget og
oppdaget lokalt, ikke kjørt i denne rettingsrunden.

`verified-results.json` binder TRX-navnene og hashkontrollen. Alle 460
kilde-/CI-filer og 1368 runtimefiler er uendret gjennom prøvene. Mot
startgrunnlaget er bare den ene testlinjen endret i kildefilene; hvis linjen
fjernes, er testfilens bytes identiske med startgrunnlaget. De nødvendige
Release-byggene regenererte 44 egne DLL/PDB-filer. Dette er journalført i
`runtime-build-changes.json`; generert assemblymetadata angir lokal HEAD.
`source-initial.json`/`runtime-initial.json` bevarer de tidligere runtime-
og kildehashene. `candidate-files.json` binder de to endrede filene.
Repoets uendrede hemmelighetskontroll består alle nye TRX-filer. Tidligere
bevis og databaser er bevart.

Prøven dekker at videre Tab beholder fokus når lastetilstanden er rendret.
Den korte overgangen før denne renderingen er ikke dekket av ventingen;
rettingen beviser ikke at en umiddelbar Tab i dette intervallet beholder
fokus. Det er ikke gjort eller prøvd noen produktendring for dette intervallet.
CI-løkken har ingen ny individuell review eller sikkerhetsaudit. Ny kandidat-CI
må bestå alle tre faktiske porter før autorisert merge; ingen deploy inngår.

## Bevisgrense

SQL Server er bare bygget og oppdaget lokalt; `AARSHJUL_TEST_SQLSERVER` er ikke
tilgjengelig. Faktisk SQL Server og hele den låste Chromium-suiten må bestå i
kandidat-CI. Lokal Edge/SQLite beviser ikke Entra-tilbakekalling, Azure SQL,
Key Vault eller produksjon. Ingen deploy inngår. Andre worktrees, demoer,
historiske SQLite-filer og tidligere bevis er bevart.
