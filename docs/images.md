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

Once open, flick left and right to move through the other pictures on the page without going back
to it — the counter in the caption says where you are, and arrow keys work on the desktop heads. The
list is re-read from the page on every move rather than snapshotted when the viewer opened, so
pictures the feed has loaded in the meantime are there to flick to; freezing it would strand the
reader at whatever happened to be loaded when they tapped. A flick only turns the page when the
picture is not zoomed: zoomed in, the same drag pans, because that is how you read a tall comic.

The zoom and pan arithmetic lives in `ZoomState`, a value type with no dependency on a control, so
the part a reader feels but never sees is tested rather than eyeballed. Dismissal waits out the
double-tap window: both gestures start with the same tap, and closing immediately fires halfway
through every double-tap.

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
