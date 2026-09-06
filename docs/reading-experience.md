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

The feed, the community directory and a post's comment thread are wrapped in a `RefreshContainer`
that holds its deferral until the fetch actually finishes, so the spinner lasts as long as the work
does. All three reload by fetching first and swapping second, which keeps what is on screen there
through the round trip and means a refresh that fails costs the reader the update rather than the
list. The gesture stays off for mouse users — on desktop the toolbar button is the affordance.

On a post, the gesture re-reads the thread and not the post above it. The post arrived already drawn
from the row that was tapped, and putting a second request behind a gesture whose whole point is the
conversation would make the slower half of the work the part nobody asked for. The consequence is
worth knowing: the comment count in the post header is the one that came with the post, so it can
sit a few behind the thread underneath it.

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

## The unread count, and what it deliberately leaves out

Lemmy keeps three separate counts of what is waiting for an account — replies, mentions and private
messages — and one endpoint that reports all three. The badge beside the account name counts the
first two only. There is nowhere in this app to read a private message, so counting them would put a
number on screen that nothing the reader could do would ever clear. `UnreadTally` holds all three
and adds up only the two, which keeps the omission in one place with the reason attached.

The two lists behind the count are separate endpoints as well, and Lemmy numbers their rows
separately: reply 7 and mention 7 are different things, marked read through different endpoints. So
the kind travels with the identifier everywhere — a `Notification` carries both, and the wire record
decides the kind from which marker block arrived rather than from which request it answered. The two
marker blocks have identical fields, which is why one record reads both.

The client fetches both lists at once and merges them into one time order. Concatenating them would
have given the reader all the replies and then all the mentions, which is not a notification list.
The timestamp it sorts on is the marker's, not the comment's: a federated comment can reach this
instance long after it was written, and what the reader is being told about is the arrival.

The count is shared state rather than a callback, like the subscription tracker and the current
account before it — the header shows it and a page two levels down changes it. Marking one read
moves it by one rather than recounting the rows on screen, because those rows are one page of a
possibly longer list and recounting would silently shrink a badge that is telling the truth. Marking
everything read empties it, since the endpoint really does clear the lot. Leaving the list asks the
server again, which is the cheap way to be right after a page of local guesses.

Nothing polls. A badge a few minutes stale costs nothing; a phone waking its radio on a timer to
count something costs battery all day. It is asked for at launch, on signing in, and on coming back
from the list.

## A thread goes on screen a screenful at a time

Opening a busy post used to freeze the app. Measured on a Galaxy S24 against a 179-comment thread
on lemmy.world, the main thread was blocked for **1.3 seconds** on a Release build — 4.2 on Debug —
with nothing drawn and no tap answered for the duration.

The measurement is worth keeping because it contradicts the obvious guess. Fetching is not the
problem, and neither is building the view models: 179 of them, Markdown parsed and all, take 26 ms.
The whole cost is realising and measuring the controls. A comment is a `CommentView` holding a
`MarkdownView`, a `WrapPanel` of actions and an `ItemsControl` of more `CommentView`s, and the
thread hangs in a plain `ItemsControl` inside a `ScrollViewer` — which does not virtualise. So every
comment in the thread was measured before the first one could be drawn.

Comments are therefore handed to the layout in batches of roughly eight, waiting for each batch to
be drawn before adding the next. The total work is unchanged; what changes is that the reader sees
the top of the thread almost immediately and can scroll and tap while the rest fills in. The worst
single stall drops from 1.3 s to around 0.3 s.

Where the batch waits matters. `Task.Yield` posts its continuation at the default priority, which in
Avalonia sits *above* `Render` — so yielding that way would queue every batch ahead of the drawing
they are waiting for and change nothing. The wait is posted at `Background`, below `Render` and
below `Input`, so the frame lands and a tap is answered before the next batch is handed over.

Filling across several turns of the dispatcher means two threads can now race — a sort changed or a
refresh pulled while the previous one is still going on. Each fill takes a generation number and
stops as soon as another starts, so the later thread owns the list rather than interleaving with it.

The remaining stall is a single top-level comment with a large sub-thread: the budget is only
checked between roots, so a root with thirty replies still goes on in one pass. Removing that
entirely means virtualising the list, which needs the post header to become the list's header rather
than a sibling inside the same scroller — a change to how the page scrolls, not just how it fills.

## Reading past where the thread was cut

A thread arrives in one request, eight levels deep. That covers almost everything — but Lemmy
threads are a back-and-forth between two people, and those run deeper than eight almost every time
they run at all. Below the cut the server still says how many replies it did not send, and that
number is the control: pressing it fetches the sub-thread from exactly the comment the thread was
cut at.

Three things about the request make it work. The depth counts from that comment rather than from the
post, so the same eight reaches as far below the cut as the first request reached below the post —
verified against lemmy.world, where asking from a comment at depth 8 returns comments at depth 9.
The thread's own sort is carried along, so what arrives is ordered like what is already on screen
rather than by the server's default. And the response leads with the comment that was asked about,
which is already there, so the merge has to recognise it rather than file it beneath itself.

Placement goes by `CommentPath`, not by the shape of the response. A sub-thread comes back flat and
in sort order, so a reply can arrive before the comment it hangs off; comments are filed in repeated
passes, each pass placing whatever has found a parent, until a pass places nothing. What is left
then hangs off a comment that never arrived, and is dropped — filing it at the top would claim it
replied to something it did not.

The same replies are missing from every comment above the cut, and each of those is on screen
saying so. So a fetch settles the counts from the top of the thread down, not just at the comment
that was pressed. Without that, expanding deep in a chain left the ancestor a few lines up still
offering to fetch a reply that had just appeared below it — which is exactly what a real thread on
lemmy.world did before the comments were given a link to their parent.

The count itself cannot be trusted to reach zero. `child_count` includes replies the server will not
serve: ones a moderator removed, ones from a blocked account. So the offer disappears when a fetch
brings back nothing new, whatever the count still says, rather than inviting the reader to press a
button that will never do anything. `limit` is worth mentioning too — lemmy.world ignores it on
comment lists, returning all 169 comments of a thread for a request that asked for five — so depth,
not page size, is the only thing that actually truncates, and there is no second page to ask for.

## Writing a comment

One composer serves all three jobs — a new comment, a reply, an edit — because they differ only in
what the button says and where the result goes.

What it must never do is lose what was typed. The box is cleared only after the server has accepted
the comment, never before, and a failure leaves both the text and the reason on screen. That is not
hypothetical: lemmy.world refuses posting from VPN exit nodes with a 401, and the first real comment
this app tried to make came back exactly that way.

`AcceptsReturn` is not enough to be able to type a paragraph. It says what the control does with a
Return it receives; a soft keyboard decides for itself whether to send one, and it does not read
that property. Left alone, Android offers a **Done** key that closes the keyboard, so a comment
could only ever be one paragraph. The boxes that take prose — the comment box and the post body —
therefore also ask the platform directly, through `TextInputOptions.Multiline` and a
`ReturnKeyType` of `Return`. Nothing about this is visible on the desktop heads, where a physical
Return has always just worked, which is why it survived to a phone.

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

## Writing a post

A post is a page rather than a box in a feed. A comment is one field and belongs where it is being
made; a post is a title, a link, a body, a flag and a community, which does not fit above a feed on
a phone. Being a page also means it is left by going back, which on Android is the gesture people
already use.

`PostDraft` carries what a post says and not where it goes. Lemmy cannot move a post between
communities, so an edit has no community to offer and a new post must have one; a field that only
half the callers may set is worse than a parameter that both are honest about. The community is
therefore an argument to `CreatePostAsync` and simply absent from `EditPostAsync`.

Empty is not the same as absent on the way out. Lemmy reads a missing field as *leave this one
alone* and an empty one as *clear it*, so the two requests are deliberately built differently:
creating omits a link it does not have, because `""` would be rejected as a malformed address, while
editing sends `""` for both the link and the body. Omitting them there would make removing a link
possible only by editing on the web.

The body is refused when it is over-long rather than clipped. `MarkdownText` truncates, which is
right for text arriving from a server we do not control — a clipped body is better than a post
missing from the feed — and wrong for the author's own writing, where silently dropping the end of
it is the worst available answer.

A link typed without a scheme gets `https://`. People paste `example.com/article`, and refusing that
is pedantry rather than validation. A scheme that is present and is not the web is refused instead
of being prefixed, so `ftp://host` does not quietly become `https://ftp://host`.

Afterwards the composer is left behind and the finished post is opened. The feed it was started from
also gets the post inserted at the top, rather than being refetched: a brand new post does not
necessarily sort into the first page of Hot or Top, so a refresh could plausibly answer without it,
which reads as the post having failed.

Editing takes the post back from the server and leaves the comment thread alone — an edit does not
touch it — and the picture is only re-fetched when the link actually changed, so a delete or a
restore does not make the image flicker. Deleting is a flag, exactly as it is for comments, so the
author gets a **Restore** where the delete was. Edit and Delete are shown only to the account that
wrote the post, matched by person id, on the same reasoning as comments.

One consequence worth naming: the page heading is the post's title, and an edit changes it while the
page is on screen. The shell follows the current page's `Title` rather than reading it once when the
page is pushed, which is a subscription that lasts exactly as long as the page is the current one.

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
