# Utvikling

EV10 / #120 videreføres i eksisterende [PR #147](https://github.com/depmartin/Budsjettkalender/pull/147)
med levert main `ce439c2ee55d6d79095d0b488768b13823f36531` integrert.
Den tidligere EV10-kandidaten bestod 1403/510/359 i
[CI 40901718289](https://github.com/depmartin/Budsjettkalender/actions/runs/40901718289)
på EV09-basen; dette er historisk bevis og dekker ikke den nye integrasjonen.
Ny discovery bekrefter eksakt union: **1404 ordinære, 510 SQL Server og 414 nettlesertilfeller**.
Alle navn fra begge foreldre er bevart uten tap, dubletter eller navn utenfor unionen.
Sluttprøvene består 1404/1404 ordinære og 132/132 relevante Edge/Kestrel-tilfeller,
uten feil/hopp. Den første nettleserkjøringens ene EV08-avvik er bevart og
beskrevet sammen med etterprøvingen i
[EV10-akseptansen](acceptance/2026-10-29-epost-ev10.md#integrasjon-med-levert-kalenderliste-og-søk).
CI krever nøyaktig **510 SQL- og 414 Chromium-resultater** uten feil/hopp.
Hemmelighetssøk før TRX-opplasting, opplastingsvilkår, aktiv audit og
tidsgrenser 15/20/45 minutter består. Ny full kandidat-CI gjenstår.

#139 er levert i [PR #148](https://github.com/depmartin/Budsjettkalender/pull/148)
som denne main. Kalenderens lokale tekstfilter beholdes; «Søk i alle datoer»/Enter
åpner egen treffliste med historikk, eksplisitte år/kategorier, 25 per side,
ferske detaljer og månedsnavigasjon. Søk/side og opprinnelig kalenderkontekst
bevares. Årsavkryssing beholder fokus gjennom venting og bortfalt år uten å
beholde gamle årsvalg. [Slutt-CI 40927726909](https://github.com/depmartin/Budsjettkalender/actions/runs/40927726909)
bestod 1349 ordinære, 480 SQL Server og 409 Chromium. Dette er forelderens bevis.
[Søkeakseptansen](acceptance/2026-10-29-kalendersoek.md) bevarer de daterte
lokale prøvene og bevisgrensene. Ingen API eller migrasjon inngikk i #139.

Levert #138s [slutt-CI 40901062835](https://github.com/depmartin/Budsjettkalender/actions/runs/40901062835)
bestod 1349 ordinære, 480 SQL Server og 378 Chromium før bekreftet merge.
[EV09-avstemmingen](acceptance/2026-10-29-kommende-liste.md#nodvendig-ev09-mainintegrasjon-i-ci-lokken-29-oktober-2026)
bevarer tidligere revisjonsbundne prøver, review, rødt CI-bevis og de tre
historiske SQLite-filunntakene. Disse filene er ikke åpnet under #139.
Ingen deploy inngår.

EV08 bruker eksisterende abonnementslagring uten egen modellendring/migrasjon.
Fra `/epostvarsling` kan Aktiv/Venter bytte egen adresse og Avmeldt uttrykkelig
bytte og melde seg på igjen med beholdte varselvalg. Samme adresse er uendret.
JSON-POST `/api/epostabonnement/adresse` tar `adresse`, `egenadresseSamtykke` og
`versjon`; eieren hentes fra innlogging. Ingen type-/filtervalg skrives samtidig.
Ett minutts pause og høyst fem bekreftelser per time gjelder på tvers av bytter.
Fristvarslingen stanser frem til ny bekreftelse erstatter type-, utvalgs- og
livssyklusgrenser. Sendefeil bevarer ny ventende adresse. Fersk adresse, versjon
og tilgang kontrolleres før overlevering; UnderSending/Uavklart ryddes ikke.
Se [EV08-akseptansen](acceptance/2026-10-29-epost-ev08.md) og ADR-0010.

`20261029092000_ManuellKildelenke` legger bare til nullable `Frister.ManuellKildeUrl`,
uten tilbakefylling. Kolonnen inngår i fristens eksisterende konkurransekontroll.
Tom manuell lenke bruker tilknyttet dokuments tillatte lenke. Direkte administrator-
oppretting/redigering, import og lesing bruker samme HTTPS-/regjeringen.no-regel.
Køjustering har ikke feltet; begge godkjenningsinnganger bevarer lenken.
JSON-eksportens versjon **1** får valgfritt `manuellKildeUrl`. Gammel v1 uten
feltet betyr null. Ny app bevarer feltet; eldre appversjoner kan ikke forventes
å bevare det ved en retur. Hele importkandidaten valideres før sletting, også
ved forhåndsvisning. Ren lenkeendring gir ingen automatisk vesentlig hendelse;
dagens begrunnede administratorvalg gjelder fortsatt.

EV07s `20261029092000_HistoriskSlettingssynlighet` bevarer historiske gruppekoder
per eksplisitt slettingshendelse med FK til hendelsen, uten FK til dagens frist
eller grupperegister. Livssyklussekvensen bevarer opprinnelig aktivering gjennom
senere typevalg. Eksisterende aktiveringsrader får nåværende sekvens som grense;
type-/utvalgsgrenser, planer og hendelser beholdes uten historisk tilbakefylling.
Import sammenlikner reell publisert før-/ettertilstand per identitet atomisk med
hendelser, historisk synlighet og sekvens. Identisk gjenimport og korrektur
varsler ikke. Sletting bruker fersk mottakertilgang og siste publiserte synlighet.
Se [EV07-akseptansen](acceptance/2026-10-29-epost-ev07.md) og ADR-0010.

Begge migrasjons-ID-er beholdes med samme timestamp. EF ordner på hele ID-en:
`HistoriskSlettingssynlighet` før `ManuellKildelenke`. Førstes målmodell beskriver
EV07; kildelenkens målmodell beskriver begge leveranser. EV09s senere
`20261029143000_Avmeldingslenke` og slutt-snapshot beskriver i tillegg
avmeldingshash/-sekvens med indeks. Alle leverte og EV09s faktiske
Up/Down er uendret. SQL-prøvene starter både med bare levert EV07 og med bare den
opprinnelige kildelenkemigrasjonen registrert i migrasjonshistorikken, og krever
at normal `MigrateAsync` installerer den manglende leverte migrasjonen og EV09
idempotent. I EV09s opprinnelige byggegrunnlag var bare Avmeldingslenke ventende;
gjeldende EV10-kandidat legger til Epostadministratorstans.
Disse tilfellene krever ekte SQL Server i CI når lokal testvariabel mangler.
Normal `Demoskjema.SikreAsync` oppgraderer EV02–EV05 og hver enkelt gren atomisk og
idempotent. Tidlig retur krever begge leverte skjemaendringer og begge EV09-
kolonner; ny demo bruker EnsureCreated. En egen main→EV09-prøve bevarer faktiske
kildelenker og ventende post med en fungerende snever avmeldingsrett.
Demo-/SQL-prøvene bevarer lenke eller historisk publikum, hele datagrunnlaget og
en ventende legacy-plan. Modellprøvene sammenlikner faktisk Up/Down og målmodeller
og krever ingen ventende SQL Server-modellendringer.

Migrasjonen `20261028193853_Abonnementsutvalg` legger til nullable `UtvalgJson` i
e-postabonnementet. Produksjon bruker vanlig EF-migrasjon; `Demoskjema.SikreAsync`
oppgraderer eksisterende EV02-, EV03- og EV04-demoer atomisk og idempotent. Ingen
abonnementer eller historiske hendelser opprettes av oppgraderingen. Eldre demoer
enn EV02 må fortsatt håndteres etter veiledningen nedenfor.

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
For samlet Atelier-visning på Windows finnes også
`pwsh -NoProfile -File ./scripts/Start-AtelierDemo.ps1`. Helperen bruker SDK fra
`global.json` via PATH eller `AARSHJUL_DOTNET`, kopierte publiserte Release-filer,
en ny disponibel database og bare loopback på 5093. Den endrer ikke gamle demofiler
eller prosessene på 5076/5077/5088. Portabilitet, stopp og avgrensning står i
[overleveringen](acceptance/2026-10-28-atelier-overlevering.md#lokal-demo-med-ny-database).
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

EV03 oppgraderer en eksisterende EV02-demo additivt ved normal oppstart gjennom
`Demoskjema.SikreAsync`. De fem nye tabellene opprettes samlet i en transaksjon,
uten historiske hendelser. Eldre demoer uten EV02-skjema avvises med beholdte data;
bruk en ny disponibel database, og bevar eksisterende filer. SQL Server bruker
`20261028133321_DagligEpostoppsummering` og
`20261028142358_OrdnetEpostaktivering` i den vanlige migrasjonskjeden.
Migrasjonene følger etter levert UX16s UnikeInternrunder. Begge EV03-målmodellene
er generert på nytt med hele den kronologiske modellen; faktiske Up/Down er uendret.
Den lokale oppgraderingsprøven starter fra UX16-/EV02-modellen, bevarer aktive og
ventende abonnementer og internkalenderens data/unikhet/versjonsvern, og prøver ny
bekreftelse og publisering uten historisk varslingsflom. SQL-prosjektet har både
EV02→UX16→EV03 og en særskilt oppgradering fra levert UX16; faktisk SQL-kjøring
kreves i ny EV03-kandidat-CI.

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

## Kontrollert daglig oppsummering (EV03)

Vanlig demo har automatisk dagsjobb etter skjemaoppstart. `DagligEpost:Klokkeslett`
har standard `08:00`, hver dag i Europe/Oslo. TimeProvider-timeren vekker jobben
hvert minutt; operasjonen avgjør om tidspunktet er nådd. Ved vårens ugyldige lokale
klokkeslett brukes første gyldige minutt etter gapet. Ved høstens overlapp brukes
første forekomst, og samme lokale dato gir fortsatt høyst én oppsummering.
Manglende Oslo-sonedata stanser kjøring; det brukes ingen UTC-reserve for utsending.

Sett `DagligEpost:OffentligBasisadresse` til appens betrodde HTTPS-adresse.
Demoprofilen setter `http://localhost:5014`; HTTP godtas bare på lokal loopback i
demo/Testing. Nettleserprøvene angir selv sin disponible loopback-adresse. Lenken
går til `/kalender` og krever vanlig innlogging; ingen feedhemmelighet brukes.

For manuell, gjentakbar demonstrasjon, sett `DagligEpost__KontrollertKjoring=true`
i en disponibel demo. Dette registrerer en kontrollert klokke på 1. januar 2030
kl. 06.00 UTC og stanser den automatiske jobben. Meld på og bekreft en persona.
Logg inn som demo-administrator og åpne `/demo/epostkjoring`. Sett klokken frem
før direkte publisering, publiser frister, sett den til minst 07.00 UTC (08.00 norsk
vintertid), og kjør. «Utsett kontrollert transport» lar en varig plan vente uten
overlevering. Logg inn som abonnenten og åpne `/demo/epostmottaker` for eget innhold.
Andre brukere får verken vedkommendes meldinger eller bekreftelseslenke.
Kontrollene krever både eksplisitt konfigurasjon, kontrollert miljø og administrator.
Produksjon registrerer aldri kontrollert klokke, tilgangskilde eller postadapter.

EV03s opprinnelige direkte administratoroppretting er senere utvidet med
direkte endringer (EV04), kø/godkjenning (EV06) og import/sletting (EV07-kandidat).
Abonnementets aktive varselvalg,
adressebekreftelse og databaseordnet aktiveringsgrense gjelder; avmelding og ny aktivering gir
ingen tilbakefylling fra avmeldt periode. Manglende aktiv konto eller fersk tilgang
utsetter mottakeren. Gjeldende godkjente frist og synlighet filtreres før projeksjon,
og abonnement/tilgang kontrolleres på nytt rett før adapteren mottar innhold.

Planer bevarer status og hendelses-IDer, men ingen rendret e-post eller mottakerliste.
Bekreftet overlevering betyr at kontrollert transport tok imot meldingen, ikke at
M365 leverte den. Kontrollert mottakerinnhold er bare i minnet og forsvinner ved
omstart; den varige bekreftelsen hindrer ny overlevering. Ubehandlede hendelser etter
nedetid samles i dagens oppsummering; historiske dagsmailer gjenskapes ikke.
Hendelser som committer sent, forblir ventende gjennom uttrykkelig ID-kobling.
En seedet koordinasjonsrad låses ved aktivering og direkte publisering og holdes
gjennom samme transaksjon som endringen. Publiseringssekvens etter den lagrede
aktiveringssekvensen avgjør utvalget, også ved like UTC-ticks eller forskjøvet klokke.
Tidligere aktive EV02-abonnenter får fremtidige hendelser fra grense 0 uten historisk
tilbakefylling. Mellomversjonen av den lokale EV03-kandidaten får sekvens 0 på sine
tidligere hendelser ved oppgradering; disse historiske hendelsene sendes ikke.

`UnderSending` etter avbrudd og `Uavklart` etter mulig overlevering stanser automatisk
retry for mottakeren. Tilstanden kontrolleres også på selve innleste dagsplanen;
overgangen krever Planlagt med forventet status og versjon. Ikke nullstill disse radene eller bekreftelsesstatus uten
avklaring hos transporten; EV03 har ingen slik reparasjonskommando. Utfallet og
utsettelsesgrunnen kan observeres ved `KjorAsync` og i varig planstatus. Vertjobben
logger bare status/antall eller en trygg feilmelding. Produksjonsadaptere er
utilgjengelige, så ingen fristdata overleveres der. Reell Entra-/gjestebrukertilgang,
avsender, tjenestevalg, ekstern levering, drift/opprydding og sikker stans krever
senere EV-porter. Dette er ingen produksjonsaktivering.

Målrettede prøver:

```sh
dotnet test backend/Aarshjul.slnx -c Release --filter FullyQualifiedName~DagligEpost
dotnet test backend/Aarshjul.BrowserTests -c Release --filter FullyQualifiedName~DagligEpostTester
dotnet test backend/Aarshjul.SqlServerTests -c Release --filter FullyQualifiedName~DagligEpostTester
```

SQLite og ekte SQL Server deler 49 oppsummeringskontrakttilfeller. SQLite prøver
normal demooppgradering fra UX16-/EV02-modellen; SQL Server prøver både EV02→EV03
og særskilt levert UX16→EV03 med autentiske migrasjoner. Ordinær suite har i tillegg to prøver av vertsjobb og
testoppstart. [EV03-akseptansen](acceptance/2026-10-28-epost-ev03.md) skiller lokale
bevis fra kandidatens SQL Server-/Chromium-CI og senere konfliktoppfølging.

## Varselvalg og direkte endringer (EV04)

På `/epostvarsling` kan brukeren velge Nye frister, Vesentlige endringer eller begge
ved påmelding og senere endre valgene. Minst én type kreves. Valgene lagres med
versjonskontroll for brukerens egen identitet; kalenderens visningsfiltre påvirker
dem ikke. Utvidelse gjelder fra det aktuelle valgets nye databaseordnede grense.
Den andre typens ventende hendelser bevares. Innskrenkning kontrolleres før sending.

Direkte redigering på `/admin/frist/{id}` lager automatisk en vesentlig hendelse ved
endret dato, klokkeslett, presisjon, måned, kvalifikator, budsjettår eller kategori.
Endret vilkårlig dag innen samme tentative måned eller tekstkorrektur varsles ikke.
Administrator kan markere annen faktisk innholdsendring og må da gi 1–300 tegn med
forklaring. Markeringen styrer ikke de automatiske tilfellene. Oppsummeringen viser
tidligere og nåværende dato/perioder, budsjettår og kategori, og samler hendelser per
frist. En ny frist som også er endret vises én gang for hvert av de tre varselvalgene.

`20261028165341_VarseltyperOgEndringer` utvider EV03-skjemaet additivt og bevarer
abonnement, aktiveringsgrenser, hendelses-ID-er, planer og hendelseskoblinger.
`Demoskjema.SikreAsync` oppgraderer eksisterende EV02-/EV03-demoer ved normal
oppstart uten historisk tilbakefylling. SQL Server bruker den ordinære migrasjonskjeden.
Bare direkte oppretting og redigering omfattes nå. Kø/generering, import og sletting
tilhører senere EV-tickets. Produksjonens tilgangs- og postadaptere er fortsatt
utilgjengelige frem til egne miljøporter er dokumentert.

[EV04-akseptansen](acceptance/2026-10-28-epost-ev04.md) bevarer leveransens lokale
prøver, inventar og daværende CI-porter. EV04 er merget i PR #133; ny UX18-kandidat-CI
kreves etter integrasjonen nedenfor. De eksterne miljøportene består.

## Levert UX16/EV02-verifikasjon

UX16/EV02 ble levert med 883 ordinære, 312 nettleser- og 79 SQL Server-tilfeller.
Den nye ordinære modellregresjonen kommer i tillegg til begge greners bevarte navn.
Full lokal Edge/Kestrel og ordinær suite er grønne; SQL Server er bare bygget/oppdaget
lokalt. UX16 fullførte senere 79 ekte SQL Server- og 312 Chromium-tilfeller i
[CI 40705737234](https://github.com/depmartin/Budsjettkalender/actions/runs/40705737234)
før merge i PR #129. Dette er bevis for UX16, ikke den nye EV03-integrasjonen.
Kommandoer, TRX, navn, kilde-/runtimebinding og ny topptekstinspeksjon finnes i
`TestResults/91-main-integration/` og [UX16-akseptansen](acceptance/2026-10-28-atelier-ux16.md).
De historiske kjøringene i tidligere bevismapper beholdes uendret.

## Historisk EV03/UX16-verifikasjon

EV03/PR #130 integrerer levert UX16/main `ffef7ab` lokalt. Faktisk oppdagelse gir
938 ordinære, 130 SQL Server- og 315 nettlesertilfeller, uten tap av navn fra noen
av grenene. Fire SQL Server-modelltilfeller og én lokal SQLite-oppgradering er
grønne 5/5; full ordinær sluttkjøring er grønn 938/938 uten hopp. Alle tre låste
restore med aktiv audit og Release-bygg gir 0 advarsler/feil. Begge EV03-målmodeller
er generert på nytt med riktig kronologi og hele internvernet; Up/Down er uendret.
Målrettet EV02/EV03/UX16 med Edge/Kestrel passerer 52/52 uten feil/hopp. 417
backend-/SDK-kildefiler og 1564 runtimefiler er uendret gjennom sluttprøvene.
`TestResults/113-integration/verify.ps1` og `suites.ps1` gir konkrete lokale
kommandoer og bevis. Samlet etterkontroll ble fullført; head `e4292caaa` bestod
[CI 40715203714](https://github.com/depmartin/Budsjettkalender/actions/runs/40715203714)
med 938 ordinære, 130 ekte SQL Server- og 315 Chromium-tilfeller. Disse resultatene
gjelder denne historiske kandidaten før levert UX17. Se
[EV03-akseptansen](acceptance/2026-10-28-epost-ev03.md) for bevisgrenser.

## UX17 – lesende administratoroversikt

Den smale `IAdministratoroversikt` leser bare appkjente administratorer og lagret
`AdminViaEntra`. `AdministratoroversiktKommando` håndhever ErAdministrator ved hvert
kall og bestemmer Deg fra stabil autentisert claim. Leseoperasjonen bruker aldri
det muterende brukeroppslaget. Ingen Graph eller ny rolleforvaltning inngår.
18 nye ordinære tilfeller og sju reelle Kestrel/Blazor-scenarioer kommer i tillegg
til de bevarte 883/312/79 navnene. Sluttinventar er 901/319/79. Hele den ordinære
suiten og alle 319 faktiske Edge/Kestrel-scenarioer passerer uten feil/hopp etter
siste kodeendring. Browser-TRX måler 1422,795 sekunder, prosessen 1423,884 sekunder.
SQL ble bare bygget/oppdaget lokalt. Faktisk SQL Server og Chromium passerte deretter
[CI 40717209832](https://github.com/depmartin/Budsjettkalender/actions/runs/40717209832)
før PR #131 ble merget som `959060c898048da5ebd26bfb2cd5cdb68a1c2271`.
TRX, navneinventar og SHA-256-manifest ligger i `TestResults/92-final/`.
Se [UX17-akseptansen](acceptance/2026-10-28-atelier-ux17.md) for faktisk kjøreresultat,
tokenfrie 1440-/390-bilder og verifikasjonsgrenser. Jobbgrensen på 45 minutter er uendret.
403 kildefiler og 1518 runtimefiler har ingen drift mellom manifestopptakene;
hele kandidaten på 471 filer og 16 visuelt inspiserte sluttbilder har SHA-256-bevis.

## Historisk UX18 – nødvendig EV04-mainintegrasjon i CI-løkken

PR #132 er publisert og fortsatt ikke merget. Dens head før integrasjonen,
`6acf357efdb7498281b3bdf970e3cc48e543de03`, bestod
[CI 40752611386](https://github.com/depmartin/Budsjettkalender/actions/runs/40752611386)
med 962 ordinære, 130 ekte SQL Server- og 328 Chromium-tilfeller uten feil/hopp.
EV04/PR #133 ble senere faktisk merget som main
`926ffe74e62999714f6e711327636ecce2070317` 28. oktober 2026 kl. 20.24.08 +01:00.
Denne nødvendige integrasjonen bevarer alle EV04-backendblobber, kontrakter,
migrasjoner/snapshot, `Demoskjema` og `FristInndata`-felter eksakt, sammen med UX18
og begge avgrensede CI-kvitteringer. Bare CI-/statuskonfliktene og dokumentasjon
av faktisk inventar krever manuell avstemming; ingen ny EV-funksjon inngår.

Hele ordinærporten passerer **1024/1024**, og hele faktiske Edge/Kestrel-porten passerer
**332/332**, uten feil/hopp. **186 SQL Server-tilfeller** er bygget/oppdaget. Alle tidligere
962/328/130 UX18-navn og 1018/326/186 EV04-navn er bevart eksakt uten dubletter.
Alle tre låste restore og Release-bygg med `-m:1 -nr:false`, SDK 10.0.402 og aktiv
audit (`true/all/low`) passerer uten advarsler/feil. Browser-TRX/prosess måler
1778,580/1779,356 sekunder. 430 backend-kildefiler og 1518 runtimefiler har null drift;
kandidatmanifestet har 505 filer. Bevis ligger i `TestResults/93-ci-main-integration/`,
med ferske TRX, kilde-/runtime-/kandidatmanifest og navnebevaring fra begge leveranser.
Alle 89 historier beholdes; samtlige 63 navngitte kravkartmetoder finnes i bestått ny TRX.
De 56 tidligere UX18-bildene og seks fokusbildene
er hashbevart; de viser ikke nye EV04-varselfelter. Modellprøven kontrollerer siste
SQL Server-mål/snapshot og `HasPendingModelChanges=false` uten databaseforbindelse.
SQL Server er bygget/oppdaget lokalt; ny faktisk kandidat-CI med SQL Server og
Chromium krevdes før den senere bekreftede UX18-mergen. Daværende CI krevde nøyaktig **332/186**, uten hopp eller
manglende TRX, med uendrede 15/20/45 minutter, aktiv audit og bare TRX-artefakter.
Denne avgrensede CI-konfliktrunden har ingen ny full gjennomgang eller sikkerhetsaudit.

## UX18 – historisk sluttakseptanse før EV04

UX18/#93 startet fra levert UX17/main `959060c`. En annen chat merget EV03/
PR #130 som main `93b1aea3f0953535e3144d22b36991e88ead7852` 28. oktober 2026
kl. 17.33.10 +01:00. Denne hovedgrenen er nødvendig integrert før samlet UX18-review;
ingen ny EV-ticket eller umerget PR #128 er tatt inn. Begge leveransers backendblobber,
e-postadaptervern, skjemaoppstart, modell/snapshot/migrasjoner og alle testnavn er bevart.

[UX18-rapporten](acceptance/2026-10-28-atelier-ux18.md) kartlegger alle 89 historier
individuelt. Seks nye HTTP-tilfeller, tre sammenhengende publiserings-/identitetstilfeller
og tre tilgjengelighetsregresjoner er beholdt. Ny full ordinær suite passerer **962/962**,
og hele faktiske Edge/Kestrel-suiten passerer **328/328**, uten feil/hopp. **130 SQL** er
bygget/oppdaget lokalt. Alle tidligere UX17 901/319/79, UX18 907/325/79 og EV03/UX17
956/322/130 navn består eksakt, med 63 navngitte kravkartmetoder i bestått TRX.
Alle tre låste restore/Release-bygg har null advarsler/feil med aktiv audit.
Modellregresjonen gir `HasPendingModelChanges=false` uten SQL-forbindelse.
Browser-TRX/prosess måler 1 562,594/1 563,422 sekunder.
427 kilde- og 1518 runtimefiler er uendret gjennom kjøringen; kandidaten har 501 filer.
56 tidligere inspiserte UX18-bilder og 16 UX17-bilder er hashbevart, med
seks nye tokenfrie fokusbilder fra integrert runtime, visuelt lest enkeltvis.
Fullt bevis er i `TestResults/93-main-integration/`.
Tidligere `TestResults/93-final/` bevares som historisk 907/325-bevis før mainintegrasjon.
Daværende CI krevde **328 Chromium og 130 SQL Server**, uten hopp, med uendrede 15/20/45 minutter.
Samlet review og separat audit ble senere avsluttet med 0 funn på det angitte
grunnlaget; faktisk 962/130/328-CI bestod før EV04-integrasjonen. UX18-merge gjenstår.

## Obligatorisk nettleserakseptanse

Gjeldende EV10-integrasjon krever nøyaktig **414 beståtte Chromium-scenarioer**
uten hopp eller manglende TRX. **510 ekte SQL Server-tilfeller** kreves i den
separate SQL-porten. De daterte kjøringene nedenfor viser tidligere kandidatgrunnlag.

`backend/Aarshjul.BrowserTests` ligger utenfor den ordinære løsningen. Det krever ingen
eksisterende app eller database: 414 scenarioer bruker disponible Kestrel-verter,
inklusive fem fra EV02, tre fra EV03, fire fra EV04, fire fra EV05, seks fra EV06,
to fra EV07, fire fra EV08, fire fra EV09 og fem fra EV10,
sju fra UX17, seks fra UX18, to fra #140, åtte fra #138s listeutvalg og 16 fra
felles detaljretting, samt 31 fra #139s søk, treffnavigasjon og fokus ved årsvalg. Detaljprøvene revaliderer synlighet, grupper og autentisering,
håndterer sletting, tømmer dialog-DOM ved Lukk/Escape og verner mot sene svar
og JS-åpninger. Alle tidligere navn består.
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
Den tidligere UX16-verifikasjonen før EV02-integrasjon passerer **841/841** ordinære tester
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
installerer systemavhengigheter og krever nøyaktig 414 beståtte scenarioer uten hopp.
EV05/UX18-kandidaten beholder merget mains jobbgrense på 45 minutter.
Begge greners første CI ble avbrutt ved 15 minutter uten fullført nettleser-TRX;
EV02s lokale fulle kjøringer tok 23:03 og 30:08, og UX15s sluttkjøring tok 21:42.
De lokale tidene begrunner rom for testene og oppsettet, men måler ikke Chromium.
Ren EV02-CI [40673623216](https://github.com/depmartin/Budsjettkalender/actions/runs/40673623216)
fullførte senere 226 Chromium-scenarioer: teststeget tok 11:57 og jobben 13:08.
Den målingen dekker ikke de 312 integrerte tilfellene etter UX16. UX15s tidligere
20-minuttersforsøk er historisk kontekst; 45 minutter er en beholdt øvre grense,
ingen påstand om nødvendig kjøretid eller garanti for det nye inventaret.
Ingen forventningstimeout, antallskontroll eller kontroll av manglende TRX er svekket.
Ny kandidat-CI må fullføre alle 414; et nytt tidsavbrudd krever diagnose før ytterligere økning.
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
[EV02-prøvene](acceptance/2026-10-28-epost-ev02.md),
[UX17-prøvene](acceptance/2026-10-28-atelier-ux17.md) og
[UX18-kravkartet](acceptance/2026-10-28-atelier-ux18.md) for faktisk prøvd scope og eksterne grenser.

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
hadde før EV03 samlet 79 testtilfeller: de tidligere 46, 14 delte internkalendertilfeller,
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
siste EV03-migrasjon via `20261028112544_UnikeInternrunder`. Den positive prøven bevarer også et aktivt og
et ventende e-postabonnement; den ventende bekreftelsen kan brukes etter oppgraderingen.
EV02s tidligere oppgradering fra FellesFasemal er fortsatt en egen prøve.
Den ordinære modellregresjonen kontrollerer SQL Server-modellen uten tilkobling:
ingen ventende modellendringer, e-postentiteten og begge internrundeindeksene i
siste migrasjonsmål, samt alle berørte samtidighetspredikater. Dette erstatter
ikke de 186 faktiske SQL Server-prøvene i ny UX18-kandidat-CI.
Regelinnholdet ved synk er snapshotet fra operasjonens siste lesing før lagring;
en regelendring etter denne lesingen blir synlig i neste forhåndsvisning. Dette er
egen delt kontrakt, og UI leser derfor forslagene på nytt etter lagring. Synk låser
ikke regelregisteret mot samtidig redigering i andre runder. Fullføring/manuell
oppgaveendring og dobbel synk har separat vern ved det endelige skriveøyeblikket.

EV02 legger til 15 delte e-postabonnementstilfeller og én oppgradering fra FellesFasemal
med bevart frist-/forslags-/kilde-/synlighetsinnhold. Eierskap, eksplisitte standarder,
varig starttidspunkt, utløp, engangsbruk, forsøkstak, nyutsending og to samtidige
instanser prøves gjennom `IEpostabonnement`, også avmelding/aktivering mens transporten
fullfører en overlevering. Sammen med UX16 hadde prosjektet 79 tilfeller før EV03.

EV03 legger til 49 delte oppsummeringstilfeller og én migrasjonsprøve fra EV02, med
bevart abonnement, frist, forslag, kilde, synlighet, feed og internkalender. Prosjektet
hadde etter integrasjon med levert UX16 og en ny oppgraderingsprøve 130 tilfeller.
EV04 utvider med direkte endringer, varselvalg og oppgradering fra levert EV03;
det faktiske samlede inventaret er nå 186 SQL Server-tilfeller.
Triggerprøven krever feil fra den faktiske hendelses-INSERT-en;
en annen databasefeil teller ikke som rollbackbevis. To instanser prøver både den
unike førstegangsplanen og vinnende overgang for en eksisterende plan.

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
Den gjenoppretter låste pakker og krever en TRX-fil med nøyaktig 510 beståtte tester og null hopp.
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

## EV09 – levert avmeldingslenke

[EV09-akseptansen](acceptance/2026-10-29-epost-ev09.md) beskriver rød/grønn,
1348 ordinære tilfeller, delte SQLite-/SQL Server-kontrakter, migrasjonskjede
og anonym avmelding fra faktisk e-post. EV09 er levert i PR #146, merget som
`dd2a24c87aceb692c576eafa155d02ee0541b12b`. Dens slutt-CI
[40887998599](https://github.com/depmartin/Budsjettkalender/actions/runs/40887998599)
bestod 1348/480/354; dette er forelderens bevis. #138s tidligere union var 1349/480/378.
Alle navn fra EV09 og #138 bevares ved den nye EV10-integrasjonen. Lokal Edge er et kontrollert
nettleserbevis; faktisk SQL Server og full Chromium utføres i kandidat-CI.

CI kontrollerer TRX for avmeldingsfragment og 64-tegns hexverdier før hver
opplasting, og laster ingenting opp når kontrollen feiler. Innholdsassertions
beholder forventningene og redigerer bare hemmelig avmeldings-URL før eventuell
xUnit-feiltekst. Tokenets faktiske tilstedeværelse og rettighet prøves direkte
med boolske assertions uten verdiutskrift. Bare TRX er tillatt som artefakt;
databaser, app-logger, DOM og rå nettleserspor lastes ikke opp.

Kjøreeksempler, i tillegg til de ordinære portene over:

```powershell
dotnet test backend/Aarshjul.Tests -c Release --no-build `
  --filter 'FullyQualifiedName~DagligEpostTester|FullyQualifiedName~MigrasjonsmodellTester|FullyQualifiedName~EpostabonnementHttpTester'
dotnet test backend/Aarshjul.BrowserTests -c Release --no-build `
  --filter 'FullyQualifiedName~EpostabonnementTester|FullyQualifiedName~DagligEpostTester'
python .github/scripts/kontroller-hemmeligheter.py TestResults
```

Ingen ekstra produksjonsadapter, tenant-/postbokstilgang eller utrulling
aktiveres av EV09. Fremtidig e-postadapter må oppfylle den synkrone
transportmottakskontrakten i ADR-0010 før den kan tas i bruk.

## EV10 – abonnentadministrasjon

Administrator-innstillinger → Åpne e-postabonnenter er den nye flaten.
`/admin/innstillinger/epostvarsling` og `/api/admin/epostabonnenter` krever
administrator. JSON-POST til `/api/admin/epostabonnenter/stans` tar `brukerId`,
`versjon` og `bekreftet`; aktøren hentes fra innloggingen. Konflikt gir 409 og
krever ny lesing/bekreftelse. Valideringsfeil gir 400, feil innholdstype 415,
lagringsfeil 503. Alle svar har no-store. Kalenderfeed er fortsatt en egen flate.

Migrasjonen `20261029170000_Epostadministratorstans` legger til et tomt,
varig revisjonsspor. Historiske migrasjoner og begge ID-er med timestamp
20261029092000 beholdes. Demo bruker samme additive operasjoner i én transaksjon,
med SQLite INTEGER-nøkkel for revisjonsrekkefølge. Ufullstendig audit-tabell eller
manglende indeks avvises uten endring av eksisterende data.

```powershell
dotnet test backend/Aarshjul.Tests -c Release --no-build `
  --filter 'FullyQualifiedName~EV10|FullyQualifiedName~EpostabonnentadministrasjonHttpTester|FullyQualifiedName~MigrasjonsmodellTester'
dotnet test backend/Aarshjul.BrowserTests -c Release --no-build `
  --filter 'FullyQualifiedName~EpostabonnementTester|FullyQualifiedName~DagligEpostTester|FullyQualifiedName~AdministratoroversiktTester|FullyQualifiedName~AbonnementTester'
# Lokal Windows-prøve kan bruke AARSHJUL_BROWSER_CHANNEL=msedge.
# Kandidat-CI bruker installert Chromium og ekte disponibel SQL Server.
python .github/scripts/kontroller-hemmeligheter.py TestResults/120/final
```

Bruk [akseptansen](acceptance/2026-10-29-epost-ev10.md) for konkret kandidatgrunnlag,
rød/grønn, faktisk inventar og lokale resultater. Ingen produksjonsadapter eller
eksterne sender-/Entra-rettigheter aktiveres av denne leveransen.
