namespace Aero.Cms.Web.Bootstrap;

internal sealed class AeroCmsPipelineState
{
    public bool RoutingApplied { get; set; }

    public bool SiteAndLocalizationApplied { get; set; }

    public bool RequestPipelineApplied { get; set; }

    public bool EndpointsMapped { get; set; }

    public bool TerminalPipelineApplied { get; set; }
}
