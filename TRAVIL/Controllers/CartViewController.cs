using Microsoft.AspNetCore.Mvc;

namespace TRAVEL.Controllers
{
    /// <summary>
    /// Controller for Cart Views
    /// </summary>
    public class CartViewController : Controller
    {
        /// <summary>
        /// Cart page - shows all items in cart
        /// </summary>
        [HttpGet]
        [Route("cart")]
        public IActionResult Index()
        {
            return View("~/Views/Cart/Index.cshtml");
        }

        /// <summary>
        /// Checkout page
        /// </summary>
        [HttpGet]
        [Route("cart/checkout")]
        [Route("checkout")]
        public IActionResult Checkout()
        {
            return View("~/Views/Cart/Checkout.cshtml");
        }

        /// <summary>
        /// Order confirmation page
        /// </summary>
        [HttpGet]
        [Route("cart/confirmation")]
        [Route("order/confirmation")]
        public IActionResult Confirmation()
        {
            return View("~/Views/Cart/Confirmation.cshtml");
        }
    }
}