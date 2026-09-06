using System.Collections.Immutable;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.TextFormatting;
using System.Diagnostics;
using Avalonia.Threading;
using Lemmy.Services;
using Avalonia.VisualTree;
using Lemmy.Domain;
using Lemmy.Domain.Markdown;
using Lemmy.ViewModels;
using Lemmy.Views;

namespace Lemmy.Views.Markdown;

/// <summary>
/// Draws parsed Markdown. Blocks become controls; a paragraph's styling becomes inlines over one
/// continuous string, which is what lets a paragraph wrap and be selected as a whole while still
/// having tappable links inside it.
/// </summary>
public sealed class MarkdownView : Decorator
{
    /// <summary>Pictures in a body are drawn small; decoding them larger just burns memory.</summary>
    private const int ImageDecodeWidth = 720;

    /// <summary>How tall one may grow before it starts pushing the text off the screen.</summary>
    private const int ImageMaxHeight = 420;

    /// <summary>How often an animation is asked which frame it is on.</summary>
    private static readonly TimeSpan FrameTick = TimeSpan.FromMilliseconds(40);

    private readonly List<RunningPicture> animations = [];

    /// <summary>Bumped whenever the drawn tree is replaced, so a load in flight can tell.</summary>
    private int generation;

    /// <summary>The blocks to draw.</summary>
    public static readonly StyledProperty<ImmutableArray<MarkdownBlock>> BlocksProperty =
        AvaloniaProperty.Register<MarkdownView, ImmutableArray<MarkdownBlock>>(nameof(Blocks));

    /// <summary>Invoked with the <see cref="WebLink"/> the reader pressed.</summary>
    /// <summary>How to fetch and open pictures written into the body.</summary>
    public static readonly StyledProperty<MarkdownMedia?> MediaProperty =
        AvaloniaProperty.Register<MarkdownView, MarkdownMedia?>(nameof(Media));

    public static readonly StyledProperty<ICommand?> LinkCommandProperty =
        AvaloniaProperty.Register<MarkdownView, ICommand?>(nameof(LinkCommand));

    /// <summary>How links are coloured.</summary>
    public static readonly StyledProperty<IBrush?> LinkBrushProperty =
        AvaloniaProperty.Register<MarkdownView, IBrush?>(nameof(LinkBrush));

    /// <summary>What sits behind code, inline and fenced.</summary>
    public static readonly StyledProperty<IBrush?> CodeBackgroundProperty =
        AvaloniaProperty.Register<MarkdownView, IBrush?>(nameof(CodeBackground));

    /// <summary>The bar down the side of a quotation.</summary>
    public static readonly StyledProperty<IBrush?> QuoteBrushProperty =
        AvaloniaProperty.Register<MarkdownView, IBrush?>(nameof(QuoteBrush));

    /// <summary>The line a thematic break draws.</summary>
    public static readonly StyledProperty<IBrush?> RuleBrushProperty =
        AvaloniaProperty.Register<MarkdownView, IBrush?>(nameof(RuleBrush));

    private static readonly FontFamily MonospaceFont = new("Cascadia Mono,Consolas,Menlo,DejaVu Sans Mono,monospace");

    /// <summary>Which text each rendered block came from, for working out what was pressed.</summary>
    private readonly Dictionary<TextBlock, RichText> sources = [];

    static MarkdownView()
    {
        // The palette arrives from styles, which is what makes it follow the system theme; a
        // rebuild on any of it keeps what is already drawn in step with a theme change.
        BlocksProperty.Changed.AddClassHandler<MarkdownView>((view, _) => view.Rebuild());
        LinkBrushProperty.Changed.AddClassHandler<MarkdownView>((view, _) => view.Rebuild());
        CodeBackgroundProperty.Changed.AddClassHandler<MarkdownView>((view, _) => view.Rebuild());
        QuoteBrushProperty.Changed.AddClassHandler<MarkdownView>((view, _) => view.Rebuild());
        RuleBrushProperty.Changed.AddClassHandler<MarkdownView>((view, _) => view.Rebuild());
    }

    /// <inheritdoc cref="BlocksProperty" />
    public ImmutableArray<MarkdownBlock> Blocks
    {
        get => GetValue(BlocksProperty);
        set => SetValue(BlocksProperty, value);
    }

    /// <inheritdoc cref="MediaProperty" />
    public MarkdownMedia? Media
    {
        get => GetValue(MediaProperty);
        set => SetValue(MediaProperty, value);
    }

    /// <inheritdoc cref="LinkCommandProperty" />
    public ICommand? LinkCommand
    {
        get => GetValue(LinkCommandProperty);
        set => SetValue(LinkCommandProperty, value);
    }

    /// <inheritdoc cref="LinkBrushProperty" />
    public IBrush? LinkBrush
    {
        get => GetValue(LinkBrushProperty);
        set => SetValue(LinkBrushProperty, value);
    }

    /// <inheritdoc cref="CodeBackgroundProperty" />
    public IBrush? CodeBackground
    {
        get => GetValue(CodeBackgroundProperty);
        set => SetValue(CodeBackgroundProperty, value);
    }

    /// <inheritdoc cref="QuoteBrushProperty" />
    public IBrush? QuoteBrush
    {
        get => GetValue(QuoteBrushProperty);
        set => SetValue(QuoteBrushProperty, value);
    }

    /// <inheritdoc cref="RuleBrushProperty" />
    public IBrush? RuleBrush
    {
        get => GetValue(RuleBrushProperty);
        set => SetValue(RuleBrushProperty, value);
    }

    private void Rebuild()
    {
        sources.Clear();
        StopAnimations();

        ImmutableArray<MarkdownBlock> blocks = Blocks;
        Child = blocks.IsDefaultOrEmpty ? null : BuildBlocks(blocks);
    }

    private Control BuildBlocks(ImmutableArray<MarkdownBlock> blocks)
    {
        var panel = new StackPanel { Spacing = 8 };

        foreach (MarkdownBlock block in blocks)
        {
            panel.Children.Add(Build(block));
        }

        return panel;
    }

    private Control Build(MarkdownBlock block) => block switch
    {
        MarkdownParagraph paragraph => BuildText(paragraph.Content),
        MarkdownHeading heading => BuildHeading(heading),
        MarkdownCodeBlock code => BuildCode(code),
        MarkdownQuote quote => BuildQuote(quote),
        MarkdownList list => BuildList(list),
        MarkdownSpoiler spoiler => BuildSpoiler(spoiler),
        MarkdownImage image => BuildImage(image),
        MarkdownThematicBreak => BuildRule(),
        MarkdownTable table => BuildTable(table),
        _ => new TextBlock { Text = string.Empty },
    };

    private Control BuildHeading(MarkdownHeading heading)
    {
        // Level one is barely larger than body text on purpose: these headings sit inside a post,
        // under a title that is already the largest thing on screen.
        double size = heading.Level switch
        {
            1 => 19,
            2 => 17,
            3 => 16,
            _ => 15,
        };

        SelectableText text = BuildText(heading.Content);
        text.FontSize = size;
        text.FontWeight = FontWeight.SemiBold;
        text.Margin = new Thickness(0, 4, 0, 0);

        return text;
    }

    private Control BuildCode(MarkdownCodeBlock code) =>
        new Border
        {
            Background = CodeBackground,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8),
            Child = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = new SelectableText
                {
                    Text = code.Text,
                    FontFamily = MonospaceFont,
                    FontSize = 13,
                    TextWrapping = TextWrapping.NoWrap,
                },
            },
        };

    private Control BuildQuote(MarkdownQuote quote) =>
        new Border
        {
            BorderBrush = QuoteBrush,
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(10, 2, 0, 2),
            Child = BuildBlocks(quote.Children),
        };

    private Control BuildList(MarkdownList list)
    {
        var panel = new StackPanel { Spacing = 4 };

        for (int index = 0; index < list.Items.Length; index++)
        {
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                Margin = new Thickness(4, 0, 0, 0),
            };

            var marker = new TextBlock
            {
                Text = list.IsOrdered ? $"{list.Start + index}." : "•",
                Margin = new Thickness(0, 0, 8, 0),
                MinWidth = list.IsOrdered ? 20 : 12,
            };

            Control content = BuildBlocks(list.Items[index]);
            Grid.SetColumn(marker, 0);
            Grid.SetColumn(content, 1);

            row.Children.Add(marker);
            row.Children.Add(content);
            panel.Children.Add(row);
        }

        return panel;
    }

    /// <summary>A spoiler is a fold, which is the whole point of writing one.</summary>
    private Control BuildSpoiler(MarkdownSpoiler spoiler) =>
        new Expander
        {
            Header = spoiler.Title,
            IsExpanded = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = BuildBlocks(spoiler.Children),
        };

    /// <summary>
    /// A picture, folded away behind its alt text until asked for.
    /// </summary>
    /// <remarks>
    /// Collapsed by default and fetched only on the first expand. A thread can carry dozens of
    /// these, and opening every one on sight would spend the reader's data and their scroll
    /// position on pictures they never asked to see. Once open, tapping it opens it full screen,
    /// where it can be zoomed.
    /// </remarks>
    private Control BuildImage(MarkdownImage image)
    {
        var content = new Panel { MinHeight = 24 };

        var expander = new Expander
        {
            // A picture with no alt text has nothing to add after the dash.
            Header = string.IsNullOrWhiteSpace(image.AltText) ? "Image" : $"Image — {image.AltText}",
            IsExpanded = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = content,
        };

        expander.PropertyChanged += (_, change) =>
        {
            if (change.Property == Expander.IsExpandedProperty
                && expander.IsExpanded
                && content.Children.Count == 0)
            {
                // Not awaited: the expander opens now and fills in when the picture arrives.
                _ = RevealAsync(content, image);
            }
        };

        return expander;
    }

    private async Task RevealAsync(Panel content, MarkdownImage image)
    {
        int mine = generation;

        if (Media is not { } media)
        {
            content.Children.Add(new TextBlock { Text = "No way to load pictures here." });
            return;
        }

        var progress = new ProgressBar { IsIndeterminate = true, Height = 3, Margin = new Thickness(0, 8) };
        content.Children.Add(progress);

        // The picture loader rather than the thumbnail one: it decodes every frame, so a GIF in a
        // comment moves. What comes back is ours to dispose — unlike a cached thumbnail, which is
        // shared with whatever else is showing it.
        PictureLoad load = await media.Images
            .LoadPictureAsync(image.Source, ImageDecodeWidth)
            .ConfigureAwait(true);

        content.Children.Remove(progress);

        if (load.Picture is not { } picture)
        {
            content.Children.Add(new TextBlock
            {
                Text = load.Message,
                TextWrapping = TextWrapping.Wrap,
            });
            return;
        }

        // The view may have been rebuilt or torn down while that was in flight, in which case this
        // picture belongs to a tree nobody is looking at. Checked by generation rather than by
        // attachment: an expander's content is not attached until it has been laid out, which has
        // not happened yet at this point.
        if (mine != generation)
        {
            picture.Dispose();
            return;
        }

        var control = new Image
        {
            Source = picture.FirstFrame,
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            HorizontalAlignment = HorizontalAlignment.Left,
            MaxHeight = ImageMaxHeight,
            Cursor = new Cursor(StandardCursorType.Hand),
        };

        // Tapped rather than pressed: the thread scrolls, and a drag that begins on a picture is
        // somebody scrolling past it.
        control.Tapped += (_, _) => media.OpenPicture?.Execute(image);

        content.Children.Add(control);
        Play(picture, control);
    }

    /// <summary>
    /// Starts a picture moving, if it moves at all. Time is mapped to a frame rather than a frame
    /// advanced per tick, so a device that cannot keep up drops frames instead of playing the whole
    /// thing in slow motion — the same rule the full-screen viewer follows.
    /// </summary>
    private void Play(AnimatedImage picture, Image control)
    {
        if (!picture.IsAnimated)
        {
            animations.Add(new RunningPicture(picture, null, null));
            return;
        }

        var clock = Stopwatch.StartNew();
        var timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = FrameTick };

        timer.Tick += (_, _) => control.Source = picture.Frames[picture.FrameIndexAt(clock.Elapsed)].Image;
        timer.Start();

        animations.Add(new RunningPicture(picture, timer, clock));
    }

    /// <summary>
    /// Stops and releases every picture this view started. Called when the blocks change and when
    /// the view leaves the tree: a comment thread scrolls, and a timer left running on a comment
    /// nobody is looking at costs frames for nothing.
    /// </summary>
    private void StopAnimations()
    {
        generation++;

        foreach (RunningPicture running in animations)
        {
            running.Timer?.Stop();
            running.Clock?.Stop();
            running.Picture.Dispose();
        }

        animations.Clear();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        StopAnimations();
        base.OnDetachedFromVisualTree(e);
    }

    private sealed record RunningPicture(AnimatedImage Picture, DispatcherTimer? Timer, Stopwatch? Clock);

    private Control BuildRule() =>
        new Border
        {
            Height = 1,
            Margin = new Thickness(0, 6),
            Background = RuleBrush,
        };

    private Control BuildTable(MarkdownTable table)
    {
        int columns = Math.Max(
            table.Header.Length,
            table.Rows.Length == 0 ? 0 : table.Rows.Max(row => row.Length));

        if (columns == 0)
        {
            return new TextBlock { Text = string.Empty };
        }

        var grid = new Grid { ColumnSpacing = 12, RowSpacing = 4 };
        for (int column = 0; column < columns; column++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        }

        int rowIndex = 0;
        if (!table.Header.IsEmpty)
        {
            AddRow(grid, table.Header, rowIndex++, isHeader: true);
        }

        foreach (ImmutableArray<RichText> row in table.Rows)
        {
            AddRow(grid, row, rowIndex++, isHeader: false);
        }

        return new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = grid,
        };
    }

    private void AddRow(Grid grid, ImmutableArray<RichText> cells, int rowIndex, bool isHeader)
    {
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        for (int column = 0; column < cells.Length; column++)
        {
            SelectableText cell = BuildText(cells[column]);
            cell.TextWrapping = TextWrapping.NoWrap;

            if (isHeader)
            {
                cell.FontWeight = FontWeight.SemiBold;
            }

            Grid.SetRow(cell, rowIndex);
            Grid.SetColumn(cell, column);
            grid.Children.Add(cell);
        }
    }

    /// <summary>
    /// Builds one paragraph. The text stays a single string with the styling applied as inlines
    /// over it, so it wraps and selects normally; link targets are remembered by character range
    /// and resolved by hit-testing whatever the reader pressed.
    /// </summary>
    private SelectableText BuildText(RichText text)
    {
        var block = new SelectableText
        {
            TextWrapping = TextWrapping.Wrap,
            Padding = default,
            Inlines = BuildInlines(text),
        };

        if (text.Spans.Any(span => span.IsLink))
        {
            // Hit testing skips a control with no background, so a paragraph with nothing painted
            // behind it never sees the press that was meant for the link inside it.
            block.Background = Brushes.Transparent;

            sources[block] = text;

            // Tapped rather than PointerReleased. Inside a ScrollViewer the scroll gesture
            // recogniser captures the pointer on touch, so a release never reaches the text — and
            // Tapped already tells a tap apart from a drag, which is what selecting text is.
            block.AddHandler(TappedEvent, OnTextTapped, RoutingStrategies.Bubble, handledEventsToo: true);
        }

        return block;
    }

    private InlineCollection BuildInlines(RichText text)
    {
        var inlines = new InlineCollection();

        foreach ((int start, int length, MarkdownSpanStyle style, WebLink? link) in Segment(text))
        {
            inlines.Add(Style(new Run(text.Text.Substring(start, length)), style, link));
        }

        return inlines;
    }

    /// <summary>
    /// Splits the text at every span boundary, so each piece has one consistent style. Spans do not
    /// overlap, so walking them in order and filling the gaps is enough.
    /// </summary>
    private static IEnumerable<(int Start, int Length, MarkdownSpanStyle Style, WebLink? Link)> Segment(RichText text)
    {
        int position = 0;

        foreach (MarkdownSpan span in text.Spans.OrderBy(span => span.Start))
        {
            if (span.Start > position)
            {
                yield return (position, span.Start - position, MarkdownSpanStyle.None, null);
            }

            int start = Math.Max(position, span.Start);
            int end = Math.Min(text.Text.Length, span.End);

            if (end > start)
            {
                yield return (start, end - start, span.Style, span.Link);
                position = end;
            }
        }

        if (position < text.Text.Length)
        {
            yield return (position, text.Text.Length - position, MarkdownSpanStyle.None, null);
        }
    }

    private Run Style(Run run, MarkdownSpanStyle style, WebLink? link)
    {
        if (style.HasFlag(MarkdownSpanStyle.Bold))
        {
            run.FontWeight = FontWeight.Bold;
        }

        if (style.HasFlag(MarkdownSpanStyle.Italic))
        {
            run.FontStyle = FontStyle.Italic;
        }

        if (style.HasFlag(MarkdownSpanStyle.Strikethrough))
        {
            run.TextDecorations = TextDecorations.Strikethrough;
        }

        if (style.HasFlag(MarkdownSpanStyle.Code))
        {
            run.FontFamily = MonospaceFont;

            if (CodeBackground is { } codeBackground)
            {
                run.Background = codeBackground;
            }
        }

        if (style.HasFlag(MarkdownSpanStyle.Superscript))
        {
            run.BaselineAlignment = BaselineAlignment.Superscript;
            run.FontSize = 10;
        }

        if (style.HasFlag(MarkdownSpanStyle.Subscript))
        {
            run.BaselineAlignment = BaselineAlignment.Subscript;
            run.FontSize = 10;
        }

        if (link.HasValue)
        {
            // Only when there is a brush to use: assigning null here overrides inheritance and the
            // text is drawn with nothing at all, which is to say invisibly.
            if (LinkBrush is { } linkBrush)
            {
                run.Foreground = linkBrush;
            }

            run.TextDecorations = TextDecorations.Underline;
        }

        return run;
    }

    /// <summary>
    /// Opens the link under the tap, if there is one.
    /// </summary>
    /// <remarks>
    /// Matched against each link's own glyph rectangles rather than by hit-testing to a character
    /// index. <c>HitTestPoint</c> answers with a caret position, which rounds to the nearest
    /// character boundary — so a tap on the left half of a link's first letter reports the
    /// character before it, and a tap just past the last letter reports the link. Asking the layout
    /// where a link was actually drawn has no such ambiguity, and it handles a link that wrapped
    /// across lines, which comes back as one rectangle per line.
    /// </remarks>
    private void OnTextTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not TextBlock block || !sources.TryGetValue(block, out RichText? text))
        {
            return;
        }

        Point point = e.GetPosition(block);

        foreach (MarkdownSpan span in text.Spans)
        {
            if (span.Link is not { } link || span.Length <= 0)
            {
                continue;
            }

            foreach (Rect rectangle in block.TextLayout.HitTestTextRange(span.Start, span.Length))
            {
                if (!rectangle.Contains(point))
                {
                    continue;
                }

                if (LinkCommand is { } command && command.CanExecute(link))
                {
                    command.Execute(link);
                    e.Handled = true;
                }

                return;
            }
        }
    }
}
