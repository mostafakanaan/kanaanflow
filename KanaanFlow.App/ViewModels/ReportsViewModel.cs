namespace KanaanFlow.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

public partial class ReportsViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<ReportItem> reports = [];

    public ReportsViewModel()
    {
        LoadMockData();
    }

    private void LoadMockData()
    {
        Reports =
        [
            new ReportItem
            {
                Id = 1,
                Name = "Monthly Sales Report",
                Description = "Sales performance for December 2024",
                Type = "Sales",
                Date = "Dec 15, 2024",
                Status = "Completed"
            },
            new ReportItem
            {
                Id = 2,
                Name = "Customer Analytics",
                Description = "Customer behavior and trends",
                Type = "Analytics",
                Date = "Dec 12, 2024",
                Status = "Completed"
            },
            new ReportItem
            {
                Id = 3,
                Name = "Financial Summary Q4",
                Description = "Quarterly financial overview",
                Type = "Finance",
                Date = "Dec 10, 2024",
                Status = "Pending"
            },
            new ReportItem
            {
                Id = 4,
                Name = "Partner Performance",
                Description = "Partner contribution analysis",
                Type = "Partners",
                Date = "Dec 8, 2024",
                Status = "Completed"
            },
            new ReportItem
            {
                Id = 5,
                Name = "Inventory Report",
                Description = "Stock levels and movement",
                Type = "Inventory",
                Date = "Dec 5, 2024",
                Status = "Draft"
            },
            new ReportItem
            {
                Id = 6,
                Name = "Marketing ROI",
                Description = "Campaign effectiveness metrics",
                Type = "Marketing",
                Date = "Dec 1, 2024",
                Status = "Completed"
            }
        ];
    }

    [RelayCommand]
    private async Task ViewReportAsync(ReportItem report)
    {
        await Shell.Current.DisplayAlert("View Report", $"Opening: {report.Name}", "OK");
    }

    [RelayCommand]
    private async Task GenerateReportAsync()
    {
        await Shell.Current.DisplayAlert("Generate Report", "Report generation wizard coming soon!", "OK");
    }
}

public partial class ReportItem : ObservableObject
{
    [ObservableProperty]
    private int id;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string description = string.Empty;

    [ObservableProperty]
    private string type = string.Empty;

    [ObservableProperty]
    private string date = string.Empty;

    [ObservableProperty]
    private string status = string.Empty;

    public Color TypeColor => Type switch
    {
        "Sales" => Color.FromArgb("#667eea"),
        "Analytics" => Color.FromArgb("#17a2b8"),
        "Finance" => Color.FromArgb("#28a745"),
        "Partners" => Color.FromArgb("#fd7e14"),
        "Inventory" => Color.FromArgb("#6f42c1"),
        "Marketing" => Color.FromArgb("#e83e8c"),
        _ => Color.FromArgb("#6c757d")
    };

    public Color StatusColor => Status switch
    {
        "Completed" => Color.FromArgb("#E8F5E9"),
        "Pending" => Color.FromArgb("#FFF3E0"),
        "Draft" => Color.FromArgb("#E3F2FD"),
        _ => Color.FromArgb("#E0E0E0")
    };

    public Color StatusTextColor => Status switch
    {
        "Completed" => Color.FromArgb("#2E7D32"),
        "Pending" => Color.FromArgb("#E65100"),
        "Draft" => Color.FromArgb("#1565C0"),
        _ => Color.FromArgb("#616161")
    };
}
