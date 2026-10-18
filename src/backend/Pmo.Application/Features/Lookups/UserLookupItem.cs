using Microsoft.EntityFrameworkCore;
using Pmo.Application.Common.Abstractions;

namespace Pmo.Application.Features.Lookups;

public record UserLookupItem(int Id, string FullName, string Email, string Role, int DepartmentId);
