# Varige hendelser og daglig e-postoppsummering

Direkte manuell oppretting lagrer frist, synlighet og ny frist-hendelse i samme
SaveChanges. Hendelsen refererer til fristidentiteten uten slettingskaskade;
eksisterende data tilbakefylles ikke. Oppsummeringen knytter behandlede hendelses-IDer
uttrykkelig til mottakerens plan, fordi et tids- eller ID-høyvannsmerke kan miste en
transaksjon som committer sent.

Aktivering og vellykket publisering ordnes av én seedet koordinasjonsrad i databasen.
Adressebekreftelse og direkte manuell oppretting aktiverer uttrykkelig kontekstens
siste lagringsinterceptor; andre lagringer bruker den ikke. Interceptoren oppdaterer
koordinasjonsraden og holder skrivelåsen gjennom samme transaksjon som abonnementets
aktiveringsgrense eller fristen med synlighet og hendelse. Nummeret lagres som
publiseringssekvens eller egen aktiveringsrad. Rollback omfatter også koordineringen.
En senere vellykket transaksjon kan dermed ikke passere en tidligere nummerert
transaksjon på verken SQLite eller SQL Server, heller ikke mellom prosesser.
Forespørselens UTC-ticks beholdes som metadata; de avgjør ikke utvalget.
Dette serialiserer de to korte skriveoperasjonene; tilgangsoppslag og posttransport
kjøres utenfor koordineringstransaksjonen.

Abonnentens aktiveringssekvens er en eksklusiv nedre grense. Like klokkeslett og
forespørsler startet før, men fullført etter aktivering, behandles etter databasens
rekkefølge. Ny aktivering erstatter grensen. Eksisterende EV02-abonnenter har grense
0 og får fremtidige hendelser etter migrasjonen uten historisk tilbakefylling.

Unik bruker/lokal dato og et portabelt versjonstoken avgjør hvilken jobb som får
overlevere innhold. Bare den faktisk innleste Planlagt-statusen er sendbar, og
UPDATE krever både forventet status og versjon. UnderSending/Uavklart avvises også
når tilstanden endres mellom mottakerens første statusoppslag og planlesing.
Vinneren lagrer UnderSending før adapterkallet; avbrudd i denne
tilstanden og uavklart adapterutfall blokkerer automatisk gjentakelse for mottakeren.
Dette kan kreve manuell avklaring også når ingenting faktisk ble sendt, men unngår
blind gjentakelse etter mulig overlevering. EV11 utvider feilhåndteringen; EV03
inneholder ingen automatisk reparasjon eller garanti om nøyaktig én ekstern levering.

Planen lagrer ingen rendret e-post. Ferskt mottakeroppslag, nåværende godkjent frist
og sentralt synlighetsfilter begrenser innholdet før projeksjon. Abonnement, tilgang
og utvalg revalideres etter varig planlegging rett før overlevering. Produksjon har
utilgjengelige identitets- og postadaptere inntil egne miljøporter er levert.

Etter levert UX16 er migrasjonsrekkefølgen Epostabonnement → UnikeInternrunder →
DagligEpostoppsummering → OrdnetEpostaktivering. Begge EV03-målmodellene er generert
på nytt med UX16s unike runder og samtidighetspredikater. Den første beskriver
oppsummeringsskjemaet før sekvens/aktivering; den siste samsvarer med hele snapshotet.
Alle tre migrasjonenes faktiske Up/Down beholdes uendret. Ingen advarsler undertrykkes.
Modellregresjoner sammenlikner hver overgangs skjemaoperasjoner med Up/Down og
kontrollerer samtidig at allerede levert internvern og EV02-abonnementer er bevart.
Oppgradering av en levert UX16-/EV02-database oppretter ingen historiske hendelser.

## EV04: varseltyper og direkte endringer

EV04 viderefører den samme hendelsestabellen og eksisterende hendelses-ID-er og
plankoblinger. Type Ny er standard for tidligere rader. Unik FristId gjelder fortsatt
førstegangspublisering, med filter for Ny, mens Endret tillater flere hendelser per
frist. Før-/ettermetadata normaliserer tentative datoer til år/måned/kvalifikator uten
en betydningsbærende referansedag eller klokkeslett. Endret dag, tid, presisjon,
måned, kvalifikator, budsjettår og kategori er automatisk vesentlig. Andre faktiske
innholdsendringer kan markeres med en forklaring på 1–300 tegn. Korrektur gir ingen
automatisk hendelse. Nåværende publisert innhold inngår som samtidighetspredikater
ved fristoppdatering, slik at gammel redigering ikke lagrer feil førtilstand.

Nye frister og Vesentlige endringer har hver sin eksklusive nedre sekvensgrense i
aktiveringsraden. Bekreftelse etter ny påmelding erstatter begge grenser. Å slå på én
type erstatter bare denne typens grense, i samme databaseordnede transaksjon som
valget. Ventende hendelser for den andre typen bevares. Avslåtte typer filtreres
ved kjøring, og abonnementets versjon kontrolleres igjen før overlevering. Dette
unngår etterslep fra avslått periode også ved identiske eller forskjøvede UTC-ticks.

Oppsummeringen grupperer relevante hendelser per frist. Relevant ny publisering
vises én gang med gjeldende tilstand, også når fristen siden er endret. Bare
endringsvalget viser i stedet første relevante førverdi, gjeldende etterverdi og
eventuelle begrunnelser. Synlighet på dagens godkjente frist og fersk mottakertilgang
begrenser både innhold og historiske opplysninger; historikken er ingen tilgangsrett.
Eksisterende sistekontroller, dagsvern og håndtering av uavklart overlevering består.

Migrasjonen VarseltyperOgEndringer er additiv. Den oppretter ingen historiske
hendelser eller abonnementer og bevarer eksisterende planer og koblinger. Normal
demooppstart oppgraderer både EV02 og EV03 atomisk og idempotent. SQLite bruker
TEXT for SQL Servers ubundne tekstkolonner. Historiske migrasjonsmål beskriver
fortsatt sine faktiske skjemaoverganger; bare siste mål skal samsvare med dagens
snapshot. EV04 omfatter direkte administratorredigering; kø/generering, import og
sletting har egne senere leveranser.

## EV05: eget abonnementsutvalg og historiske aktiveringsgrenser

Budsjettår og kategorier lagres i abonnementet, uavhengig av kalenderens
visningsfilter. Offentlig null eller tom samling betyr Alle. Konkrete verdier
normaliseres til sortert, unik liste; år må være 1–9999 og kategorier må finnes i
domene-enumen. Flere verdier innen en dimensjon er union, mens dimensjonene
kombineres med snitt. Alle år er et wildcard som omfatter senere år uten
materialisering av dagens tilgjengelige år.

Én aktiveringsgrense for hele filteret ville enten mistet gamle ventende treff
eller inkludert historikk fra et nytilvalgt område. Abonnementets private JSON
bevarer derfor aktive utvalgsklausuler med år, kategorier og eksklusiv
publiseringssekvens. Ved endring klippes hver gammel klausul til snittet med det
nye utvalget, med bevart sekvens. Hele det nye utvalget legges til med fersk
databaseordnet sekvens. Tomme interne snitt fjernes; de betyr aldri Alle.
Klausuler som allerede dekkes av en tidligere klausul, fjernes. Innsnevring og
senere gjenutvidelse kan dermed ikke hente tilbake hendelser fra avslått område.

Utvalg, klausuler, abonnementets versjon og eventuelle typegrenser lagres i samme
EpostrekkefolgeLagring-transaksjon som EV03/EV04. Ny bekreftet påmelding erstatter
alle utvalgsgrenser. Legacy-rader med null JSON har implisitt Alle/Alle med grense
0, i tillegg til de eksisterende typegrensene. UTC-tid er fortsatt bare metadata.

En hendelse må passere både typegrensen og minst én utvalgsklausul. Endret matcher
hele førtilstanden eller hele ettertilstanden: år fra før kan ikke kombineres med
kategori fra etter. Nåværende godkjent frist og fersk mottakertilgang bestemmer
fortsatt hvilket innhold som kan brukes. Utvalg filtrerer hendelsene før gruppering,
førverdi, forklaringer og plankoblinger; dagens års-/kategoriutvalg brukes ikke som
fristfilter som ville mistet utflyttingsendringer. Ny fra EV03 uten ettermetadata
bruker eksplisitt dagens allerede autoriserte frist som fallback, uten konstruert
førhistorikk. Begge sistekontroller av abonnementets versjon og fristinnhold består.

Migrasjonen Abonnementsutvalg legger bare til én nullable tekstkolonne. Demo
oppgraderer EV02, EV03 og EV04 atomisk og idempotent. Historiske testkontekster
beholder sin opprinnelige modell; ny migrasjon endrer ikke eldre målmodeller.

## EV06: godkjenning i kø og av genererte forslag

GodkjennAsync og JusterOgGodkjennAsync utvider den eksisterende felles
publiseringsoperasjonen med Ny eller Endret og samme koordinering som direkte
administratorpublisering. Førtilstanden leses fra målfristen i operasjonens
kontekst før overskriving; Fristtilstand normaliserer dato-/periodebetydningen.
Den felles hendelsesbyggeren for kø og direkte redigering validerer faktisk
innholdsendring og begrunnelse på 1–300 tegn ved manuell markering. Den kan ikke
undertrykke automatisk vesentlighet. Rene korrekturer og identisk innhold gir
ingen hendelse. Godkjenners markering tilhører publiseringshandlingen og lagres
ikke på bidragsyterens forslag.

Forslag har fortsatt ingen klokkeslettkolonne. Direkte godkjenning av et
endringsforslag bevarer målfristens klokkeslett ved dagspresisjon. Justerings-
skjemaet prefyller denne tiden og kan endre eller tømme den; månedspresisjon
fjerner meningsløst klokkeslett. Nye forslag er heldags ved direkte godkjenning,
mens justering kan angi klokkeslett. Det kreves ingen migrasjon eller ny
genereringsmodell.

Forslagets status/justerte felter, ny eller endret frist, synlighet,
innboksmelding, hendelse og publiseringssekvens fullføres i samme transaksjon.
Endringsforslag bevarer synlighet, opphav, kilde og dokumentkobling. Robotbevis
blir på det bevarte forslaget. Innsending, generering/regenerering og avvisning
oppretter ingen fristvarsling. Innsenders ordinære innboks fungerer også når en
godkjent korrektur ikke gir e-posthendelse.

Konkurranseoversettelsen forblir avgrenset til konflikter på Forslag. Slettet
mål eller samtidig redigering av målinnhold beholder databasefeilen; hele
godkjenningen rulles tilbake uten feil førmetadata eller ekstra hendelse.
EV05s hele før-eller-etter-utvalg, filtrering før gruppering og dagens synlighet
med fersk mottakertilgang brukes uendret. Begge køinnganger prøves gjennom
samme delte SQLite-/SQL Server-kontrakter og reell Kestrel-nettlesergrense.

## EV07: logisk importdiff og historisk synlighet ved bortfall

Importens tekniske slett/innsett beholdes for kompatibilitet, men publisert
førtilstand med synlighet leses inne i samme transaksjon og sammenliknes per
Frist.Id med godkjent ettertilstand. Ny er første publisering av identiteten;
Endret bruker felles semantikk uten manuell tekstklassifisering fra en fristfil.
Faktisk bortfall fra godkjent utvalg er Slettet. Hendelser, historiske grupper,
begge saves og publiseringssekvens deler importtransaksjonen. En tildelt ID for
en tom import-ID brukes både av den nye fristen og hendelsen. Forhåndsvisning
tildeler ingen ID. V1-kontrakten og den fullstendige fristeksporten beholdes.

Slettet lagrer bare FoerJson med siste publiserte tilstand, ingen EtterJson.
Historiske gruppekoder ligger relasjonelt med hendelses-ID som fremmednøkkel,
uten FK til dagens frist eller grupperegister. Dermed kan SQL filtrere historisk
tilgang før metadata materialiseres, også etter at grupper fjernes. Mottakerens
ferske egen rolle/grupper kombineres med denne siste synligheten ved begge
innholdskontroller. Tom historisk synlighet gir bare administrator innsyn,
etter den samme SerAlt-regelen som ordinære frister. Ny/Endret krever fortsatt
dagens godkjente, synlige frist; manglende rad alene gir ingen historisk fallback.

Slettet bruker Endringssekvens og Vesentlige endringer. År/kategori matcher hele
siste publiserte tilstand. Endret matcher fortsatt hele før eller hele etter.
Filtrering skjer før gruppering, begrunnelser og plankoblinger. En sletting tar
derfor ikke med tidligere Endret-metadata som ville manglet dagens lesetilgang.
Bare siste livsløpshendelse kan beskrive et bortfall, og aldri når identiteten
er godkjent igjen. Begge sistekontroller verner også mot nytt bortfall eller
gjeninnføring under planlagringen; sene hendelser blir igjen til neste forsøk.

En egen Livssyklussekvens settes ved full adresseaktivering, uavhengig av
Nye/Endret-valgene. Typevalg og filterutvidelser endrer ikke denne grensen.
Ny etter denne grensen og etterfølgende bortfall utelates hvis mottakeren aldri
har fått en bekreftet oppsummering av identiteten. Livssyklusoppslaget bruker
hendelsesidentiteter og bekreftede plankoblinger uten å gjengi skjult historikk.
Ny publisert før aktivering regnes som etablert; tidligere bekreftet Ny eller
Endret lar en senere sletting varsles. Bare av/på-status for Nye eller fravær
av en plan ville gitt feil resultat for abonnement på bare endringer.

Oppgradering fra EV05 oppretter ingen hendelser eller historiske grupper og
bevarer type-/utvalgsgrenser og planer. Eksisterende frister er installasjonens
utgangspunkt: eksisterende aktiveringsrader får dagens koordinasjonssekvens som
livssyklusgrense. Dette rekonstruerer ingen ukjent opprinnelig aktiveringsrekkefølge
for eldre typevalg. Nye aktiveringer får en eksakt databaseordnet grense. Demoskjema
oppgraderer EV02–EV05 atomisk og idempotent; historiske testmodeller beholder sitt
autentiske skjema. Migrasjonens Designer og snapshot beskriver den nye modellen.

Den livsvarige filtrerte Ny-indeksen beholdes. En eksplisitt gjeninnført identitet
sammenliknes med siste bevarte publiserte tilstand og kan gi Endret, uten en ekstra
Ny, ny identitet eller sletting av historikk. Dette virker også for eldre frister
uten Ny, fordi det atomiske bortfallet har bevart siste publiserte tilstand.

Slettingslenken er vanlig /kalender med validerbare år-, kategori- og datoparametre
og en generell fjerningsmarkør. Ingen tittel, notat, identitet, adresse, JSON eller
hemmelighet inngår i lenken. Siden krever vanlig innlogging og serverfiltrering,
viser generell melding og beholder valgt år selv om årets siste frist er fjernet.
Dette innfører ingen avlysningsmodul eller ny lese-/delingsrettighet.

## EV08: adressebytte med ny full aktivering

Adressebytte er en egen versjonskontrollert operasjon på brukerens eksisterende
abonnement. Den vanlige påmeldingen avviser fortsatt en annen registrert adresse.
Web binder eieren fra autentisert forespørsel eller krets; operasjonen tar bare
ny adresse, uttrykkelig egenadressesamtykke og sist leste versjon. Samme
normaliserte adresse er uendret, også uten transport. Avmeldt bruker kan velge
adressebytte og ny påmelding uttrykkelig. En eldre forespørsel med versjonen fra
før avmelding kan aldri gjenopplive abonnementet.

Ny adresse og VenterPaaBekreftelse lagres før bekreftelsestransporten får noe.
Aktivert tømmes; tidligere engangslenke erstattes med nytt hash og 15 minutters
utløp. Opprettelsestid, varseltyper, eget år-/kategoriutvalg og eksisterende
aktiveringsgrenser beholdes. Ett minutts pause og høyst fem bekreftelsesmeldinger
per time deles med påmelding og nyutsending, også over flere adressebytter og
sendefeil. Transportfeil gjør aldri gammel adresse aktiv igjen. Forsinket
transportretur bevarer en samtidig vinnende status.

Ny bekreftelse bruker den eksisterende ordnede aktiveringen og erstatter begge
typegrenser, livssyklusgrensen og alle utvalgsklausuler i samme transaksjon som
Aktiv. Lagrede valg beholdes. Hendelser fra før eller under venting gir dermed
ingen tilbakefylling. Databasens commitrekkefølge avgjør fortsatt grensen ved
like eller bakovergående UTC-tider.

Senderen krever Aktiv og Aktivert og kontrollerer adresse/versjon igjen før
innholdsoverlevering. Adressebytte stanser kølagt, ennå ikke overlevert innhold
til begge adresser. Det rydder ingen UnderSending/Uavklart-planer og tillater
ingen blind gjentakelse av mulig allerede overlevert post. Ingen adressehistorikk,
parallelle abonnementer, modellendring eller ny migrasjon innføres.
