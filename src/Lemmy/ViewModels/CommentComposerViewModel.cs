using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemmy.Api;
using Lemmy.Domain;

namespace Lemmy.ViewModels;

/// <summary>What a composer is being used for, which is all that differs between the three.</summary>
public enum ComposerPurpose
{
    /// <summary>A new comment on the post.</summary>
    Comment,

    /// <summary>A reply to another comment.</summary>
    Reply,

    /// <summary>Rewriting one of the reader's own comments.</summary>
    Edit,
}

/// <summary>
/// The text box for writing a comment, shared by the post's own box, every reply box and every edit
/// box. Markdown is typed rather than composed: the renderer already understands it, and a toolbar
/// of formatting buttons is a different feature from being able to comment at all.
/// </summary>
public sealed partial class CommentComposerViewModel : ViewModelBase
{
    private readonly Func<CommentDraft, CancellationToken, Task> submit;
    private readonly Action? closed;

    /// <summary>Creates a composer.</summary>
    /// <param name="purpose">Which of the three jobs this box is doing.</param>
    /// <param name="submit">Sends what was written.</param>
    /// <param name="closed">Called when the reader dismisses the box, or <see langword="null"/> if it cannot be dismissed.</param>
    /// <param name="initialText">What to start with, for an edit.</param>
    public CommentComposerViewModel(
        ComposerPurpose purpose,
        Func<CommentDraft, CancellationToken, Task> submit,
        Action? closed = null,
        string? initialText = null)
    {
        ArgumentNullException.ThrowIfNull(submit);

        Purpose = purpose;
        this.submit = submit;
        this.closed = closed;
        text = initialText ?? string.Empty;
    }

    /// <summary>Which job this box is doing.</summary>
    public ComposerPurpose Purpose { get; }

    /// <summary>What the send button says.</summary>
    public string SubmitLabel => Purpose switch
    {
        ComposerPurpose.Reply => "Reply",
        ComposerPurpose.Edit => "Save",
        _ => "Post",
    };

    /// <summary>The prompt shown in an empty box.</summary>
    public string Placeholder => Purpose switch
    {
        ComposerPurpose.Reply => "Write a reply — Markdown works",
        ComposerPurpose.Edit => "Edit your comment",
        _ => "Add a comment — Markdown works",
    };

    /// <summary>Whether the box can be dismissed; the post's own box is always there.</summary>
    public bool CanDismiss => closed is not null;

    /// <summary>What has been written.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LengthLabel))]
    [NotifyPropertyChangedFor(nameof(IsOverLength))]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    private string text;

    /// <summary>Set while the comment is being sent.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    private bool isBusy;

    /// <summary>Why the last attempt did not take, or <see langword="null"/>.</summary>
    [ObservableProperty]
    private string? errorMessage;

    /// <summary>
    /// How much room is left, shown only once it starts to matter. A counter on an empty box is
    /// noise; one on a comment approaching the limit is the only warning there is.
    /// </summary>
    public string LengthLabel => $"{Text.Trim().Length} / {CommentDraft.MaxLength}";

    /// <summary>Whether the counter should be shown at all.</summary>
    public bool IsOverLength => Text.Trim().Length > CommentDraft.MaxLength - 500;

    /// <summary>Sends what was written.</summary>
    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task SubmitAsync(CancellationToken cancellationToken)
    {
        if (!CommentDraft.TryCreate(Text.AsSpan(), out CommentDraft draft))
        {
            ErrorMessage = CommentDraft.Explain(Text.AsSpan());
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            await submit(draft, cancellationToken).ConfigureAwait(true);

            // Only clear once it is really posted. Losing what was typed to a failed request is the
            // one outcome a comment box must never have.
            Text = string.Empty;
            closed?.Invoke();
        }
        catch (OperationCanceledException)
        {
            // The page went away; what was typed goes with it.
        }
        catch (LemmyApiException exception)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Dismisses the box, discarding what was written.</summary>
    [RelayCommand]
    private void Dismiss() => closed?.Invoke();

    private bool CanSubmit() => !IsBusy && CommentDraft.TryCreate(Text.AsSpan(), out _);
}
