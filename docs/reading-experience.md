# Navigation and the reading surfaces

## Android is the platform this is aimed at

The system back gesture unwinds the page stack, then returns to the feed from another section, then
reports itself unhandled so Android can background the app — swallowing it at the root would trap
you in a client you cannot back out of. That needs `android:enableOnBackInvokedCallback="true"` in
the manifest, without which none of it runs: an app targeting SDK 35+ gets back through the
predictive-back callback only, and the legacy `Activity.onBackPressed` it would otherwise arrive on
is never called. The app targets SDK 36, so Android insets the shell for the status bar and gesture
pill. Release builds are profiled-AOT and fully trimmed, and are what to sideload.

## Pull to refresh, on the surfaces that scroll

The feed and the community directory are wrapped in a `RefreshContainer` that holds its deferral
until the fetch actually finishes, so the spinner lasts as long as the work does. Both reload by
fetching first and swapping second, which keeps the rows on screen through the round trip and means
a refresh that fails costs the reader the update rather than the list. The gesture stays off for
mouse users — on desktop the toolbar button is the affordance.

## Coming back lands where you left

Returning from a post restores the feed to the row you were reading, not the top. The position is
remembered as a row index plus how far that row was cut off, never as a pixel offset: a virtualising
panel's scroll extent is an estimate that shifts as rows are realised, so the same pixel means
something different before and after a navigation. Restoring takes several layout passes — realise,
measure, correct — and only ever moves the offset directly, because `ScrollIntoView` schedules its
scroll rather than applying it and mixing the two makes each correction land on a jump that has not
happened yet.

## Voting happens before the server agrees

The arrows and the score are one small view model shared by the feed row, the post header and every
comment, because the behaviour is identical in all three and three copies of it would drift.

Pressing an arrow moves the score immediately and sends the vote afterwards. Waiting for the round
trip first feels broken on a phone, and the number was only ever approximately right — other people
are voting on the same post while you look at it. When the server answers, its counts replace the
guess outright rather than being merged with it; when it refuses, the previous state is put back
exactly as it was and the reason appears beside the arrows. Re-deriving the old numbers by undoing
the arithmetic would compound the guess instead of discarding it, so the whole prior value is kept
and restored.

Pressing the arrow already chosen retracts the vote, which Lemmy models as a score of `0` rather
than as a delete. Switching sides is one request, not a retraction followed by a vote, and moves the
score by two.

The arrows only exist when somebody is signed in. Signed out the score stays the plain badge it
always was, rather than controls that are visibly present and do nothing.

## Links go to the browser

A link post's URL is a control rather than a line of text, and every post also offers "open on the
web" — which matters more in a client that only reads than it would elsewhere, because voting,
commenting and subscribing all live on the other side of that link. Launching goes through
`ILinkOpener`, so the view models stay testable; the implementation is handed the window once the
shell is on screen, since a desktop window and a mobile activity's view get there by different
routes and the launcher hangs off the window either way.
