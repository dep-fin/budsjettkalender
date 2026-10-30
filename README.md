# Budsjettkalender — årshjul for budsjettfrister

Delt fristoversikt for Seksjon for statsbudsjett og regnskap (SBR), FIN.
Forslag fra dokumentuttrekk og årsmaler krever administratorgodkjenning.
Synlighet håndheves på server.

ASP.NET Core / .NET 10, Blazor Interactive Server, EF Core og Azure SQL.
Lokal demo bruker SQLite og persona-innlogging.

## Prøv demoen

Krever .NET 10 SDK:
```sh
dotnet run --project backend/Aarshjul.Web --launch-profile demo
```
Åpne `http://localhost:5014/demo` og velg persona.
[GitHub Codespaces](https://codespaces.new/dep-fin/budsjettkalender) bygger og starter
demoen på port 5014 gjennom devcontaineren.

## Utvikling og arbeidsmåte

- [CONTRIBUTING.md](CONTRIBUTING.md): Matt Pocock-arbeidsflyt fra avklaring til PR.
- [AGENTS.md](AGENTS.md): felles instruksjoner; CLAUDE.md importerer disse.
- [Utviklingsoppsett](docs/development.md): bygg, test, demo, pakker og migrasjoner.
- [Arkitektur](docs/architecture.md) og [kodestandarder](CODING_STANDARDS.md).
- [Begreper](GLOSSARY.md), [beslutninger](docs/adr/) og [ferdighetsoppsett](docs/agents/skills.md).
- [Gjennomgang 20. oktober 2026](docs/reviews/2026-10-20.md): funn, rettinger og gjenstående arbeid.

```sh
dotnet restore backend/Aarshjul.slnx --locked-mode
dotnet build backend/Aarshjul.slnx -c Release --no-restore
dotnet test backend/Aarshjul.slnx -c Release --no-build
```

## Funksjonsstatus

Implementert: kalender-/årshjulvisninger, Entra-innlogging, manuell fristforvaltning,
forslag/godkjenningskø, dokumentopplasting med heuristisk uttrekk, årsmaler/generering,
Word/JSON/ICS-eksport, tokenbasert kalenderabonnement og SBRs internkalender.

Gjenstår: live oppdagelse/henting fra regjeringen.no, versjonsmatching og kjørende
bakgrunnsjobb. Historiske miljøsperrer i faseplanene er ikke bekreftet i dagens driftsmiljø.
Azure SQL, ekte Entra og Outlook-abonnement må verifiseres ved utrulling.

## Krav og historikk

[Full kravspesifikasjon](kravdokument-aarshjul-frister_v2.md),
[teknisk detaljmodell](SYSTEMARKITEKTUR.md) og [brukerhistorier](BRUKERHISTORIER.md)
beholdes som produktreferanser. Tidligere faseplaner ligger i roten.
[docs/archive](docs/archive/) inneholder tidligere Claude-kontekst som historikk.

Kode og Blazor-grensesnitt ligger i `backend/`. `frontend/` er en historisk mappepeker,
`infra/` inneholder Bicep og `.github/workflows/` inneholder CI/deploy.
