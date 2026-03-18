namespace HelloOrder.Api.Common;

public class ApiResult<T>
{
    public int Code { get; set; }
    public string Message { get; set; } = "";
    public T? Data { get; set; }

    public static ApiResult<T> Ok(T data) => new() { Code = 0, Message = "success", Data = data };
    public static ApiResult<T> Fail(string msg) => new() { Code = 1, Message = msg };
}

public class PagedResult<T>
{
    public List<T> List { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
