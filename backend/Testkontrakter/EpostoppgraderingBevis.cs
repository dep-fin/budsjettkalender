using System.Text.Json;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Epost;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Aarshjul.Testkontrakter;

public static class EpostoppgraderingBevis
{
    public static async Task LeggTilAsync(Epostdatabase database)
    {
        await FasedataBevis.LeggTilHistorikkAsync(database.Fabrikk());
        var post = new KontrollertBekreftelsespost();
        var paamelding = new EpostabonnementTjeneste(database.Fabrikk(), TimeProvider.System, post);
        await paamelding.MeldPaaAsync("eldre", "eldre@fin.dep.no", true);
        await using var db = database.Fabrikk().CreateDbContext();
        // Seed autentisk EV02-aktivering før EV03-tabellene finnes.
        var naa = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Epostabonnementer SET Status = 'Aktiv', Aktivert = {naa}, Endret = {naa}, BekreftelsesHash = NULL, BekreftelseUtloeper = NULL WHERE BrukerId = 'eldre'");
        db.Kalenderabonnementer.Add(new() { Id = Guid.NewGuid(), Token = "EV03-syntetisk-feedtoken", Etikett = "Bevar FA", GruppeKode = "FA", Aktiv = true });
        db.InternRunder.Add(new() { Id = Guid.NewGuid(), Aar = 2031, OpprettetAv = "eldre", OpprettetTid = DateTimeOffset.UtcNow,
            Gjoeremaal = [new() { Id = Guid.NewGuid(), Tittel = "Bevar internkalender", Dato = new(2030, 11, 10) }] });
        await db.SaveChangesAsync();
    }

    public static async Task<string> HentAsync(Epostdatabase database)
    {
        await using var db = database.Fabrikk().CreateDbContext();
        async Task<string[]> Rader<T>() where T : class
        {
            var rader = await db.Set<T>().ToArrayAsync();
            return rader.Select(r => JsonSerializer.Serialize(db.Entry(r).Properties.OrderBy(p => p.Metadata.Name)
                .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue))).Order(StringComparer.Ordinal).ToArray();
        }
        return JsonSerializer.Serialize(new { Fasedata = await FasedataBevis.HentAsync(database.Fabrikk()),
            Abonnement = await Rader<Epostabonnement>(), Feed = await Rader<Kalenderabonnement>(), Runder = await Rader<InternRunde>(),
            Gjoeremaal = await Rader<InterntGjoeremaal>() });
    }

    public static async Task BekreftIngenTilbakefylling(Epostdatabase database)
    {
        var klokke = new EpostabonnementKontrakt.Proeveklokke();
        var kilde = new KontrollertMottakertilgang();
        kilde.Sett("eldre", new(MottakertilgangStatus.Aktiv, new Aarshjul.Application.Synlighet.Synlighetskontekst(true, [])));
        var post = new KontrollertOppsummeringspost();
        var jobb = new DagligEpostoppsummering(database.Fabrikk(), klokke, kilde, post,
            Microsoft.Extensions.Options.Options.Create(new DagligEpostOpsjoner()));
        Assert.Equal("IngenHendelser", Assert.Single((await jobb.KjorAsync()).Mottakere).Status);
        Assert.Empty(post.Hent("eldre"));
        Assert.Equal("Aktiv", (await new EpostabonnementTjeneste(database.Fabrikk(), klokke, new KontrollertBekreftelsespost()).HentAsync("eldre")).Status);
        await new Aarshjul.Infrastructure.Frister.FristskrivingTjeneste(database.Fabrikk(), klokke).OpprettAsync(new()
        {
            Tittel = "Ny frist etter EV03-oppgradering", Dato = new(2030, 11, 20), Budsjettaar = 2031, Synlighetskoder = ["FA"]
        });
        Assert.Equal("Bekreftet", Assert.Single((await jobb.KjorAsync()).Mottakere).Status);
        var melding = Assert.Single(post.Hent("eldre"));
        Assert.Contains("Ny frist etter EV03-oppgradering", melding.Tekst);
        Assert.Equal(1, melding.Tekst.Split("Ny frist:", StringSplitOptions.None).Length - 1);
    }
}
