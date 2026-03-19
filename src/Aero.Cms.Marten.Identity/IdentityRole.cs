namespace Aero.Cms.Marten.Identity;

public class IdentityRole
{
    public ulong Id { get; set; }

    public string Name { get; set; }

    public string NormalizedName { get; set; }

    public IList<IdentityClaim> Claims { get; set; }
}
