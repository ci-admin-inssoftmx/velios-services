using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using VeliosProveedores.Models;

namespace velios.Api.Controllers
{
    /// <summary>
    /// Controlador encargado de autenticación del proveedor.
    /// </summary>
    public class AccountController : Controller
    {
        private readonly ILogger<AccountController> _logger;
        private readonly IAuthenticationService _authenticationService;

        public AccountController(
            ILogger<AccountController> logger,
            IAuthenticationService authenticationService)
        {
            _logger = logger;
            _authenticationService = authenticationService;
        }

        /// <summary>
        /// Muestra la pantalla de login.
        /// </summary>
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View(new LoginViewModel());
        }

        /// <summary>
        /// Procesa el inicio de sesión del proveedor.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                _logger.LogInformation("Intentando iniciar sesión para el correo: {Email}", model.Email);

                // =========================================================
                // VALIDACIÓN TEMPORAL
                // =========================================================
                // Reemplaza esta parte por tu llamada real al servicio/API:
                //
                // var result = await _authenticationService.LoginAsync(model.Email, model.Password);
                // if (!result.Success) { ... }
                // =========================================================

                var loginValido =
                    model.Email.Equals("proveedor@velios.com", StringComparison.OrdinalIgnoreCase) &&
                    model.Password == "Velios123!";

                if (!loginValido)
                {
                    _logger.LogWarning("Login inválido para el correo: {Email}", model.Email);
                    ModelState.AddModelError(string.Empty, "Correo o contraseña incorrectos.");
                    return View(model);
                }

                // Claims del usuario autenticado
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, model.Email),
                    new Claim(ClaimTypes.Email, model.Email),
                    new Claim(ClaimTypes.Role, "Proveedor")
                };

                var claimsIdentity = new ClaimsIdentity(
                    claims,
                    CookieAuthenticationDefaults.AuthenticationScheme);

                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = model.RememberMe,
                    ExpiresUtc = model.RememberMe
                        ? DateTimeOffset.UtcNow.AddDays(7)
                        : DateTimeOffset.UtcNow.AddHours(8)
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                _logger.LogInformation("Login exitoso para el correo: {Email}", model.Email);

                if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                return RedirectToAction("Dashboard", "Home");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al iniciar sesión para el correo: {Email}", model.Email);
                ModelState.AddModelError(string.Empty, "Ocurrió un error al iniciar sesión. Intenta nuevamente.");
                return View(model);
            }
        }

        /// <summary>
        /// Cierra la sesión actual.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }

        /// <summary>
        /// Pantalla para acceso denegado.
        /// </summary>
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}