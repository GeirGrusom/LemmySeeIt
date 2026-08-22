using System.Text.Json;
using System.Text.Json.Serialization;
using Lemmy.Api.Dto;

namespace Lemmy.Api;

/// <summary>
/// The source-generated serializer for every Lemmy wire type. Reflection-based serialization is
/// switched off repository-wide, so a type that is not listed here will fail to serialize rather
/// than silently costing us Native AOT compatibility.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(GetPostsResponse))]
[JsonSerializable(typeof(GetPostResponse))]
[JsonSerializable(typeof(GetCommentsResponse))]
[JsonSerializable(typeof(ListCommunitiesResponse))]
[JsonSerializable(typeof(SearchResponse))]
[JsonSerializable(typeof(GetSiteResponse))]
[JsonSerializable(typeof(LoginResponse))]
[JsonSerializable(typeof(LoginRequestWire))]
[JsonSerializable(typeof(ErrorResponse))]
internal sealed partial class LemmyJsonContext : JsonSerializerContext;

/// <summary>Serializer options shared by every request the client makes.</summary>
internal static class LemmyJson
{
    /// <summary>
    /// The context, with the lenient timestamp converter attached. Built once: constructing a
    /// context per request would re-run the generated type resolution on every page of a feed.
    /// </summary>
    internal static LemmyJsonContext Context { get; } = new(CreateOptions());

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        options.Converters.Add(new LenientTimestampConverter());
        return options;
    }
}
