using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Footers;

/// <summary>
/// Represents a class for Footer6BlockMapper.
/// </summary>
public static class Footer6BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(Footer6Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.footers.6",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["ctaTitle"] = JsonSerializer.SerializeToElement(block.CtaTitle),
            ["ctaDescription"] = JsonSerializer.SerializeToElement(block.CtaDescription),
            ["emailPlaceholder"] = JsonSerializer.SerializeToElement(block.EmailPlaceholder),
            ["buttonText"] = JsonSerializer.SerializeToElement(block.ButtonText),
            ["servicesLinks"] = JsonSerializer.SerializeToElement(block.ServicesLinks),
            ["companyLinks"] = JsonSerializer.SerializeToElement(block.CompanyLinks),
            ["helpfulLinks"] = JsonSerializer.SerializeToElement(block.HelpfulLinks),
            ["bottomLinks"] = JsonSerializer.SerializeToElement(block.BottomLinks),
            ["copyrightText"] = JsonSerializer.SerializeToElement(block.CopyrightText)
        }
    };

        /// <summary>
    /// FromNode method.
    /// </summary>
public static Footer6Block FromNode(NeoPageNode node) => new()
    {
        CtaTitle = GetString(node, "ctaTitle", "Request a Demo"),
        CtaDescription = GetString(node, "ctaDescription", "Lorem ipsum dolor sit amet consectetur adipisicing elit. Veritatis, harum deserunt nesciunt praesentium, repellendus eum perspiciatis ratione pariatur a aperiam eius numquam doloribus asperiores sunt."),
        EmailPlaceholder = GetString(node, "emailPlaceholder", "john@rhcp.com"),
        ButtonText = GetString(node, "buttonText", "Sign Up"),
        ServicesLinks = GetList<FooterLink>(node, "servicesLinks") ?? Footer6Block.DefaultServicesLinks.Select(CloneLink).ToList(),
        CompanyLinks = GetList<FooterLink>(node, "companyLinks") ?? Footer6Block.DefaultCompanyLinks.Select(CloneLink).ToList(),
        HelpfulLinks = GetList<FooterLink>(node, "helpfulLinks") ?? Footer6Block.DefaultHelpfulLinks.Select(CloneLink).ToList(),
        BottomLinks = GetList<FooterLink>(node, "bottomLinks") ?? Footer6Block.DefaultBottomLinks.Select(CloneLink).ToList(),
        CopyrightText = GetString(node, "copyrightText", "&copy; 2022. Company Name. All rights reserved.")
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;

    private static List<T>? GetList<T>(NeoPageNode node, string key) =>
        node.Properties.TryGetValue(key, out var element) && element.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<T>>(element.GetRawText())
            : null;

    private static FooterLink CloneLink(FooterLink link) => new()
    {
        Text = link.Text,
        Url = link.Url
    };
}
