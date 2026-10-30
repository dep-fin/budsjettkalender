# #150 – resultatbarrierer i nettleserprøvene

Lokal rettingskandidat 30. oktober 2026, fra HEAD
`d4c8a323b483d9b0cedcc749ac9e61e4afc1e9f0`, tree
`515aabf3ca3ad5f6743b7c388a69fa5870091537`.
[Oppgaven](https://github.com/depmartin/Budsjettkalender/issues/150) og dens
aksept gjelder fortsatt. Dette dokumentet attesterer lokal diagnose og retting;
samlet review, separat sikkerhetsaudit, kandidat-CI og merge gjenstår.
Den første kørettingen er bevart i `d44a00714b11d66a9fe6199904ea2661e8b41f6e`,
tree `3f299203ff6d8252eebcbdc80e4d7cef1dc8bb1a`. Tillegget nedenfor retter en
separat, eksisterende synkroniseringsfeil som ble avdekket i valideringsporten.
Tidligere review/audit av køkandidaten dekker ikke hele denne utvidede kandidaten.

## Årsak og avgrensning

`SelectOptionAsync` venter på nettleserens select-handling, men ikke på Blazors
asynkrone kølesing og siste resultat-render. Målforslaget «AK juster» finnes både
før og etter siste kategoriutvalg. Dets tilstedeværelse alene kan derfor ikke
bevise at siste filter er ferdig. `Godkjenningsko.LastAsync` lar gamle kort stå
mens lesingen venter, og tømmer deretter `_tilstand` når siste lesing fullfører.
Den gamle testen kunne klikke Godkjenn mens denne fullføringen fortsatt pågikk.

Diagnosen på uendrede produksjonskilder viste to kontrollerte rekkefølger:

1. Godkjenn åpner faktisk bekreftelsen; deretter fullfører filteret og fjerner
   bekreftelsen ved tilstandstømming. Avbryt-klikket får 10000 ms-timeout.
2. Filteret fullfører på serveren, mens originale serverrammer holdes før
   nettleseren. Et klikk i gammel DOM treffer en gammel captured korttilstand.
   Etter rammeleveransen mangler Avbryt, med samme timeout.

Historiske Linux-logger inneholder ikke hendelses-/rendertelemetri. De kan ikke
avgjøre hvilken av disse rekkefølgene som inntraff i hvert gammelt forsøk.
Påvist mekanisme er ikke en rekonstruksjon av historiske millisekunder eller en
påstand om at runneren bare var treg. Årslesing/Space har ingen påvist felles årsak.

## Rettet prøve

Kørettingen endrer `backend/Aarshjul.BrowserTests/ForslagTester.cs`.
`Send` får en valgfri kategori til syntetisk oppsett; eksisterende kall beholder
samme feltvalg. Køprøven sender også «AK filterkontroll», Opphav=Bruker,
Type=NyFrist og Kategori=Gulbok. Tittelen inngår ikke i målets `AK juster`-prefix.

Før siste Kategori=Budsjett bevises kontrollkortets tilstedeværelse. Etter valget
venter prøven på kontrollkortets bortfall og målkortets tilstedeværelse før
Godkjenn. Bortfallet er et synlig resultat av siste kategoriutvalg. Testnavn,
eksisterende assertions, Avbryt, justering, gammel andre fane og nøyaktig én
målpublisering beholdes. Ingen søvn, retry eller timeoutøkning er lagt til.

Produksjonskomponenten endres ikke. Den eksisterende annulleringen av korttilstand
ved filterfullføring består; en ny produktgaranti for handlinger under pågående
filterlasting er ikke del av denne sekvensielle prøverettingen.

## Rød → grønn på den faktiske originalmetoden

Nye lokale bevis ligger i `TestResults/issue150-build/`; tidligere diagnosebevis
ligger urørt i `TestResults/issue150-diagnosis/`. Gamle SQLite-filer er ikke åpnet.
Den nye harnessen linker den faktiske `ForslagTester.cs`, uten omskriving av
testmetoden. Den bruker ekte Kestrel, Blazor, Playwright/Edge, nye SQLite-data og
faktisk `GodkjenningskoTjeneste`, med eksisterende testautentisering.

En dekoratør holder retur fra siste ekte kategori-lesing. Lokale adaptere rundt
offentlige Playwright-operasjoner styrer kun rekkefølgen: Uten resultatbarrieren
får Godkjenn åpne en faktisk synlig bekreftelse før filteret slippes og fjerner den.
Med barrieren holdes lesingen til den faktiske kontrollkort-assertionen venter;
kontrollkortet finnes da fortsatt, før ekte retur og ekte DOM-bortfall tillates.
Alle operasjoner og assertions videresendes til Playwright. Ingen DOM, resultater,
godkjenninger eller publiseringer fabrikkeres. Samme adapterkilde brukes før og
etter rettingen. Dette er kontrollert diagnose, ikke den ordinære browserverten.

| Bevismappe | Faktisk utført | Resultat |
| --- | --- | --- |
| `original-red-02` | Uendret originalmetode | 1 feil, 0 bestått/hopp; original linje 189, Avbryt-timeout 10000 ms |
| `original-green-01` | Rettet originalmetode | 1 bestått, 0 feil/hopp; hele køforløpet fullført |
| `barrier-mutant-red` | Kandidatkopi med bare siste kontrollkort-barriere fjernet | 1 feil, 0 bestått/hopp; samme Avbryt-timeout |

Mutasjonen beholder begge forslag, kontrollkortets tilstedeværelse før valget og
målkort-assertionen. Den viser at ekstra oppsett og et fortsatt synlig målkort
alene ikke forklarer grønt resultat. Originalens eksisterende HTTP-assertions i
den grønne kjøringen beviser upublisert forslag etter Avbryt, «allerede behandlet»
i gammel andre fane etter justering og nøyaktig én målpublisering.

Før-kildens SHA-256 er
`00e3ebaed5e89a748a6e9c5fc6c6e64e9bdadcbb66ac98348effe6f44388b6a8`;
kandidatens er
`028bc80d21141a3a72d90a9a044bfa959f89b1fe2165550eb4b3c4d011384282`.
Adapterkilden har samme SHA-256 i alle tre kjøringer:
`6b81cfd280f037f6a1889b906efdc4d80e984f74e2c2b65a756f0727ab5e6f4d`.
Hver bevismappe har kildekopier, TRX, stage-/count-logg og `run.json` med kommando,
tidspunkt, base-HEAD/tree og kilde-/assemblyhash.

`original-red-01` ble avbrutt fordi sandboxens testhost ikke koblet til innen
uendret 90-sekundersgrense; den utførte ingen test og er ikke rødt årsaksbevis.
Første sandbox-restore ga NU1900 fordi NuGet-audit ikke var tilgjengelig. Senere
låst restore med nettverk og aktiv audit, samt bygg, passerte uten advarsler/feil.
Begge mellomresultatene er bevart; ingen grense eller auditregel er svekket.

## Første kandidats regresjoner og bredt tilleggsfunn

Se `TestResults/issue150-build/validation-runs.json` og `ordinary-run.json` for
eksakte kommandoer og kildebinding. Låste restore og Release-bygg for ordinary og
browser passerer uten advarsler/feil, med aktiv audit og SDK 10.0.402.
`DOTNET_ROOT`, `PATH` og `AARSHJUL_DOTNET` settes også for barneprosesser.

Browser-discovery finner alle 418 navn, eksakt lik navneunionen i den tidligere
TRX-en fra `ev01-ci-40966411644/diagnosis-browser/browser.trx`. Alle eksisterende
assertion-linjer i `ForslagTester.cs` består; tre nye forventninger er lagt til.

Det brede browserfilteret traff også `KalenderforslagTester` fordi navnet
inneholder `ForslagTester`. Kjøringen utførte 47 tilfeller: 46 bestått, 1 feil,
0 hopp. Alle 39 tilfeller i de avtalte klassene/metodene bestod: alle fire i
`ForslagTester`, alle fem i `GruppeadministrasjonTester`, alle 24 i
`KalendersoekKapploepTester` (inkludert alle tre bortfalt-år-/Space-variantene)
og alle seks EV06-tilfeller som bruker `Send`-hjelperen.

Det ekstra tilfellet
`KalenderforslagTester.Tentativ_maaned_med_alle_kvalifikatorer_bevarer_presisjon_etter_godkjenning`
feilet i `KontrollerVisninger`, linje 367: «UX81 tentativ Primo» manglet i
Tidslinje for budsjettår 2031. Denne metoden bruker ikke `Send`-hjelperen, og dens
kilder var uendret i denne kjøringen. Den etterfølgende, separate diagnosen og
rettingen står nedenfor. Historisk TRX og scenario-data er bevart; denne
kjøringen forblir **46/47** og skal ikke presenteres som samlet grønn.
`Run-Validation.ps1` stoppet etter dette avviket; ordinary utføres separat med
samme kandidat og egen kommandokvittering.

Den første kandidatens separate fulle ordinary-kjøring bestod **1449/1449**,
uten feil/hopp. Alle fem årsaks-/regresjons-/ordinary-TRX-er har korrekte
tellere og unike test-/execution-ID-er. Lokal hemmelighetskontroll passerer med
PowerShell-ekvivalenten av begge mønstrene fra uendret CI-scanner; faktisk
Python-scanner skal fortsatt kjøres i kandidat-CI. `proof.json` og
`hashmanifest.json` binder resultater og kildekopier. Sammenligning med diagnosens
506 ikke-Markdown-filer viser bare den bestilte testfilendringen; de øvrige 505,
inkludert produksjonskilder, historiske migrasjoner og CI-vern, er hashbevart.

## Separat resultatbarriere før årsvisningen leses

Den ferske diagnosen i `TestResults/issue150-extra-diagnosis/DIAGNOSIS.md` og
`proof.json` påviste at `KalenderforslagTester.KontrollerVisninger` kunne ta
gruppenes `AllAsync`-øyeblikksbilde før årsresultatet var rendret.
`aria-pressed=true` angir valgt modus mens årsdata fortsatt lastes. Ved andre
tentative frist i samme måned blir begge fristene lagt i en lukket gruppe.
Et tomt snapshot åpner ingen gruppe; Primo finnes senere under `hidden`, mens
den uendrede tilgjengelighetsbaserte fristassertionen fortsatt finner null.

To kontrollerte røde kjøringer av hele originalmetoden, en grønn kontroll med
tidlig fullføring og minimale én-/to-fristkontroller skilte mekanismen fra
publisering, presisjon og krysstestdata. Dette er en tilstrekkelig årsak til
nøyaktig det historiske avviket, og den best støttede forklaringen. Den gamle
46/47-kjøringen manglet hendelsesinstrumentering; dens konkrete millisekundrekkefølge
kan ikke rekonstrueres. Det er ingen dokumentert produktregresjon eller felles
rotårsak med køfeilen.

Den eneste nye kodeendringen etter `d44a007` er én synlighetsassertion med en
forklarende kommentar i `KalenderforslagTester.cs`: riktig navngitt resultatregion
for visning og budsjettår må være synlig før `AllAsync`. Produksjonsflaten rendrer
denne regionen først etter ferdig årslesing. Alle gamle gruppesjekker, fristantall,
ID-er, detaljer og kvalifikatorforventninger beholdes. Ingen produksjonskode,
tidslinjefunksjon, `IncludeHidden`-forretningsassertion, søvn, retry eller timeout
endres. Den tidligere kørettingen er hashbevart.

Nye bevis ligger bare i `TestResults/issue150-validation-fix/`; eldre bevis og
SQLite-filer er urørt. Harnessen linker hele den faktiske testfilen og bruker ekte
Kestrel, Blazor, Playwright/Edge og synlighetsfiltrert lesing på nye SQLite
in-memory-data. Samme adapterkilde holder den reelle årslesingen ved begge utfall:
uten barriere returnerer ekte `AllAsync` null grupper før årsresultatet frigis;
med barriere starter den ekte synlighetsassertionen mens regionen mangler og
lesingen fortsatt holdes, før reell fullføring tillates. Ingen DOM eller
assertionresultater fabrikkeres.

| Bevismappe under `issue150-validation-fix` | Faktisk utført | Resultat |
| --- | --- | --- |
| `original-red` | Hele uendrede originalmetoden før retting | 1 feil, 0 bestått/hopp; Primo-locator, original linje 367, 5000 ms |
| `candidate-green` | Hele rettede originalmetoden med samme kontrollerte årslesing | 1 bestått, 0 feil/hopp; alle fire kvalifikatorer og visningskontroller |
| `barrier-mutant-red` | Kandidatkopi med bare den nye synlighetsassertionen fjernet | 1 feil, 0 bestått/hopp; samme Primo-locator og 5000 ms |

Grønn hendelseslogg viser regionantall 0 og `aria-pressed=true`, en faktisk
ventende synlighetsassertion, frigitt årsresultat, regionantall 1 og deretter
gruppesnapshot med én gruppe. Den eksisterende koden åpner gruppen og fullfører
resten av originaltesten. Rød mutasjon viser at akkurat resultatbarrieren er
nødvendig under den kontrollerte rekkefølgen.

Originalkildens SHA-256 er
`6d8847af2c509806732c33fc664a3818285312faa32fd15d6144b4c21a6168e8`;
rettet kilde er `4dd1f78f4843b99491f3b7995a2bb15b5e79309e0a145e6005747e153226d881`.
Adapter-SHA-256 er identisk i rød, grønn og mutasjon:
`d47b3281b6dcc12c0d9b0c8b3482a102790c3c78d0068df672255d71115b3da4`.
Kommandoer, tider, kilde-/assemblyhash og opprinnelig HEAD/tree står i hver
`run.json`; faktiske kildekopier, TRX og hendelseslogger er bevart.

## Ny kandidats regresjoner og gjenstående porter

Full ordinary er kjørt på nytt etter siste kodeendring og består **1449/1449**,
uten feil/hopp. Dette er en ny utføring, adskilt fra første kandidats resultat.
Kommandoer og kildehash står i `issue150-validation-fix/validation-runs.json`.
Låste restore og Release-bygg for ordinary og browser har null advarsler/feil,
med aktiv audit og SDK 10.0.402 også for barneprosesser.

En ny målrettet kjøring i ordinær Windows/Edge-browserverten består **90/90**,
uten feil/hopp og uten diagnoseadapter. Den omfatter de samme 47 tilfellene som
tidligere ga 46/47, samt alle 43 tilfeller i `ArbeidsaarTester` (3),
`DokumentflytTester` (6) og `MalproevingTester` (34). Disse tre klassene bruker
også den endrede visningshjelperen, og begrunner det utvidede regresjonsutvalget.
De opprinnelige 39 kø-/samspilltilfellene og alle åtte kalenderforslagstilfellene
består i denne nye utføringen. Den gamle kjøringen forblir uendret 46/47.

Uavhengig discovery og de nye resultatene bevarer alle 1449 ordinary-navn;
browser-discovery bevarer alle 418 navn fra den tidligere kandidatens discovery.
Ingen testnavn er lagt til, fjernet eller endret. Alle gamle assertion-linjer
beholdes; visningshjelperen får én ny assertion. Av 506 ikke-Markdown-filer fra
main-grunnlaget er 504 hashbevart; bare de to navngitte browserprøvefilene
skiller seg. Den tidligere kørettingen har uendret SHA-256
`028bc80d21141a3a72d90a9a044bfa959f89b1fe2165550eb4b3c4d011384282`.
SQL Server-kilder, historiske migrasjoner, produksjonskode og CI-vern er dermed
uendret. `proof.json` og `hashmanifest.json` samler bindingene, resultatantall,
unike test-/execution-ID-er og lokal kontroll med begge CI-hemmelighetsmønstrene.
Faktisk Python-scanner og ferskt SQL-/Chromium-resultat gjenstår i kandidat-CI.

SQL Server og full Linux/Chromium skal fortsatt utføres i ny kandidat-CI.
Eksisterende 1449/539/418-navneunion, historiske migrasjoner, serverautorisasjon,
synlighet, 15/20/45-minuttersgrenser, aktiv audit, hemmelighetskontroll og
bare-TRX-opplasting beholdes. Lokale rålogger/databaser lastes ikke opp.
Windows/Edge-bevisene erstatter ingen av de gjenstående portene.

Ingen deploy, ekstern prøvepost, tilgangsendring eller arbeid på andre tickets
inngår. #110/#136 og #122–#124 endres ikke av denne leveransen.
