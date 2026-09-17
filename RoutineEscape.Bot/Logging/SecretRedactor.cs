using System.Data.Common;
using System.Text.RegularExpressions;

namespace RoutineEscape.Bot.Logging;

public sealed class SecretRedactor
{
    private readonly string[] secrets;

    public SecretRedactor(IConfiguration configuration)
    {
        var values = new List<string>();
        foreach (var item in configuration.AsEnumerable())
        {
            if (string.IsNullOrEmpty(item.Value)) continue;
            if (Regex.IsMatch(item.Key, "token|password|secret|api.?key", RegexOptions.IgnoreCase))
                values.Add(item.Value);
            if (Regex.IsMatch(item.Key, "connection.?string", RegexOptions.IgnoreCase))
            {
                values.Add(item.Value);
                try
                {
                    var connection = new DbConnectionStringBuilder { ConnectionString = item.Value };
                    foreach (string key in connection.Keys)
                        if (Regex.IsMatch(key, "password|pwd|token", RegexOptions.IgnoreCase))
                            values.Add(connection[key].ToString()!);
                }
                catch (ArgumentException) { }
            }
        }
        secrets = values.Where(value => value.Length > 0).Distinct().OrderByDescending(value => value.Length).ToArray();
    }

    public string Redact(string value)
    {
        foreach (var secret in secrets)
        {
            value = value.Replace(secret, "[REDACTED]", StringComparison.Ordinal);
            value = value.Replace(Uri.EscapeDataString(secret), "[REDACTED]", StringComparison.OrdinalIgnoreCase);
        }
        value = Regex.Replace(value, @"https?://(?:api\.)?telegram\.org/[^\s""<>]+", "[TELEGRAM_URL_REDACTED]", RegexOptions.IgnoreCase);
        value = Regex.Replace(value, @"\b\d{5,}:[A-Za-z0-9_-]{20,}\b", "[TOKEN_REDACTED]");
        value = Regex.Replace(value, @"\b(?:sk|ghp|gho|github_pat)[-_][A-Za-z0-9_-]{10,}\b", "[KEY_REDACTED]");
        value = Regex.Replace(value, @"(?i)((?:password|passwd|pwd|пароль|token|токен|api[_ -]?key|ключ|secret|authorization)(?:\s*[""']?\s*[=:]\s*|\s+))(?:""[^""]*""|'[^']*'|[^\s;,]+)", "$1[REDACTED]");
        value = Regex.Replace(value, @"(?i)(https?://)[^\s/@]+:[^\s/@]+@", "$1[REDACTED]@");
        value = Regex.Replace(value, @"(?i)\bBearer\s+[^\s"";,]+", "Bearer [REDACTED]");
        return value;
    }
}
