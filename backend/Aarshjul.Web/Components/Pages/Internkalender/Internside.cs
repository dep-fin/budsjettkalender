using Aarshjul.Application;
using Microsoft.AspNetCore.Components;

namespace Aarshjul.Web.Components.Pages.Internkalender;

/// <summary>Knytter sene svar til siden som startet lesingen eller skrivingen.</summary>
public abstract class Internside : ComponentBase, IDisposable
{
    [Inject] protected ILogger<Internside> Logg { get; set; } = default!;
    protected readonly CancellationTokenSource Levetid = new();
    protected int Visning;
    protected bool Avsluttet;
    protected bool ErGjeldende(int visning) => !Avsluttet && visning == Visning;

    protected string Feiltekst(Exception feil, string melding)
    {
        if (feil is Valideringsfeil) return feil.Message;
        if (feil is UnauthorizedAccessException) return "Du har ikke tilgang til internkalenderen. Logg inn på nytt med SBR-tilgang.";
        Logg.LogError(feil, "Internkalender: {Melding}", melding);
        return melding;
    }

    public void Dispose()
    {
        Avsluttet = true;
        Levetid.Cancel();
        Levetid.Dispose();
        GC.SuppressFinalize(this);
    }
}
