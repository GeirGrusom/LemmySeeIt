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

Signing out is deliberately behind the account sheet rather than the header control. It cannot be
undone — the token is invalidated server-side, so getting back in means typing a password — and the
header shifts as the account name replaces "Sign in", which makes a stray tap easy. One landed
during testing and ended a live session, which is why it works this way now.

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
