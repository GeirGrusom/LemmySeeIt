using Lemmy.Api;
using Lemmy.Domain;
using Lemmy.Domain.Models;
using Lemmy.Tests.TestSupport;

namespace Lemmy.Tests.Api;

/// <summary>Reading an account's page off the wire.</summary>
[TestFixture]
internal sealed class PersonApiTests
{
    private const string PersonBody = """
        {"person_view":{"is_admin":true,
          "person":{"id":1,"name":"alice","display_name":"Alice","bio":"Reads more than posts.",
                    "actor_id":"https://lemmy.world/u/alice","local":true,"banned":false,"deleted":false,
                    "bot_account":false,"instance_id":1,"published":"2023-06-01T00:00:00"},
          "counts":{"person_id":1,"post_count":12,"comment_count":340}},
         "posts":[],
         "comments":[{"my_vote":0,
           "comment":{"id":11,"post_id":10,"creator_id":1,"content":"Said something","path":"0.11",
                      "ap_id":"https://lemmy.world/comment/11","language_id":37,
                      "removed":false,"deleted":false,"distinguished":false,
                      "published":"2026-08-22T10:00:00"},
           "creator":{"id":1,"name":"alice","actor_id":"https://lemmy.world/u/alice","published":"2023-06-01T00:00:00"},
           "counts":{"score":4,"upvotes":4,"downvotes":0,"child_count":0}}],
         "moderates":[]}
        """;

    [Test]
    public async Task ItAsksForThePersonAndTheirNewestFirst()
    {
        using var handler = StubHttpMessageHandler.Returning(PersonBody);
        var client = new LemmyApiClient(handler.CreateClient(), Sample.Instance);

        await client.GetPersonAsync(new PersonId(1));

        Assert.Multiple(() =>
        {
            Assert.That(handler.SingleRequestedUri.AbsolutePath, Is.EqualTo("/api/v3/user"));
            Assert.That(handler.SingleRequestedUri.Query, Does.Contain("person_id=1").And.Contain("sort=New"));
        });
    }

    [Test]
    public async Task ItReadsWhoTheyAreAndWhatTheyWrote()
    {
        using var handler = StubHttpMessageHandler.Returning(PersonBody);
        var client = new LemmyApiClient(handler.CreateClient(), Sample.Instance);

        PersonProfile profile = await client.GetPersonAsync(new PersonId(1));

        Assert.Multiple(() =>
        {
            Assert.That(profile.Person.PreferredName, Is.EqualTo("Alice"));
            Assert.That(profile.Person.QualifiedName, Is.EqualTo("@alice@lemmy.world"));
            Assert.That(profile.Bio.Value, Is.EqualTo("Reads more than posts."));
            Assert.That(profile.IsAdmin, Is.True);
            Assert.That(profile.Tally.Posts.Value, Is.EqualTo(12));
            Assert.That(profile.Tally.Comments.Value, Is.EqualTo(340));
            Assert.That(profile.Comments, Has.Length.EqualTo(1));
        });
    }

    [Test]
    public async Task AnAccountWithNoBioIsFineRatherThanMissing()
    {
        using var handler = StubHttpMessageHandler.Returning(
            PersonBody.Replace("\"bio\":\"Reads more than posts.\",", "", StringComparison.Ordinal));
        var client = new LemmyApiClient(handler.CreateClient(), Sample.Instance);

        PersonProfile profile = await client.GetPersonAsync(new PersonId(1));

        Assert.Multiple(() =>
        {
            Assert.That(profile.HasBio, Is.False);
            Assert.That(profile.Bio.Value, Is.Empty);
        });
    }

    [Test]
    public void AResponseWithNoAccountInItIsAFailureRatherThanAnEmptyPage()
    {
        using var handler = StubHttpMessageHandler.Returning("""{"posts":[],"comments":[]}""");
        var client = new LemmyApiClient(handler.CreateClient(), Sample.Instance);

        Assert.That(
            async () => await client.GetPersonAsync(new PersonId(1)),
            Throws.TypeOf<LemmyApiException>());
    }

    [Test]
    public async Task ProfileCommentsCarryNoRepliesBecauseTheResponseHasNone()
    {
        using var handler = StubHttpMessageHandler.Returning(PersonBody);
        var client = new LemmyApiClient(handler.CreateClient(), Sample.Instance);

        PersonProfile profile = await client.GetPersonAsync(new PersonId(1));

        // A list of what somebody wrote is not a thread; empty replies is the truth here.
        Assert.That(profile.Comments[0].Replies, Is.Empty);
    }
}
