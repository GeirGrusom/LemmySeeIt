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

## Pinching to zoom

Two things about Avalonia's pinch gesture are easy to read wrongly, and both were. `Scale` is the
distance between the fingers over their distance when the gesture started — one number that grows
through the gesture, not a per-event delta — so passing it straight to a relative zoom multiplied it
by itself on every event and the picture slammed to maximum on the first pinch. And `ScaleOrigin` is
already in pixels in the target's coordinates; multiplying it by the viewport size, as though it
were a fraction, threw the origin thousands of pixels past the corner, and the edge clamp then
pinned the picture so that its bottom-right corner filled the screen.

The geometry in `ZoomState` was right throughout. Both bugs were in reading the gesture, which is an
argument for the split: the part that is hard to get right is testable without a finger on a screen,
and the part that needed a device was small enough to inspect once the symptoms named it.

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

## Subscribing has to agree with itself in several places at once

A community is on screen in more places than one: a dozen feed rows, the directory, search results,
the post it was opened from, its own page. Every one of those was told the subscription state by the
response that carried it, so subscribing in any of them would leave all the others claiming the
opposite until something refetched.

`SubscriptionTracker` holds what the app has learned since. Each control asks it for the state,
falling back to what its own response said, and is told when that community changes. The result is
that following a community from its page marks the feed rows behind it immediately. The tracker is
emptied whenever the instance changes or the session ends, because community ids are instance-local
and one server's answers must never be attached to another's communities.

A feed row gets a tick rather than a button. A subscribe control on every row of a scrolling feed is
a column of mis-taps waiting to happen, and the community's own page is a tap away.

Following a remote community normally comes back **Pending** rather than subscribed — the follow has
to travel to the community's home instance and be acknowledged, which is not instant and is not a
failure. Pending counts as following, so the button unsubscribes rather than trying to follow twice.
The optimistic state is Pending too, for the same reason: it is the honest guess.

## Writing a comment

One composer serves all three jobs — a new comment, a reply, an edit — because they differ only in
what the button says and where the result goes.

What it must never do is lose what was typed. The box is cleared only after the server has accepted
the comment, never before, and a failure leaves both the text and the reason on screen. That is not
hypothetical: lemmy.world refuses posting from VPN exit nodes with a 401, and the first real comment
this app tried to make came back exactly that way.

Editing takes the comment back from the server and keeps the node's replies. The edit and delete
endpoints answer with the comment alone, and adopting their whole view would replace a node that has
a thread under it with one that has none.

Deleting is a flag rather than a removal, which is Lemmy's own model: the comment keeps its place so
that replies below it still have a parent, and the reader sees the placeholder. Because it is a
flag, it can be turned off again, so the author gets a **Restore** where the delete used to be. An
irreversible destructive action one tap away from an edit button is a bad trade for the one line of
code that undoes it.

A comment shows Edit and Delete only to the account that wrote it, matched by person id. A session
restored while offline knows its own name but not its id, and an unknown id owns nothing — better to
withhold the buttons from the rightful owner for one launch than to offer them on somebody else's
comment.

## Selecting text, and not selecting it

Avalonia reports a touch contact as a pressed left button, so `SelectableTextBlock` treats the start
of a scroll as the start of a selection: dragging a thread left a line highlighted behind it. And
none of what makes a selection worth having exists on a phone — no drag handles to adjust it, no
long-press menu to copy it — so the reader got the cost without the feature.

`SelectableText` ignores touch for selection purposes and leaves the events unhandled, which is what
lets the surrounding scroll gesture carry on. The check is on the pointer rather than the platform,
so a touchscreen laptop still selects with its mouse and scrolls with a finger.

That leaves copying, which a finger then cannot do at all, so post bodies and comments carry a Copy
button. It copies the Markdown the author wrote rather than the rendered text, because that is what
can be pasted back into a reply and still mean the same thing. The button says "Copied" afterwards:
the clipboard gives no feedback of its own, and a button that appears to do nothing reads as broken.

## Links go to the browser

A link post's URL is a control rather than a line of text, and every post also offers "open on the
web" — which matters more in a client that only reads than it would elsewhere, because voting,
commenting and subscribing all live on the other side of that link. Launching goes through
`ILinkOpener`, so the view models stay testable; the implementation is handed the window once the
shell is on screen, since a desktop window and a mobile activity's view get there by different
routes and the launcher hangs off the window either way.
