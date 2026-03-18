using HelloOrder.Application.Services;
using Microsoft.EntityFrameworkCore;

namespace HelloOrder.Infrastructure.Services;

public class DataScopeService : IDataScopeService
{
    private readonly AppDbContext _db;

    public DataScopeService(AppDbContext db) => _db = db;

    public async Task<List<Guid>?> GetVisibleBusinessUserIdsAsync(Guid? userId, string? roleCode, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(roleCode)) return new List<Guid>();
        if (roleCode == "Admin" || roleCode == "Boss") return null;
        if (roleCode == "Business" && userId.HasValue) return new List<Guid> { userId.Value };
        if (roleCode == "Assistant" && userId.HasValue)
        {
            var ids = await _db.UserBusinessAssistants
                .AsNoTracking()
                .Where(x => x.AssistantUserId == userId.Value)
                .Select(x => x.BusinessUserId)
                .ToListAsync(ct);
            return ids;
        }
        return new List<Guid>();
    }
}
