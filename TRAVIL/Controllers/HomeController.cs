using Microsoft.AspNetCore.Mvc;

namespace TRAVEL.Controllers
{
    /// <summary>
    /// Controller for Home/Landing page
    /// </summary>
    public class HomeController : Controller
    {
        /// <summary>
        /// Home page - route: /
        /// </summary>
        [HttpGet]
        [Route("/")]
        [Route("/home")]
        [Route("/index")]
        public IActionResult Index()
        {
            return View("~/Views/Home/Index.cshtml");
        }
    }
}
