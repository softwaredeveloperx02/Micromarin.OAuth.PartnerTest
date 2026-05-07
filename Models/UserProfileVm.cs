namespace Micromarin.OAuth.PartnerTest.Models;

public sealed class UserProfileVm
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public string LoginSource { get; set; } = "";
    public string? Email { get; set; }
    public string? AccountId { get; set; }

    public string Initials
    {
        get
        {
            var f = string.IsNullOrWhiteSpace(FirstName) ? "" : FirstName.Trim()[..1];
            var l = string.IsNullOrWhiteSpace(LastName) ? "" : LastName.Trim()[..1];
            var combined = (f + l).ToUpperInvariant();
            return string.IsNullOrEmpty(combined) ? "U" : combined;
        }
    }

    public string FullName => string.Join(" ", new[] { FirstName, LastName }
        .Where(s => !string.IsNullOrWhiteSpace(s)));
}
