using AbcRetail.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AbcRetail.Web.Controllers;

[Authorize(Roles="Admin")]
public class CustomerManagementController : Controller
{
    private readonly ICustomerRepository _customers;
    public CustomerManagementController(ICustomerRepository customers)
    {
        _customers = customers;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string q = "")
    {
        IEnumerable<AbcRetail.Core.Models.Customer> list;
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            var nameMatches = await _customers.SearchByNameAsync(q, 200);
            var emailMatches = await _customers.SearchByEmailAsync(q, 200);
            list = nameMatches.Concat(emailMatches).GroupBy(c=>c.CustomerId).Select(g=>g.First());
        }
        else
        {
            list = await _customers.ListAsync(200);
        }
        return View(list.OrderBy(c=>c.Email).ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(string id, bool disable, string? q = null)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            await _customers.SetDisabledAsync(id, disable);
        }
        return RedirectToAction("Index", new { q });
    }
}