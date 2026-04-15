using CommunityToolkit.Mvvm.ComponentModel;
using XArchiver.Core.Models;

namespace XArchiver.ViewModels;

public sealed class ScraperDiagnosticsEventItemViewModel : ObservableObject
{
    public ScraperDiagnosticsEventItemViewModel(ScraperDiagnosticsEvent diagnosticsEvent)
    {
        DiagnosticsEvent = diagnosticsEvent;
    }

    public string DetailsText => string.Join(
        " · ",
        new[]
        {
            string.IsNullOrWhiteSpace(DiagnosticsEvent.ArtifactPath) ? null : DiagnosticsEvent.ArtifactPath,
            string.IsNullOrWhiteSpace(DiagnosticsEvent.Selector) ? null : $"Selector: {DiagnosticsEvent.Selector}",
            string.IsNullOrWhiteSpace(DiagnosticsEvent.Url) ? null : DiagnosticsEvent.Url,
        }.Where(value => !string.IsNullOrWhiteSpace(value)));

    public ScraperDiagnosticsEvent DiagnosticsEvent { get; }

    public string CategoryText => DiagnosticsEvent.Category;

    public string MessageText => DiagnosticsEvent.Message;

    public string SeverityText => DiagnosticsEvent.Severity switch
    {
        ScraperDiagnosticsSeverity.Warning => "Warning",
        ScraperDiagnosticsSeverity.Error => "Error",
        _ => "Info",
    };

    public string TimestampText => DiagnosticsEvent.TimestampUtc.ToLocalTime().ToString("T", System.Globalization.CultureInfo.CurrentCulture);
}
