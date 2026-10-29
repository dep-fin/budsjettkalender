using Aarshjul.Domain;

namespace Aarshjul.Infrastructure.Epost;

internal static class EpostabonnementTilstand
{
    internal static void Stans(Epostabonnement rad, DateTimeOffset naa)
    {
        rad.Status = "Avmeldt";
        rad.Endret = naa;
        rad.BekreftelsesHash = null;
        rad.BekreftelseUtloeper = null;
        rad.Versjon = Guid.NewGuid();
    }
}
