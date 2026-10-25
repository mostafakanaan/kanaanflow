namespace KanaanFlow.Web.Shared;

using System;
using KanaanFlow.Core.Finance;
using KanaanFlow.Web.Services;
using Microsoft.AspNetCore.Components;

/// <summary>
/// Base for every screen: gives access to the book and its text, and redraws when the book changes.
/// </summary>
public abstract class KfComponent : ComponentBase, IDisposable
{
    [Inject]
    protected BookService Books { get; set; } = default!;

    [Inject]
    protected ToastService Toasts { get; set; } = default!;

    [Inject]
    protected NavigationManager Nav { get; set; } = default!;

    protected Book Book => Books.Book;

    protected string Lang => Books.Language;

    protected DateOnly Today => Books.Today;

    protected string T(string key) => Books.T(key);

    protected string T(string key, int number) => Books.T(key, number);

    protected string Sym(Currency currency) => Books.Sym(currency);

    public virtual void Dispose()
    {
        Books.Changed -= OnBookChanged;
        GC.SuppressFinalize(this);
    }

    protected override void OnInitialized()
    {
        Books.Changed += OnBookChanged;
    }

    private void OnBookChanged() => InvokeAsync(StateHasChanged);
}
