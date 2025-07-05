namespace ImportSeries.Models;

/// <summary>
/// A message to report progress of the import process.
/// </summary>
public class ImportProgressMessage
{
    /// <summary>
    /// The text to display for the current progress status.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// The progress value, typically between 0.0 and 1.0.
    /// </summary>
    public double Value { get; }

    public ImportProgressMessage(string text, double value)
    {
        Text = text;
        Value = value;
    }
}