# Én DbContext per tjenesteoperasjon

En scoped tjeneste lever gjennom en Blazor-krets og kan kalles samtidig. Den kan derfor
ikke eie en scoped AppDbContext. Runtime-adapterne mottar IDbContextFactory<AppDbContext>
og oppretter en ny kontekst med await using i hver offentlig operasjon. DI registrerer
fabrikken med leverandørens tilkoblingsstreng; hver kontekst får sin egen tilkobling.
Design-time-fabrikken for migrasjoner er uavhengig av dette.

Private hjelpere mottar operasjonens kontekst. En offentlig operasjon skal ikke kalle
en annen offentlig operasjon for å dele databasearbeid; de bruker en felles privat hjelper.
Godkjenning og varsel, generering, opplasting og import beholder sine samlede lagringer
og transaksjoner. Ved feil disponeres konteksten, slik at sporing ikke følger neste kall.
Application-grensesnittene og serverens synlighetsfiltrering beholdes.

Oppgave #22 verifiseres ved IFristlesing og IFristskriving: samme tjeneste i samme scope
fullfører et nytt kall mens første SELECT venter i en deterministisk testport. SQLite
bruker uavhengige tilkoblinger til samme database. Dette løser kontekstdeling; samtidig
godkjenning av samme forslag krever fortsatt egen konkurransekontroll i #23.
