# Budsjettkalender

Delt årshjul for budsjettfrister i FIN/SBR. Bruk norsk domenespråk i kode og diskusjoner.
Les `GLOSSARY.md` før domeneanalyse, og relevante beslutninger i `docs/adr/`.
Les [gjeldende status](docs/status.md) for leverte endringer og åpne oppgaver.

## Bærende regler

- Identifiser en frist på funksjon/løp; rundskrivnummer varierer mellom år.
- Robotuttrekk og generering lager forslag. Publisering krever administratorgodkjenning.
- Synlighet filtreres på server før data sendes til klienten. POL krever et aktivt valg.
- Endringsforslag endrer innhold; fristens synlighet beholdes.
- Internkalender er SBR-intern; dagens `ErSbr`-policy tilsvarer administrator.
- Kalenderfeed bruker et hemmelig token som tilgang; vurder ADR-0003 før endringer i deling.

## Arbeidsflyt

Følg `CONTRIBUTING.md`: avgrens ønsket atferd og testgrense, bygg én vertikal endring
om gangen med rød → grønn test, og gjennomgå endringen mot både standarder og bestilling.
Avklar uavklarte produktvalg; en avgrenset, allerede bestilt feilretting kan utføres direkte.
Se `CODING_STANDARDS.md` ved kodeendringer og `docs/architecture.md` ved modulendringer.

Ved tekniske valg, nye svar fra produkteier/systemansvarlig og relevante leveranser:
les og vedlikehold [avklaringsregisteret #151](https://github.com/depmartin/Budsjettkalender/issues/151)
etter `docs/agents/issue-tracker.md`.

## Agent skills

### Issue tracker
GitHub Issues i `depmartin/Budsjettkalender`. Les `docs/agents/issue-tracker.md` før oppgaveoperasjoner.

### Triage labels
Fem standardroller er definert i `docs/agents/triage-labels.md`.

### Domain docs
Én `GLOSSARY.md` og `docs/adr/`. Lesereglene står i `docs/agents/domain.md`.

### Metode og ferdigheter
Bruk Matt Pococks små, komponerbare ferdigheter som beskrevet i `docs/agents/skills.md`.
Testene i `backend/Aarshjul.Tests` er eksisterende testgrenser; bruk dem ved feilretting.
Nye grenser avklares i oppgaven før implementering.

## Verifikasjon

Kjør fra repo-roten:
```sh
dotnet restore backend/Aarshjul.slnx --locked-mode
dotnet build backend/Aarshjul.slnx -c Release --no-restore
dotnet test backend/Aarshjul.slnx -c Release --no-build
```

For demo, migrasjoner og oppdatering av pakkelåser: `docs/development.md`.
For åpne funn og verifikasjonsgrenser: `docs/reviews/2026-10-20.md`.
Historikken i `docs/archive/` leses bare når en eldre beslutning må spores.
