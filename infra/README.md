# infra

Bicep-maler for Azure-infrastrukturen (kravdok. 9.2).

- `main.bicep` — App Service-plan + App Service (Blazor + API), Azure SQL (Entra-only auth,
  serverless), Key Vault (RBAC), Application Insights + Log Analytics. Web-appens managed
  identity gis lesetilgang til Key Vault og brukes til SQL-tilkobling (ingen lagrede passord).
- `main.bicepparam` — eksempelparametre; fyll inn reelle verdier eller overstyr i CI.

[EV01 – driftskontrakt for e-postvarsling](../docs/operations/epostvarsling-ev01.md)
er avstemt mot levert EV11/PR #149. Den beskriver anbefalt Graph/Exchange-kandidat,
nødvendige rettigheter, autoritativ identitetskilde og konkrete operatørsteg.
D1–D5 står åpne; dokumentleveransen etablerer ingen faktisk tjeneste, avsender,
tilgang eller produksjonsaksept.

Applikasjonen har nå minuttvis `DagligEpostjobb`, varig dagskoordinering,
beskyttet HTTP/Blazor-driftsstatus og stans, begrenset retry/venting og trygg
retensjon, se [ADR-0010](../docs/adr/0010-varig-daglig-epostoppsummering.md).
Produksjonsadapterne for identitet og post er fortsatt utilgjengelige.
Bicep-malen etablerer ingen postboks, Graph-/Exchange-rettigheter eller egen
jobbressurs og setter ikke `alwaysOn`. Faktisk App Service-oppsett og navngitte
operatører må avklares ved D2/D4; repoets tekniske standarder er ikke godkjent
produksjonspolicy. Ingen infrastruktur eller kjørbar konfigurasjon endres av EV01.

## Validering og utrulling

- Kompiler/valider lokalt: `bicep build infra/main.bicep`
- What-if: `az deployment group what-if -g <rg> -f infra/main.bicep -p navnPrefiks=<x> miljo=dev sqlAdAdminLogin=<grp> sqlAdAdminObjectId=<oid>`
- Deploy skjer normalt via `.github/workflows/deploy.yml` (manuell dispatch).

Etter første utrulling må web-appens managed identity få databasetilgang i SQL via T-SQL
(`CREATE USER [<web-app-navn>] FROM EXTERNAL PROVIDER;` + `db_datareader`/`db_datawriter`/
`db_ddladmin` for migrasjoner). Dette gjøres som Entra-administrator på databasen.

## Key Vault ved weboppstart

[#40](https://github.com/dep-fin/budsjettkalender/issues/40) kobler Biceps
`KeyVault__Uri` til webappens konfigurasjon. Oppstarten laster hemmelighetene med den
offisielle Azure-provideren før valg av demo, registrering av database og Entra-binding.
`ManagedIdentityCredential` bruker eksplisitt webappens systemtilordnede identitet,
slik `main.bicep` oppretter den. Det er ingen automatisk bruk av lokale utviklerkontoer.
Pakker: Azure.Extensions.AspNetCore.Configuration.Secrets 1.5.2 og Azure.Identity 1.21.0;
hele grafen er låst i prosjektets `packages.lock.json`.

Sett `KeyVault__Uri` til hvelvets HTTPS-rotadresse, for eksempel
`https://mitt-hvelv.vault.azure.net/`. Avsluttende skråstrek er valgfri. Adressen kan ikke
ha brukerinfo, annen port enn 443, ekstra sti, spørring eller fragment. Manglende nøkkel
deaktiverer lastingen; en eksplisitt tom eller ugyldig verdi stanser oppstarten.
La derfor nøkkelen være fraværende i lokal Demo/Testing som skal kjøre uten Azure.
En eksplisitt URI aktiverer lasting også i disse miljøene.

Provideren legges sist og har høyest prioritet: **Key Vault → kommandolinje →
miljøvariabler → user secrets i Development, dersom konfigurert →
appsettings.{miljø}.json → appsettings.json → vertens reservekonfigurasjon**.
Verdier som bare finnes i en lavere kilde, beholdes. URI-en er oppstartskonfigurasjon
og må finnes før hvelvet leses; den skal ikke hentes fra hvelvet selv.

Vanlig Azure-navnemapping brukes uten egne prefikser eller filtre:

| Hemmelighetsnavn i Key Vault | Konfigurasjonsnøkkel i appen |
| --- | --- |
| `AzureAd--ClientSecret` | `AzureAd:ClientSecret` |
| `AzureAd--ClientId` | `AzureAd:ClientId` |
| `ConnectionStrings--Aarshjul` | `ConnectionStrings:Aarshjul` |

Alle aktiverte hemmeligheter i hvelvet inngår; bruk et hvelv avgrenset til denne appen.
Provideren mellomlagrer verdiene gjennom appens levetid. Ingen periodisk oppdatering
er konfigurert; start appen på nytt etter rotasjon eller andre hemmelighetsendringer.

Ved ugyldig URI eller lastingsfeil stanser oppstarten uten tilbakefall til lokale
verdier. Feilen viser en fast norsk veiledning. Rå URI, SDK-unntak og indre unntak
videresendes ikke. HTTP-pipelinelogging, innholdslogging og Activity-sporing er slått av
for denne Key Vault-klienten og managed identity-klienten. Kontroller først URI-en,
deretter nettverk/DNS, aktivert systemidentitet og rettighetene til både å liste og
lese hemmeligheter. Gjør slike kontroller i Azure uten å eksportere hemmelighetsverdier
eller skru på rå SDK-logging i appen.

Azure.Identity kan likevel skrive leverandørens feilrespons direkte til EventSource,
også med alle disse diagnostikkflaggene av og uten MSALs PII-logging. SDK-en har ingen
støttet innstilling som stanser alle slike hendelser per legitimasjon. Derfor
undertrykker appen hele `Azure.Identity`-kategorien ved sin `ILoggerFactory`, også
leverandørspesifikke konfigurasjonsregler og etter konfigurasjonsreload. Dette gjelder
alle appens identitetsklienter, også uten Key Vault-URI. Andre loggkategorier og deres
eksisterende filtre beholdes. Ingen responsinnhold redigeres; identitetsdiagnostikken
utelates fra appens loggsinker. Dette koster de detaljerte identitetsloggene.

Standardoppstarten registrerer ingen direkte Azure-SDK-logglytter. Ved eventuell
videresending gjennom `AzureEventSourceLogForwarder` blir `Azure-Identity` til
`Azure.Identity`, og appens filter gjelder. En rå EventSource-/ETW-/EventPipe-lytter,
`AzureEventSourceListener.CreateConsoleLogger`, en debugger eller en ekstern
telemetriagent som leser rå hendelser, går utenom `ILoggerFactory`. Appen kan ikke
hindre disse i å lese feilrespons eller andre prosessdata. Slik diagnose har samme
tillitsgrense som tilgang til hemmeligheter i prosessen og skal ikke videresendes
til ordinære driftslogger. P13–P15 må kontrollere faktisk agent-/telemetrioppsett i Azure;
lokale tester bekrefter ikke eksterne innsamleres konfigurasjon.

Lokal aksept bruker WebApplicationFactory, samme oppstartsmetode som Main og en ekte
SecretClient/provider med syntetisk legitimasjon og styrt HTTP-transport. Den prøver
databasens oppstartslesing, reell Entra-opsjonsbinding, kildeprioritet, `--`-mapping,
URI-avvisning, liste-/hente-/nettverks-/legitimasjonsfeil og fravær av følsomme verdier
i oppstartsfeil og logger. Produksjonsvarianten av Entra-bindingen bytter bare den
eksterne databasen med SQLite. En egen regresjon bruker ekte systemtilordnet
`ManagedIdentityCredential` mot en syntetisk App Service-identitetsrespons via SDK-ens
støttede HTTP-transport. Rå SDK-hendelser inneholder bevisst en følsom testmarkør;
videresending til appens faktiske loggerfabrikk slipper den ikke gjennom, også med
eksplisitt Trace-regel per loggleverandør og konfigurasjonsreload. Øvrig applogging og
kontrollert oppstartsfeil beholdes. Prosessmiljøet gjenopprettes, og testsamlingen
kjører uten parallellitet med andre testklasser. Dette er ikke bevis for Entra-innlogging, SQL Server,
managed identity, RBAC eller Azure-nettverk. Faktisk miljøaksept hører til P13–P15 i
[#38](https://github.com/dep-fin/budsjettkalender/issues/38).
Data Protection-oppsett er en egen oppgave (#41).

Primærkilder: [Key Vault-provider, navnemapping og mellomlagring](https://learn.microsoft.com/en-us/aspnet/core/security/key-vault-configuration?view=aspnetcore-10.0),
[ASP.NET-konfigurasjon og kildeprioritet](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-10.0),
[deterministisk legitimasjon i produksjon](https://learn.microsoft.com/en-us/dotnet/azure/sdk/authentication/best-practices)
og [systemtilordnet managed identity](https://learn.microsoft.com/en-us/dotnet/azure/sdk/authentication/system-assigned-managed-identity).
Logggrensen bygger på [Microsofts SDK-loggmodell og videresending](https://learn.microsoft.com/en-us/dotnet/azure/sdk/logging)
og [valg av loggregel per leverandør](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-10.0#how-filtering-rules-are-applied).
Den låste Azure.Core 1.61.0-kilden viser at [HTTP-loggflagget](https://github.com/Azure/azure-sdk-for-net/blob/3830815f87881cce7af68dd9dd4126cbd90e197b/sdk/core/Azure.Core/src/DiagnosticsOptions.cs)
ikke styrer [direkte identitetsfeilhendelser](https://github.com/Azure/azure-sdk-for-net/blob/3830815f87881cce7af68dd9dd4126cbd90e197b/sdk/core/Azure.Core/src/Identity/CredentialDiagnosticScope.cs).
[AzureIdentityEventSource](https://github.com/Azure/azure-sdk-for-net/blob/3830815f87881cce7af68dd9dd4126cbd90e197b/sdk/core/Azure.Core/src/Identity/AzureIdentityEventSource.cs)
formaterer både feil og indre feil; [den offisielle videresenderen](https://github.com/Azure/azure-sdk-for-net/blob/3830815f87881cce7af68dd9dd4126cbd90e197b/sdk/extensions/Microsoft.Extensions.Azure/src/AzureEventSourceLogForwarder.cs)
viser kategorimappingen som regresjonen bruker.
