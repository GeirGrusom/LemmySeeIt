using System.Buffers;
using System.Globalization;
using System.Text;

namespace Lemmy.Api;

/// <summary>
/// Builds a percent-encoded query string into a caller-supplied stack buffer, growing into a pooled
/// array only if the caller guessed too small. Feeds re-issue near-identical requests every few
/// seconds while scrolling, so the usual path here allocates exactly one string: the finished URL.
/// </summary>
internal ref struct QueryStringBuilder : IDisposable
{
    private const int Utf8StackBudget = 256;

    private Span<char> buffer;
    private char[]? rented;
    private int position;
    private bool hasParameter;

    /// <summary>Starts a builder over <paramref name="initialBuffer"/>, typically a <c>stackalloc</c>.</summary>
    public QueryStringBuilder(Span<char> initialBuffer)
    {
        buffer = initialBuffer;
        rented = null;
        position = 0;
        hasParameter = false;
    }

    /// <summary>The query string so far, including the leading <c>?</c>; empty when nothing was added.</summary>
    public readonly ReadOnlySpan<char> Span => buffer[..position];

    /// <summary>Appends <c>name=value</c>, percent-encoding the value.</summary>
    public void Append(scoped ReadOnlySpan<char> name, scoped ReadOnlySpan<char> value)
    {
        WriteSeparator();
        Write(name);
        Write('=');
        WriteEncoded(value);
    }

    /// <summary>Appends <c>name=value</c> for anything that can format itself into a span.</summary>
    /// <remarks>
    /// Domain identifiers implement <see cref="ISpanFormattable"/> precisely so this path never has
    /// to materialise an intermediate string per parameter.
    /// </remarks>
    public void Append<TValue>(scoped ReadOnlySpan<char> name, TValue value)
        where TValue : ISpanFormattable
    {
        WriteSeparator();
        Write(name);
        Write('=');

        int written;
        while (!value.TryFormat(buffer[position..], out written, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture))
        {
            Grow(32);
        }

        position += written;
    }

    /// <summary>Appends <c>name=true</c> or <c>name=false</c>, the spelling Lemmy expects.</summary>
    public void Append(scoped ReadOnlySpan<char> name, bool value) => Append(name, value ? "true" : "false");

    /// <summary>Renders the query string, including the leading <c>?</c>.</summary>
    public override readonly string ToString() => new(Span);

    /// <summary>Returns the growth buffer to the pool.</summary>
    public void Dispose()
    {
        char[]? toReturn = rented;
        this = default;

        if (toReturn is not null)
        {
            ArrayPool<char>.Shared.Return(toReturn);
        }
    }

    private void WriteSeparator()
    {
        Write(hasParameter ? '&' : '?');
        hasParameter = true;
    }

    private void Write(char value)
    {
        if (position == buffer.Length)
        {
            Grow(1);
        }

        buffer[position++] = value;
    }

    private void Write(scoped ReadOnlySpan<char> value)
    {
        if (value.Length > buffer.Length - position)
        {
            Grow(value.Length);
        }

        value.CopyTo(buffer[position..]);
        position += value.Length;
    }

    /// <summary>
    /// Percent-encodes per RFC 3986. The text is transcoded to UTF-8 in one go rather than
    /// character by character: search terms arrive in every script there is, and a surrogate pair
    /// encoded half at a time produces bytes no server can decode.
    /// </summary>
    private void WriteEncoded(scoped ReadOnlySpan<char> value)
    {
        int maxBytes = Encoding.UTF8.GetMaxByteCount(value.Length);
        byte[]? rentedBytes = maxBytes > Utf8StackBudget ? ArrayPool<byte>.Shared.Rent(maxBytes) : null;
        Span<byte> utf8 = rentedBytes is not null ? rentedBytes.AsSpan() : stackalloc byte[Utf8StackBudget];

        try
        {
            int byteCount = Encoding.UTF8.GetBytes(value, utf8);
            WriteEncoded(utf8[..byteCount]);
        }
        finally
        {
            if (rentedBytes is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedBytes);
            }
        }
    }

    private void WriteEncoded(scoped ReadOnlySpan<byte> utf8)
    {
        foreach (byte value in utf8)
        {
            if (IsUnreserved(value))
            {
                Write((char)value);
            }
            else if (value == (byte)' ')
            {
                Write('+');
            }
            else
            {
                Write('%');
                Write(HexDigit(value >> 4));
                Write(HexDigit(value & 0xF));
            }
        }
    }

    private static bool IsUnreserved(byte value) =>
        char.IsAsciiLetterOrDigit((char)value) || value is (byte)'-' or (byte)'.' or (byte)'_' or (byte)'~';

    private static char HexDigit(int nibble) => (char)(nibble < 10 ? '0' + nibble : 'A' + (nibble - 10));

    private void Grow(int additional)
    {
        int required = Math.Max(buffer.Length * 2, position + additional);
        char[] replacement = ArrayPool<char>.Shared.Rent(required);

        buffer[..position].CopyTo(replacement);

        char[]? toReturn = rented;
        buffer = replacement;
        rented = replacement;

        if (toReturn is not null)
        {
            ArrayPool<char>.Shared.Return(toReturn);
        }
    }
}
