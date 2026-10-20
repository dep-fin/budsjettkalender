# Arkitektur

ASP.NET Core / .NET 10 med Blazor Interactive Server. Azure SQL i produksjon,
SQLite i lokal demo og tester. Entra ID autentiserer produksjonsbrukere.

| Modul | Ansvar og testgrense |
| --- | --- |
| Aarshjul.Domain | Frist, forslag, regler, internrunde og rene datoberegninger |
| Aarshjul.Application | Offentlige tjenestegrensesnitt, DTO-er og tolkning/validering av regelkontrakter |
| Aarshjul.Infrastructure | EF Core, import/eksport, generering og uttrekk |
| Aarshjul.Web | Blazor, lese-/eksport-API, autentisering og policyer |
| Aarshjul.Kilder (backend/kilder) | IKilde og kildeuavhengige utfall/filtrering |
| Aarshjul.Tests | xUnit med SQLite og WebApplicationFactory |
| Aarshjul.SqlServerTests | Ekte SQL Server med migrasjoner; egen obligatorisk CI-jobb |

## Kritiske strømmer

Kilde/opplasting → uttrekk → forslag → administratorgodkjenning → frist.
Mal → genererte forslag → egen gjennomgang → frist.
Lesing → synlighetsfilter i SQL → DTO → HTTP/Blazor/eksport.
Internkalenderens gjøremål behandles separat bak `ErSbr`.

HTTP-policyer og Blazor-sideautorisasjon er tilgangsgrensen for skrivetjenestene.
Tjenestegrensesnittene i seg selv er ikke en ekstra autorisasjonsgrense.
Ved nye innganger må policyene håndheves og testes der.

## Gjeldende beslutninger

- [Lagdelt løsning og plattform](adr/0001-plattform-og-moduler.md).
- [Godkjenning og synlighet](adr/0002-godkjenning-og-synlighet.md).
- [Entra-roller og tokenfeed](adr/0003-tilgang-og-kalenderdeling.md).
- [Internkalender](adr/0004-internkalender.md).
- [Arbeidsflyt](adr/0005-arbeidsflyt.md).
- [Én DbContext per tjenesteoperasjon](adr/0006-operasjonsavgrenset-dbcontext.md).
- [Konkurransekontroll ved godkjenning](adr/0007-konkurransekontroll-ved-godkjenning.md).
- [Felles validering av gjentaksregler](adr/0008-felles-validering-av-gjentaksregler.md).

Se [gjeldende status](status.md) for leverte endringer og åpne oppgaver.

Detaljmodellen finnes i `SYSTEMARKITEKTUR.md`; kravene i
`kravdokument-aarshjul-frister_v2.md` og `BRUKERHISTORIER.md`.
Faseplanene og `docs/archive/` er historikk. Gjenstående live kildehenting er ikke
implementert bare fordi grensesnittet og en faseplan finnes.
