namespace XArchiver.Core.Utilities;

public static class PostIdComparer
{
    public static int Compare(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left))
        {
            return string.IsNullOrWhiteSpace(right) ? 0 : -1;
        }

        if (string.IsNullOrWhiteSpace(right))
        {
            return 1;
        }

        if (left.Length != right.Length)
        {
            return left.Length.CompareTo(right.Length);
        }

        return string.CompareOrdinal(left, right);
    }

    public static string? Max(string? left, string? right)
    {
        return Compare(left, right) >= 0 ? left : right;
    }
}
