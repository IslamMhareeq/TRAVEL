using Microsoft.AspNetCore.Mvc;

namespace TRAVEL.Controllers
{
    /// <summary>
    /// Controller for Account Views (Login and Register pages)
    /// </summary>
    [Route("account")]
    public class AccountViewController : Controller
    {
        /// <summary>
        /// Login page
        /// </summary>
        [HttpGet("login")]
        public IActionResult Login()
        {
            return View("~/Views/Account/Login.cshtml");
        }

        /// <summary>
        /// Register page
        /// </summary>
        [HttpGet("register")]
        public IActionResult Register()
        {
            return View("~/Views/Account/Register.cshtml");
        }

        /// <summary>
        /// User dashboard page
        /// </summary>
        [HttpGet("dashboard")]
        public IActionResult Dashboard()
        {
            return View("~/Views/Account/Dashboard.cshtml");
        }

        /// <summary>
        /// User profile page
        /// </summary>
        [HttpGet("profile")]
        public IActionResult Profile()
        {
            return View("~/Views/Account/Profile.cshtml");
        }

        /// <summary>
        /// User bookings page
        /// </summary>
        [HttpGet("bookings")]
        public IActionResult MyBookings()
        {
            return View("~/Views/Account/MyBookings.cshtml");
        }
    }
}