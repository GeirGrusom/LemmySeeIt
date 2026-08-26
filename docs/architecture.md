# Architecture

One shared project holds the domain model, the API client and the whole user interface; each
platform gets a thin head that starts it.

## Domain types instead of primitives

A post id, a comment id and a community id are all `int` on the wire, and passing one where another
belongs is the kind of bug that produces a plausible-looking wrong page rather than an exception.
Each is a `readonly record struct` that validates on the way in — `PostId`, `CommentId`,
`CommunityId`, `PersonId`, `InstanceAddress`, `CommunityName`, `Username`, `ActorId`, `WebLink`,
`CommentPath`, `PageCursor`, `PostTitle`, `MarkdownText`, `Score`, `VoteCount`, `PageSize`,
`CommentDepth`, `SearchTerm`. Every one offers a throwing constructor and a non-throwing
`TryCreate`/`TryParse`; anything that came off the wire or out of a text box uses the latter.

Identifiers also implement `ISpanFormattable`, which is not decoration: it lets `QueryStringBuilder`
write them straight into a stack buffer, so composing a request URL allocates one string rather than
one per parameter.

## Immutable by default

Domain models are records, collections are `ImmutableArray<T>`, and requests are
`readonly record struct`s varied with `with`. Public signatures name concrete collection types
rather than interfaces, so a caller can see — and a JIT can inline — what it is really holding. The
mutable exceptions are the `ObservableCollection<T>`s a list control binds to.

## Ahead-of-time compatible throughout

`IsAotCompatible` is on for every non-test project, so the trim and AOT analysers run on every
build. Two things follow from that: JSON is source-generated and reflection-based serialization is
switched off repository-wide, so an unregistered type fails the build instead of quietly costing AOT
compatibility; and the `ViewLocator` is an explicit switch rather than the usual name-based
`Type.GetType` lookup, which trimming would remove.

## Tolerant of what federation actually sends

A post that cannot be mapped is dropped from the page rather than failing it, over-long titles are
clipped rather than rejected, timestamps missing their `Z` are read as UTC rather than as local
time, and unreadable settings fall back field by field. Each of those is a test.

## Comment threads are rebuilt, not received

Lemmy sends a thread as a flat list plus a materialised path (`0.100.200`) on each comment.
`CommentPath` parses that with spans and `CommentTreeBuilder` turns it into a tree, preserving the
server's ordering within each level. A comment whose parent is absent becomes a root, which is what
makes a "load more replies" sub-thread renderable on its own.

## Attribution is generated, not maintained

The licences page lists every third-party package the build ships, and nobody keeps that list by
hand. An MSBuild task in `build/Attribution.targets` reads the packages MSBuild resolved for the
project and emits a C# file of `PackageAttribution` values into `obj`, compiled in like any other
source.

It reads `RuntimeCopyLocalItems` and `NativeCopyLocalItems` rather than `PackageReference`. Those
are the assets that actually end up beside the binary, which gets three things right at once:
transitive dependencies are covered, build-only tooling such as ILLink and the AOT compiler is
excluded because none of it ships, and native asset packs for other platforms are left out — a
Linux build does not claim to carry the macOS Skia binaries. Each package's licence, copyright and
project page come from its `.nuspec` in the NuGet cache, so the build needs no network.

Every head ships a different set, so each generates its own and hands it over at startup through
`Attribution.Use`. The desktop heads carry X11 and D-Bus; the Android head carries AndroidX, which
is why it lists around 75 packages where the shared project alone has 13. The same mechanism as
`App.SessionStoreOverride`, and for the same reason: the shared UI cannot know what its host
brought with it.

Anything a package declines to declare is shown as missing rather than guessed at — a package with
no licence expression is listed as undeclared with a link to its project page, because inventing an
answer there would be worse than admitting the gap. Licence *texts* are embedded from `licenses/`
and shown once each, since the body of MIT is the same for every package under it and only the
copyright line differs.

## What it talks to

Lemmy's HTTP API v3 — `post/list`, `post`, `comment/list`, `community/list`, `search` and `site` for
reading, anonymously unless signed in; `post`, `post/like`, `post/delete`, `comment`,
`comment/like`, `comment/delete` and `community/follow` for writing, which always needs a session. That is the version every current instance serves; instances running
Lemmy 1.0 also expose a v4, and `LemmyApiClient` is the only file that would need to know.
