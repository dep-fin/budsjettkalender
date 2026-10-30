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


## Levende tekniske avklaringer

[Avklaringsregisteret #151](https://github.com/depmartin/Budsjettkalender/issues/151)
er felles sjekkliste for spørsmål til produkteier/systemansvarlig, delt etter når
svaret trengs. Sjekklistens ID-er, status og beslutningshistorikk vedlikeholdes i
GitHub-oppgaven; repoet peker dit fremfor å kopiere listen.

Ved tekniske valg, nye svar og relevante leveranser: hent registerets gjeldende
tekst og kommentarer og følg vedlikeholdsreglene der. Dokumenter svar fra chat i
GitHub, kryss av tilstrekkelig avklarte punkter, behold restspørsmål åpne, legg til
nye nødvendige avklaringer og gjenåpne endrede forutsetninger. Vurder konsekvensene
for fagoppgaver, ADR-er og driftsdokumenter før arbeidet som avhenger av valget
fortsetter. Oppgi eventuell manglende tilgang eller upublisert oppdatering ved levering.

Registeret holdes åpent gjennom prosjektet til produkteier beslutter sluttføring.
Bruk `Refs #151` ved levering av vedlikeholdsregler; opprettelse av registeret eller
en dokumentasjons-PR oppfyller ikke registerets løpende ferdigkriterium.
