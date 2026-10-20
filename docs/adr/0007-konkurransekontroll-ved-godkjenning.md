# Bare én behandling av et åpent forslag

To administratorer kan lese samme åpne forslag i hver sin operasjonskontekst. Et filter
på status ved lesing hindrer derfor ikke at begge oppretter en frist og et varsel.

Status kan gå fra Forslag via Avvist tilbake til Forslag når bidragsyteren reviderer og
sender inn samme rad på nytt. Status alene oppdager derfor ikke at en gammel behandling
gjelder en tidligere innsending. En slik behandling kan ellers publisere gammelt innhold
og samtidig markere den reviderte raden som godkjent.

Forslag har et lagringsinternt Guid-felt, Versjon, som EF Core-konkurransetoken.
AppDbContext fornyer feltet ved alle sporede innsettinger og endringer, i både synkron og
asynkron SaveChanges. Godkjenning, justering, avvisning og ny innsending bruker dermed
samme mekanisme. Oppdatering og sletting krever at lagret versjon fortsatt er den som ble
lest. En gammel operasjon kan ikke behandle en senere innsending selv om status igjen er
Forslag. Feltet er en EF-skyggeegenskap: domenemodellen og Application-kontraktene får
ingen nye medlemmer, og tjenestene trenger ikke å forvalte tokenet hver for seg.
Dette gjelder på både SQLite og SQL Server uten leverandørspesifikk rowversion.

EF-generert migrasjon ForslagVersjonKonkurransekontroll legger til den obligatoriske
kolonnen fra produksjonsskjemaet etter Internkalender. Eksisterende rader får Guid.Empty;
tokenet sammenlignes sammen med radens id, så det trenger ikke være unikt mellom rader.
Første endring fornyer også disse tokenene. Nye SQLite-databaser bruker EnsureCreated.
Snapshot og migrasjonens målmodell er EF-generert fra samme modell.

GodkjennAsync og JusterOgGodkjennAsync deler fortsatt én publiseringshjelper. Forslagets
status og eventuelt justerte felter, ny eller endret frist, synlighet og varsel lagres
i én SaveChangesAsync. Leverandørens transaksjon ruller tilbake hele lagringen ved feil,
også skriv som er utført før konkurransekonflikten oppdages. Avvisning bruker samme
lagringshjelper og bevarer status/varsel som én operasjon. DbUpdateConcurrencyException
oversettes til Valideringsfeil med beskjed om at forslaget allerede er behandlet bare når
feilen har konfliktoppføringer og alle gjelder Forslag. Konflikter på andre enheter eller
blandede oppføringer beholdes uendret som databasefeil, sammen med øvrige lagringsfeil.
Hvis en import sletter målfristen etter at et endringsforslag er lest, kan publiseringen
feile mens forslaget fortsatt er åpent etter rollback. Det skal derfor ikke meldes som
allerede behandlet. Operasjonskonteksten disponeres etter kallet.

Aktivt POL-valg og endringsforslags eksisterende synlighet følger ADR-0002. Fabrikken
og konteksten per operasjon følger ADR-0006. Dette innfører ikke generell versjonering
av fristinnhold eller koordinering mellom forskjellige forslag til samme frist.

Kontrakttester ved IGodkjenningsko deles mellom den ordinære SQLite-suiten og et separat
SQL Server-prosjekt. To testporter venter ved SaveChangesAsync etter at begge operasjoner
har lest forslaget og klargjort skrivene. Hver operasjon holder egen tilkobling. Første
behandling får fullføre før den andre slippes. En tredje kontekst kontrollerer sluttdata.
Testene dekker begge godkjenningsinnganger, begge rekkefølger av godkjenning/avvisning,
POL og endringsforslag. En databasetrigger fremtvinger en feil etter at forslag, frist og
varsel faktisk er skrevet i transaksjonen, og viser at alt rulles tilbake.
Begge godkjenningsinnganger prøves også med en erstatt-alt-import som sletter målfristen
før lagring. Databasefeilen beholdes, forslaget er fortsatt i køen med opprinnelig innhold,
og innsenderen får ikke et varsel fra den mislykkede publiseringen.
Seks tilfeller holder gammel godkjenning, justering eller avvisning tilbake mens en annen
administrator avviser og bidragsyteren reviderer og sender inn via IForslagsinnsending.
De dekker både ny frist og endringsforslag. Den reviderte innsendingen forblir åpen uten
publisering eller ekstra varsel fra det gamle kallet; et nytt kall på samme tjeneste kan
publisere riktig innhold. SQL Server-prosjektet har i tillegg en oppgraderingstest som
legger inn et forslag i produksjonsskjemaet før den nye migrasjonen og behandler det etterpå.

CI starter en disponibel SQL Server og krever vellykket tilkobling, ekte migrasjoner og
utførte tester uten hopp. Manglende SQL-tilkobling er feil, aldri en hoppet test. Prosjektet
ligger utenfor den ordinære løsningen for å bevare lokale tester uten SQL Server/Docker.
Deploy gjenbruker hele CI-arbeidsflyten og venter dermed også på SQL-jobben.
