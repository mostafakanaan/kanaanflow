namespace KanaanFlow.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

public partial class FamilyViewModel : ObservableObject
{
    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<FamilyMemberItem> familyMembers = [];

    public FamilyViewModel()
    {
        LoadMockData();
    }

    private void LoadMockData()
    {
        FamilyMembers =
        [
            new FamilyMemberItem
            {
                Id = 1,
                Name = "Ahmad Kanaan",
                Relationship = "Father",
                Age = 55,
                Phone = "+1 555-0201",
                Email = "ahmad.k@family.com",
                AvatarColor = "#667eea"
            },
            new FamilyMemberItem
            {
                Id = 2,
                Name = "Fatima Kanaan",
                Relationship = "Mother",
                Age = 52,
                Phone = "+1 555-0202",
                Email = "fatima.k@family.com",
                AvatarColor = "#e83e8c"
            },
            new FamilyMemberItem
            {
                Id = 3,
                Name = "Omar Kanaan",
                Relationship = "Brother",
                Age = 28,
                Phone = "+1 555-0203",
                Email = "omar.k@family.com",
                AvatarColor = "#17a2b8"
            },
            new FamilyMemberItem
            {
                Id = 4,
                Name = "Layla Kanaan",
                Relationship = "Sister",
                Age = 25,
                Phone = "+1 555-0204",
                Email = "layla.k@family.com",
                AvatarColor = "#fd7e14"
            },
            new FamilyMemberItem
            {
                Id = 5,
                Name = "Yusuf Kanaan",
                Relationship = "Uncle",
                Age = 60,
                Phone = "+1 555-0205",
                Email = "yusuf.k@family.com",
                AvatarColor = "#28a745"
            },
            new FamilyMemberItem
            {
                Id = 6,
                Name = "Maryam Hassan",
                Relationship = "Aunt",
                Age = 48,
                Phone = "+1 555-0206",
                Email = "maryam.h@family.com",
                AvatarColor = "#6f42c1"
            },
            new FamilyMemberItem
            {
                Id = 7,
                Name = "Ibrahim Kanaan",
                Relationship = "Grandfather",
                Age = 78,
                Phone = "+1 555-0207",
                Email = "ibrahim.k@family.com",
                AvatarColor = "#20c997"
            },
            new FamilyMemberItem
            {
                Id = 8,
                Name = "Salma Kanaan",
                Relationship = "Grandmother",
                Age = 75,
                Phone = "+1 555-0208",
                Email = "salma.k@family.com",
                AvatarColor = "#dc3545"
            }
        ];
    }

    [RelayCommand]
    private async Task AddMemberAsync()
    {
        await Shell.Current.DisplayAlert("Add Family Member", "Add member form coming soon!", "OK");
    }

    [RelayCommand]
    private async Task EditMemberAsync(FamilyMemberItem member)
    {
        await Shell.Current.DisplayAlert("Edit Member", $"Editing: {member.Name}", "OK");
    }

    [RelayCommand]
    private async Task CallMemberAsync(FamilyMemberItem member)
    {
        await Shell.Current.DisplayAlert("Call", $"Calling: {member.Name} at {member.Phone}", "OK");
    }
}

public partial class FamilyMemberItem : ObservableObject
{
    [ObservableProperty]
    private int id;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string relationship = string.Empty;

    [ObservableProperty]
    private int age;

    [ObservableProperty]
    private string phone = string.Empty;

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string avatarColor = "#667eea";

    public string Initials => string.IsNullOrEmpty(Name) 
        ? "?" 
        : string.Concat(Name.Split(' ').Where(s => !string.IsNullOrEmpty(s)).Select(s => s[0])).ToUpperInvariant();

    public Color RelationshipColor => Relationship switch
    {
        "Father" or "Mother" => Color.FromArgb("#667eea"),
        "Brother" or "Sister" => Color.FromArgb("#17a2b8"),
        "Uncle" or "Aunt" => Color.FromArgb("#fd7e14"),
        "Grandfather" or "Grandmother" => Color.FromArgb("#6f42c1"),
        "Cousin" => Color.FromArgb("#28a745"),
        "Spouse" => Color.FromArgb("#e83e8c"),
        _ => Color.FromArgb("#6c757d")
    };
}
