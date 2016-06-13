using System.Web.Mvc;
using WebApiProxy.Tasks;

namespace WebApiProxy.Api.Sample.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            ViewBag.Title = "Home Page";

            var task = new ProxyGenerationTask(Server.MapPath("~/ProxiesFiles"));
            task.Generate();

            return Content("generate done!");
        }
    }
}
