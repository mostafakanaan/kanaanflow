namespace KanaanFlow.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class DashboardViewModel : ObservableObject
{
    [ObservableProperty]
    private string customerName = string.Empty;

    [ObservableProperty]
    private string validUntil = string.Empty;

    [ObservableProperty]
    private int totalCustomers = 156;

    [ObservableProperty]
    private int activePartners = 24;

    [ObservableProperty]
    private int familyMembers = 42;

    [ObservableProperty]
    private int monthlyReports = 12;

    [RelayCommand]
    private async Task ShowLicenseInfoAsync()
    {
        string message = string.IsNullOrEmpty(ValidUntil) 
            ? "License information not available" 
            : $"Customer: {CustomerName}\nValid Until: {ValidUntil}";
        await Shell.Current.DisplayAlert("License Information", message, "OK");
    }

    [RelayCommand]
    private async Task NavigateToReportsAsync()
    {
        await Shell.Current.GoToAsync("//Reports");
    }

    [RelayCommand]
    private async Task NavigateToCustomersAsync()
    {
        await Shell.Current.GoToAsync("//Customers");
    }

    [RelayCommand]
    private async Task NavigateToPartnersAsync()
    {
        await Shell.Current.GoToAsync("//Partners");
    }

    [RelayCommand]
    private async Task NavigateToFamilyAsync()
    {
        await Shell.Current.GoToAsync("//Family");
    }

    [RelayCommand]
    private async Task NavigateToCurrencyAsync()
    {
        await Shell.Current.DisplayAlert("Currency Converter", "Currency Converter feature coming soon!", "OK");
    }

    [RelayCommand]
    private async Task NavigateToNotesAsync()
    {
        await Shell.Current.DisplayAlert("Notes", "Notes feature coming soon!", "OK");
    }
}
