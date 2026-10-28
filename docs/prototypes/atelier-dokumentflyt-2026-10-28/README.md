# Atelier – avgrenset dokumentflytprototype

Åpne `prototype-dokumentflyt.html` direkte i en nettleser. Ingen installasjon eller server trengs.

**Prototype med forhåndsdefinerte eksempeldata. PDF-innholdet analyseres ikke. Ingen virkelig publisering.**

Skissen undersøker om administrator kan følge arbeidsår 2031 fra det navngitte innebygde eksempeldokumentet, via uttrekksbevis/usikkerhet og kontroll/justering, til aktivt synlighetsvalg og godkjenning. Én eksempelfrist vises så i Kalender, Budsjettår/Tidslinje, Budsjettår/Fristliste og Fristregister. Kalenderdatoen er 20. november 2030; budsjettåret er 2031.

Bruk «Følg et eksempel» for hele kjeden, simulert null uttrekk eller simulert innlesingsfeil. Fanebytte starter fra tom eksempeltilstand. «Prøv fritt» tillater handlingene i valgfri rekkefølge og forklarer manglende grunnlag. Tilstandspanelet viser antall og alle relevante verdier etter hver handling. POL er aldri forhåndsvalgt. Gjentatt godkjenning beholder samme ene fristidentitet. Presisjon, dato, tittel, kategori og notat kan justeres; tentative måneder vises uten oppdiktet fristdag.

HTML-filen er selvstendig, uten nettverk, ekte filer, personer, lagring eller produksjonstjenester. Oppdatering av siden nullstiller minnet. Den rene prototypereduceren og DOM-skallet er begge forkastbar referanse og skal ikke kopieres til Blazor. Appens eksisterende dokument- og godkjenningstjenester skal gjenbrukes i produksjonsarbeidet.

Grønn Atelier følger primærreferansen `e517ed91a066872c3947e8c463704655b6cd3313`. Dette er et avgrenset tillegg for UX13 / [#88](https://github.com/depmartin/Budsjettkalender/issues/88), under [#75](https://github.com/depmartin/Budsjettkalender/issues/75). Ingen tidligere prototype eller produksjonsfil er endret. Skissen skal bevares på egen prototypegren utenfor main. Root viser skissen før produksjonsombyggingen.

Manuelle nettleserobservasjoner og skjermbilder lagres lokalt under ignorert `TestResults/88-prototype/`. Prototypebilder beviser bare prototypeatferden. Ekte PDF/PdfPig, lagring, samtidighet, serverroller/synlighet, SQL Server, Chromium-CI, Entra, Azure og Outlook krever egne appbevis.

Lokal gjennomgang 28. oktober 2026 i Edge 156.0.4314.42, ved 1440 og 390 piksler: hele kjeden kunne gjennomføres. Før godkjenning fantes bare ett åpent forslag og ingen publisert frist. Etter tittel-/kategorijustering og aktivt FA-valg viste alle fire visninger én `EKSEMPEL-FRIST-2031-01` med dato 20. november 2030 og budsjettår 2031. Gjentatt godkjenning beholdt én frist. Null uttrekk, innlesingsfeil, nytt forsøk, godkjenning uten grunnlag/synlighet, eksplisitt POL-valg og fjerning, tentativ måned uten dagsmarkering, frilek/tilstandspanel og nullstilling ved sideoppdatering ble gjennomgått. Escape returnerte fokus til fristtittelen; scenariefanene kunne skiftes med piltast. Ingen JavaScript-feil eller eksterne nettverksforespørsler ble registrert. Dokumentbredden var 1440/1440 og 390/390; tidslinjen har tilsiktet lokal horisontal rulling. Faktiske skjermbilder er visuelt inspisert. Dette er observasjoner av skissen, ikke en brukertest eller produksjonsakseptanse.
