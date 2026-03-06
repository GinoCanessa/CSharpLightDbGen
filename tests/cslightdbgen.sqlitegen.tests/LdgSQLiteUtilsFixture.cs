using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace cslightdbgen.sqlitegen.tests;

public static class LdgSQLiteUtilsFixture
{
    public enum LdgNumericOperatorCodes : int
    {
        Equals = 0,
        NotEquals = 1,
        GreaterThan = 2,
        GreaterThanOrEquals = 3,
        LessThan = 4,
        LessThanOrEquals = 5,
    }

    public static string GetSqlOperator(LdgNumericOperatorCodes op) => op switch
    {
        LdgNumericOperatorCodes.Equals => "=",
        LdgNumericOperatorCodes.NotEquals => "!=",
        LdgNumericOperatorCodes.GreaterThan => ">",
        LdgNumericOperatorCodes.GreaterThanOrEquals => ">=",
        LdgNumericOperatorCodes.LessThan => "<",
        LdgNumericOperatorCodes.LessThanOrEquals => "<=",
        _ => throw new NotSupportedException($"Unsupported operator code: {op}"),
    };

    private static readonly System.Text.RegularExpressions.Regex HtmlStripRegex = new("<.*?>", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
    };

    public static bool TrySerializeForDb<T>(T? instance, [NotNullWhen(true)] out string? json) where T : class
    {
        if (instance == null)
        {
            json = null;
            return false;
        }

        json = JsonSerializer.Serialize(instance, Options);
        return true;
    }

    public static bool TrySerializeForDb<T>(List<T>? instances, [NotNullWhen(true)] out string? json) where T : class
    {
        if ((instances == null) || (instances.Count == 0))
        {
            json = null;
            return false;
        }

        json = JsonSerializer.Serialize(instances, Options);
        return true;
    }

    public static T? ParseFromDb<T>(string json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<T>(json, Options);
    }

    public static List<T> ParseArrayFromDb<T>(string json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<T>>(json, Options) ?? [];
    }

    public static string StripHtml(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        return HtmlStripRegex.Replace(input, string.Empty).Trim();
    }
}
