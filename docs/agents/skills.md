# Matt Pocock-ferdigheter

Metodekilde: [dep-fin/Matt-Pocock](https://github.com/dep-fin/Matt-Pocock/tree/49dd158d1076134a641b33efb035946536778336).
Oppsettet her er tilpasset repoets .NET-stakk. Det er ingen TypeScript-konvertering.
Prosjektkonfigurasjonen som setup-ferdigheten forventer, ligger allerede i `docs/agents/`.

Ferdighetene leveres av pluginen. Installer den én gang per agentmiljø, og bruk én
installasjonsmåte slik at samme ferdighet ikke lastes dobbelt. Repoet kopierer ikke
ferdigheter som allerede finnes i pluginen.

Metoderepoets README beskriver administrert installasjon fra oppstrøms `mattpocock/skills`:
```sh
# Codex
codex plugin marketplace add mattpocock/skills
codex plugin add mattpocock-skills@mattpocock

# Claude Code
claude plugin install mattpocock-skills@claude-plugins-official
```

For en redigerbar installasjon direkte fra den angitte FIN-forken følger du den alternative
skills.sh-ruten i metoderepoets README, med kilde `dep-fin/Matt-Pocock` og valgt agent.
Denne oppdateres manuelt. Kontroller endringer i fork og oppstrøms før oppdatering.

## Hvilket verktøy passer?

- Uklart produktbehov: `grill-with-docs`.
- Avklart behov til oppgave: `to-spec`; større arbeid: `to-tickets`.
- Avgrenset implementering: `implement` med `tdd`.
- Ukjent feilårsak: `diagnosing-bugs`.
- Vanskelig modulgrense: `codebase-design` / `improve-codebase-architecture`.
- Terminologi og beslutninger: `domain-modeling`.
- Endringskontroll: `code-review`, deretter `pr`.

Installerte versjoner kan ha litt andre navn. Les tilgjengelig ferdighet før bruk.
