using Bogus;
using Pmo.Domain.Finance;
using Pmo.Domain.Portfolio;

namespace DataGenerator.Generation;

/// <summary>Departments, users (2 admins, 5 PMs, 11 contributors) and cost categories.</summary>
public static class PeopleSeeder
{
    public const string Admin = "Administrator";
    public const string ProjectManager = "ProjectManager";
    public const string Contributor = "Contributor";

    // Department ids: 1 DIGI, 2 NETW, 3 ENTS. PM i manages projects of the matching department (see ProjectSeeder).
    private static readonly int[] PmDepartments = [1, 2, 3, 2, 3];

    public static void Seed(SeedData data, Faker faker)
    {
        data.Departments.Add(new Department { Id = 1, Code = "DIGI", Name = "Digital Channels" });
        data.Departments.Add(new Department { Id = 2, Code = "NETW", Name = "Network Engineering" });
        data.Departments.Add(new Department { Id = 3, Code = "ENTS", Name = "Enterprise Solutions" });

        var id = 1;
        for (var i = 1; i <= 2; i++)
        {
            data.Users.Add(new SeedUser(id++, $"admin{i}@pmo.example", faker.Name.FullName(), i == 1 ? 3 : 1,
                "PMO Administrator", Admin));
        }

        for (var i = 1; i <= 5; i++)
        {
            data.Users.Add(new SeedUser(id++, $"pm{i}@pmo.example", faker.Name.FullName(), PmDepartments[i - 1],
                "Project Manager", ProjectManager));
        }

        string[] titles = ["Software Engineer", "Business Analyst", "QA Engineer", "Network Engineer", "Data Engineer"];
        for (var i = 1; i <= 11; i++)
        {
            data.Users.Add(new SeedUser(id++, $"con{i}@pmo.example", faker.Name.FullName(), i % 3 + 1,
                titles[i % titles.Length], Contributor));
        }

        string[][] categories =
        [
            ["LABOUR", "Labour"], ["SOFTWARE", "Software Licences"], ["HARDWARE", "Hardware"],
            ["SERVICES", "Professional Services"], ["TRAVEL", "Travel"]
        ];
        var categoryId = 1;
        foreach (var category in categories)
        {
            data.Categories.Add(new CostCategory { Id = categoryId++, Code = category[0], Name = category[1] });
        }
    }

    public static int AdminId(SeedData data, int n) => data.UserByEmail($"admin{n}@pmo.example").Id;

    public static int PmId(SeedData data, int n) => data.UserByEmail($"pm{n}@pmo.example").Id;

    public static IReadOnlyList<int> ContributorIds(SeedData data) =>
        data.Users.Where(u => u.Role == Contributor).Select(u => u.Id).ToList();
}
