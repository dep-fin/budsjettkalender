using Aarshjul.Application.Godkjenningsko;
using Aarshjul.Domain;
using Aarshjul.Infrastructure.Godkjenningsko;

namespace Aarshjul.Tests;

public sealed class KoKildefilterTester : IDisposable
{
    private readonly Testdatabase _t = new();
    private readonly Dictionary<string, Guid> _ider = [];

    public KoKildefilterTester()
    {
        var dokument = new BehandletDokument
        {
            Id = Guid.NewGuid(), Kilde = "regjeringen", DokumentNokkel = "felles-dokument",
            Tittel = "Leverandør er ikke køetikett"
        };
        _t.Db.BehandledeDokumenter.Add(dokument);
        var opplasting = new BehandletDokument
        {
            Id = Guid.NewGuid(), Kilde = "opplastet", DokumentNokkel = "opplastet-dokument",
            Tittel = "Opplastet dokument med egen køetikett"
        };
        _t.Db.BehandledeDokumenter.Add(opplasting);
        Legg("a-ny", "R-A", dokument.Id);
        Legg("a-endring", "R-A", dokument.Id, type: ForslagType.Endring);
        Legg("a-ukjent", "R-A", dokument.Id, loep: null);
        Legg("a-gul", "R-A", opplasting.Id, kategori: Kategori.Gulbok);
        Legg("a-bruker", "R-A", dokument.Id, opphav: Opphav.Bruker, loep: null);
        Legg("b-ny", "R-B", dokument.Id);
        Legg("opplasting", "opplastet-budsjett.pdf", opplasting.Id);
        Legg("innsender", "bruker-73", null, opphav: Opphav.Bruker);
        Legg("null", null, null);
        Legg("tom", "", null);
        Legg("blank", " \t ", null);
        Legg("raa", " R-A ", null);
        Legg("godkjent", "bare-godkjent", null, status: FristStatus.Godkjent);
        Legg("avvist", "bare-avvist", null, status: FristStatus.Avvist);
        Legg("forkastet", "bare-forkastet", null, status: FristStatus.Forkastet);
        Legg("generert", "bare-generert", null, type: ForslagType.Generert);
        _t.Db.SaveChanges();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    public async Task Alle_beholder_forslag_uten_kilde_og_utelater_behandlede_og_genererte(string? kilde)
        => await Forvent(new() { Kilde = kilde }, "a-ny", "a-endring", "a-ukjent", "a-gul", "a-bruker",
            "b-ny", "opplasting", "innsender", "null", "tom", "blank", "raa");

    [Theory]
    [InlineData("R-A", new[] { "a-ny", "a-endring", "a-ukjent", "a-gul", "a-bruker" })]
    [InlineData("R-B", new[] { "b-ny" })]
    [InlineData("opplastet-budsjett.pdf", new[] { "opplasting" })]
    [InlineData("bruker-73", new[] { "innsender" })]
    [InlineData(" R-A ", new[] { "raa" })]
    [InlineData("r-a", new string[0])]
    [InlineData("ukjent", new string[0])]
    [InlineData("regjeringen", new string[0])]
    [InlineData("opplastet", new string[0])]
    [InlineData("bare-godkjent", new string[0])]
    [InlineData("bare-avvist", new string[0])]
    [InlineData("bare-forkastet", new string[0])]
    [InlineData("bare-generert", new string[0])]
    public async Task Eksakt_koetikett_velger_lagrede_IDer_uavhengig_av_leverandoer_og_dokument(
        string kilde, string[] forventet) => await Forvent(new() { Kilde = kilde }, forventet);

    [Fact]
    public async Task Kildefilter_er_snitt_med_hvert_oevrig_filter_og_alle_sammen()
    {
        await Forvent(new() { Kilde = "R-A", Opphav = Opphav.Robot }, "a-ny", "a-endring", "a-ukjent", "a-gul");
        await Forvent(new() { Kilde = "R-A", Kategori = Kategori.Gulbok }, "a-gul");
        await Forvent(new() { Kilde = "R-A", ForslagType = ForslagType.Endring }, "a-endring");
        await Forvent(new() { Kilde = "R-A", UkjentType = true }, "a-ukjent");
        await Forvent(new() { Kilde = "R-A", UkjentType = false }, "a-ny", "a-endring", "a-gul", "a-bruker");
        var alle = new Kofilter
        {
            Kilde = "R-A", Opphav = Opphav.Robot, Kategori = Kategori.Budsjett,
            ForslagType = ForslagType.NyFrist, UkjentType = true
        };
        await Forvent(alle, "a-ukjent");
        await Forvent(alle with { Kilde = "R-B" });
        await Forvent(alle with { Kilde = null }, "a-ukjent");
        await Forvent(alle with { Opphav = Opphav.Bruker });
        await Forvent(alle with { Kategori = Kategori.Regnskap });
        await Forvent(alle with { ForslagType = ForslagType.Generert });
    }

    private async Task Forvent(Kofilter filter, params string[] navne)
    {
        IGodkjenningsko ko = new GodkjenningskoTjeneste(_t.Factory);
        Assert.Equal(navne.Select(n => _ider[n]).Order(), (await ko.HentKoAsync(filter)).Select(e => e.Id).Order());
    }

    private void Legg(string navn, string? kilde, Guid? dokument,
        Opphav opphav = Opphav.Robot, ForslagType type = ForslagType.NyFrist,
        Kategori kategori = Kategori.Budsjett, string? loep = "rammefordeling",
        FristStatus status = FristStatus.Forslag)
    {
        var id = Guid.NewGuid();
        _ider.Add(navn, id);
        _t.Db.Forslag.Add(new()
        {
            Id = id, Tittel = navn, KildeEllerInnsender = kilde, DokumentId = dokument,
            Opphav = opphav, ForslagType = type, Kategori = kategori, Loep = loep,
            Status = status, Dato = new DateOnly(2030, 11, 20), Budsjettaar = 2031
        });
    }

    public void Dispose() => _t.Dispose();
}
