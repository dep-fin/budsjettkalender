# Atelier som felles løsning for leser og administrator

27. oktober 2026

Atelier-løsningen med Kalender og grafisk Budsjettår videreføres som fast utgangspunkt. Administrator bruker samme ramme, farger, typografi, tidslinje, datovisning og detaljpanel som leseren. Ekstra handlinger legges til i arbeidskonteksten; den ordinære leseopplevelsen er fortsatt grunnlaget.

[Åpne administratorutkastet](prototype-atelier-admin.html). Velg «Se som leser» øverst for å sammenligne den samme visningen uten administrative handlinger. Utkastet starter i Budsjettår for å vise faseinndelingen. Kalender skal fortsatt være hovedinngang i den ferdige appen. Tilbakestilling skjer ved å laste siden på nytt. Skissen bruker bare konstruerte data og har ingen forbindelse til produksjonsappen.

## Avklarte beslutninger

- Én felles fasemal gjelder alle budsjettår. Det skal ikke finnes årsspesifikke unntak.
- Faseskifte skjer 1. mars t−1, 1. oktober t−1, 1. januar t og 1. januar t+1.
- Regnskap varer til og med april t+1.
- Administrator kan redigere navn og månedsgrenser i fasemalen. Endringen gjelder også leservisningen.
- Kalender og Budsjettår presenterer de samme publiserte fristene. Ingen separat kopi av en frist opprettes for den andre visningen.
- Leseropplevelsen er viktigst. Administratorutformingen skal videreføre den valgte løsningen.

## Felles faseinndeling

| Fase | Fra og med | Til og med |
| --- | --- | --- |
| Forberedelser | Oktober t−2 | Februar t−1 |
| Utarbeidelse av budsjettforslag | Mars t−1 | September t−1 |
| Fremleggelse og vedtak | Oktober t−1 | Desember t−1 |
| Gjennomføring og revidering | Januar t | Desember t |
| Regnskap | Januar t+1 | April t+1 |

Fasemalen beskriver relative måneder rundt budsjettåret t. Den har ingen egen liste med frister. Hver frist plasseres etter sin faktiske dato og vises med sin lagrede presisjon. En frist utenfor faseinndelingen beholdes synlig, og tidslinjen utvides ved behov.

## Tydeligere markering av i dag

Tidslinjen har en rød loddrett strek og en etikett med «I dag» og dato. Rødt skiller markøren fra budsjettårets farge. Markøren ligger bak fristkortene, slik at tekst ikke krysses av streken. I utkastet er eksempeldatoen 27. oktober 2026; den ferdige løsningen skal bruke faktisk dato i Oslo-tid.

## Administratorens arbeidsflate

**Kalender og Budsjettår** har samme visning som for leseren. Administrator får «Ny frist», «Rediger frist» i detaljene og «Rediger faser» ved tidslinjen. Klikk på en dato i kalenderen åpner et skjema med datoen forhåndsutfylt. Dette er administratorens direkte fristforvaltning, adskilt fra bidragsyterens forslag.

**Fristredigering** bruker samme felter og formspråk som øvrige Atelier-dialoger. «Lagre og publiser» gjør virkningen tydelig. Synlighet velges uttrykkelig, og POL er ikke forhåndsvalgt. Registreringen oppdaterer ett fristobjekt, som begge visningene deretter bruker. Kilderundskriv vises over «Om fristen» når en gyldig kobling finnes.

**Rediger fasemal** er én dialog for alle fem faser. Administrator redigerer navn, startmåned og år i forhold til budsjettåret. En fase slutter når neste begynner. Den siste fasens siste måned velges uttrykkelig. Dette hindrer overlapp og hull uten at brukeren må skrive samme grense to ganger. Før lagring fremgår det at malen gjelder alle år og ikke flytter frister.

**Administrasjon** samler Godkjenningskø, Fristregister, Felles fasemal, Dokumenter og årsmal, Grupper og innsyn, Eksport og deling, og Internkalender i lokal navigasjon. Godkjenningskøen bruker samme kort, knapper, felt og tilbakemeldinger som resten av løsningen. Den skiller mellom ny frist og endringsforslag, og viser gjeldende og foreslått dato.

Dokumentopplasting, generering, grupper, innsyn, eksport, deling og internkalender er plassert i skissen, men deres eksisterende funksjoner er ikke bygget på nytt her. Dagens årshjul og tidligere ukevisning beholdes. Skissen utforsker de sentrale administratorhandlingene og deres forhold til den valgte leseopplevelsen.

## Samme data i alle visninger

I utkastet bruker Kalender, Budsjettår, Årshjul og Fristregister den samme samlingen av fristobjekter i minnet. Opprett eller rediger en frist og velg «Vis i kalenderen» eller «Vis i Budsjettår» for å følge den samme posten. Godkjenning av et forslag gjør også fristen tilgjengelig i begge visninger.

I produksjon skal dette følge eksisterende autorisert fristlesing og fristskriving. Visningsbytte oppretter ingen kopi eller egen publisering. Søk, perioder og budsjettårsvalg kan gi ulike utvalg, men samme frist skal ha samme identitet og verdier når den inngår i begge. Forslag blir ikke publiserte frister før godkjenning. Synlighet filtreres på server.

Fasemalen er separat fra fristene. Endring av en fase kan endre hvilken fase en frist faller innenfor, men endrer verken dato, tittel, publiseringsstatus, kilde eller synlighet. Dette skal også kontrolleres når malen lagres.

## Veien til implementering

Den felles redigerbare fasemalen er et tillegg til den tidligere spesifikasjonen i #75. Den trenger varig lagring, administratorbeskyttet oppdatering og kontroll av konkurrerende endringer; dette er ikke en statisk CSS-endring. En avgrenset oppgave bør levere hele flyten fra administratorens endring til lagret mal og oppdatert leservisning.

Eksisterende IFristlesing og fristforvaltnings-/godkjenningsgrenser gjenbrukes for fristene. Fasemalen får én tydelig applikasjonsgrense for lesing og oppdatering dersom ingen eksisterende port dekker behovet. Valideringen må avvise manglende navn, ikke-stigende grenser og ugyldige sluttmåneder. En gammel administratorfane skal ikke kunne overskrive en nyere mal uten tydelig håndtering.

Bruk eksisterende reelle nettleserprøver for oppretting, redigering, forslag/godkjenning, leserrolle og navigasjon. Legg til scenarier som viser at en endret frist får samme identitet og verdier i begge visninger, at en felles faseendring gjelder flere budsjettår og begge roller, og at alle fristopplysninger er uendret etter endring av fasemalen. Tjeneste-/HTTP-grenser kontrollerer tilgang og lagring; UI alene er ikke sikkerhetsbevis.

## Avgrensning

Dette er et interaktivt designutkast, ikke en implementering av appen. Rollebryteren demonstrerer presentasjon og er ikke innlogging. Alle endringer forsvinner ved omlasting. Den eksisterende produksjonskoden og hovedgrenen endres ikke av utkastet.

## Kontroll av utkastet

Kontrollert i isolert Edge med faktiske klikk: opprett frist i tidslinjen, finn den i kalenderen, rediger den og se oppdateringen i tidslinjen; godkjenn forslag til samme datasett; endre felles fase for flere budsjettår; bytt mellom administrator og leser. Mobilkontroll ved 390 piksler viser ingen horisontal utvidelse av hele siden. Faseeditoren passer innenfor dialogbredden. Ingen JavaScript-feil ble registrert i de kontrollerte flytene. Dette er prototypebevis, ikke produksjonsakseptanse.
