namespace ImportSeries.Models
{
    // Simple progress message used with WeakReferenceMessenger
    public sealed record class ImportProgressMessage(string Text, double Progress);
}