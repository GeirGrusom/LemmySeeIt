# LemmySeeIt

A reading client for [Lemmy](https://join-lemmy.org), the federated link-aggregator that Reddit
refugees landed on. Browse an instance's front page, drill into a community, read a comment thread,
search, and switch between servers. Sign in and you also get your subscribed feed.

It only reads: there is no way to post, vote, comment or subscribe from here. Each post links out to
its page on the web for that.

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

**Choosing a server.** The instance name in the header opens Settings, where you can type another
one — `lemmy.ml`, `beehaw.org`, `sh.itjust.works` or any other — and switch. Lemmy is many servers,
and posts, communities and accounts each belong to one of them. Your choice is remembered.

**The feed.** Two pickers set what you see: a sort (Active, Hot, Scaled, New, Top of the
day/week/month/all time, Most Comments) and a listing (All, or Local for communities hosted on the
server you are pointed at). Scrolling to the bottom loads more. Pull down to refresh on a touch
screen, or use the Refresh button.

**Reading a post.** Tap a row to open it with its comment thread, which sorts by Hot, Top, New, Old
or Controversial. **Open on the web** hands the post to your browser, which is where you go to vote,
comment or subscribe. Back returns to the feed at the row you were on, not the top.

**Pictures.** Tap the thumbnail on a feed row to open the picture full screen without opening the
post. Double-tap or pinch to zoom, drag to pan, and flick left and right to move through the other
pictures on the page — the feed keeps loading as you reach the end, so a comic or art community
reads a picture at a time. GIFs and animated WebP play here. Tap, press Escape or go back to close;
arrow keys work on the desktop heads.

**Communities and search.** Communities lists the directory for the current server, and Search
covers both posts and communities on it.

**Signing in** is optional — everything above works without it. An account adds the **Subscribed**
listing to the feed, which is the usual reason to bother, and shows the votes you have already cast.
The account control is in the header, next to the instance name; signing out lives inside the
account sheet so a stray tap cannot end your session.

**Licences.** Settings also links to the attribution page: this app's licence, the notice for the
typeface it embeds, and every third-party package it ships, each with its licence and copyright.

**Content flagged NSFW** is hidden by default. Settings has a switch for it, plus one to blur those
images until tapped. Signing in adopts whatever your account already has set.

## Limitations

- Voting, commenting and subscribing are not wired up; each post links out to the web instead.
- AVIF images do not decode, so those posts show the server's preview rather than the original.
- A comment thread is fetched eight levels deep in one request; "12 more replies" is shown but not
  yet loadable, and the thread has no pull-to-refresh.
- `@user@instance` and `!community@instance` mentions are not linkified, and images inside a post
  body render as a link rather than inline.
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
