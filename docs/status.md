# Gjeldende status

Oppdatert 20. oktober 2026. [PR #25](https://github.com/depmartin/Budsjettkalender/pull/25)
og [PR #31](https://github.com/depmartin/Budsjettkalender/pull/31) er merget i main.
Utgangspunktet for #23 er `9682de20ece1f1fb92d6b0c3fe405fe76f5f225a`.
Den separate sikkerhetsgjennomgangen av `404ff93` rapporterte ingen nye funn.
[Gjennomgangen fra 20. oktober](reviews/2026-10-20.md) er det daterte utgangspunktet.

## Oppgave #22: DbContext i Blazor-kretser

Levert i PR #31: alle 15 runtime-adaptere bruker IDbContextFactory og én
await using-kontekst per offentlig operasjon. Oppstart og testoppsett bruker egne
kontekster. Beslutningen og grensene står i [ADR-0006](adr/0006-operasjonsavgrenset-dbcontext.md).

De to nye regresjonstestene ved IFristlesing og IFristskriving feilet først med EF Cores
feil om en andre operasjon på samme kontekst. Etter retting fullfører andre kall på samme
tjeneste i samme scope mens første SELECT holdes tilbake. Fabrikken lager separate
kontekster og SQLite-tilkoblinger. Første lesesnitt ble validert før resten av migreringen.
Importens triggerstyrte lagringsfeil, rollback og ny import på samme tjeneste beholdes.
Låst restore og Release-bygg med SDK 10.0.402 samt hele testsuiten (234 tester) passerer.
Bygget har fortsatt den eksisterende CS0649-advarselen i Runde.razor.

## Oppgave #23: samtidig behandling av samme forslag

Implementert: Forslag har et lagringsinternt Guid-versjonstoken på både SQLite og SQL
Server. AppDbContext fornyer det ved alle sporede innsettinger og endringer, også når et
avvist forslag revideres og sendes inn på nytt. Bare én administrator kan fullføre en
behandling av den versjonen som ble lest. Gamle GodkjennAsync, JusterOgGodkjennAsync og
AvvisAsync får Valideringsfeil om at forslaget allerede er behandlet eller endret.
Forslag, ny eller endret frist, synlighet og varsel beholder én samlet lagring og rollback
ved feil. POL krever fortsatt aktivt valg; endringsforslag beholder fristens eksisterende
synlighet. Se [ADR-0007](adr/0007-konkurransekontroll-ved-godkjenning.md).

Den første regresjonstesten feilet fordi begge godkjenninger lyktes. Det senere
sikkerhetsfunnet ble også reprodusert: en gammel godkjenning lyktes etter at samme rad var
avvist og revidert til et nytt forslag. Regresjonen feilet med manglende forventet
Valideringsfeil før versjonstokenet ble innført og passerer etter retting. Den delte
kontrakten har nå 21 tilfeller: de tidligere 15 og seks kombinasjoner av gammel godkjenning,
justering eller avvisning for nye frister og endringsforslag etter revidert innsending.
Testene bruker offentlige tjenester, egne kontekster og tilkoblinger og deterministiske
porter. De viser også at revidert innhold kan godkjennes gjennom samme tjeneste etter
konflikten. En databasetrigger viser rollback etter at forslag, frist og varsel er skrevet.

Bare konkurransefeil med oppføringer utelukkende for Forslag oversettes til Valideringsfeil.
Andre og blandede konflikter beholdes som databasefeil. De to tilfellene som sletter
målfristen med erstatt-alt-import før publisering passerer fortsatt; forslaget forblir
åpent med opprinnelig innhold, uten varsel fra den mislykkede publiseringen.

Den nye migrasjonen er EF-generert og legger til Versjon fra eksisterende produksjonsskjema.
Snapshot og målmodell samsvarer med modellen. SQL Server-prosjektet har i tillegg en test
som oppgraderer et eksisterende forslag og bekrefter at det kan behandles. Det gir 22
SQL-tilfeller. Låst restore, Release-bygg og alle 255 ordinære tester passerer med SDK
10.0.402 (0 feilet, 0 hoppet over), inkludert de 21 SQLite-kontrakttilfellene. SQL-prosjektets
låste restore og Release-bygg passerer. EF finner ingen utestående modellendringer, og
generering av idempotent oppgraderingsskript fra produksjonsskjemaet passerer.

SQL-jobben i CI starter en disponibel Linux-instans og krever utførte tester uten hopp;
deploy venter også på denne jobben. Denne arbeidsstasjonen har ingen brukbar SQL Server,
så en vellykket SQL-kjøring må bekreftes i CI. Lokal SQLite-verifikasjon erstatter ikke
det kravet. Oppsett og kommandoer står i [utviklingsveiledningen](development.md).

## Gjenstående arbeid

- [#24](https://github.com/depmartin/Budsjettkalender/issues/24): felles validering av regelkontrakter.
- Produksjonsgrenser fra gjennomgangen består: Azure SQL/Entra, feed-/M365-policy og
  tokenvern, SQL-brannmur, migrering og Data Protection krever eget driftsoppsett og miljøtest.
  Live kildehenting er fortsatt uferdig.
- Eldre PR #8 inngår ikke i dette arbeidet og er ikke integrert eller merget her.
