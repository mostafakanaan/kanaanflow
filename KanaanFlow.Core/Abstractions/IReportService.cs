namespace KanaanFlow.Core.Abstractions;

using KanaanFlow.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface IReportService
{
    Task<DailyReport> GenerateDailySummaryAsync(DateTime date, CancellationToken cancellationToken);
    Task<IReadOnlyList<DailyReport>> GenerateWeeklySummaryAsync(DateTime weekStart, CancellationToken cancellationToken);
    Task<IReadOnlyList<DailyReport>> GenerateMonthlySummaryAsync(int year, int month, CancellationToken cancellationToken);
}
