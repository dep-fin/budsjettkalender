## Problem Statement

Brukerne trenger en tydelig, visuell oversikt over budsjettfrister i en faktisk kalender, med enkel veksling mellom måned, uke og liste. Dagens funksjoner finnes på flere arbeidsflater og må fortsatt være tilgjengelige. En ny kalender skal bevare funksjonene fra årshjul, forslag, eksport og administratorens arbeidsflyter. Årshjul samles under Budsjettår → Fristliste.

En bidragsyter som kjenner datoen for en ny frist, må i dag starte et separat forslag og registrere datoen på nytt. Det bryter sammenhengen mellom planlegging og innsending. Samtidig må en kalenderhandling aldri forveksles med direkte publisering eller gi lesere nye rettigheter.

## Solution

Bruk Atelier som visuelt og strukturelt utgangspunkt for videre utvikling. Kalenderen blir hovedvisning når brukeren åpner applikasjonen. Den viser måned, uke eller liste og har et rolig sidepanel for dato og utvalg på stor skjerm.

Behold dagens funksjoner som andre faner eller områder med samme tilgang som før. Kalender og Budsjettår er separate hovedfaner. Den nye grafiske Budsjettår-visningen følger ett valgt budsjettår gjennom flere kalenderår. Dagens Årshjul samles under Budsjettår → Fristliste; den separate Årshjul-fanen fjernes. Tidslinje og Fristliste er lokale alternativer for samme budsjettår. Eksisterende funksjoner og direkte innganger bevares, med videreføring til den samlede visningen der det trengs. Mine forslag og varsler er tilgjengelige for bidragsytere, Eksport og deling for dagens autoriserte roller, og Internkalender og Administrasjon etter eksisterende policyer. Administrasjon har seks hovedvalg: Budsjettår og frister, Årsmal, Grupper og innsyn, Eksport og deling, Internkalender og Administrator-innstillinger. Arbeid med faktiske frister samles under valgt budsjettår; generelle regler ligger i Årsmal. Faseinndeling er flyttet til Administrator-innstillinger. Eksisterende kø, opplasting, fristregister, generering, mal, innsyn, grupper og data beholdes gjennom lokal undernavigasjon.

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
24. Som bruker vil jeg finne dagens årshjulfunksjoner under Budsjettår → Fristliste, slik at jeg får én samlet listeinngang uten å miste funksjoner eller gamle direkte lenker.
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
58. Som bruker vil jeg bytte mellom Tidslinje og Fristliste under Budsjettår, slik at jeg kan velge grafikk eller en lesbar liste med de samme autoriserte fristene.
59. Som leser vil jeg få en egen Kilderundskriv-seksjon over Om fristen med lenke til det tilknyttede rundskrivet på regjeringen.no, slik at jeg enkelt kan lese kilden.
60. Som bruker vil jeg at frister uten offentlig rundskriv ikke får en oppdiktet kilde eller en tom kildeseksjon, slik at opphavet fremstilles korrekt.

61. Som leser vil jeg se en tydelig rød I dag-markør med dato i tidslinjen, slik at dagens plassering lett kan skilles fra budsjettårsfargen.
62. Som administrator vil jeg redigere fasenes navn og månedsgrenser i én felles fasemal, slik at inndelingen kan vedlikeholdes uten kodeendring.
63. Som administrator vil jeg at fasemalen gjelder alle budsjettår, slik at det ikke oppstår uønskede årsspesifikke varianter.
64. Som bruker vil jeg se de fem avklarte fasene med tydelige grenser, slik at jeg forstår hvor i prosessen en frist ligger.
65. Som administrator vil jeg redigere fasemalen uten å flytte eller endre fristene, slik at presentasjon og faktisk fristinformasjon holdes adskilt.
66. Som leser vil jeg få samme faseinndeling som administrator, slik at de to rollene leser prosessen likt.
67. Som administrator vil jeg ha samme formspråk og visninger som leseren, med kontekstuelle skrivehandlinger lagt til, slik at jeg slipper å lære en annen applikasjon.
68. Som administrator vil jeg opprette eller redigere en frist én gang og få samme identitet og verdier i Kalender, Budsjettår og Fristregister, slik at jeg ikke må vedlikeholde kopier.
69. Som administrator vil jeg se at et godkjent forslag blir tilgjengelig i begge relevante visninger, slik at publiseringsløpet henger sammen.
70. Som administrator vil jeg få tydelige feil ved ugyldige fasegrenser eller konflikt med en nyere mal, slik at inndelingen ikke overskrives eller blir selvmotsigende.
71. Som leser vil jeg bruke samme visninger uten redigeringshandlinger eller administrative opplysninger, slik at rollen min ikke endrer datavernet.
72. Som administrator vil jeg at den felles fasemalen bevares ved ny innlogging og omstart, slik at den ikke bare er en innstilling i min egen nettleser.

73. Som bruker vil jeg bruke minikalenderen i Kalenderens sidepanel til å velge dato og navigere i perioder, med markering av dager som har synlige frister i årsutvalget.
74. Som administrator vil jeg nå alle forvaltningsområdene gjennom lokal navigasjon også på liten skjerm, slik at administrasjonen beholder samme gjenkjennelige struktur som leserløsningen.

75. Som administrator vil jeg velge ett konkret budsjettår og finne frister, løpende forslag, dokumentinnlesing, Ny frist og generering samlet der, slik at arbeidet følger budsjettet jeg vedlikeholder.
76. Som administrator vil jeg ha en egen fane for eksisterende gjentaksregler og en egen fane Nye gjentaksregler med hele regelbyggeren, slik at oversikt og registrering ikke konkurrerer om plassen.
77. Som administrator vil jeg kombinere søk, flere kategorier og sammenhengsfilter i regellisten, slik at jeg raskt finner aktuelle regler og får et tydelig tomt resultat når ingenting er valgt.
78. Som administrator vil jeg se et lite milepælmerke på selvstendige regler som andre regler bygger på, slik at jeg finner utgangspunktene for avhengige frister.
79. Som administrator vil jeg at et avhengig mellomledd fortsatt merkes Avhengig selv når andre frister bygger videre på det, slik at det ikke forveksles med en selvstendig milepæl.
80. Som administrator vil jeg registrere fast dato, ukedag i måneden, ukedag fra og med en dato og før/etter en milepæl gjennom vanlige felter, slik at tidligere regelmuligheter beholdes uten kode eller JSON-innskriving.
81. Som administrator vil jeg se regelen som en vanlig setning og kontrollere beregnede datoer for flere budsjettår med tydelig kalenderår, slik at årsgrense og årsforskyvning kan forstås før lagring.
82. Som administrator vil jeg prøve hele malen med manuelle ankerdatoer, valgårsvalg og synlige feil i avhengige kjeder, slik at jeg kan kontrollere samspillet uten å publisere frister.
83. Som administrator vil jeg fortsatt eksportere og importere gjentaksregler med forhåndsvisning, advarsler og tydelig erstatt-alt-konsekvens, slik at de tidligere overføringsmulighetene består.
84. Som administrator vil jeg finne Administrator-innstillinger rett under Internkalender, med den felles faseinndelingen flyttet dit, slik at generelle innstillinger er skilt fra fristreglene.
85. Som administrator vil jeg se hvem som har administratortilgang og om den følger Entra eller er manuelt tildelt, slik at tilgangen har en forståelig kilde.
86. Som administrator vil jeg prøve utnevnelse og fjerning med eksempelpersoner i skissen, slik at den nye rolleforvaltningen kan vurderes før en særskilt backendimplementering.

87. Som administrator vil jeg sortere eksisterende gjentaksregler kronologisk etter beregnede datoer for et valgt budsjettår, slik at rekkefølgen gjennom hele budsjettløpet blir enkel å forstå.
88. Som administrator vil jeg bytte mellom tidligst først, senest først og norsk tittelrekkefølge med filtre beholdt, slik at listen kan undersøkes uten å endre reglene.
89. Som administrator vil jeg se tentative måneder med bevart presisjon og uberegnbare regler sist med forklaring i begge datoretninger, slik at sorteringen ikke gjetter eller skjuler frister.

## Implementation Decisions

- **Valgt retning:** Atelier er besluttet av produkteier. Videre varianter skal utvikle Atelier, mens Panorama og Arbeidsro bevares som tidligere sammenlikninger. Ingen ny designkonkurranse er en forutsetning for implementering.
- **Samlet navigasjon:** Kalender er standard ved ordinær åpning. Årshjulets funksjoner samles i Budsjettår → Fristliste, og den separate Årshjul-fanen tas ut. Eksisterende listeatferd, forslag, varsler, eksport, deling, internkalender og alle administratorfunksjoner beholdes. Faner er navigasjon til arbeidsflater, med lokal undernavigasjon der funksjonene er mange. Eksisterende direkte innganger og rollepolicyer skal fortsatt fungere.
- **Teknologi:** Blazor Interactive Server og eksisterende modulgrenser videreføres. Prototypen er visuell referanse og skal ikke kopieres direkte som produksjonskode. Et eventuelt kalenderbibliotek er et internt implementeringsvalg som må oppfylle denne spesifikasjonen.
- **Flervalg av budsjettår:** Kalenderens årsfilter er et sett av valgte tilgjengelige budsjettår, representert med avkryssing og eksplisitt Velg alle. For eksempel 2026 og 2028 viser deres union, avgrenset av øvrige filtre og serverens synlighet, uten at 2027 følger med. Ingen valgte år gir null treff i den nye flaten; det skal ikke uventet tolkes som alle. Årsvalgene utledes bare fra autoriserte data og beholder øvrige tilgjengelige år ved avgrensning. Dagens leseport støtter allerede en samling år; den skal ikke snevres inn til et enkelt år.
- **Minikalender:** Behold datovelgeren i Kalenderens sidepanel på stor skjerm. Dato- og månedsvalg oppdaterer den samme kalenderkonteksten som hovedvisningen. Dagsmarkeringer utledes fra autoriserte frister i aktive filtre; markeringene skal ikke avsløre skjulte frister. På liten skjerm må tilsvarende datonavigasjon være tilgjengelig.
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
- **Grafisk Budsjettår:** Denne visningen fokuserer ett valgt budsjettår og er et annet perspektiv enn kalenderens samtidige flervalg. Kalenderens årsutvalg beholdes separat. Tidslinjen viser hele løpet, også historikk, som standard oktober t−2 til april t+1. Om faktiske synlige frister ligger utenfor rammen, utvides den eller de vises i en tydelig tilknyttet del; ingen frist utelates stille. Fristliste samler det eksisterende årshjulets funksjoner. Gamle direkte innganger videreføres til den samlede visningen uten funksjonstap.
- **Milepæler og faktiske frister:** Eksemplet for budsjettår 2026 går fra oktober 2024, via innspill januar 2025, marskonferanse mars 2025, augustkonferanse august 2025, fremleggelse oktober 2025, revidert budsjett april 2026, nysaldering november 2026 og regnskap april 2027. Dette er produkteierens illustrasjon av ønsket struktur, ikke nye vedtatte datoer. Produksjonsvisningen henter faktiske, autoriserte, godkjente frister etter Budsjettår, med bevart presisjon. Prosessoverskrifter eller faser oppretter ingen hendelser og fyller ikke inn manglende frister. Løp i domenemodellen identifiserer en funksjon; det er ikke synonymt med hele budsjettårets prosess eller rundskrivnummer.
- **Tetthet og detaljer i tidslinjen:** Flere frister på samme tidspunkt samles visuelt med en synlig vei til alle enkeltfrister. Felles fristdetaljer og gyldig kildelenke gjenbrukes. Listealternativ, tastatur og mobil må gi tilgang til det samme autoriserte innholdet. En månedspresis milepæl får ingen oppdiktet dagsdato.
- **Tilgangsregler:** POL krever fortsatt aktivt valg. Endringsforslag endrer innhold og bevarer synlighet. SBRs interne gjøremål forblir egne enheter og sendes ikke til andre roller. Abonnementets tokenvern og målgruppe endres ikke.
- **Data og tid:** Det er ikke identifisert behov for ny databasemodell for måneds-/ukevisning eller forslag fra dato. Oslo-tid og norske kalenderkonvensjoner brukes ved visning av dato og klokkeslett. Eventuell migrasjon krever en særskilt dokumentert begrunnelse.

- **Avklart felles fasemal 27. oktober:** Én felles mal gjelder alle budsjettår; ingen årsspesifikke unntak. Standard: Forberedelser oktober t−2–februar t−1; Utarbeidelse av budsjettforslag mars–september t−1; Fremleggelse og vedtak oktober–desember t−1; Gjennomføring og revidering januar–desember t; Regnskap januar–april t+1. Faseskifte skjer den første i måneden. Fasemalen angir relative måneder, ikke oppdiktede fristdatoer.
- **Administrasjon av fasemal:** Bare administrator kan oppdatere navn og månedsgrenser. Visningens faser skal leses fra samme lagrede mal for alle roller og budsjettår. Valider navn, stigende grenser og siste inkluderte måned. Faseendring påvirker gruppering/presentasjon, aldri fristens identitet, dato, presisjon, innhold, publiseringsstatus, kilde eller synlighet. Frister utenfor malens tidsrom forblir synlige.
- **Lagring av fasemal:** Tidligere vurdering om at selve kalenderen ikke krever ny datamodell omfatter ikke denne nye, redigerbare konfigurasjonen. Lever varig lagring og administratorbeskyttet oppdatering gjennom én tydelig applikasjonsgrense. Endelig skjema og eventuell migrasjon avgrenses i egen vertikal oppgave. En gammel fane skal ikke stille overskrive en nyere mal.
- **Fast Atelier-utgangspunkt:** Den valgte leserløsningen er referansen for alle nye administratorflater. Administrator får Ny frist, Rediger frist og Rediger faser i den samme kalenderen/tidslinjen. Godkjenningskø, dokumentinnlesing og generering har lokal navigasjon under Budsjettår og frister. Genererte forslag behandles fortsatt adskilt fra den løpende køen. Denne utvidelsen gir ingen tillatelse til å fjerne eksisterende funksjoner.
- **Samme fristdata:** Kalender, Budsjettår med Tidslinje/Fristliste og Fristregister bruker de samme autoriserte fristobjektene. Oppretting, redigering og godkjenning oppdaterer ett lagret fristobjekt. Ulike dato-/årsfiltre kan gi ulike utvalg, men ikke ulike verdier eller kopier av den samme fristen.
- **Administrasjonsmeny, avklart 27. oktober:** De seks valgene er Budsjettår og frister, Årsmal, Grupper og innsyn, Eksport og deling, Internkalender og Administrator-innstillinger. Sistnevnte står under Internkalender. Det konkrete arbeidsåret avgrenser fristregister, løpende forslag, dokumentmetadata og generering. Forslag i andre år skal ha en synlig vei inn, eksempelvis telling og snarvei, slik at årsavgrensning ikke skjuler ventende arbeid.
- **Regelliste og registrering:** Årsmal åpner Eksisterende gjentaksregler. Listen har tittel, kategori, lesbar beregning og sammenheng med vei til redigering og eksempeldatoer. Søk, kategoriavkryssing og sammenheng danner snitt; null kategorier gir null treff. Nye gjentaksregler åpner hele regelbyggeren på egen flate. Filtre beholdes ved redigering/retur, og ulagrerte felt skal ikke forsvinne utilsiktet.
- **Visuelt milepælmerke:** Merket er avledet fra eksisterende regler og ankerkoblinger. En ikke-relativ regel som er anker for andre regler er en selvstendig milepæl. En ikke-relativ regel uten avhengige regler er øvrig selvstendig. En relativ regel er alltid Avhengig, også som mellomledd med egne avhengige. Filtervalgene kan være Alle, Milepæler, Andre selvstendige og Avhengige. Dette er ingen ny domenetype, lagret milepæl-boolean eller endret beregningslogikk. Avhengige kjeder følger ankerberegningen som før; merket autoriserer ikke automatisk flytting/publisering av allerede godkjente frister.
- **Kronologisk regelliste:** Standard er tidligst først etter eksisterende beregning for et eksplisitt valgt målbudsjettår. Beregnede kalenderdatoer i t−2, t−1, t og t+1 sorteres i én tidsrekkefølge, ikke bare etter måned/dag. Administrator kan velge senest først eller norsk tittelrekkefølge. Tentative frister plasseres på månedsnivå uten presentert oppdiktet dagsdato. Like datoer får stabil tittel/identitetsrekkefølge. Uberegnbare regler står sist i begge datoretninger med forklaring. Filtre og sortering virker sammen og bevares ved redigering/retur; ingen regel, forslag eller frist lagres som følge av visningsendringen.
- **Kodefri regelbygger:** Gjenbruk eksisterende tre regeltyper og parserkontrakt. RelativUkedag får to tydelige UI-valg: forekomst i måneden og første ukedag fra og med dag. Eksempelvis mandag fra og med 20. juli gir vinduet 20.–26. juli. Forekomst 1–5 med siste som reserve, positiv/negativ årsforskyvning, valgårssensitivitet, kategori og funksjon/løp bevares. Relativ til milepæl velger eksisterende eller manuelt fastsatt anker og kalenderdager før/etter, inkludert 0. JSON er et internt lagrings-/overføringsformat; vanlig registrering skal bruke felter.
- **Bevarte beregningsvalg:** Fast dato og ukedagsregler følger dagens helgejustering innen samme kalenderår, uten ny helligdagsmodell. Relativ forskyvning teller kalenderdager uten ny helgejustering. Valgår avgjøres av datoens kalenderår; stortingsvalgår gir tentativ måned, kommunevalgår flagg med bevart dagsdato. Manuelt anker overstyrer, og tentativ presisjon arves i kjeder. Manglende/sirkulært anker og ugyldig dato gir synlig feil fremfor gjettet dato. Én regel gir ett beregningsresultat også når flere deler løp; eksisterende representant for ankerløpet beholdes.
- **Prøving, generering og maloverføring:** Prøv malen er en sideeffektfri forhåndsvisning. Generering til behandling skjer under valgt konkret budsjettår og bruker samme lagrede mal, med bevart kategori, presisjon og synlighetskontroll. Regelendring flytter ikke tidligere publiserte frister. Maleksport og import beholder versjonert format, gamle kompatibilitetsvalg, advarsler og erstatt-alt-semantikk; en forhåndsvisning/konsekvensforklaring erstatter ikke servervalidering. Fasemal er separat konfigurasjon og legges ikke stille til gjentaksreglenes gamle eksportformat.
- **Administrator-innstillinger:** Den ene felles fasemalen forvaltes her; snarveier fra tidslinjen kan åpne samme område. Administratoroversikten skal skille Entra-utledet og manuelt tildelt tilgang. Skissen viser utnevnelse/fjerning for eksempelpersoner; Entra-styrt tilgang vises som forvaltet i Entra. Den nye reelle manuelle rolleforvaltningen er ikke levert av skissen og krever egen avgrenset oppgave med serverpolicy, stabilt identitetsvalg, revisjonsspor, egen/siste administrator og samspill med Entra avklart. Ingen faktisk rolle eller gruppe endres av prototypehandlingen.
- **I dag:** Bruk en tydelig rød strek med tekstetikett og faktisk Oslo-dato. Tekst og fristkort skal fortsatt kunne leses; markøren skal ikke ligne en friststatus eller endre budsjettårsfargene.

## Testing Decisions

- **Avklart med produkteier:** Eksisterende reelle nettlesertester er hovedgrensen. Produkteier har eksplisitt bekreftet dette i samtalen 23. oktober 2026.
- **Hovedscenario:** Bidragsyter åpner standardkalenderen, velger en dato, starter forslag, ser riktig forhåndsutfylt dato, sender inn og får kvittering. Forslaget finnes i Mine forslag og administrators kø, men ikke i ordinær fristkalender. Administrator godkjenner; en ny autorisert lesing viser nøyaktig én publisert frist med korrekt dato og besluttet synlighet.
- **Nettlesergrense:** Utvid eksisterende Playwright-scenarioer mot ekte Kestrel og faktiske Blazor-handlinger. Bruk tilgjengelige navn og synlige utfall. Kalender-, forslag-, eksport-, abonnements- og internkalenderscenarioene er eksisterende eksempler på tilsvarende testnivå. Ingen ny parallell testarkitektur innføres.
- **Navigasjonsregresjon:** Kontroller at kalender er hovedinngang, og at Budsjettår → Fristliste samler årshjulfunksjonene uten separat Årshjul-fane, og at eksisterende listeatferd, dedikert forslag, varsler, eksport/deling og administrator-/SBR-områder fortsatt kan nås og brukes med samme roller. Eksisterende direkte innganger skal ikke forsvinne. Tidligere tester oppdateres bare for tilsiktet presentasjonsendring; deres atferdsbevis beholdes.
- **Rolleprøve:** Bruk minst leser, FA-bidragsyter, annen eksisterende bidragsyter og administrator. Leser har ikke forslagskontroll eller skriveadgang. Administrator beholder sitt eget løp. Kontroller faktisk serveravvisning der tilgang forsøkes uten rettighet; skjulte knapper alene er ikke bevis.
- **Eksisterende tjenestegrenser:** Behold eller utvid kontrakttester ved IFristlesing og IForslagsinnsending når periode, presisjon eller forhåndsutfylling påvirker dem. Test bare returnert atferd og lagret resultat, uten å binde testene til CSS-klasser, private hjelpemetoder eller komponentenes interne tilstand.
- **Synlighet og publisering:** Kontroller tillatt og skjult innhold, POL-isolasjon, eierskap til forslag, ingen publisering ved avbryt eller feil, og uendret synlighet for endringsforslag. Eksisterende godkjennings-/samtidighetskontrakter gjenbrukes. Reell SQL Server-kontroll beholdes når publisering og lagring inngår.
- **Årsfilter og liste:** Velg to ikke-sammenhengende budsjettår og bekreft deres eksakte union, at mellomliggende år ikke vises, og at kategori/søk fortsatt danner snitt. Prøv Velg alle, ingen år, ett år og bytte mellom visninger. Kontroller at årsvalg og tellinger ikke avslører skjulte data. Liste skal ha eksplisitt budsjettår og samme fargebetydning som Måned/Uke.
- **Grafisk budsjettår:** Velg budsjettår 2026 og prøv hele vinduet fra 2024 til 2027. Sammenlikn alle viste frister med IFristlesing for samme budsjettår, inklusive historikk, tentative frister, flere frister på ett tidspunkt og frister utenfor standardvinduet. Kontroller kalenderår/t-markering, årbytte, listealternativ og overgang tilbake til kalenderen uten å miste dens flervalg. Veiledende fasetekst skal ikke fremstå som nye publiserte data.
- **Kilderundskriv:** Klikk en kildekoblet frist som vanlig leser og verifiser lenken over Om fristen med korrekt, tillatt adresse. Prøv også kildefri frist, ugyldig adresse og skjult frist. Eksisterende kildelenke-/HTTP-/tjenesteregler er tidligere testbevis som videreføres; det innføres ingen klientbasert utvidelse av URL-tillatelser.
- **Minikalender og samlet Fristliste:** Prøv datovalg og månedsskifte med bevart årsutvalg, inkludert årsskifte. Kontroller at markeringer følger autorisert innhold. Åpne gamle årshjul-innganger, sammenlikn fristutvalget i Fristliste og Tidslinje, og kontroller mobiltilgang til alle administratorområder.
- **Kalenderprøver:** Verifiser bytte mellom måned/uke/liste med beholdt dato og filtre, alle frister på travle dager, samtidige klokkeslett, heldag, tidlige/sene klokkeslett, årsskifte, skuddår og måneder med seks uker.
- **Tentative prøver:** En frist med månedspresisjon må være tilgjengelig fra en uke som ikke inneholder dens avledede sorteringsdag. Test også uke over månedsskifte. Den må verken forsvinne eller tegnes på en oppdiktet dag.
- **Registreringsprøver:** Kontroller korrekt dato, redigering av forhåndsvalg, budsjettår som avviker fra kalenderår, null/tomt utvalg, ugyldige felt, bevart inndata, avbryt og retur. Kontroller at klikk på eksisterende frist ikke åpner nyregistrering, og at den vanlige forslagsinngangen fortsatt fungerer.
- **Tilgjengelighet og skjermbredde:** Kontroller tastatur, synlig fokus, dialognavn, Escape/fokusretur, feltfeil, skjermleserstatus, kontrast, zoom og mobil. Liste er et tilgjengelig alternativ. Samlet pilotakseptanse samordnes med #56 og erstattes ikke av prototypebilder.
- **Ferdig kodekandidat:** Hver avgrensede PR skal ha relevante regresjoner, Standards-/Spec-review, nødvendig sikkerhetskontroll og grønne ordinære, SQL Server- og nettleserjobber etter prosjektets regler. Lokal SQLite/demo dokumenterer ikke ekte Entra, Azure eller Outlook.

- **Tillegg for felles fasemal:** Prøv administratorens endring gjennom faktisk UI og den avgrensede lagringsporten. Kontroller at endringen er synlig for leser, flere budsjettår og etter ny sesjon/omstart. Test avvisning for leser, ugyldige grenser og gammel versjon. Sammenlikn lagrede fristidentiteter og alle fristverdier før og etter faseendringen; de skal være uendret.
- **Samme frist i flere visninger:** Opprett en frist og kontroller samme identitet/dato/innhold i relevant kalenderperiode og budsjettår. Rediger via én visning, og les gjennom den andre uten separat publisering. Gjenta med godkjenning av forslag og kontroller synlighet. Fravær på grunn av eksplisitt filter skal ikke forveksles med manglende synkronisering.

- **Regelliste og fleksible regler:** Prøv søk + ikke-sammenhengende kategorivalg + sammenhengsfilter, tomt kategoriutvalg og retur fra redigering. Kontroller at selvstendig anker får lite merke, at et avhengig mellomledd ikke gjør det, og at merket ikke endrer resultatene. Prøv hele regelbyggeren gjennom UI med 20.–26. juli over flere år, n-te/femte ukedag, skuddår, årsgrenser, årsforskyvning og relative kjeder. Gjenbruk tjenestegrensene ved IMaltjeneste, Regelparser, Genereringsberegning og IMalDatautveksling; ingen ny parallell regelmotor.
- **Samlet administrasjon:** Opprett/rediger malregel, beregn for et konkret budsjettår uten publisering, og godkjenn ett forslag med korrekt kategori og presisjon i de ordinære visningene. Prøv forslag i andre arbeidsår, flyttet faseforvaltning og mobil tilgang til de seks områdene. Prototypehandlinger for administratorutnevnelse er ikke serverautorisering eller akseptanse av reell rolleforvaltning.

## Out of Scope

- Å fjerne årshjulets funksjoner, eksisterende listeatferd, forslag, eksport, varsler, internkalender eller administratorfunksjoner. Sammenslåingen av Årshjul-fanen i Budsjettår → Fristliste er avtalt.
- Å velge mellom Panorama og Arbeidsro på nytt som hovedretning; Atelier er valgt.
- React-omskriving, ny backend, parallell kalenderdatabase eller direkte innføring av prototypens JavaScript i produksjonsappen.
- Automatisk publisering, nye funksjonsroller, nye rettigheter basert bare på FA-medlemskap eller automatisk POL-valg.
- Å endre feedpolicy, tokenvern, eksportformat, offisiell Word-mal eller reell Outlook-integrasjon. Disse følger egne eksisterende oppgaver.
- Ny kildeinnhenting, robotuttrekk, matching av dokumentversjoner eller PDF-kandidatbehandling.
- Dra-og-slipp for å flytte frister, utledning av møtelengder eller nye fullført/forsinket-tilstander på publiserte frister.
- Å utvide brukerforslag med klokkeslett. Kalenderen viser eksisterende tidfestede frister, mens den nye forslagsinngangen i første omgang forhåndsutfyller dato og dagens øvrige støttede felter.
- Varige personlige visningsinnstillinger, ny idempotensordning, ny medlemskapsadministrasjon eller en generell funksjonell omskriving av administratorverktøyene. Klikkbare Atelier-skisser for alle administratorområder er en visuell referanse; de er ikke automatisk et mandat til nye backendfunksjoner.
- Reell manuell utnevnelse/fjerning av administratorer som del av selve prototypen. Den godkjente visuelle skissen skal videreføres til egen avgrenset implementeringsoppgave før ny backend/tilgangspolicy bygges.
- Produksjonsutrulling og generell mergeautorisasjon.

## Further Notes

Dette er den samlede produktbestillingen etter valget av Atelier. Den erstatter tidligere anbefaling om Panorama og presiserer at ny kalender er én visning i den eksisterende løsningen. De øvrige tiltakene i den opprinnelige UX-gjennomgangen er bakgrunn, ikke automatisk del av denne implementeringen.

## Implementeringsplan – konkrete tickets

Dette er en koordinerende spesifikasjon, ikke én samlet kodeoppgave. **Start med [UX01 · #76](https://github.com/depmartin/Budsjettkalender/issues/76)** og implementer én ticket om gangen. De tidligere foreslåtte syv delene er erstattet av følgende publiserte oppgaver.

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

UX01–UX18 er en bevisst serialisert leveranseplan. Blocked by uttrykker både leveranseforutsetning og avtalt rekkefølge; ikke alle nabotickets har en isolert teknisk avhengighet. Bare UX01 er merket ready-for-agent nå. Etter verifisert levering/lukking av forgjengeren skal neste kodeoppgave kontrolleres og merkes startklar. Åpne blokkeringer skal ikke merkes som startklare bare fordi spesifikasjonen er ferdig.

AD01/AD02 er egne oppgaver for den fremtidige reelle manuelle administratorforvaltningen; AD01 avventer også #39. De er ikke skjult innføring av ny rollepolicy i UI-arbeidet. UX17 leverer den lesende administratoroversikten. Historie 86 er allerede demonstrert som prototype; reell mutasjon har egne beslutnings-/implementeringsporter.

Tilgjengelig connector eksponerer ikke native avhengighets-/subissue-mutasjoner. Hver ticket har eksplisitt Blocked by og lenke tilbake hit. Denne tabellen er den samlede lenkeoversikten; ticketens faktiske livsløp styrer videre arbeid.

Første del kan starte fra oppdatert hovedgren. Rekkefølgen innebærer avhengigheter mellom senere implementeringsoppgaver; disse skal registreres eksplisitt ved oppretting. De skal ikke merkes startklare før forutsetningene er levert. Små regresjoner og tilgjengelighet følger hver leveranse; den siste deloppgaven er samlet verifikasjon, ikke utsettelse av grunnleggende kvalitet.

#56 er fortsatt den eksisterende brede pilotprøven med egne avhengigheter. #42/#43/#55 styrer kalenderdeling og klientakseptanse. #47 og #49 beholder sine egne produktvalg. Ingen av disse oppgavene erklæres fullført av denne spesifikasjonen.

Produkteierens avklaringer: Atelier videreføres; kalenderen er hovedvisning; dagens funksjoner beholdes som faner/områder; bidragsytere kan starte forslag fra dato; flere konkrete budsjettår kan velges samtidig; Liste skal vise tydelig årsskille; grafisk budsjettår følger hele prosessen; kilderundskriv plasseres over Om fristen; eksisterende nettleser- og tjenestegrenser er godkjent. Ingen nye produktspørsmål gjenstår for denne spesifikasjonen.

Prototypereferansen er bevart på egen gren, uten endring av hovedgrenen. Primærkilder ved commit `a36f3e61b01118c66007cf178a760f3d383d1423`:

- [Atelier-kalender med flervalg, årsfarget liste og kilderundskriv](https://github.com/depmartin/Budsjettkalender/blob/a36f3e61b01118c66007cf178a760f3d383d1423/docs/prototypes/atelier-2026-10-23/prototype-kalender.html).
- [Grafisk budsjettår gjennom fire kalenderår](https://github.com/depmartin/Budsjettkalender/blob/a36f3e61b01118c66007cf178a760f3d383d1423/docs/prototypes/atelier-2026-10-23/prototype-budsjettaar.html).
- [Avgrensning og bruk av prototypene](https://github.com/depmartin/Budsjettkalender/blob/a36f3e61b01118c66007cf178a760f3d383d1423/docs/prototypes/atelier-2026-10-23/kalenderforslag.md).

Prototypene er kontrollert i isolert Edge med flervalg 2026+2028 uten 2027, tydelig farge i Liste, kildeplassering, tidslinjen oktober 2024–april 2027, årbytte, valgt milepæl og overgang til kalenderens måned. Begge mobilflater har sidebredde 390 av 390 piksler og ingen registrerte JavaScript-feil i disse scenarioene. Dette er prototypebevis og ikke akseptanse av implementert Blazor-app. Datobasert forslag er spesifisert, men ikke implementert i den lesende prototypen.


### Avklaringer 27. oktober 2026

Produkteier har bekreftet den valgte løsningen som fast referanse for videre endringer. De to spørsmålene om faseforvaltning er avklart: kun én felles standardmal for alle år, og månedsgrensene beskrevet ovenfor. Ingen unntak per budsjettår skal innføres.

[Administratorutkast og beskrivelse](https://github.com/depmartin/Budsjettkalender/tree/77f8b6d0873e8fe164b01d5a6feb76419ada4b0f/docs/prototypes/atelier-admin-2026-10-27) er bevart på prototypegrenen. Utkastet demonstrerer de samme fristobjektene i Kalender, Budsjettår og Fristregister, ny/redigert frist, godkjenning, felles faseendring og lesermodus. Det starter på Budsjettår for å vise fasene; Kalender er fortsatt avtalt hovedinngang i appen.

Nettleserkontroll med konstruerte data viste en ny frist i tidslinje og kalender, en redigering tilbake i tidslinjen, godkjenning til samme datasett, felles faseendring i 2027 og i leservisningen, og uendrede fristdata etter faseendring. Mobil sidebredde var 390/390 og faseeditor 364/364, uten registrerte JavaScript-feil i de prøvde flytene. Dette er prototypekontroll; ingen produksjonskode, varig faselagring eller reell autorisasjon er levert av prototypen.


### Utvidet administratorutkast og navigasjonsvalg 27. oktober 2026

Produkteier har nå eksplisitt valgt å samle den separate Årshjul-fanen under **Budsjettår → Fristliste**, og bedt om alle administratorområdene som klikkbare arbeidsflyter med eksempeldata. Minikalenderen skal være tilbake i Kalenderens sidepanel. Disse avklaringene erstatter tidligere ordlyd om egen Årshjul-fane.

[Ny Atelier-skisse med arbeidsflyter](https://github.com/depmartin/Budsjettkalender/tree/d57db15a1abaaa155f28d47cb0bcd60a4f0a2e76/docs/prototypes/atelier-admin-arbeidsflyter-2026-10-27) er bevart på prototypegrenen. Kalender er startvisning. Skissen har kø med justering/godkjenning/avvisning, fristregister, felles fasemal, dokumentskjermer, årsmal med faste/relative regler, generering med enkeltgodkjenning, grupper/innsyn, eksport/deling, demokopi og internrunder/gjøremål. Alle endringer gjelder konstruerte data i nettleserminnet. Den foregående skissen er bevart.

**Dokumentbehandling er utsatt etter avtale.** Nå vises filvalg/metadata og en separat forhåndsdefinert uttrekksoversikt. Valgt PDF blir verken lest eller behandlet og oppretter ingen forslag. En senere prototype skal demonstrere dokumentvalg → uttrekksforslag → kontroll/godkjenning → frist i begge visninger. Det gjenstående arbeidet skal tas med ved senere oppdeling; denne skissen erklærer ikke PDF-flyten levert. Word og kalenderfil er forhåndsvisninger, JSON inneholder eksempeldata og abonnementslenkene bruker et ugyldig demonstrasjonsdomene. Produksjonens tidligere funksjoner, policyer og særskilte oppgaver består.

Edge-kontroll prøvde dato i minikalender, sju dagers uke, samlet Fristliste, dokumentmetadata uten behandling, oppretting av gjentaksregel, generering uten publisering, enkeltgodkjenning, ny gruppe, gruppeinnsyn uten redigeringskontroller, eksportforhåndsvisninger og JSON-nedlasting, demolenke, kopiering/gjenoppretting, internrunde, fullført gjøremål og synkronisering. Kalender og dokumentskjerm hadde sidebredde 390/390 på mobil, og ingen JavaScript-feil ble registrert. Dette er prototypebevis; eksisterende avtalte nettleser-/tjenestegrenser kreves fortsatt ved implementering.


### Godkjent årsmal og samlet administrasjon – siste avklaring 27. oktober 2026

Produkteier har godkjent den fleksible årsmal-demoen og bestilt følgende endelige justeringer: egen liste over eksisterende regler med kategori-/sammenhengsfilter og redigering, separat Nye gjentaksregler, og Administrator-innstillinger under Internkalender. Faseinndeling flyttes dit. Spørsmålene er besvart: Et avhengig mellomledd forblir avhengig, milepælmerket er rent visuelt, og skisse av manuell utnevnelse/fjerning av administratorer skal være med.

Gjeldende primærkilde er nå [samlet Atelier-skisse og beskrivelse](https://github.com/depmartin/Budsjettkalender/tree/aa5c2cbecfbf3131409f8b84ed1e92acb9129850/docs/prototypes/atelier-samlet-2026-10-27). [Referanseindeksen](https://github.com/depmartin/Budsjettkalender/blob/aa5c2cbecfbf3131409f8b84ed1e92acb9129850/docs/prototypes/README.md) fastsetter hva fremtidig Atelier-arbeid skal bygge på og peker til historiske varianter. [Den tidligere separate årsmal-demoen](https://github.com/depmartin/Budsjettkalender/tree/aa5c2cbecfbf3131409f8b84ed1e92acb9129850/docs/prototypes/atelier-aarsmal-2026-10-27) er også arkivert. De tidligere referansene over beholdes som historikk og overstyrer ikke denne avklaringen.

Den samlede skissen bevarer Kalender som start, minikalender, måned/uke/liste, Budsjettår med tidslinje/Fristliste, kilderundskriv, samme fristdata, kø/godkjenning og øvrige administratorarbeidsflyter. Årsmalens nye liste og register bruker de fleksible reglene, og faktisk demogenerering under arbeidsåret bruker den samme redigerte malen. Faseendring deles med tidslinjen. PDF-behandling er fortsatt utsatt; dette arbeidet erklæres ikke ferdig. Skissen endrer ingen reelle brukere eller tilganger.

Sluttkontroll i Edge bekreftet syv eksempelregler, separat liste/form, kombinasjonsfilter, avhengig mellomledd, lagret ny/redigert datovinduregel, utnevnelse/fjerning i demoen, faseendring delt med tidslinjemodellen, generering fra den nylig endrede malen uten publisering, enkeltgodkjenning, annen års kø via snarvei, utsatt PDF-knapp og tilgang til øvrige områder. Hovedside var 390/390 piksler og regelmodul 358/358 på mobil; ingen JavaScript-feil ble registrert. Dette er prototypekontroll, ikke testbevis for produksjonsappen.

Oppdelingen ovenfor suppleres med en egen vertikal oppgave for kodefri årsmal og filtrerbar regelliste. Reell manuell administratorforvaltning avgrenses særskilt etter skissen og skal ikke snevres inn til bare en ny knapp. Denne leveransen er varig spesifikasjon/prototypereferanse på egen gren; hovedgren, produksjonskode og utrulling er uendret.


### Sortering og sesjonsoverlevering 27. oktober 2026

Produkteier har bedt om kronologisk regelliste og full oppdeling av denne spesifikasjonen i konkrete GitHub-tickets. Sorteringen er lagt inn i [den samlede skissen](https://github.com/depmartin/Budsjettkalender/tree/36dd6f06e471113b98571d24ebd93dd044b848fc/docs/prototypes/atelier-samlet-2026-10-27), kontrollert med årsvalg, filtre, begge tidsretninger og uendrede regler. Uberegnbar eksempelregel stod sist også ved synkende sortering; mobilbredde var 390/390 og ingen JavaScript-feil ble registrert. Dette er prototypebevis, ikke produksjonsakseptanse.

De 20 publiserte ticketene #76–#95 har avgrensning, brukerhistorier, akseptansekriterier, testgrense og eksplisitte avhengigheter. #75 er fortsatt åpen til den avtalte koden er levert og kontrollert; planlegging og prototyper lukker ikke produktbestillingen. Testgrensene som tidligere er godkjent av produkteier videreføres uten ny intervju-/godkjenningsrunde. Ny reell rolleforvaltning må først gjennom AD01s beslutning om policy og høy testgrense.

[Overlevering med full oppstartsinstruks og ticketkart](https://github.com/depmartin/Budsjettkalender/blob/codex/atelier-kalender-prototype-2026-10-23/docs/prototypes/atelier-samlet-2026-10-27/overlevering.md) skal brukes når arbeidet gjenopptas. [Gjeldende referanseindeks](https://github.com/depmartin/Budsjettkalender/blob/codex/atelier-kalender-prototype-2026-10-23/docs/prototypes/README.md) peker til den samlede skissen og denne planen. Ingen produksjonskode, hovedgren eller utrulling er endret av planleggingen.
