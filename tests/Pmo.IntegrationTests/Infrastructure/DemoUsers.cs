namespace Pmo.IntegrationTests.Infrastructure;

/// <summary>
/// Seeded demo accounts (tools/DataGenerator). Memberships that tests rely on:
/// pm1 owns PRJ-001/006/009, pm2 owns PRJ-002/003, pm3 owns PRJ-004/005, pm4 owns PRJ-007/010 and is only a contributor on PRJ-001,
/// pm5 owns PRJ-008. Contributors are spread over the projects (see project_members).
/// </summary>
public static class DemoUsers
{
    public const string Admin1 = "admin1@pmo.example";
    public const string Admin2 = "admin2@pmo.example";
    public const string Pm1 = "pm1@pmo.example";
    public const string Pm2 = "pm2@pmo.example";
    public const string Pm3 = "pm3@pmo.example";
    public const string Pm4 = "pm4@pmo.example";
    public const string Pm5 = "pm5@pmo.example";
    public const string Con1 = "con1@pmo.example";
}
