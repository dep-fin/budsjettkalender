# Arbeid i Budsjettkalender

Arbeidsmåten bygger på [dep-fin/Matt-Pocock](https://github.com/dep-fin/Matt-Pocock),
undersøkt ved commit `49dd158d1076134a641b33efb035946536778336`.
Ferdighetene er små verktøy som kombineres etter oppgaven.

## Fra behov til endring

1. **Avklar.** Beskriv bruker, konkret utløsende handling, forventet resultat og avgrensning.
   Bruk `grill-with-docs` når produktvalg er åpne. Et kjent avvik med tydelig forventet resultat
   trenger ingen ny intervjurunde.
2. **Spesifiser.** Bruk `to-spec` til å samle avklarte krav i en GitHub-oppgave.
   Navngi berørte moduler og offentlige testgrenser. Bruk begrepene i `GLOSSARY.md`;
   registrer varige avveininger som korte ADR-er.
3. **Del opp.** Bruk `to-tickets` for større arbeid. Hver oppgave skal gi observerbar atferd
   gjennom de nødvendige lagene, med akseptansekriterier og eksplisitte blokkeringer.
4. **Implementer.** Lag en gren fra oppdatert main. Reproduser feilen med en test gjennom
   avtalt grense. Se testen feile av riktig grunn; gjør minste endring som gjør den grønn.
   Gjenta én atferd om gangen. Bruk `diagnosing-bugs` når årsaken er uklar.
5. **Gjennomgå.** Bruk `code-review` mot oppgitt base-commit. Vurder standarder og bestilling
   separat. Refaktorer der gjennomgangen viser en konkret gevinst; en modul skal skjule
   kompleksitet bak et lite grensesnitt.
6. **Lever.** Kjør bygg og hele testsuiten etter siste kodeendring. PR-en forklarer problem,
   atferd før/etter, testbevis og begrensninger. Bruk `pr` ved utforming av PR-beskrivelsen.

## Ferdigkriterium

- Akseptansekriteriene er demonstrert gjennom offentlige grenser.
- Feilrettinger har en regresjonstest som faktisk fanget feilen.
- Tilgang/synlighet er verifisert på returnerte data når dette påvirkes.
- Bygg og tester er grønne, og eventuelle utestående funn er oppgitt.
- Standard- og spesifikasjonsgjennomgang er dokumentert.
- Dokumentasjon, kontrakter og pakkelåser samsvarer med koden.

Se [utviklingsoppsett](docs/development.md) for kommandoer og
[ferdighetsoppsett](docs/agents/skills.md) for installasjon.
