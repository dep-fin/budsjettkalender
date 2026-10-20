# Utvikling

Krever SDK-versjonen i `global.json` (10.0.402). Kjør fra repo-roten.
SDK-en er eksakt låst fordi Blazor legger til en SDK-avhengig asset-pakke i avhengighetsgrafen.
CI, deploy og devcontainer bruker samme versjon. Ved SDK-oppdatering endres også
devcontainerens versjon, og pakkelåsene regenereres og verifiseres.

```sh
dotnet restore backend/Aarshjul.slnx --locked-mode
dotnet build backend/Aarshjul.slnx -c Release --no-restore
dotnet test backend/Aarshjul.slnx -c Release --no-build
# Målrettet test under utvikling (bygger også testprosjektet)
dotnet test backend/Aarshjul.slnx --filter FullyQualifiedName~FristDatautvekslingTester
dotnet run --project backend/Aarshjul.Web --launch-profile demo
```

Demo: åpne `http://localhost:5014/demo`. Profilen bruker Development + Demo=true og
SQLite-filen `aarshjul-demo.db`. Bare Development eller eksplisitt Demo-miljø kan bruke
demoinnlogging. Miljøet Demo krever publiserte assets; bruk launch-profilen under utvikling.
Ved modellendring må demo-databasen flyttes til en sikkerhetskopi eller gjenskapes;
`EnsureCreated` oppgraderer ikke skjemaet.

## Obligatorisk SQL Server-kontroll i CI

`backend/Aarshjul.SqlServerTests` kjører samme 21 kontrakttilfeller ved IGodkjenningsko som
SQLite-suiten: to godkjenninger, godkjenning mot avvisning i begge rekkefølger, justering,
POL, endringsforslags synlighet, rollback ved en reell lagringsfeil og sletting av målfristen
ved import før et endringsforslag lagres. Sistnevnte skal gi databasefeil, beholde forslaget
åpent og unngå varsel fra den mislykkede publiseringen. Testportene holder
to uavhengige kontekster og åpne tilkoblinger før første behandling får fullføre.
Rollback-triggeren kontrollerer at forslag, frist og varsel allerede er skrevet i den
pågående transaksjonen før den kaster feil. SQL Server bruker MaxBatchSize(1) i testene.
Seks tilfeller prøver gammel godkjenning, justering og avvisning etter avvisning og revidert
innsending av samme rad, for både nye frister og endringsforslag. De bekrefter at den nye
innsendingen forblir åpen, uten feil publisering eller ekstra varsel. En separat SQL-test
oppgraderer produksjonsskjemaet etter Internkalender med et eksisterende forslag og
bekrefter at innholdet bevares og kan behandles. SQL-prosjektet har dermed 22 testtilfeller.

Prosjektet ligger bevisst utenfor `backend/Aarshjul.slnx`. De ordinære lokale kommandoene
krever derfor verken SQL Server eller Docker. SQL-testene krever en disponibel testinstans
med rettighet til å opprette og slette databaser. Oppgi tilkobling til `master`, aldri en
applikasjonsdatabase. Hvert tilfelle oppretter et nytt navn `AarshjulGodkjenning_<guid>`,
kjører MigrateAsync og sletter bare databasen det selv har opprettet, også ved testfeil.
Eksisterende databaser blir ikke resatt eller slettet. Ikke bruk produksjonsinstansen.

```sh
export AARSHJUL_TEST_SQLSERVER='Server=localhost,1433;Database=master;User Id=sa;Password=<testpassord>;Encrypt=True;TrustServerCertificate=True'
dotnet restore backend/Aarshjul.SqlServerTests/Aarshjul.SqlServerTests.csproj --locked-mode
dotnet test backend/Aarshjul.SqlServerTests/Aarshjul.SqlServerTests.csproj -c Release --no-restore
```

På PowerShell settes miljøvariabelen med `$env:AARSHJUL_TEST_SQLSERVER = '...'`.
Manglende variabel gir en testfeil med en gang. Testoppsettet venter inntil 60 sekunder
på vellykket SQL-tilkobling og SELECT 1 før det oppretter databasen; utilgjengelig server
gir feil. Ingen tester hopper over SQL-kravet eller erstatter det med SQLite.

CI-jobben `SQL Server – godkjenning og rollback` starter
[SQL Server 2022 CU22 GDR1](https://mcr.microsoft.com/en-us/artifact/mar/mssql/server/tag/2022-CU22-GDR1-ubuntu-22.04)
i en Linux-container med tilfeldig testpassord som maskeres sammen med tilkoblingsstrengen.
Den gjenoppretter låste pakker og krever en TRX-fil med minst én utført test og null hopp.
Containeren fjernes til slutt. `deploy.yml` kaller hele CI-arbeidsflyten; også SQL-jobben
må lykkes før utrulling. Denne kontrollen dekker SQL Server, mens Azure SQL/Entra og
driftsmiljøets nettverk fortsatt må testes separat.

## Avhengigheter

`packages.lock.json` i hvert prosjekt låser hele avhengighetsgrafen.
Ved bevisst pakkeoppdatering, endre referansene og kjør:
```sh
dotnet restore backend/Aarshjul.slnx --force-evaluate
dotnet test backend/Aarshjul.slnx -c Release
```
Commit prosjektfilene og oppdaterte låsefiler sammen. Restore feiler ved kjente
NuGet-sårbarheter; vurder og oppdater pakken fremfor å slå av kontrollen.

## Migrasjoner og produksjon

Installer dotnet-ef i kompatibel 10.x-versjon ved behov.
```sh
dotnet ef migrations add <Navn> --project backend/Aarshjul.Infrastructure --startup-project backend/Aarshjul.Infrastructure
```
Produksjonsappen kjører foreløpig migrasjoner ved oppstart. Egen deploy-migrering,
SQL/Entra-oppsett, Data Protection og Azure-nettverk må verifiseres i et driftsmiljø.
Se `infra/README.md` og gjennomgangens åpne oppgaver før utrulling.
