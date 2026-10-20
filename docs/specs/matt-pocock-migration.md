# Omlegging til Matt Pocock-arbeidsflyt

Bestilt av repoets eier 2026-10-20: analyser repoet, finn og utbedre feil/forbedringer, og
tilpass hele repoets arbeidsmåte til https://github.com/dep-fin/Matt-Pocock.

## Avgrensning og kriterier

- Felles agentindeks, begrepsliste, varige beslutninger og oppgave-/triagekonfigurasjon.
- Kort, konkret bidragsveiledning med avklaring → spec → vertikale oppgaver →
  rød/grønn test → separat standard-/spec-gjennomgang → PR.
- Eksisterende funksjonelle krav og .NET-plattform videreføres.
- Historisk Claude-kontekst arkiveres og gjeldende dokumentasjon peker til riktig kilde.
- Bekreftede importfeil rettes med tester ved IFristDatautveksling/IMalDatautveksling:
  ingen sletting ved avvist format, atomisk fristimport og bevaring av klokkeslett.
- Demoautentisering avvises ved produksjonsoppstart, testet gjennom web-hostens oppstart.
- Bygg/test og pakkekontroll kjører før manuell deploy; avhengigheter låses.
- Gjennomgangen skiller rettede feil, gjenstående risiko og ikke verifisert drift.

Hele løsningen gjennomgås på modul-/arbeidsflytnivå. Dette er ikke en påstand om full
penetrasjonstest eller ende-til-ende-verifikasjon mot Azure SQL, Entra og Outlook.
