namespace JobBoard.Api.Common;

/// <summary>Case/underscore-insensitive enum parsing for API inputs ("full_time" == "FullTime").</summary>
public static class EnumValue<T> where T : struct, Enum
{
    public static bool TryParse(string? value, out T result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Replace("_", string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty);
        return Enum.TryParse(normalized, ignoreCase: true, out result) && Enum.IsDefined(result);
    }

    /// <summary>Convenience overload for FluentValidation's <c>Must(...)</c>.</summary>
    public static bool IsValid(string? value) => TryParse(value, out _);

    public static T Parse(string? value, string fieldName)
    {
        if (TryParse(value, out var result))
            return result;

        throw ApiException.BadRequest("invalid_enum", $"{fieldName} has an invalid value.",
            new Dictionary<string, string[]> { [fieldName] = [$"Expected one of: {string.Join(", ", Enum.GetNames<T>())}."] });
    }
}
