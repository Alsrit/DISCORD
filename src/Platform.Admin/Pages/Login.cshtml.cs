using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Platform.Application.Services;

namespace Platform.Admin.Pages;

public sealed class LoginModel(IAdminPlatformService adminPlatformService) : PageModel
{
    [BindProperty]
    public LoginInputModel Input { get; set; } = new();

    public string ErrorMessage { get; private set; } = string.Empty;

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var result = await adminPlatformService.ValidateAdminCredentialsAsync(Input.UserName, Input.Password, cancellationToken);
        if (!result.Succeeded || result.Data is null)
        {
            ErrorMessage = result.Message;
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.Data.UserId.ToString()),
            new(ClaimTypes.Name, result.Data.UserName),
            new(ClaimTypes.GivenName, result.Data.DisplayName),
            new(ClaimTypes.Role, result.Data.Role)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8) });

        return RedirectToPage("/Index");
    }

    public sealed class LoginInputModel
    {
        [Required]
        public string UserName { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}
