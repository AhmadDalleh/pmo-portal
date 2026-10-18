using Microsoft.AspNetCore.Mvc;
using Pmo.Application.Features.Lookups;

namespace Pmo.Api.Controllers;

/// <summary>B6. Dropdown data for forms. Any signed-in user.</summary>
[ApiController]
[Route("api/lookups")]
public class LookupsController(LookupService lookups) : ControllerBase
{
    [HttpGet("departments")]
    public async Task<ActionResult<List<LookupItem>>> Departments(CancellationToken ct) => Ok(await lookups.DepartmentsAsync(ct));

    [HttpGet("users")]
    public async Task<ActionResult<List<UserLookupItem>>> Users([FromQuery] string? role, CancellationToken ct)
        => Ok(await lookups.UsersAsync(role, ct));

    [HttpGet("cost-categories")]
    public async Task<ActionResult<List<LookupItem>>> CostCategories(CancellationToken ct) => Ok(await lookups.CostCategoriesAsync(ct));
}
