# Overlevering – Atelier og implementeringsplan

Datert 27. oktober 2026. GitHub Issues er livsløpskilden. Ingen appkode eller produksjonsutrulling er utført i denne planleggingsleveransen.

## Start neste gang

1. Åpne [hovedspesifikasjon #75](https://github.com/depmartin/Budsjettkalender/issues/75), [UX01 · #76](https://github.com/depmartin/Budsjettkalender/issues/76) og deres kommentarer.
2. Les prosjektets gjeldende AGENTS, ordliste, status, relevante ADR-er og faktiske branch/worktrees. Den tidligere undersøkte lokale kopien på codex/73-kildefilter er bare bakgrunn og skal ikke resettes eller regnes som oppdatert hovedgren.
3. Start **kun UX01** fra oppdatert hovedgren i egnet isolert gren/worktree. Prototypegrenen er en kilde til designvalg og eksempler, ikke en ferdig appgren som skal merges.
4. Bruk dagens avtalte testgrense: ekte Playwright mot Kestrel/Blazor, supplert med eksisterende tjenestekontrakter og SQL Server når lagring berøres. Ingen ny mock-akseptanse som erstatning.
5. Gjennomfør én avgrenset ticket, review/sikkerhetskontroll, relevante tester og prosjektets CI. Ticketarbeid er ikke generell merge-/deployautorisasjon.
6. Oppdater ticket og #75 ved faktisk levering. Kontroller at forutsetninger er lukket før neste ticket merkes ready-for-agent. Ikke start alt fra parent #75.

## Lagret design

- [Samlet kjørbar HTML-skisse](prototype-atelier-admin.html). Åpne lokalt etter å ha hentet hele mappen med tilhørende filer; GitHub viser HTML-kilden.
- [Funksjoner, avklaringer og prototypegrenser](README.md).
- [Samlet spesifikasjon som snapshot](spesifikasjon.md); #75 er kilden ved senere endringer.
- [Kravkart for historier 1–89](kravkart.md).
- [Ticketkopier](tickets/) og [maskinlesbart ticketkart](ticketkart.json); senere live-endringer i GitHub gjelder foran disse datert kopiene.
- [Varig indeks over nye og historiske prototyper](../README.md).

Kronologisk sortering ble lagret ved commit `36dd6f06e471113b98571d24ebd93dd044b848fc` på grenen `codex/atelier-kalender-prototype-2026-10-23`. Denne overleveringen legges på samme gren i en etterfølgende dokumentasjonscommit. Arbeidet kan gjenopptas uten tilgang til lokale filer eller denne chathistorikken; hent referansene fra GitHub.

## Oppgaverekkefølge og status ved overlevering

| Trinn | Konkret GitHub-ticket | Avhengigheter | Startstatus |
| --- | --- | --- | --- |
| UX01 | [#76 · Atelier: grunnstruktur, rollebasert navigasjon og bevarte innganger ](https://github.com/depmartin/Budsjettkalender/issues/76) | Ingen | ready-for-agent |
| UX02 | [#77 · Atelier: månedskalender, minikalender, dagsagenda og fristdetaljer ](https://github.com/depmartin/Budsjettkalender/issues/77) | #76 | Blokkert av forgjenger |
| UX03 | [#78 · Atelier: felles kalenderfiltre med flervalg av budsjettår ](https://github.com/depmartin/Budsjettkalender/issues/78) | #77 | Blokkert av forgjenger |
| UX04 | [#79 · Atelier: uke- og listevisning med bevart kalenderkontekst ](https://github.com/depmartin/Budsjettkalender/issues/79) | #78 | Blokkert av forgjenger |
| UX05 | [#80 · Atelier: Budsjettår med tidslinje og samlet Fristliste ](https://github.com/depmartin/Budsjettkalender/issues/80) | #79 | Blokkert av forgjenger |
| UX06 | [#81 · Atelier: forslag fra valgt kalenderdato til godkjent frist ](https://github.com/depmartin/Budsjettkalender/issues/81) | #80 | Blokkert av forgjenger |
| UX07 | [#82 · Atelier: konkret arbeidsår for fristregister og godkjenningskø ](https://github.com/depmartin/Budsjettkalender/issues/82) | #81 | Blokkert av forgjenger |
| UX08 | [#83 · Atelier: lagret felles fasemal under Administrator-innstillinger ](https://github.com/depmartin/Budsjettkalender/issues/83) | #82 | Blokkert av forgjenger |
| UX09 | [#84 · Atelier: kodefri regelbygger for nye og eksisterende gjentaksregler ](https://github.com/depmartin/Budsjettkalender/issues/84) | #83 | Blokkert av forgjenger |
| UX10 | [#85 · Atelier: regelliste med kategorifilter, milepælmerke og kronologisk sortering ](https://github.com/depmartin/Budsjettkalender/issues/85) | #84 | Blokkert av forgjenger |
| UX11 | [#86 · Atelier: prøv årsmal og generer fra samme regler for konkret budsjettår ](https://github.com/depmartin/Budsjettkalender/issues/86) | #85 | Blokkert av forgjenger |
| UX12 | [#87 · Atelier: maleksport og forhåndsvist import uten kontrakttap ](https://github.com/depmartin/Budsjettkalender/issues/87) | #86 | Blokkert av forgjenger |
| UX13 | [#88 · Atelier: fullfør dokumentflyt fra årsvisning til kontroll og godkjent frist ](https://github.com/depmartin/Budsjettkalender/issues/88) | #87 | Blokkert av forgjenger |
| UX14 | [#89 · Atelier: grupper og innsyn med bevarte tilgangsregler ](https://github.com/depmartin/Budsjettkalender/issues/89) | #88 | Blokkert av forgjenger |
| UX15 | [#90 · Atelier: eksport, kalenderdeling og dataoverføring med uendret policy ](https://github.com/depmartin/Budsjettkalender/issues/90) | #89 | Blokkert av forgjenger |
| UX16 | [#91 · Atelier: internkalender med runder, egne gjøremål og gjøremålsregler ](https://github.com/depmartin/Budsjettkalender/issues/91) | #90 | Blokkert av forgjenger |
| UX17 | [#92 · Atelier: lesbar administratoroversikt med tilgangskilde ](https://github.com/depmartin/Budsjettkalender/issues/92) | #91 | Blokkert av forgjenger |
| UX18 | [#93 · Atelier: samlet kompatibilitets- og tilgjengelighetsakseptanse for #75 ](https://github.com/depmartin/Budsjettkalender/issues/93) | #92 | Blokkert av forgjenger |
| AD01 | [#94 · Administratortilganger: avklar produksjonskontrakt før manuell utnevnelse ](https://github.com/depmartin/Budsjettkalender/issues/94) | #39, #92 | Avventer beslutning/forutsetninger |
| AD02 | [#95 · Administratortilganger: implementer godkjent manuell utnevnelse og fjerning ](https://github.com/depmartin/Budsjettkalender/issues/95) | #94 | Avventer beslutning/forutsetninger |

UX01–UX18 er bevisst serialisert som leveranseporter for ønsket om én oppgave om gangen. De er ikke alle isolert tekniske avhengigheter. Bare UX01 er merket ready-for-agent nå; blokkerte oppgaver har Blocked by i selve saken og er ikke merket startklare. Connectoren eksponerer ikke native avhengighetsmutasjoner, så eksplisitte lenker er valgt etter prosjektets regel.

AD01/AD02 gjelder ny **reell** manuell administratorforvaltning og er adskilt fra Atelier-kjernens visuelt godkjente demo. AD01 avventer også #39 om pilot/Entra/nødadministrator. Ny sikkerhetspolicy/testgrense skal fastsettes der før AD02 starter. UX17 leverer lesende oversikt over appkjente administratorer med tilgangskilde.

## Hva som er ferdig og hva som ikke er det

Ferdig i denne sesjonen: visuelle skisser, kronologisk regellistesortering, produktavklaringer, samlet spesifikasjon, 20 publiserte tickets med kriterier og avhengigheter, kravkart og overlevering. Ingen UX-ticket er implementert i produksjonsappen av planleggingen. #75 og barna forblir åpne til deres egne ferdigkriterier er oppfylt.

Prototypen har søk/kategori/sammenheng, separate eksisterende/nye regler, fire kodefrie datovalg, prøve/generering fra samme mal, felles faser under Administrator-innstillinger og ufarlig adminutnevnelse med eksempelpersoner. Kalender er start; minikalender, måned/uke/liste, Budsjettår/tidslinje/Fristliste og de øvrige adminflatene videreføres. Milepælmerket er bare en avledet visuell markør på selvstendige ankre; et avhengig mellomledd forblir avhengig.

Sorteringen bruker valgt målbudsjettårs beregnede datoer på tvers av kalenderår, med tidligst først som standard og valg for senest først/tittel. Tentativitet bevares, like datoer har stabil rekkefølge og uberegnbare regler står sist. Testet i isolert Edge med årsendring/filter/retning, uendrede regler og mobilbredde 390/390, uten registrerte JavaScript-feil. Dette er prototypebevis, ikke Blazor-appakseptanse eller CI-bevis.

Fortsatt utsatt: hele dokumentdemonstrasjonen og modernisert faktisk dokumentflyt følges opp uttrykkelig i UX13 (#88), med eksisterende opplastings-/køgrenser. Ingen valgt PDF er analysert av dagens HTML-skisse. #47 beholder sine spørsmål om uavklarte kandidater; #49 eier kobling av manuell frist til mal; #45 eier manuelle gruppemedlemskap. Feedpolicy, offisiell Word-mal, Office/Outlook, reell Entra/Azure og driftsporter følger fortsatt egne eksisterende saker.

## Referansenes presedens

Nyeste beslutninger i #75 og den aktuelle implementeringsticketen går foran eldre prototyper og dokumentkopier. Alle brukerhistorier er kartlagt. Historie 86 gjelder utnevnelsesskissen som allerede er demonstrert; den innebærer ikke at ny backendrolleforvaltning er ferdig. Den kjørbare HTML-koden og innfelte modul er utforskingskode og skal ikke kopieres direkte til produksjon.
