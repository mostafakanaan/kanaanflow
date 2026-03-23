namespace KanaanFlow.App.ViewModels;

using CommunityToolkit.Mvvm.Input;
using KanaanFlow.Core.Abstractions;
using KanaanFlow.Core.Models;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

public sealed partial class ReportsViewModel : BaseViewModel
{
    private readonly IReportService reportService;

    private string selectedPeriod = "Daily";

    public string SelectedPeriod
    {
        get => selectedPeriod;
        set => SetProperty(ref selectedPeriod, value);
    }

    public ObservableCollection<DailyReport> Reports { get; } = new ObservableCollection<DailyReport>();
    public ObservableCollection<string> Periods { get; } = new ObservableCollection<string>
    {
        "Daily",
        "Weekly",
        "Monthly"
    };

    public ReportsViewModel(IReportService reportServiceInstance)
    {
        reportService = reportServiceInstance;
        Title = "Reports";
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            Reports.Clear();

            if (SelectedPeriod == "Daily")
            {
                DailyReport report = await reportService.GenerateDailySummaryAsync(DateTime.Today, CancellationToken.None);
                Reports.Add(report);
            }
            else if (SelectedPeriod == "Weekly")
            {
                DateTime weekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
                IReadOnlyList<DailyReport> reports = await reportService.GenerateWeeklySummaryAsync(weekStart, CancellationToken.None);
                foreach (DailyReport r in reports)
                {
                    Reports.Add(r);
                }
            }
            else if (SelectedPeriod == "Monthly")
            {
                IReadOnlyList<DailyReport> reports = await reportService.GenerateMonthlySummaryAsync(DateTime.Today.Year, DateTime.Today.Month, CancellationToken.None);
                foreach (DailyReport r in reports)
                {
                    Reports.Add(r);
                }
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
