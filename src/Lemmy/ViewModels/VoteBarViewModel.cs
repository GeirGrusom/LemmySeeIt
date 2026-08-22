using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lemmy.Api;
using Lemmy.Domain;

namespace Lemmy.ViewModels;

/// <summary>
/// The two arrows and the score beside a post or a comment. Shared by the feed row, the post header
/// and every comment, because the behaviour is identical in all three and voting is the one place
/// where getting it subtly different in three copies would be visible to the reader.
/// </summary>
public sealed partial class VoteBarViewModel : ViewModelBase
{
    private readonly Func<Vote, CancellationToken, Task<VoteOutcome>> cast;

    private VoteOutcome state;

    /// <summary>Creates a bar over the counts something arrived with.</summary>
    /// <param name="initial">The score, split and the account's own vote as the server last said.</param>
    /// <param name="canVote">Whether anybody is signed in; the arrows are hidden when nobody is.</param>
    /// <param name="cast">Sends the vote and answers with what the server made of it.</param>
    public VoteBarViewModel(
        VoteOutcome initial,
        bool canVote,
        Func<Vote, CancellationToken, Task<VoteOutcome>> cast)
    {
        ArgumentNullException.ThrowIfNull(cast);

        state = initial;
        CanVote = canVote;
        this.cast = cast;
    }

    /// <summary>Whether to offer the arrows at all.</summary>
    public bool CanVote { get; }

    /// <summary>How the account stands on this right now, including a vote not yet acknowledged.</summary>
    public Vote MyVote => state.MyVote;

    /// <summary>Whether the up arrow is lit.</summary>
    public bool IsUpvoted => state.MyVote == Vote.Up;

    /// <summary>Whether the down arrow is lit.</summary>
    public bool IsDownvoted => state.MyVote == Vote.Down;

    /// <summary>The score, shortened for a badge.</summary>
    public string ScoreLabel => state.Score.ToCompactString();

    /// <summary>Whether the community has voted this down on balance.</summary>
    public bool IsNegative => state.Score.IsNegative;

    /// <summary>Set while a vote is in flight, so the arrows cannot be hammered.</summary>
    [ObservableProperty]
    private bool isVoting;

    /// <summary>
    /// Why the last vote did not take, or <see langword="null"/>. Shown next to the arrows rather
    /// than as a page error: the rest of the page is fine, and only this went wrong.
    /// </summary>
    [ObservableProperty]
    private string? errorMessage;

    /// <summary>Votes the post or comment up, or takes an existing upvote back.</summary>
    [RelayCommand(CanExecute = nameof(CanCast))]
    private Task UpvoteAsync(CancellationToken cancellationToken) => CastAsync(Vote.Up, cancellationToken);

    /// <summary>Votes the post or comment down, or takes an existing downvote back.</summary>
    [RelayCommand(CanExecute = nameof(CanCast))]
    private Task DownvoteAsync(CancellationToken cancellationToken) => CastAsync(Vote.Down, cancellationToken);

    private bool CanCast() => CanVote && !IsVoting;

    private async Task CastAsync(Vote pressed, CancellationToken cancellationToken)
    {
        VoteOutcome before = state;
        Vote wanted = state.MyVote.Toggle(pressed);

        // Move the arrows and the score now. A vote that has to wait for a round trip before it
        // shows feels broken on a phone, and the numbers are only ever approximately right anyway.
        Publish(state.WithVote(wanted));
        IsVoting = true;
        ErrorMessage = null;

        try
        {
            Publish(await cast(wanted, cancellationToken).ConfigureAwait(true));
        }
        catch (OperationCanceledException)
        {
            // The page went away. Leave what is on screen alone: it is about to be discarded.
        }
        catch (LemmyApiException exception)
        {
            // Put back exactly what was there. Re-deriving it would compound the guess.
            Publish(before);
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsVoting = false;
        }
    }

    private void Publish(VoteOutcome next)
    {
        state = next;
        OnPropertyChanged(nameof(MyVote));
        OnPropertyChanged(nameof(IsUpvoted));
        OnPropertyChanged(nameof(IsDownvoted));
        OnPropertyChanged(nameof(ScoreLabel));
        OnPropertyChanged(nameof(IsNegative));
    }

    partial void OnIsVotingChanged(bool value)
    {
        UpvoteCommand.NotifyCanExecuteChanged();
        DownvoteCommand.NotifyCanExecuteChanged();
    }
}
