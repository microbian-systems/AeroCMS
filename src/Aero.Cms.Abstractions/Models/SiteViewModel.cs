using System.Net;

namespace Aero.Cms.Abstractions.Models;

public record SiteViewModel : EntityViewModel
{
    public string? Name { get; set; }
    public IList<SiteHostViewModel> Hosts { get; set; } = [];

    public record SiteHostViewModel : EntityViewModel
    {
        public long SiteId { get; set; }
        public (string name, IPAddress addr) Host { get; set; }
    }
}


