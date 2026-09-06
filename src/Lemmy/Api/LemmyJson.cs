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
[JsonSerializable(typeof(GetPersonDetailsResponse))]
[JsonSerializable(typeof(GetSiteResponse))]
[JsonSerializable(typeof(PostResponse))]
[JsonSerializable(typeof(CommunityResponse))]
[JsonSerializable(typeof(CreatePostRequestWire))]
[JsonSerializable(typeof(EditPostRequestWire))]
[JsonSerializable(typeof(DeletePostRequestWire))]
[JsonSerializable(typeof(CreateCommentRequestWire))]
[JsonSerializable(typeof(EditCommentRequestWire))]
[JsonSerializable(typeof(DeleteCommentRequestWire))]
[JsonSerializable(typeof(FollowCommunityRequestWire))]
[JsonSerializable(typeof(CommentResponse))]
[JsonSerializable(typeof(VotePostRequestWire))]
[JsonSerializable(typeof(VoteCommentRequestWire))]
[JsonSerializable(typeof(LoginResponse))]
[JsonSerializable(typeof(LoginRequestWire))]
[JsonSerializable(typeof(GetUnreadCountResponse))]
[JsonSerializable(typeof(GetRepliesResponse))]
[JsonSerializable(typeof(GetPersonMentionsResponse))]
[JsonSerializable(typeof(CommentReplyResponse))]
[JsonSerializable(typeof(PersonMentionResponse))]
[JsonSerializable(typeof(MarkCommentReplyReadRequestWire))]
[JsonSerializable(typeof(MarkPersonMentionReadRequestWire))]
[JsonSerializable(typeof(MarkAllReadRequestWire))]
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
        // These have to repeat what JsonSourceGenerationOptions declares: constructing the context
        // with options replaces the attribute's settings rather than adding to them, so anything
        // omitted here silently reverts to the default. Leaving out DefaultIgnoreCondition once meant
        // every optional field went out as an explicit null.
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        options.Converters.Add(new LenientTimestampConverter());
        return options;
    }
}
