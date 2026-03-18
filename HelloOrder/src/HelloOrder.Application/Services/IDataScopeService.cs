namespace HelloOrder.Application.Services;

/// <summary>数据范围：当前用户可见的业务员ID列表。null=不按业务员过滤(Admin/Boss)，空列表=无数据，非空=仅这些业务员的数据。</summary>
public interface IDataScopeService
{
    Task<List<Guid>?> GetVisibleBusinessUserIdsAsync(Guid? userId, string? roleCode, CancellationToken ct = default);
}
