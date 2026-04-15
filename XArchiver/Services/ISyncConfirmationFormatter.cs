using XArchiver.Core.Models;

namespace XArchiver.Services;

public interface ISyncConfirmationFormatter
{
    string FormatBody(ArchiveProfile profile, decimal costPerThousandPostReads);
}
