# Gjeldende status

Oppdatert 29. oktober 2026. Dette er samlet leveransestatus. [PR #144](https://github.com/depmartin/Budsjettkalender/pull/144), [PR #143](https://github.com/depmartin/Budsjettkalender/pull/143), [PR #142](https://github.com/depmartin/Budsjettkalender/pull/142),
[PR #135](https://github.com/depmartin/Budsjettkalender/pull/135), [PR #134](https://github.com/depmartin/Budsjettkalender/pull/134), [PR #132](https://github.com/depmartin/Budsjettkalender/pull/132)
og kommentarene i [#140](https://github.com/depmartin/Budsjettkalender/issues/140),
[#110](https://github.com/depmartin/Budsjettkalender/issues/110), [#75](https://github.com/depmartin/Budsjettkalender/issues/75) og
[#93](https://github.com/depmartin/Budsjettkalender/issues/93) er kilden til siste
publiserte head, faktisk CI, mergecommit, verifisert main og oppgavenes livsløp. Tidligere kandidatbeskrivelser og lokale
testtall er bevart som [datert historikk](archive/status-foer-ux18-2026-10-28.md),
og gjelder den revisjonen som er oppgitt der.

Atelier UX01–UX17 / #76–#92 er levert og merget. UX17/PR #131 ble levert som
`959060c898048da5ebd26bfb2cd5cdb68a1c2271` med grønn
[CI 40717209832](https://github.com/depmartin/Budsjettkalender/actions/runs/40717209832):
901 ordinære, 319 Chromium- og 79 ekte SQL Server-tilfeller. Senere ble EV03/
[PR #130](https://github.com/depmartin/Budsjettkalender/pull/130) merget av en annen
chat som daværende main `93b1aea3f0953535e3144d22b36991e88ead7852`,
28. oktober 2026 kl. 17.33.10 +01:00. Denne mergede hovedgrenen er nødvendig
integrert i UX18 før samlet review. [UX18s leveranseregister](acceptance/2026-10-28-atelier-ux18.md#leveranseregister)
bevarer de enkelte leveransenes historiske PR-/CI-bevis. Senere EV-arbeid har egne autoriserte undertickets.

EV03 ([#113](https://github.com/depmartin/Budsjettkalender/issues/113)) er levert i
[PR #130](https://github.com/depmartin/Budsjettkalender/pull/130), bekreftet merget som
`93b1aea3f0953535e3144d22b36991e88ead7852`. Slutt-[CI 40722978578](https://github.com/depmartin/Budsjettkalender/actions/runs/40722978578)
bestod 956 ordinære, 130 ekte SQL Server- og 322 Chromium-tilfeller uten feil/hopp.
Kandidaten er avstemt med levert UX16/UX17. Varige publiseringshendelser, dagsplan,
sekvensgrense, synlighetskontroll og fersk kontrollert mottakertilgang består.
[EV03-akseptansen](acceptance/2026-10-28-epost-ev03.md) bevarer historiske prøver og grenser.
Produksjon sender ingen e-post.

EV04 ([#114](https://github.com/depmartin/Budsjettkalender/issues/114)) er deretter
merget i [PR #133](https://github.com/depmartin/Budsjettkalender/pull/133) som faktisk
main `926ffe74e62999714f6e711327636ecce2070317` 28. oktober 2026 kl. 20.24.08 +01:00.
Denne mergede hovedgrenen er nødvendig integrert i UX18 etter mergekonflikt.
EV04s slutt-CI bestod 1018 ordinære, 186 ekte SQL Server- og 326 Chromium-tilfeller uten feil/hopp.
Brukeren velger nye frister, vesentlige endringer eller begge, med
versjonskontroll og egen aktiveringsgrense per type. Direkte administratorredigering
varsler semantisk dato-/periode-, budsjettår- og kategoriendring automatisk.
Annen faktisk innholdsendring kan markeres med kort forklaring. Oppsummeringen samler
hendelser per frist og viser før-/etterverdier innen dagens synlighet og ferske tilgang.
Additiv migrasjon og idempotent demooppgradering bevarer EV03-data uten tilbakefylling.
[EV04-akseptansen](acceptance/2026-10-28-epost-ev04.md) bevarer leveransens daterte
lokale prøver og daværende porter; gjeldende livsløp står i PR #133. Kø/generering, import/sletting
og produksjonsporter tilhører senere EV-tickets. Ingen deploy er utført.

EV05 ([#115](https://github.com/depmartin/Budsjettkalender/issues/115)) er levert i
[PR #134](https://github.com/depmartin/Budsjettkalender/pull/134), med eget budsjettår-/kategoriutvalg
ved påmelding og versjonskontrollerte innstillinger. Null/tomt betyr Alle, også
fremtidige år. Utvidelser beholder ventende treff uten historisk tilbakefylling;
innsnevring håndheves før overlevering. Endringer matcher hele før- eller
etterutvalget innen dagens synlighet og ferske mottakertilgang. Additiv migrasjon
og demooppgradering bevarer EV02–EV04. Levert UX18 er avstemt før bekreftet merge
som `29421c2ff1a1595ecfaba5e80d18ce529ade8c03` på main.
Slutt-[CI 40770318096](https://github.com/depmartin/Budsjettkalender/actions/runs/40770318096)
bestod 1063 ordinære, 216 ekte SQL Server- og 336 Chromium-tilfeller uten feil/hopp.
[EV05-akseptansen](acceptance/2026-10-28-epost-ev05.md) skiller opprinnelige lokale
bevis fra integrasjonsprøver og ny kandidat-CI. Ingen deploy inngår.

EV06 ([#116](https://github.com/depmartin/Budsjettkalender/issues/116)) er levert i
[PR #135](https://github.com/depmartin/Budsjettkalender/pull/135), bekreftet merget som
`28694c580141b91f9707c616a53ee95f05246e11`. Slutt-[CI 40843392655](https://github.com/depmartin/Budsjettkalender/actions/runs/40843392655)
bestod 1157 ordinære, 310 ekte SQL Server- og 342 Chromium-tilfeller uten feil/hopp.
Samlet Standards-/Spec-review og separat sikkerhetsaudit er avklart i PR-en.
Begge godkjenningsinnganger lagrer førstegangs-
eller vesentlig endringshendelse atomisk med forslag, frist og innboksmelding.
Godkjennerens begrunnede innholdsendring følger samme regel som direkte redigering;
endringsforslag beholder synlighet og direkte godkjenning bevarer målfristens tid.
Innsending, generering/regenerering, avvisning og korrektur gir ingen fristpost.
[EV06-akseptansen](acceptance/2026-10-29-epost-ev06.md) beskriver rød/grønn,
konkurranseprøver og reelle brukerforløp. Oppdaget inventar er 1157 ordinære,
310 SQL Server- og 342 nettlesertilfeller med hele basens navn bevart. Akseptansens
beskrivelse av en lokal kandidat er datert byggbevis før denne leveringen.

EV07 ([#117](https://github.com/depmartin/Budsjettkalender/issues/117)) er bekreftet
merget i [PR #143](https://github.com/depmartin/Budsjettkalender/pull/143) som main
`d3bec41e7a42106e6f2b8bee80de0916c3f65b60`. Import beholder erstatt-alt og v1-formatet,
men sammenlikner publisert innhold per identitet i samme transaksjon. Reelle Ny,
Endret og Slettet lagres atomisk med import, historisk publikum og sekvens;
identisk gjenimport og korrektur varsler ikke. Sletting bruker fersk mottakertilgang.
Livssyklus utelater ny-og-fjernet før første oppsummering uavhengig av varseltyper.
Vanlige kalenderlenker viser relevant utvalg og generell fjerningsmelding.
[EV07-akseptansen](acceptance/2026-10-29-epost-ev07.md) bevarer daterte lokale og
første CI-bevis; PR #143 er kilden til leveransens slutt-CI og livsløp.
Slutt-[CI 40857856203](https://github.com/depmartin/Budsjettkalender/actions/runs/40857856203)
bestod 1228 ordinære, 379 SQL Server- og 344 Chromium-tilfeller uten feil/hopp.

EV08 ([#118](https://github.com/depmartin/Budsjettkalender/issues/118)) er merget i
[PR #144](https://github.com/depmartin/Budsjettkalender/pull/144) som faktisk main
`b6a85f2251831eaba1cd69d17249ec57e13578c3`. Eget adressebytte krever gjeldende
versjon og egenadressesamtykke. Ny adresse venter varig på ny bekreftelse, med
stanset fristvarsling, beholdte varselvalg og felles forsøksgrenser. Bekreftelsen
setter nye databaseordnede type-, utvalgs- og livssyklusgrenser uten etterslep.
Ingen egen modellendring eller migrasjon. [EV08-akseptansen](acceptance/2026-10-29-epost-ev08.md)
bevarer datert byggbevis; PR #144 er kilden til leveransens livsløp.

[#140](https://github.com/depmartin/Budsjettkalender/issues/140), parent #136 punkt 4,
er kandidat i [PR #142](https://github.com/depmartin/Budsjettkalender/pull/142).
Direkte administratoroppretting/redigering får valgfri manuell kildelenke med
prioritet foran dokumentets lenke og forklaring ved tømming. Én serverregel
gjelder skriving, JSON-import og lesing; proveniens og køjustering bevares.
JSON v1 får et valgfritt felt. Lenkeendring følger eksisterende begrunnede
vesentlighetsvalg. Additiv migrasjon/demo og innholdsøyeblikksbilder er avstemt
med levert EV07; begge migrasjonene med timestamp 20261029092000 beholdes.
Forrige kandidat på EV07-basen bestod alle tre faktiske porter i
[CI 40868270127](https://github.com/depmartin/Budsjettkalender/actions/runs/40868270127):
1242 ordinære, 389 SQL Server og 346 Chromium. Dette er historisk bevis for den
gamle basen. Kandidaten er nå avstemt med levert EV08, og alle navn fra begge
foreldre er bevart; samlet inventar er 1302/442/350. Lokal ordinærsuite består
1302/1302, målrettet samspill 545/545 og relevante Edge/Kestrel-forløp 31/31,
uten feil/hopp. SQL Server er bygget/oppdaget, ikke lokalt kjørt. Prøver og
presise bevisgrenser står i [kildelenkeakseptansen](acceptance/2026-10-29-manuell-kildelenke.md).
Alle tre faktiske nye kandidat-CI-porter må være grønne. Ingen ny formell
review/audit er utført i denne autoriserte CI-løkken. Ingen deploy inngår.

## UX18 – samlet akseptanse og publisering

[#93](https://github.com/depmartin/Budsjettkalender/issues/93) prøver hele Atelier
fra UX17-basen med de senere faktisk mergede EV03- og EV04-hovedgrenene integrert. [Akseptanserapporten](acceptance/2026-10-28-atelier-ux18.md)
kartlegger **alle 89 historier individuelt** med oppgave/PR, navngitt prøve,
resultat og revisjonsgrunnlag. Den sammenhengende publiseringsprøven dekker FA og
FIN-FAG: dato → ordinært forslag → Mine forslag/kø → eget aktivt POL-valg → én
frist i Måned/Uke/Liste/Tidslinje/Fristliste/Fristregister, også etter endringsforslag.
Direkte administratoroppretting/redigering har samme identitet og verdier i alle
seks flater. Hele HTTP-svar prøves med anonym, FAG, POL, FA, FIN-FAG og administrator;
gamle innganger, seks adminområder, interne data og skjult metadata er med.

PR #132 er nå merget som `134857de41527e4442ed3f7009620487f13f82f4`. De følgende tallene er historiske bevis fra før denne mergen. Før EV04-integrasjonen bestod publisert head
`6acf357efdb7498281b3bdf970e3cc48e543de03`
[CI 40752611386](https://github.com/depmartin/Budsjettkalender/actions/runs/40752611386)
med **962 ordinære, 130 ekte SQL Server- og 328 Chromium-tilfeller**, uten feil/hopp.
UX18s PR viser slutt-CI og bekreftet merge etter EV04-integrasjonen; de daterte lokale bevisene følger nedenfor.
Den nye fulle lokale kjøringen passerer **1024/1024 ordinære og 332/332 faktiske
Edge/Kestrel-scenarioer**, uten feil/hopp. **186 SQL Server-tilfeller** er bygget/oppdaget.
Alle tidligere 962/328/130 UX18-navn og 1018/326/186 EV04-navn består eksakt.
Alle tre låste restore og Release-bygg passerer med aktiv audit og null advarsler/feil.
430 backend-kildefiler og 1518 runtimefiler har null drift; kandidatmanifestet har 505 filer.
Browser-TRX/prosess måler 1778,580/1779,356 sekunder. Alle 63 kravkartmetoder er bestått
i ny TRX; kartet har alle 89 historier, og 62 tidligere inspiserte bilder er hashbevart.
Ferske lokale bevis ligger i `TestResults/93-ci-main-integration/`. Alle EV04-backendblobber,
migrasjoner, kontrakter, DI og varselfelter samt UX18s kilder og to CI-kvitteringer
er bevart eksakt. CI krever 332/186, uendrede 15/20/45 minutter, aktiv audit og bare TRX.
Dette er en nødvendig CI-konfliktrunde uten nytt fullt review eller sikkerhetsaudit.

Den tidligere samlede lokale kjøringen før EV04 passerte **962/962 ordinære og 328/328 faktiske
Edge/Kestrel-scenarioer** uten feil/hopp. **130 SQL-tilfeller** er bygget/oppdaget
lokalt. Alle tidligere 901/319/79, 907/325/79 og 956/322/130 navn er bevart eksakt.
Låst restore og alle tre Release-bygg har null advarsler/feil med aktiv audit.
Alle 89 historier er kartlagt individuelt. De 88 app-/kompatibilitetshistoriene
har navngitt bestått bevis fra 63 metoder i siste TRX; historie 86 er prototype.
Tre observerte tilgjengelighetsfeils opprinnelige røde/grønne bevis og 56 inspiserte
sluttbilder er bevart. Seks nye fokusbilder fra integrert runtime er visuelt lest.
427 kilde- og 1518 runtimefiler har ingen drift under fullkjøringen;
sluttkandidaten har 501 filer. `TestResults/93-main-integration/` binder
resultat, inventar og SHA-256; tidligere 907/325-bevis i `TestResults/93-final/`
er historisk. Daværende CI krevde nøyaktig 328/130, bare TRX og uendret 45-minutters browsergrense.
Det ferdige kildegrunnlaget har full Standards-review, full Spec-review og en
uavhengig sikkerhetsaudit, alle med **0 funn**. [Publiseringsregisteret](acceptance/2026-10-28-atelier-ux18.md#publisering-og-gjeldende-livsløp)
binder det lokale kildegrunnlaget og identisk første publiserte tree til PR #132.
PR-ens kontroller og oppgavekommentarene viser endelig CI- og mergestatus.
Beskrivelsen av en upublisert lokal kandidat før PR #132 er historikk fra
28. oktober 2026. Ingen deploy inngår.

## Levert app og bevarte funksjoner

Kalender er hovedinngang med Måned/Uke/Liste, minikalender, dagsagenda, fullstendige
detaljer, kilder og eksplisitte flervalg av kategorier/budsjettår. Budsjettår samler
Tidslinje og Fristliste for hele årets autoriserte innhold; `/aarshjul` bevarer
tidligere flere-år-/historikkatferd. Forslag fra dato bruker det eksisterende
godkjenningsløpet, og endringsforslag beholder originalens synlighet.

Administratorens seks områder har konkret arbeidsår, kø, dokumentinnlesing med
faktisk offline PDF-uttrekk, kodefri regelbygger, filtersnitt, kronologisk sortering,
sideeffektfri malprøve, generering og forhåndsvist mal-/fristoverføring. Felles
fasemal er varig, versjonert og adskilt fra faktiske frister. Grupper og lesende
innsyn bevarer servergrensen. Faktiske Word/ICS/JSON-filer og tilbakekallbar
tokenfeed er prøvd. Internkalenderens runder, fem tidfestinger, ansvarlige, Mine,
fullføring og bekreftet synk er egne SBR-enheter. Innstillinger viser bare
appkjente administratorer med lagret tilgangskilde og Deg fra stabil identitet.

EV02 / [PR #126](https://github.com/depmartin/Budsjettkalender/pull/126) er merget
som `1973ac0c729a29da2da687e91359ac5a8f751a9a` og integrert før UX16/PR #129.
Egen påmelding, kontrollert adressebekreftelse, varig aktivering og avmelding er
bevart for alle tre funksjonsroller. Ingen bruker meldes automatisk på, og ingen
ekstern e-post sendes av EV02. Mergede EV03 bevarer atomiske nye-frist-hendelser,
varig daglig oppsummering, databaseordnet aktivering og kontrollert demotransport.
Produksjonens e-postadaptere er fortsatt utilgjengelige. Senere EV-arbeid under
#110 inngår ikke i UX18.
EV03/PR #130 og EV04/PR #133 er bevart fra faktisk main; separat PR #128 er ikke integrert.

## Prototype, videre oppgaver og miljøporter

Historie 86 i #75 gjelder utnevnelse/fjerning av eksempelpersoner i den godkjente
skissen. Reell rolleforvaltning er ikke levert av prototypen eller den lesende
oversikten i #92. #94 AD01 er egen beslutning med #39/#92 som forutsetninger;
#95 AD02 avventer godkjent #94. Ingen av disse startes i dette mandatet.

Lukking av #75 bygger på kravkartet, fullført review/audit, grønn CI på siste
publiserte kandidat og bekreftet merge. Den faktiske beslutningen og lukkestatusen
står i #75/#93 og PR #132; rapporten lukker ikke andre oppgaver automatisk.
#42/#43/#48/#55 og #38/#56/#57 beholder sine egne kalenderpolicy-/Office-/M365-/
Entra-/Azure-/pilotporter. Lokal SQLite/Edge og vanlig CI beviser ikke virkelig
innlogging/tilbakekalling, Azure SQL/migrering, Key Vault/RBAC/managed identity,
nettverk eller Data Protection. Live kildeworker og full PDF-kandidatbehandling
er ikke attestert. Ingen produksjonsgodkjenning eller deploy inngår.

[Overleveringen](acceptance/2026-10-28-atelier-overlevering.md) har kjørekommandoer,
en portabel lokal demo med ny database på 5093, bevisgrenser, grener/worktrees og
kopierbar oppstart for en ny chat. Arbeidet stopper etter #93. Tidligere demoer,
andre uferdige checkouter, PR #65 og plan-/prototypegrener er bevart.
