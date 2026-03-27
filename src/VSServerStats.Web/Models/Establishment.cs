namespace VSServerStats.Web.Models;

public class Establishment
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime? FoundedIn { get; set; }
    public List<string> Pictures { get; set; } = new List<string>();

}
