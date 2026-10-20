# Backend og web

Løsningen i `Aarshjul.slnx` inneholder Domain, Application, Infrastructure, Web,
Tests og Kilder. Blazor-grensesnittet ligger i `Aarshjul.Web/Components`.
`Aarshjul.SqlServerTests` ligger utenfor løsningen og kjøres eksplisitt mot en disponibel
SQL Server. Kontrakttestene deles med SQLite via `Testkontrakter`. Se utviklingsoppsettet.
`Aarshjul.BrowserTests` ligger også utenfor løsningen. Det kjører reelle nettleserflyter
mot en ny demo/database per scenario; testmiljøet trenger Playwright Chromium eller lokal Edge.

Se [arkitektur](../docs/architecture.md), [utvikling](../docs/development.md) og
[arbeidsflyt](../CONTRIBUTING.md). Tilgangskontroll håndheves på server.
