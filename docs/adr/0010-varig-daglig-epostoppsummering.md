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
