using Aarshjul.Application;
using Aarshjul.Application.Brukere;
using Aarshjul.Application.Brukerforslag;
using Aarshjul.Application.Datautveksling;
using Aarshjul.Application.Frister;
using Aarshjul.Application.Godkjenningsko;
using Aarshjul.Application.Varsler;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Brukerforslag;
using Aarshjul.Infrastructure.Datautveksling;
using Aarshjul.Infrastructure.Frister;
using Aarshjul.Infrastructure.Godkjenningsko;
using Aarshjul.Infrastructure.Varsler;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Aarshjul.Testkontrakter;

/// <summary>Samme publiseringskontrakt kjøres mot SQLite lokalt og SQL Server i CI.</summary>
public abstract class GodkjenningskonkurranseKontrakt : Genereringskontrakt
{
    private static Task<long> Sekvens(AppDbContext db)
        => db.Database.SqlQueryRaw<long>("SELECT Verdi AS Value FROM Epostrekkefolge").SingleAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Endringsfeil_ruller_tilbake_justerte_felter_maal_innboks_hendelse_og_sekvens(bool juster)
    {
        await using var database = await OpprettDatabaseAsync();
        var forslagId = await LeggForslagAsync(database, true);
        // Varsel skrives etter både forslag, målfrist og hendelse. Triggeren beviser at
        // disse reelle skrivene og den ordnede sekvensen inngår i samme rollback.
        await using (var db = database.Fabrikk().CreateDbContext())
            await db.Database.ExecuteSqlRawAsync(db.Database.IsSqlServer() ? """
                CREATE TRIGGER TestfeilEndring ON Varsler AFTER INSERT AS
                BEGIN
                    SET NOCOUNT ON;
                    IF (SELECT COUNT(*) FROM Forslag WHERE Status = 1) = 1
                       AND (SELECT COUNT(*) FROM Frister WHERE Tittel <> 'Opprinnelig') = 1
                       AND (SELECT COUNT(*) FROM NyFristHendelser WHERE Type = 'Endret') = 1
                       AND (SELECT Verdi FROM Epostrekkefolge WHERE Id = 1) = 2
                        THROW 51001, 'Testfeil etter endringsskriv', 1;
                    ELSE THROW 51002, 'Testoppsett: endringsskriv mangler', 1;
                END
                """ : """
                CREATE TRIGGER TestfeilEndring AFTER INSERT ON Varsler BEGIN
                    SELECT CASE WHEN
                        (SELECT COUNT(*) FROM Forslag WHERE Status = 1) = 1 AND
                        (SELECT COUNT(*) FROM Frister WHERE Tittel <> 'Opprinnelig') = 1 AND
                        (SELECT COUNT(*) FROM NyFristHendelser WHERE Type = 'Endret') = 1 AND
                        (SELECT Verdi FROM Epostrekkefolge WHERE Id = 1) = 2
                    THEN RAISE(ABORT, 'Testfeil etter endringsskriv')
                    ELSE RAISE(ABORT, 'Testoppsett: endringsskriv mangler') END;
                END;
                """);
        IGodkjenningsko ko = new GodkjenningskoTjeneste(database.Fabrikk());
        var feil = await Assert.ThrowsAsync<DbUpdateException>(() => GodkjennAsync(ko, forslagId, juster));
        Assert.Contains("Testfeil etter endringsskriv", feil.InnerException?.Message);
        await using (var db = database.Fabrikk().CreateDbContext())
        {
            var forslag = await db.Forslag.SingleAsync(); Assert.Equal(FristStatus.Forslag, forslag.Status);
            Assert.Equal("Budsjettfrist", forslag.Tittel); Assert.Equal(new DateOnly(2027, 3, 15), forslag.Dato);
            Assert.Equal("Opprinnelig", (await db.Frister.SingleAsync()).Tittel);
            Assert.Equal(new DateOnly(2027, 2, 1), (await db.Frister.SingleAsync()).Dato);
            Assert.Equal(new[] { "FA", "POL" }, await db.FristSynlighet.OrderBy(x => x.GruppeKode).Select(x => x.GruppeKode).ToArrayAsync());
            Assert.Empty(await db.Varsler.ToListAsync()); Assert.Single(await db.NyFristHendelser.ToListAsync());
            Assert.Equal(1, await Sekvens(db));
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER TestfeilEndring");
        }
        await GodkjennAsync(ko, forslagId, juster); // Ny operasjonskontekst på samme tjeneste.
        await using var etter = database.Fabrikk().CreateDbContext();
        Assert.Equal(juster ? "Justert frist" : "Budsjettfrist", (await etter.Frister.SingleAsync()).Tittel);
        Assert.Single(await etter.Varsler.ToListAsync()); Assert.Equal(2, await etter.NyFristHendelser.CountAsync());
        Assert.Equal(2, await Sekvens(etter));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Samtidig_maalredigering_avviser_gammel_foertilstand_uten_ekstra_koehendelse(bool juster)
    {
        await using var database = await OpprettDatabaseAsync(); var forslagId = await LeggForslagAsync(database, true);
        var port = new Lagringsport(); IGodkjenningsko ko = new GodkjenningskoTjeneste(database.Fabrikk(port));
        var ventende = GodkjennAsync(ko, forslagId, juster);
        try
        {
            await port.Ankommet.WaitAsync(TimeSpan.FromSeconds(20));
            Guid maal; await using (var db = database.Fabrikk().CreateDbContext()) maal = (await db.Frister.SingleAsync()).Id;
            IFristskriving skriv = new FristskrivingTjeneste(database.Fabrikk());
            var input = (await skriv.HentForRedigeringAsync(maal))!; input.Dato = new(2027, 6, 1);
            await skriv.OppdaterAsync(maal, input); port.Slipp();
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => ventende.WaitAsync(TimeSpan.FromSeconds(20)));
            Assert.Single(await ko.HentKoAsync());
            await using var kontroll = database.Fabrikk().CreateDbContext();
            Assert.Equal(new DateOnly(2027, 6, 1), (await kontroll.Frister.SingleAsync()).Dato);
            Assert.Empty(await kontroll.Varsler.ToListAsync()); Assert.Equal(2, await kontroll.NyFristHendelser.CountAsync());
            Assert.Equal(2, await Sekvens(kontroll));
        }
        finally { port.Slipp(); try { await ventende; } catch { /* Pågående operasjon frigjøres. */ } }
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    public async Task Ny_innsending_kan_ikke_behandles_av_en_gammel_operasjon(bool juster, bool avvis, bool endringsforslag)
    {
        await using var database = await OpprettDatabaseAsync();
        var forslagId = await LeggForslagAsync(database, endringsforslag);
        var port = new Lagringsport();
        IGodkjenningsko gammelAdministrator = new GodkjenningskoTjeneste(database.Fabrikk(port));
        IGodkjenningsko administrator = new GodkjenningskoTjeneste(database.Fabrikk());
        IForslagsinnsending innsending = new ForslagsinnsendingTjeneste(database.Fabrikk());
        IFristDatautveksling eksport = new FristDatautveksling(database.Fabrikk(), TimeProvider.System);
        IVarseltjeneste varsler = new Varseltjeneste(database.Fabrikk());
        var foer = await eksport.EksporterAsync();
        Task gammelBehandling = avvis
            ? gammelAdministrator.AvvisAsync(forslagId)
            : GodkjennAsync(gammelAdministrator, forslagId, juster);

        try
        {
            await port.Ankommet.WaitAsync(TimeSpan.FromSeconds(20));
            await administrator.AvvisAsync(forslagId, "Må revideres");
            var revidert = await innsending.HentForRedigeringAsync(forslagId, "innsender");
            Assert.NotNull(revidert);
            revidert.Tittel = "Revidert budsjettfrist";
            revidert.Dato = new DateOnly(2027, 5, 20);
            await innsending.SendInnPaaNyttAsync(forslagId, revidert,
                new GjeldendeBruker("innsender", "Innsender", Funksjonsrolle.Bidragsyter, ["FA"]));

            port.Slipp();
            var feil = await Assert.ThrowsAsync<Valideringsfeil>(() => gammelBehandling.WaitAsync(TimeSpan.FromSeconds(20)));
            Assert.Contains("Oppdater godkjenningskøen", feil.Message);
            var aapent = Assert.Single(await administrator.HentKoAsync());
            Assert.Equal(forslagId, aapent.Id);
            Assert.Equal("Revidert budsjettfrist", aapent.Tittel);
            Assert.Equal(new DateOnly(2027, 5, 20), aapent.Dato);
            Assert.Equal(FristStatus.Forslag, Assert.Single(await innsending.HentMineAsync("innsender")).Status);
            var etter = await eksport.EksporterAsync();
            Assert.Equal(foer.Frister.Select(f => (f.Id, f.Tittel, f.Dato)), etter.Frister.Select(f => (f.Id, f.Tittel, f.Dato)));
            if (endringsforslag)
                Assert.Equal(new[] { "FA", "POL" }, Assert.Single(etter.Frister).SynligFor.OrderBy(k => k));
            var varsel = Assert.Single(await varsler.HentForBrukerAsync("innsender"));
            Assert.Equal("Forslaget ditt er avvist.", varsel.Tekst);
            Assert.Equal("Må revideres", varsel.Begrunnelse);
            await using (var kontroll = database.Fabrikk().CreateDbContext())
                Assert.Equal(endringsforslag ? 1 : 0, await kontroll.NyFristHendelser.CountAsync());

            // Samme tjeneste kan lese den nye innsendingen og publisere det reviderte innholdet.
            await GodkjennAsync(gammelAdministrator, forslagId, false);
            var publisert = Assert.Single((await eksport.EksporterAsync()).Frister);
            Assert.Equal("Revidert budsjettfrist", publisert.Tittel);
            Assert.Equal(new DateOnly(2027, 5, 20), publisert.Dato);
            Assert.Equal(FristStatus.Godkjent, Assert.Single(await innsending.HentMineAsync("innsender")).Status);
            Assert.Equal(2, (await varsler.HentForBrukerAsync("innsender")).Count);
            await using var slutt = database.Fabrikk().CreateDbContext();
            var hendelse = Assert.Single(await slutt.NyFristHendelser.Where(h => h.Type == (endringsforslag ? "Endret" : "Ny")).ToListAsync());
            Assert.True(hendelse.Publiseringssekvens > 0);
        }
        finally
        {
            port.Slipp();
            try { await gammelBehandling.WaitAsync(TimeSpan.FromSeconds(20)); }
            catch { /* Frigjør operasjonen uten å skjule testfeilen. */ }
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task To_godkjenninger_publiserer_bare_en_frist_og_ett_varsel(bool justerA, bool justerB)
        => await ToGodkjenninger(justerA, justerB, false);

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task To_godkjenninger_av_endring_gir_bare_en_endringshendelse(bool justerA, bool justerB)
        => await ToGodkjenninger(justerA, justerB, true);

    private async Task ToGodkjenninger(bool justerA, bool justerB, bool endring)
    {
        await using var database = await OpprettDatabaseAsync();
        var forslagId = await LeggForslagAsync(database, endring);
        var a = new Lagringsport();
        var b = new Lagringsport();
        IGodkjenningsko administratorA = new GodkjenningskoTjeneste(database.Fabrikk(a));
        IGodkjenningsko administratorB = new GodkjenningskoTjeneste(database.Fabrikk(b));
        var forste = GodkjennAsync(administratorA, forslagId, justerA);
        var andre = GodkjennAsync(administratorB, forslagId, justerB);

        try
        {
            await Task.WhenAll(a.Ankommet, b.Ankommet).WaitAsync(TimeSpan.FromSeconds(20));
            Assert.NotSame(a.Kontekst, b.Kontekst);
            Assert.NotSame(a.Tilkobling, b.Tilkobling);
            a.Slipp();
            var fristId = await forste.WaitAsync(TimeSpan.FromSeconds(20));
            b.Slipp();
            var feil = await Assert.ThrowsAsync<Valideringsfeil>(() => andre.WaitAsync(TimeSpan.FromSeconds(20)));
            Assert.Contains("allerede behandlet", feil.Message);

            await using var kontroll = database.Fabrikk().CreateDbContext();
            var frist = Assert.Single(await kontroll.Frister.ToListAsync());
            Assert.Equal(fristId, frist.Id);
            Assert.Equal(justerA ? "Justert frist" : "Budsjettfrist", frist.Tittel);
            Assert.Equal(FristStatus.Godkjent, (await kontroll.Forslag.SingleAsync()).Status);
            Assert.Contains("godkjent", Assert.Single(await kontroll.Varsler.ToListAsync()).Tekst);
            Assert.Equal(endring ? new[] { "FA", "POL" } : ["FA"],
                await kontroll.FristSynlighet.OrderBy(s => s.GruppeKode).Select(s => s.GruppeKode).ToArrayAsync());
            Assert.Single(await kontroll.NyFristHendelser.Where(h => h.Type == (endring ? "Endret" : "Ny")).ToListAsync());
            Assert.Equal(endring ? 2 : 1, await kontroll.NyFristHendelser.CountAsync());
        }
        finally
        {
            a.Slipp();
            b.Slipp();
            // Portene frigjøres også ved testfeil før databasen disponeres.
            try { await Task.WhenAll(forste, andre).WaitAsync(TimeSpan.FromSeconds(20)); }
            catch { /* Resultatene er kontrollert over; opprydding skal ikke skjule testfeilen. */ }
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Godkjenning_og_avvisning_kan_ikke_overskrive_hverandre(bool avvisForst, bool juster)
        => await GodkjennOgAvvis(avvisForst, juster, false);

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Godkjenning_og_avvisning_av_endring_bevarer_vinner_uten_ekstra_hendelse(bool avvisForst, bool juster)
        => await GodkjennOgAvvis(avvisForst, juster, true);

    private async Task GodkjennOgAvvis(bool avvisForst, bool juster, bool endring)
    {
        await using var database = await OpprettDatabaseAsync();
        var forslagId = await LeggForslagAsync(database, endring);
        var a = new Lagringsport();
        var b = new Lagringsport();
        IGodkjenningsko administratorA = new GodkjenningskoTjeneste(database.Fabrikk(a));
        IGodkjenningsko administratorB = new GodkjenningskoTjeneste(database.Fabrikk(b));
        Task forste = avvisForst ? administratorA.AvvisAsync(forslagId) : GodkjennAsync(administratorA, forslagId, juster);
        Task andre = avvisForst ? GodkjennAsync(administratorB, forslagId, juster) : administratorB.AvvisAsync(forslagId);

        try
        {
            await Task.WhenAll(a.Ankommet, b.Ankommet).WaitAsync(TimeSpan.FromSeconds(20));
            Assert.NotSame(a.Kontekst, b.Kontekst);
            Assert.NotSame(a.Tilkobling, b.Tilkobling);
            a.Slipp();
            await forste.WaitAsync(TimeSpan.FromSeconds(20));
            b.Slipp();
            var feil = await Assert.ThrowsAsync<Valideringsfeil>(() => andre.WaitAsync(TimeSpan.FromSeconds(20)));
            Assert.Contains("allerede behandlet", feil.Message);

            await using var kontroll = database.Fabrikk().CreateDbContext();
            Assert.Equal(avvisForst ? FristStatus.Avvist : FristStatus.Godkjent,
                (await kontroll.Forslag.SingleAsync()).Status);
            Assert.Equal(endring || !avvisForst ? 1 : 0, await kontroll.Frister.CountAsync());
            Assert.Equal(endring ? 2 : avvisForst ? 0 : 1, await kontroll.FristSynlighet.CountAsync());
            Assert.Equal(avvisForst ? 0 : 1, await kontroll.NyFristHendelser.Where(h => h.Type == (endring ? "Endret" : "Ny")).CountAsync());
            Assert.Equal(avvisForst ? "Forslaget ditt er avvist." : "Forslaget ditt er godkjent.",
                Assert.Single(await kontroll.Varsler.ToListAsync()).Tekst);
        }
        finally
        {
            a.Slipp();
            b.Slipp();
            try { await Task.WhenAll(forste, andre).WaitAsync(TimeSpan.FromSeconds(20)); }
            catch { /* Resultatene er kontrollert over; frigjør pågående operasjoner. */ }
        }
    }

    [Fact]
    public async Task Lagringsfeil_ruller_tilbake_forslag_frist_og_varsel_og_tillater_nytt_forsok()
        => await NyLagringsfeil(true);

    [Fact]
    public async Task Direkte_godkjenningsfeil_ruller_tilbake_nyhending_og_sekvens()
        => await NyLagringsfeil(false);

    private async Task NyLagringsfeil(bool juster)
    {
        await using var database = await OpprettDatabaseAsync();
        var forslagId = await LeggForslagAsync(database);
        await database.InstallerLagringsfeilAsync();
        IGodkjenningsko administrator = new GodkjenningskoTjeneste(database.Fabrikk());

        var feil = await Assert.ThrowsAsync<DbUpdateException>(() => GodkjennAsync(administrator, forslagId, juster));
        Assert.Contains("Testfeil etter publiseringsskriv", feil.InnerException?.Message);
        await using (var kontroll = database.Fabrikk().CreateDbContext())
        {
            var forslag = await kontroll.Forslag.SingleAsync();
            Assert.Equal(FristStatus.Forslag, forslag.Status);
            Assert.Equal("Budsjettfrist", forslag.Tittel);
            Assert.Empty(await kontroll.Frister.ToListAsync());
            Assert.Empty(await kontroll.FristSynlighet.ToListAsync());
            Assert.Empty(await kontroll.Varsler.ToListAsync());
            Assert.Empty(await kontroll.NyFristHendelser.ToListAsync());
            Assert.Equal(0, await Sekvens(kontroll));
        }

        await database.FjernLagringsfeilAsync();
        await GodkjennAsync(administrator, forslagId, juster);
        await using var etterpa = database.Fabrikk().CreateDbContext();
        Assert.Equal(FristStatus.Godkjent, (await etterpa.Forslag.SingleAsync()).Status);
        Assert.Equal(juster ? "Justert frist" : "Budsjettfrist", Assert.Single(await etterpa.Frister.ToListAsync()).Tittel);
        Assert.Single(await etterpa.Varsler.ToListAsync());
        Assert.Single(await etterpa.NyFristHendelser.ToListAsync());
        Assert.Equal(1, await Sekvens(etterpa));
    }

    private static Task<Guid> GodkjennAsync(IGodkjenningsko administrator, Guid forslagId, bool juster)
        => juster
            ? administrator.JusterOgGodkjennAsync(forslagId, new FristInndata
            {
                Tittel = "Justert frist", Dato = new DateOnly(2027, 4, 1), Budsjettaar = 2028,
                Kategori = Kategori.Budsjett, Synlighetskoder = ["FA"]
            })
            : administrator.GodkjennAsync(new GodkjennInndata { ForslagId = forslagId, Synlighetskoder = ["FA"] });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pol_publiseres_bare_ved_aktivt_valg(bool juster)
    {
        await using var database = await OpprettDatabaseAsync();
        var forslagId = await LeggForslagAsync(database);
        IGodkjenningsko administrator = new GodkjenningskoTjeneste(database.Fabrikk());
        await Assert.ThrowsAsync<Valideringsfeil>(() => administrator.GodkjennAsync(new GodkjennInndata
        {
            ForslagId = forslagId, Synlighetskoder = ["FA", "POL"], PolBekreftet = false
        }));
        await using (var kontroll = database.Fabrikk().CreateDbContext())
        {
            Assert.Equal(FristStatus.Forslag, (await kontroll.Forslag.SingleAsync()).Status);
            Assert.Empty(await kontroll.Frister.ToListAsync());
            Assert.Empty(await kontroll.Varsler.ToListAsync());
        }

        if (juster)
            await administrator.JusterOgGodkjennAsync(forslagId, new FristInndata
            {
                Tittel = "Aktivt POL-valg", Dato = new DateOnly(2027, 4, 1), Budsjettaar = 2028,
                Kategori = Kategori.Budsjett, Synlighetskoder = ["FA", "POL"]
            });
        else
            await administrator.GodkjennAsync(new GodkjennInndata
            {
                ForslagId = forslagId, Synlighetskoder = ["FA", "POL"], PolBekreftet = true
            });
        await using var etterpa = database.Fabrikk().CreateDbContext();
        Assert.Equal(new[] { "FA", "POL" }, await etterpa.FristSynlighet.OrderBy(s => s.GruppeKode).Select(s => s.GruppeKode).ToArrayAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Slettet_maalfrist_beholder_databasefeilen_og_endringsforslaget_aapent(bool juster)
    {
        await using var database = await OpprettDatabaseAsync();
        var forslagId = await LeggForslagAsync(database);
        var fristId = Guid.NewGuid();
        await using (var oppsett = database.Fabrikk().CreateDbContext())
        {
            var frist = new Frist
            {
                Id = fristId, Tittel = "Opprinnelig", Dato = new DateOnly(2027, 2, 1),
                Budsjettaar = 2028, Kategori = Kategori.Budsjett, Status = FristStatus.Godkjent, Opphav = Opphav.Admin
            };
            frist.Synlighet.Add(new FristSynlighet { GruppeKode = "FA" });
            oppsett.Frister.Add(frist);
            var forslag = await oppsett.Forslag.SingleAsync();
            forslag.ForslagType = ForslagType.Endring;
            forslag.EndrerFristId = fristId;
            await oppsett.SaveChangesAsync();
        }

        var port = new Lagringsport();
        IGodkjenningsko administrator = new GodkjenningskoTjeneste(database.Fabrikk(port));
        IFristDatautveksling import = new FristDatautveksling(database.Fabrikk(), TimeProvider.System);
        IVarseltjeneste varsler = new Varseltjeneste(database.Fabrikk());
        var godkjenning = GodkjennAsync(administrator, forslagId, juster);

        try
        {
            await port.Ankommet.WaitAsync(TimeSpan.FromSeconds(20));
            // Erstatt-alt-importen sletter målfristen etter lesing, før godkjenningen lagrer.
            var resultat = await import.ImporterAsync(new FristDatabase { Frister = [] });
            Assert.Equal(1, resultat.AntallErstattet);
            await using var foerFeil = database.Fabrikk().CreateDbContext();
            var slettet = Assert.Single(await foerFeil.NyFristHendelser.ToListAsync());
            Assert.Equal("Slettet", slettet.Type); Assert.Equal(fristId, slettet.FristId);
            var sekvensFoerFeil = await Sekvens(foerFeil);
            Assert.True(sekvensFoerFeil > 0);
            port.Slipp();
            var feil = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => godkjenning.WaitAsync(TimeSpan.FromSeconds(20)));
            Assert.DoesNotContain("allerede behandlet", feil.Message);

            var aapent = Assert.Single(await administrator.HentKoAsync());
            Assert.Equal(forslagId, aapent.Id);
            Assert.Equal("Budsjettfrist", aapent.Tittel);
            Assert.Null(aapent.Gjeldende);
            Assert.Empty((await import.EksporterAsync()).Frister);
            Assert.Empty(await varsler.HentForBrukerAsync("innsender"));
            await using var kontroll = database.Fabrikk().CreateDbContext();
            var bevart = Assert.Single(await kontroll.NyFristHendelser.ToListAsync());
            Assert.Equal(slettet.Id, bevart.Id); Assert.Equal(slettet.FoerJson, bevart.FoerJson);
            Assert.Equal(slettet.Publiseringssekvens, bevart.Publiseringssekvens);
            Assert.Equal(sekvensFoerFeil, await Sekvens(kontroll));
        }
        finally
        {
            port.Slipp();
            try { await godkjenning.WaitAsync(TimeSpan.FromSeconds(20)); }
            catch { /* Frigjør operasjonen uten å skjule testfeilen. */ }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Endringsforslag_bevarer_eksisterende_synlighet(bool juster)
    {
        await using var database = await OpprettDatabaseAsync();
        var forslagId = await LeggForslagAsync(database);
        var fristId = Guid.NewGuid();
        await using (var oppsett = database.Fabrikk().CreateDbContext())
        {
            var frist = new Frist
            {
                Id = fristId, Tittel = "Opprinnelig", Dato = new DateOnly(2027, 2, 1),
                Budsjettaar = 2028, Kategori = Kategori.Budsjett, Status = FristStatus.Godkjent, Opphav = Opphav.Admin
            };
            frist.Synlighet.Add(new FristSynlighet { GruppeKode = "FA" });
            frist.Synlighet.Add(new FristSynlighet { GruppeKode = "POL" });
            oppsett.Frister.Add(frist);
            var forslag = await oppsett.Forslag.SingleAsync();
            forslag.ForslagType = ForslagType.Endring;
            forslag.EndrerFristId = fristId;
            await oppsett.SaveChangesAsync();
        }

        IGodkjenningsko administrator = new GodkjenningskoTjeneste(database.Fabrikk());
        Assert.Equal(fristId, await GodkjennAsync(administrator, forslagId, juster));
        await using var kontroll = database.Fabrikk().CreateDbContext();
        var endret = Assert.Single(await kontroll.Frister.Include(f => f.Synlighet).ToListAsync());
        Assert.Equal(juster ? "Justert frist" : "Budsjettfrist", endret.Tittel);
        Assert.Equal(new[] { "FA", "POL" }, endret.Synlighet.Select(s => s.GruppeKode).OrderBy(k => k).ToArray());
        Assert.Equal(FristStatus.Godkjent, (await kontroll.Forslag.SingleAsync()).Status);
        Assert.Single(await kontroll.Varsler.ToListAsync());
    }

    private static async Task<Guid> LeggForslagAsync(Godkjenningsdatabase database, bool endringsforslag = false)
    {
        await using (var db = database.Fabrikk().CreateDbContext())
        {
            db.Synlighetsgrupper.Add(new Synlighetsgruppe { Id = Guid.NewGuid(), Kode = "FA", Navn = "FA" });
            db.Synlighetsgrupper.Add(new Synlighetsgruppe { Id = Guid.NewGuid(), Kode = "POL", Navn = "Politisk ledelse" });
            await db.SaveChangesAsync();
        }
        Guid? endrerFristId = null;
        if (endringsforslag)
        {
            IFristskriving frister = new FristskrivingTjeneste(database.Fabrikk());
            endrerFristId = await frister.OpprettAsync(new FristInndata
            {
                Tittel = "Opprinnelig", Dato = new DateOnly(2027, 2, 1), Budsjettaar = 2028,
                Kategori = Kategori.Budsjett, Synlighetskoder = ["FA", "POL"]
            });
        }
        IForslagsinnsending innsending = new ForslagsinnsendingTjeneste(database.Fabrikk());
        return await innsending.SendInnAsync(new BrukerforslagInndata
        {
            Tittel = "Budsjettfrist", Dato = new DateOnly(2027, 3, 15), Budsjettaar = 2028,
            Kategori = Kategori.Budsjett, EndrerFristId = endrerFristId
        }, new GjeldendeBruker("innsender", "Innsender", Funksjonsrolle.Bidragsyter, ["FA"]));
    }

    private sealed class Lagringsport : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource _ankomst = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _frigitt = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Ankommet => _ankomst.Task;
        public AppDbContext? Kontekst { get; private set; }
        public System.Data.Common.DbConnection? Tilkobling { get; private set; }
        public void Slipp() => _frigitt.TrySetResult();

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Kontekst = (AppDbContext)eventData.Context!;
            await Kontekst.Database.OpenConnectionAsync(cancellationToken);
            Tilkobling = Kontekst.Database.GetDbConnection();
            _ankomst.TrySetResult();
            // Begge administratorer har lest det åpne forslaget og klargjort alle skriv.
            await _frigitt.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            return result;
        }
    }
}

public abstract class Godkjenningsdatabase : IAsyncDisposable
{
    protected abstract DbContextOptions<AppDbContext> Alternativer(SaveChangesInterceptor? port);
    public IDbContextFactory<AppDbContext> Fabrikk(SaveChangesInterceptor? port = null) => new FabrikkImpl(Alternativer(port));
    public abstract Task InstallerLagringsfeilAsync();
    public abstract Task FjernLagringsfeilAsync();
    public abstract ValueTask DisposeAsync();

    private sealed class FabrikkImpl(DbContextOptions<AppDbContext> alternativer) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(alternativer);
    }
}
