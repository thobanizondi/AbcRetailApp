using AbcRetail.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AbcRetail.Web.Controllers;

[Route("api/customers")] 
[ApiController]
public class CustomerController : ControllerBase
{
    private readonly ICustomerRepository _customers;
    public CustomerController(ICustomerRepository customers) => _customers = customers;

    [HttpGet("search")] 
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int take = 10)
    {
        var results = await _customers.SearchByNameAsync(q, take);
        return Ok(results.Select(c => new { id = c.CustomerId, name = c.Name, email = c.Email }));
    }
}
