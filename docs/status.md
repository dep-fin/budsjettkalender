# Gjeldende status

Oppdatert 28. oktober 2026. Dette er samlet leveransestatus. [PR #132](https://github.com/depmartin/Budsjettkalender/pull/132)
og kommentarene i [#75](https://github.com/depmartin/Budsjettkalender/issues/75) og
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
bevarer de enkelte leveransenes historiske PR-/CI-bevis. Nytt EV-arbeid er ikke bestilt.

## UX18 – samlet akseptanse og publisering

[#93](https://github.com/depmartin/Budsjettkalender/issues/93) prøver hele Atelier
fra UX17-basen med den senere faktisk mergede EV03-main integrert. [Akseptanserapporten](acceptance/2026-10-28-atelier-ux18.md)
kartlegger **alle 89 historier individuelt** med oppgave/PR, navngitt prøve,
resultat og revisjonsgrunnlag. Den sammenhengende publiseringsprøven dekker FA og
FIN-FAG: dato → ordinært forslag → Mine forslag/kø → eget aktivt POL-valg → én
frist i Måned/Uke/Liste/Tidslinje/Fristliste/Fristregister, også etter endringsforslag.
Direkte administratoroppretting/redigering har samme identitet og verdier i alle
seks flater. Hele HTTP-svar prøves med anonym, FAG, POL, FA, FIN-FAG og administrator;
gamle innganger, seks adminområder, interne data og skjult metadata er med.

Samlet lokal sluttkjøring passerer **962/962 ordinære og 328/328 faktiske
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
er historisk. CI krever nøyaktig 328/130, bare TRX og uendret 45-minutters browsergrense.
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
EV03/PR #130 er bevart fra faktisk main; separat PR #128 er ikke integrert.

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
