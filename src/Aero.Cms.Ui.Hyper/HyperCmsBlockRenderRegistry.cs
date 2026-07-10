using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Shared.Blocks.Rendering;
using Aero.Cms.Ui.Hyper.Blocks.Announcements;
using Aero.Cms.Ui.Hyper.Blocks.Banners;
using Aero.Cms.Ui.Hyper.Blocks.BlogCards;
using Aero.Cms.Ui.Hyper.Blocks.Buttons;
using Aero.Cms.Ui.Hyper.Blocks.Cards;
using Aero.Cms.Ui.Hyper.Blocks.Carts;
using Aero.Cms.Ui.Hyper.Blocks.ContactForms;
using Aero.Cms.Ui.Hyper.Blocks.Ctas;
using Aero.Cms.Ui.Hyper.Blocks.EmptyContent;
using Aero.Cms.Ui.Hyper.Blocks.Faqs;
using Aero.Cms.Ui.Hyper.Blocks.FeatureGrids;
using Aero.Cms.Ui.Hyper.Blocks.Footers;
using Aero.Cms.Ui.Hyper.Blocks.Headers;
using Aero.Cms.Ui.Hyper.Blocks.LogoClouds;
using Aero.Cms.Ui.Hyper.Blocks.NewsletterSignup;
using Aero.Cms.Ui.Hyper.Blocks.Polls;
using Aero.Cms.Ui.Hyper.Blocks.Pricing;
using Aero.Cms.Ui.Hyper.Blocks.ProductCards;
using Aero.Cms.Ui.Hyper.Blocks.ProductCollections;
using Aero.Cms.Ui.Hyper.Blocks.Sections;
using Aero.Cms.Ui.Hyper.Blocks.Stats;
using Aero.Cms.Ui.Hyper.Blocks.TeamSections;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Ui.Hyper;

/// <summary>
/// Explicit DI render registry for HyperUI blocks. This mirrors the source-generator
/// metadata so public page rendering does not depend on generated registry discovery.
/// </summary>
public sealed class HyperCmsBlockRenderRegistry : ICmsBlockRenderRegistry
{
    private static readonly IReadOnlyDictionary<string, ICmsBlockRenderAdapter> Adapters = CreateAdapters();

        /// <summary>
    /// TryGet method.
    /// </summary>
public bool TryGet(string blockType, out ICmsBlockRenderAdapter adapter) =>
        Adapters.TryGetValue(blockType, out adapter!);

    private static IReadOnlyDictionary<string, ICmsBlockRenderAdapter> CreateAdapters()
    {
        var adapters = new Dictionary<string, ICmsBlockRenderAdapter>(StringComparer.OrdinalIgnoreCase);

        Add<Pricing1Block, Pricing1BlockRenderer>(adapters, Pricing1Block.BlockTypeId);
        Add<Pricing2Block, Pricing2BlockRenderer>(adapters, Pricing2Block.BlockTypeId);
        Add<FeatureGrids1Block, FeatureGrids1BlockRenderer>(adapters, FeatureGrids1Block.BlockTypeId);
        Add<FeatureGrids2Block, FeatureGrids2BlockRenderer>(adapters, FeatureGrids2Block.BlockTypeId);
        Add<FeatureGrids3Block, FeatureGrids3BlockRenderer>(adapters, FeatureGrids3Block.BlockTypeId);
        Add<FeatureGrids4Block, FeatureGrids4BlockRenderer>(adapters, FeatureGrids4Block.BlockTypeId);
        Add<Header1Block, Header1BlockRenderer>(adapters, Header1Block.BlockTypeId);
        Add<Header2Block, Header2BlockRenderer>(adapters, Header2Block.BlockTypeId);
        Add<Header3Block, Header3BlockRenderer>(adapters, Header3Block.BlockTypeId);
        Add<Header4Block, Header4BlockRenderer>(adapters, Header4Block.BlockTypeId);
        Add<Footer1Block, Footer1BlockRenderer>(adapters, Footer1Block.BlockTypeId);
        Add<Footer2Block, Footer2BlockRenderer>(adapters, Footer2Block.BlockTypeId);
        Add<Footer3Block, Footer3BlockRenderer>(adapters, Footer3Block.BlockTypeId);
        Add<Footer4Block, Footer4BlockRenderer>(adapters, Footer4Block.BlockTypeId);
        Add<Footer5Block, Footer5BlockRenderer>(adapters, Footer5Block.BlockTypeId);
        Add<Footer6Block, Footer6BlockRenderer>(adapters, Footer6Block.BlockTypeId);
        Add<Footer7Block, Footer7BlockRenderer>(adapters, Footer7Block.BlockTypeId);
        Add<Footer8Block, Footer8BlockRenderer>(adapters, Footer8Block.BlockTypeId);
        Add<Footer9Block, Footer9BlockRenderer>(adapters, Footer9Block.BlockTypeId);
        Add<Footer10Block, Footer10BlockRenderer>(adapters, Footer10Block.BlockTypeId);
        Add<Footer11Block, Footer11BlockRenderer>(adapters, Footer11Block.BlockTypeId);
        Add<Footer12Block, Footer12BlockRenderer>(adapters, Footer12Block.BlockTypeId);
        Add<Banner1Block, Banner1BlockRenderer>(adapters, Banner1Block.BlockTypeId);
        Add<Banner2Block, Banner2BlockRenderer>(adapters, Banner2Block.BlockTypeId);
        Add<Banner3Block, Banner3BlockRenderer>(adapters, Banner3Block.BlockTypeId);
        Add<Stats1Block, Stats1BlockRenderer>(adapters, Stats1Block.BlockTypeId);
        Add<Stats2Block, Stats2BlockRenderer>(adapters, Stats2Block.BlockTypeId);
        Add<Stats3Block, Stats3BlockRenderer>(adapters, Stats3Block.BlockTypeId);
        Add<Cta1Block, Cta1BlockRenderer>(adapters, Cta1Block.BlockTypeId);
        Add<Cta2Block, Cta2BlockRenderer>(adapters, Cta2Block.BlockTypeId);
        Add<Cta3Block, Cta3BlockRenderer>(adapters, Cta3Block.BlockTypeId);
        Add<Cta4Block, Cta4BlockRenderer>(adapters, Cta4Block.BlockTypeId);
        Add<Faq1Block, Faq1BlockRenderer>(adapters, Faq1Block.BlockTypeId);
        Add<Faq2Block, Faq2BlockRenderer>(adapters, Faq2Block.BlockTypeId);
        Add<Faq3Block, Faq3BlockRenderer>(adapters, Faq3Block.BlockTypeId);
        Add<LogoClouds1Block, LogoClouds1BlockRenderer>(adapters, LogoClouds1Block.BlockTypeId);
        Add<LogoClouds2Block, LogoClouds2BlockRenderer>(adapters, LogoClouds2Block.BlockTypeId);
        Add<LogoClouds3Block, LogoClouds3BlockRenderer>(adapters, LogoClouds3Block.BlockTypeId);
        Add<LogoClouds4Block, LogoClouds4BlockRenderer>(adapters, LogoClouds4Block.BlockTypeId);
        Add<Sections1Block, Sections1BlockRenderer>(adapters, Sections1Block.BlockTypeId);
        Add<Sections2Block, Sections2BlockRenderer>(adapters, Sections2Block.BlockTypeId);
        Add<Sections3Block, Sections3BlockRenderer>(adapters, Sections3Block.BlockTypeId);
        Add<Sections4Block, Sections4BlockRenderer>(adapters, Sections4Block.BlockTypeId);
        Add<Announcement1Block, Announcement1BlockRenderer>(adapters, Announcement1Block.BlockTypeId);
        Add<Announcement2Block, Announcement2BlockRenderer>(adapters, Announcement2Block.BlockTypeId);
        Add<Announcement3Block, Announcement3BlockRenderer>(adapters, Announcement3Block.BlockTypeId);
        Add<Announcement4Block, Announcement4BlockRenderer>(adapters, Announcement4Block.BlockTypeId);
        Add<Announcement5Block, Announcement5BlockRenderer>(adapters, Announcement5Block.BlockTypeId);
        Add<Announcement6Block, Announcement6BlockRenderer>(adapters, Announcement6Block.BlockTypeId);
        Add<BlogCard1Block, BlogCard1BlockRenderer>(adapters, BlogCard1Block.BlockTypeId);
        Add<BlogCard2Block, BlogCard2BlockRenderer>(adapters, BlogCard2Block.BlockTypeId);
        Add<BlogCard3Block, BlogCard3BlockRenderer>(adapters, BlogCard3Block.BlockTypeId);
        Add<BlogCard4Block, BlogCard4BlockRenderer>(adapters, BlogCard4Block.BlockTypeId);
        Add<BlogCard5Block, BlogCard5BlockRenderer>(adapters, BlogCard5Block.BlockTypeId);
        Add<BlogCard6Block, BlogCard6BlockRenderer>(adapters, BlogCard6Block.BlockTypeId);
        Add<BlogCard7Block, BlogCard7BlockRenderer>(adapters, BlogCard7Block.BlockTypeId);
        Add<Card1Block, Card1BlockRenderer>(adapters, Card1Block.BlockTypeId);
        Add<Card2Block, Card2BlockRenderer>(adapters, Card2Block.BlockTypeId);
        Add<Card3Block, Card3BlockRenderer>(adapters, Card3Block.BlockTypeId);
        Add<Card4Block, Card4BlockRenderer>(adapters, Card4Block.BlockTypeId);
        Add<Card5Block, Card5BlockRenderer>(adapters, Card5Block.BlockTypeId);
        Add<Card6Block, Card6BlockRenderer>(adapters, Card6Block.BlockTypeId);
        Add<Card7Block, Card7BlockRenderer>(adapters, Card7Block.BlockTypeId);
        Add<Card8Block, Card8BlockRenderer>(adapters, Card8Block.BlockTypeId);
        Add<Card9Block, Card9BlockRenderer>(adapters, Card9Block.BlockTypeId);
        Add<ProductCard1Block, ProductCard1BlockRenderer>(adapters, ProductCard1Block.BlockTypeId);
        Add<ProductCard2Block, ProductCard2BlockRenderer>(adapters, ProductCard2Block.BlockTypeId);
        Add<ProductCard3Block, ProductCard3BlockRenderer>(adapters, ProductCard3Block.BlockTypeId);
        Add<ProductCard4Block, ProductCard4BlockRenderer>(adapters, ProductCard4Block.BlockTypeId);
        Add<ProductCard5Block, ProductCard5BlockRenderer>(adapters, ProductCard5Block.BlockTypeId);
        Add<ProductCard6Block, ProductCard6BlockRenderer>(adapters, ProductCard6Block.BlockTypeId);
        Add<ProductCard7Block, ProductCard7BlockRenderer>(adapters, ProductCard7Block.BlockTypeId);
        Add<ProductCard8Block, ProductCard8BlockRenderer>(adapters, ProductCard8Block.BlockTypeId);
        Add<NewsletterSignup1Block, NewsletterSignup1BlockRenderer>(adapters, NewsletterSignup1Block.BlockTypeId);
        Add<NewsletterSignup2Block, NewsletterSignup2BlockRenderer>(adapters, NewsletterSignup2Block.BlockTypeId);
        Add<ContactForm1Block, ContactForm1BlockRenderer>(adapters, ContactForm1Block.BlockTypeId);
        Add<ContactForm2Block, ContactForm2BlockRenderer>(adapters, ContactForm2Block.BlockTypeId);
        Add<ContactForm3Block, ContactForm3BlockRenderer>(adapters, ContactForm3Block.BlockTypeId);
        Add<ContactForm4Block, ContactForm4BlockRenderer>(adapters, ContactForm4Block.BlockTypeId);
        Add<ContactForm5Block, ContactForm5BlockRenderer>(adapters, ContactForm5Block.BlockTypeId);
        Add<TeamSection1Block, TeamSection1BlockRenderer>(adapters, TeamSection1Block.BlockTypeId);
        Add<TeamSection2Block, TeamSection2BlockRenderer>(adapters, TeamSection2Block.BlockTypeId);
        Add<TeamSection3Block, TeamSection3BlockRenderer>(adapters, TeamSection3Block.BlockTypeId);
        Add<ProductCollection1Block, ProductCollection1BlockRenderer>(adapters, ProductCollection1Block.BlockTypeId);
        Add<ProductCollection2Block, ProductCollection2BlockRenderer>(adapters, ProductCollection2Block.BlockTypeId);
        Add<ProductCollection3Block, ProductCollection3BlockRenderer>(adapters, ProductCollection3Block.BlockTypeId);
        Add<ProductCollection4Block, ProductCollection4BlockRenderer>(adapters, ProductCollection4Block.BlockTypeId);
        Add<EmptyContent1Block, EmptyContent1BlockRenderer>(adapters, EmptyContent1Block.BlockTypeId);
        Add<EmptyContent2Block, EmptyContent2BlockRenderer>(adapters, EmptyContent2Block.BlockTypeId);
        Add<EmptyContent3Block, EmptyContent3BlockRenderer>(adapters, EmptyContent3Block.BlockTypeId);
        Add<EmptyContent4Block, EmptyContent4BlockRenderer>(adapters, EmptyContent4Block.BlockTypeId);
        Add<EmptyContent5Block, EmptyContent5BlockRenderer>(adapters, EmptyContent5Block.BlockTypeId);
        Add<Cart1Block, Cart1BlockRenderer>(adapters, Cart1Block.BlockTypeId);
        Add<Cart2Block, Cart2BlockRenderer>(adapters, Cart2Block.BlockTypeId);
        Add<Cart3Block, Cart3BlockRenderer>(adapters, Cart3Block.BlockTypeId);
        Add<Poll1Block, Poll1BlockRenderer>(adapters, Poll1Block.BlockTypeId);
        Add<Poll2Block, Poll2BlockRenderer>(adapters, Poll2Block.BlockTypeId);
        Add<Poll3Block, Poll3BlockRenderer>(adapters, Poll3Block.BlockTypeId);
        Add<Button1Block, Button1BlockRenderer>(adapters, Button1Block.BlockTypeId);
        Add<Button2Block, Button2BlockRenderer>(adapters, Button2Block.BlockTypeId);
        Add<Button3Block, Button3BlockRenderer>(adapters, Button3Block.BlockTypeId);
        Add<Button4Block, Button4BlockRenderer>(adapters, Button4Block.BlockTypeId);
        Add<Button5Block, Button5BlockRenderer>(adapters, Button5Block.BlockTypeId);
        Add<Button6Block, Button6BlockRenderer>(adapters, Button6Block.BlockTypeId);
        Add<Button7Block, Button7BlockRenderer>(adapters, Button7Block.BlockTypeId);
        Add<Button8Block, Button8BlockRenderer>(adapters, Button8Block.BlockTypeId);
        Add<Button9Block, Button9BlockRenderer>(adapters, Button9Block.BlockTypeId);
        Add<Button10Block, Button10BlockRenderer>(adapters, Button10Block.BlockTypeId);
        Add<Button11Block, Button11BlockRenderer>(adapters, Button11Block.BlockTypeId);
        Add<Button12Block, Button12BlockRenderer>(adapters, Button12Block.BlockTypeId);

        return adapters;
    }

    private static void Add<TBlock, TComponent>(
        Dictionary<string, ICmsBlockRenderAdapter> adapters,
        string blockType)
        where TBlock : BlockBase
        where TComponent : IComponent
    {
        adapters[blockType] = new ComponentCmsBlockRenderAdapter<TBlock, TComponent>(blockType);
    }
}
