# Separat forslag: administrasjonsmeny og årsmal

27. oktober 2026. **Til vurdering, ikke godkjent eller innarbeidet i den samlede skissen.** Ingen GitHub-endringer er gjort i denne runden. Den foregående Atelier-prototypen beholdes.

Åpne `prototype-aarsmal.html` direkte i nettleseren. Bare Årsmal er bygget her. De andre menyvalgene forklarer foreslått innhold. Alle regler er konstruerte; endringer ligger i minnet og nullstilles ved omlasting.

## Foreslått fordeling

| Hovedvalg under Administrasjon | Underområder |
| --- | --- |
| Budsjettår og frister | Velg konkret budsjettår. Oversikt/fristregister, løpende forslag til behandling, dokumentinnlesing, Ny frist og Generer fra årsmal. Genererte forslag beholder sin egen behandlingsflate under dette året. Årsvalget skal kunne vise antall ubehandlede forslag i andre år, slik at de ikke glemmes. |
| Årsmal | Gjentaksregler som gjelder flere budsjettår; Prøv malen; felles faseinndeling; overføring av regelsett. |
| Grupper og innsyn | Grupper, aktivering/deaktivering og forhåndsvisning av gruppenes innsyn. |
| Eksport og deling | Word, ICS, abonnement og dataoverføring med eksisterende policyer. Maleksport/-import kan også ha en kontekstuell inngang fra Årsmal. |
| Internkalender | SBRs runder, interne gjøremål, egne oppgaver og gjøremålsregler. |

Skillet følger hva administrator endrer: **det konkrete budsjettåret** eller **den generelle malen**. Årsmalens «Prøv malen» er bare en forhåndsvisning; den oppretter ikke forslag. Generering for faktisk behandling hører til under det konkrete budsjettåret. Den avtalte PDF-flyten er fortsatt utsatt og bygges ikke i denne demoen.

## Årsmal-demoen

Gjentaksregler har en oversikt til venstre og en regelbygger med tre steg: hva fristen gjelder, hvordan datoen beregnes, og kontroll av fem budsjettår. Feltene lager en lesbar setning og beregner resultatene mens man registrerer. Ingen regelparametre må skrives som kode eller JSON.

| Valg i regelbyggeren | Eksisterende funksjon som ivaretas |
| --- | --- |
| Fast dato | Måned, dag og kalenderår i forhold til budsjettåret. |
| Ukedag i måneden | Første til femte forekomst. Femte bruker siste hvis bare fire finnes. |
| Ukedag fra en dato | Første valgte ukedag fra og med en dag i måneden. «Mandag fra og med 20. juli» treffer alltid 20.–26. juli før eventuell helgejustering. Dette er et sjudagersvindu, ikke en ISO-kalenderuke. |
| Før eller etter en milepæl | Velg eksisterende milepæl eller en manuelt fastsatt milepæl, antall kalenderdager og før/etter. 0 betyr samme dag. Koblinger kan danne kjeder; sirkler og manglende ankre gir synlige feil. |

Alle regler beholder tittel, kategori, funksjon/løp og valgårsvalg. Nye regler får funksjonsnavn automatisk; feltet kan justeres under Kobling og detaljer. Oppretting, redigering, sletting og kopiering kan prøves. Sletting opplyser om avhengige regler. Det er mulig å lagre en relativ regel som trenger manuelt anker; den får ingen gjettet dato.

**Prøv malen** viser alle lagrede regler for valgt målbudsjettår. Manuelle ankerdatoer gjelder kun den prøveberegningen og kan fjernes igjen. Tentativitet arves fra et tentativt anker. Ulagrede utkast tas ikke med i denne samlede prøven.

**Faseinndeling** viderefører den ene felles malen med de fem avklarte fasene. Faseskifter skjer den første i måneden. Endringer flytter ingen frister og endrer ingen gjentaksregler.

**Overfør mal** demonstrerer nedlasting og forhåndsvisning/import av gjentaksregler. Formatet følger den undersøkte MalDatabase-kontrakten med versjon, ID, løp, tittel, kategori, regeltype, parametre og valgårsvalg. Import er fortsatt erstatt-alt med gyldige regler; ugyldige regler vises som utelatte. Er ingen regler gyldige, opplyses det eksplisitt at demomalen blir tom. Faseinndeling er egen konfigurasjon og inngår ikke i dette eldre regelsettformatet. Ingen fil sendes til en server.

## Eksisterende beregningsvalg er bevart

- Årsforskyvning må ikke forveksles med fristens budsjettår. Eksempelvis kan budsjettåret 2026 ha en dato i juli 2025.
- Fast dato og ukedagsregler følger eksisterende helgejustering: lørdag til fredag, søndag til mandag, med tilpasning for å bli innen samme kalenderår. Helligdager inngår ikke. Helg og justert resultat forklares i forhåndsvisningen.
- Relativ forskyvning teller kalenderdager og helgejusteres ikke på nytt.
- Valgårsvalg følger datoens kalenderår: stortingsvalgår gir tentativ måned; kommunevalgår gir markering og beholder dato. Manuelt satt ankerdato overstyrer beregningen.
- En ugyldig dato, for eksempel 29. februar i et år uten skuddår, gir synlig beregningsfeil for det året. 29. februar kan fortsatt lagres i en mal for flere år.
- Flere regler for samme løp må ikke forsvinne. Beregning gir ett resultat per regel; ankerkobling bruker første regel for løpet som i prosjektet.

## Undersøkt prosjektgrunnlag

Lest fra den eksisterende lokale prosjektkopien `work/Budsjettkalender-73`, uten endringer i produksjonsfiler:

- `GLOSSARY.md` og `docs/adr/0008-felles-validering-av-gjentaksregler.md`.
- `backend/Aarshjul.Application/Generering/Regelparametre.cs` og `Genereringsberegning.cs`.
- `backend/Aarshjul.Domain/Datoberegning.cs`, `Gjentaksregel.cs` og `Valgaar.cs`.
- `backend/Aarshjul.Web/Components/Pages/Admin/RedigerMalregel.razor` og `Mal.razor`.
- `backend/Aarshjul.Application/Datautveksling/IMalDatautveksling.cs`.

Bekreftelsen viser at den tidligere skissens to regelvalg var en forenkling av prosjektets funksjoner. Den nye demoen gjør de eksisterende regeltypene tilgjengelige uten innskriving av JSON. Demoens beregninger er lokale illustrasjoner; produksjonen skal gjenbruke prosjektets validering og beregning ved en senere implementering.

## Utført prototypekontroll

Edge-kontroll viste at juli-eksempelet ga 21. juli 2025, 20. juli 2026, 26. juli 2027, 24. juli 2028 og 23. juli 2029, for budsjettårene 2026–2030 med ett års forskyvning bakover. Femte mandag i februar 2026 ga 23. februar. Ny regel ble lagret i demoen; manuelt anker 8. oktober 2025 ga avhengig frist 1. oktober. Valgårsvalg fulgte kalenderåret; redigert fase endret ikke reglene. Nedlasting og eksport/import av eksempelreglene ble kontrollert, og importeksempelet viste én gyldig og én ugyldig regel uten å endre malen før bekreftelse. Mobilvisningen hadde sidebredde 390/390. Ingen JavaScript-feil ble registrert i disse flytene.

Dette er ingen verifikasjon av produksjonsappen, faktiske fristdatoer eller varig lagring. Etter brukerens godkjenning skal menyvalgene og regelbyggeren innarbeides i den samlede skissen, og deretter lagres med oppdatert spesifikasjon og referanser i GitHub. Avtalte nettleser-/tjenestetestgrenser videreføres ved faktisk implementering.
