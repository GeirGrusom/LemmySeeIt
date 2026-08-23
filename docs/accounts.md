# Accounts, tokens and flagged content

## Signing in

Optional, and the app is fully usable without it. Signing in adds the **Subscribed** feed — the
reason most people bother — and shows the votes you have already cast.

`POST /api/v3/user/login` returns a bearer token that every later request carries. Two things about
that token drive the design. It **never expires**, so signing out calls `/api/v3/user/logout` rather
than merely forgetting it locally — a token only forgotten stays usable by anyone holding a copy.
And a dead token is **not rejected**: Lemmy answers a request carrying one with a perfectly normal
200 that simply omits the account, so "is this session still good" is a content check, not a status
code. The password is used for one request and never stored.

Signing out lives on the account's own page rather than in the header. It cannot be undone — the
token is invalidated server-side, so getting back in means typing a password — and the header shifts
as the account name replaces "Sign in", which makes a stray tap easy. One landed during testing and
ended a live session, which is why it works this way now. It first moved behind a sheet; the sheet
became the profile page, which is a better home for it than a modal whose only purpose was to be an
obstacle.

Signing out takes the page with it: rebuilding the sections discards the back stack, so the reader
lands on the feed rather than on a page describing an account they are no longer signed in as, whose
own button would be left offering to sign out again.

## Anybody's page

`GET /api/v3/user` answers with the account, its counts, and the most recent of what it wrote. The
comments come back as a flat list rather than as threads, so they are modelled that way — a
`ProfileCommentViewModel` with no replies, no reply box and none of the author's own controls, which
is what a list of things somebody wrote actually is. What it offers instead is the way back: the
comment carries only its post's identifier, so opening one fetches that post first.

Every byline leads here — a feed row, a post, a comment, a reply. The row view models take the
navigator rather than another callback threaded down from the page, because opening a person needs
no page context at all: there is no gallery to move through and no list to come back to, just an
identifier. Whether the page offers to sign out is decided by the shell, comparing the person to the
signed-in account, rather than by whichever byline was tapped — so a byline that happens to be your
own opens the same page with the button, and everyone else's opens it without.

## Which servers to offer

The picker lists servers the reader has used, then a dozen well-known ones they have not. The
suggestions are baked into the app rather than fetched: the lists that rank Lemmy instances live on
third-party aggregators, and calling one every time the picker opens would add a dependency on
somebody else's uptime, tell them the app is running, and leave the picker empty offline — to
populate a list the reader overwrites as soon as they choose anything. The cost is staleness, which
is written down where the list is.

Switching signs the reader out, because a token belongs to one server and Lemmy invalidates it
properly rather than just forgetting it. A tap in a scrolling list is far too little ceremony for
that, so while signed in the tap asks first and the switch waits for an answer. Signed out there is
nothing to lose and it happens immediately.

Remembering the servers meant putting an `ImmutableArray` on `AppSettings`, which quietly broke that
record's equality: a record compares members with the default comparer, and for `ImmutableArray` that
is reference equality on the array behind it. Two settings holding the same servers would have
compared unequal, and `AppSettings` is compared with `==` to decide whether a change is worth saving
and re-applying. It has hand-written `Equals` and `GetHashCode` for that reason.

## Offering to sign up

The API has no field naming a registration page — nothing in `/api/v3/site` points at one. What it
does carry is `registration_mode`, so the sheet can say whether the instance is **open**, wants an
**application** an admin will read, or is **closed**, and can withhold the offer entirely in the
last case. It also reports whether an email address has to be confirmed, which is worth knowing
before leaving the app rather than after.

The link itself goes to `/signup`, the route Lemmy's own frontend uses. That is a convention rather
than a contract, and it does not hold everywhere: an instance running a different frontend can
answer that path with something else — `lemmy.blahaj.zone` serves a "choose your interface" page —
in which case the reader lands on that server's front door instead of its registration form. Still
the right server, one click from the right page, and there is nothing better to point at.

When the instance cannot be reached, the offer stays hidden rather than appearing with a guess.
Silence is better than inviting somebody to register somewhere that has closed.

## Where the token is kept

The bar is that a token must never sit unencrypted on disk, so that a backup, a disk image or a
swept-up home directory yields nothing usable. Defending against code already running as you is
explicitly *not* the goal — no desktop can — which is why each platform's own keychain is the answer
rather than anything home-made.

| Platform | Where | Why a bulk grab gets nothing |
|---|---|---|
| Linux | Secret Service via libsecret | never our file; the keychain is encrypted at rest and unlocked by your login |
| Windows | file encrypted with DPAPI, `CurrentUser` | key belongs to the Windows account and never leaves the machine |
| Android | AES-GCM under a key in the device keystore | the key is non-exportable and hardware-backed; a device image yields ciphertext and no key |
| Nothing available | memory only | signing in again next launch beats a file that only looks protected |

The last row is deliberate. A plain file with tight permissions still hands a working session to
anything that copies a home directory, so the fallback keeps nothing at all.

libsecret is called through its `*v_sync` entry points rather than the friendlier ones, because
those are C variadic functions and P/Invoke does not reliably support them — on x86-64 a variadic
callee reads a register the runtime does not set. The `v` forms take a hash table instead.

## Content flagged NSFW

Off by default, and switchable in Settings. Two things had to be true for this to work at all, and
neither was: there was no switch anywhere in the app, and the app sent `show_nsfw=false` on every
request — which **overrides the account's own preference**, so someone who had turned it on through
their instance's web interface still saw nothing here.

So signing in now adopts `show_nsfw` and `blur_nsfw` from the account and saves them, and the switch
in Settings takes over from there. Turning it on refetches rather than unfiltering what is already
loaded, because the filtering happens on the server.
