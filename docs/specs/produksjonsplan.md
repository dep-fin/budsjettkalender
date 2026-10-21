# Gjennomføringsplan for manuell pilot og automatikk

Besluttet 21. oktober 2026: Budsjettkalender skal først tas i bruk som en avgrenset pilot
med manuell PDF-opplasting. Automatisk kildehenting leveres etter piloten.
[Hovedoppgave #38](https://github.com/depmartin/Budsjettkalender/issues/38) samler arbeidet.
GitHub-oppgavene er kilden for løpende status; tabellen nedenfor er oppstartsplanen.

## Utgangspunkt og omfang

Main ved kartleggingen er `745439f`. Låst restore og Release-bygg med SDK 10.0.402,
447 ordinære tester og 12 nettleserscenarioer i Edge passerte 21. oktober, uten feil
eller hopp. Dette bekrefter dagens lokale atferd. SQL Server er ikke kjørt lokalt på nytt;
Azure SQL, ekte Entra, Outlook og driftsoppsettet har egne porter.

[Kravkartet](../acceptance/kravkart-2026-10-21.md) dekker brukerhistoriene,
kravdokumentets områder og detaljarkitekturens tilleggsflyter. Kartleggingen er statisk
utover den rapporterte testkjøringen. Hver implementeringsoppgave skal demonstrere
sine kriterier gjennom oppgitt offentlig grense før den lukkes.

Piloten bygger på manuell fristforvaltning, brukerforslag, PDF-opplasting med kontrollert
uttrekk, administratorgodkjenning, årsmaler, eksport og internkalender. Manuell opplasting
forblir en permanent inntaksvei etter at automatikk er levert. Innhold fra opplasting,
robot og generering publiseres først etter administratorgodkjenning. POL krever aktivt valg.

Piloten er ennå ikke godkjent for reelle interne data. P01 fastsetter tillatte data,
publikum, driftsansvar og eventuelle uttrykkelige utsettelser av funksjonsgap. Kalenderfeed,
tverrdepartemental tilgang og modellbruk er separate beslutninger. Valg av manuell pilot
avgjør ingen av disse.

## Arbeidspakker og avhengigheter

P01–P19 gjelder den manuelle piloten; A01–A07 gjelder etterfølgende automatikk.
P19 er pilotporten, A07 er sluttporten. En avhengighet må være lukket med relevant bevis
før etterfølgeren starter. A01 og A04 avhenger av P19 for å uttrykke besluttet leveranserekkefølge;
dette er en leveranseport, ikke en teknisk binding.

| Oppgave | Leveranse | Avhenger av | Rolle ved oppretting |
| --- | --- | --- | --- |
| [P01 · #39](https://github.com/depmartin/Budsjettkalender/issues/39) | Avklar pilotomfang, tillatte data og driftsansvar | Ingen | ready-for-human |
| [P02 · #40](https://github.com/depmartin/Budsjettkalender/issues/40) | Last konfigurerte hemmeligheter fra Key Vault | Ingen | ready-for-agent |
| [P03 · #41](https://github.com/depmartin/Budsjettkalender/issues/41) | Bevar Data Protection-nøkler ved omstart og skalering | [P01](https://github.com/depmartin/Budsjettkalender/issues/39) | needs-info |
| [P04 · #42](https://github.com/depmartin/Budsjettkalender/issues/42) | Beslutt publikum og tilgangsmodell for kalenderabonnement | [P01](https://github.com/depmartin/Budsjettkalender/issues/39) | ready-for-human |
| [P05 · #43](https://github.com/depmartin/Budsjettkalender/issues/43) | Håndhev vedtatt feedpolicy og vern token i logger og cache | [P04](https://github.com/depmartin/Budsjettkalender/issues/42) | needs-triage |
| [P06 · #44](https://github.com/depmartin/Budsjettkalender/issues/44) | Forvalt synlighetsgrupper fra administratorflaten | Ingen | ready-for-agent |
| [P07 · #45](https://github.com/depmartin/Budsjettkalender/issues/45) | Forvalt manuelle gruppemedlemskap uten å miste Entra-opphavet | [P01](https://github.com/depmartin/Budsjettkalender/issues/39), [P06](https://github.com/depmartin/Budsjettkalender/issues/44) | needs-info |
| [P08 · #46](https://github.com/depmartin/Budsjettkalender/issues/46) | Vis deterministiske usikkerhetsflagg for opplastede robotforslag | Ingen | ready-for-agent |
| [P09 · #47](https://github.com/depmartin/Budsjettkalender/issues/47) | Bevar uavklarte PDF-kandidater og gi reverserbar forkastet-liste | [P01](https://github.com/depmartin/Budsjettkalender/issues/39), [P08](https://github.com/depmartin/Budsjettkalender/issues/46) | needs-info |
| [P10 · #48](https://github.com/depmartin/Budsjettkalender/issues/48) | Bruk offisiell FIN-mal ved Word-eksport | [P01](https://github.com/depmartin/Budsjettkalender/issues/39) | needs-info |
| [P11 · #49](https://github.com/depmartin/Budsjettkalender/issues/49) | Koble manuell frist til årsmal fra redigeringsskjemaet | [P01](https://github.com/depmartin/Budsjettkalender/issues/39) | needs-info |
| [P12 · #50](https://github.com/depmartin/Budsjettkalender/issues/50) | Fullfør kildelenker, ventende endringsmerke og kildefilter | Ingen | needs-triage |
| [P13 · #51](https://github.com/depmartin/Budsjettkalender/issues/51) | Gjør Azure-testmiljø og utrulling reproduserbar | [P01](https://github.com/depmartin/Budsjettkalender/issues/39), [P02](https://github.com/depmartin/Budsjettkalender/issues/40), [P03](https://github.com/depmartin/Budsjettkalender/issues/41), [P05](https://github.com/depmartin/Budsjettkalender/issues/43) | needs-triage |
| [P14 · #52](https://github.com/depmartin/Budsjettkalender/issues/52) | Verifiser Azure SQL, migrering og gjenoppretting | [P13](https://github.com/depmartin/Budsjettkalender/issues/51) | needs-triage |
| [P15 · #53](https://github.com/depmartin/Budsjettkalender/issues/53) | Verifiser reell Entra-innlogging og alle pilotroller | [P01](https://github.com/depmartin/Budsjettkalender/issues/39), [P07](https://github.com/depmartin/Budsjettkalender/issues/45), [P14](https://github.com/depmartin/Budsjettkalender/issues/52) | needs-triage |
| [P16 · #54](https://github.com/depmartin/Budsjettkalender/issues/54) | Etabler helsesjekk, overvåking og driftsrutiner | [P05](https://github.com/depmartin/Budsjettkalender/issues/43), [P13](https://github.com/depmartin/Budsjettkalender/issues/51) | needs-triage |
| [P17 · #55](https://github.com/depmartin/Budsjettkalender/issues/55) | Prøv Word og kalenderdeling i ekte Office og Outlook | [P05](https://github.com/depmartin/Budsjettkalender/issues/43), [P10](https://github.com/depmartin/Budsjettkalender/issues/48), [P15](https://github.com/depmartin/Budsjettkalender/issues/53) | ready-for-human |
| [P18 · #56](https://github.com/depmartin/Budsjettkalender/issues/56) | Gjennomfør mobil-, tilgjengelighets-, belastnings- og sikkerhetsprøve | [P06](https://github.com/depmartin/Budsjettkalender/issues/44), [P07](https://github.com/depmartin/Budsjettkalender/issues/45), [P08](https://github.com/depmartin/Budsjettkalender/issues/46), [P09](https://github.com/depmartin/Budsjettkalender/issues/47), [P10](https://github.com/depmartin/Budsjettkalender/issues/48), [P11](https://github.com/depmartin/Budsjettkalender/issues/49), [P12](https://github.com/depmartin/Budsjettkalender/issues/50), [P15](https://github.com/depmartin/Budsjettkalender/issues/53), [P16](https://github.com/depmartin/Budsjettkalender/issues/54), [P17](https://github.com/depmartin/Budsjettkalender/issues/55) | needs-triage |
| [P19 · #57](https://github.com/depmartin/Budsjettkalender/issues/57) | Godkjenn og sett den manuelle piloten i drift | [P14](https://github.com/depmartin/Budsjettkalender/issues/52), [P15](https://github.com/depmartin/Budsjettkalender/issues/53), [P16](https://github.com/depmartin/Budsjettkalender/issues/54), [P17](https://github.com/depmartin/Budsjettkalender/issues/55), [P18](https://github.com/depmartin/Budsjettkalender/issues/56) | ready-for-human |
| [A01 · #58](https://github.com/depmartin/Budsjettkalender/issues/58) | Oppdag og hent rundskriv bak IKilde | [P19](https://github.com/depmartin/Budsjettkalender/issues/57) | needs-triage |
| [A02 · #59](https://github.com/depmartin/Budsjettkalender/issues/59) | Koble kildehenting til felles dokumentbehandling og deduplisering | [A01](https://github.com/depmartin/Budsjettkalender/issues/58), [P09](https://github.com/depmartin/Budsjettkalender/issues/47) | needs-triage |
| [A03 · #60](https://github.com/depmartin/Budsjettkalender/issues/60) | Behandle nye dokumentversjoner med matching og foreslått fjerning | [A02](https://github.com/depmartin/Budsjettkalender/issues/59) | needs-triage |
| [A04 · #61](https://github.com/depmartin/Budsjettkalender/issues/61) | Avklar uttrekksleverandør, data og kvalitetskrav | [P19](https://github.com/depmartin/Budsjettkalender/issues/57) | ready-for-human |
| [A05 · #62](https://github.com/depmartin/Budsjettkalender/issues/62) | Lever og evaluer avtalt uttrekk bak IDatouttrekk | [A02](https://github.com/depmartin/Budsjettkalender/issues/59), [A04](https://github.com/depmartin/Budsjettkalender/issues/61) | needs-info |
| [A06 · #63](https://github.com/depmartin/Budsjettkalender/issues/63) | Kjør periodisk innhenting med forsøksteller og synlig status | [A02](https://github.com/depmartin/Budsjettkalender/issues/59), [A03](https://github.com/depmartin/Budsjettkalender/issues/60), [P16](https://github.com/depmartin/Budsjettkalender/issues/54) | needs-triage |
| [A07 · #64](https://github.com/depmartin/Budsjettkalender/issues/64) | Godkjenn automatikk og fullfør produksjonsoverleveringen | [P19](https://github.com/depmartin/Budsjettkalender/issues/57), [A03](https://github.com/depmartin/Budsjettkalender/issues/60), [A05](https://github.com/depmartin/Budsjettkalender/issues/62), [A06](https://github.com/depmartin/Budsjettkalender/issues/63) | ready-for-human |

De fem triagerollene følger [ordlisten](../agents/triage-labels.md).
`needs-triage` med avhengigheter betyr planlagt arbeid som må avgrenses før bygging.
`needs-info` betyr at kriteriene fortsatt krever en uttrykkelig beslutning.
En bred arbeidspakke deles i små vertikale underoppgaver før implementering; pakken
beholder samlet akseptanseansvar. Nye underoppgaver og avhengigheter lenkes fra #38.
Ingen implementering begynner med en uavklart ny testgrense.

## Anbefalt første arbeidsrekkefølge

1. Produkteier og IT behandler [P01](https://github.com/depmartin/Budsjettkalender/issues/39) og deretter feedbeslutningen
   [P04](https://github.com/depmartin/Budsjettkalender/issues/42). Bare manuell pilot er besluttet så langt.
2. Første kodeoppgave er [P08](https://github.com/depmartin/Budsjettkalender/issues/46): koble eksisterende usikkerhetsregler
   inn i PDF-/køflyten. Dette er et konkret pilotgap med en avgrenset testgrense.
3. Deretter følger [P02](https://github.com/depmartin/Budsjettkalender/issues/40) og [P06](https://github.com/depmartin/Budsjettkalender/issues/44), én om gangen.
   Disse kan bygges og prøves lokalt mens driftsavklaringene pågår.
4. Når P01 og P04 er lukket, vurderes de frigjorte etterfølgerne i tabellen.
   GitHub-oppgavenes faktiske blokkeringer styrer videre rekkefølge.
5. Piloten tas i bruk først etter [P19](https://github.com/depmartin/Budsjettkalender/issues/57). Deretter følger
   kilde, felles inntak, versjoner, avtalt uttrekk og worker frem til [A07](https://github.com/depmartin/Budsjettkalender/issues/64).

## Ferdigkriterier og bevis

Hver kodeoppgave får en egen gren fra oppdatert main og en egen PR.
En regresjon skal først feile av riktig grunn ved den avtalte tjeneste-/HTTP-/nettlesergrensen.
Bygg én observerbar atferd om gangen. Dokumenter standard- og spesifikasjonsgjennomgang
separat, vurder sikkerhet der oppgaven berører tilgang/data, og krev de tre kandidatjobbene
i CI: ordinære tester, ekte SQL Server og nettleserakseptanse.
Tester som utvides, må også utvide eventuell kontroll av forventet testantall i CI.

En miljøoppgave lukkes med faktisk miljøbevis, navngitt ansvarlig og dokumentert utfall.
Lokal SQLite og demo-personaer erstatter ikke Azure SQL, Entra eller M365.
Rapporter revisjon, miljø, syntetiske eksempeldata, utfall og begrensninger.
Hemmeligheter, bearer-URL-er og rå dokument-/nettleserspor skal ikke publiseres.

P19 krever samlet godkjenning fra produkteier og drift, navngitte pilotbrukere,
gjennomført gjenoppretting, support-/tilbakeføringsrutiner og konkret autorisert utrulling.
A07 krever dessuten endrings-/fjerningsflyt, uttrekkskvalitet, gjentatte forsøk, omstart
og synlig innhentingsstatus gjennom hele kjeden. Hovedoppgaven lukkes ved fullført
overlevering eller en uttrykkelig omfangsendring.

Ved utsatt funksjon oppdateres P01, kravkartet, den berørte oppgaven og avhengighetene.
En avhengighet skal ikke omgås ved å markere utestet atferd som ferdig.

## Avgrensninger

Graph-/M365-native deling og flere kilder enn regjeringen.no er betingede utvidelser.
Dersom P04 velger Graph, skal egen avhengighetskjede opprettes før delingsarbeidet starter.
Modellleverandør og kjørelokasjon avgjøres i A04 før adapteren i A05.
Varsler til bidragsytere forblir i appen. Internkalenderen forblir SBR-intern uten eksport/feed.
Mobil, tilgjengelighet og belastning verifiseres i P18; konkrete krav og mål avklares der.
