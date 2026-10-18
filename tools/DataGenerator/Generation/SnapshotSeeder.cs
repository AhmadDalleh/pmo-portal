using Pmo.Domain.Delivery;

namespace DataGenerator.Generation;

/// <summary>
/// One progress snapshot per project per month-end. The last snapshot equals today's real progress and spend; earlier ones
/// ramp up linearly, which is enough to draw a believable history line.
/// </summary>
public static class SnapshotSeeder
{
    public static void Seed(SeedData data)
    {
        long id = 1;
        foreach (var project in data.Projects)
        {
            var limit = SeedClock.Min(SeedClock.AsOf, project.ActualEndDate ?? SeedClock.AsOf);
            var finalProgress = SeedMath.Progress(data, project.Id) ?? 0m;
            var tasks = data.Tasks.Where(t => t.ProjectId == project.Id && t.Status != ProjectTaskStatus.Cancelled).ToList();
            var doneNow = tasks.Count(t => t.Status == ProjectTaskStatus.Done);
            var span = Math.Max(1, limit.DayNumber - project.StartDate.DayNumber);

            foreach (var date in SnapshotDates(project.StartDate, limit))
            {
                var ramp = date == limit ? 1m : (decimal)(date.DayNumber - project.StartDate.DayNumber) / span;
                data.Snapshots.Add(new ProgressSnapshot
                {
                    Id = id++, ProjectId = project.Id, SnapshotDate = date,
                    ProgressPct = Math.Round(finalProgress * ramp, 2), TasksTotal = tasks.Count,
                    TasksDone = (int)Math.Round(doneNow * ramp), ActualSpend = SeedMath.ActualSpend(data, project.Id, date)
                });
            }
        }
    }

    /// <summary>Month-ends from the project start up to the limit, plus the limit itself.</summary>
    private static List<DateOnly> SnapshotDates(DateOnly start, DateOnly limit)
    {
        var dates = new List<DateOnly>();
        for (var month = SeedClock.MonthStart(start); ; month = month.AddMonths(1))
        {
            var monthEnd = month.AddMonths(1).AddDays(-1);
            if (monthEnd >= limit)
            {
                break;
            }

            if (monthEnd >= start)
            {
                dates.Add(monthEnd);
            }
        }

        dates.Add(limit);
        return dates;
    }
}
