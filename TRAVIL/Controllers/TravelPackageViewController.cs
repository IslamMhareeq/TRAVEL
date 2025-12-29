using Microsoft.AspNetCore.Mvc;

namespace TRAVEL.Controllers
{
    /// <summary>
    /// Controller for Travel Package Views
    /// </summary>
    public class TravelPackageViewController : Controller
    {
        /// <summary>
        /// Package Gallery/Search - route: /packages OR /TravelPackage
        /// </summary>
        [HttpGet]
        [Route("packages")]
        [Route("TravelPackage")]
        public IActionResult Index()
        {
            return View("~/Views/TravelPackage/Index.cshtml");
        }

        /// <summary>
        /// Package Details - route: /packages/{id} OR /TravelPackage/Details/{id}
        /// </summary>
        [HttpGet]
        [Route("packages/{id}")]
        [Route("TravelPackage/Details/{id}")]
        public IActionResult Details(int id)
        {
            ViewData["PackageId"] = id;
            return View("~/Views/TravelPackage/Details.cshtml");
        }

        /// <summary>
        /// Search Packages
        /// </summary>
        [HttpGet]
        [Route("packages/search")]
        [Route("TravelPackage/Search")]
        public IActionResult Search()
        {
            return View("~/Views/TravelPackage/Index.cshtml");
        }
    }
}
