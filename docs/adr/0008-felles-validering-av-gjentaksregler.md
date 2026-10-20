# Én kontroll av gjentaksreglenes parameterkontrakt

Manuell mallagring og JSON-import hadde hver sin parameterkontroll. Deserialisering
alene godtok blant annet FastDato `{}` med måned/dag 0 og ukjent regeltype. Regelparser
i Application eier nå både tolkning og validering av JSON-kontrakten. Maltjeneste og
MalDatautveksling bruker samme Valider-kall; Domain beholder rene datoberegninger uten
JSON- eller importavhengigheter. Typede parserkall gir FormatException også for ugyldig
JSON og null, slik eksisterende generering og forhåndsutfylling kan håndtere eldre regler.

FastDato krever måned og dag. RelativUkedag krever måned, kjent ukedag og enten fra_dag
eller uke. Fra_dag har forrang og gjør uke valgfri; uten fra_dag kreves uke 1–5. Dette
strammer inn tidligere klamping av 0 og vilkårlige forekomstnumre. En femte forekomst som
ikke finnes, bruker fortsatt siste forekomst. Måned/dag kontrolleres mot et fast skuddår,
slik at 29. februar kan brukes i en flerårig mal mens 30. februar avvises. Om datoen finnes
i et bestemt målår, avgjøres ved beregningen. Årsforskyvning er valgfri med standard 0.
RelativTilMilepael krever et ikke-tomt ankerløp og eksplisitt offset_dager; 0 og negative
verdier er tillatt. Forskyvningene får ingen vilkårlig grense utover heltallstypen. Et
anker trenger ikke egen regel ved lagring, siden administrator kan sette datoen manuelt.

Feilresponsen hører til inngangen: manuell oppretting/oppdatering gir Valideringsfeil før
mutasjon. Import beholder kontrakten om én advarsel og utelating per ugyldig regel.
Den er fortsatt erstatt-alt: bare filas gyldige regler blir den nye malen, også når
ingen er gyldige. Vi avviser ikke hele fila for en regel som kan utelates. Filversjon,
null-liste/-rad og dupliserte ID-er avviser derimot hele importen før endring.
Manuell lagring krever tittel utenfor parameterkontrollen. Eldre importfiler uten tittel
beholdes; null, tom og blank tittel normaliseres til tom tekst før lagring.

Alle importkandidater klargjøres og valideres før eksisterende regler spores for sletting
eller nye regler legges til konteksten. Sletting og innlegging lagres i én SaveChangesAsync
med leverandørens transaksjon. En lagringsfeil ruller tilbake hele erstatningen, og
operasjonskonteksten disponeres etter ADR-0006. Testene ved IMaltjeneste og
IMalDatautveksling bruker samme ugyldige datasett, kontrollerer blandet og helt ugyldig
import og viser rollback med en AFTER DELETE-trigger samt vellykket ny import på samme
tjeneste. Ingen publisering, synlighet eller feed-policy endres.
