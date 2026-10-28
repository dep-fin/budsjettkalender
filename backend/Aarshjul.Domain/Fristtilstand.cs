namespace Aarshjul.Domain;

/// <summary>Publisert innhold med semantisk dato. Tentative frister har bare måned.</summary>
public sealed record Fristtilstand(string Tittel, DateOnly Dato, Datopresisjon Presisjon,
    Datokvalifikator? Kvalifikator, TimeOnly? Tid, int Budsjettaar, Kategori Kategori,
    string? Loep, string? Notat, string? Kilde, Guid? DokumentId)
{
    public static Fristtilstand Fra(Frist f) => new(f.Tittel,
        f.Datopresisjon == Datopresisjon.Maaned ? new(f.Dato.Year, f.Dato.Month, 1) : f.Dato,
        f.Datopresisjon, f.Datopresisjon == Datopresisjon.Maaned ? f.Datokvalifikator : null,
        f.Datopresisjon == Datopresisjon.Dag ? f.Klokkeslett : null,
        f.Budsjettaar, f.Kategori, f.Loep, f.Notat, f.Kilde, f.DokumentId);

    public bool ErAutomatiskVesentlig(Fristtilstand etter)
        => Dato != etter.Dato || Presisjon != etter.Presisjon || Kvalifikator != etter.Kvalifikator
            || Tid != etter.Tid || Budsjettaar != etter.Budsjettaar || Kategori != etter.Kategori;
}
