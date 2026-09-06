# LemmySeeIt

A reading client for [Lemmy](https://join-lemmy.org), the federated link-aggregator that Reddit
refugees landed on. Browse an instance's front page, drill into a community, read a comment thread,
search, and switch between servers. Sign in and you also get your subscribed feed.

Signed in you can vote, subscribe to communities, comment — write one, reply to one, and edit or
delete your own — and post: write one, edit it, or delete it.

Runs on Windows, Linux and Android from one shared codebase, with an iOS head that is kept
compiling-ready.

```
Lemmy.slnx
├── src/
│   ├── Lemmy/            domain + API client + view models + views   (net10.0)
│   ├── Lemmy.Windows/    desktop head, win-x64, Native AOT           (net10.0)
│   ├── Lemmy.Linux/      desktop head, linux-x64, Native AOT         (net10.0)
│   ├── Lemmy.Android/    single-view head                            (net10.0-android)
│   └── Lemmy.iOS/        single-view head, Native AOT                (net10.0-ios)
└── tests/
    └── Lemmy.Tests/      NUnit + NSubstitute
```

## Building

Requires the .NET 10 SDK. The desktop heads and the tests need nothing else:

```sh
dotnet build src/Lemmy.Linux/Lemmy.Linux.csproj      # or Lemmy.Windows
dotnet test  tests/Lemmy.Tests/Lemmy.Tests.csproj
dotnet run   --project src/Lemmy.Linux/Lemmy.Linux.csproj
```

### Android

Needs the workload, a JDK 17+ and the Android SDK:

```sh
dotnet workload install android
dotnet build src/Lemmy.Android/Lemmy.Android.csproj -t:Install   # deploys to an attached device
```

Use `-t:Install`, not `adb install`, for Debug builds: they leave the managed assemblies out of the
APK and rely on the tooling pushing them over adb, so a hand-sideloaded Debug APK aborts on launch
with *"No assemblies found ... Assuming this is part of Fast Deployment"*. To get an APK that can be
sideloaded on its own, build Release — `dotnet publish -c Release` writes a self-contained
`com.lemmyseeit.app-Signed.apk` alongside the `.aab`.

If you have no Android SDK, the workload can fetch one — along with a JDK — without touching your
system packages:

```sh
dotnet build src/Lemmy.Android/Lemmy.Android.csproj -t:InstallAndroidDependencies \
  -p:AndroidSdkDirectory=$HOME/.android-sdk \
  -p:JavaSdkDirectory=$HOME/.jdk \
  -p:AcceptAndroidSDKLicenses=true
```

The build finds them through `ANDROID_HOME` and `JAVA_HOME`, or through an uncommitted
`Directory.Build.local.props` beside this file:

```xml
<Project>
  <PropertyGroup>
    <AndroidSdkDirectory>/home/you/.android-sdk</AndroidSdkDirectory>
    <JavaSdkDirectory>/home/you/.jdk</JavaSdkDirectory>
  </PropertyGroup>
</Project>
```

### iOS

Needs `dotnet workload install ios` and, to build at all, a Mac with Xcode — Apple's toolchain does
not run elsewhere. The head is kept compiling-ready rather than verified.

### Publishing

Windows and Linux publish as a single self-contained native binary with no .NET runtime to install:

```sh
dotnet publish src/Lemmy.Windows/Lemmy.Windows.csproj -c Release
dotnet publish src/Lemmy.Linux/Lemmy.Linux.csproj     -c Release
```

Native AOT on Linux needs a C toolchain and the zlib development package; on Windows it needs the
Visual Studio C++ build tools. `dotnet publish -c Release` on the Android head produces a
profiled-AOT, trimmed `.aab`.

### Tests

```sh
dotnet test tests/Lemmy.Tests/Lemmy.Tests.csproj
```

Domain validation and parsing, the query-string builder, the wire mapper and comment-tree builder
against response bodies copied from a live instance, the API client against a stubbed transport, the
settings store, and the view models against substituted services. The UI tests start the real `App`
on Avalonia's headless platform with Skia drawing, build each screen and assert on what appears —
compiled bindings catch typos at build time, but a missing resource key or a template that throws
only shows up once a control is realised. A few more assert codec support against real image files
checked into the repository, so they need no network.

## Using it

The app opens on **lemmy.world**. Three sections sit along the bottom: **Feed**, **Communities** and
**Search**.

**Choosing a server.** The instance name in the header opens Settings, which lists the servers you
have used and a dozen well-known ones to start from — or you can type any other address. Lemmy is
many servers, and posts, communities and accounts each belong to one of them, so switching signs you
out of the one you are on; while signed in the app asks before doing that. Your choice and the
servers you have visited are remembered.

**The feed.** Two pickers set what you see: a sort (Active, Hot, Scaled, New, Top of the
day/week/month/all time, Most Comments) and a listing (All, or Local for communities hosted on the
server you are pointed at). Scrolling to the bottom loads more. Pull down to refresh on a touch
screen, or use the Refresh button.

**Reading a post.** Tap a row to open it with its comment thread, which sorts by Hot, Top, New, Old
or Controversial. **Open on the web** hands the post to your browser, for the things this client
does not do. Back returns to the feed at the row you were on, not the top.

**Voting.** Signed in, every feed row, post and comment gets an up and a down arrow around its
score. Pressing the arrow you already chose takes the vote back. The score moves as you press it
rather than after the server answers, and moves back with a reason if the vote does not take.
Signed out the arrows are not shown at all — just the score, as before.

**Copying text.** Post bodies and comments have a **Copy** button, which puts the original Markdown
on the clipboard. Dragging a finger scrolls rather than selecting — a phone has no handles to adjust
a selection with and no menu to copy it from, so the button is the way. With a mouse, text still
selects by dragging and copies with Ctrl+C.

**Pictures in a body.** An image written into a post or comment appears folded away behind its
description — *Image — the chart* — and opens where you tap it. Once open, tapping the picture puts
it full screen with pinch-zoom. Nothing is downloaded until you ask for it, so a thread full of
images costs nothing to scroll past.

**Pictures.** Tap the thumbnail on a feed row to open the picture full screen without opening the
post. Double-tap or pinch to zoom, drag to pan, and flick up and down to move through the other
pictures on the page — the same direction the feed itself scrolls, and the controls sit above and
below the picture to match. The feed keeps loading as you reach the end, so a comic or art community
reads a picture at a time. GIFs and animated WebP play here. Tap, press Escape or go back to close;
the up and down arrow keys work on the desktop heads.

**Subscribing.** Signed in, the community directory, a community's own page, a post page and search
results all carry a subscribe button. Following a community on another server usually reads
**Pending** for a moment: the follow has to reach that server and be acknowledged. Feed rows from a
community you follow carry a green tick beside the community name, and subscribing anywhere updates
every row already on screen without a refresh. The **Subscribed** listing on the feed is what
following a community is for.

**Commenting.** Signed in, a box above the thread adds a comment to the post, and every comment has
a **Reply**. Your own comments also offer **Edit** and **Delete**; a deleted comment leaves its
placeholder in the thread — so the replies under it still hang off something — and you can
**Restore** it afterwards. Comments are written as Markdown, the same Markdown the app renders.

**Posting.** Signed in, the feed carries a **New post** button. From a community's own page the post
goes there; from the front page the form asks where first, offering the communities you follow and a
search box for anywhere else. A post needs a title; a link and a Markdown body are both optional, and
a link typed without `https://` gets one. Your own posts offer **Edit** and **Delete** on the post
page, and a deleted post can be **Restored** — Lemmy's delete is a flag rather than a removal.
A post cannot be moved between communities afterwards, which is Lemmy's rule rather than ours.

**Communities and search.** Signed in, Communities opens on the ones you follow; the picker also
offers All and Local, and signed out it opens on Local. Finding communities you do not already
follow is what Search is for — it covers both posts and communities on the current server.

**No account yet?** The sign-in sheet says whether the current server is taking new ones — some
require an admin to approve each application — and links out to its own sign-up page. Account
creation happens in a browser, because it can involve a captcha, an application to write and an
email to confirm.

**Profiles.** Tapping any author's name — in a feed row, on a post, on a comment — opens their page:
who they are, what they wrote about themselves, how much they have posted, and their recent posts
and comments. Tap a comment there to open the post it was written on.

**Your profile.** Tapping your account name in the header opens your own page: who you are, what you
wrote about yourself, how much you have posted, and your recent posts and comments — tap a comment
to open the post it was written on. Signing out lives here.

**Signing in** is optional — everything above works without it. An account adds the **Subscribed**
listing to the feed, shows the votes you have already cast, and is what turns the vote arrows on.
The account control is in the header, next to the instance name; signing out lives inside the
profile page so a stray tap cannot end your session.

**Notifications.** When somebody replies to you or writes your name into a comment, a count appears
to the left of your account name in the header. Tapping it lists them newest first, whichever of the
two Lemmy keeps them in. Each one shows what was written and the post it was written on; opening one
marks it read, and there is **Mark all read** for the rest. The list starts on unread only and can
be switched to everything, and it pulls down to refresh.

**Licences.** Settings also links to the attribution page: this app's licence, the notice for the
typeface it embeds, and every third-party package it ships, each with its licence and copyright.

**Content flagged NSFW** is hidden by default. Settings has a switch for it, plus one to blur those
images until tapped. Signing in adopts whatever your account already has set.

## Limitations

- Post and comment boxes are plain Markdown text areas: no formatting toolbar and no preview.
- Posts carry text and a link. There is no image upload, so a picture post means linking one that is
  already hosted somewhere.
- Editing or deleting a post updates the post's own page; a feed already on screen behind it still
  shows what it showed before until refreshed.
- AVIF images do not decode. Most posts fall back to the server's preview; on an instance whose
  pict-rs is set to AVIF the preview is AVIF too, and the picture then says which format defeated it
  rather than claiming it could not be loaded.
- A comment thread is fetched eight levels deep in one request. Deeper replies are fetched on
  demand — the count of what is missing is the control that fetches it. Pulling the page down
  re-reads the thread; the post above it keeps the counts it arrived with.
- `@user@instance` and `!community@instance` mentions are not linkified.
- The notification count leaves out private messages. The server counts them, but there is nowhere
  here to read one, so counting them would show a number nothing could clear.
- Nothing polls for notifications. The count is fetched at launch, on signing in, and on leaving the
  notification list — so a reply arriving while you read shows up the next time one of those happens.
- The iOS head keeps a session in memory only — it has no keychain support yet, so signing in does
  not survive a restart there.

## Design notes

Why it is built this way — domain typing, AOT, the image pipeline, token storage — is in
[docs/](docs/README.md).

## License

[MIT](LICENSE). Every dependency is permissive too: MIT for most, Apache-2.0 for the AndroidX
bindings the Android head carries, BSD-2-Clause for Markdig, and BSD-3-Clause for NSubstitute, which
is test-only. The embedded Inter typeface is under the [SIL Open Font License](licenses/Inter-OFL-1.1.txt)
rather than the MIT of the package that ships it.

The app carries all of this itself, under **Settings → Licences and attribution**: its own licence,
the font notice, and every package with its version, licence and copyright. That list is generated
during the build from the packages the build actually ships, so it cannot drift from the binary —
see [docs/architecture.md](docs/architecture.md#attribution-is-generated-not-maintained).
