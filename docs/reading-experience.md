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

## Links go to the browser

A link post's URL is a control rather than a line of text, and every post also offers "open on the
web" — which matters more in a client that only reads than it would elsewhere, because voting,
commenting and subscribing all live on the other side of that link. Launching goes through
`ILinkOpener`, so the view models stay testable; the implementation is handed the window once the
shell is on screen, since a desktop window and a mobile activity's view get there by different
routes and the launcher hangs off the window either way.
