namespace HelloOrder.Core.Entities;

public class SysOperationLog
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string? Module { get; set; }
    public string? Action { get; set; }
    public string? TargetId { get; set; }
    public string? Detail { get; set; }
    public string? Ip { get; set; }
    public DateTime CreatedAt { get; set; }
}
