# Manuell kildelenke – lokal akseptanse for #140

Oppgave [#140](https://github.com/depmartin/Budsjettkalender/issues/140), parent
[#136 punkt 4](https://github.com/depmartin/Budsjettkalender/issues/136). Hele begge
sakene og deres tomme kommentarlister er lest live. Kandidaten er bygget fra
`28694c580141b91f9707c616a53ee95f05246e11`, bekreftet lokal HEAD og faktisk
mergecommit for [EV06 / PR #135](https://github.com/depmartin/Budsjettkalender/pull/135).
Ingen umerget integrasjon inngår. Dette er byggerens lokale bevis før samlet
review, separat sikkerhetsaudit, commit og kandidat-CI hos orkestratoren.

## Atferd og avgrensning

Direkte administratoroppretting/redigering får «Kildelenke (valgfritt)» i det
eksisterende Atelier-skjemaet. Feltfeil er koblet til feltet med `aria-invalid`
og `aria-describedby`. Hjelpeteksten forklarer manuell prioritet og at tømming
bruker dokumentlenken hvis den finnes. Køjustering har ikke feltet. Forslag,
maler og dokumentopprinnelse får ingen nye felter eller syntetiske dokumenter.

`Frist.ManuellKildeUrl` er nullable. Direkte skriving, import og leseprojeksjon
bruker én serverregel: absolutt HTTPS på eksakt regjeringen.no eller
www.regjeringen.no, port 443 og ingen brukerinfo. `FristDto.KildeUrl` er fortsatt
den effektive validerte lenken. Synlighet filtreres før projeksjon; eksisterende
trygge lenkeattributter, POL-valg og filnavnskjerming består.

Migrasjonen `20261029092000_ManuellKildelenke` legger bare til kolonnen uten
tilbakefylling. Kolonnen inngår i eksisterende konkurransepredikater. Demo
oppgraderes idempotent også når EV05 ellers ville gitt tidlig retur. Historiske
oppgraderingsprøver bruker en uttrykkelig eldre fristmodell før kolonnen finnes;
andre nåværende tester bruker hele modellen.

JSON versjon 1 får valgfritt `manuellKildeUrl`. Gammel v1 uten feltet betyr null.
Ny app bevarer verdien gjennom serialisering, forhåndsvisning og import.
**Eldre appversjoner kan ikke forventes å bevare det nye feltet ved en retur.**
Forhåndsvisning er sideeffektfri. Hele kandidaten valideres før erstatt-alt-sletting;
en ugyldig senere rad avviser også en gyldig første rad.

Lenkeendring er vanlig innholdsendring, med dagens begrunnede manuelle
vesentlighetsvalg. Den gir ingen ny automatisk kategori. `Fristtilstand` får et
valgfritt siste felt, og eldre hendelses-JSON uten feltet leses fortsatt.
Begge køinnganger bevarer målfristens lenke og eksisterende publiseringsatomikk.
Samtidig lenkeendring ruller hele godkjenningen tilbake. Endring under dagsplanlegging
krever fersk innholdslesing før overlevering.

## Lokale prøver

SDK er den eksakte 10.0.402 fra `global.json`. Alle tre låste restore med aktiv
audit og alle tre Release-bygg bestod med null advarsler/feil. Innledende forsøk
innenfor sandkassen manglet fungerende testvert og NuGet-tilgang; kontrollene ble
deretter kjørt med fungerende lokal prosess-/nettverkstilgang, uten svekket audit.

| Grense | Rødt bevis | Grønt bevis |
| --- | --- | --- |
| IFristskriving → IFristlesing | Ny lenke kom tilbake som null | Opprette, gjenåpne, normalisere, endre og tømme |
| IFristDatautveksling | Eksport mistet lenken | JSON-v1-retur, eldre fil, sideeffektfri forhåndsvisning |
| Dagens manuelle vesentlighet | Ren lenkeendring ble avvist som uendret | Begrunnet markering varsler; umerket lenkeendring varsler ikke; eldre JSON leses |
| SQL-migrasjonsmodell | Ny migrasjon manglet | Nullable additiv Up/Down, token, snapshot og alle tidligere modelloverganger |
| Gammel SQLite-demo | EV05 tidlig retur ga «no such column» | Normal oppstart oppgraderer idempotent uten datatap/historiske hendelser |
| Reell Kestrel/Edge | Administrators kildelenkefelt manglet på begge bredder | Direkte registrering/redigering, forståelig feltfeil, fallback, bevaring etter begge køinnganger, kalender/budsjettår og lenkeattributter |

Røde/grønne TRX ligger i `TestResults/140/140-{red,green}-*.trx`. To midlertidige
navigasjonsfeil i den nye nettleserprøven ble rettet til de eksisterende Liste-
og «Velg budsjettår»-kontrollene; forventningstimeouter er uendret.

Full ordinær sluttkjøring: **1167/1167**, uten feil/hopp,
`TestResults/140/140-ordinary-final.trx`. Den målrettede kjøringen bestod 298/298.
Hele #69s negative URL-matrise brukes også på manuelt lagrede verdier, direkte
skriving, atomisk import og sideeffektfri forhåndsvisning. Hele HTTP-responser
prøves for alle roller, POL, anonymitet og skjult metadata.

Reell Edge/Kestrel: **36/36**, uten feil/hopp, 4 minutter 24 sekunder,
`TestResults/140/140-browser-acceptance.trx`. Utvalget er KildelenkeTester,
DagligEpostTester og FristoverfoeringTester. Det inkluderer mobil/tastatur,
bevarte EV06-forløp og faktisk JSON-overføring. De to nye tilfellene bruker
1440 og 390 piksler og egne disponible databaser/loopback-porter.

Et rent kildearkiv fra base-HEAD er bygget separat for uavhengig inventarsammenlikning.
Alle **1157/310/342** basenavn er bevart eksakt, uten dubletter. Kandidaten har
**1167 ordinære, 316 SQL Server- og 344 nettlesertilfeller**; økningen er 10/6/2.
`TestResults/140/inventory-comparison.json` binder de faktiske navnelistene.
CI-antall er oppdatert til 316/344. Tidsgrenser 15/20/45 minutter, krav om
utførte tester uten hopp, aktiv audit og bare TRX-artefakter er bevart.

## Visuell kontroll og bevisgrenser

Følgende fire sluttbilder er inspisert enkeltvis. Atelier-farger, typografi,
skjemalinjer og detaljkort er bevart. Hjelpetekst og felt passer mobilbredden;
lenken har synlig tastaturfokus. Ingen vannrett dokumentoverflyt ble målt.
Stier er lokale under `TestResults/140/browser-acceptance/`:

- `ecf3ef0b7e1e42fa8f987175c2d52e66/kilde140-admin-1440.png`
- `ecf3ef0b7e1e42fa8f987175c2d52e66/kilde140-detaljer-1440.png`
- `249895501a014be397c59a209e96b78e/kilde140-admin-390.png`
- `249895501a014be397c59a209e96b78e/kilde140-detaljer-390.png`

SQL Server-porten er **bygget/oppdaget, ikke lokalt kjørt**; miljøet har ingen
`AARSHJUL_TEST_SQLSERVER`. Den nye faktiske SQL-migrasjonsprøven og de delte
kontraktene må fullføres i kandidat-CI. 36 lokale Edge-forløp er et avgrenset
utvalg, ikke full Chromium-akseptanse. Alle tre faktiske kandidat-CI-porter,
samlet Standards-/Spec-review og separat sikkerhetsaudit gjenstår.
Lokal SQLite/demo er ikke bevis for Entra, Azure SQL, Key Vault, M365 eller
ekstern e-postlevering. Lenken gir ingen tilgang til dokumentet og attesterer
ikke lenkemålets tilgjengelighet. Ingen deploy inngår.

## Datert CI-retting 29. oktober 2026

Første publiserte kandidat `87459d77a0c3a9bc5d29a331b6c5bba69f847661`
har samme tree `45e2a5a2f0e9fdf92dd4aa63e1f5921e2c9b6d3d` som lokal
`a3bb71088dedfc3cb19858ef4506eb224ec98a86`. Faktisk
[CI 40855047644](https://github.com/depmartin/Budsjettkalender/actions/runs/40855047644)
ga **315 beståtte, 1 feil og 0 hopp av 316 SQL Server-tilfeller**.
`TestResults/140/ci-40855047644/sql-job.log` bevarer det røde beviset:
fasemalens oppgraderingsprøve leste `ManuellKildeUrl` før kolonnen fantes.

Bare førlesingen av det historiske datagrunnlaget bruker nå den eksisterende
`HistoriskFristFabrikk`. Migrasjonsmålet er fortsatt
`20261020135232_ForslagVersjonKonkurransekontroll`; begge etterlesinger bruker
dagens fabrikk etter `MigrateAsync`. Migrasjon, runtimekode, testantall og
CI-vern er uendret.

Ny låst SQL-restore med aktiv audit og Release-bygg består med SDK 10.0.402,
null advarsler og null feil. Ny oppdagelse har **316 unike SQL-testnavn**,
alle identiske med inventaret før rettingen. Loggene er
`TestResults/140/140-ci-sql-fix-{restore,build}.log` og
`TestResults/140/140-ci-sql-fix-inventory.txt`. SQL Server er fortsatt ikke
kjørt lokalt: `AARSHJUL_TEST_SQLSERVER`, Docker og sqlcmd mangler.
Grønt bevis for rettingen krever en ny faktisk kandidat-CI.

## Datert nettleserretting 29. oktober 2026

Neste publiserte head `2a1ed7fda80896f2a2d369d40faa9d0aec83e0d6` har samme
tree `a350cb7fb2420400ee9c4070f0047027121bcec1` som lokal
`1ad72ab2aa7162756ebe8a7e45405b97060f09e6`. Faktisk
[CI 40857118722](https://github.com/depmartin/Budsjettkalender/actions/runs/40857118722)
bestod **1167 ordinære og 316 SQL Server-tilfeller**, mens Chromium ga
**342 beståtte, 2 feil og 0 hopp av 344**. Begge feilene gjelder
`KildelenkeTester.Administrator_lagrer_endrer_fjerner_og_bevarer_manuell_kildelenke_etter_endringsforslag`,
med `juster: False, bredde: 1440` og `juster: True, bredde: 390`.
`TestResults/140/ci-40857118722/browser-job.log` og
`actual-browser/browser.trx` under samme mappe bevarer dette røde beviset.

Feilen oppstod etter klikk på «Liste»: den globale fristlokatoren kunne finne
den gamle månedsvisningens `.kalenderhendelse` eller `.agenda-frist` før
Blazor hadde rendret listevisningen. Enter ble sendt til den utgående knappen,
og detaljdialogen åpnet aldri. Dermed feilet den uendrede href-forventningen
på linje 84; beviset viste ikke en endret eller tapt lenke.
Uendret C#-test reproduserte **1 feil av 2** lokalt i `diagnose-edge-1.trx`.
Separat navigasjonsdiagnose med 75 ms forsinket Blazor-trafikk ga **9 feil av 9**
med global lokator og **9 beståtte av 9** med lokator avgrenset til
«Kalenderliste»: seks forsøk per variant på skrivebordsvisning og tre ved
390 piksler. `diagnose-navigation.cjs` og `minimal-*/results.json` i samme
CI-mappe bevarer tastaturmål og utfall. Forsinkelsen gjelder bare dette
diagnostiske årsaksbeviset.

Bare fristlokatoren umiddelbart etter «Liste» er nå avgrenset med
`GetByRole(AriaRole.Region, new() { Name = "Kalenderliste", Exact = true })`.
Playwright venter dermed på knappen i den valgte visningen før Enter.
Alle 45 Assert-/Expect-kall, alle åtte Enter-handlinger, testnavn og testantall
er bevart. Ingen produksjonskode, timeouter, ventepauser eller CI-vern er endret;
ingen integrasjon fra #138 inngår.

Fersk låst nettleser-restore med aktiv audit og full Release-rebuild av
nettleserprosjektet og dets appreferanser bestod med SDK 10.0.402 og null
advarsler/feil. Fem nye C#-kjøringer av begge tilfellene bestod **10/10**;
deretter bestod alle tre `KildelenkeTester` **3/3**, uten feil/hopp. Dermed er
begge bredder kjørt seks ganger hver etter rettingen. Disse faktiske
Edge/Kestrel-forløpene bruker uendret testvert, egne disponible databaser/porter
og ingen injisert trafikkforsinkelse. Fersk oppdagelse har **344 unike navn**,
eksakt likt både tidligere lokalt inventar og denne CI-kjøringens 344 navn.

Nye bevis ligger i `TestResults/140/ci-browser-fix/`: `restore.log`, `build.log`,
`repeat-{1..5}.{log,trx}`, `kildelenke-all.{log,trx}`, `browser-inventory.txt`,
`inventory-comparison.json` og `locator-change-check.json`. Manifestene og
`verification.json` i samme mappe binder kildefiler, fersk runtime og prøver.
Tidligere røde/grønne og faktiske CI-bevis er bevart.

Dette er en avgrenset CI-retting av en testlokator. De 13 lokale utføringene
erstatter ikke en ny full Chromium-kjøring eller de øvrige kandidat-CI-portene.
Ingen ny samlet review, sikkerhetsaudit, commit, push, GitHub-endring eller
deploy er utført i denne rettingsrunden.


## Datert main-integrasjon 29. oktober 2026

Avsnittene ovenfor er historiske bevis for de revisjonene som navngis der.
Den nødvendige integrasjonen i PR #142s pågående CI-løkke har foreldre
`f9ad140089164f4abcaeb3fe7ded5e6e4c828ec5` (lokal kandidat etter de to
avgrensede CI-rettingene) og faktisk EV07-main
`d3bec41e7a42106e6f2b8bee80de0916c3f65b60` (merget PR #143).
Tidligere publisert head `f5afea1c7cd47c31471699267dd9903d88b6ebc2` har
samme tree `1e0344a8bc84236029e514a9420d35bf3569789e` som første forelder.
`git merge --no-commit origin/main` ga faktisk filoverlapp. Ingen umerget
#138-/liste-/søkeendring inngår. Begge bestillingene, kommentarene og leverte
kontrakter er lest på nytt. Andre checkouts, databaser og prosesser er bevart.

De ti konfliktfilene var CI, FristDatautveksling, Demoskjema,
DagligEpostoppsummering, begge DagligEpostTester, SQLs
InternkalenderLagringsvernTester, EpostoppgraderingBevis, development og status.
Import beholder både felles URL-validering før sletting, v1-feltet, proveniens
og EV07s atomiske publiseringsdiff, slettingshistorikk og sekvens. Demoskjema
krever begge skjemaendringer før tidlig retur. Historiske testmodeller utelater
bare felt som faktisk manglet i sine opprinnelige skjemaer. Begge oppgraderings-
testene og deres opprinnelige navn/forventninger er bevart; forventet samlet
migrasjonshistorikk omfatter nå begge additive migrasjoner.

Funksjonell integrasjonskontroll fanget to konkrete avvik før retting:
`integration-red.trx` har 7 beståtte og 2 feil av 9. Kildelenkemigrasjonens gamle
målmodell beskrev feilaktig bortfall av EV07s historiske tabell/kolonne.
Den kronologisk siste målmodellen er derfor oppdatert til den samlede modellen.
Valg av EV07s nye felles innholdsleser alene mistet også URL-en fra sammenlikningen
før overlevering: eksisterende `Lenkeendring_under_planlegging...` ga Bekreftet
der kontrakten krever EndretFoerOverlevering. Den felles leseren inkluderer nå
manuell URL i både dagens og slettet innhold, og brukes fortsatt ved begge
ferske tilgangs-/innholdskontroller. Begge eksisterende prøver består etter
rettingen. Dette er funksjonell samspillkontroll i CI-løkken.

Begge migrasjons-ID-er beholder timestamp 20261029092000 og uendrede Up/Down.
EFs faktiske sortering på hele ID-en er HistoriskSlettingssynlighet og deretter
ManuellKildelenke. EV07s Designer er bevart; kildelenkens siste Designer og
snapshot beskriver begge leveranser. Modellprøvene sammenlikner alle berørte
Up/Down-overganger og siste snapshot og krever ingen ventende SQL-modellendringer.
Nye SQLite-demo- og SQL-tilfeller starter fra hver opprinnelige gren: den ene
migrasjonens autentiske Up-operasjoner, og i SQL også dens historikkrad, installeres
før normal oppgradering. SQL krever nøyaktig den andre som pending. Begge
oppgraderingsstier prøver idempotens, bevart lenke eller historisk publikum,
hele tidligere datagrunnlaget og fullføring av en ventende legacy-plan.
SQLite-demoene består lokalt; SQL-tilfellene må kjøres faktisk i ny CI.

To nye delte importprøver verifiserer normalisert lenke gjennom forhåndsvisning,
eksport, publisert før-/etter-/slettingsøyeblikksbilde og senere sikker
oppsummering. Ren lenkeendring gir ingen automatisk vesentlig hendelse. En ugyldig
senere importlenke bevarer hele eksporten, hendelser, historiske grupper og
sekvens; den tidligere legitime slettingen kan fortsatt oppsummeres.
EV07s to reelle fil-/e-postforløp er utvidet med faktisk nedlastet og gjenimportert
manuell lenke, leserutens effektive URL og en ren lenkeendring uten ny post,
før eksisterende datoendring, feil/rollback/retry og sletting prøves.

De tidligere CI-rettingene er bevart som eksakt samme Git-blob: SQLs FasemalTester
bruker historisk førlesing, og KildelenkeTester avgrenser tastaturlokatoren til
Kalenderliste. Ingen større timeout eller søvn er innført. Begge migrasjoners
Up/Down, EV07s Designer og daterte akseptanse samt alle 15 kildefunnsfiler fra
leverte #137 er også bevart som opprinnelige Git-blobber. PDF-en har fortsatt
491309 bytes og SHA-256 B902EABED996A1C077D1F242D11B24ECFDE044F898880BEB1ADAD5CEBB9FB60A.
`preserved-sourceblobs.json` binder disse 21 filene; de 54 gamle bevisfilene i
de to tidligere bevismanifestene har null hashdrift.
`scope-preservation.json` kontrollerer dessuten åtte uendrede kontraktfiler:
Kalender/Kalendertilstand er eksakt levert main, mens skjema, URL-regel,
skriving/lesing, vesentlighetsbygger og Fristtilstand er eksakt tidligere #140.

Sluttbevis ligger i `TestResults/140/main-integration/`:

| Kontroll på integrert kandidat | Faktisk lokalt resultat | Bevis |
| --- | --- | --- |
| Tre låste restore med aktiv audit | Bestått, 0 advarsler/feil; true/all/low, ingen suppressions | `restore-final-{ordinary,sql,browser}.log`, `audit-properties-*.json` |
| Tre Release-bygg med SDK 10.0.402 | Bestått, 0 advarsler/feil | `build-final-{ordinary,sql,browser}.log` |
| Målrettet funksjonell integrasjonsprøve | 372/372, 0 feil/hopp | `integration-targeted.trx` |
| Hele ordinærsuiten, kjørt én gang etter siste kildeendring | 1242/1242, 0 feil/hopp | `ordinary-final.trx` |
| Relevante ekte Edge/Kestrel-forløp | 89/89, 0 feil/hopp; 560.815 sekunder | `browser-final.trx`, `browser-filter.txt`, `browser-selected-names.txt` |
| Faktisk inventar | 1242 ordinære / 389 SQL Server / 346 nettleser | `inventory-comparison.json`, `*-names.txt` |
| Kilde-/runtimebinding | 444 backendfiler / 1495 runtimefiler; 0 drift | `backend-stability.json`, `runtime-stability.json` |

Nettleserutvalget er 19 daglige e-postforløp, 3 kildelenkeforløp, 16 fristoverføringer,
5 overføringskappløp, 5 påmeldinger, 4 fasemal-/omstartsforløp, 4 tilgangsforløp,
9 kalenderforløp, 3 kalenderfiltre, 5 kalenderperspektiver og 16 fristnavigasjoner.
Hvert scenario bruker sin egen disponible testdatabase/loopback-port og avslutter
bare sin egen Kestrel. Fire ferske sluttbilder er lest enkeltvis: mobilt kildelenke-
skjema, kildelenkedetaljer på skrivebord og EV07s slettingslenke ved 390/1280 piksler.
Hjelpetekst, tastaturfokus, vanlig innlogget kalender og relevant års-/kategoriutvalg
består uten målt vannrett dokumentoverflyt. Bildene ligger under `browser-final/`.

Et rent arkiv av faktisk main er låst gjenopprettet, Release-bygget og oppdaget
separat innen denne checkoutens TestResults. Sammenlikningen er ordinal og
tegnsensitiv: alle mains 1228/379/344 og den tidligere kandidatens 1167/316/344
navn finnes eksakt i den integrerte kandidaten, uten dubletter. Samlet økning
fra main er 14/10/2. Utover unionen av foreldrenes eksisterende tilfeller er det
4 nye ordinære og 4 nye SQL-tilfeller. CI endrer bare faktiske antall til 389/346;
hele resten av arbeidsflyten er kontrollert lik main etter linjeskiftnormalisering.
15/20/45 minutter, audit, testutføring uten hopp, TRX-krav og artefaktvern består.

Et innledende bygg av den nye delte migrasjonshjelperen viste en manglende
SQLite-utvidelsesmetode i SQL-prosjektet. Hjelperen velger nå leverandør via
ProviderName uten ny pakkeavhengighet. SQL-byggfeilen er bevart i
`build-sql-provider-error.log`; alle senere sluttbygg og den ene fulle
ordinærkjøringen bruker den rettede kilden.

`candidate-files.json`, `evidence-files.json`, `merge-candidate.json` og
`verification.json` er nye sluttmanifest. De binder faktisk staged tree,
Git-blobber, arbeidsfilenes bytes, runtime og lokale bevis. `git write-tree`
lager ingen commit. Orkestratoren skal committe denne mergede kandidaten og
publisere eksakt samme tree før ny full CI.

Ingen lokal AARSHJUL_TEST_SQLSERVER er konfigurert. 389 SQL-tilfeller er bygget
og oppdaget, ikke kjørt; de to virkelige SQL-oppgraderingsstiene er fortsatt
en uttrykkelig ny CI-port. 89 Edge-forløp er et avgrenset utvalg av 346 og
erstatter ikke full Chromium. Alle tre faktiske nye kandidat-CI-jobber må være
grønne. Tidligere samlede review-/auditresultater gjelder de historiske
kandidatene; ingen ny formell review eller sikkerhetsaudit er utført i denne
autoriserte CI-løkken. Lokal demo/SQLite/kontrollert post beviser ikke Entra,
Azure SQL, M365 eller ekstern levering. Ingen commit, push, GitHub-mutasjon,
produksjonsutsending, ekstern tilgangsendring eller deploy er utført her.

## Datert EV08-main-avstemming 29. oktober 2026

Denne nødvendige avstemmingen i PR #142s CI-løkke har fast lokal forelder
`f80e46a3846fe6620166f60de95e4b919aad7c42` og levert EV08-main
`b6a85f2251831eaba1cd69d17249ec57e13578c3`, mergecommit for PR #144 / #118.
Forrige publiserte head `e1f6d72027dd858d4460ac851f888d75c4bb7a2e` og lokal
forelder har begge tree `3d9f7a1befbb9831865fc64549828ee5ca267b7f`.
Faktisk [CI 40868270127](https://github.com/depmartin/Budsjettkalender/actions/runs/40868270127)
er lest på nytt: alle tre jobbene er grønne, med **1242 ordinære, 389 SQL Server-
og 346 Chromium-tilfeller**, null feil/hopp. Jobbloggene bekrefter PR-testmerge
`34113d5` mot gammel EV07-main `d3bec41`. Dette er historisk grønt bevis for
forrige kandidat og base; det attesterer ikke denne EV08-avstemmingen.

`git merge --no-commit origin/main` ga nøyaktig tre tekstkonflikter:
`.github/workflows/ci.yml`, `docs/development.md` og `docs/status.md`. CI beholder
hele leverte main-arbeidsflyten med bare faktisk antall endret fra 432/348 til
442/350. Dokumentasjonen beholder begge leveransene, skiller historiske bevis
fra dagens kandidat og registrerer EV08 som merget. Ingen umerget gren,
#138-/liste-/søkeendring eller nytt funksjonsvalg inngår.

EV08 endrer ikke AppDbContext, FristDatautveksling, migrasjoner, demaskjema eller
den daglige senderen. Alle **300 produktfiler** er eksakte Git-blobber fra én
levert forelder: **295 er uendret fra forrige #140-kandidat**, og de fem øvrige
er eksakt EV08-main (abonnementsgrense/-tjeneste, HTTP, kretskommando og skjema).
Manuell URL, før-/etter-/slettingsøyeblikksbilder, importvalidering før sletting,
EV07s atomiske hendelser, historiske publikum og legacy-planer er derfor bevart.
Begge migrasjoners ID, Up/Down, Designer og siste snapshot er uendret; alle
35 migrasjonsfiler er eksakt forrige forelder. Begge minimale CI-testrettinger
er også bevart som samme blob. De gamle kildefunnene og tidligere bevis er bevart.

Den automatisk flettede DagligEpostKontrakt er kontrollert som eksakt tidligere
fil pluss hele EV08s uendrede testblokk. EpostabonnementKontrakt er eksakt levert
EV08. Dermed prøves både URL-innholdets ferskhetskontroll og adressebyttets
stans under begge sistekontroller, ny bekreftelse, fersk mottakeradresse,
commitrekkefølge for Ny/Endret/Slettet, beholdte utvalg og UnderSending/Uavklart.
Det er ingen ny runtimekonflikt eller lokal produktendring utover levert main.

Fersk lokal verifikasjon bruker SDK 10.0.402 og ligger under
`TestResults/140/ev08-integration/`:

| Kontroll på denne integrasjonen | Faktisk resultat | Bevis |
| --- | --- | --- |
| Tre låste restore med aktiv audit | Bestått, 0 advarsler/feil; true/all/low, ingen suppressions | `restore-{ordinary,sql,browser}.log`, `audit-properties-*.json` |
| Tre Release-bygg | Bestått, 0 advarsler/feil | `build-{ordinary,sql,browser}.log` |
| Målrettet funksjonelt samspill | **545/545**, 0 feil/hopp | `integration-targeted.trx`, `integration-filter.txt` |
| Hele ordinærsuiten, kjørt én gang | **1302/1302**, 0 feil/hopp | `ordinary-final.trx` |
| Reelle Edge/Kestrel-forløp | **31/31**, 0 feil/hopp, omtrent 4 minutter 22 sekunder | `browser-final.trx`, `browser-filter.txt`, `browser-selected-names.txt` |
| Faktisk testoppdagelse | **1302 ordinære / 442 SQL Server / 350 nettleser** | `inventory-comparison.json`, `*-names.txt` |
| Kilde-/runtimebinding | **445 backendfiler / 1518 runtimefiler**, 0 hashdrift under prøvene | `backend-stability.json`, `runtime-stability.json` |

De 31 nettleserforløpene omfatter alle 23 DagligEpostTester (inklusive EV07s
faktisk nedlastede/gjenimporterte manuelle URL, endring/sletting, feil/rollback/retry,
EV06s køinnganger og EV08s fire adressebytter), fem EpostabonnementTester og tre
KildelenkeTester. Hvert scenario bruker egen disponibel database/loopback-port.
Utvalget følger den faktiske e-post-/adresseavhengigheten; brede, urelaterte
kalender-/liste-/søke-/fasemalflater er ikke gjentatt. De øvrige tilfellene hører
til full ny Chromium-CI. Eksisterende forventninger, timeouter og testnavn er uendret.

Et rent arkiv av fast EV08-main er låst gjenopprettet, Release-bygget og oppdaget
separat inne i denne checkoutens TestResults. Ordinal og tegnsensitiv sammenlikning
viser at alle **1288/432/348** navn fra main og alle **1242/389/346** navn fra
forrige kandidat er bevart eksakt. Samlet inventar er nøyaktig unionen av begge
foreldre, uten manglende navn, dubletter eller nye tilfeller utover unionen.
CI beholder 15/20/45 minutter, audit, krav om faktisk utføring uten hopp,
TRX-kontroll og bare TRX som opplastet artefakt. `ci-invariants.json` krever at
hele resten av arbeidsflyten er identisk med levert main etter linjeskiftnormalisering.
De tre tidligere bevismanifestenes **21 + 33 + 217 filer** har null hashdrift.

`candidate-files.json`, `merge-candidate.json`, `verification.json` og
`evidence-files.json` binder staged tree, alle Git-blobber, arbeidsfilenes bytes,
runtime, faktisk inventar og prøver. `product-scope-preservation.json`,
`preserved-sourceblobs.json` og `shared-contract-preservation.json` binder
foreldrebevaringen. Historisk grønn CI er bevart i `historical-ci.json` og
`historical-ci-40868270127/`. `git write-tree` lager ingen commit.

SQL Server er bygget og alle 442 tilfeller oppdaget, **ikke lokalt kjørt**;
ingen AARSHJUL_TEST_SQLSERVER er konfigurert. 31 lokale Edge-forløp erstatter
ikke full Chromium med 350 tilfeller. Orkestratoren må committe og publisere
eksakt staged tree og innhente alle tre nye faktiske grønne CI-jobber på dette
grunnlaget. Ingen ny formell review eller sikkerhetsaudit er utført, heller
ikke indirekte via implement. Andre checkouts, prosesser og databaser er urørt.
Ingen commit, push, GitHub-mutasjon, produksjonsutsending, ekstern tilgangsendring
eller deploy er utført. Lokal SQLite/Edge/kontrollert post beviser ikke
Entra, Azure SQL, M365 eller ekstern levering.
