## Problem Statement

Brukerne trenger en tydelig, visuell oversikt over budsjettfrister i en faktisk kalender, med enkel veksling mellom måned, uke og liste. Dagens funksjoner finnes på flere arbeidsflater og må fortsatt være tilgjengelige. En ny kalender skal ikke erstatte årshjul, forslag, eksport eller administratorens arbeidsflyter.

En bidragsyter som kjenner datoen for en ny frist, må i dag starte et separat forslag og registrere datoen på nytt. Det bryter sammenhengen mellom planlegging og innsending. Samtidig må en kalenderhandling aldri forveksles med direkte publisering eller gi lesere nye rettigheter.

## Solution

Bruk Atelier som visuelt og strukturelt utgangspunkt for videre utvikling. Kalenderen blir hovedvisning når brukeren åpner applikasjonen. Den viser måned, uke eller liste og har et rolig sidepanel for dato og utvalg på stor skjerm.

Behold dagens funksjoner som andre faner eller områder med samme tilgang som før. Kalender og Budsjettår er separate hovedfaner. Den nye grafiske Budsjettår-visningen følger ett valgt budsjettår gjennom flere kalenderår. Dagens Årshjul beholdes som egen eksisterende inngang, og kan grupperes under Budsjettår i lokal navigasjon uten å fjerne visningen eller bryte direkte innganger. Mine forslag og varsler er tilgjengelige for bidragsytere, Eksport og deling for dagens autoriserte roller, og Internkalender og Administrasjon etter eksisterende policyer. Under administrasjon beholdes kø, opplasting, fristregister, generering, mal, innsyn, grupper og data. Detaljer innenfor hvert område legges i lokal navigasjon, slik at alle verktøy ikke konkurrerer på samme nivå.

En valgt kalenderdag åpner dagens agenda. For bidragsytere inneholder denne handlingen «Foreslå frist» med valgt dato forhåndsutfylt. Forslaget registreres med samme validering og godkjenningsløp som dagens forslagsskjema. Det blir ikke synlig som publisert frist før administrator har godkjent det. En vanlig leser får bare agenda og fristdetaljer.

## User Stories

1. Som leser vil jeg møte kalenderen først når jeg åpner løsningen, slik at jeg straks ser fristene i sammenheng.
2. Som bruker vil jeg ha Atelier som et gjennomgående visuelt utgangspunkt, slik at videre forbedringer fremstår som én løsning.
3. Som bruker vil jeg se en faktisk månedskalender med mandag først og ukenummer, slik at jeg kan orientere meg etter dato og uke.
4. Som bruker vil jeg bytte til ukevisning, slik at jeg kan undersøke en travel uke nærmere.
5. Som bruker vil jeg bytte til liste, slik at jeg kan lese fristene uten et romlig rutenett.
6. Som bruker vil jeg beholde valgt dato, søk og filtre ved visningsbytte, slik at jeg slipper å sette opp utvalget på nytt.
7. Som bruker vil jeg gå til forrige eller neste periode og tilbake til I dag, slik at jeg kan planlegge og finne tilbake.
8. Som bruker vil jeg at månedslengde, skuddår, årsskifte og ISO-ukenummer er riktige, slik at kalenderen er pålitelig.
9. Som bruker vil jeg skille datoens kalenderår fra fristens budsjettår, slik at overlappende budsjettløp ikke blandes.
10. Som bruker vil jeg filtrere på kategori og budsjettår, slik at jeg kan avgrense oversikten.
11. Som bruker vil jeg kunne søke i titler og notater innenfor synlige data, slik at jeg raskt finner en kjent frist.
12. Som bruker vil jeg se aktive filtre og et forståelig resultatantall, slik at jeg vet hvilket utvalg kalenderen viser.
13. Som bruker vil jeg få null treff når ingen kategori er valgt og kunne nullstille valgene, slik at avhuking ikke uventet utvider utvalget.
14. Som bruker vil jeg se alle frister i valgt kalenderperiode, også tidligere datoer i perioden, slik at kalenderbildet er komplett.
15. Som bruker vil jeg fortsatt ha tilgang til den tidligere listefunksjonen og dens avtalte atferd, slik at ny kalender ikke fjerner eksisterende muligheter.
16. Som bruker vil jeg se heldagsfrister atskilt fra frister med klokkeslett, slik at jeg ikke må tolke en dagsfrist som et møte.
17. Som bruker vil jeg se samtidige fristtidspunkter hver for seg, slik at ingen frist skjules bak en annen.
18. Som bruker vil jeg kunne finne frister med klokkeslett utenfor et standard tidsområde, slik at tidlige og sene frister ikke forsvinner.
19. Som bruker vil jeg kunne åpne hele dagens agenda når månedsruten ikke har plass til alt, slik at travle dager fortsatt er lesbare.
20. Som bruker vil jeg åpne full tittel, dato, eventuell tid, kategori, budsjettår, notat og gyldig offentlig kilde fra enhver visning, slik at jeg slipper å lete på nytt.
21. Som bruker vil jeg lukke detaljer og beholde kalenderkonteksten, slik at jeg kan fortsette der jeg var.
22. Som bruker vil jeg se tentative månedsfrister i et eget månedsfelt med eventuell primo, medio eller ultimo, slik at en usikker dato ikke fremstår fastsatt.
23. Som bruker vil jeg også finne slike månedsfrister når jeg undersøker en uke i måneden, slik at sorteringsdagen ikke bestemmer om de er synlige.
24. Som bruker vil jeg åpne Årshjul som egen fane med dagens budsjettløp, slik at den eksisterende oversikten fortsatt er tilgjengelig.
25. Som bidragsyter vil jeg fortsatt finne Mine forslag, varsler og den vanlige inngangen til et nytt forslag, slik at kalenderen er en ekstra inngang til eksisterende arbeid.
26. Som autorisert administrator vil jeg fortsatt finne eksport og kalenderdeling, slik at ny navigasjon ikke fjerner eksisterende leveranser.
27. Som SBR-bruker vil jeg fortsatt finne Internkalender med internrunder og gjøremål, slik at interne oppgaver holdes atskilt fra publiserte frister.
28. Som administrator vil jeg fortsatt finne godkjenningskø, opplasting, fristregister, generering, mal, innsyn, grupper og data, slik at forvaltningen fungerer som før.
29. Som bruker vil jeg at fanene bare viser funksjoner jeg har rett til å bruke, slik at navigasjonen er oversiktlig og tilgangene uendret.
30. Som bidragsyter i FA vil jeg klikke på en kalenderdag og velge Foreslå frist, slik at datoen følger direkte inn i registreringen.
31. Som annen eksisterende bidragsyter vil jeg ha samme datobaserte forslagsinngang, slik at funksjonen følger funksjonsrollen og ikke bare gruppen FA.
32. Som bidragsyter vil jeg ha valgt dato forhåndsutfylt og kunne endre den før innsending, slik at feilklikk er enkle å rette.
33. Som bidragsyter vil jeg velge budsjettår uavhengig av kalenderdatoen, slik at fristen knyttes til riktig budsjett.
34. Som bidragsyter vil jeg fylle inn tittel, kategori, notat og foreslått synlighet med dagens validering, slik at kalenderinngangen gir samme kvalitet som det vanlige skjemaet.
35. Som bidragsyter vil jeg kunne registrere tentativ måned i stedet for en fast dag, slik at et datoklikk ikke tvinger frem falsk presisjon.
36. Som bidragsyter vil jeg se at forslaget sendes til behandling og ikke publiseres direkte, slik at handlingens konsekvens er tydelig.
37. Som bidragsyter vil jeg få en kvittering og lenke til Mine forslag etter innsending fra kalenderen, slik at jeg vet at forslaget er mottatt.
38. Som bidragsyter vil jeg beholde kalenderperiode og filtre ved innsending eller avbryt, slik at registreringen ikke bryter planleggingen.
39. Som bidragsyter vil jeg beholde utfylte verdier ved valideringsfeil og unngå utilsiktet dobbeltinnsending, slik at registreringen er trygg å gjennomføre.
40. Som bidragsyter vil jeg kunne foreslå endring fra en synlig frists detaljer med dagens endringsløp, slik at innhold kan korrigeres uten å endre synlighet.
41. Som leser vil jeg kunne klikke på en dato uten å få skrivehandlinger, slik at kalenderen fortsatt er en ren leseflate for meg.
42. Som administrator vil jeg beholde dagens direkte fristforvaltning og godkjenningsansvar, slik at kalenderklikk ikke innfører en ny publiseringsmodell.
43. Som bruker vil jeg kunne betjene datoer, hendelser, filtre og forslag med tastatur og synlig fokus, slik at løsningen fungerer uten mus.
44. Som bruker med skjermleser vil jeg ha meningsfulle navn, overskrifter, statusmeldinger og fokusretur, slik at kalenderens handlinger kan forstås.
45. Som mobilbruker vil jeg ha en lesbar liste eller agenda og tilgang til måned og uke, slik at frister og forslag også kan håndteres på liten skjerm.
46. Som bruker vil jeg ha laste-, tom-, feil- og bekreftelsestilstander som forklarer neste handling, slik at jeg kommer videre ved manglende treff eller teknisk feil.
47. Som bruker vil jeg bare motta godkjente frister jeg har tilgang til, slik at ingen skjulte frister, forslag eller tellinger lekker gjennom den nye kalenderen.
48. Som administrator vil jeg fortsatt bekrefte POL aktivt og beholde originalfristens synlighet ved endringsforslag, slik at den nye inngangen følger dagens tilgangsregler.
49. Som bruker vil jeg velge flere bestemte budsjettår samtidig, for eksempel 2026 og 2028 uten 2027, slik at jeg kan sammenlikne nettopp løpene jeg arbeider med.
50. Som bruker vil jeg ha eksplisitte årskryss, Velg alle og et tydelig tomt årsutvalg, slik at filteret ikke er begrenset til alle eller ett år.
51. Som bruker vil jeg se samme budsjettårsfarge og eksplisitte årstall i Liste som i Måned og Uke, slik at frister fra ulike år lett kan skilles.
52. Som bruker vil jeg åpne en egen Budsjettår-visning og velge ett budsjettår, slik at jeg kan følge dette budsjettet gjennom hele prosessen.
53. Som bruker vil jeg se en horisontal tidslinje fra oktober t−2 til april t+1 som utgangspunkt, slik at forberedelser, budsjettarbeid, gjennomføring og regnskap henger sammen.
54. Som bruker vil jeg se kalenderår og deres forhold til budsjettåret t, slik at eksempelvis en hendelse i 2025 knyttet til budsjettåret 2026 er lett å plassere.
55. Som bruker vil jeg kunne åpne alle synlige frister og milepæler i det valgte budsjettåret, slik at grafikken ikke bare viser et utvalg faste konferanser.
56. Som bruker vil jeg kunne åpne en milepæls detaljer og gå til dens måned i den vanlige kalenderen, slik at de to perspektivene kan brukes sammen.
57. Som bruker vil jeg beholde kalenderens flervalg av budsjettår når jeg undersøker ett separat budsjettløp, slik at jeg slipper å sette opp sammenlikningen på nytt.
58. Som bruker vil jeg ha et lesbart listealternativ til tidslinjen og tilgang til dagens årshjul, slik at ny grafikk ikke fjerner tidligere eller tilgjengelige alternativer.
59. Som leser vil jeg få en egen Kilderundskriv-seksjon over Om fristen med lenke til det tilknyttede rundskrivet på regjeringen.no, slik at jeg enkelt kan lese kilden.
60. Som bruker vil jeg at frister uten offentlig rundskriv ikke får en oppdiktet kilde eller en tom kildeseksjon, slik at opphavet fremstilles korrekt.

## Implementation Decisions

- **Valgt retning:** Atelier er besluttet av produkteier. Videre varianter skal utvikle Atelier, mens Panorama og Arbeidsro bevares som tidligere sammenlikninger. Ingen ny designkonkurranse er en forutsetning for implementering.
- **Additiv navigasjon:** Kalender er standard ved ordinær åpning. Årshjul, eksisterende liste, forslag, varsler, eksport, deling, internkalender og alle administratorfunksjoner beholdes. Faner er navigasjon til arbeidsflater, med lokal undernavigasjon der funksjonene er mange. Eksisterende direkte innganger og rollepolicyer skal fortsatt fungere.
- **Teknologi:** Blazor Interactive Server og eksisterende modulgrenser videreføres. Prototypen er visuell referanse og skal ikke kopieres direkte som produksjonskode. Et eventuelt kalenderbibliotek er et internt implementeringsvalg som må oppfylle denne spesifikasjonen.
- **Flervalg av budsjettår:** Kalenderens årsfilter er et sett av valgte tilgjengelige budsjettår, representert med avkryssing og eksplisitt Velg alle. For eksempel 2026 og 2028 viser deres union, avgrenset av øvrige filtre og serverens synlighet, uten at 2027 følger med. Ingen valgte år gir null treff i den nye flaten; det skal ikke uventet tolkes som alle. Årsvalgene utledes bare fra autoriserte data og beholder øvrige tilgjengelige år ved avgrensning. Dagens leseport støtter allerede en samling år; den skal ikke snevres inn til et enkelt år.
- **Kalendertilstand:** Måned, uke og ny periodeavgrenset liste deler valgt dato, kalenderperiode, kategori, budsjettår og søk i den aktive brukerkonteksten. Kalender og budsjettår er ulike begreper. Den eksisterende listemuligheten og årshjulets avtalte historikkatferd bevares; kalenderens komplette periode skal ikke utilsiktet endre deres tilstand.
- **Serverfiltrert lesing:** Gjenbruk IFristlesing og eksisterende FristFilter med datogrenser. Godkjent status og tilgang filtreres før data returneres. Søk, tellinger og årsvalg bygger bare på autoriserte data. Eventuelle kontrakttillegg er additive; ingen egen parallell kalenderdatabase eller obligatorisk ny HTTP-port er nødvendig.
- **Komplett kalenderperiode:** Kalenderen viser hele valgt tidsrom, inkludert tidligere dager. Månedspresise frister som gjelder en berørt måned må hentes selv om deres avledede sorteringsdag ligger utenfor valgt uke. Dette kan løses gjennom eksisterende autorisert leseport med et tilstrekkelig månedsvindu; dagsfrister avgrenses til den viste perioden. Ingen full usensurert datasamling hentes til klienten.
- **Filterkompatibilitet:** Et eksplisitt tomt kategorivalg i kalenderen skal ikke bli alle kategorier. Eksisterende leseports betydning av tom/null som alle beholdes for øvrige brukere av kontrakten; den nye presentasjonen må representere skillet uttrykkelig. Nullstilling gir Budsjett og Gul bok på, Regnskap av og et eksplisitt årsutvalg.
- **Presisjon:** Dagsfrister tegnes på dato. Klokkeslett er fristtidspunkt, ikke varighet. Tentative månedsfrister tegnes utenfor dagsrutene med måned og eventuell kvalifikator. Ukevisning viser en månedsoversikt for månedene uken berører. Ingen sorteringsdag brukes som en bestemt fristdag.
- **Datoklikk:** Et datoklikk åpner agenda for datoen, også når dagen er tom. Bidragsytere får en tydelig Foreslå frist-handling i den konteksten. Registreringen åpnes i samme arbeidskontekst og gjenbruker forslagsskjemaets felter og validering. Klikk på en eksisterende hendelse åpner hendelsesdetaljer og starter ikke et nytt forslag. Samme handlinger må kunne utføres med tastatur.
- **Forhåndsutfylling:** Den valgte datoen fylles inn ved et nytt datobasert forslag og kan redigeres. Dersom nøyaktig ett budsjettår er valgt, kan dette brukes som forhåndsvalg; ellers brukes dagens forslagsskjema sitt standardvalg, tydelig synlig og redigerbart. Kalenderdatoens år brukes ikke som bevis på budsjettår. Ett entydig kategorivalg kan forhåndsutfylles; øvrige valg følger dagens standard. Synlighet arves ikke ukritisk fra kalenderfilter eller annen frist.
- **Roller:** Den nye brukerforslagsinngangen følger samme bidragsyterpolicy som dagens forslagsskjema. Medlemskap i FA er ikke i seg selv ny skriverettighet. Andre eksisterende bidragsytere beholder sin mulighet. Administrator gis ikke en ny brukerforslagsflyt eller automatisk publisering ved datoklikk.
- **Innsending:** Gjenbruk IForslagsinnsending med innsender hentet fra innlogget identitet. Kalenderinngangen lager et ordinært upublisert forslag i dagens godkjenningskø. Eierskap, avvisning, revidert innsending, varsler og administratorgodkjenning beholdes. Kvitteringen fra kalenderinngangen viser at forslaget venter på behandling og lenker til Mine forslag. Den vanlige dedikerte forslagsinngangen beholdes.
- **Retur og feil:** Innsending og avbryt fra kalenderen bevarer kalenderkontekst. Valideringsfeil beholder feltverdier og har tydelig fokus. Innsendingsknappen hindrer utilsiktet gjentatt innsending mens operasjonen pågår; en ny idempotensordning er ikke del av denne bestillingen.
- **Budsjettår og kilder:** Fargekoding etter budsjettår videreføres konsekvent i Måned, Uke og Liste. Liste viser både en tydelig årsfarget markering og eksplisitt budsjettår; farge alene er ikke tilstrekkelig. Fristdetaljer viderefører gyldig offentlig kildelenke og eksisterende rollebestemte handlinger. Administrativt ventemerke og gruppemetadata skal ikke eksponeres i gruppelesing.
- **Synlig kildeplassering:** Fristens detaljer viser en egen Kilderundskriv-seksjon over Om fristen når fristen har en gyldig KildeUrl. Lenken merkes forståelig, eksempelvis Les rundskrivet på regjeringen.no, og varsler åpning i ny fane. Lenken gjenbruker dagens servervaliderte dokumentkobling og tillatte HTTPS-adresser. Den konstrueres ikke fra tittel, dokumentnummer eller filnavn. Manglende eller avvist kilde gir ingen lenke og ingen tom seksjon. Dette gjelder lesere og alle andre som kan se fristen, i kalender, liste og grafisk budsjettår. Ny fane bruker eksisterende referrer- og rel-vern. Rundskrivnummer eller tittel vises bare hvis de faktisk finnes i tilknyttede metadata.
- **Grafisk Budsjettår:** Denne visningen fokuserer ett valgt budsjettår og er et annet perspektiv enn kalenderens samtidige flervalg. Kalenderens årsutvalg beholdes separat. Tidslinjen viser hele løpet, også historikk, som standard oktober t−2 til april t+1. Om faktiske synlige frister ligger utenfor rammen, utvides den eller de vises i en tydelig tilknyttet del; ingen frist utelates stille. Det eksisterende årshjulet beholdes med sine funksjoner og direkte innganger.
- **Milepæler og faktiske frister:** Eksemplet for budsjettår 2026 går fra oktober 2024, via innspill januar 2025, marskonferanse mars 2025, augustkonferanse august 2025, fremleggelse oktober 2025, revidert budsjett april 2026, nysaldering november 2026 og regnskap april 2027. Dette er produkteierens illustrasjon av ønsket struktur, ikke nye vedtatte datoer. Produksjonsvisningen henter faktiske, autoriserte, godkjente frister etter Budsjettår, med bevart presisjon. Prosessoverskrifter eller faser oppretter ingen hendelser og fyller ikke inn manglende frister. Løp i domenemodellen identifiserer en funksjon; det er ikke synonymt med hele budsjettårets prosess eller rundskrivnummer.
- **Tetthet og detaljer i tidslinjen:** Flere frister på samme tidspunkt samles visuelt med en synlig vei til alle enkeltfrister. Felles fristdetaljer og gyldig kildelenke gjenbrukes. Listealternativ, tastatur og mobil må gi tilgang til det samme autoriserte innholdet. En månedspresis milepæl får ingen oppdiktet dagsdato.
- **Tilgangsregler:** POL krever fortsatt aktivt valg. Endringsforslag endrer innhold og bevarer synlighet. SBRs interne gjøremål forblir egne enheter og sendes ikke til andre roller. Abonnementets tokenvern og målgruppe endres ikke.
- **Data og tid:** Det er ikke identifisert behov for ny databasemodell for måneds-/ukevisning eller forslag fra dato. Oslo-tid og norske kalenderkonvensjoner brukes ved visning av dato og klokkeslett. Eventuell migrasjon krever en særskilt dokumentert begrunnelse.

## Testing Decisions

- **Avklart med produkteier:** Eksisterende reelle nettlesertester er hovedgrensen. Produkteier har eksplisitt bekreftet dette i samtalen 23. oktober 2026.
- **Hovedscenario:** Bidragsyter åpner standardkalenderen, velger en dato, starter forslag, ser riktig forhåndsutfylt dato, sender inn og får kvittering. Forslaget finnes i Mine forslag og administrators kø, men ikke i ordinær fristkalender. Administrator godkjenner; en ny autorisert lesing viser nøyaktig én publisert frist med korrekt dato og besluttet synlighet.
- **Nettlesergrense:** Utvid eksisterende Playwright-scenarioer mot ekte Kestrel og faktiske Blazor-handlinger. Bruk tilgjengelige navn og synlige utfall. Kalender-, forslag-, eksport-, abonnements- og internkalenderscenarioene er eksisterende eksempler på tilsvarende testnivå. Ingen ny parallell testarkitektur innføres.
- **Navigasjonsregresjon:** Kontroller at kalender er hovedinngang, og at årshjul, eksisterende liste, dedikert forslag, varsler, eksport/deling og administrator-/SBR-områder fortsatt kan nås og brukes med samme roller. Eksisterende direkte innganger skal ikke forsvinne. Tidligere tester oppdateres bare for tilsiktet presentasjonsendring; deres atferdsbevis beholdes.
- **Rolleprøve:** Bruk minst leser, FA-bidragsyter, annen eksisterende bidragsyter og administrator. Leser har ikke forslagskontroll eller skriveadgang. Administrator beholder sitt eget løp. Kontroller faktisk serveravvisning der tilgang forsøkes uten rettighet; skjulte knapper alene er ikke bevis.
- **Eksisterende tjenestegrenser:** Behold eller utvid kontrakttester ved IFristlesing og IForslagsinnsending når periode, presisjon eller forhåndsutfylling påvirker dem. Test bare returnert atferd og lagret resultat, uten å binde testene til CSS-klasser, private hjelpemetoder eller komponentenes interne tilstand.
- **Synlighet og publisering:** Kontroller tillatt og skjult innhold, POL-isolasjon, eierskap til forslag, ingen publisering ved avbryt eller feil, og uendret synlighet for endringsforslag. Eksisterende godkjennings-/samtidighetskontrakter gjenbrukes. Reell SQL Server-kontroll beholdes når publisering og lagring inngår.
- **Årsfilter og liste:** Velg to ikke-sammenhengende budsjettår og bekreft deres eksakte union, at mellomliggende år ikke vises, og at kategori/søk fortsatt danner snitt. Prøv Velg alle, ingen år, ett år og bytte mellom visninger. Kontroller at årsvalg og tellinger ikke avslører skjulte data. Liste skal ha eksplisitt budsjettår og samme fargebetydning som Måned/Uke.
- **Grafisk budsjettår:** Velg budsjettår 2026 og prøv hele vinduet fra 2024 til 2027. Sammenlikn alle viste frister med IFristlesing for samme budsjettår, inklusive historikk, tentative frister, flere frister på ett tidspunkt og frister utenfor standardvinduet. Kontroller kalenderår/t-markering, årbytte, listealternativ og overgang tilbake til kalenderen uten å miste dens flervalg. Veiledende fasetekst skal ikke fremstå som nye publiserte data.
- **Kilderundskriv:** Klikk en kildekoblet frist som vanlig leser og verifiser lenken over Om fristen med korrekt, tillatt adresse. Prøv også kildefri frist, ugyldig adresse og skjult frist. Eksisterende kildelenke-/HTTP-/tjenesteregler er tidligere testbevis som videreføres; det innføres ingen klientbasert utvidelse av URL-tillatelser.
- **Kalenderprøver:** Verifiser bytte mellom måned/uke/liste med beholdt dato og filtre, alle frister på travle dager, samtidige klokkeslett, heldag, tidlige/sene klokkeslett, årsskifte, skuddår og måneder med seks uker.
- **Tentative prøver:** En frist med månedspresisjon må være tilgjengelig fra en uke som ikke inneholder dens avledede sorteringsdag. Test også uke over månedsskifte. Den må verken forsvinne eller tegnes på en oppdiktet dag.
- **Registreringsprøver:** Kontroller korrekt dato, redigering av forhåndsvalg, budsjettår som avviker fra kalenderår, null/tomt utvalg, ugyldige felt, bevart inndata, avbryt og retur. Kontroller at klikk på eksisterende frist ikke åpner nyregistrering, og at den vanlige forslagsinngangen fortsatt fungerer.
- **Tilgjengelighet og skjermbredde:** Kontroller tastatur, synlig fokus, dialognavn, Escape/fokusretur, feltfeil, skjermleserstatus, kontrast, zoom og mobil. Liste er et tilgjengelig alternativ. Samlet pilotakseptanse samordnes med #56 og erstattes ikke av prototypebilder.
- **Ferdig kodekandidat:** Hver avgrensede PR skal ha relevante regresjoner, Standards-/Spec-review, nødvendig sikkerhetskontroll og grønne ordinære, SQL Server- og nettleserjobber etter prosjektets regler. Lokal SQLite/demo dokumenterer ikke ekte Entra, Azure eller Outlook.

## Out of Scope

- Å fjerne årshjul, eksisterende liste, forslag, eksport, varsler, internkalender eller administratorfunksjoner.
- Å velge mellom Panorama og Arbeidsro på nytt som hovedretning; Atelier er valgt.
- React-omskriving, ny backend, parallell kalenderdatabase eller direkte innføring av prototypens JavaScript i produksjonsappen.
- Automatisk publisering, nye funksjonsroller, nye rettigheter basert bare på FA-medlemskap eller automatisk POL-valg.
- Å endre feedpolicy, tokenvern, eksportformat, offisiell Word-mal eller reell Outlook-integrasjon. Disse følger egne eksisterende oppgaver.
- Ny kildeinnhenting, robotuttrekk, matching av dokumentversjoner eller PDF-kandidatbehandling.
- Dra-og-slipp for å flytte frister, utledning av møtelengder eller nye fullført/forsinket-tilstander på publiserte frister.
- Å utvide brukerforslag med klokkeslett. Kalenderen viser eksisterende tidfestede frister, mens den nye forslagsinngangen i første omgang forhåndsutfyller dato og dagens øvrige støttede felter.
- Varige personlige visningsinnstillinger, ny idempotensordning, ny medlemskapsadministrasjon eller full redesign av alle administratorskjemaer.
- Produksjonsutrulling og generell mergeautorisasjon.

## Further Notes

Dette er den samlede produktbestillingen etter valget av Atelier. Den erstatter tidligere anbefaling om Panorama og presiserer at ny kalender er én visning i den eksisterende løsningen. De øvrige tiltakene i den opprinnelige UX-gjennomgangen er bakgrunn, ikke automatisk del av denne implementeringen.

Foreslått oppdeling i vertikale implementeringsoppgaver:

1. **Atelier, standardinngang og bevarte faner:** etabler visuell grunnstruktur og rollebasert navigasjon; bevis at alle eksisterende funksjoner fortsatt nås.
2. **Månedskalender med dagsagenda og detaljer:** gjenbruk autorisert fristlesing, vis datofestede og tentative frister, og prøv travle dager og perioder.
3. **Uke og liste med felles tilstand:** vis heldag, klokkeslett og overlapp, og bevar dato/filter ved bytte.
4. **Grafisk budsjettår:** lever hele løpet på tvers av kalenderår med alle faktiske frister, felles detaljer, kilderundskriv og bevaring av kalenderens flervalg.
5. **Forslag fra valgt kalenderdato:** lever hele bidragsyterflyten gjennom eksisterende innsending, kø, godkjenning og publisert resultat.
6. **Samlet kompatibilitet og tilgjengelighet:** gjennomfør tverrgående navigasjon, roller, tastatur, mobil, periodegrenser og tidligere funksjoner på den samlede kandidaten.

Første del kan starte fra oppdatert hovedgren. Rekkefølgen innebærer avhengigheter mellom senere implementeringsoppgaver; disse skal registreres eksplisitt ved oppretting. De skal ikke merkes startklare før forutsetningene er levert. Små regresjoner og tilgjengelighet følger hver leveranse; den siste deloppgaven er samlet verifikasjon, ikke utsettelse av grunnleggende kvalitet.

#56 er fortsatt den eksisterende brede pilotprøven med egne avhengigheter. #42/#43/#55 styrer kalenderdeling og klientakseptanse. #47 og #49 beholder sine egne produktvalg. Ingen av disse oppgavene erklæres fullført av denne spesifikasjonen.

Produkteierens avklaringer: Atelier videreføres; kalenderen er hovedvisning; dagens funksjoner beholdes som faner/områder; bidragsytere kan starte forslag fra dato; flere konkrete budsjettår kan velges samtidig; Liste skal vise tydelig årsskille; grafisk budsjettår følger hele prosessen; kilderundskriv plasseres over Om fristen; eksisterende nettleser- og tjenestegrenser er godkjent. Ingen nye produktspørsmål gjenstår for denne spesifikasjonen.

Prototypereferansen er bevart på egen gren, uten endring av hovedgrenen. Primærkilder ved commit `a36f3e61b01118c66007cf178a760f3d383d1423`:

- [Atelier-kalender med flervalg, årsfarget liste og kilderundskriv](https://github.com/depmartin/Budsjettkalender/blob/a36f3e61b01118c66007cf178a760f3d383d1423/docs/prototypes/atelier-2026-10-23/prototype-kalender.html).
- [Grafisk budsjettår gjennom fire kalenderår](https://github.com/depmartin/Budsjettkalender/blob/a36f3e61b01118c66007cf178a760f3d383d1423/docs/prototypes/atelier-2026-10-23/prototype-budsjettaar.html).
- [Avgrensning og bruk av prototypene](https://github.com/depmartin/Budsjettkalender/blob/a36f3e61b01118c66007cf178a760f3d383d1423/docs/prototypes/atelier-2026-10-23/kalenderforslag.md).

Prototypene er kontrollert i isolert Edge med flervalg 2026+2028 uten 2027, tydelig farge i Liste, kildeplassering, tidslinjen oktober 2024–april 2027, årbytte, valgt milepæl og overgang til kalenderens måned. Begge mobilflater har sidebredde 390 av 390 piksler og ingen registrerte JavaScript-feil i disse scenarioene. Dette er prototypebevis og ikke akseptanse av implementert Blazor-app. Datobasert forslag er spesifisert, men ikke implementert i den lesende prototypen.

