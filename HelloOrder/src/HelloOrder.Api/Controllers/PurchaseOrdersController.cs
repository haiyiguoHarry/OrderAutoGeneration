using HelloOrder.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HelloOrder.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PurchaseOrdersController : ControllerBase
{
    [HttpGet]
    public ActionResult<ApiResult<PagedResult<object>>> Get([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        return Ok(ApiResult<PagedResult<object>>.Ok(new PagedResult<object> { List = new List<object>(), Total = 0, Page = page, PageSize = pageSize }));
    }
}
