using Microsoft.EntityFrameworkCore;
using Pmo.Application.Common.Abstractions;

namespace Pmo.Application.Features.Lookups;

public record LookupItem(int Id, string Code, string Name);
