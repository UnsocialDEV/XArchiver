using XArchiver.Core.Models;

namespace XArchiver.Core.Interfaces;

public interface IProfileUrlValidator
{
    ProfileUrlValidationResult? Validate(string profileUrl);
}
