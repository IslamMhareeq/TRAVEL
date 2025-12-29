using Microsoft.AspNetCore.Mvc;

namespace TRAVEL.Controllers
{
    /// <summary>
    /// Controller for Admin Views
    /// </summary>
    public class AdminViewController : Controller
    {
        /// <summary>
        /// Admin Dashboard
        /// </summary>
        [HttpGet]
        [Route("admin")]
        [Route("admin/dashboard")]
        public IActionResult Dashboard()
        {
            return View("~/Views/Admin/Dashboard.cshtml");
        }

        /// <summary>
        /// Admin Profile
        /// </summary>
        [HttpGet]
        [Route("admin/profile")]
        public IActionResult Profile()
        {
            return View("~/Views/Admin/Profile.cshtml");
        }

        /// <summary>
        /// Admin Packages
        /// </summary>
        [HttpGet]
        [Route("admin/packages")]
        public IActionResult Packages()
        {
            return View("~/Views/Admin/Packages.cshtml");
        }

        /// <summary>
        /// Edit Package
        /// </summary>
        [HttpGet]
        [Route("admin/packages/edit")]
        [Route("admin/packages/edit/{id?}")]
        public IActionResult EditPackage(int? id)
        {
            ViewData["PackageId"] = id;
            return View("~/Views/Admin/EditPackage.cshtml");
        }

        /// <summary>
        /// Admin Bookings
        /// </summary>
        [HttpGet]
        [Route("admin/bookings")]
        public IActionResult Bookings()
        {
            return View("~/Views/Admin/Bookings.cshtml");
        }

        /// <summary>
        /// Admin Users
        /// </summary>
        [HttpGet]
        [Route("admin/users")]
        public IActionResult Users()
        {
            return View("~/Views/Admin/Users.cshtml");
        }

        /// <summary>
        /// Admin Reviews
        /// </summary>
        [HttpGet]
        [Route("admin/reviews")]
        public IActionResult Reviews()
        {
            return View("~/Views/Admin/Reviews.cshtml");
        }

        /// <summary>
        /// Admin Prices/Discounts
        /// </summary>
        [HttpGet]
        [Route("admin/prices")]
        public IActionResult Prices()
        {
            return View("~/Views/Admin/Prices.cshtml");
        }

        /// <summary>
        /// Admin Settings
        /// </summary>
        [HttpGet]
        [Route("admin/settings")]
        public IActionResult Settings()
        {
            return View("~/Views/Admin/Settings.cshtml");
        }
    }
}
