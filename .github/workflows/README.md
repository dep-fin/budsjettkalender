# .github/workflows

- `ci.yml` — bygger og kjører ordinære tester og en egen obligatorisk SQL Server-jobb på
  hver PR og push til main. SQL-jobben starter en disponibel container med tilfeldig,
  maskert testpassord, og kontrollerer TRX for utførte tester uten hopp. Den trenger ingen
  lagrede secrets. [SQL-testoppsettet](../../docs/development.md) beskriver kontrakten.
- `deploy.yml` — manuell (workflow_dispatch): Bicep what-if, og deploy + app-publisering når
  input `deploy` er true. Bruker OIDC-innlogging mot Azure. Gjenbruker hele `ci.yml` som
  `checks`; både ordinær suite og SQL-jobb må være grønne før infrastruktur eller app rulles ut.

## Nødvendige secrets/variabler for `deploy.yml`

Secrets: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`,
`SQL_AD_ADMIN_LOGIN`, `SQL_AD_ADMIN_OBJECT_ID`.

Variabler: `AZURE_RESOURCE_GROUP`, `NAVN_PREFIKS`, `MILJO`.

Innlogging bruker OIDC (ingen lagrede passord) — sett opp en federated credential på
app-registreringen mot dette repoet.
