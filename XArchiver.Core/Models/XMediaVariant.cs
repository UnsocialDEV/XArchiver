namespace XArchiver.Core.Models;

public sealed class XMediaVariant
{
    public string Url { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public int? BitRate { get; set; }
}
