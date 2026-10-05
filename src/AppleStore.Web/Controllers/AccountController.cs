using Microsoft.AspNetCore.Mvc;

namespace AppleStore.Web.Controllers;

// RED stub: every action answers 501 until the GREEN commit.
public class AccountController : Controller
{
    public IActionResult Index() => StatusCode(501);
}
