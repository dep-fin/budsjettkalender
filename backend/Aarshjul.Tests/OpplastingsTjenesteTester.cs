using Aarshjul.Application.Datouttrekk;
using Aarshjul.Application.Opplasting;
using Aarshjul.Application.Godkjenningsko;
using Aarshjul.Domain;
using Aarshjul.Infrastructure.Datouttrekk;
using Aarshjul.Infrastructure.Generering;
using Aarshjul.Infrastructure.Opplasting;
using Aarshjul.Infrastructure.Godkjenningsko;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Aarshjul.Tests;

/// <summary>
/// Opplastingspipelinen (Fase 2, manuell inntaksvei): samme uttrekk/klassifisering/kø som
/// automatisk innhenting. Bruker en stub-PDF-leser så testene slipper ekte PDF-filer.
/// </summary>
public class OpplastingsTjenesteTester : IDisposable
{
    private readonly Testdatabase _t = new();

    private sealed class StubPdftekst(string tekst) : IPdftekst
    {
        public string HentTekst(byte[] innhold) => tekst;
    }

    private OpplastingsTjeneste Tjeneste(string dokumenttekst, IDatouttrekk? uttrekk = null)
    {
        var synlighetsregel = new Synlighetsregel(Options.Create(new SynlighetsregelOpsjoner()));
        return new OpplastingsTjeneste(
            _t.Factory, new StubPdftekst(dokumenttekst), uttrekk ?? new HeuristiskDatouttrekk(),
            synlighetsregel, new FastKlokke(new DateOnly(2026, 6, 1)));
    }

    private static OpplastetDokument Fil(string navn = "rundskriv.pdf")
        => new(navn, [1, 2, 3]);

    private sealed class Modelluttrekk(Uttrekksresultat resultat) : IDatouttrekk
    {
        public Task<Uttrekksresultat> TrekkUtAsync(string tekst, int budsjettaar, CancellationToken ct = default)
            => Task.FromResult(resultat);
    }

    [Fact]
    public async Task Hoy_konfidens_med_relativ_kilde_vises_med_forklaring_etter_opplasting()
    {
        const string kilde = "Innleveres ultimo mars; se tidsplan 23. januar 2027";
        IDokumentopplasting opplasting = Tjeneste(kilde, new Modelluttrekk(new Uttrekksresultat
        {
            Felter = [new Uttrekksfelt
            {
                Felt = Uttrekksfelter.Dato, TolketVerdi = "2027-03-31",
                Kildeutdrag = "Innleveres ultimo mars", Konfidens = 0.99
            }]
        }));
        var resultat = await opplasting.LesOgLagForslagAsync(Fil(), 2027);
        IGodkjenningsko ko = new GodkjenningskoTjeneste(_t.Factory);

        var forslag = Assert.Single(await ko.HentKoAsync());
        var bevis = Assert.Single(forslag.Bevis);
        Assert.Equal(1, resultat.AntallForslag);
        Assert.Equal(new DateOnly(2027, 3, 31), forslag.Dato);
        Assert.Equal("2027-03-31", bevis.TolketVerdi);
        Assert.Equal("Innleveres ultimo mars", bevis.Kildeutdrag);
        Assert.Equal(0.99, bevis.Konfidens);
        Assert.Contains(bevis.Flagg, f => f.Grunn == Usikkerhetsgrunn.RelativFormuleringTolketTilDato
            && f.Forklaring == "Kilden er relativt formulert, men er tolket til en konkret dato — kontroller.");
    }

    [Theory]
    [InlineData("2027-03-31", "Fristen fremgår av vedlegget", Usikkerhetsgrunn.KildeutdragUtenDato,
        "Tolket dato har ingen gjenkjennelig dato i kildeutdraget — kontroller.")]
    [InlineData("2030-03-31", "31. mars 2030", Usikkerhetsgrunn.DatoUtenforForventetVindu,
        "Tolket årstall 2030 er utenfor forventet vindu for budsjettår 2027.")]
    [InlineData("2100-03-31", "31. mars 2100", Usikkerhetsgrunn.DatoUtenforForventetVindu,
        "Tolket årstall 2100 er utenfor forventet vindu for budsjettår 2027.")]
    public async Task Usikre_datobevis_vises_med_konkret_forklaring_i_koen(
        string tolket, string kilde, Usikkerhetsgrunn grunn, string forklaring)
    {
        IDokumentopplasting opplasting = Tjeneste("Tidsplan 23. januar 2027", new Modelluttrekk(new Uttrekksresultat
        {
            Felter = [new Uttrekksfelt
            {
                Felt = Uttrekksfelter.Dato, TolketVerdi = tolket, Kildeutdrag = kilde, Konfidens = 0.99
            }]
        }));
        await opplasting.LesOgLagForslagAsync(Fil(), 2027);
        IGodkjenningsko ko = new GodkjenningskoTjeneste(_t.Factory);

        var forslag = Assert.Single(await ko.HentKoAsync());
        var bevis = Assert.Single(forslag.Bevis);
        var flagg = Assert.Single(bevis.Flagg);
        Assert.Equal(grunn, flagg.Grunn);
        Assert.Equal(forklaring, flagg.Forklaring);
        Assert.Equal(tolket, bevis.TolketVerdi);
        Assert.Equal(kilde, bevis.Kildeutdrag);
    }

    private const string Tekst =
        "Tidsplan for budsjettarbeidet\n" +
        "Frist for innsending av satsingsforslag er 23. januar 2027.\n" +
        "Hovedbudsjettskriv sendes ut 15.03.2027.\n" +
        "Dette avsnittet har ingen dato.\n" +
        "Rapportering til statsregnskapen skjer innen 2027-02-15.\n";

    [Fact]
    public async Task Lager_forslag_av_datolinjer_med_uttrekksbevis()
    {
        var r = await Tjeneste(Tekst).LesOgLagForslagAsync(Fil(), 2027);

        Assert.Equal(3, r.AntallForslag);
        var forslag = await _t.Db.Forslag.Include(f => f.UttrekksBevis).ToListAsync();
        Assert.Equal(3, forslag.Count);
        Assert.All(forslag, f =>
        {
            Assert.Equal(Opphav.Robot, f.Opphav);
            Assert.StartsWith("Opplastet:", f.KildeEllerInnsender);
            Assert.Equal(FristStatus.Forslag, f.Status);
            Assert.NotNull(f.DokumentId);
            Assert.NotEmpty(f.UttrekksBevis);
        });
    }

    [Fact]
    public async Task Klassifiserer_kjente_loep_som_automatisk()
    {
        await Tjeneste(Tekst).LesOgLagForslagAsync(Fil(), 2027);
        var forslag = await _t.Db.Forslag.ToListAsync();

        Assert.Contains(forslag, f => f.Loep == "rammefordeling"); // «hovedbudsjettskriv»
        Assert.Contains(forslag, f => f.Loep == "rapportering");   // «rapportering til statsregnskapen»
        Assert.Contains(forslag, f => f.Loep is null);             // satsingsforslag = ukjent type
    }

    [Fact]
    public async Task Registrerer_behandlet_dokument_og_publiserer_ingenting()
    {
        await Tjeneste(Tekst).LesOgLagForslagAsync(Fil(), 2027);

        Assert.True(await _t.Db.BehandledeDokumenter.AnyAsync(d => d.Kilde == OpplastingsTjeneste.Kilde));
        // Ingenting publiseres uten godkjenning.
        Assert.Empty(await _t.Db.Frister.ToListAsync());
    }

    [Fact]
    public async Task Samme_dokument_gir_ikke_dubletter()
    {
        await Tjeneste(Tekst).LesOgLagForslagAsync(Fil(), 2027);
        var r2 = await Tjeneste(Tekst).LesOgLagForslagAsync(Fil(), 2027);

        Assert.True(r2.AlleredeBehandlet);
        Assert.Equal(3, await _t.Db.Forslag.CountAsync());
    }

    [Fact]
    public async Task Uten_datoer_lages_ingen_forslag()
    {
        var r = await Tjeneste("Et notat helt uten datoer i teksten.").LesOgLagForslagAsync(Fil(), 2027);

        Assert.Equal(0, r.AntallForslag);
        Assert.Contains("ingen datoer", r.Melding, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await _t.Db.Forslag.ToListAsync());
    }

    public void Dispose() => _t.Dispose();
}
