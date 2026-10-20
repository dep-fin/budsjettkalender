# Gjeldende status

Oppdatert 20. oktober 2026. [PR #25](https://github.com/depmartin/Budsjettkalender/pull/25)
er merget i main ved `89a117a4439ebef27200a13ccf7977dc653b3699`. Den separate
sikkerhetsgjennomgangen av `404ff93` rapporterte ingen nye funn.
[Gjennomgangen fra 20. oktober](reviews/2026-10-20.md) er det daterte utgangspunktet.

## Oppgave #22: DbContext i Blazor-kretser

Implementert fra `89a117a`: alle 15 runtime-adaptere bruker IDbContextFactory og én
await using-kontekst per offentlig operasjon. Oppstart og testoppsett bruker egne
kontekster. Beslutningen og grensene står i [ADR-0006](adr/0006-operasjonsavgrenset-dbcontext.md).
Endringen venter på samlet standard-/spesifikasjonsgjennomgang og sikkerhetsaudit før
commit, PR og merge.

De to nye regresjonstestene ved IFristlesing og IFristskriving feilet først med EF Cores
feil om en andre operasjon på samme kontekst. Etter retting fullfører andre kall på samme
tjeneste i samme scope mens første SELECT holdes tilbake. Fabrikken lager separate
kontekster og SQLite-tilkoblinger. Første lesesnitt ble validert før resten av migreringen.
Importens triggerstyrte lagringsfeil, rollback og ny import på samme tjeneste beholdes.
Låst restore og Release-bygg med SDK 10.0.402 samt hele testsuiten (234 tester) passerer.
Bygget har fortsatt den eksisterende CS0649-advarselen i Runde.razor.

## Gjenstående arbeid

- [#23](https://github.com/depmartin/Budsjettkalender/issues/23): samtidig godkjenning av samme forslag.
- [#24](https://github.com/depmartin/Budsjettkalender/issues/24): felles validering av regelkontrakter.
- Produksjonsgrenser fra gjennomgangen består: Azure SQL/Entra, feed-/M365-policy og
  tokenvern, SQL-brannmur, migrering og Data Protection krever eget driftsoppsett og miljøtest.
  SQLite-testene bekrefter ikke SQL Server-konkurranse. Live kildehenting er fortsatt uferdig.
- Eldre PR #8 inngår ikke i dette arbeidet og er ikke integrert eller merget her.
