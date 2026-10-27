# Atelier – gjeldende visuelle referanse

Den samlede referansen er [Atelier med oppdatert administrasjon og årsmal](atelier-samlet-2026-10-27/README.md). Start med [den samlede skissen](atelier-samlet-2026-10-27/prototype-atelier-admin.html). Dette er produkteierens valgte videreføring per 27. oktober 2026.

[Issue #75](https://github.com/depmartin/Budsjettkalender/issues/75) inneholder den samlede bestillingen og testgrensene. Fremtidig arbeid skal ta utgangspunkt i den nyeste samlede referansen og avklaringene i oppgaven. Prototypene er ikke produksjonskode eller bevis på ferdig appimplementering.

## Avgjørende valg

- Atelier videreføres for lesere og administratorer. Kalender er hovedvisning; Budsjettår har Tidslinje og Fristliste. Separat Årshjul-fane er samlet i Fristliste med funksjoner og gamle innganger bevart ved implementering.
- Administrasjon har seks hovedvalg: Budsjettår og frister, Årsmal, Grupper og innsyn, Eksport og deling, Internkalender og Administrator-innstillinger.
- Konkrete frister, forslag, dokumentinnlesing og generering grupperes under valgt budsjettår. Generelle gjentaksregler ligger under Årsmal.
- Eksisterende og nye gjentaksregler har egne faner. Liste støtter kategori, søk og sammenheng. Selvstendige regler med avhengige regler merkes visuelt som milepæler. Et avhengig mellomledd forblir avhengig. Merket er ingen ny regeltype.
- Regelbyggeren må bevare prosjektets tidligere fleksibilitet uten JSON-innskriving: fast dato, n-te ukedag, første ukedag fra og med dato, relative milepæler, årsforskyvning, valgårsvalg, manuelle ankre og maloverføring.
- Felles faseinndeling ligger i Administrator-innstillinger. Én mal gjelder alle år og endrer ingen frister. Utnevnelse/fjerning av administrator er et avklart visuelt utkast; reell rolleforvaltning krever egen avgrenset implementering og viderefører Entra-skillet.
- Kalender, Budsjettår og Fristregister bruker samme fristobjekter. Generering bruker samme årsmal som redigeres. Forslag publiseres først etter godkjenning.
- PDF-behandling er fortsatt utsatt. Den senere dokumentflyten må fortsatt demonstreres. Prototypens eksempeluttrekk er ikke resultat av en opplastet PDF.

## Historiske referanser

- [Første kalender og grafiske budsjettår](atelier-2026-10-23/).
- [Første administratorutkast](atelier-admin-2026-10-27/).
- [Utvidede administratorarbeidsflyter](atelier-admin-arbeidsflyter-2026-10-27/).
- [Separat demo av fleksibel årsmal](atelier-aarsmal-2026-10-27/forslag.md). Denne demonstrerte regelbyggeren før regellisten og Administrator-innstillinger ble tatt inn i den samlede referansen.

Historiske varianter skal ikke overstyre nyere avklaringer i den samlede skissen eller issue #75.
