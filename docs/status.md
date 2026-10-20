# Gjeldende status

Oppdatert 20. oktober 2026. Denne siden beskriver implementert kode, testbevis og
gjenstående grenser. GitHub-lenkene er kilden for oppgavenes og PR-enes livsløp.
[Gjennomgangen fra 20. oktober](reviews/2026-10-20.md) er et datert utgangspunkt,
ikke en liste over dagens urettede kodefeil.

| Oppgave | Implementert atferd | Leveranse |
| --- | --- | --- |
| [#21](https://github.com/depmartin/Budsjettkalender/issues/21) | Pocock-arbeidsflyt, kort agentindeks, dokumenterte standarder og rettinger fra gjennomgangen | [PR #25](https://github.com/depmartin/Budsjettkalender/pull/25) |
| [#22](https://github.com/depmartin/Budsjettkalender/issues/22) | Én DbContext per offentlig tjenesteoperasjon | [PR #31](https://github.com/depmartin/Budsjettkalender/pull/31) |
| [#23](https://github.com/depmartin/Budsjettkalender/issues/23) | Konkurransekontroll ved behandling av samme forslag, også etter revidert innsending | [PR #32](https://github.com/depmartin/Budsjettkalender/pull/32) |
| [#24](https://github.com/depmartin/Budsjettkalender/issues/24) | Felles parameterkontroll for manuell mallagring og JSON-import | Implementert i denne endringen |

## DbContext og behandling av forslag

Alle 15 runtime-adaptere bruker IDbContextFactory og await using per offentlig operasjon.
Deterministiske regresjonstester ved IFristlesing og IFristskriving viste først EF-feilen
om en andre operasjon på samme kontekst. Etter retting fullfører neste kall på samme
tjeneste mens første SELECT venter. Hvert kall har egen kontekst og SQLite-tilkobling.
Samlede lagringer og rollback beholdes; se [ADR-0006](adr/0006-operasjonsavgrenset-dbcontext.md).

Forslag har et lagringsinternt Guid-versjonstoken som AppDbContext fornyer ved alle
sporede innsettinger og endringer, på både SQLite og SQL Server. Bare én behandling
av lest versjon kan fullføre. Gammel godkjenning, justering eller avvisning gir
Valideringsfeil, også når raden er avvist, revidert og sendt inn på nytt.
Forslag, frist, synlighet og varsel lagres samlet. Bare konkurransefeil med oppføringer
utelukkende for Forslag oversettes; andre databasefeil beholdes.
Se [ADR-0007](adr/0007-konkurransekontroll-ved-godkjenning.md).

De 21 delte kontrakttilfellene ved IGodkjenningsko dekker konkurrerende behandlinger,
POL, endringsforslags synlighet, revidert innsending, slettet målfrist og rollback ved
lagringsfeil. SQL Server-suiten har i tillegg en oppgraderingstest fra produksjonsskjemaet.
EF-generert migrasjon, snapshot og målmodell ble verifisert i #23.

## Regelvalidering og import

Regelparser i Application er den felles kontrollen for regeltype, påkrevde JSON-felter,
ukedag og gyldig måned/dag. IMaltjeneste gir Valideringsfeil før en ugyldig oppretting
eller oppdatering endrer malen. IMalDatautveksling bruker samme kontroll, gir én advarsel
per ugyldig regel og erstatter hele malen med de gyldige reglene. Ingen gyldige regler
betyr derfor en tom mal. Filfeil i versjon, null-liste/-rad eller duplisert ID avviser
fortsatt hele importen uten endring. Alle kandidater kontrolleres før sletting og sporing;
én SaveChangesAsync lagrer erstatningen atomisk. Beslutningen står i
[ADR-0008](adr/0008-felles-validering-av-gjentaksregler.md).

53 ugyldige parameterkombinasjoner prøves både ved manuell oppretting/oppdatering og
import. De omfatter manglende felter, ugyldig JSON, JSON-null, feil felttyper, nullukedag,
umulig måned/dag og ukjent regeltype. Testene bekrefter uendret mal etter manuell avvisning,
advarsel per ugyldig importregel og at gyldige regler overlever blandet import.
De første røde testene viste at FastDato {} ble lagret og at import godtok ukjent enumverdi.

Gyldige standardregler, fra_dag uten uke, fra_dag foran uke, femte ukedags fallback til
siste forekomst, skuddår, årsforskyvning −1/0/+1 og relative ankerkjeder er bevart.
Uke uten fra_dag kreves nå i området 1–5; dette strammer inn tidligere implisitt klamping.
29. februar er en gyldig maldato; beregning for et ikke-skuddår gir kontrollert feil.
Manuell lagring krever tittel. Import støtter eldre filer uten tittel og normaliserer
null/tom tittel til tom tekst. En rød test viste tidligere NullReferenceException ved null.
En AFTER DELETE-trigger viser rollback av import, og neste import på samme tjeneste lykkes.
Eldre lagrede regler med JSON-feil rapporteres som genereringsfeil uten å stanse gyldige regler.

## Verifikasjon

SDK 10.0.402: låst restore og Release-bygg av ordinær løsning passerer. Alle **393 ordinære
tester passerer**, 0 feilet og 0 hoppet over. Dette inkluderer de tidligere 255 og 138 nye
tilfeller for #24. Den eksisterende CS0649-advarselen i Runde.razor er uendret.
SQL Server-prosjektets låste restore og Release-bygg passerer. #24 endrer verken EF-modell,
migrasjoner eller SQL-kontrakttilfeller. Lokale Markdown-lenker og git diff --check passerer.

[CI for PR #32](https://github.com/depmartin/Budsjettkalender/actions/runs/39435808475)
bekreftet før merge 255 ordinære og **22 SQL Server-tester**, begge med 0 feil og 0 hopp.
Denne arbeidsstasjonen har ingen brukbar SQL Server; SQL-testene er ikke kjørt lokalt for #24.
CI krever ekte SQL Server-tester uten hopp, og deploy venter på den jobben.
Kommandoer og miljøkrav står i [utviklingsveiledningen](development.md).

## Gjenstående grenser

- For RelativTilMilepael har offset_dager ingen vilkårlig grense utover heltallstypen,
  jf. [ADR-0008](adr/0008-felles-validering-av-gjentaksregler.md). En forskyvning utenfor
  DateOnlys datoområde gir fortsatt en uhåndtert ArgumentOutOfRangeException fra AddDays i
  [Genereringsberegning](../backend/Aarshjul.Application/Generering/Genereringsberegning.cs)
  og avbryter genereringen. Begrensningen fantes før #24 og er ikke rettet i denne endringen.
- Azure SQL/Entra, feed-/M365-policy og tokenvern, SQL-brannmur, migrering og Data Protection
  krever eget driftsoppsett og miljøtest. Lokal SQLite-verifikasjon erstatter ikke disse.
- Live kildehenting er fortsatt uferdig.
- Eldre [PR #8](https://github.com/depmartin/Budsjettkalender/pull/8) inngår ikke i dette arbeidet.
