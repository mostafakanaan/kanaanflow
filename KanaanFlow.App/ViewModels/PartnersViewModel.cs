namespace KanaanFlow.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

public partial class PartnersViewModel : ObservableObject
{
    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<PartnerItem> partners = [];

    public PartnersViewModel()
    {
        LoadMockData();
    }

    private void LoadMockData()
    {
        Partners =
        [
            new PartnerItem
            {
                Id = 1,
                CompanyName = "Tech Solutions Inc.",
                ContactPerson = "Mark Johnson",
                PartnerType = "Technology",
                PartnerSince = "Jan 2022",
                Revenue = "$125,000",
                Status = "Active",
                AvatarColor = "#667eea"
            },
            new PartnerItem
            {
                Id = 2,
                CompanyName = "Global Marketing Co.",
                ContactPerson = "Sarah Williams",
                PartnerType = "Marketing",
                PartnerSince = "Mar 2021",
                Revenue = "$89,500",
                Status = "Active",
                AvatarColor = "#17a2b8"
            },
            new PartnerItem
            {
                Id = 3,
                CompanyName = "Logistics Plus",
                ContactPerson = "David Chen",
                PartnerType = "Logistics",
                PartnerSince = "Jun 2023",
                Revenue = "$45,200",
                Status = "Pending",
                AvatarColor = "#fd7e14"
            },
            new PartnerItem
            {
                Id = 4,
                CompanyName = "Creative Design Studio",
                ContactPerson = "Emma Thompson",
                PartnerType = "Design",
                PartnerSince = "Sep 2022",
                Revenue = "$67,800",
                Status = "Active",
                AvatarColor = "#e83e8c"
            },
            new PartnerItem
            {
                Id = 5,
                CompanyName = "Finance Partners LLC",
                ContactPerson = "Robert Miller",
                PartnerType = "Finance",
                PartnerSince = "Dec 2020",
                Revenue = "$234,000",
                Status = "Active",
                AvatarColor = "#28a745"
            },
            new PartnerItem
            {
                Id = 6,
                CompanyName = "Cloud Services Corp.",
                ContactPerson = "Jennifer Garcia",
                PartnerType = "Technology",
                PartnerSince = "Feb 2023",
                Revenue = "$156,300",
                Status = "Active",
                AvatarColor = "#6f42c1"
            }
        ];
    }

    [RelayCommand]
    private async Task AddPartnerAsync()
    {
        await Shell.Current.DisplayAlert("Add Partner", "Add partner form coming soon!", "OK");
    }
}

public partial class PartnerItem : ObservableObject
{
    [ObservableProperty]
    private int id;

    [ObservableProperty]
    private string companyName = string.Empty;

    [ObservableProperty]
    private string contactPerson = string.Empty;

    [ObservableProperty]
    private string partnerType = string.Empty;

    [ObservableProperty]
    private string partnerSince = string.Empty;

    [ObservableProperty]
    private string revenue = string.Empty;

    [ObservableProperty]
    private string status = string.Empty;

    [ObservableProperty]
    private string avatarColor = "#667eea";

    public string Initials => string.IsNullOrEmpty(CompanyName) 
        ? "?" 
        : string.Concat(CompanyName.Split(' ').Where(s => !string.IsNullOrEmpty(s)).Take(2).Select(s => s[0])).ToUpperInvariant();

    public Color StatusColor => Status switch
    {
        "Active" => Color.FromArgb("#E8F5E9"),
        "Pending" => Color.FromArgb("#FFF3E0"),
        "Inactive" => Color.FromArgb("#FFEBEE"),
        _ => Color.FromArgb("#E0E0E0")
    };

    public Color StatusTextColor => Status switch
    {
        "Active" => Color.FromArgb("#2E7D32"),
        "Pending" => Color.FromArgb("#E65100"),
        "Inactive" => Color.FromArgb("#C62828"),
        _ => Color.FromArgb("#616161")
    };
}
