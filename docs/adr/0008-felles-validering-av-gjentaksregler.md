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

## Feil ved beregning

Parameterkontrollen vurderer malens struktur; en datoforskyvning avhenger også av målåret
eller ankerets dato. Derfor beholder manuell lagring og import hele int-området for både
offset_dager og aar_forskyvning. Ved generering gir et resultat utenfor
0001-01-01–9999-12-31 en norsk BeregnetRegel.Feil og Dato = null. FastDato og RelativUkedag
summerer målår og årsforskyvning som long og kontrollerer 1–9999 før datokonstruksjon.
RelativTilMilepael håndterer AddDays-feil ved selve beregningen. Femte ukedag klampes til
siste forekomst før en dato konstrueres, slik at desember 9999 også kan beregnes.

En feil forplanter seg gjennom alle avhengige regler, også når ankeret løses rekursivt.
Manuelt satte ankre overstyrer fortsatt beregningen. Null, negative og store forskyvninger
med representerbart resultat beholdes, også datoene på de eksakte yttergrensene.
IGenereringstjeneste rapporterer feilene og lager bare forslag for den gyldige delmengden.
Én SaveChangesAsync erstatter årets tidligere åpne genererte forslag atomisk; en
regenerering gir ikke dubletter. Er ingen regler beregnbare, blir erstatningen tom.
Dette viderefører eksisterende erstatt-alt-semantikk. En lagringsfeil ruller derimot
tilbake hele erstatningen; neste kall bruker en ny operasjonskontekst etter ADR-0006.

[Oppgave #34](https://github.com/depmartin/Budsjettkalender/issues/34) dekker beregningsgrensen
og IGenereringstjeneste med SQLite, samt bevaring av forskyvninger ved IMaltjeneste og
IMalDatautveksling. Andre datooperasjoner i feed, lister og internoppgaver har egne kontrakter.
