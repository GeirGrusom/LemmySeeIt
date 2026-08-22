using CommunityToolkit.Mvvm.ComponentModel;

namespace Lemmy.ViewModels;

/// <summary>
/// Base for everything bound to a view. <see cref="ObservableObject"/> raises change notifications
/// through source-generated code rather than reflection, which is what keeps binding working once
/// the app is compiled ahead of time.
/// </summary>
public abstract class ViewModelBase : ObservableObject;
