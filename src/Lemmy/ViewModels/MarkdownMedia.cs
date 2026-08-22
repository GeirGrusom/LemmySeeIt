using System.Windows.Input;
using Lemmy.Services;

namespace Lemmy.ViewModels;

/// <summary>
/// What the Markdown renderer needs in order to draw pictures written into a body: something to
/// fetch them with, and somewhere to send one that is tapped.
/// </summary>
/// <remarks>
/// Bundled rather than passed as two more arguments down through every comment in a thread. A
/// picture in a body is the only part of Markdown that needs anything from outside the text.
/// </remarks>
/// <param name="Images">Fetches and decodes the picture.</param>
/// <param name="OpenPicture">
/// Opens one full screen, taking the <see cref="Lemmy.Domain.Markdown.MarkdownImage"/> as its
/// parameter; <see langword="null"/> when there is nowhere to open it.
/// </param>
public sealed record MarkdownMedia(IImageLoader Images, ICommand? OpenPicture);
