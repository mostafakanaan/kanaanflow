namespace KanaanFlow.App.ViewModels;

using KanaanFlow.Core.Abstractions;
using KanaanFlow.Core.Models;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

public sealed class MainPageViewModel
{
    private readonly INoteRepository repo;

    public ObservableCollection<Note> Notes { get; } = new ObservableCollection<Note>();

    public ICommand RefreshCommand { get; }
    public ICommand AddSampleCommand { get; }

    public MainPageViewModel(INoteRepository repository)
    {
        repo = repository;

        RefreshCommand = new Command(async () => await RefreshAsync());
        AddSampleCommand = new Command(async () => await AddSampleAsync());
    }

    public async Task RefreshAsync()
    {
        var items = await repo.GetAllAsync(CancellationToken.None);

        Notes.Clear();
        for (int i = 0; i < items.Count; i++)
        {
            Notes.Add(items[i]);
        }
    }

    private async Task AddSampleAsync()
    {
        Note note = new()
        {
            Id = Guid.NewGuid(),
            Title = "Hello from Core @ " + DateTime.UtcNow.ToString("u"),
            CreatedUtc = DateTime.UtcNow
        };

        await repo.AddAsync(note, CancellationToken.None);
        await RefreshAsync();
    }
}
