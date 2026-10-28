namespace Aarshjul.Application.Brukere;

/// <summary>Leser bare administratorer som allerede er kjent i appen. Endrer ingen brukere.</summary>
public interface IAdministratoroversikt
{
    Task<IReadOnlyList<AdministratortilgangDto>> HentAsync(CancellationToken ct = default);
}

public sealed record AdministratortilgangDto(string BrukerId, string Navn, bool AdminViaEntra);
