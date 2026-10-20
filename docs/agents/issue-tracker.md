# Issue tracker: GitHub

Oppgaver og spesifikasjoner ligger i GitHub Issues i `depmartin/Budsjettkalender`.
Bruk tilkoblet GitHub-verktøy når tilgjengelig, ellers `gh` inne i klonen.

- Les: `gh issue view <nummer> --json number,title,body,labels,comments`.
- Finn arbeid: `gh issue list --repo depmartin/Budsjettkalender --state open`.
- Opprett: `gh issue create --repo depmartin/Budsjettkalender --title "..." --body-file <fil>`.
- Oppdater: `gh issue edit <nummer> --body-file <fil>`.
- Etiketter: `gh issue edit <nummer> --add-label <etikett>`.
- Lukk: `gh issue close <nummer>` etter at avtalt ferdigkriterium er oppfylt.

Når en ferdighet sier «publish to the issue tracker», opprett en GitHub-oppgave.
Når den sier «fetch the relevant ticket», les hele oppgaven og relevante kommentarer.

Oppgaven inneholder problem, forventet atferd, avgrensning, berørte moduler/testgrenser,
akseptansekriterier og blokkeringer. Bruk native GitHub-avhengigheter når verktøyet støtter
det; ellers skriv `Blocked by: #<nummer>` øverst. Klar betyr at alle blokkeringer er lukket.

**PRs as a request surface: no.** PR-er leverer endringer; de inngår ikke som forespørsler i triage.

Ved manglende GitHub-tilgang beholdes utkast i `docs/specs/`, merket som upublisert.
Oppgi avviket ved levering. Et lokalt utkast er ikke en publisert oppgave.
