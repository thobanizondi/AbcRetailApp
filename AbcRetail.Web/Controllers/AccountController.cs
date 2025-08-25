namespace AbcRetail.Web.Controllers;

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AbcRetail.Core.Interfaces;
using AbcRetail.Core.Models;
using System.ComponentModel.DataAnnotations;

public class AccountController : Controller
{
    private readonly ICustomerRepository _customers;
    public AccountController(ICustomerRepository customers)
    {
        _customers = customers;
    }
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        return View(new RegisterModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterModel model)
    {
        if (!ModelState.IsValid) return View(model);
        // Check if customer exists
        var existing = await _customers.GetAsync(model.CustomerId.Trim());
        if (existing != null)
        {
            ModelState.AddModelError("CustomerId", "A user with this email already exists.");
            return View(model);
        }
        var customer = new Customer
        {
            CustomerId = model.CustomerId.Trim(),
            Name = model.Name.Trim(),
            Email = model.CustomerId.Trim(),
            ShippingAddress = model.ShippingAddress?.Trim() ?? "",
            PasswordHash = HashPassword(model.Password)
        };
        await _customers.UpsertAsync(customer);
        // Sign in
        var claims = new List<Claim> {
            new Claim(ClaimTypes.Name, customer.CustomerId),
            new Claim(ClaimTypes.Role, "Customer")
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        return RedirectToAction("Index", "Home");
    }

    public class RegisterModel
    {
        [Required, EmailAddress, Display(Name="Email")]
        public string CustomerId { get; set; } = string.Empty;
        [Required, StringLength(120)]
        public string Name { get; set; } = string.Empty;
        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
        [Required, DataType(DataType.Password), Compare("Password")]
        public string ConfirmPassword { get; set; } = string.Empty;
        [StringLength(200)]
        public string? ShippingAddress { get; set; }
    }
    private static string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password)) return string.Empty;
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(password);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string username, string password, string? returnUrl = null)
    {
        // Demo only: hard-coded users.
        // customer: user/pass => customer1 / pass123
        // admin: admin / admin123
        string role;
        if (username == "admin" && password == "admin123")
        {
            role = "Admin";
        }
        else
        {
            // Look up customer in table storage
            var customer = await _customers.GetAsync(username.Trim());
            if (customer == null || string.IsNullOrEmpty(customer.PasswordHash))
            {
                ModelState.AddModelError(string.Empty, "Invalid credentials");
                return View();
            }
            // Verify password
            var attempted = HashPassword(password);
            if (!string.Equals(attempted, customer.PasswordHash, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(string.Empty, "Invalid credentials");
                return View();
            }
            role = "Customer";
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, username.Trim()),
            new Claim(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    public IActionResult AccessDenied() => View();
}
