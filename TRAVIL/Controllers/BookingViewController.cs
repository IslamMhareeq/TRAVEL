using Microsoft.AspNetCore.Mvc;

namespace TRAVEL.Controllers
{
    /// <summary>
    /// Controller for Booking Views
    /// </summary>
    public class BookingViewController : Controller
    {
        /// <summary>
        /// Create Booking - route: /booking/create/{packageId}
        /// </summary>
        [HttpGet]
        [Route("booking/create/{packageId?}")]
        public IActionResult Create(int? packageId)
        {
            ViewData["PackageId"] = packageId ?? 1;
            return View("~/Views/Booking/Create.cshtml");
        }

        /// <summary>
        /// Booking Details - route: /booking/{id} OR /booking/details/{id}
        /// </summary>
        [HttpGet]
        [Route("booking/{id}")]
        [Route("booking/details/{id}")]
        public IActionResult Details(int id)
        {
            ViewData["BookingId"] = id;
            return View("~/Views/Booking/Details.cshtml");
        }

        /// <summary>
        /// Payment page (placeholder)
        /// </summary>
        [HttpGet]
        [Route("booking/payment/{id}")]
        public IActionResult Payment(int id)
        {
            ViewData["BookingId"] = id;
            // For now, redirect to details
            return View("~/Views/Booking/Details.cshtml");
        }
    }
}
