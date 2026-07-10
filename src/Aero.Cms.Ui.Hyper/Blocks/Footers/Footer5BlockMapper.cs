using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Footers;

/// <summary>
/// Represents a class for Footer5BlockMapper.
/// </summary>
public static class Footer5BlockMapper
{
        /// <summary>
    /// ToNode method.
    /// </summary>
public static NeoPageNode ToNode(Footer5Block block) => new()
    {
        NodeId = string.Empty,
        CatalogId = "hyper.footers.5",
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["imageUrl"] = JsonSerializer.SerializeToElement(block.ImageUrl),
            ["callUsText"] = JsonSerializer.SerializeToElement(block.CallUsText),
            ["phoneNumber"] = JsonSerializer.SerializeToElement(block.PhoneNumber),
            ["hours"] = JsonSerializer.SerializeToElement(block.Hours),
            ["socialLinks"] = JsonSerializer.SerializeToElement(block.SocialLinks),
            ["servicesLinks"] = JsonSerializer.SerializeToElement(block.ServicesLinks),
            ["companyLinks"] = JsonSerializer.SerializeToElement(block.CompanyLinks),
            ["bottomLinks"] = JsonSerializer.SerializeToElement(block.BottomLinks),
            ["copyrightText"] = JsonSerializer.SerializeToElement(block.CopyrightText)
        }
    };

        /// <summary>
    /// FromNode method.
    /// </summary>
public static Footer5Block FromNode(NeoPageNode node) => new()
    {
        ImageUrl = GetString(node, "imageUrl", "https://images.unsplash.com/photo-1642370324100-324b21fab3a9?auto=format&fit=crop&q=80&w=1160"),
        CallUsText = GetString(node, "callUsText", "Call us"),
        PhoneNumber = GetString(node, "phoneNumber", "0123456789"),
        Hours = GetList<string>(node, "hours") ?? ["Monday to Friday: 10am - 5pm", "Weekend: 10am - 3pm"],
        SocialLinks = GetList<FooterSocialLink>(node, "socialLinks") ?? FooterDefaults.DefaultSocialLinks.Select(CloneSocialLink).ToList(),
        ServicesLinks = GetList<FooterLink>(node, "servicesLinks") ?? Footer5Block.DefaultServicesLinks.Select(CloneLink).ToList(),
        CompanyLinks = GetList<FooterLink>(node, "companyLinks") ?? Footer5Block.DefaultCompanyLinks.Select(CloneLink).ToList(),
        BottomLinks = GetList<FooterLink>(node, "bottomLinks") ?? Footer5Block.DefaultBottomLinks.Select(CloneLink).ToList(),
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

    private static FooterSocialLink CloneSocialLink(FooterSocialLink link) => new()
    {
        Name = link.Name,
        Url = link.Url
    };
}
