# Driftskontrakt for e-postvarsling – EV01

Undersøkt 28. oktober 2026 for [EV01/#111](https://github.com/depmartin/Budsjettkalender/issues/111), med [#110](https://github.com/depmartin/Budsjettkalender/issues/110) som kravgrunnlag. Begge oppgaver og alle kommentarer er lest, inkludert [oppstartskommentaren i #111](https://github.com/depmartin/Budsjettkalender/issues/111#issuecomment-6467293658); #110 hadde ingen kommentarer ved kontrollen. Det opprinnelige repo-utgangspunktet var `61834a05f4f73c4a79c35f870a61e0351ae3daf4`.

Kontrakten og repoets kildepekere er kontrollert på nytt 28. oktober 2026 mot `main` ved `31bfddbe38feb6088f907d38f2a28357b856e320`, etter innfletting av [UX15/PR #127](https://github.com/depmartin/Budsjettkalender/pull/127). Eksport- og delingsleveransen endrer ikke de påviste identitets- og infrastrukturforutsetningene for EV01. Beslutningsportene og miljøgrensene nedenfor består.

## 1. Anbefalt kandidat og beslutningsstatus

Anbefalt tjenestekandidat er **Exchange Online gjennom Microsoft Graph v1.0 `sendMail`, fra én egen avsenderpostboks, med Exchange Application RBAC avgrenset til denne postboksen**. Foreslått visningsnavn er **Budsjettkalenderen**, og foreslått adresse er **`budsjettkalender@fin.dep.no`**. Adressen er et navneforslag. Verken domene, ledig adresse, postboks, lisens, administratorer eller rettigheter er bekreftet i tenant.

Anbefalt kjøreform er en `BackgroundService` i eksisterende Linux App Service, med Always On, varig koordinering i Azure SQL og tidsberegning for Europe/Oslo i applikasjonen. Identitetskandidaten er webappens systemtilordnede managed identity i samme tenant som avsender og ressurskatalog. Dette viderefører [plattformbeslutningen](../adr/0001-plattform-og-moduler.md) og eksisterende [infrastrukturoppsett](../../infra/README.md), men gir hele webprosessen de nye tekniske rettighetene. IT må godkjenne denne tillitsgrensen. Krav om egen prosess/identitet utløser valg av separat jobbvert før EV12/EV13 implementeres.

Dokumentet velger en konkret kandidat for videre beslutning og beskriver kontrakten kandidaten må oppfylle. **Faktisk tjenestevalg, avsender, kilder for eksterne brukere, ansvarstildeling og produksjonsaktivering gjenstår.** Verdiene og grensene nedenfor er forslag til driftsoppsett, med mindre de uttrykkelig er knyttet til et vedtatt krav i #110. Ingen kode, konfigurasjon eller tenant er endret av EV01.

Produkteier har opplyst at tenant, postboks og IT-eier er ukjent, og bedt undersøkelsen fortsette der det er mulig. Dette gir grunnlag for planen, men bekrefter ingen miljøforutsetninger eller ansvarstildeling.

| Beslutningsport | Konkret beslutning og nødvendig bevis | Foreslått beslutningseier; faktisk ansvarlig må utpekes | Status |
| --- | --- | --- | --- |
| D1 – tjeneste/avsender | Godta Graph/Exchange, reserver faktisk adresse, avklar postbokstype/lisens, svaradresse og hvem som følger opp NDR/karantene. | Produkteier og M365-tjenesteeier | Åpen |
| D2 – teknisk identitet | Godta kjøring i webprosessen eller velg separat vert. Registrer tenant-ID, principal-ID og client-ID samt avsenderpostboksens objekt-ID. | Azure-/Entra-tjenesteeier | Åpen |
| D3 – autoritativ tilgang | Fastsett ressurs-tenant, gruppe-/attributtmapping og ekstern identitetsbinding. Godta kilde og reell forsinkelsesgrense for tilbakekalling, eller krev en sterkere sperrekilde. | Entra-tjenesteeier og sikkerhetsansvarlig | Åpen |
| D4 – drift | Godta Always On, kapasitet, klokkeslett, stoppmekanisme, feilhåndtering, retensjon, overvåking og navngitte operatører. | Driftsansvarlig og produkteier | Åpen |
| D5 – miljøaksept | Signer bevis for begrenset avsender, tilgangstap uten innlogging, faktisk mottak, gjenopptakelse og stans. | Tjenesteeiere og produkteier | Ikke utført; EV12–EV14 |

EV01 kan leveres som undersøkelse med disse åpne portene synlig registrert. Dokumentet lukker ingen avhengighet på egen hånd. Før [EV12/#122](https://github.com/depmartin/Budsjettkalender/issues/122) og [EV13/#123](https://github.com/depmartin/Budsjettkalender/issues/123) starter, må deres øvrige ticketavhengigheter og relevante D1–D4-beslutninger være dokumentert. Manglende faktisk miljøtilgang registreres som en konkret blokkering for miljøprøven. [EV14/#124](https://github.com/depmartin/Budsjettkalender/issues/124) krever samlet miljøbevis før produksjonsklarhet kan vurderes.

## 2. Påvist utgangspunkt og alternativer

Repoet har .NET/Blazor, EF Core, Azure SQL, Entra-innlogging, systemtilordnet identitet og Key Vault-provider. [Bicep-malen](../../infra/main.bicep) angir Linux/B1 som standard, men setter ikke `alwaysOn` og etablerer ingen e-postpostboks, Graph-rettighet, Exchange-rolle eller utsendingsjobb. Malens `subscription().tenantId` gjelder Azure-ressursen og skal ikke antas å være innloggingens ressurs-tenant. En mal er heller ikke bevis for faktisk utrulling. [Gjeldende status](../status.md) og [driftsveiledningen](../../infra/README.md) skiller lokale prøver fra Azure/Entra/M365-bevis.

[Brukeroppslaget](../../backend/Aarshjul.Infrastructure/Brukere/BrukeroppslagTjeneste.cs) bygger i dag på innloggingsclaims, beholder manuelle medlemskap og nødadministrator og revaliderer automatisk administrator ved oppslag. Det er ikke en bakgrunnskilde for kontostatus. [ADR-0002](../adr/0002-godkjenning-og-synlighet.md), [ADR-0003](../adr/0003-tilgang-og-kalenderdeling.md) og [GLOSSARY](../../GLOSSARY.md) gjelder fortsatt: synlighet avgjøres på server, POL velges eksplisitt, og endringsforslag beholder synligheten. Internkalender og eksisterende tokenfeed inngår ikke i e-postvarslingen.

| Alternativ | Vurdering for denne leveransen |
| --- | --- |
| Graph/Exchange med egen postboks | Anbefalt kandidat: passer M365-konteksten og har dokumentert avgrensning og sendekontrakt. Krever at IT faktisk har Exchange Online og kan etablere avsenderen. |
| Departementets godkjente SMTP-relé | Reserve dersom IT krever denne veien. IT må da dokumentere tjenestens autentisering, avsender-/mottakeravgrensning, transportvern, kvitteringer og uavklart utfall. Ingen antakelse om tilgjengelig SMTP eller passordbasert innlogging. |
| Annen transaksjonstjeneste, eksempelvis Azure Communication Services Email | Krever et eget leverandørvalg med domeneoppsett, databehandling, nettverk og leveringskontrakt. Ingen slik tjeneste er påvist; gir ingen grunn til å utvide EV01 til etablering. |
| Delegerte Graph-tokens fra siste innlogging | Avvises som driftskandidat: jobbens autoritet og levetid skal være uavhengig av en enkelt innlogget bruker. |

Anbefalingen er en prosjektvurdering. Den dokumenterer ikke at departementets faktiske driftsmiljø støtter kandidaten. Microsofts relevante tjenestekontrakter er kontrollert i primærkildene ved påstandene nedenfor.

## 3. Avsender, autentisering og minste rettigheter

Jobben bruker eksplisitt valgt `ManagedIdentityCredential` for produksjonsidentiteten og Graph-ressursen `https://graph.microsoft.com/.default`; ingen automatisk kjede til utviklerens konto. Managed identity kan kalle Graph som applikasjon uten lagret klienthemmelighet. Katalogrettighetene til Graph må gis til identitetens service principal; Azure RBAC for SQL/Key Vault gir dem ikke. Senderettigheten etableres separat i Exchange som angitt nedenfor, uten en parallell Entra-tildeling av `Mail.Send`. Dette bygger på [Microsofts App Service/Graph-veiledning](https://learn.microsoft.com/en-us/entra/identity-platform/multi-service-web-app-access-microsoft-graph-as-app) og [veiledningen for å gi API-rettigheter til managed identity](https://learn.microsoft.com/en-us/powershell/entra-powershell/grant-api-permissions-managed-identity?view=entra-powershell).

| Behov | Foreslått rettighet og avgrensning | Uttrykkelig grense |
| --- | --- | --- |
| Sende e-post | Exchange `Application Mail.Send`, bundet til navngitt service principal og et resource scope som bare treffer avsenderpostboksen. | Ingen organisasjonsvid `Mail.Send`-tildeling i Entra. Ingen `Mail.ReadWrite`, full Exchange-tilgang eller rutinemessig lesing av andre postbokser. |
| Lese kontostatus | Graph application `User.Read.All` i ressurs-tenant, direkte brukeroppslag med minst mulig `$select`. | Ingen skrivetilgang, `User.EnableDisableAccount.All`, katalogadministratorrolle, `Directory.Read.All` eller `AuditLog.Read.All` som standard. |
| Lese transitive gruppemedlemskap | Samme `User.Read.All` ved `/users/{oid}/transitiveMemberOf`. Sammenlikn gruppe-ID-er, uten å hente navn og annet gruppeinnhold. | `Group.Read.All`/`GroupMember.Read.All` er ikke automatisk et tillegg; velges bare hvis en annen dokumentert operasjon faktisk krever det. |
| Skjulte medlemskap | `Member.Read.Hidden` bare dersom en relevant tilgangsgruppe har skjult medlemskap og IT godkjenner innsynet. | Manglende tillatelse skal ikke tolkes som et fullstendig medlemskapsbevis. |

Minste application-rettighet for [Get user](https://learn.microsoft.com/en-us/graph/api/user-get?view=graph-rest-1.0) er `User.Read.All`. `accountEnabled` må velges uttrykkelig, jf. [user-egenskapene](https://learn.microsoft.com/en-us/graph/api/resources/user?view=graph-rest-1.0). Dokumentasjonens kombinasjon for å *lese og skrive* kontostatus er ikke begrunnelse for å tildele skriverettigheter til denne lesejobben. Det faktiske `accountEnabled`-svaret må prøves med lesetilgangen i EV12.

[transitiveMemberOf-kontrakten](https://learn.microsoft.com/en-us/graph/api/user-list-transitivememberof?view=graph-rest-1.0) tillater `User.Read.All` uten brukerinnlogging. Utilgjengelige objekttyper kan returneres med bare type og ID; dette er tilstrekkelig for en konfigurert gruppe-ID. Skjulte medlemskap trenger ekstra rettighet. Dokumentasjonen advarer om utdatert indeks ved avanserte spørringer. Kandidaten henter den komplette, paginerte relasjonen uten søk, filter eller OData-cast og behandler gruppetypene lokalt.

**Exchange- og Entra-tildelinger er additive.** Et avgrenset Exchange-scope innskrenker ikke en parallell bred Entra-`Mail.Send`. Exchange-testkommandoen undersøker heller ikke slike Entra-tildelinger, og dens test omgår Exchange-rettighetscache. Endringer kan bruke 30 minutter–2 timer i Exchange. Dette følger [Application RBAC-kontrakten](https://learn.microsoft.com/en-us/exchange/permissions-exo/application-rbac). IT skal derfor inventere begge tildelingsflatene og prøve ekte Graph-kall både innenfor og utenfor scope etter propagasjon. En vellykket simulering alene er utilstrekkelig bevis.

Avsenderen identifiseres med fast postboks-ID i `/users/{avsender-id}/sendMail`; ingen klientoppgitt `from`. Scope skal enumereres og gi nøyaktig én postboks. En egen, IT-forvaltet postboks foreslås; IT avgjør om den opprettes som delt postboks og hvilke lisens-/lagringsvilkår som gjelder. Interaktiv innlogging for avsenderkontoen skal sperres etter godkjent M365-oppsett. Svar og returmeldinger skal følges opp av en utpekt operatør. Kandidaten beholder sendMail-standardens kopi i Sent Items til manuell avklaring, med særskilt bevaring ved D4.

`User.Read.All` er en bred leserettighet i katalogen selv om applikasjonen bare skal slå opp egne abonnenter. Exchange-scope innskrenker ikke denne katalogtilgangen. IT må godkjenne innsynet; et smalere IT-forvaltet identitetsoppslag er alternativet dersom denne retten ikke kan gis til webprosessen.

## 4. Autoritativ tilgang uten innlogging

### Identitetsbinding og oppslagsrekkefølge

Abonnementet må bindes til **ressurs-tenant-ID og brukerens objekt-ID i denne tenant**. `sub`, e-post, visningsnavn og gjestens `#EXT#`-UPN skal ikke gjettes om til en Graph-objekt-ID. Dagens oppslag har reserve til `NameIdentifier`/`sub`; eksisterende brukere uten entydig binding må få status `IdentitetUavklart` og ny verifisert binding før varsling. E-postbekreftelse viser disposisjon over adressen og gir ingen funksjonsrolle eller synlighetsgruppe.

Forslag til kontrakt for hvert sendeforsøk:

1. Kontroller stoppbryter og abonnementets gjeldende versjon, adressebekreftelse, aktivering, utvalg, avmelding og administratorstans.
2. Hent `/v1.0/users/{oid}?$select=id,accountEnabled,userType,externalUserState` fra avtalt ressurs-tenant. Bare et fullført svar med riktig ID og `accountEnabled=true` kan brukes. Egenskapene for ekstern bruker bidrar til klassifisering, men erstatter ikke ITs identitetsbinding.
3. Hent hele `/v1.0/users/{oid}/transitiveMemberOf`, følg alle `@odata.nextLink` på forventet Graph-vert, og sammenlikn bare objekt-ID-er av gruppetype med ITs godkjente mapping. Feil på en side gjør hele grunnlaget uavklart. Et komplett, vellykket tomt resultat betyr ingen Entra-medlemskap; manglende resultat betyr feil.
4. Avklar ekstern kontostatus som beskrevet nedenfor. Kombiner deretter ferske, godkjente Entra-funn med gjeldende manuelle medlemskap og funksjonsrolle fra appdatabasen. Automatisk SBR-administrator må bekreftes på nytt. Manuell nødadministrator bevares, men omgår aldri kravet om aktiv, entydig konto.
5. Anvend eksisterende sentrale synlighetsregel på mottakerens kontekst før tittel, dato eller endringsinnhold bygges for e-postadapteren. Arbeidsidentitetens rettigheter inngår aldri i mottakerrollen. Eksisterende frister bruker gjeldende synlighet; slettingshendelser bruker siste publiserte synlighet sammen med mottakerens revaliderte rettigheter.
6. Les abonnement/stopp/synlighet på nytt umiddelbart før ekstern overlevering, innenfor en konkurransebeskyttet forsøksoperasjon. Ved endring bygges innholdet på nytt eller forsøket stanses. Et nytt forsøk krever nye oppslag, også etter tjenestefeil.

Gruppemappingen må beholde skillet mellom Entra-utledede og manuelle medlemskap. Eksisterende medlemskap i en deaktivert synlighetsgruppe kan bevares når autoritativt medlemskap fortsatt gjelder; deaktivering tillater ingen nye tildelinger. Bortfalt Entra-medlemskap fjernes. POL mappes aldri automatisk. Gjeldende administrator har likevel `SerAlt`, også for POL-frister; dokumentet endrer ikke denne eksisterende leseregelen. Dersom dagens `KildeClaimType` gjelder et annet attributt enn gruppe-ID-er, må IT angi den konkrete betrodde katalogegenskapen og leseoperasjonen. Det er en åpen D3-beslutning, ikke en tillatelse til å oversette tekst eller e-postdomene til tilgang.

### Ferskhet og tilbakekalling

Foreslått applikasjonsgrense er **nye oppslag for hvert sendeforsøk, fullført høyst 60 sekunder før overlevering**, med både start- og sluttid registrert. Venting, mange sider eller nytt sendeforsøk som overskrider grensen krever nytt oppslag. Ingen positiv cache fra siste innlogging eller tidligere døgn brukes. Tallet er et driftsforslag; det er ingen garanti for at katalogdata er yngre enn 60 sekunder.

Microsoft beskriver asynkron replikering til sekundærreplikaene som betjener kataloglesing, og oppgir at application-only-kall ikke får sesjonskonsistens, jf. [Entra-arkitekturen](https://learn.microsoft.com/en-us/entra/architecture/architecture). **Et nytt Graph-svar gir derfor ikke bevis for øyeblikkelig tilbakekalling.** D3 må fastsette hvilket tilbakekallingskrav som gjelder og hvilke faktiske kilder som kan oppfylle det. Miljøprøver skal måle tilgangstap fra registrert endring til at adapteren stanser innhold, uten ny innlogging. Målte tider er prøvebevis og skal ikke fremstilles som Microsofts maksimale forsinkelse.

Dersom IT krever umiddelbar sperring, foreslås en IT-forvaltet sperrekilde som inngår i samme beslutning før hvert forsøk, eventuelt global stans før katalogendringer. En slik kilde er ikke påvist eller bestilt implementert i EV01. Uten godkjent ferskhetsmodell og operatørprosedyre holdes fristutsending av. Ved kjent pågående synkroniseringsfeil eller ukjent ferskhet skal en mottaker utsettes, selv om et tidligere oppslag ga tilgang.

### Eksterne brukere og hjemmetenant

En B2B-bruker har et eget objekt i ressurskatalogen, og eksterne identiteter kan være både `Guest` og `Member`, jf. [Microsofts B2B-egenskaper](https://learn.microsoft.com/en-us/entra/external-id/user-properties). Derfor kan verken `userType=Member`, akseptert invitasjon eller ressursobjektets `accountEnabled=true` alene bevise at hjemmekontoen fortsatt er aktiv. `externalUserState=Accepted` er en invitasjonstilstand, ikke en løpende aktivkontokontroll.

Anbefalt kandidat for eksterne brukere er **ressursoppslag kombinert med et betrodd, IT-forvaltet oppslag av hjemmekontoens status**. Hver ekstern abonnent trenger en verifisert binding mellom ressursens tenant/objekt-ID og hjemmets tenant/objekt-ID. Kilden skal returnere aktiv/inaktiv/uavklart, observasjonstid og kildeidentitet. Manglende binding, utilgjengelig kilde eller for gammelt resultat gir utsatt sending uten fristinnhold.

IT må velge og prøve én av disse veiene per berørt tenant:

| Vei | Nødvendig etablering og vurdering |
| --- | --- |
| Betrodd hjemmekontooppslag, anbefalt | Hjemmetenantens tjenesteeier tilbyr en godkjent identitetstjeneste, eventuelt eget app-only Graph-oppslag av den bundne hjemmekontoen med `User.Read.All`. Tjenesten må godkjenne bindingen og ferskhetsmodellen. Ressursens grupper avgjør fortsatt appens tilgang. |
| Overvåket cross-tenant-synkronisering | Krever dokumentert kilde, attributtmapping, deprovisjonering og feilovervåking, med en akseptert øvre driftsgrense for forsinkelse. En ordinær B2B-invitasjon er utilstrekkelig. Kan bare velges hvis D3 uttrykkelig godtar den svakere ferskhetsmodellen. |

Cross-tenant-synkronisering kan overføre sperret konto fra kilde til mål. Microsoft oppgir startintervall på 40 minutter og varierende kjøretid; brutt synkroniseringsforhold rydder ikke automatisk bort eksisterende eksterne brukere, jf. [synkroniseringskontrakten](https://learn.microsoft.com/en-us/entra/identity/multi-tenant-organizations/cross-tenant-synchronization-overview). Dette gir ikke alene en kjent maksimal forsinkelse eller tilstrekkelig bevis ved utsending. **Ingen ekstern bruker får fristinnhold før IT har valgt og prøvd en godkjent kilde.** Dette er en synlig miljøblokkering, ikke en utelatelse av gjestekravet i #110.

Managed identity støtter ikke direkte tilgang på tvers av kataloger, jf. [Microsofts managed identity-FAQ](https://learn.microsoft.com/en-us/entra/identity/managed-identities-azure-resources/managed-identities-faq). Appens lokale identitet skal derfor ikke antas å kunne lese alle hjemmetenanter. Eventuelle hjemmetenantapper, tillatelser, sertifikat-/fødereringsvalg og tjenesteavtaler krever egen IT-etablering. De opprettes ikke her.

### Resultater som stanser innhold

| Funn | Observerbar driftsstatus og handling |
| --- | --- |
| Konto deaktivert/slettet, eller kjent sperre | `UtsattTilgang`: ingen overlevering av fristinnhold. Behold årsak og hendelser for kontrollert oppfølging. Ingen automatisk omgjøring til aktiv konto. |
| `accountEnabled=null`, manglende felt, ID-/tenant-avvik eller ikke fullført svar | `IdentitetUavklart`: ingen antakelse om aktiv bruker eller tom gruppe. |
| Manglende tillatelse, 401/403, 429, timeout, kildesvikt, ufullstendig paginering eller ukjent ferskhet | `UtsattIdentitetskilde`: utsett mottakeren; systematiske feil stanser utsending globalt og varsles til drift. |
| Komplett medlemskapsresultat uten relevante grupper | Bruk bare gyldige manuelle rettigheter og revalidert funksjonsrolle. Hvis intet innhold er synlig, sendes ingen tom oppsummering. |
| Bortfalt automatisk administrator | Ingen videre bruk av gammel `SerAlt`; vurder utvalget på nytt med gjeldende mottakerrettigheter. |

## 5. Sendestatus, levering og nye forsøk

[Graph `sendMail`](https://learn.microsoft.com/en-us/graph/api/user-sendmail?view=graph-rest-1.0) returnerer `202 Accepted` uten responsinnhold når forespørselen er akseptert. Det bekrefter ikke fullført behandling eller levering. Endpointet tilbyr ingen dokumentert idempotensnøkkel i denne kontrakten. Et eget korrelasjonsfelt kan hjelpe feilsøking, men skal ikke fremstilles som leverandørdeduplisering.

| Tilstand | Betydning og overgang |
| --- | --- |
| `Planlagt` | Varig oppsummering med intern ID, abonnementsversjon, lokal dato og hendelsesavgrensning. Ingen ekstern overlevering. |
| `UnderBehandling` | Én arbeidstaker har varig lease/forsøk. Krasj etter mulig overlevering gir `Uavklart`, også når leasen utløper. |
| `Utsatt` | Stoppbryter, tilgangskontroll eller sikker venting hindrer forsøk. Hendelser beholdes. |
| `AkseptertAvTjenesten` | Et fullstendig `202`-svar er varig registrert. Samme oppsummering gjentas ikke. Hendelser merkes som overlevert, aldri som observert levert. |
| `Feilet` | Entydig avvisning eller sikkert feil før overlevering. Årsak og eventuell neste forsøksdato registreres; permanent feil krever operatør. |
| `Uavklart` | Timeout, avbrutt forbindelse, prosesskrasj eller lagringsfeil kan ha skjedd etter at tjenesten tok imot innhold. Ingen automatisk ny sending eller automatisk flytting av de samme hendelsene til neste døgn. |
| `ObservertMottak` | Separat bevisopplysning fra kontrollert mottakers postboks, med tidspunkt og referanse. Endrer ikke sendekontrakten for andre meldinger. |

Status lagres varig ved hvert forsøk. `AkseptertAvTjenesten` kan få senere NDR-/sporingsbevis om avvisning. Den skal ikke overskrives til «aldri sendt» og gis et automatisk nytt forsøk. Microsoft beskriver transport, policyvurdering og mulige returmeldinger etter at Graph-kallet er avsluttet, jf. [sendeforløpet](https://learn.microsoft.com/en-us/graph/outlook-things-to-know-about-send-mail).

Foreslåtte forsøksgrenser er høyst tre automatiske forsøk per oppsummering per lokal dag ved **entydig gjenforsøkbar avvisning**, eksempelvis fullstendig 429-svar. [Graph krever venting etter `Retry-After`](https://learn.microsoft.com/en-us/graph/throttling); uten denne foreslås ett og deretter fem minutters venting med variasjon. Lengre leverandørventing respekteres selv om dagsvinduet passeres. Mislykkede, sikkert usendte oppsummeringer kan tas med ved neste dags gjenopptakelse, uten en ny parallell plan for de samme hendelsene.

Ikke la SDK-, proxy- og applikasjonslag sende samme POST flere ganger gjennom uavhengige retryregler. EV13 skal prøve adapterens faktiske HTTP-forløp, inkludert eventuell innebygget SDK-retry. 5xx, nettverksbrudd og timeout etter at POST kan være sendt behandles konservativt som `Uavklart`. 400/401/403 gir operatørfeil fremfor blind retry; legitim fornyelse av token alene er ikke tillatelse til å gjenta en mulig akseptert sending.

Ved `Uavklart` undersøker M365-operatøren avsender, mottaker, UTC-tidsrom og intern forsøksreferanse i [Exchange message trace](https://learn.microsoft.com/en-us/exchange/monitoring/trace-an-email-message/message-trace-modern-eac), Sent Items og returmeldinger. Fravær i sporingen er ikke alene bevis for at forespørselen aldri ble mottatt. Operatøren registrerer funn og velger videre venting, bekreftet overlevering eller uttrykkelig ny sending med kjent dublettrisiko. Ny sending får nytt forsøksnummer og full tilgangs-/stanskontroll. Appen gis ikke `Mail.Read` bare for denne manuelle undersøkelsen.

En post i Sent Items eller en transportkvittering dokumenterer sin del av kjeden. EV13 krever at kontrollert mottaker faktisk finner bekreftelsesmeldingen og fullfører adressebekreftelsen. EV14 krever også observert oppsummering med forventet innhold. Ingen generell garanti om nøyaktig én ekstern melding eller lest e-post gis.

## 6. Kjøreform, Europe/Oslo og gjenopptakelse

Kandidaten bruker én applikasjonsoperasjon for planlegging og utsending. `BackgroundService` er en støttet modell i ASP.NET Core, jf. [hosted services](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-10.0). En minuttvis vekking undersøker forfalt arbeid; lokal dato og standard kl. **08.00 Europe/Oslo, alle dager**, beregnes fra en kontrollerbar klokke og tidssonens regler. Ingen fast UTC-forskyvning eller 24-timers intervall brukes som døgnplan.

Foreslått driftskontrakt:

- Bruk Linux-tidssonen `Europe/Oslo`; verifiser at publisert image har nødvendige tidssonedata. Vinterkjøring kl. 08.00 tilsvarer kl. 07.00 UTC, sommerkjøring kl. 06.00 UTC. Prøv begge DST-overganger, også hvis klokkeslett senere konfigureres i en tvetydig eller manglende time. Forslag ved slikt klokkeslett er første gyldige tidspunkt etter et sprang og første forekomst ved gjentatt time, med én plan per lokal dato.
- Plan-ID beskyttes med unik nøkkel for abonnent/lokal dato, versjonerte overganger og varig lease. Samtidige instanser og manuell gjenopptakelse bruker samme operasjon og koordinering. Databasens koordinering skal også beskytte selve forsøksstarten; en utløpt lease er ikke tillatelse til å gjenta `Uavklart` POST.
- Oppstart før kl. 08.00 venter til dagens tidspunkt. Oppstart etter tidspunktet behandler forfalt, sikkert usendt arbeid i én samlet oppsummering. Etter flere døgns nedetid samles ventende hendelser frem til gjenopptakelsens avgrensning; det sendes ingen historisk melding for hvert mistet døgn. Gjenopptakelse og dagens nye arbeid samordnes slik at gamle plan-ID-er ikke gir flere aksepterte oppsummeringer til samme abonnent på faktisk lokal sendingsdato. Nye/utvidede abonnementer får fortsatt ingen historisk tilbakefylling.
- Etter dagens aksepterte oppsummering går nye hendelser til neste dag. En ventende `Uavklart` oppsummering holder sine hendelser uten å blokkere lagring av nye hendelser. Ingen hendelser merkes som levert ved planlegging, ukjent sendeforløp eller kildesvikt.
- Behold transaksjonsgrensene fra [ADR-0006](../adr/0006-operasjonsavgrenset-dbcontext.md) og konkurranseprinsippene fra [ADR-0007](../adr/0007-konkurransekontroll-ved-godkjenning.md). Publisering og varslingshendelse lagres atomisk; ekstern sending skjer utenfor publiseringstransaksjonen.

Always On må etableres og prøves i valgt plan. Uten Always On kan App Service lastes ut etter inaktivitet, jf. [App Service-innstillingene](https://learn.microsoft.com/en-us/azure/app-service/configure-common?tabs=portal#configure-general-settings). Always On er ingen garanti mot utrulling, omstart eller feil; varig innhenting etter nedetid er derfor nødvendig.

Alternative jobbverter beholdes som beslutningsmuligheter ved D2:

| Kandidat | Konsekvens og beslutningsgrunnlag |
| --- | --- |
| Planlagt .NET WebJob | Støttes nå også på Linux. Microsofts veiledning krever Always On og angir `WEBSITE_SKIP_RUNNING_KUDUAGENT=false` for Linux. Dette må prøves mot faktisk App Service-image og utrullingsform, jf. [planlagt WebJob](https://learn.microsoft.com/en-us/azure/app-service/quickstart-webjobs) og [WebJob-drift](https://learn.microsoft.com/en-us/azure/app-service/webjobs-create). Mer pakking/utrulling enn hovedkandidaten; samme databaselås og tilgangskontroll kreves. |
| Azure Functions timer | Egen vert og identitet kan gi ønsket prosessgrense, men krever plan, lagring, nettverk og driftsoppsett. Linux Premium/Dedicated støtter `WEBSITE_TIME_ZONE=Europe/Oslo`; Linux Consumption/Flex støtter ikke innstillingen. En UTC-basert minuttvekking med Oslo-beregning i applikasjonen er et alternativ. Timeren gir ikke automatisk omkjøring etter feil; varig gjenopptakelse er fortsatt nødvendig, jf. [timer-kontrakten](https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-timer). |

Ingen ny scheduler, WebJob eller Function opprettes i EV01. En jobbvert skal ikke eksponere et anonymt HTTP-endepunkt som kan utløse e-post.

## 7. Nettverk, konfigurasjon og stopp

Følgende er nødvendige konfigurasjonsopplysninger, ikke ferdige konfigurasjonsnøkler: ressurs-/avsendertenant, valgt principal, avsender-ID/adresse, offentlig HTTPS-basisadresse, daglig tidspunkt/tidssone, gruppe-/attributtmapping, identitetskildeferskhet, forsøks- og bekreftelsesgrenser, kontrollerte testmottakere og stoppbryter. Basisadressen skal være en fast godkjent origin; brukerinnsendt Host/URL skal ikke bestemme bekreftelses-, frist- eller avmeldingslenker.

Azure-/nettverksoperatøren må dokumentere og prøve DNS og utgående TLS/443 mot Graph og nødvendige Entra-/identitetsendepunkter i valgt sky. Lokalt managed identity-endepunkt må fungere. Azure SQL, Key Vault og telemetri trenger sitt eksisterende, særskilt godkjente nettverk; hjemmekontotjenesten trenger et eksplisitt tillegg. Graph-veien krever ingen SMTP-port. Faktiske vertsnavn, proxy/TLS-policy og eventuell privat nettverksføring må registreres av IT, ikke utledes av at utviklermaskinen kan lese offentlig dokumentasjon.

Managed identity er hovedvalget uten ny e-posthemmelighet. Eventuell godkjent sertifikatbasert reserve krever navngitt eier, utløpsvarsel, rotasjon og Key Vault, etter [eksisterende kildeprioritet og oppstartsgrense](../../infra/README.md#key-vault-ved-weboppstart). Providerens verdier leses ved oppstart og har høyere prioritet enn miljøvariabler; stoppbryteren må derfor ikke kunne overstyres av en gammel hvelvverdi.

**Stoppkandidat:** en varig global sendebryter, kontrollert før hvert eksternt kall, standard av ved manglende/ugyldig status. Av/på skal ha tilgangsvern og revisjonsspor. En separat oppstartsinnstilling må også tillate sending; begge må være på. Staging, lokal demo og CI tillates bare kontrollert testmottaker og ingen produksjonsfristdata. Produksjonsstans skal omfatte både oppsummering og bekreftelsespost; kalenderlesing og publisering skal fortsatt virke.

Foreslått effektgrense er høyst ett minutt til nye forsøk stanser, prøvd med to instanser. Pågående POST kan allerede være overlevert og kan ikke tilbakekalles. Den gjenværende konkurransegrensen skal beskrives og måles i EV14. Nettverks-/policybasert e-poststans kan være en IT-reserve; den skal vurderes for påvirkning på identitetsoppslag og kalenderen. Fjerning av RBAC/Graph-rettigheter er ikke en øyeblikkelig stoppmekanisme, blant annet på grunn av [identitets- og rettighetscache](https://learn.microsoft.com/en-us/entra/identity/managed-identities-azure-resources/managed-identities-faq).

## 8. Feilgrenser, oppfølging og retensjon

Foreslått startkapasitet er én samtidig sendeforespørsel og maksimalt 20 sendeforsøk per minutt. Dette er en forsiktig prosjektgrense, ikke dokumentasjon av tenantens Exchange-kvote. IT skal verifisere faktisk volum, kvoter, transportregler, avvisnings-/karantenebehandling og passende grenser før kontrollert prøve. Hver e-post har én mottaker uten felles CC/BCC; brukerinnhold vises som tekst i HTML-/tekstvariant uten å tolkes som HTML eller headere.

Foreslått overvåking og ansvar:

| Signal | Grense og handling | Foreslått operatør |
| --- | --- | --- |
| Jobbens livstegn | Manglende livstegn i 5 minutter undersøkes; forfalt arbeid uten fremdrift 15 minutter etter kl. 08.00 utløser avvik. | Azure-/applikasjonsdrift |
| 401/403 eller feil avsender/tenant | Stans nye sendeforsøk globalt umiddelbart, undersøk identitet/rettigheter; kalenderen fortsetter. | Entra-/M365-operatør |
| Kildesvikt/ukjent tilgang | Utsett berørte mottakere; tre påfølgende systemfeil gir global pause og driftsavvik. | Entra-/applikasjonsdrift |
| 429 | Respekter leverandørventing, reduser belastning; vedvarende feil eller uttømt forsøksgrense gir avvik. | Applikasjonsdrift, deretter M365 |
| Uavklart sending | Ingen automatisk ny sending. Operatør vurderer samme arbeidsdag. | M365-operatør |
| NDR/permanent adressefeil | Stans abonnementets videre sending med årsak og tidspunkt. Krev brukerretting/ny bekreftelse før videre fristpost; ikke bytt adresse automatisk. | Abonnementsadministrator med M365-operatør |
| Kø eldre enn 24 timer | Undersøk kilder, stopp og utfall; planlegg kontrollert gjenopptakelse uten å gjenta aksepterte meldinger. | Driftsansvarlig |

Monitorering må ha mottaker/vaktkanal som er uavhengig av den samme utgående e-postadapteren. EV01 oppretter eller sender ingen driftsmeldinger. Faktiske ansvarlige, stedfortreder, tjenestetid og eskaleringskanal må fylles inn ved D4; rollenavn i tabellen viser oppgavefordelingen og hevder ikke at en vaktordning finnes.

Logger begrenses til intern abonnements-/oppsummerings-/forsøks-ID, UTC-tid, lokal kjøredato, tilstand, fast feilkategori og egnet tjenestereferanse. Ingen fristtekst, adresser i ordinære logger, tokens, rå Graph-/Entra-respons, MIME, bekreftelses- eller avmeldingslenker. Tilgangsstyrt abonnementsadministrasjon viser adressen når oppgaven krever det. Rå SDK-/proxy-/telemetriopptak må kontrolleres særskilt; eksisterende identitetsloggvern er beskrevet i infra-veiledningen.

Foreslåtte retensjonsgrenser må godtas av data-/tjenesteeier før drift. De gjelder varslingens kopier og endrer ikke kalenderens originale frister eller øvrige bevaringsregler:

| Opplysninger | Foreslått frist og håndtering |
| --- | --- |
| Bekreftelseshemmelighet | Kun hash/egnet beskyttet representasjon. Utløp 24 timer; slett hemmelighetsmaterialet straks det er brukt, erstattet eller utløpt. Hemmelige testlenker inngår aldri i varig evidens. |
| Aktivt abonnement og bekreftet adresse | Behold mens abonnementet er aktivt. Inaktiv påmelding/avmelding ryddes etter 30 dager, med nødvendig minimalt revisjonsspor separat. |
| Ventende hendelser og nødvendige før-/ettermetadata | Behold mens de er sikkert ubehandlet eller i uavklart sending. Varsle etter 24 timer; operatøravgjørelse innen 30 dager. Ikke slett stilltiende eller merk som levert ved tidsutløp. Lengre hold krever begrunnet sluttdato. |
| Overleverte hendelseskopier/bygget e-post | Unngå varig lagring av full melding. Rydd nødvendige hendelseskopier senest 7 dager etter avklart overlevering når ingen ventende oppsummering trenger dem. |
| Forsøk, utfall og stansrevisjon | 90 dager med minste nødvendige metadata; rydd også eventuell kobling til slettet adresse. Ordinær telemetri 30 dager, i samsvar med malens Log Analytics-standard. |
| Avsenderpostboks, NDR, sporingsuttrekk og sikkerhetskopier | IT må fastsette egen, forenlig bevaring/sletting og tilgang i M365/backup. Appopprydding beviser ikke sletting i eksterne systemer eller hos mottakeren. |

## 9. Konkrete operatørsteg før miljøprøve

Stegene er en arbeidsordre til riktige operatører **etter separat autorisasjon**. De er ikke kjørt som del av EV01. Resultater dokumenteres med tidspunkt, miljø, principal/ressurs-ID, forsøksreferanse og bevisplassering uten hemmeligheter.

1. **Produkteier og tjenesteeiere:** Besvar D1–D4, utpek navngitte operatører og kontrollerte mottakere. Registrer godkjent tjeneste/avsender, offentlig origin og hvilke tenanter/identitetstyper første miljøprøve skal dekke. Godta retensjon og faktisk tilbakekallingsmodell.
2. **Azure-operatør:** Les faktisk App Service/plan/image, identitet, nettverk og slotoppsett. Dokumenter avvik fra repoets mal. Etabler Always On og nødvendig kapasitet i senere autorisert konfigurasjonsleveranse. Staging skal ikke arve aktiv produksjonsutsending ved slotbytte.
3. **Entra-operatør:** Verifiser riktig tenant og identitetens service principal-ID/client-ID. Tildel bare godkjent `User.Read.All` som application app role til denne principalen. Les tilbake den faktiske tildelingen. Operatørens rett til å gi Graph-roller skal ikke gis videre til applikasjonen; Microsofts [tildelingsveiledning](https://learn.microsoft.com/en-us/powershell/entra-powershell/grant-api-permissions-managed-identity?view=entra-powershell) beskriver særskilt privilegert administrasjon for førsteparts-API-er.
4. **Entra-/identitetsoperatør:** Registrer godkjente ressursgrupper/attributter, skjulte grupper og direkte/nestede medlemskap. Etabler og dokumenter autoritativ kilde for ekstern kontostatus og verifisert hjem-/ressursbinding. Prøv konto sperret, slettet, ukjent og endret medlemskap uten ny innlogging, også i hjemmetenant.
5. **M365-operatør:** Etabler egen avsenderpostboks og avklar lisens, transport, svar, NDR, karantene og lagring. Opprett et Exchange-scope som ved enumerering gir bare den avtalte postboksen. Registrer Exchange-pointer til riktig Entra service principal, tildel bare `Application Mail.Send` til scopet, og dokumenter begge sider av bindingen. Bruk identitetsobjektet, ikke appregistreringens objekt-ID.
6. **Entra-/M365-operatør sammen:** Inventer Graph-/Exchange-tildelinger og fjern eller avvis parallelle brede senderettigheter i den autoriserte etableringen. Dokumenter positiv og negativ `Test-ServicePrincipalAuthorization`, men stol ikke på den alene. Etter propagasjon prøves virkelig `sendMail` fra avsenderen og en separat kontrollpostboks utenfor scope, alltid til godkjent testmottaker uten fristdata. Utenfor-scope-forsøket skal avvises. Ikke test mot en uvedkommendes personpostboks.
7. **Nettverks-/sikkerhetsoperatør:** Prøv valgte endepunkter fra faktisk jobbvert. Kontroller token-/innholdsredigering i applikasjon, SDK, proxy, accesslogger og telemetri. Verifiser at offentlige lenker har riktig HTTPS-origin og at hemmelige lenker ikke lagres i evidens.
8. **Applikasjonsdrift og utvikler:** Gjennomfør EV13 med bare adressebekreftelse, EV12 med ekte tilgangskilde og EV14 med syntetiske frister/kontrollerte abonnenter. Prøv stopp før kall, omstart etter mulig POST og nedetidsinnhenting. Hold produksjonsbryteren av.
9. **Produkteier og tjenesteeiere:** Vurder D5 med faktiske funn og gjenstående grenser. Dokumenter hvem som kan aktivere, stanse og gjenoppta. Produksjonsaktivering og reell abonnentutsending krever egen uttrykkelig beslutning; EV01 eller vellykket CI autoriserer ikke dette.

## 10. Akseptprøver og hva de kan bevise

| Senere leveranse | Minste observerbare prøve og bevis |
| --- | --- |
| EV12/#122 – intern identitet | Ekte app-only oppslag viser aktiv konto og komplette direkte/nestede grupper. Fjern relevant medlemskap/SBR-rolle uten ny innlogging: adapteren får ikke skjult tittel, dato eller forklaring. Manuell rettighet/nødadministrator og eksplisitt POL følger eksisterende regler. |
| EV12/#122 – usikkerhet | Null/manglende kontofelt, 404, 403, sidefeil, skjult gruppe uten nødvendig tillatelse, kildetimeout og utløpt ferskhet gir synlig utsatt status og null fristinnhold til adapteren. En `id`-only gruppe må kunne matches når dette er tilstrekkelig. |
| EV12/#122 – eksterne identiteter | Ressurskonto aktiv mens hjemmekonto sperres, både ekstern Guest og ekstern Member. Manglende mapping/synkroniseringssvikt stanser sending. Registrer endringstid, kildeobservasjoner og siste mulige innholdsoverlevering. Uten riktig hjemmekilde er prøven blokkert, ikke bestått. |
| EV12/#122 – fristsynlighet | Endret gjeldende synlighet og sletting med siste publiserte synlighet vurderes mot revalidert mottaker. Jobbens tekniske rettigheter gir aldri `SerAlt`. |
| EV13/#123 – avsender og bekreftelse | Faktisk credential og RBAC sender bare fra avtalt postboks. Kontroller både scope og parallelle tildelinger. Mottaker finner melding fra riktig avsender på kontrollert adresse, åpner korrekt HTTPS-lenke som riktig bruker og fullfører adressebekreftelse. Ingen fristdata inngår. |
| EV13/#123 – sendekontrakt | Fullstendig 202, entydig avvisning, 429/venting og avbrudd etter mulig POST gir ulike varige utfall. Ingen automatisk replay av `Uavklart`; eventuell SDK-retry er undersøkt. Sporingsbevis, mottaksbevis og bekreftelseshandling oppgis separat. |
| EV14/#124 – planlegging og feil | To instanser, begge DST-overganger, dato-/årsskifte, omstart ved hver tilstand og flere døgns nedetid gir ingen tapte hendelser eller to planlagte oppsummeringer for samme lokale dato. Bekreftede overleveringer gjentas ikke; uavklart sendeforløp påstås ikke sikkert deduplisert. |
| EV14/#124 – brukerforløp og stans | Registrering, publisering fra alle veier, relevante utvalg, én oppsummering, faktisk mottak, adressebytte, avmelding, administratorstans og global stans observeres. Kontroller rettigheter igjen ved retry og ved stans mellom planlegging og POST. Kalenderen virker under e-poststans. |
| EV14/#124 – drift | Mål livstegn/avvik, feilgrenser, retensjonsopprydding, gjenopptakelse, slot-/omstartsvirkning og logger uten hemmeligheter. Vanlige, ekte SQL Server- og Chromium-porter samt påkrevd review/audit gjelder kodekandidaten. |

EV01 har kontrollert offentlig dokumentasjon og repoets dokument-/kildegrunnlag. Ingen Azure-innlogging, tenantinventering, rettighetsendring, postboksopprettelse eller prøveutsending er utført. Det foreligger derfor **ingen bekreftelse av faktisk tenanttilgang, avsender, gjestelivssyklus, Azure-kjøring eller levering**. Kildegrunnlaget gjør planen vurderbar; de åpne beslutningsportene og miljøprøvene angir hva som fortsatt må dokumenteres.
