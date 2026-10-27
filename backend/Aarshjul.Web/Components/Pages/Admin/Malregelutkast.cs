using System.Globalization;
using System.Text.Json;
using Aarshjul.Application.Generering;
using Aarshjul.Domain;

namespace Aarshjul.Web.Components.Pages.Admin;

// Feltutkastene tilhører Web. Regelparser eier fortsatt den lagrede parameterkontrakten.
internal sealed class Malregelutkast
{
    internal enum Valg { FastDato, Forekomst, FraDato, Milepael }
    internal sealed class Datofelt(string dag, string maaned, string uke = "2")
    {
        public string Dag { get; set; } = dag;
        public string Maaned { get; set; } = maaned;
        public string Uke { get; set; } = uke;
        public string Ukedag { get; set; } = "man";
        public string Aar { get; set; } = "0";
    }

    public string Tittel { get; set; } = "";
    public string Loep { get; set; } = "";
    public Kategori Kategori { get; set; } = Kategori.Budsjett;
    public bool Valgaarssensitiv { get; set; }
    public Valg Type { get; set; }
    public Datofelt Fast { get; } = new("1", "1");
    public Datofelt Forekomst { get; } = new("1", "3");
    public Datofelt Fra { get; } = new("20", "7");
    public string Anker { get; set; } = "";
    public string Antall { get; set; } = "7";
    public string Retning { get; set; } = "for";
    public bool MaaKorrigeres { get; private set; }
    private string? _opprinneligeParametre, _opprinneligParameterbilde;

    public static readonly string[] Maaneder = ["januar", "februar", "mars", "april", "mai", "juni", "juli", "august", "september", "oktober", "november", "desember"];
    public static readonly (string Kode, string Navn)[] Ukedager = [("man", "mandag"), ("tir", "tirsdag"), ("ons", "onsdag"), ("tor", "torsdag"), ("fre", "fredag"), ("lor", "lørdag"), ("son", "søndag")];
    public static readonly string[] Forekomster = ["Første", "Andre", "Tredje", "Fjerde", "Femte"];
    public Datofelt AktivDato => Type switch { Valg.Forekomst => Forekomst, Valg.FraDato => Fra, _ => Fast };

    public static Malregelutkast FraInndata(MalRegelInndata data)
    {
        var felt = new Malregelutkast { Tittel = data.Tittel, Loep = data.Loep, Kategori = data.Kategori, Valgaarssensitiv = data.Valgaarssensitiv };
        try
        {
            switch (data.Regeltype)
            {
                case Regeltype.FastDato:
                    var fastdato = Regelparser.FastDato(data.Regelparametre);
                    Sett(felt.Fast, fastdato.Maaned, fastdato.Dag, fastdato.AarForskyvning);
                    break;
                case Regeltype.RelativUkedag:
                    var ukedag = Regelparser.RelativUkedag(data.Regelparametre);
                    felt.Type = ukedag.FraDag is null ? Valg.Forekomst : Valg.FraDato;
                    var dato = felt.AktivDato;
                    Sett(dato, ukedag.Maaned, ukedag.FraDag ?? 1, ukedag.AarForskyvning);
                    dato.Uke = Tall(ukedag.Uke);
                    dato.Ukedag = Ukedager.First(u => Regelparser.TolkUkedag(u.Kode) == Regelparser.TolkUkedag(ukedag.Ukedag)).Kode;
                    break;
                case Regeltype.RelativTilMilepael:
                    var relativ = Regelparser.RelativTilMilepael(data.Regelparametre);
                    felt.Type = Valg.Milepael;
                    felt.Anker = relativ.AnkerLoep;
                    felt.Antall = Math.Abs((long)relativ.OffsetDager).ToString(CultureInfo.InvariantCulture);
                    felt.Retning = relativ.OffsetDager < 0 ? "for" : relativ.OffsetDager > 0 ? "etter" : "samme";
                    break;
                default: throw new FormatException("Ukjent regeltype.");
            }
            felt._opprinneligeParametre = data.Regelparametre;
            felt._opprinneligParameterbilde = felt.Parameterbilde();
        }
        catch (FormatException) { felt.MaaKorrigeres = true; }
        return felt;
    }

    public void Korriger() => MaaKorrigeres = false;
    public string Bilde() => JsonSerializer.Serialize(new { Tittel, Loep, Kategori, Valgaarssensitiv, Type, Fast, Forekomst, Fra, Anker, Antall, Retning, MaaKorrigeres });
    private string Parameterbilde() => Type == Valg.Milepael ? JsonSerializer.Serialize(new { Type, Anker, Antall, Retning }) : JsonSerializer.Serialize(new { Type, Dato = AktivDato });

    public bool ProevInndata(out MalRegelInndata data, out Dictionary<string, string> feil)
    {
        feil = [];
        data = new() { Tittel = Tittel, Loep = Loep, Kategori = Kategori, Valgaarssensitiv = Valgaarssensitiv };
        if (string.IsNullOrWhiteSpace(Tittel)) feil["regel-tittel"] = "Fyll ut tittel.";
        if (string.IsNullOrWhiteSpace(Loep)) feil["regel-loep"] = "Fyll ut funksjon / løp.";
        if (MaaKorrigeres) { feil["regel-korriger"] = "Den eldre regelen må korrigeres uttrykkelig før lagring."; return false; }
        if (Type == Valg.Milepael)
        {
            data.Regeltype = Regeltype.RelativTilMilepael;
            if (string.IsNullOrWhiteSpace(Anker)) feil["regel-anker"] = "Velg eller skriv et ankerløp.";
            var offset = 0L;
            if (Retning != "samme")
            {
                if (!long.TryParse(Antall, NumberStyles.None, CultureInfo.InvariantCulture, out var antall) || antall < 0 || antall > 2147483648L)
                    feil["regel-antall"] = "Antall kalenderdager må være et heltall fra 0 til 2147483648.";
                else offset = Retning == "for" ? -antall : antall;
            }
            if (offset is < int.MinValue or > int.MaxValue) feil["regel-antall"] = "Denne retningen støtter høyst 2147483647 kalenderdager.";
            if (feil.Count == 0) data.Regelparametre = Regelparser.Serialiser(new RelativTilMilepaelParametre(Anker, (int)offset));
        }
        else
        {
            var dato = AktivDato;
            if (!int.TryParse(dato.Aar, NumberStyles.Integer, CultureInfo.InvariantCulture, out var aar)) feil["regel-aar"] = "Årsforskyvning må være et heltall fra −2147483648 til 2147483647.";
            if (!int.TryParse(dato.Maaned, out var maaned) || maaned is < 1 or > 12) feil["regel-maaned"] = "Velg en måned.";
            var dag = 1;
            var uke = 1;
            if (Type != Valg.Forekomst && (!int.TryParse(dato.Dag, out dag) || maaned is >= 1 and <= 12 && (dag < 1 || dag > DateTime.DaysInMonth(2000, maaned))))
                feil["regel-dag"] = "Dagen finnes ikke i den valgte måneden. 29. februar er tillatt.";
            if (Type == Valg.Forekomst && (!int.TryParse(dato.Uke, out uke) || uke is < 1 or > 5)) feil["regel-uke"] = "Velg første til femte forekomst.";
            if (Type != Valg.FastDato && !Ukedager.Any(u => u.Kode == dato.Ukedag)) feil["regel-ukedag"] = "Velg en ukedag.";
            data.Regeltype = Type == Valg.FastDato ? Regeltype.FastDato : Regeltype.RelativUkedag;
            if (feil.Count == 0)
                data.Regelparametre = Type == Valg.FastDato
                    ? Regelparser.Serialiser(new FastDatoParametre(maaned, dag, aar))
                    : Regelparser.Serialiser(new RelativUkedagParametre(maaned, Type == Valg.FraDato && int.TryParse(dato.Uke, out var inaktivUke) ? inaktivUke : uke, dato.Ukedag, aar, Type == Valg.FraDato ? dag : null));
        }
        if (feil.Count > 0) return false;
        // Bevar blant annet inaktiv eldre uke=99 når fra_dag er satt og feltene er urørt.
        if (_opprinneligeParametre is not null && _opprinneligParameterbilde == Parameterbilde()) data.Regelparametre = _opprinneligeParametre;
        try { Regelparser.Valider(data.Regeltype, data.Regelparametre); }
        catch (FormatException e) { feil["regel-type"] = e.Message; }
        return feil.Count == 0;
    }

    public string Setning()
    {
        if (MaaKorrigeres) return "Denne eldre regelen kan ikke beskrives før parameterfeltene er korrigert.";
        if (Type == Valg.Milepael)
            return Retning == "samme" ? $"Samme dag som {Ankernavn()}." : $"{Antall} kalenderdager {(Retning == "for" ? "før" : "etter")} {Ankernavn()}.";
        var d = AktivDato;
        var maaned = int.TryParse(d.Maaned, out var m) && m is >= 1 and <= 12 ? Maaneder[m - 1] : "valgt måned";
        var ukedag = Ukedager.FirstOrDefault(u => u.Kode == d.Ukedag).Navn ?? "valgt ukedag";
        var beregning = Type switch
        {
            Valg.Forekomst => $"{(int.TryParse(d.Uke, out var u) && u is >= 1 and <= 5 ? Forekomster[u - 1] : "Valgt")} {ukedag} i {maaned}",
            Valg.FraDato => $"Første {ukedag} fra og med {d.Dag}. {maaned}",
            _ => $"{d.Dag}. {maaned}"
        };
        return $"{beregning}, {Aarstekst(d.Aar)}.";
    }

    public static string Beskriv(MalRegelDto regel) => FraInndata(new() { Loep = regel.Loep, Tittel = regel.Tittel, Kategori = regel.Kategori, Regeltype = regel.Regeltype, Regelparametre = regel.Regelparametre, Valgaarssensitiv = regel.Valgaarssensitiv }).Setning();
    public static int Avhengige(IReadOnlyList<MalRegelDto> regler, string loep) => regler.Count(r =>
    {
        if (r.Regeltype != Regeltype.RelativTilMilepael) return false;
        try { return string.Equals(Regelparser.RelativTilMilepael(r.Regelparametre).AnkerLoep, loep, StringComparison.OrdinalIgnoreCase); }
        catch (FormatException) { return false; }
    });
    private string Ankernavn() => string.IsNullOrWhiteSpace(Anker) ? "valgt ankerløp" : $"«{Anker}»";
    private static string Aarstekst(string aar) => int.TryParse(aar, out var a) ? a switch { 0 => "i budsjettåret", < 0 => $"{-(long)a} år før budsjettåret", _ => $"{a} år etter budsjettåret" } : "med valgt årsforskyvning";
    private static string Tall(int tall) => tall.ToString(CultureInfo.InvariantCulture);
    private static void Sett(Datofelt d, int maaned, int dag, int aar) { d.Maaned = Tall(maaned); d.Dag = Tall(dag); d.Aar = Tall(aar); }
}

