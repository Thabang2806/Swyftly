using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Mabuntle.Infrastructure.Payments;

public static class PayFastFormEncoder
{
    public const string SignatureFieldName = "signature";

    public static IReadOnlyList<KeyValuePair<string, string>> ParseOrderedPairs(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return [];
        }

        var pairs = new List<KeyValuePair<string, string>>();
        var segments = payload.Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            var equalsIndex = segment.IndexOf('=');
            var rawKey = equalsIndex >= 0 ? segment[..equalsIndex] : segment;
            var rawValue = equalsIndex >= 0 ? segment[(equalsIndex + 1)..] : string.Empty;

            pairs.Add(new KeyValuePair<string, string>(
                DecodeFormValue(rawKey),
                DecodeFormValue(rawValue)));
        }

        return pairs;
    }

    public static IReadOnlyDictionary<string, string> ToDictionary(
        IEnumerable<KeyValuePair<string, string>> pairs)
    {
        var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in pairs)
        {
            dictionary[pair.Key] = pair.Value;
        }

        return dictionary;
    }

    public static string ComputeSignature(
        IEnumerable<KeyValuePair<string, string>> fields,
        string? passphrase)
    {
        var parameterString = BuildParameterString(fields, passphrase);
        var bytes = Encoding.UTF8.GetBytes(parameterString);
        var hash = MD5.HashData(bytes);

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string BuildFormPayload(IEnumerable<KeyValuePair<string, string>> fields)
    {
        return string.Join("&", fields.Select(field =>
            $"{EncodeFormValue(field.Key)}={EncodeFormValue(field.Value)}"));
    }

    public static string FormatAmount(decimal amount) =>
        amount.ToString("0.00", CultureInfo.InvariantCulture);

    private static string BuildParameterString(
        IEnumerable<KeyValuePair<string, string>> fields,
        string? passphrase)
    {
        var parts = new List<string>();
        foreach (var field in fields)
        {
            if (field.Key.Equals(SignatureFieldName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = field.Value.Trim();
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            parts.Add($"{EncodeFormValue(field.Key.Trim())}={EncodeFormValue(value)}");
        }

        if (!string.IsNullOrWhiteSpace(passphrase))
        {
            parts.Add($"passphrase={EncodeFormValue(passphrase.Trim())}");
        }

        return string.Join("&", parts);
    }

    private static string DecodeFormValue(string value) =>
        Uri.UnescapeDataString(value.Replace("+", " "));

    private static string EncodeFormValue(string value)
    {
        var encoded = WebUtility.UrlEncode(value) ?? string.Empty;
        var builder = new StringBuilder(encoded.Length);

        for (var i = 0; i < encoded.Length; i++)
        {
            if (encoded[i] == '%' && i + 2 < encoded.Length)
            {
                builder.Append('%');
                builder.Append(char.ToUpperInvariant(encoded[i + 1]));
                builder.Append(char.ToUpperInvariant(encoded[i + 2]));
                i += 2;
                continue;
            }

            builder.Append(encoded[i]);
        }

        return builder.ToString();
    }
}
