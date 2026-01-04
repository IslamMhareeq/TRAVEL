using Microsoft.AspNetCore.Mvc;

namespace TRAVEL.Controllers
{
    /// <summary>
    /// Controller for Admin Views
    /// </summary>
    [Route("admin")]
    public class AdminViewController : Controller
    {
        /// <summary>
        /// Admin Dashboard
        /// </summary>
        [HttpGet("")]
        [HttpGet("dashboard")]
        public IActionResult Dashboard()
        {
            return View("~/Views/Admin/Dashboard.cshtml");
        }

        /// <summary>
        /// Admin Profile
        /// </summary>
        [HttpGet("profile")]
        public IActionResult Profile()
        {
            return View("~/Views/Admin/Profile.cshtml");
        }

        /// <summary>
        /// Admin Packages List
        /// </summary>
        [HttpGet("packages")]
        public IActionResult Packages()
        {
            return View("~/Views/Admin/Packages.cshtml");
        }

        /// <summary>
        /// Create New Package (no ID)
        /// </summary>
        [HttpGet("packages/edit")]
        [HttpGet("packages/create")]
        public IActionResult CreatePackage()
        {
            ViewData["PackageId"] = null;
            return View("~/Views/Admin/EditPackage.cshtml");
        }

        /// <summary>
        /// Edit Existing Package (with ID)
        /// </summary>
        [HttpGet("packages/edit/{id:int}")]
        public IActionResult EditPackage(int id)
        {
            ViewData["PackageId"] = id;
            return View("~/Views/Admin/EditPackage.cshtml");
        }

        /// <summary>
        /// Admin Bookings
        /// </summary>
        [HttpGet("bookings")]
        public IActionResult Bookings()
        {
            return View("~/Views/Admin/Bookings.cshtml");
        }

        /// <summary>
        /// Admin Users
        /// </summary>
        [HttpGet("users")]
        public IActionResult Users()
        {
            return View("~/Views/Admin/Users.cshtml");
        }

        /// <summary>
        /// Admin Reviews
        /// </summary>
        [HttpGet("reviews")]
        public IActionResult Reviews()
        {
            return View("~/Views/Admin/Reviews.cshtml");
        }

        /// <summary>
        /// Admin Prices/Discounts
        /// </summary>
        [HttpGet("prices")]
        public IActionResult Prices()
        {
            return View("~/Views/Admin/Prices.cshtml");
        }

        /// <summary>
        /// Admin Settings
        /// </summary>
        [HttpGet("settings")]
        public IActionResult Settings()
        {
            return View("~/Views/Admin/Settings.cshtml");
        }
    }
}