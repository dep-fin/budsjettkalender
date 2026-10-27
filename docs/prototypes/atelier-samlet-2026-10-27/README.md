# Atelier – samlet visuell referanse

27. oktober 2026. Dette er den videreførte skissen etter produkteierens valg av Atelier, godkjenning av regelbyggeren og avklaring av regelliste, milepælmerke og Administrator-innstillinger. Tidligere varianter er historiske referanser. Denne mappen er det nyeste samlede utgangspunktet.

Åpne **prototype-atelier-admin.html** direkte i nettleseren. Kalender er startvisning. Velg Administrasjon for administratorområdene. Prototypen har bare konstruerte frister, regler og personer. Endringer beholdes til siden lastes på nytt. Ingen reell tilgang, publisering, filopplasting eller Entra-endring utføres.

## Administrasjonsmeny

1. **Budsjettår og frister:** Velg konkret budsjettår. Frister, Forslag, Dokumenter og Generer fra årsmal er lokale faner. Ny frist opprettes i samme årskontekst. Snarveier med antall viser forslag som venter i andre år. Genererte forslag holdes adskilt fra den løpende køen.
2. **Årsmal:** Eksisterende gjentaksregler, Nye gjentaksregler, Prøv malen og Overfør mal. Her vedlikeholdes generelle regler, ikke konkrete publiserte frister.
3. **Grupper og innsyn:** Grupper og forhåndsvisning av deres innsyn.
4. **Eksport og deling:** Word-/ICS-forhåndsvisning, JSON-eksempel, demolenker og demokopi. Overføring av selve årsmalen åpner samme regelsett under Årsmal.
5. **Internkalender:** Interne runder, gjøremål, ansvarlige og gjøremålsregler.
6. **Administrator-innstillinger:** Administratorer og Faseinndeling. Menyvalget står rett under Internkalender.

## Regelliste og nyregistrering

Eksisterende regler vises i en egen liste med tittel, kategori, sammenheng, lesbar datoregel, Rediger og Se datoer. Søk, kategorikryss og sammenhengsfilter virker sammen; tomt kategoriutvalg gir ingen resultater. Filter og søk beholdes når en regel åpnes for redigering. Nye gjentaksregler har en egen side med hele regelbyggeren uten regellisten ved siden av.

Listen sorteres som standard kronologisk for et eksplisitt valgt budsjettår, på beregnede kalenderdatoer også før og etter dette året. Administrator kan bytte til senest først eller norsk tittelrekkefølge. Tentative frister plasseres etter måned, uten oppdiktet dagsdato. Like datoer får stabil tittelrekkefølge. Regler som ikke kan beregnes, står sist i begge datoretninger med forklaring. Filtre og sortering virker sammen og påvirker bare presentasjonen.

Milepælmerket er **avledet presentasjon**, ikke en ny domenetype eller en ny beregningsgren:

- En selvstendig regel med avhengige regler får et lite flagg og merket **Milepæl**.
- En selvstendig regel uten avhengige regler står som **Selvstendig**.
- En relativ regel står alltid som **Avhengig**, også når andre regler bygger videre på den. Den får ikke merket selvstendig milepæl.
- Oversikten viser antall direkte og samlede avhengige regler. Se sammenhengen forklarer den aktuelle koblingen. Beregning følger eksisterende ankerkjeder; merket endrer ingenting.
- Ingen ny milepæl-avkryssing lagres, og endring av malen flytter ikke allerede publiserte frister automatisk. Avhengige beregninger oppdateres i forhåndsvisning og ny generering. Publisering beholder godkjenningsløpet.

Regelbyggeren viderefører fast dato, første–femte ukedag i måneden med siste som reserve, første ukedag fra og med en dato, og før/etter en milepæl. Eksempelvis uttrykkes mandag mellom 20. og 26. juli som første mandag fra og med 20. juli. Årsforskyvning, valgårsvalg, kategori og funksjon/løp beholdes. Vanlig registrering krever ikke JSON eller kode. Regelen vises som en setning og beregnes over fem budsjettår. Prøv malen støtter manuelle ankerdatoer og viser feil i manglende/sirkulære kjeder. Overfør mal viderefører eksport og erstatt-alt-import av gyldige regler med forhåndsvisning og advarsler.

## Samme data

Kalender, Budsjettår og Fristregister viser de samme fristobjektene. Generering bruker den samme oppdaterte årsmalen som redigeres i regelbyggeren; den er ikke en separat forenklet to-regelstype-mal. Generering gir forslag, som må godkjennes enkeltvis, og bevarer kategori og datopresisjon. En tentativ måned blir ikke gjort om til en fast dato.

Faseinndelingen er flyttet fra Årsmal til Administrator-innstillinger. Én felles mal gjelder alle budsjettår. Endret fase blir også brukt i leserens tidslinje. De fem avklarte standardfasene og grensene er videreført. Kalenderens minikalender og flervalg, tydelig årsskille i Liste, kilderundskriv over Om fristen og den røde I dag-markøren videreføres.

## Administratortilgang er et nytt utkast

Oversikten skiller mellom manuell tilgang og tilgang gjennom Entra-gruppe. Entra-styrte tilganger vises som forvaltet i Entra. Demoen lar administrator utnevne og fjerne manuelt tildelte administratorer blant oppdiktede eksempelpersoner, med bekreftelse. Ingen invitasjon sendes og ingen reell bruker endres.

Den undersøkte prosjektkoden har Entra-utledet administratorrolle og bevarer manuelt satte administratorer, men har ikke den nye ferdige utnevnelsesflaten. Reell manuell rolleforvaltning skal derfor avgrenses som egen implementeringsoppgave med serverautorisering, identitetsvalg, revisjonsspor, håndtering av egen/siste administrator og samspill med Entra. Denne skissen leverer ikke slik backend eller endrer dagens tilgangspolicyer.

## Fortsatt utsatt eller illustrert

PDF-behandling er fortsatt utsatt etter avtale. Filvalg viser metadata; innholdet leses ikke og skaper ingen forslag. Uttrekksoversikten er et separat forhåndsdefinert eksempel. Den senere PDF-demonstrasjonen skal fortsatt leveres. Word og ICS er forhåndsvisninger; abonnementslenker bruker eksempel.invalid. JSON-nedlasting inneholder kun eksempeldata. Leser-/administratorbyttet er en presentasjonskontroll, ikke innlogging.

## Prototypekontroll

Kontrollert i isolert Edge: kalender som start og minikalender; seks administratorvalg; separat regelliste og registreringsflate; kombinasjonen kategori og milepælfilter; et avhengig mellomledd som forblir avhengig; oppretting og redigering av datovinduregel; utnevnelse/fjerning i demoen; faseendring delt med tidslinjens modell; generering fra den nylig endrede malen uten publisering; enkeltgodkjenning; årsavgrenset kø og snarvei til andre år; deaktivert PDF-behandling og tilgjengelige øvrige administratorområder. Mobil sidebredde var 390/390 og innfelt regelmodul 358/358. Ingen JavaScript-feil ble registrert i disse flytene. Dette er ingen akseptansetest av produksjonsappen.

Den tidligere separate årsmal-demoen dokumenterer dessuten juli-vindu over fem år, femte ukedag med reserve, valgårsvalg, manuell ankerdato, null dagers forskyvning og eksport/import av eksempelreglene. Beregningsgrunnlaget er undersøkt i prosjektets Regelparametre, Genereringsberegning, Datoberegning og ADR-0008.

Tilleggskontroll for sortering viste kronologisk rekkefølge fra mars i t−1 til regnskap i t+1, omvendt rekkefølge, endret beregningsår og kategori-/avhengighetsutvalg. Uberegnbar eksempelregel stod sist også ved synkende sortering. De lagrede reglene var identiske før og etter sortering; mobilbredde var 390/390 og ingen JavaScript-feil ble registrert. Dette gjelder bare prototypen.

## Videre arbeid

Issue #75 er den samlede produktspesifikasjonen. Denne mappen og den oppdaterte referanseindeksen skal brukes ved fremtidig Atelier-arbeid. Ved implementering gjenbrukes eksisterende Blazor-, tjeneste- og testgrenser; prototypens JavaScript og innfelte dokument er utforskingskode som ikke skal kopieres til produksjon. Produksjonskode, hovedgren og utrulling er ikke endret av denne leveransen.

Spesifikasjonen er delt i 20 konkrete GitHub-tickets, #76–#95. [Overleveringen](overlevering.md) har oppstartsinstruks, avhengigheter og startstatus; [kravkartet](kravkart.md) dekker alle 89 brukerhistoriene. Begynn med UX01 (#76), som er eneste startklare Atelier-kodeoppgave ved denne overleveringen. #75 er koordinering og skal ikke implementeres som én stor endring. Ticketene har egne akseptansekriterier og Blocked by-lenker; reell administratorutnevnelse har egne beslutnings-/implementeringsporter.

