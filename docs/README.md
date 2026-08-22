# Design notes

Why the app is built the way it is. These are background reading — nothing here is needed to build
or use it, which the [main README](../README.md) covers.

- [Architecture](architecture.md) — domain types, immutability, AOT, the API surface
- [Navigation and the reading surfaces](reading-experience.md) — back, refresh, scroll restoration, browser links
- [Pictures](images.md) — the viewer, prefetch, formats, animation, the thumbnail cache
- [Markdown](markdown.md) — parsing behind an own model, and hit-testing links
- [Accounts, tokens and flagged content](accounts.md) — sign-in, per-platform token storage, NSFW
