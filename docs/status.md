# Gjeldende status

Oppdatert 28. oktober 2026. Denne siden beskriver implementert kode, testbevis og
gjenstående grenser. GitHub-lenkene er kilden for oppgavenes og PR-enes livsløp.
[Gjennomgangen fra 20. oktober](reviews/2026-10-20.md) er et datert utgangspunkt,
ikke en liste over dagens urettede kodefeil.

E-postvarsling EV02 ([#112](https://github.com/depmartin/Budsjettkalender/issues/112)) er
implementert som leveransekandidat med egen påmelding for alle tre funksjonsroller,
servervalidert `.dep.no`-adresse, kontrollert testmottaker, engangsbekreftelse, varig
aktivering og avmelding. Standardutvalget Nye frister, Alle budsjettår og Alle kategorier
lagres uttrykkelig. Ingen eksisterende bruker meldes automatisk på. Produksjonstransport,
fristoppsummering, adressebytte, utvidede valg og administratoroversikt tilhører senere
EV-tickets under [#110](https://github.com/depmartin/Budsjettkalender/issues/110).
821 ordinære tester og alle fem nye reelle Edge/Kestrel-forløp er bestått lokalt.
Siste komplette nettleserport utførte 226 tilfeller med 224 bestått og to avvik i
uendrede kø-/kalenderscenarioer; den er ikke grønn. SQL Server-prosjektet er bygget med
60 oppdagede tilfeller; reell SQL Server- og Chromium-kjøring er fortsatt CI-port før levering.
[EV02-akseptansen](acceptance/2026-10-28-epost-ev02.md) inneholder de konkrete bevisene.

Atelier UX01 ([#76](https://github.com/depmartin/Budsjettkalender/issues/76)) er levert i
[PR #96](https://github.com/depmartin/Budsjettkalender/pull/96), merget som
`c1f250ea4e6ddbf05075059e0d1fc47f582d805b`, med felles skall, rollebasert hovedmeny, seks virksomme
administrasjonsområder og Budsjettår → Fristliste. `/aarshjul` og tidligere direkte
innganger består. [Akseptanse og prøvekommandoer](acceptance/2026-10-27-atelier-ux01.md)
beskriver rød/grønn, mobil/tastatur, 629 ordinære tester og 26 nettleserscenarioer.
Atelier UX02 ([#77](https://github.com/depmartin/Budsjettkalender/issues/77)) er levert i
[PR #97](https://github.com/depmartin/Budsjettkalender/pull/97), merget som
`a65ae35c1edd74c2780f347948f7e5962e11f20e`, med månedskalender, minikalender, dagsagenda og fullstendige fristdetaljer.
Hele valgt måned vises, mens Fristlistens historikkvalg bevares separat. Tentative
månedsfrister ligger utenfor dagsrutene. [Akseptanse](acceptance/2026-10-27-atelier-ux02.md)
beskriver kalender-, kilde-, synlighets-, rolle- og tastaturprøvene.
Atelier UX03 ([#78](https://github.com/depmartin/Budsjettkalender/issues/78)) er levert i
[PR #98](https://github.com/depmartin/Budsjettkalender/pull/98), merget som
`f72b2b7dca618067bc408723d0295a6af0471ab0`, med eksplisitte års-/kategorivalg, søk i tittel/notat, nullstilling og synlig
periode-/filterkontekst. Kalenderen beholder sin egen tilstand i kretsen; Fristlistens
historikk og leseportens null/tom=alle består. [Akseptanse](acceptance/2026-10-27-atelier-ux03.md)
beskriver union, snitt, nulltreff, autoriserte årsvalg og mobilprøvene.
Atelier UX04 ([#79](https://github.com/depmartin/Budsjettkalender/issues/79)) er levert i
[PR #99](https://github.com/depmartin/Budsjettkalender/pull/99), merget som
`19d7949d1950d3bc8319430b3d62eacaefd54783`, med Uke og Liste på samme kalenderkontekst. Listen følger eksplisitt den sist
valgte måned-/ukeperioden. Uken viser heldag og alle klokkeslett hver for seg, samt
tentative frister fra alle berørte måneder uten oppdiktet dagsdato eller varighet.
[Akseptanse](acceptance/2026-10-27-atelier-ux04.md) beskriver periode-, detalj-, rolle-,
mobil- og tastaturprøvene.
Atelier UX05 ([#80](https://github.com/depmartin/Budsjettkalender/issues/80)) er levert i
[PR #100](https://github.com/depmartin/Budsjettkalender/pull/100), merget som
`b1404d87f108854a535e307db33787b35f1e9e20`, med komplett Tidslinje og Fristliste for ett budsjettår, inklusive alle kategorier,
historikk og frister utenfor standardvinduet. Tette månedsgrupper åpner alle frister;
tentative måneder beholder presisjon og kvalifikator. Kalenderovergangen bevarer søk,
kategorier og årsvalg og legger til målets budsjettår ved behov. Det gamle `/aarshjul`
åpner Flere år og historikk med tidligere filterkontrakt. [Akseptanse](acceptance/2026-10-27-atelier-ux05.md)
beskriver kilder, roller, Oslo-dato, datogrenser, mobil og tastatur.
Atelier UX06 ([#81](https://github.com/depmartin/Budsjettkalender/issues/81)) er levert i
[PR #101](https://github.com/depmartin/Budsjettkalender/pull/101), merget som
`1a656261041790e503eddeaa1aefea422f452f17`, med Foreslå frist fra dagsagenda, redigerbar forhåndsutfylt dato, selvstendig
budsjettår, tentativ måned og kvittering. Forslaget er i Mine forslag og godkjenningskøen
før publisering. Administratorgodkjenning gir én frist med samme identitet og verdier i
Kalender og begge Budsjettår-visninger. Endringsforslag fra synlige detaljer bevarer
original synlighet. [Akseptanse](acceptance/2026-10-27-atelier-ux06.md) beskriver
rolle-/serververn, feil, kontekst, dobbeltinnsending, mobil og tastatur. Etter samlet
Standards-/Spec-review og separat sikkerhetsaudit er tømt månedsfelt rettet med en
reell nettleserregresjon; 643 ordinære og 56 Edge/Kestrel-scenarioer passerer.
Rettingen er etterkontrollert, og ordinær, SQL Server- og Chromium-CI er grønne.
Atelier UX07 ([#82](https://github.com/depmartin/Budsjettkalender/issues/82)) er levert i
[PR #102](https://github.com/depmartin/Budsjettkalender/pull/102), merget som
`37d12d7c1d46fa75576099c8cf18cb7cc194b96b`, med konkret arbeidsår for Frister, Forslag,
Dokumenter og Generer fra årsmal. Register og løpende kø bruker budsjettår uavhengig av
kalenderdato, kombinerte filtre og synlige snarveier til andre års forslag. 651 ordinære
og 61 Edge/Kestrel-scenarioer passerer. Etter samlet review, separat sikkerhetsaudit og
rettede initialiserings-/dialogkappløp er ordinær, SQL Server- og Chromium-CI grønn i
[kjøring 40579908804](https://github.com/depmartin/Budsjettkalender/actions/runs/40579908804).
[Akseptanse](acceptance/2026-10-27-atelier-ux07.md) beskriver bevisene.
Atelier UX08 ([#83](https://github.com/depmartin/Budsjettkalender/issues/83)) er levert i
[PR #103](https://github.com/depmartin/Budsjettkalender/pull/103), merget som
`2990c1b8f644788d9405a78434b2b92cab664690`, med én varig, versjonert felles fasemal under Administrator-innstillinger →
Faseinndeling. Administrator lagrer fem navn/månedsgrenser; alle roller og budsjettår
leser samme mal. Serverkommandoen håndhever administratorpolicy. Ugyldige felt og gammel
versjon avvises med beholdte felt. Reell egen-appomstart på samme SQLite-fil bevarer
mal/versjon; komplette frist-, synlighets-, forslag-, dokument- og regeldata er sammenliknet
før/etter uten endring, og frister utenfor vinduet består. 675 ordinære tester og 65
reelle Edge/Kestrel-scenarioer passerer uten feil eller hopp.
[Akseptanse](acceptance/2026-10-27-atelier-ux08.md) beskriver fire nye nettleserflyter,
14 delte lagringstilfeller og SQL-oppgradering fra siste tidligere migrasjon. Samlet
Standards-/Spec-review, visuell gjennomgang og separat audit hadde ingen funn.
[CI 40585115379](https://github.com/depmartin/Budsjettkalender/actions/runs/40585115379)
er grønn med 37 ekte SQL Server-prøver og 65 Chromium-scenarioer.
Atelier UX09 ([#84](https://github.com/depmartin/Budsjettkalender/issues/84)) er levert i
[PR #104](https://github.com/depmartin/Budsjettkalender/pull/104), merget som
`ec8588b947b249e29be11940d97f086004f23d3a`, med separate flater for eksisterende og nye gjentaksregler. Fire kodefrie
feltvalg åpner, redigerer og kopierer de samme tre lagrede regeltypene. Lesbar setning
og sideeffektfri femårsvisning bruker eksisterende beregning, også for skuddår,
årsforskyvning, valgår og ankerkjeder. Ulagret arbeid, felt-/lagringsfeil og sletting
har forklaring, tastatur og fokus. 705 ordinære tester og 89 reelle Edge/Kestrel-scenarioer
passerer lokalt uten feil eller hopp; alle tidligere 675/65 er bevart. 26 faktiske
desktop-/mobilbilder er visuelt inspisert. [Akseptanse](acceptance/2026-10-27-atelier-ux09.md)
beskriver rød/grønn og datainvariantene. Standards-/Spec-review og separat audit fant
samme P2 ved navigasjon under lasting; et utkast kunne ellers bli lagret på en annen regel.
Rettingen binder lasting, preview og lagring til samme visning og er dekket av 14 nye
reelle nettlesertilfeller. Etterkontroll hadde ingen åpne funn; ordinær, ekte SQL Server-
og Chromium-CI er grønn i [kjøring 40594188807](https://github.com/depmartin/Budsjettkalender/actions/runs/40594188807).
Atelier UX10 ([#85](https://github.com/depmartin/Budsjettkalender/issues/85)) er levert i
[PR #105](https://github.com/depmartin/Budsjettkalender/pull/105), merget som
`54c30b19e8d54e1185e38931ab4e01bf5fdfc105`, med søk, kategoriunion og sammenhengsfilter over hele gjentaksmalen. Milepæl er
avledet bare for selvstendige ankerrepresentanter; relative mellomledd forblir Avhengig.
Et eksplisitt beregningsår fra 1 til 9999 gir full kalenderkronologi, to datoretninger og
norsk tittelrekkefølge med stabil identitet. Tentative måneder beholder presisjon;
uberegnbare regler har forklaring og står sist i begge datoretninger. Utvalg, år og
sortering bevares ved dialog, redigering, avbryt, lagring og browser-back. Skjult lagret
rad forklares, og riktig handling/overskrift får fokus. Kun faktisk redigering endrer
målregelen. 716 ordinære og 106 reelle Edge/Kestrel-scenarioer passerer lokalt uten feil
eller hopp; alle tidligere 705/89 er bevart. 54 navngitte desktop-/mobilbilder er inspisert.
Samlet Standards-/Spec-review, visuell gjennomgang og separat sikkerhetsaudit hadde
ingen funn. [CI 40601039335](https://github.com/depmartin/Budsjettkalender/actions/runs/40601039335)
er grønn med alle 37 ekte SQL Server-prøver og 106 Chromium-scenarioer.
[Akseptanse](acceptance/2026-10-27-atelier-ux10.md) beskriver regresjoner,
samme duplikatrepresentant som generering og komplette datainvarianter.
Atelier UX11 ([#86](https://github.com/depmartin/Budsjettkalender/issues/86)) er levert i
[PR #106](https://github.com/depmartin/Budsjettkalender/pull/106), merget som
`75b579a0065e34f3282b7ed78fe1535d50191ee3`. Sideeffektfri Prøv malen og konkret generering
bruker samme lagrede mal med midlertidige ankre. Fjorårspresisjon, tom/atomisk regenerering
og aktiv godkjenning er bevart. Ventelås-, fokus- og JS-livsløpsfunn er rettet og
etterkontrollert uten åpne funn. 731 ordinære og 144 Edge/Kestrel-scenarioer passerer lokalt;
[CI 40618906003](https://github.com/depmartin/Budsjettkalender/actions/runs/40618906003)
passerte alle tre porter, inklusive 46 ekte SQL Server-prøver og 144 Chromium-scenarioer.
[UX11-akseptansen](acceptance/2026-10-27-atelier-ux11.md) bevarer detaljene.
Atelier UX12 ([#87](https://github.com/depmartin/Budsjettkalender/issues/87)) er levert i
[PR #107](https://github.com/depmartin/Budsjettkalender/pull/107), merget som
`092b22b5f5bea2a5920e731207635c26cabc76b0`, med Overfør mal under Årsmal og kontekstuell inngang fra gamle `/admin/data`.
Hele versjon-1-malen lastes ned uavhengig av listefiltre. Filvalg viser gyldige/utelatte
regler og erstatt-alt-konsekvens før eksplisitt native bekreftelse; null gyldige og tom
liste krever egen tømmingsbekreftelse. Eldre titler/parametre, alle eksportfelter og
flerårsberegninger bevares. Avbryt, filfeil og atomisk lagringsfeil bevarer gammel mal;
filbytte, dobbelbekreftelse og ny bekreftelse ved observert endret antall er prøvd med
kontrollert asynkron fullføring i ekte Kestrel/Blazor. Importert mal åpner regelbyggeren,
prøves og gir faktiske genererte forslag uten publisering. Fasemalen er separat.
747 ordinære tester og hele suiten med 172 reelle Edge/Kestrel-scenarioer passerer uten
feil eller hopp; alle tidligere 731/144 navn er bevart. Alle 46 SQL-navn er beholdt.
16 faktiske desktop-/mobilbilder er inspisert uten funn.
Samlet Standards-review og separat sikkerhetsaudit hadde ingen funn. Spec-reviewets
ene P3 om en foreldet navigasjonsventestatus er rettet med reell rød/grønn-regresjon
etter både suksess og lagringsfeil/nytt forsøk. Fil/preview, kvittering/feil og alle
ventelåser består. Seks nye desktop-/mobilbilder er inspisert uten nye visuelle funn.
[CI 40626361976](https://github.com/depmartin/Budsjettkalender/actions/runs/40626361976)
passerte ordinære tester, alle 46 ekte SQL Server-prøver og 172 Chromium-scenarioer.
[UX12-akseptansen](acceptance/2026-10-28-atelier-ux12.md) beskriver de leverte bevisene.
Atelier UX13 ([#88](https://github.com/depmartin/Budsjettkalender/issues/88)) er levert i
[PR #108](https://github.com/depmartin/Budsjettkalender/pull/108), merget som
`84765aacf949f684eff4436168d82e93d5e91238`, med dokumentinnlesing under arbeidsåret, eksakt kildeutvalg i køen og originale
uttrekksbevis også ved justering. Filvalg behandler ingenting; innlesing lager forslag,
og administrator godkjenner før publisering. Tekst-, dato- og 40-kandidatgrensene vises
før handling og ved resultat. Lokal PDF har dokumentkobling uten oppdiktet offentlig URL.
Den forhåndsdefinerte prototypen ble vist før produksjonsarbeidet og er bevart separat.
766 ordinære og 197 reelle Edge/Kestrel-scenarioer passerer lokalt uten feil/hopp;
alle tidligere 751/181 navn og 46 SQL-navn er bevart. 42 tidligere og seks nye faktiske
desktop-/mobilbilder er inspisert. Samlet Standards-/Spec-review hadde ingen funn.
Separat audit fant to P2 som nå er rettet med rød/grønn: fristskjemaets sene svar er
bundet til lastet målidentitet, og gruppelesing skjuler administrativ opplastingsetikett
med filnavn. Fullt administratorinnsyn og faktisk kontrollert offentlig URL er beholdt.
Fokusert sikkerhets-/Spec-etterkontroll er gjennomført uten åpne funn.
[CI 40640595314](https://github.com/depmartin/Budsjettkalender/actions/runs/40640595314)
passerte ordinære tester, alle 46 ekte SQL Server-prøver og 197 Chromium-scenarioer.
[UX13-akseptansen](acceptance/2026-10-28-atelier-ux13.md) skiller prototype, faktisk PDF-flyt
og verifikasjonens grenser.

Atelier UX14 ([#89](https://github.com/depmartin/Budsjettkalender/issues/89)) har en lokal
kandidat med samlet gruppeoversikt, native opprettings-/redigeringsdialog og eksplisitt
publisert gruppeinnsyn med lesende detaljer/kilder. Gruppevedlikehold har serverautorisering
ved hvert skriv; ID, kode, standardidentitet, historisk deling og medlemskapskilder beholdes.
Sene liste-/detaljsvar kan ikke overta et nyere gruppevalg. Ingen medlemskapsforvaltning
eller migrasjon er innført. [UX14-akseptansen](acceptance/2026-10-28-atelier-ux14.md)
beskriver rød/grønn, nøyaktige gruppeutvalg, felt-/lagringsfeil og bevisgrenser.
Et statisk P2 om Escape før levert ventemerke er reprodusert med faktisk asynkron
lagring og kontrollert Blazor-transport. Serveren avgjør nå avbruddet før native
lukking; feil beholder dialog, felt og fokusert melding med mulighet for ny innsending.
782 ordinære og 221 reelle Edge/Kestrel-scenarioer passerer uten feil/hopp; alle
tidligere 766/197 navn og de 46 SQL-navnene er bevart. Alle 38 opprinnelige og 12 nye Escape-bilder
er inspisert uten åpne visuelle funn. Opprinnelig Standards-review og separat
sikkerhetsaudit hadde ingen funn. Root håndterer fokusert etterkontroll av Spec-
rettingen og kandidat-CI.

| Oppgave | Implementert atferd | Leveranse |
| --- | --- | --- |
| [#21](https://github.com/depmartin/Budsjettkalender/issues/21) | Pocock-arbeidsflyt, kort agentindeks, dokumenterte standarder og rettinger fra gjennomgangen | [PR #25](https://github.com/depmartin/Budsjettkalender/pull/25) |
| [#22](https://github.com/depmartin/Budsjettkalender/issues/22) | Én DbContext per offentlig tjenesteoperasjon | [PR #31](https://github.com/depmartin/Budsjettkalender/pull/31) |
| [#23](https://github.com/depmartin/Budsjettkalender/issues/23) | Konkurransekontroll ved behandling av samme forslag, også etter revidert innsending | [PR #32](https://github.com/depmartin/Budsjettkalender/pull/32) |
| [#24](https://github.com/depmartin/Budsjettkalender/issues/24) | Felles parameterkontroll for manuell mallagring og JSON-import | [PR #33](https://github.com/depmartin/Budsjettkalender/pull/33) |
| [#34](https://github.com/depmartin/Budsjettkalender/issues/34) | Kontrollert datooverløp per regel, trygg årsregning og femte ukedag ved øvre datogrense | Livsløp i oppgaven |
| [#36](https://github.com/depmartin/Budsjettkalender/issues/36) | 12 utførte nettleserscenarioer, demo-utlogging, manuelle ankre og ansvar i internkalender rettet | [Akseptanserapport og eksterne porter](acceptance/2026-10-20.md) |
| [#46](https://github.com/depmartin/Budsjettkalender/issues/46) | Deterministiske usikkerhetsflagg med forklaring ved robotforslagenes uttrekksbevis, også for eldre forslag | Livsløp i oppgaven |
| [#40](https://github.com/depmartin/Budsjettkalender/issues/40) | Key Vault-hemmeligheter lastes før Entra/database, med dokumentert kildeprioritet og trygge oppstartsfeil | Livsløp i oppgaven |
| [#44](https://github.com/depmartin/Budsjettkalender/issues/44) | Administratorflate for oppretting, navneendring og deaktivering av synlighetsgrupper med stabil kode og bevarte referanser | Lokal kandidat; supplerende bevis i [akseptanserapporten](acceptance/2026-10-20.md#supplerende-akseptanse-for-44) |
| [#69](https://github.com/depmartin/Budsjettkalender/issues/69) | Valgfri, servervalidert HTTPS-kildelenke fra fristens tilknyttede dokument til kalenderens fristkort | [PR #70](https://github.com/depmartin/Budsjettkalender/pull/70); supplerende bevis i [akseptanserapporten](acceptance/2026-10-20.md#supplerende-akseptanse-for-69) |
| [#71](https://github.com/depmartin/Budsjettkalender/issues/71) | Avledet «venter endring»-merke på publiserte fristkort bare for administrator, undertrykt ved gruppelesing | Lokal kandidat; supplerende bevis i [akseptanserapporten](acceptance/2026-10-20.md#supplerende-akseptanse-for-71) |
| [#73](https://github.com/depmartin/Budsjettkalender/issues/73) | Kilde eller innsender i administratorens kø, kombinert med øvrige filtre og bevart gjennom behandling | Lokal kandidat; supplerende bevis i [akseptanserapporten](acceptance/2026-10-20.md#supplerende-akseptanse-for-73) |
| [#76](https://github.com/depmartin/Budsjettkalender/issues/76) | Atelier-skall, rollebasert navigasjon og bevarte innganger | Merget i [PR #96](https://github.com/depmartin/Budsjettkalender/pull/96), `c1f250e` |
| [#77](https://github.com/depmartin/Budsjettkalender/issues/77) | Månedskalender, minikalender, dagsagenda og fristdetaljer med gyldig kilde og bevart tilgang | Merget i [PR #97](https://github.com/depmartin/Budsjettkalender/pull/97), `a65ae35`; [akseptanse](acceptance/2026-10-27-atelier-ux02.md) |
| [#78](https://github.com/depmartin/Budsjettkalender/issues/78) | Eksplisitt flervalg av budsjettår/kategorier, søk, nullstilling og bevart kalenderkontekst | Merget i [PR #98](https://github.com/depmartin/Budsjettkalender/pull/98), `f72b2b7`; [akseptanse](acceptance/2026-10-27-atelier-ux03.md) |
| [#79](https://github.com/depmartin/Budsjettkalender/issues/79) | Uke og periodeavgrenset Liste med bevart dato/filter/søk, tentative måneder og fullstendige fristdetaljer | Merget i [PR #99](https://github.com/depmartin/Budsjettkalender/pull/99), `19d7949`; [akseptanse](acceptance/2026-10-27-atelier-ux04.md) |
| [#80](https://github.com/depmartin/Budsjettkalender/issues/80) | Komplett enkeltårsutvalg i Tidslinje/Fristliste, alle samtidige og tentative frister, bevart kalenderutvalg og eksplisitt eldre flereårs-/historikkvalg | Merget i [PR #100](https://github.com/depmartin/Budsjettkalender/pull/100), `b1404d8`; [akseptanse](acceptance/2026-10-27-atelier-ux05.md) |
| [#81](https://github.com/depmartin/Budsjettkalender/issues/81) | Forslag fra valgt kalenderdato og synlig fristdetalj, gjenbrukt kø/godkjenning, bevart presisjon/synlighet og én publisert frist i Kalender/Tidslinje/Fristliste | Merget i [PR #101](https://github.com/depmartin/Budsjettkalender/pull/101), `1a65626`; [akseptanse](acceptance/2026-10-27-atelier-ux06.md) |
| [#82](https://github.com/depmartin/Budsjettkalender/issues/82) | Konkret arbeidsår i fire lokale faner, årsavgrenset register/kø, synlige andre års forslag, bevart behandling/filter/synlighet og én frist på tvers av visninger | Merget i [PR #102](https://github.com/depmartin/Budsjettkalender/pull/102), `37d12d7`; [akseptanse](acceptance/2026-10-27-atelier-ux07.md) |
| [#83](https://github.com/depmartin/Budsjettkalender/issues/83) | Én varig felles fasemal med versjonert atomisk lagring, administratorvern, alle års lesing, feil/konflikt og bevarte fristdata | Merget i [PR #103](https://github.com/depmartin/Budsjettkalender/pull/103), `2990c1b`; [akseptanse](acceptance/2026-10-27-atelier-ux08.md) |
| [#84](https://github.com/depmartin/Budsjettkalender/issues/84) | Fire kodefrie regelvalg, femårsvisning, bevart parser-/anker-/valgårskontrakt, redigering/kopi/slettekonsekvens og vern av ulagret arbeid | Merget i [PR #104](https://github.com/depmartin/Budsjettkalender/pull/104), `ec8588b`; [akseptanse](acceptance/2026-10-27-atelier-ux09.md) |
| [#85](https://github.com/depmartin/Budsjettkalender/issues/85) | Kategori-/sammenhengs-/søkesnitt, avledet milepæl, målårsberegning, stabil norsk/kronologisk sortering, bevart retur og uendrede data ved visning | Merget i [PR #105](https://github.com/depmartin/Budsjettkalender/pull/105), `54c30b1`; [akseptanse](acceptance/2026-10-27-atelier-ux10.md) |
| [#86](https://github.com/depmartin/Budsjettkalender/issues/86) | Hel sideeffektfri prøve, midlertidige ankre, samme lagrede mal ved konkret generering, presisjonsriktig fjorår, bekreftet atomisk erstatning og enkeltgodkjenning | Merget i [PR #106](https://github.com/depmartin/Budsjettkalender/pull/106), `75b579a`; [akseptanse](acceptance/2026-10-27-atelier-ux11.md) |
| [#87](https://github.com/depmartin/Budsjettkalender/issues/87) | Hel maleksport, sideeffektfri importpreview, kompatibilitet, eksplisitt erstatning/tømming og atomisk gjenoppretting | Merget i [PR #107](https://github.com/depmartin/Budsjettkalender/pull/107), `092b22b`; [akseptanse](acceptance/2026-10-28-atelier-ux12.md) |
| [#88](https://github.com/depmartin/Budsjettkalender/issues/88) | Dokumentvalg, reelt uttrekk, arbeidsår/kilde i kø, bevis og aktiv godkjenning til én publisert frist | Merget i [PR #108](https://github.com/depmartin/Budsjettkalender/pull/108), `84765aa`; [akseptanse](acceptance/2026-10-28-atelier-ux13.md) |
| [#89](https://github.com/depmartin/Budsjettkalender/issues/89) | Atelier-gruppevedlikehold med stabile referanser og rent, serverfiltrert publisert gruppeinnsyn | Lokal kandidat; [akseptanse](acceptance/2026-10-28-atelier-ux14.md) |

## Samlet akseptanse

[Akseptanserapporten](acceptance/2026-10-20.md) skiller faktisk UI, HTTP/tjenestebevis og
eksterne miljøporter. UX14 utvider testomfanget til 221 reelle Playwright-/Edge-scenarioer
og 782 ordinære tester, alle bestått lokalt uten feil eller hopp. Sluttkjøringen dokumenteres i UX14-akseptansen.
Egen obligatorisk Chromium-jobb er lagt til i CI;
deploy venter på ordinære tester, SQL Server og nettlesertester. Kandidaten skal ha alle
tre jobber grønne samt Standards-/Spec-review og separat sikkerhetsaudit før merge.

## Kildelenke på synlige fristkort

[#69](https://github.com/depmartin/Budsjettkalender/issues/69) projiserer bare Url fra
dokumentraden som er knyttet til den synlige, godkjente fristen. IFristlesing returnerer
KildeUrl for liste, landing og enkeltoppslag etter kontroll av absolutt HTTPS, eksakt
regjeringen.no/www.regjeringen.no, standardport 443 og fravær av brukerinfo. Ugyldig
URI-syntaks avvises også. Tillatte adresser får Uri sin kanoniske form; lagrede adresser
endres ikke. Manglende kobling/rad, tom eller avvist URL gir null og beholder fristen.
Ingen adresse utledes fra dokument-ID, kilde, filnavn eller opphav.

Kalenderens Fristkort viser «Les kildedokument» med vanlig HTML-koding, no-referrer,
noopener og noreferrer ved åpning i ny fane. Alle autoriserte funksjonsroller får samme
kildelenke for samme frist. Synlighet filtreres fortsatt i SQL før dokumentprojeksjon;
POL velges fortsatt eksplisitt. Datamodell, migrasjoner, eksport, innhenting og årshjulets
separate visning er uendret.

40 SQLite-tilfeller ved IFristlesing og 15 HTTP-tilfeller kontrollerer tillatte og avviste
adresser, alle tre leseutvalg, manglende dokument, roller, anonym tilgang og fravær av
skjulte markører i hele svaret. Ett nytt reelt nettleserscenario prøver lenken for leser,
bidragsyter og administrator, kildefrie kort og skjult DOM samt POL-isolasjon. Bare
syntetiske dokumentmetadata brukes; ingen ekstern navigasjon eller kildehenting utføres.
Se akseptanserapporten for konkrete røde/grønne bevis og lokal testgrense.

P12a er levert i PR #70. #50 og #38 forblir åpne; planleggingsgren og draft-PR #65 er urørt.
Ingen deploy er utført.

## Ventemerke på publiserte fristkort

[#71](https://github.com/depmartin/Budsjettkalender/issues/71) utleder VenterEndring ved
lesing: minst ett forslag med status Forslag, type Endring og EndrerFristId lik fristens
ID. Liste, landing og enkeltoppslag deler projeksjonen etter synlighetsfilteret.
Bare SerAlt-konteksten får indikatoren. Gruppelesing og administrators «se som gruppe»
får alltid false; forslagets innsender, ID, tittel og foreslåtte verdier returneres ikke.
Fristkort viser «venter endring» under eksisterende administratorpolicy. VisGrupper gir
ingen rett til ventemerket. Ingen ny handling, lagret status eller migrasjon er innført.

17 SQLite-tilfeller kontrollerer null/ett/to åpne forslag, øvrige statuser/typer,
gruppelesing og faktisk godkjenning, justering og avvisning via IGodkjenningsko i begge
rekkefølger. Ny lesing bevarer merket etter første behandling og fjerner det etter siste;
fristens identitet og FA/POL-synlighet bevares. 19 HTTP-tilfeller kontrollerer roller,
anonym, POL, enkeltoppslag og hele responsens fravær av skjulte markører/forslagmetadata.
Ett nytt nettleserscenario prøver administrator, leser og bidragsyter, innsyn med
VisGrupper, POL-isolasjon og ny innlasting etter godkjenning/avvisning.
Se akseptanserapporten for røde/grønne bevis og lokal testgrense.

P12b er en lokal kandidat. Samlet Standards-/Spec-review, separat sikkerhetsaudit og alle
tre kandidatjobber i CI gjenstår før mergeavklaring. #50 og #38 forblir åpne; planleggingsgren
og draft-PR #65 er urørt. Ingen deploy er utført.

## Kildevalg i godkjenningskøen

[#73](https://github.com/depmartin/Budsjettkalender/issues/73) gir administrator valget
«Kilde eller innsender». «Alle» og distinkte ikke-blanke råverdier hentes fra hele den
åpne, ikke-genererte køen. Valget sendes som eksisterende Kofilter.Kilde, som sammenlikner
Forslag.KildeEllerInnsender eksakt. Køetikett/innsender, dokumentleverandør, dokument-ID og
opphav er fortsatt ulike dimensjoner; tjenestekontrakten og adapteren er uendret.

Kilde og de fire eksisterende filtrene danner et snitt. Kildelisten beholder de andre
kildene også ved tomt utvalg. Aktivt valg beholdes ved godkjenning, avvisning og retur fra
justering, også når siste treff forsvinner. Justeringsreturen oppbevarer filteret én gang
i samme Blazor-krets; vanlig navigasjon forkaster det. Ingen varig filterlagring er innført.
POL forhåndsvelges fortsatt ikke, og endringsforslag bevarer fristens synlighet.

17 SQLite-tilfeller, fem faktiske HTTP-tilfeller på GET /admin/ko og fire nye
nettleserscenarioer prøver kilde-/snittsemantikk, tomt utvalg, tilgang, trygg tekst og
behandling. De første 16 tjenestetilfellene passerte allerede før UI-endringen.
Nettleseren var rød ved manglende kildevalg og deretter ved tapt valg etter justering;
begge flytene er grønne. Se akseptanserapporten for resultatfiler og nøyaktig bevisgrense.

P12c er en lokal kandidat. Samlet Standards-/Spec-review, separat sikkerhetsaudit og alle
tre kandidatjobber i CI gjenstår før mergeavklaring. #50 og #38 forblir åpne;
planleggingsgren og draft-PR #65 er urørt. Ingen deploy er utført.

## Forvaltning av synlighetsgrupper

[#44](https://github.com/depmartin/Budsjettkalender/issues/44) gir administrator en egen
flate på `/admin/grupper` og lenken «Grupper». Egendefinerte grupper opprettes aktive.
Kode trimmes, lagres med store bokstaver og har 1–64 ASCII-bokstaver, sifre eller bindestrek,
med bokstav først. Visningsnavn trimmes og har 1–256 tegn. Duplikater avvises også for
inaktive grupper. Navn og aktiv-status kan endres for eksisterende grupper; kode, ID og
innebygd identitet endres aldri, og det finnes ingen sletteoperasjon.

IGruppetjeneste bruker én kontekst per operasjon. Nye grupper inngår i de eksisterende
synlighetsvalgene, og deaktivering fjerner dem fra nye valg og forhåndsvalg ved godkjenning,
justering, generering og ny innsending av reviderte brukerforslag. Fristers tidligere koder
og serverfiltrerte leseutvalg beholdes.
Innhold kan redigeres med eksisterende inaktive tilordninger; skjemaet viser dem med status
og mulighet for å fjerne dem. Nye inaktive tilordninger og gjeninnføring etter fjerning avvises.
Eksisterende Entra-medlemskap beholdes ved deaktivering så lenge claimet fortsatt gjelder;
bortfalt claim fjerner medlemskapet som før. Nye Entra-medlemskap krever aktiv gruppe.
Det eksplisitte POL-valget og databasefilteret er uendret. Ny medlemskapsadministrasjon er
fortsatt [#45](https://github.com/depmartin/Budsjettkalender/issues/45).
Datamodell og migrasjoner er uendret.

26 tjenestetilfeller og åtte HTTP-tilfeller prøver lagret utvalg, gyldige/ugyldige koder,
navn, deaktivering, reaktivering, innebygd identitet og tilgang. Den første nye tjenesteprøven
feilet fordi oppretting manglet; neste prøve feilet ved manglende oppdatering. Negative
prøver fanget lagring av ugyldige verdier og rå databasefeil ved duplikater. HTTP-prøven
viste først 404 for den manglende siden, og viser nå 401/403/200 for anonym/ikke-admin/admin.
Direkte POST uten interaktivt skjema avvises med 400 uten endring; den er ikke et skrive-API.
Nettleserprøven feilet først ved det manglende opprettingsskjemaet og passerer etter
implementering. Se den supplerende akseptansematrisen for bevis og avgrensning.

To tjenesteregresjoner fra review feilet først fordi samme Entra-principal mistet SKOK
etter deaktivering og innholdsredigering av en SKOK-frist ble avvist. Etter retting passerer
ekte brukeroppslag, gjenoppslag, SQLite-persistens og serverfiltrert lesing. Negative prøver
bekrefter at nye inaktive tildelinger avvises og bortfalt claim fortsatt fjerner medlemskap.
Nettleseren viste først skjult inaktivt valg ved fristredigering og avvist godkjenning i kø,
justering, generering og ny innsending, selv etter aktivt FA-valg. Alle fem flyter passerer etter retting.
Sluttbevis: TestResults/44-review-final2-ordinary.trx og
BrowserTestResults/44-review-final2-browser.trx. Akseptanserapporten angir de enkelte
røde/grønne resultatfilene og den målrettede kontraktkjøringen.

P06 er implementert som lokal kandidat. Manuell pilotakseptanse og de eksterne portene
P13–P15 under [#38](https://github.com/depmartin/Budsjettkalender/issues/38) står fortsatt åpne.
Ingen produksjonsgodkjenning eller deploy er utført.

## Usikkerhetsflagg for robotforslag

[#46](https://github.com/depmartin/Budsjettkalender/issues/46) vurderer lagrede uttrekksbevis
med Usikkerhetsregler når godkjenningskøen leses. Forklaringene vises ved tolket verdi og
kildeutdrag: relativ formulering tolket til konkret dato, kildeutdrag uten gjenkjennelig
dato og dato utenfor det eksisterende vinduet fra budsjettår −2 til +1, inklusive hele år.
Lav konfidens alene gir ingen flagg. Firesifrede år utenfor 1900–2099 kontrolleres også.
Avledning ved lesing dekker både nye opplastinger og eldre forslag uten migrasjon eller
tilbakefylling. Kandidatutvalg, forkasting, publisering og aktivt POL-valg er uendret.

Første regresjon gjennom IDokumentopplasting → IGodkjenningsko feilet fordi relativ kilde
med konfidens 0,99 manglet flagg; samme test er grønn etter retting. En ny vindusregresjon
fanget at 2100 ikke ble flagget. Det utvidede PDF-scenarioet feilet først med 0 av 3 synlige
flagg og viser nå alle tre forklaringene. Nettleseren bekrefter også at kildeutdrag med
HTML-tegn vises som tekst, gyldig forslag er uten flagg og ingen frist publiseres.
De 12 eksisterende nettleserscenarioene er beholdt; ingen ny scenarioantallsgrense er innført.

Lokal #46-evidens: TestResults/46-01-red.trx, 46-01-green.trx, 46-02-red.trx,
46-02-green.trx, 46-03-browser-red.trx, 46-03-browser-green.trx,
46-final-ordinary.trx og 46-final-browser.trx. App-logger, syntetiske demo-databaser og
skjermbilde er bare lagret lokalt under temp/budsjettkalender-46-browser. Dette er
SQLite-/Edge-bevis for kandidaten, ikke SQL Server-, Entra-, Azure- eller M365-bevis.

Rapporten har konkrete operatørhandlinger for Entra/Azure SQL, Outlook/M365 og infrastruktur.
Key Vault-provider er implementert i #40; faktisk Azure-tilgang krever fortsatt miljøtest.
Vedvarende Data Protection-oppsett og live-kildeworker er fortsatt implementerings-/oppsettsgap
som demoinnloggingen ikke dekker.

## Hemmeligheter ved oppstart

[#40](https://github.com/depmartin/Budsjettkalender/issues/40) bruker den offisielle
Key Vault-provideren med eksplisitt systemtilordnet managed identity. Hemmeligheter
lastes før oppstarten leser demo-, database- eller Entra-konfigurasjon. Key Vault har
høyest prioritet, og standard `--` → `:`-mapping bevares. Manglende URI gjør ingen
Azure-kall. Ugyldig eksplisitt URI eller lastingsfeil stanser oppstarten med en fast
norsk feil uten rå URI, leverandørunntak eller indre unntak. HTTP-pipelinelogging og
Activity-sporing er deaktivert på disse klientene. Hele `Azure.Identity`-kategorien
undertrykkes ved appens `ILoggerFactory`, også ved leverandørspesifikke loggregler
og konfigurasjonsreload; øvrig applogging beholdes. Rå SDK-EventSource/ETW/EventPipe,
direkte konsollyttere og eksterne diagnoseagenter ligger utenfor appens logggrense
og kan fortsatt se følsomme feilresponser. Se [driftskontrakten](../infra/README.md#key-vault-ved-weboppstart).

22 nye oppstartstilfeller bruker WebApplicationFactory og samme komposisjonsrot som
Main. Ekte SecretClient/provider kjører mot styrt HTTP-transport med syntetisk
legitimasjon, inklusive SDK-ens autentiseringsutfordring. Testene viser databasens
oppstartslesing, Entras opsjonsbinding, prioritet over JSON/miljø/kommandolinje,
navnemapping, nettverksfri Demo/Testing og trygge URI-/liste-/hente-/nettverks-/identitetsfeil.
Produksjonsvarianten av Entra-bindingen erstatter bare den eksterne databasen med SQLite.
Ingen ekstra tjenesteprovider eller produksjonsflagg for å hoppe over lastingen er lagt til.

Sikkerhetsregresjonen bruker ekte systemtilordnet ManagedIdentityCredential med
syntetisk App Service-endepunkt og styrt HTTP-transport. Før retting nådde responsens
følsomme markør apploggen via LogMsalError/GetTokenFailed, selv med SDK-ens tre
diagnostikkflagg av. Etter retting når disse rå hendelsene fortsatt den privilegerte
testlytteren, mens videresending til appens faktiske loggerfabrikk undertrykkes.
Testen prøver også leverandørspesifikk Trace, reload og bevaring av annen logging/filtrering.
Den bygger en separat loggvert fordi mislykket konfigurasjonslasting skjer før
webvertens loggerfabrikk kan bygges. Ingen ekstra produksjonsprovider opprettes.

Første røde regresjon feilet ved oppstart fordi lokal ugyldig databasetilkobling ble lest
i stedet for hvelvets verdi. URI-regresjonene feilet fordi ugyldig eksplisitt URI ikke ble
avvist. Feilregresjonene viste rå Azure-/nettverks-/aggregerte unntak; samme tilfeller gir nå
trygge oppstartsfeil, og syntetiske følsomme markører mangler i logger og feiltekst.
Lokal evidens: TestResults/40-01-red.trx, 40-01-green.trx, 40-02-red.trx,
40-02-green.trx, 40-03-red.trx, 40-03-green.trx, 40-04-kontrakter.trx,
40-05-identitet.trx, 40-06-identitetslogging-red.trx,
40-06-identitetslogging-green.trx, 40-07-identitetslogging-kontrakter.trx,
40-security-final-ordinary.trx og 40-security-final-browser.trx.
Faktisk managed identity, RBAC, Key Vault/Azure-nettverk og Entra-innlogging er fortsatt
miljøporter i P13–P15 under #38. Ingen Azure- eller produksjonsutrulling er utført.

## DbContext og behandling av forslag

Alle 15 runtime-adaptere bruker IDbContextFactory og await using per offentlig operasjon.
Deterministiske regresjonstester ved IFristlesing og IFristskriving viste først EF-feilen
om en andre operasjon på samme kontekst. Etter retting fullfører neste kall på samme
tjeneste mens første SELECT venter. Hvert kall har egen kontekst og SQLite-tilkobling.
Samlede lagringer og rollback beholdes; se [ADR-0006](adr/0006-operasjonsavgrenset-dbcontext.md).

Forslag har et lagringsinternt Guid-versjonstoken som AppDbContext fornyer ved alle
sporede innsettinger og endringer, på både SQLite og SQL Server. Bare én behandling
av lest versjon kan fullføre. Gammel godkjenning, justering eller avvisning gir
Valideringsfeil, også når raden er avvist, revidert og sendt inn på nytt.
Forslag, frist, synlighet og varsel lagres samlet. Bare konkurransefeil med oppføringer
utelukkende for Forslag oversettes; andre databasefeil beholdes.
Se [ADR-0007](adr/0007-konkurransekontroll-ved-godkjenning.md).

De 21 delte kontrakttilfellene ved IGodkjenningsko dekker konkurrerende behandlinger,
POL, endringsforslags synlighet, revidert innsending, slettet målfrist og rollback ved
lagringsfeil. SQL Server-suiten har i tillegg en oppgraderingstest fra produksjonsskjemaet.
EF-generert migrasjon, snapshot og målmodell ble verifisert i #23.

## Regelvalidering og import

Regelparser i Application er den felles kontrollen for regeltype, påkrevde JSON-felter,
ukedag og gyldig måned/dag. IMaltjeneste gir Valideringsfeil før en ugyldig oppretting
eller oppdatering endrer malen. IMalDatautveksling bruker samme kontroll, gir én advarsel
per ugyldig regel og erstatter hele malen med de gyldige reglene. Ingen gyldige regler
betyr derfor en tom mal. Filfeil i versjon, null-liste/-rad eller duplisert ID avviser
fortsatt hele importen uten endring. Alle kandidater kontrolleres før sletting og sporing;
én SaveChangesAsync lagrer erstatningen atomisk. Beslutningen står i
[ADR-0008](adr/0008-felles-validering-av-gjentaksregler.md).

53 ugyldige parameterkombinasjoner prøves både ved manuell oppretting/oppdatering og
import. De omfatter manglende felter, ugyldig JSON, JSON-null, feil felttyper, nullukedag,
umulig måned/dag og ukjent regeltype. Testene bekrefter uendret mal etter manuell avvisning,
advarsel per ugyldig importregel og at gyldige regler overlever blandet import.
De første røde testene viste at FastDato {} ble lagret og at import godtok ukjent enumverdi.

Gyldige standardregler, fra_dag uten uke, fra_dag foran uke, femte ukedags fallback til
siste forekomst, skuddår, årsforskyvning −1/0/+1 og relative ankerkjeder er bevart.
Uke uten fra_dag kreves nå i området 1–5; dette strammer inn tidligere implisitt klamping.
29. februar er en gyldig maldato; beregning for et ikke-skuddår gir kontrollert feil.
Manuell lagring krever tittel. Import støtter eldre filer uten tittel og normaliserer
null/tom tittel til tom tekst. En rød test viste tidligere NullReferenceException ved null.
En AFTER DELETE-trigger viser rollback av import, og neste import på samme tjeneste lykkes.
Eldre lagrede regler med JSON-feil rapporteres som genereringsfeil uten å stanse gyldige regler.

## Datooverløp ved generering

[Oppgave #34](https://github.com/depmartin/Budsjettkalender/issues/34) håndteres ved
Genereringsberegning.Beregn og IGenereringstjeneste. Relativ dagforskyvning utenfor
0001-01-01–9999-12-31 gir norsk feil per regel og ingen dato. FastDato og RelativUkedag
summerer målår og årsforskyvning som long og avviser kalenderår utenfor 1–9999 kontrollert.
Femte ukedag klampes før datokonstruksjon, også i desember 9999: mandag blir 27. desember,
mens femte fredag fortsatt blir 31. desember. Se feilkontrakten i
[ADR-0008](adr/0008-felles-validering-av-gjentaksregler.md).

Feil i ankeret forplanter seg gjennom hele kjeden uavhengig av reglenes rekkefølge.
Manuelle ankre overstyrer fortsatt beregningen. Gyldige uavhengige regler blir forslag;
feilede regler og deres avhengige regler blir det ikke. Den gyldige delmengden erstatter
årets tidligere åpne genererte forslag i én atomisk lagring. Regenerering gir ingen
dubletter. En helt mislykket beregning gir en tom erstatning, som før; en lagringsfeil
ruller tilbake hele erstatningen. Et nytt kall på samme tjeneste kan lykkes når feilen er fjernet.

Regresjonene viste først ukontrollert AddDays-unntak, feil kalenderdato etter
heltallsoverløp og avvist femte mandag i desember 9999. Testene dekker int.MinValue/MaxValue,
én dag utenfor området, eksakte yttergrenser, store gyldige positive og negative
forskyvninger, null, rekursive ankerkjeder og SQLite-lagring med rollback.
Manuell oppretting, oppdatering og import beholder hele int-området for forskyvningene;
gyldige store forskyvninger beregnes også etter import. Ingen vilkårlig parametergrense
innføres. Datooperasjoner for feed, lister og internoppgaver er utenfor denne rettingen.

## Verifikasjon

SDK 10.0.402: alle tre låste restore og Release-bygg (ordinær løsning, BrowserTests og
SqlServerTests) passerer etter siste UX14-kildeendring med 0 feil. Ordinært ombygg
viser den eksisterende CS0649-advarselen i `Runde._feil`; ingen ny advarsel er innført. Alle
782 ordinære tester og 221 reelle Edge/Kestrel-scenarioer passerer uten feil eller hopp.
Sluttbevis etter Escape-retting ligger i UX14-akseptansen. Alle tidligere UX13s
766/197 navn er bevart, med 16 og 24 tillegg; tidligere forslag-, genererings-,
publiserings- og navigasjonsregresjoner består. SQL-prosjektets 46 navn er bygget,
oppdaget og bevart, uten endring av domenemodell eller migrasjoner.

TRX, navneinventar, direkte restore-/bygglogg, målt kjøretid og manifest av 342
kilde-/konfigurasjonsfiler og fem binær-/stilfiler ligger i `TestResults/89-escape-final/`.
Etterkontroll av 31 avhengighetsbinærer viser også null drift. Ingen auditkontroll er
deaktivert; låst restore har ingen NU1900. CI krever nøyaktig 221 nettleserscenarioer.
Forgjengerens faktiske Chromium-CI kjørte 197 scenarioer på 11 minutter og 34 sekunder;
CI-jobbens 15 minutter er beholdt, og ingen forventningstimeout er økt.

Alle 38 opprinnelige og 12 nye Escape-bilder er visuelt lest. De nye kontrollerte
Escape-bildene kommer fra sluttbygget. Eldre bilder fra samtidig alternativ outputbygging tas ikke med som
sluttbevis. Fokusert etterkontroll av Spec-rettingen og UX14s ferske ordinære/ekte
SQL Server-/Chromium-CI gjenstår før merge. UX13s leverte
[CI40640595314](https://github.com/depmartin/Budsjettkalender/actions/runs/40640595314)
passerte alle 46 ekte SQL Server-prøver og 197 Chromium-scenarioer.

Lokalt SQLite-/Edge-bevis gjelder ikke produksjonens Azure SQL, Entra, Key Vault,
Outlook/M365, OCR eller komplett PDF-kandidatbehandling. Ingen deploy eller arbeid
med #90 er utført. Se [UX14-akseptansen](acceptance/2026-10-28-atelier-ux14.md) og
[utviklingsveiledningen](development.md).

## Gjenstående grenser

- Azure SQL/Entra, feed-/M365-policy og tokenvern, SQL-brannmur, migrering og Data Protection
  krever eget driftsoppsett og miljøtest. Lokal SQLite-verifikasjon erstatter ikke disse.
- Live kildehenting er fortsatt uferdig.
- Eldre [PR #8](https://github.com/depmartin/Budsjettkalender/pull/8) inngår ikke i dette arbeidet.
