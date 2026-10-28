using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using System.Text.RegularExpressions;
using Aarshjul.Application;
using Aarshjul.Application.Epost;
using Aarshjul.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aarshjul.Infrastructure.Epost;

public sealed class EpostabonnementTjeneste(IDbContextFactory<AppDbContext> fabrikk, TimeProvider klokke,
    IBekreftelsespost post) : IEpostabonnement
{
    public async Task<EpostabonnementDto> HentAsync(string brukerId, CancellationToken ct = default)
    {
        await using var db = await fabrikk.CreateDbContextAsync(ct);
        return TilDto(await db.Epostabonnementer.AsNoTracking().SingleOrDefaultAsync(x => x.BrukerId == brukerId, ct));
    }

    public Task<EpostabonnementDto> MeldPaaAsync(string brukerId, string adresse, bool godtarStandardvalg, CancellationToken ct = default)
        => MeldPaaAsync(brukerId, adresse, godtarStandardvalg, true, false, ct);

    public async Task<EpostabonnementDto> MeldPaaAsync(string brukerId, string adresse, bool godtar, bool nyeFrister, bool vesentligeEndringer, CancellationToken ct = default)
    {
        adresse = NormaliserAdresse(adresse);
        if (!godtar) throw new Valideringsfeil("Bekreft valgene og at adressen er din egen jobbadresse.");
        ValiderValg(nyeFrister, vesentligeEndringer);
        if (!post.Tilgjengelig) throw new Valideringsfeil("Adressebekreftelse er ikke satt i drift. Du er ikke meldt på.");
        await using var db = await fabrikk.CreateDbContextAsync(ct);
        var rad = await db.Epostabonnementer.SingleOrDefaultAsync(x => x.BrukerId == brukerId, ct);
        var naa = klokke.GetUtcNow();
        if (rad is not null)
        {
            if (rad.Adresse != adresse) throw new Valideringsfeil("Du har allerede registrert en annen jobbadresse. Adressebytte er ikke tilgjengelig ennå.");
            if (rad.Status is "Aktiv" or "VenterPaaBekreftelse") return TilDto(rad);
            KontrollerSending(rad, naa);
        }
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var erNy = rad is null;
        rad ??= new() { BrukerId = brukerId, Adresse = adresse, Opprettet = naa, SendevinduStart = naa };
        rad.NyeFrister = nyeFrister;
        rad.VesentligeEndringer = vesentligeEndringer;
        KlargjoerBekreftelse(rad, token, naa);
        if (erNy) db.Epostabonnementer.Add(rad);
        try { await LagreAsync(db, rad, ct); }
        catch (DbUpdateException) when (erNy)
        {
            // Den unike identiteten avgjør også to samtidige førstegangsinnsendinger.
            // Ikke skjul andre databasefeil: en uavhengig lesing må finne vinnerens rad.
            await using var nyDb = await fabrikk.CreateDbContextAsync(ct);
            var eksisterende = await nyDb.Epostabonnementer.AsNoTracking().SingleOrDefaultAsync(x => x.BrukerId == brukerId, ct);
            if (eksisterende is null) throw;
            if (eksisterende.Adresse != adresse) throw new EpostabonnementKonflikt();
            return TilDto(eksisterende);
        }
        return await SendAsync(db, rad, token, ct);
    }

    public async Task<EpostabonnementDto> EndreValgAsync(string brukerId, bool nyeFrister, bool vesentligeEndringer, Guid versjon, CancellationToken ct = default)
    {
        ValiderValg(nyeFrister, vesentligeEndringer);
        await using var db = await fabrikk.CreateDbContextAsync(ct);
        var rad = await db.Epostabonnementer.SingleOrDefaultAsync(x => x.BrukerId == brukerId, ct);
        if (rad?.Status is not ("Aktiv" or "VenterPaaBekreftelse")) throw new Valideringsfeil("Meld deg på før du endrer varselvalg.");
        if (rad.Versjon != versjon) throw new EpostabonnementKonflikt();
        rad.NyeFrister = nyeFrister;
        rad.VesentligeEndringer = vesentligeEndringer;
        rad.Endret = klokke.GetUtcNow();
        db.KoordinerEpostlagring();
        await LagreAsync(db, rad, ct);
        return TilDto(rad);
    }

    private static void ValiderValg(bool nye, bool endringer)
    {
        if (!nye && !endringer) throw new Valideringsfeil("Velg minst én varseltype.");
    }

    public async Task<EpostabonnementDto> SendNyAsync(string brukerId, CancellationToken ct = default)
    {
        if (!post.Tilgjengelig) throw new Valideringsfeil("Adressebekreftelse er ikke satt i drift.");
        await using var db = await fabrikk.CreateDbContextAsync(ct);
        var rad = await db.Epostabonnementer.SingleOrDefaultAsync(x => x.BrukerId == brukerId, ct);
        if (rad?.Status != "VenterPaaBekreftelse") throw new Valideringsfeil("Ingen påmelding venter på bekreftelse.");
        var naa = klokke.GetUtcNow();
        KontrollerSending(rad, naa);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        KlargjoerBekreftelse(rad, token, naa);
        await LagreAsync(db, rad, ct);
        return await SendAsync(db, rad, token, ct);
    }

    public async Task<EpostabonnementDto> BekreftAsync(string brukerId, string token, CancellationToken ct = default)
    {
        await using var db = await fabrikk.CreateDbContextAsync(ct);
        var rad = await db.Epostabonnementer.SingleOrDefaultAsync(x => x.BrukerId == brukerId, ct);
        const string feil = "Bekreftelsen er ugyldig, utløpt eller allerede brukt. Logg inn som samme bruker og be om en ny lenke ved behov.";
        if (rad?.Status != "VenterPaaBekreftelse" || rad.BekreftelsesHash is null)
            throw new Valideringsfeil(feil);
        var naa = klokke.GetUtcNow();
        var gyldig = rad.BekreftelseUtloeper > naa && rad.Bekreftelsesforsoek < 5
            && token is { Length: 64 } && CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(rad.BekreftelsesHash), Encoding.ASCII.GetBytes(Hash(token)));
        if (!gyldig)
        {
            rad.Bekreftelsesforsoek++;
            if (rad.Bekreftelsesforsoek >= 5 || rad.BekreftelseUtloeper <= naa) rad.BekreftelsesHash = null;
            await LagreAsync(db, rad, ct);
            throw new Valideringsfeil(feil);
        }
        rad.Status = "Aktiv";
        rad.Aktivert = naa;
        rad.Endret = naa;
        FjernBekreftelse(rad);
        db.KoordinerEpostlagring();
        await LagreAsync(db, rad, ct);
        return TilDto(rad);
    }

    public async Task<EpostabonnementDto> MeldAvAsync(string brukerId, CancellationToken ct = default)
    {
        await using var db = await fabrikk.CreateDbContextAsync(ct);
        var rad = await db.Epostabonnementer.SingleOrDefaultAsync(x => x.BrukerId == brukerId, ct);
        if (rad is null || rad.Status == "Avmeldt") return TilDto(rad);
        rad.Status = "Avmeldt";
        rad.Endret = klokke.GetUtcNow();
        FjernBekreftelse(rad);
        await LagreAsync(db, rad, ct);
        return TilDto(rad);
    }

    private static void KontrollerSending(Epostabonnement rad, DateTimeOffset naa)
    {
        if (naa < rad.SistSendt.AddMinutes(1)) throw new Valideringsfeil("Vent minst ett minutt før du ber om en ny bekreftelsesmelding.");
        if (naa < rad.SendevinduStart.AddHours(1) && rad.AntallSendt >= 5)
            throw new Valideringsfeil("Du kan få høyst fem bekreftelsesmeldinger per time. Prøv igjen senere.");
    }

    private static void KlargjoerBekreftelse(Epostabonnement rad, string token, DateTimeOffset naa)
    {
        if (naa >= rad.SendevinduStart.AddHours(1)) { rad.SendevinduStart = naa; rad.AntallSendt = 0; }
        rad.AntallSendt++;
        rad.SistSendt = naa;
        rad.Endret = naa;
        rad.Status = "VenterPaaBekreftelse";
        rad.Aktivert = null;
        rad.BekreftelsesHash = Hash(token);
        rad.BekreftelseUtloeper = naa.AddMinutes(15);
        rad.Bekreftelsesforsoek = 0;
    }

    private async Task<EpostabonnementDto> SendAsync(AppDbContext db, Epostabonnement rad, string token, CancellationToken ct)
    {
        try { await post.SendAsync(rad.BrukerId, rad.Adresse, token, ct); }
        catch (Exception feil) when (feil is not OperationCanceledException)
        {
            // Transportfeil kan inneholde mottaker/hemmelig lenke. Behold bare trygg domenemelding.
            // Status er fortsatt ventende; ingen aktivering eller automatisk nytt sendeforsøk.
            throw new Valideringsfeil("Bekreftelsesmeldingen kunne ikke sendes. Vent ett minutt og be om en ny lenke.");
        }
        // Versjonskontrollen trengs også etter transportoverlevering: en annen
        // operasjon kan ha aktivert eller meldt av påmeldingen mens sendingen ventet.
        try { await LagreAsync(db, rad, ct); }
        catch (EpostabonnementKonflikt)
        {
            // Returner statusen som faktisk vant, ikke en eldre ventende status.
            await using var nyDb = await fabrikk.CreateDbContextAsync(ct);
            return TilDto(await nyDb.Epostabonnementer.AsNoTracking().SingleAsync(x => x.BrukerId == rad.BrukerId, ct));
        }
        return TilDto(rad);
    }

    private static void FjernBekreftelse(Epostabonnement rad)
    {
        rad.BekreftelsesHash = null;
        rad.BekreftelseUtloeper = null;
    }

    private static async Task LagreAsync(AppDbContext db, Epostabonnement rad, CancellationToken ct)
    {
        rad.Versjon = Guid.NewGuid();
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new EpostabonnementKonflikt(); }
    }

    private static string NormaliserAdresse(string? verdi)
    {
        const string feil = "Oppgi én gyldig jobbadresse på et domene som slutter på .dep.no, for eksempel navn@fin.dep.no.";
        if (string.IsNullOrWhiteSpace(verdi) || verdi.Length > 254 || verdi.Any(char.IsControl)
            || !MailAddress.TryCreate(verdi, out var adresse) || adresse.Address != verdi || adresse.DisplayName.Length > 0)
            throw new Valideringsfeil(feil);
        // MailAddress godtar enkelte leverandørutvidelser, blant annet avsluttende
        // punktum. Jobbadressen må også følge grensene for en vanlig addr-spec.
        if (Encoding.UTF8.GetByteCount(adresse.User) > 64
            || (!adresse.User.StartsWith('"') && (adresse.User.StartsWith('.') || adresse.User.EndsWith('.') || adresse.User.Contains("..", StringComparison.Ordinal))))
            throw new Valideringsfeil(feil);
        string domene;
        try { domene = new IdnMapping().GetAscii(adresse.Host).ToLowerInvariant(); }
        catch (ArgumentException) { throw new Valideringsfeil(feil); }
        if (!domene.EndsWith(".dep.no", StringComparison.Ordinal) || domene.Length <= ".dep.no".Length
            || domene.Length > 253 || domene.Split('.').Any(x => !Regex.IsMatch(x, @"\A[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\z")))
            throw new Valideringsfeil(feil);
        return adresse.User + "@" + domene;
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static EpostabonnementDto TilDto(Epostabonnement? rad) => rad is null ? new("IkkePaameldt", null)
        : new(rad.Status, rad.Adresse, rad.NyeFrister, rad.VesentligeEndringer, rad.AlleBudsjettaar, rad.AlleKategorier, rad.Aktivert, rad.Endret, rad.Versjon);
}
