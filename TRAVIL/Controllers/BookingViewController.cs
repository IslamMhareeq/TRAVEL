using Microsoft.AspNetCore.Mvc;

namespace TRAVEL.Controllers
{
    /// <summary>
    /// Controller for Booking Views
    /// </summary>
    [Route("booking")]
    public class BookingViewController : Controller
    {
        /// <summary>
        /// Create Booking page - /booking/create/{packageId}
        /// </summary>
        [HttpGet("create")]
        [HttpGet("create/{packageId:int}")]
        public IActionResult Create(int? packageId)
        {
            ViewData["PackageId"] = packageId ?? 1;
            return View("~/Views/Booking/Create.cshtml");
        }

        /// <summary>
        /// Booking Details - /booking/{id} or /booking/details/{id}
        /// </summary>
        [HttpGet("{id:int}")]
        [HttpGet("details/{id:int}")]
        public IActionResult Details(int id)
        {
            ViewData["BookingId"] = id;
            return View("~/Views/Booking/Details.cshtml");
        }

        /// <summary>
        /// Payment page - /booking/payment/{bookingId}
        /// </summary>
        [HttpGet("payment/{id:int}")]
        public IActionResult Payment(int id)
        {
            ViewData["BookingId"] = id;
            return View("~/Views/Booking/Payment.cshtml");
        }
        /// <summary>
        /// Buy Now page - /booking/buynow/{packageId}
        /// Direct purchase with payment in one step
        /// </summary>
        [HttpGet("buynow/{packageId:int}")]
        public IActionResult BuyNow(int packageId)
        {
            ViewData["PackageId"] = packageId;
            return View("~/Views/Booking/BuyNow.cshtml");
        }
        /// <summary>
        /// Checkout page (for cart) - /booking/checkout
        /// </summary>
        [HttpGet("checkout")]
        public IActionResult Checkout()
        {
            return View("~/Views/Booking/Checkout.cshtml");
        }

        /// <summary>
        /// Payment Success page - /booking/success
        /// </summary>
        [HttpGet("success")]
        public IActionResult Success()
        {
            return View("~/Views/Booking/Success.cshtml");
        }

        /// <summary>
        /// My Bookings page - /booking/my-bookings
        /// </summary>
        [HttpGet("my-bookings")]
        public IActionResult MyBookings()
        {
            return View("~/Views/Account/MyBookings.cshtml");
        }
    }
}