# Pictures

## Pictures open without the post

Tapping the thumbnail on a feed row opens the image full screen — pinch or double-tap to zoom, drag
to pan, tap or back to dismiss — which is what makes an art or comic community browsable a picture
at a time. Whether a post *is* a picture comes from the media type the server reported, not from the
file extension: instances serve images through proxy endpoints whose URLs have no extension at all,
and those are exactly the posts such communities are full of. The viewer loads the original rather
than the feed thumbnail, outside the shared image cache — that cache is bounded by entry count,
which suits thumbnails and would be a memory problem for a queue of full-resolution pages — and
releases the bitmap when it closes.

Reaching the last picture makes the page fetch its next batch, so the carousel keeps going instead
of stopping wherever the feed happened to have been scrolled to. It has to ask, rather than relying
on the feed's own infinite scroll: the feed is underneath a full-screen picture and nobody is
scrolling it. A batch that turns out to be all articles adds no pictures, so it asks again — three
times at most, or a picture-free stretch of feed would be paged to its end in one go.

The picture the reader is heading towards is fetched while they look at this one, so a flick shows
it immediately instead of a spinner. One neighbour, in the direction of travel — turning round
switches which side is fetched. More than one would be memory spent on pictures that may never be
reached, and an oversized neighbour is measured and dropped rather than kept, because holding two
full-resolution scans at once is how a phone kills the app mid-scroll.

Once open, flick up and down to move through the other pictures on the page without going back to
it — the counter in the caption says where you are, and the up and down arrow keys work on the
desktop heads. The list is re-read from the page on every move rather than snapshotted when the
viewer opened, so pictures the feed has loaded in the meantime are there to flick to; freezing it
would strand the reader at whatever happened to be loaded when they tapped. A flick only turns the
page when the picture is not zoomed: zoomed in, the same drag pans, because that is how you read a
tall comic.

The axis is vertical because that is the axis these pictures were already being read on: they are
the posts of a feed, and a flick up brings the next one exactly as it does in the feed itself. It
started out horizontal, which meant the reader changed direction to carry on going the same way.
Moving it also leaves the horizontal axis unclaimed, and there is something waiting for it — a post
can carry several pictures, and those belong across rather than down. A sideways drag and the left
and right arrow keys therefore do nothing at all today rather than doing what up and down do; a
gesture that means two things later is worse than one that means nothing now.

Reading the flick lives in `Flick`, next to `ZoomState` and for the same reason: which way a drag
went and whether it went far enough is a decision, and deciding it should not need a finger on a
screen.

When a picture does not appear, the reason travels with the result rather than being worked out by
whoever notices they got nothing — by then the bytes that would explain it are gone. Three answers
are worth telling apart: it never downloaded, it downloaded and is in a format nothing here can
decode, and it downloaded and is rubbish. The middle one is named from the file's own first bytes,
because a reader who is told "could not be loaded" will keep trying an AVIF that is never going to
work, and because the server's declared type is no help in exactly this case: an inline picture in a
comment usually has none, and a picture that failed to decode is the case where what the server said
cannot be trusted anyway.

Sniffing is only ever asked *after* the platform decoder has given up. Deciding what to attempt from
magic bytes instead would refuse things Skia can actually read.

The AVIF fallback to the server's preview is not the whole answer, either. An instance that has set
pict-rs to AVIF serves the thumbnail as AVIF as well, so both attempts fail for the same reason and
there is nothing softer to show. The original's reason is the one reported, since "this app cannot
show AVIF" is what the reader can act on and "the thumbnail did not load" is not.

The zoom and pan arithmetic lives in `ZoomState`, a value type with no dependency on a control, so
the part a reader feels but never sees is tested rather than eyeballed. Dismissal waits out the
double-tap window: both gestures start with the same tap, and closing immediately fires halfway
through every double-tap.

## Pictures written into a body

An image in Markdown is its own block rather than part of a paragraph. A paragraph is flattened to
one continuous string so that it wraps and selects as a whole, and a picture is not text — so a
paragraph with a picture in the middle is split into text, picture, text. Only pictures at the top
level of a paragraph are lifted out: one nested inside a link is left as the alt text it always was,
because an author who writes a picture inside a link means the link.

They start folded away behind their alt text and are fetched on the first expand, never before. A
comment thread can carry dozens, and opening every one on sight would spend the reader's data and
their place in the thread on pictures they never asked to see. Expanding again after collapsing does
not refetch. Once open, tapping the picture opens it full screen, where it can be zoomed.

That full-screen viewer was built entirely around a post. A picture in a body has no post, so the
viewer now takes either — and for the standalone case its gallery is an empty one rather than
absent, which makes every "next picture" move a no-op through the same code path that already
handled being at the end of a page.

## Formats

Decoding is Skia's, by way of Avalonia, so the answer is whatever the bundled native build supports
rather than anything this repository decides. `ImageCodecTests` asserts it against real files.

| Format | Decodes | Notes |
|---|---|---|
| JPEG, PNG | yes | |
| GIF, animated WebP | yes | animate in the viewer; feed rows show a still |
| AVIF | **no** | falls back to the server's preview, which is never AVIF |

Across 450 posts sampled from four instances, AVIF was 3 of them. Since pict-rs generates previews
in jpeg, png or webp regardless of the original, an AVIF post still shows in the feed and still
opens in the viewer; only the full-resolution copy is unavailable, and the caption says so.

A picture the platform cannot decode comes back as "no picture" rather than as an exception. That
matters more than it sounds: an unsupported format does not fail politely — AVIF surfaces as a
`NullReferenceException` thrown from inside the platform — and letting one escape leaves a feed row
blank forever and the viewer spinning on an image that is never coming.

## Animation

Animation is decoded frame by frame through `SKCodec` — Avalonia's own bitmap loader stops at the
first frame, which is right for a feed row and wrong for a viewer full of reaction GIFs. GIF frames
are differences against earlier ones, so they are decoded in order into one running canvas and each
result copied out; decoding them independently leaves the partial frames that make an animation
flicker and smear. Elapsed time is mapped onto a frame rather than a tick advancing one, so a device
that cannot keep up drops frames instead of playing everything in slow motion. Past a memory budget
a long animation is shown as a still, because a full-size GIF with every frame held as a bitmap runs
to hundreds of megabytes.

Feed thumbnails stay still deliberately: a page of animating rows is a worse page, and the thumbnail
cache would be holding every frame of every one of them.

## The thumbnail cache hands out shared bitmaps and never disposes them

It only drops them. A cached thumbnail is held for as long as the row showing it exists, and rows
outlive their place in the cache — sections stay alive when the reader switches away, so browsing
communities and search evicts the feed's thumbnails while the feed is still bound to them. Disposing
on eviction left an `Image` pointing at a dead bitmap and crashed on the next layout pass, when the
reader came back. An evicted bitmap nobody references is collected normally.
