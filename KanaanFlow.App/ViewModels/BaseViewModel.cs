namespace KanaanFlow.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;

public abstract class BaseViewModel : ObservableObject
{
    private bool isBusy;
    private string title = string.Empty;

    public bool IsBusy
    {
        get => isBusy;
        set => SetProperty(ref isBusy, value);
    }

    public string Title
    {
        get => title;
        set => SetProperty(ref title, value);
    }
}
