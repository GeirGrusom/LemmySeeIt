# Markdown

## Rendered, not flattened

Post bodies and comments draw headings, emphasis, code, quotes, lists, tables, Lemmy's `:::`
spoilers as actual folds, and links you can press. Markdig does the parsing — CommonMark is full of
edge cases and Lemmy's dialect is CommonMark plus the extensions its own frontend enables — but it
is kept behind `MarkdownParser`, which converts to this app's own immutable block model. Nothing
outside that file sees a Markdig type, so the renderer is a plain mapper over records and swapping
the parser would touch one file. It publishes under Native AOT with full trimming and no warnings,
which was checked before adopting it.

A paragraph's styling is flattened to ranges over one continuous string rather than kept as a tree,
because the text has to stay one string to wrap and select as a whole. Tapping a link is matched
against the rectangles the layout actually drew that link into, not by hit-testing a point to a
character: hit-testing answers with a *caret* position, which rounds to the nearest boundary, so a
tap on the left half of a link's first letter reports the character before it. Rectangles also
handle a link that wrapped onto a second line, which comes back as one rectangle per line.
