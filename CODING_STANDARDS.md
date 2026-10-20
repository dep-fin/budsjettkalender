# Kodestandarder

## Modulgrenser

- Domain eier rene domeneberegninger og modeller.
- Application eier tjenestegrenser og kontrakter.
- Infrastructure skjuler EF Core, dokumentbehandling og andre adaptere bak disse grensene.
- Web eier HTTP/Blazor, autentisering og autorisasjonspolicyer.
- Kildeintegrasjoner bruker `IKilde`; nye kilder skal ikke endre godkjenningsflyten.

Et nytt grensesnitt skal skjule reell kompleksitet. Foretrekk samlokalisering av logikk
som endres sammen; vurder abstraksjoner ut fra eksisterende behov.

## Atferd og data

- Bruk domenebegrepene i `GLOSSARY.md`.
- Bruk `TimeProvider` og `Datohjelp.Idag` for den norske kalenderdagen.
- Send `CancellationToken` videre i asynkrone I/O-operasjoner.
- Valider importkontrakter før sletting. Erstatt-alt-operasjoner skal være atomiske.
- Filtrer synlighet i databasespørringen før projeksjon og retur.
- Bevar godkjenningskrav, eksplisitt POL-valg og skillet mellom frister og gjøremål.

## Tester og gjennomgang

Test observerbar atferd ved Application-grenser og HTTP. Bruk SQLite for relasjonell
oppførsel i lokale tester; Azure SQL/Entra krever egne miljøtester.
Forventninger skal være uavhengige av implementasjonen.
Gjennomgangen vurderer standarder og oppgavens akseptansekriterier i to separate spor.
