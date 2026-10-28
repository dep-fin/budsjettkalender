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
