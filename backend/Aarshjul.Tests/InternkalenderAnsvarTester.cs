using Aarshjul.Application.Internkalender;
using Aarshjul.Domain;
using Aarshjul.Infrastructure.Internkalender;

namespace Aarshjul.Tests;

public sealed class InternkalenderAnsvarTester : IDisposable
{
    private readonly Testdatabase _t = new();
    private InternkalenderTjeneste Kalender => new(_t.Factory, new FastKlokke(new(2026, 10, 20)));
    private GjoeremaalRegelTjeneste Regler => new(_t.Factory);

    [Fact]
    public async Task Redigering_kan_legge_til_erstatte_og_fjerne_ansvarlige_paa_samme_gjoeremaal()
    {
        var kalender = Kalender;
        var runde = await kalender.OpprettRundeAsync(Rundetype.Regnskap, 2031, "Test");
        var id = await kalender.HurtiglaggAsync(runde, "Oppgave");
        foreach (var ansvarlige in new List<AnsvarligDto>[] { [new("sbr", "SBR")], [new(null, "Ekstern"), new("fa", "FA")], [] })
        {
            var inndata = (await kalender.HentForRedigeringAsync(id))!;
            inndata.Ansvarlige = ansvarlige;
            inndata.Tittel = "Redigert";
            await kalender.OppdaterGjoeremaalAsync(id, inndata);
            var lest = (await kalender.HentForRedigeringAsync(id))!;
            Assert.Equal("Redigert", lest.Tittel);
            Assert.Equal(ansvarlige.OrderBy(a => a.Navn), lest.Ansvarlige.OrderBy(a => a.Navn));
            Assert.Equal(ansvarlige.Count, (await kalender.HentGjoeremaalAsync(runde, null)).Single().Ansvarlige.Count);
        }
    }

    [Fact]
    public async Task Regelredigering_bevarer_rundetyper_og_erstatter_ansvarlige_uten_dubletter()
    {
        var regler = Regler;
        var id = await regler.OpprettAsync(new() { Tittel = "Regel", Rundetyper = [Rundetype.Regnskap] });
        for (var i = 0; i < 2; i++)
        {
            var inndata = (await regler.HentForRedigeringAsync(id))!;
            inndata.Ansvarlige = [new("sbr", "SBR")];
            await regler.OppdaterAsync(id, inndata);
            var lest = (await regler.HentForRedigeringAsync(id))!;
            Assert.Equal(new[] { Rundetype.Regnskap }, lest.Rundetyper);
            Assert.Equal(new AnsvarligDto("sbr", "SBR"), Assert.Single(lest.Ansvarlige));
        }
    }

    [Fact]
    public async Task Synkronisering_erstatter_ansvarlige_paa_et_eksisterende_generert_gjoeremaal()
    {
        var regler = Regler;
        var kalender = Kalender;
        var regel = await regler.OpprettAsync(new() { Tittel = "Regel", Rundetyper = [Rundetype.Regnskap] });
        var runde = await kalender.GenererRundeAsync(Rundetype.Regnskap, 2031, "Test");
        var id = (await kalender.HentGjoeremaalAsync(runde, null)).Single().Id;
        var inndata = (await regler.HentForRedigeringAsync(regel))!;
        inndata.Ansvarlige = [new("sbr", "SBR")];
        await regler.OppdaterAsync(regel, inndata);
        var forslag = await kalender.ForberedSynkAsync(runde);
        Assert.Single(forslag);
        await kalender.SynkroniserAsync(runde, forslag);
        var lest = (await kalender.HentGjoeremaalAsync(runde, null)).Single();
        Assert.Equal(id, lest.Id);
        Assert.Equal(new AnsvarligDto("sbr", "SBR"), Assert.Single(lest.Ansvarlige));
        Assert.Empty(await kalender.ForberedSynkAsync(runde));
    }

    public void Dispose() => _t.Dispose();
}
