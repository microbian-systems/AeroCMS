using System.Globalization;
using System.Text;

namespace Aero.Cms.Modules.Ai.Services;

/// <summary>
/// Projects one JSON string property from incrementally received provider output.
/// </summary>
/// <remarks>
/// This projector exists only to provide an early, read-only suggestion preview. The complete
/// provider response is still parsed and validated by <see cref="EnhanceContentAgentOutputParser"/>
/// before the suggestion can be applied.
/// </remarks>
internal sealed class StreamingJsonStringProjector(string propertyName)
{
    private readonly StringBuilder _prefix = new();
    private bool _started;
    private bool _completed;
    private bool _escaped;
    private int _unicodeDigitsRemaining;
    private int _unicodeValue;

    /// <summary>
    /// Adds a provider response fragment and returns newly decoded property text.
    /// </summary>
    public string Append(string fragment)
    {
        if (_completed || string.IsNullOrEmpty(fragment))
        {
            return string.Empty;
        }

        if (!_started)
        {
            _prefix.Append(fragment);
            var source = _prefix.ToString();
            var propertyToken = $"\"{propertyName}\"";
            var propertyIndex = source.IndexOf(propertyToken, StringComparison.OrdinalIgnoreCase);
            if (propertyIndex < 0)
            {
                return string.Empty;
            }

            var colonIndex = source.IndexOf(':', propertyIndex + propertyToken.Length);
            if (colonIndex < 0)
            {
                return string.Empty;
            }

            var valueIndex = colonIndex + 1;
            while (valueIndex < source.Length && char.IsWhiteSpace(source[valueIndex]))
            {
                valueIndex++;
            }

            if (valueIndex >= source.Length || source[valueIndex] != '"')
            {
                return string.Empty;
            }

            _started = true;
            _prefix.Clear();
            return Decode(source.AsSpan(valueIndex + 1));
        }

        return Decode(fragment.AsSpan());
    }

    private string Decode(ReadOnlySpan<char> source)
    {
        var decoded = new StringBuilder(source.Length);
        foreach (var current in source)
        {
            if (_unicodeDigitsRemaining > 0)
            {
                if (!TryReadHexDigit(current, out var digit))
                {
                    decoded.Append('\uFFFD');
                    _unicodeDigitsRemaining = 0;
                    _unicodeValue = 0;
                    continue;
                }

                _unicodeValue = (_unicodeValue << 4) | digit;
                _unicodeDigitsRemaining--;
                if (_unicodeDigitsRemaining == 0)
                {
                    decoded.Append((char)_unicodeValue);
                    _unicodeValue = 0;
                }

                continue;
            }

            if (_escaped)
            {
                _escaped = false;
                switch (current)
                {
                    case '"':
                    case '\\':
                    case '/':
                        decoded.Append(current);
                        break;
                    case 'b':
                        decoded.Append('\b');
                        break;
                    case 'f':
                        decoded.Append('\f');
                        break;
                    case 'n':
                        decoded.Append('\n');
                        break;
                    case 'r':
                        decoded.Append('\r');
                        break;
                    case 't':
                        decoded.Append('\t');
                        break;
                    case 'u':
                        _unicodeDigitsRemaining = 4;
                        break;
                    default:
                        decoded.Append(current);
                        break;
                }

                continue;
            }

            if (current == '\\')
            {
                _escaped = true;
                continue;
            }

            if (current == '"')
            {
                _completed = true;
                break;
            }

            decoded.Append(current);
        }

        return decoded.ToString();
    }

    private static bool TryReadHexDigit(char value, out int digit)
    {
        if (int.TryParse(
                value.ToString(),
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture,
                out digit))
        {
            return true;
        }

        digit = 0;
        return false;
    }
}
