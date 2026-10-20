# Utvikling

Krever .NET 10 SDK som passer `global.json`. Kjør fra repo-roten.

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
