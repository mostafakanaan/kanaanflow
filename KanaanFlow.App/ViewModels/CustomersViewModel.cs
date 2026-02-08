namespace KanaanFlow.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

public partial class CustomersViewModel : ObservableObject
{
    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<CustomerItem> customers = [];

    [ObservableProperty]
    private string totalCustomersText = "Showing 10 of 156 customers";

    public CustomersViewModel()
    {
        LoadMockData();
    }

    private void LoadMockData()
    {
        Customers =
        [
            new CustomerItem
            {
                Id = 1,
                Name = "John Smith",
                Email = "john.smith@email.com",
                Phone = "+1 555-0101",
                Status = "Active",
                AvatarColor = "#667eea"
            },
            new CustomerItem
            {
                Id = 2,
                Name = "Sarah Johnson",
                Email = "sarah.j@company.com",
                Phone = "+1 555-0102",
                Status = "Active",
                AvatarColor = "#17a2b8"
            },
            new CustomerItem
            {
                Id = 3,
                Name = "Michael Brown",
                Email = "m.brown@business.net",
                Phone = "+1 555-0103",
                Status = "Pending",
                AvatarColor = "#fd7e14"
            },
            new CustomerItem
            {
                Id = 4,
                Name = "Emily Davis",
                Email = "emily.davis@mail.com",
                Phone = "+1 555-0104",
                Status = "Active",
                AvatarColor = "#e83e8c"
            },
            new CustomerItem
            {
                Id = 5,
                Name = "Robert Wilson",
                Email = "r.wilson@corp.com",
                Phone = "+1 555-0105",
                Status = "Inactive",
                AvatarColor = "#28a745"
            },
            new CustomerItem
            {
                Id = 6,
                Name = "Jennifer Lee",
                Email = "j.lee@enterprise.com",
                Phone = "+1 555-0106",
                Status = "Active",
                AvatarColor = "#6f42c1"
            },
            new CustomerItem
            {
                Id = 7,
                Name = "David Martinez",
                Email = "david.m@startup.io",
                Phone = "+1 555-0107",
                Status = "Active",
                AvatarColor = "#20c997"
            },
            new CustomerItem
            {
                Id = 8,
                Name = "Lisa Anderson",
                Email = "l.anderson@tech.com",
                Phone = "+1 555-0108",
                Status = "Pending",
                AvatarColor = "#dc3545"
            },
            new CustomerItem
            {
                Id = 9,
                Name = "James Taylor",
                Email = "james.t@solutions.net",
                Phone = "+1 555-0109",
                Status = "Active",
                AvatarColor = "#007bff"
            },
            new CustomerItem
            {
                Id = 10,
                Name = "Amanda White",
                Email = "a.white@global.com",
                Phone = "+1 555-0110",
                Status = "Active",
                AvatarColor = "#ffc107"
            }
        ];
    }

    [RelayCommand]
    private void Search()
    {
        // Filter logic would go here
    }

    [RelayCommand]
    private async Task AddCustomerAsync()
    {
        await Shell.Current.DisplayAlert("Add Customer", "Add customer form coming soon!", "OK");
    }

    [RelayCommand]
    private async Task EditAsync(CustomerItem customer)
    {
        await Shell.Current.DisplayAlert("Edit Customer", $"Editing: {customer.Name}", "OK");
    }
}

public partial class CustomerItem : ObservableObject
{
    [ObservableProperty]
    private int id;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string phone = string.Empty;

    [ObservableProperty]
    private string status = string.Empty;

    [ObservableProperty]
    private string avatarColor = "#667eea";

    public string Initials => string.IsNullOrEmpty(Name) 
        ? "?" 
        : string.Concat(Name.Split(' ').Where(s => !string.IsNullOrEmpty(s)).Select(s => s[0])).ToUpperInvariant();

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
