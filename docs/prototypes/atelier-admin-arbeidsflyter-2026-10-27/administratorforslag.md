# Atelier – klikkbare administratorarbeidsflyter

Dato: 27. oktober 2026. Dette er et nytt utkast bygget på den valgte Atelier-utformingen. Tidligere skisser beholdes som referanser.

Åpne `prototype-atelier-admin.html` direkte i nettleseren. Kalender er startvisningen. Velg **Administrasjon** for å prøve arbeidsflytene. **Se som leser** viser den enklere FA-presentasjonen. Alle data er konstruerte, alle endringer skjer i minnet, og omlasting tilbakestiller skissen.

## Avklarte valg

- Minikalenderen er tilbake i Kalenderens venstre sidepanel. Den bytter periode og valgt dato og markerer dager med synlige frister i valgt årsutvalg.
- Kalender har Måned, Uke og Liste, med flervalg av budsjettår. Farger og årstekst skiller årene også i listen.
- Den separate Årshjul-fanen er samlet under **Budsjettår → Fristliste**. Tidslinje er det grafiske alternativet. Ved implementering skal gamle direkte innganger fortsatt fungere.
- Kalender, Budsjettår og Fristregister leser de samme fristobjektene. Oppretting, redigering og godkjenning oppdaterer disse dataene.
- Den røde «I dag»-markøren og de fem avklarte fasene videreføres. Administrator redigerer én felles fasemal for alle år; fasene flytter ingen frister.
- Administrator har egen hovedfane og venstremeny. På mobil vises denne menyen over innholdet.

## Arbeidsflater som kan prøves

| Område | Klikkbare arbeidsflyter |
| --- | --- |
| Godkjenningskø | Filtrer på type og kilde/innsender. Sammenlign datoer, juster, godkjenn og publiser, eller avvis. Ny frist krever valgt synlighet; POL er et aktivt valg. Et endringsforslag bevarer eksisterende synlighet. |
| Fristregister | Søk, velg budsjettår, opprett og rediger frist med dato eller tentativ måned, valgfritt klokkeslett, kategori, notat og synlighetsgrupper. Se endringen i Kalender og Budsjettår. |
| Felles fasemal | Rediger navn og månedsgrenser. Den samme inndelingen brukes i alle budsjettår og i leservisningen. |
| Dokumenter og årsmal | Velg lokal PDF eller eksempelmetadata, forhåndsvis opplysninger og åpne en forhåndsdefinert uttrekksoversikt med kildeutdrag. Opprett, endre og fjern gjentaksregler; prøv faste og relative datoer. |
| Generer budsjettår | Velg år, beregn forslag fra årsmalen, og godkjenn eller avvis enkeltvis. Beregning publiserer ingen frister. Genererte forslag har egen arbeidsflate. |
| Grupper og innsyn | Opprett og rediger grupper, aktiver/deaktiver og forhåndsvis publiserte frister som valgt gruppe. Gruppekoder og eksisterende synlighetsreferanser bevares. |
| Eksport og deling | Velg publikum og datoer, se Word-oppsett og kalenderfilinnhold, last ned et JSON-eksempel. Opprett, deaktiver og slett ufarlige demolenker til abonnement. Lagre en demokopi, forhåndsvis og gjenopprett den i skissen. |
| Internkalender | Opprett internrunde, legg til gjøremål raskt eller med fullt skjema, rediger, fullfør, åpne igjen og slett. Filtrer på ansvarlig, se egne gjøremål, og vedlikehold/synkroniser gjøremålsregler. Interne gjøremål publiseres ikke som frister. |

## Dokumentbehandling skal videreføres senere

Etter brukerens avklaring vises dokumentskjermene nå, mens behandling av valgt PDF er utsatt. Skissen leser ikke filinnhold, laster ikke opp dokumenter og oppretter ikke forslag fra filvalget. «Les inn PDF» er derfor deaktivert. Uttrekksoversikten og dokumentforslaget i køen er separate forhåndsdefinerte eksempler, tydelig merket som slike.

En senere prototype skal demonstrere hele kjeden: dokumentvalg → uttrekksforslag → kontroll og godkjenning → synlig frist. Dette er et gjenstående arbeid, ikke levert dokumentbehandling.

## Grenser og kontroll

Word og kalenderfil er forhåndsvisninger, ikke ferdige filgeneratorer. JSON-nedlastingen inneholder kun eksempeldata. Abonnementsadresser bruker `eksempel.invalid` og er ingen virkelige feedlenker. Leser-/administratorbyttet viser presentasjoner og er ikke autentisering. Gruppeinnsyn er et UI-eksempel; produksjonens serverpolicyer gjelder fortsatt.

Kilderundskriv vises over «Om fristen» når et fristobjekt har en kilde. R-7/2026-lenken er en illustrativ kobling til et virkelig dokument; den konstruerte eksempelfristen er ikke dokumentert av rundskrivet.

Nettleserkontroll i Edge har prøvd minikalender, ukevisning, samlet Fristliste, dokumentmetadata uten behandling, ny gjentaksregel, generering uten publisering, enkeltgodkjenning, ny gruppe, innsyn uten redigeringskontroller, eksportforhåndsvisninger, JSON-nedlasting, abonnementslenke, demokopi/gjenoppretting, ny internrunde, fullført gjøremål og synkronisering. Kalender og dokumentflate er også kontrollert ved 390 pikslers bredde. Ingen JavaScript-feil ble registrert i disse flytene. Dette er prototypekontroll, ikke godkjenning av produksjonsappen.

Videre implementering følger spesifikasjonen i issue #75 og de avtalte nettleser-/tjenestetestgrensene. Skissen endrer ikke produksjonskode eller tilgangsregler.
