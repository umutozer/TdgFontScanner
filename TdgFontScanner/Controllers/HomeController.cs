using Microsoft.AspNetCore.Mvc;

namespace TdgFontScanner.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
