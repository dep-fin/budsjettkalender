# Årshjul for budsjettfrister

Felles språk for publiserte budsjettfrister og SBRs interne arbeidsliste.
Begrepene beskriver eksisterende kode og produktkrav.

## Frister

**Frist**: En datofestet enhet med budsjettår, kategori og synlighet. Bare godkjente frister
inngår i ordinære leseutvalg.
_Unngå_: Gjøremål (som er en annen enhet).

**Løp** (`Loep`): Funksjonen fristen tilhører, for eksempel rammefordeling.
Rundskrivnummer er kildeinformasjon og identifiserer ikke løpet.

**Budsjettår**: Året budsjettet gjelder. Fristens kalenderdato kan falle i et annet år.

**Forslag**: En upublisert kandidat fra bruker, robot eller generering, med behandlingsstatus.
Et endringsforslag viser til en eksisterende frist og endrer innholdet uten å endre synlighet.

**Godkjenningskø**: Administratorens behandling av løpende forslag. Genererte forslag
behandles på egen genereringsflate.

**Uttrekksbevis**: Kildeutdrag og tolket verdi per felt i et robotforslag.

**Gjentaksregel**: Malregel som beregner en frist for et budsjettår.
_Unngå_: Gjøremålsregel (som hører til internkalenderen).

**Anker**: Et annet løps dato som en relativ gjentaksregel beregnes fra.

**Tentativ frist**: Frist uten sikker dagsdato, representert med månedspresisjon og eventuell
primo/medio/ultimo-kvalifikator. Sorteringsdagen er et avledet visningspunkt.

## Tilgang og deling

**Funksjonsrolle**: Administrator, bidragsyter eller leser; avgjør tillatte handlinger.

**Synlighetsgruppe**: Et publikum for fristdata, blant annet FA, FIN-FAG, FAG og POL.
POL betyr politisk ledelse og tildeles eksplisitt.

**Kalenderabonnement**: Tilbakekallbar tokenlenke til en serverfiltrert iCalendar-feed.
En nedlastet ICS-fil er et øyeblikksbilde, mens abonnementet hentes på nytt av kalenderklienten.

**E-postabonnement**: Brukerens selvvalgte påmelding til e-postvarsling på egen, bekreftet
jobbadresse. Påmeldingen kan vente på adressebekreftelse, være aktiv eller være avmeldt.

**Adressebekreftelse**: Brukerens bekreftelse på at vedkommende disponerer den oppgitte
jobbadressen. Bekreftelsen tildeler ingen funksjonsrolle eller synlighetsgruppe.

**Avmeldingsrett**: En hemmelig lenke fra en oppsummering som bare gir rett til å
stanse det aktuelle e-postabonnementets bekreftede påmeldingsperiode.

**Adressebytte**: Brukerens uttrykkelige erstatning av egen jobbadresse i det samme
e-postabonnementet. Varslingen er stanset frem til den nye adressen er bekreftet.

**Administratorstans**: Administratorens bekreftede avmelding av et e-postabonnement,
med registrert aktør og tidspunkt. Brukeren kan senere melde seg på igjen ved en ny
uttrykkelig handling.

**Ny frist-hendelse**: Første publisering av en frist, som kan inngå i abonnentens
daglige oppsummering. Et upublisert forslag er ingen slik hendelse.

**Vesentlig endring**: En faktisk endring i en publisert frist som krever abonnentens
oppmerksomhet. Endret dato, tid, tentativ periode, budsjettår eller kategori er alltid vesentlig;
annen innholdsendring kan være vesentlig når administrator begrunner dette.

**Daglig oppsummering**: Én samlet e-post med relevante fristoppdateringer for én abonnent
og én norsk kalenderdag. Bekreftet overlevering betyr at transporten har tatt imot
meldingen; det bekrefter ikke at en ekstern postkasse har mottatt den.

**Slettingshendelse** (`Slettet`): Varslingshendelse om faktisk bortfall av en tidligere
publisert frist, med siste publiserte metadata og synlighet. Den tilhører Vesentlige
endringer, også når en frist fjernes fra godkjent utvalg ved import.

## Internkalender

**Internrunde**: SBRs arbeidsrunde for en rundetype og et år, med egne interne gjøremål.

**Internt gjøremål**: En arbeidsoppgave med ansvarlige og fullføringsstatus, skilt fra publiserte frister.

**Gjøremålsregel**: En mal for å generere interne gjøremål i en internrunde.
