using Microsoft.AspNetCore.Mvc.RazorPages;

namespace VSServerStats.Web.Pages.Admin;

public class LoginModel : PageModel
{
    public bool Error => Request.Query.ContainsKey("error");
}
