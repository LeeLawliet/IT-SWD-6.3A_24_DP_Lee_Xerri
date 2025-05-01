using Microsoft.AspNetCore.Mvc;

namespace CatalogAPI.Models
{
    public class Product
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
