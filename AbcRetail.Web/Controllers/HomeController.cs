using Microsoft.AspNetCore.Mvc;

namespace AbcRetail.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => View();
}
