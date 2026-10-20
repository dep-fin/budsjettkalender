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
- Runtime-tjenester bruker `IDbContextFactory<AppDbContext>` og `await using` per offentlig
  operasjon. Private hjelpere deler operasjonens kontekst; se [ADR-0006](docs/adr/0006-operasjonsavgrenset-dbcontext.md).
- Forslag bruker et lagringsinternt versjonstoken som AppDbContext fornyer ved alle sporede
  innsettinger og endringer. Ikke omgå dette med bulkoppdateringer; se [ADR-0007](docs/adr/0007-konkurransekontroll-ved-godkjenning.md).
- Valider importkontrakter før sletting. Erstatt-alt-operasjoner skal være atomiske.
- Gjentaksreglenes parameterkontrakt valideres i Regelparser for både manuell lagring og import;
  se [ADR-0008](docs/adr/0008-felles-validering-av-gjentaksregler.md) for feil- og kompatibilitetskrav.
- Filtrer synlighet i databasespørringen før projeksjon og retur.
- Bevar godkjenningskrav, eksplisitt POL-valg og skillet mellom frister og gjøremål.

## Tester og gjennomgang

Test observerbar atferd ved Application-grenser og HTTP. Bruk SQLite for relasjonell
oppførsel i lokale tester. Godkjenningskontrakten kjøres også mot ekte SQL Server i CI;
se [utviklingsoppsettet](docs/development.md). Azure SQL/Entra krever egne miljøtester.
Forventninger skal være uavhengige av implementasjonen.
Gjennomgangen vurderer standarder og oppgavens akseptansekriterier i to separate spor.
