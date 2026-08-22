using Lemmy.Api;
using Lemmy.Domain;

namespace Lemmy.Tests.Api;

[TestFixture]
internal sealed class QueryStringBuilderTests
{
    [Test]
    public void FirstParameterGetsAQuestionMarkAndTheRestGetAmpersands()
    {
        using var builder = new QueryStringBuilder(stackalloc char[128]);

        builder.Append("a", "1");
        builder.Append("b", "2");
        builder.Append("c", "3");

        Assert.That(builder.ToString(), Is.EqualTo("?a=1&b=2&c=3"));
    }

    [Test]
    public void AnEmptyBuilderProducesNothingNotABareQuestionMark()
    {
        using var builder = new QueryStringBuilder(stackalloc char[16]);

        Assert.That(builder.ToString(), Is.Empty);
    }

    [Test]
    public void SpanFormattableValuesAreWrittenWithoutAnIntermediateString()
    {
        using var builder = new QueryStringBuilder(stackalloc char[128]);

        builder.Append("post_id", new PostId(50908658));
        builder.Append("limit", PageSize.Clamp(50));

        Assert.That(builder.ToString(), Is.EqualTo("?post_id=50908658&limit=50"));
    }

    [Test]
    public void BooleansUseTheSpellingLemmyExpects()
    {
        using var builder = new QueryStringBuilder(stackalloc char[64]);

        builder.Append("show_nsfw", false);
        builder.Append("saved_only", true);

        Assert.That(builder.ToString(), Is.EqualTo("?show_nsfw=false&saved_only=true"));
    }

    [Test]
    public void ReservedCharactersArePercentEncoded()
    {
        using var builder = new QueryStringBuilder(stackalloc char[128]);

        builder.Append("q", "a&b=c?d#e/f");

        Assert.That(builder.ToString(), Is.EqualTo("?q=a%26b%3Dc%3Fd%23e%2Ff"));
    }

    [Test]
    public void UnreservedCharactersPassThroughUntouched()
    {
        using var builder = new QueryStringBuilder(stackalloc char[128]);

        builder.Append("q", "abcXYZ019-._~");

        Assert.That(builder.ToString(), Is.EqualTo("?q=abcXYZ019-._~"));
    }

    [Test]
    public void SpacesBecomePlusSigns()
    {
        using var builder = new QueryStringBuilder(stackalloc char[64]);

        builder.Append("q", "linux gaming");

        Assert.That(builder.ToString(), Is.EqualTo("?q=linux+gaming"));
    }

    /// <summary>
    /// A search term encoded one UTF-16 unit at a time would split a surrogate pair into two
    /// undecodable byte sequences. Emoji and non-Latin scripts are ordinary search input.
    /// </summary>
    [Test]
    public void NonAsciiTextIsEncodedAsUtf8Bytes()
    {
        using var builder = new QueryStringBuilder(stackalloc char[128]);

        builder.Append("q", "café");

        Assert.That(builder.ToString(), Is.EqualTo("?q=caf%C3%A9"));
    }

    [Test]
    public void SurrogatePairsSurviveEncoding()
    {
        using var builder = new QueryStringBuilder(stackalloc char[128]);

        builder.Append("q", "\U0001F600");

        Assert.That(builder.ToString(), Is.EqualTo("?q=%F0%9F%98%80"));
    }

    /// <summary>The stack buffer is a guess; overrunning it must grow, not corrupt or throw.</summary>
    [Test]
    public void TheBufferGrowsWhenTheInitialGuessIsTooSmall()
    {
        using var builder = new QueryStringBuilder(stackalloc char[4]);

        string value = new('x', 500);
        builder.Append("q", value);
        builder.Append("limit", new PostId(42));

        Assert.That(builder.ToString(), Is.EqualTo($"?q={value}&limit=42"));
    }

    [Test]
    public void GrowingDuringANumericAppendStillProducesTheWholeNumber()
    {
        using var builder = new QueryStringBuilder(stackalloc char[8]);

        builder.Append("id", new PostId(2147483647));

        Assert.That(builder.ToString(), Is.EqualTo("?id=2147483647"));
    }
}
