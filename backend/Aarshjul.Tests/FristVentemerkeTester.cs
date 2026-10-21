using Aarshjul.Application.Frister;
using Aarshjul.Application.Godkjenningsko;
using Aarshjul.Application.Synlighet;
using Aarshjul.Domain;
using Aarshjul.Infrastructure.Frister;
using Aarshjul.Infrastructure.Godkjenningsko;

namespace Aarshjul.Tests;

public sealed class FristVentemerkeTester : IDisposable
{
    private readonly Testdatabase _t = new();
    private static readonly DateOnly Idag = new(2030, 11, 1);
    private static readonly Synlighetskontekst Admin = new(true, []);
    private IFristlesing Lesing => new FristTjeneste(_t.Factory, new FastKlokke(Idag));
    private IGodkjenningsko Ko => new GodkjenningskoTjeneste(_t.Factory);

    public FristVentemerkeTester()
    {
        foreach (var kode in new[] { "FA", "POL" })
            _t.Db.Synlighetsgrupper.Add(new() { Id = Guid.NewGuid(), Kode = kode, Navn = kode });
        _t.Db.SaveChanges();
    }

    [Fact]
    public async Task Ett_aapent_endringsforslag_gir_administrator_ventemerke_i_alle_leseutvalg()
    {
        var frist = Frist();
        Forslag(frist.Id);
        await _t.Db.SaveChangesAsync();

        foreach (var dto in await LesAlle(Admin, frist.Id))
            Assert.True(dto.VenterEndring);
    }

    [Theory]
    [InlineData("FA")]
    [InlineData("POL")]
    public async Task Gruppelesing_og_se_som_gruppe_faar_samme_resultat_med_og_uten_andres_forslag(string gruppe)
    {
        var frist = Frist();
        await _t.Db.SaveChangesAsync();
        var ctx = Synlighetskontekst.ForGruppe(gruppe);
        var utenForslag = await LesAlle(ctx, frist.Id);

        Forslag(frist.Id);
        await _t.Db.SaveChangesAsync();

        var medForslag = await LesAlle(ctx, frist.Id);
        for (var i = 0; i < medForslag.Length; i++)
        {
            Assert.False(medForslag[i].VenterEndring);
            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(utenForslag[i]),
                System.Text.Json.JsonSerializer.Serialize(medForslag[i]));
        }
    }

    [Theory]
    [InlineData(null, FristStatus.Forslag, true)]
    [InlineData(ForslagType.Endring, FristStatus.Godkjent, true)]
    [InlineData(ForslagType.Endring, FristStatus.Avvist, true)]
    [InlineData(ForslagType.Endring, FristStatus.Forkastet, true)]
    [InlineData(ForslagType.Endring, FristStatus.Fullfoert, true)]
    [InlineData(ForslagType.NyFrist, FristStatus.Forslag, true)]
    [InlineData(ForslagType.Generert, FristStatus.Forslag, true)]
    [InlineData(ForslagType.Endring, FristStatus.Forslag, false)]
    public async Task Bare_aapent_endringsforslag_til_akkurat_denne_fristen_utloeser_merket(
        ForslagType? type, FristStatus status, bool pekerPaaFristen)
    {
        var frist = Frist();
        if (type is not null) Forslag(pekerPaaFristen ? frist.Id : Guid.NewGuid(), type.Value, status);
        // Et løst endringsforslag har ingen berørt frist.
        Forslag(null);
        await _t.Db.SaveChangesAsync();

        foreach (var dto in await LesAlle(Admin, frist.Id))
            Assert.False(dto.VenterEndring);
    }

    [Theory]
    [InlineData("Godkjenn", "Avvis", "Første forslag", "Første forslag")]
    [InlineData("Avvis", "Godkjenn", "Publisert original", "Andre forslag")]
    [InlineData("Juster", "Avvis", "Justert første", "Justert første")]
    [InlineData("Avvis", "Juster", "Publisert original", "Justert andre")]
    [InlineData("Godkjenn", "Juster", "Første forslag", "Justert andre")]
    [InlineData("Juster", "Godkjenn", "Justert første", "Andre forslag")]
    public async Task To_uavhengige_forslag_bevarer_merket_til_siste_faktiske_behandling(
        string forsteHandling, string sisteHandling, string etterForste, string etterSiste)
    {
        var frist = Frist();
        var forste = Forslag(frist.Id);
        forste.Tittel = "Første forslag";
        var andre = Forslag(frist.Id);
        andre.Tittel = "Andre forslag";
        await _t.Db.SaveChangesAsync();

        await BekreftFrist(frist.Id, "Publisert original", true, new(2030, 11, 20));
        await Behandle(forste.Id, forsteHandling, "Justert første");
        Assert.Equal(andre.Id, Assert.Single(await Ko.HentKoAsync()).Id);
        await BekreftFrist(frist.Id, etterForste, true,
            forsteHandling == "Avvis" ? new(2030, 11, 20) : new(2030, 11, 21));

        await Behandle(andre.Id, sisteHandling, "Justert andre");
        Assert.Empty(await Ko.HentKoAsync());
        await BekreftFrist(frist.Id, etterSiste, false, new(2030, 11, 21));
    }

    private async Task Behandle(Guid id, string handling, string justertTittel)
    {
        if (handling == "Avvis") await Ko.AvvisAsync(id, "Syntetisk begrunnelse");
        else if (handling == "Godkjenn")
            Assert.NotEqual(Guid.Empty, await Ko.GodkjennAsync(new() { ForslagId = id, Synlighetskoder = [] }));
        else
            Assert.NotEqual(Guid.Empty, await Ko.JusterOgGodkjennAsync(id, new()
            {
                Tittel = justertTittel, Dato = new(2030, 11, 21), Budsjettaar = 2031,
                // Endringsforslag skal ikke kunne endre eksisterende FA/POL.
                Synlighetskoder = []
            }));
    }

    private async Task BekreftFrist(Guid id, string tittel, bool venter, DateOnly dato)
    {
        foreach (var dto in await LesAlle(Admin, id))
        {
            Assert.Equal(id, dto.Id);
            Assert.Equal(tittel, dto.Tittel);
            Assert.Equal(dato, dto.Dato);
            Assert.Equal(FristStatus.Godkjent, dto.Status);
            Assert.Equal(new[] { "FA", "POL" }, dto.Synlighet.Order());
            Assert.Equal(venter, dto.VenterEndring);
        }
        foreach (var dto in await LesAlle(Synlighetskontekst.ForGruppe("POL"), id))
            Assert.False(dto.VenterEndring);
    }

    private async Task<FristDto[]> LesAlle(ISynlighetskontekst ctx, Guid id)
        => [Assert.Single(await Lesing.HentSynligeAsync(ctx, new())),
            Assert.Single(await Lesing.HentLandingsutvalgAsync(ctx, Idag, new())),
            Assert.IsType<FristDto>(await Lesing.HentEnAsync(ctx, id))];

    private Frist Frist()
    {
        var frist = new Frist
        {
            Id = Guid.NewGuid(), Tittel = "Publisert original", Dato = new(2030, 11, 20),
            Budsjettaar = 2031, Status = FristStatus.Godkjent,
            Synlighet = [new() { GruppeKode = "FA" }, new() { GruppeKode = "POL" }]
        };
        _t.Db.Frister.Add(frist);
        return frist;
    }

    private Forslag Forslag(Guid? fristId, ForslagType type = ForslagType.Endring, FristStatus status = FristStatus.Forslag)
    {
        var forslag = new Forslag
        {
            Id = Guid.NewGuid(), EndrerFristId = fristId, ForslagType = type, Status = status,
            Opphav = Opphav.Bruker, KildeEllerInnsender = "Syntetisk innsender",
            Tittel = "Foreslaatt endring", Dato = new(2030, 11, 21), Budsjettaar = 2031
        };
        _t.Db.Forslag.Add(forslag);
        return forslag;
    }

    public void Dispose() => _t.Dispose();
}
