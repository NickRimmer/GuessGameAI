namespace FoolMeGame.Shared.Common;

public static class StringExtension
{
    public static string Join(this IEnumerable<string?> values, string separator) => string.Join(separator, values.OfType<string>());

    public static bool IsEmpty(this string? value) => string.IsNullOrWhiteSpace(value);

    public static bool IsNotEmpty(this string? value) => !value.IsEmpty();
}
