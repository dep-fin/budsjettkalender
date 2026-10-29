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
