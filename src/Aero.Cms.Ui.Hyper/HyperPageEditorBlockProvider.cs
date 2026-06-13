using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Ui.Hyper.Blocks.Announcements;
using Aero.Cms.Ui.Hyper.Blocks.Banners;
using Aero.Cms.Ui.Hyper.Blocks.BlogCards;
using Aero.Cms.Ui.Hyper.Blocks.Cards;
using Aero.Cms.Ui.Hyper.Blocks.Ctas;
using Aero.Cms.Ui.Hyper.Blocks.Faqs;
using Aero.Cms.Ui.Hyper.Blocks.FeatureGrids;
using Aero.Cms.Ui.Hyper.Blocks.Footers;
using Aero.Cms.Ui.Hyper.Blocks.Headers;
using Aero.Cms.Ui.Hyper.Blocks.LogoClouds;
using Aero.Cms.Ui.Hyper.Blocks.Pricing;
using Aero.Cms.Ui.Hyper.Blocks.ProductCards;
using Aero.Cms.Ui.Hyper.Blocks.Sections;
using Aero.Cms.Ui.Hyper.Blocks.Stats;
using Aero.Cms.Ui.Hyper.Blocks.Buttons;
using Aero.Cms.Ui.Hyper.Blocks.Carts;
using Aero.Cms.Ui.Hyper.Blocks.ContactForms;
using Aero.Cms.Ui.Hyper.Blocks.EmptyContent;
using Aero.Cms.Ui.Hyper.Blocks.NewsletterSignup;
using Aero.Cms.Ui.Hyper.Blocks.Polls;
using Aero.Cms.Ui.Hyper.Blocks.ProductCollections;
using Aero.Cms.Ui.Hyper.Blocks.TeamSections;

namespace Aero.Cms.Ui.Hyper;

public sealed class HyperPageEditorBlockProvider : IPageEditorBlockProvider, ICmsBlockModelProvider
{
    private static readonly IReadOnlyCollection<IPageEditorBlockDefinition> Definitions =
    [
        // Phase 1
        new Pricing1EditorBlockDefinition(),
        new FeatureGrids1EditorBlockDefinition(), new FeatureGrids2EditorBlockDefinition(),
        new FeatureGrids3EditorBlockDefinition(), new FeatureGrids4EditorBlockDefinition(),
        new Header1EditorBlockDefinition(), new Header2EditorBlockDefinition(),
        new Header3EditorBlockDefinition(), new Header4EditorBlockDefinition(),
        new Footer1EditorBlockDefinition(), new Footer2EditorBlockDefinition(),
        new Footer3EditorBlockDefinition(), new Footer4EditorBlockDefinition(),
        new Footer5EditorBlockDefinition(), new Footer6EditorBlockDefinition(),
        new Footer7EditorBlockDefinition(), new Footer8EditorBlockDefinition(),
        new Footer9EditorBlockDefinition(), new Footer10EditorBlockDefinition(),
        new Footer11EditorBlockDefinition(), new Footer12EditorBlockDefinition(),
        // Wave 1
        new Banner1EditorBlockDefinition(), new Banner2EditorBlockDefinition(), new Banner3EditorBlockDefinition(),
        new Stats1EditorBlockDefinition(), new Stats2EditorBlockDefinition(), new Stats3EditorBlockDefinition(),
        new Cta1EditorBlockDefinition(), new Cta2EditorBlockDefinition(), new Cta3EditorBlockDefinition(), new Cta4EditorBlockDefinition(),
        new Faq1EditorBlockDefinition(), new Faq2EditorBlockDefinition(), new Faq3EditorBlockDefinition(),
        new LogoClouds1EditorBlockDefinition(), new LogoClouds2EditorBlockDefinition(), new LogoClouds3EditorBlockDefinition(), new LogoClouds4EditorBlockDefinition(),
        new Pricing2EditorBlockDefinition(),
        new Sections1EditorBlockDefinition(), new Sections2EditorBlockDefinition(), new Sections3EditorBlockDefinition(), new Sections4EditorBlockDefinition(),
        new Announcement1EditorBlockDefinition(), new Announcement2EditorBlockDefinition(), new Announcement3EditorBlockDefinition(),
        new Announcement4EditorBlockDefinition(), new Announcement5EditorBlockDefinition(), new Announcement6EditorBlockDefinition(),
        new BlogCard1EditorBlockDefinition(), new BlogCard2EditorBlockDefinition(), new BlogCard3EditorBlockDefinition(),
        new BlogCard4EditorBlockDefinition(), new BlogCard5EditorBlockDefinition(), new BlogCard6EditorBlockDefinition(), new BlogCard7EditorBlockDefinition(),
        new Card1EditorBlockDefinition(), new Card2EditorBlockDefinition(), new Card3EditorBlockDefinition(),
        new Card4EditorBlockDefinition(), new Card5EditorBlockDefinition(), new Card6EditorBlockDefinition(),
        new Card7EditorBlockDefinition(), new Card8EditorBlockDefinition(), new Card9EditorBlockDefinition(),
        new ProductCard1EditorBlockDefinition(), new ProductCard2EditorBlockDefinition(), new ProductCard3EditorBlockDefinition(),
        new ProductCard4EditorBlockDefinition(), new ProductCard5EditorBlockDefinition(), new ProductCard6EditorBlockDefinition(),
        new ProductCard7EditorBlockDefinition(), new ProductCard8EditorBlockDefinition(),
        // Wave 2
        new TeamSection1EditorBlockDefinition(), new TeamSection2EditorBlockDefinition(), new TeamSection3EditorBlockDefinition(),
        new ProductCollection1EditorBlockDefinition(), new ProductCollection2EditorBlockDefinition(),
        new ProductCollection3EditorBlockDefinition(), new ProductCollection4EditorBlockDefinition(),
        new EmptyContent1EditorBlockDefinition(), new EmptyContent2EditorBlockDefinition(), new EmptyContent3EditorBlockDefinition(),
        new EmptyContent4EditorBlockDefinition(), new EmptyContent5EditorBlockDefinition(),
        new Cart1EditorBlockDefinition(), new Cart2EditorBlockDefinition(), new Cart3EditorBlockDefinition(),
        new Poll1EditorBlockDefinition(), new Poll2EditorBlockDefinition(), new Poll3EditorBlockDefinition(),
        new Button1EditorBlockDefinition(), new Button2EditorBlockDefinition(), new Button3EditorBlockDefinition(),
        new Button4EditorBlockDefinition(), new Button5EditorBlockDefinition(), new Button6EditorBlockDefinition(),
        new Button7EditorBlockDefinition(), new Button8EditorBlockDefinition(), new Button9EditorBlockDefinition(),
        new Button10EditorBlockDefinition(), new Button11EditorBlockDefinition(), new Button12EditorBlockDefinition(),
        new NewsletterSignup1EditorBlockDefinition(), new NewsletterSignup2EditorBlockDefinition(),
        new ContactForm1EditorBlockDefinition(), new ContactForm2EditorBlockDefinition(), new ContactForm3EditorBlockDefinition(),
        new ContactForm4EditorBlockDefinition(), new ContactForm5EditorBlockDefinition()
    ];

    private static readonly IReadOnlyCollection<CmsBlockModelRegistration> BlockModels =
    [
        // Phase 1
        new(Pricing1Block.BlockTypeId, typeof(Pricing1Block)),
        new(FeatureGrids1Block.BlockTypeId, typeof(FeatureGrids1Block)), new(FeatureGrids2Block.BlockTypeId, typeof(FeatureGrids2Block)),
        new(FeatureGrids3Block.BlockTypeId, typeof(FeatureGrids3Block)), new(FeatureGrids4Block.BlockTypeId, typeof(FeatureGrids4Block)),
        new(Header1Block.BlockTypeId, typeof(Header1Block)), new(Header2Block.BlockTypeId, typeof(Header2Block)),
        new(Header3Block.BlockTypeId, typeof(Header3Block)), new(Header4Block.BlockTypeId, typeof(Header4Block)),
        new(Footer1Block.BlockTypeId, typeof(Footer1Block)), new(Footer2Block.BlockTypeId, typeof(Footer2Block)),
        new(Footer3Block.BlockTypeId, typeof(Footer3Block)), new(Footer4Block.BlockTypeId, typeof(Footer4Block)),
        new(Footer5Block.BlockTypeId, typeof(Footer5Block)), new(Footer6Block.BlockTypeId, typeof(Footer6Block)),
        new(Footer7Block.BlockTypeId, typeof(Footer7Block)), new(Footer8Block.BlockTypeId, typeof(Footer8Block)),
        new(Footer9Block.BlockTypeId, typeof(Footer9Block)), new(Footer10Block.BlockTypeId, typeof(Footer10Block)),
        new(Footer11Block.BlockTypeId, typeof(Footer11Block)), new(Footer12Block.BlockTypeId, typeof(Footer12Block)),
        // Wave 1
        new(Banner1Block.BlockTypeId, typeof(Banner1Block)), new(Banner2Block.BlockTypeId, typeof(Banner2Block)), new(Banner3Block.BlockTypeId, typeof(Banner3Block)),
        new(Stats1Block.BlockTypeId, typeof(Stats1Block)), new(Stats2Block.BlockTypeId, typeof(Stats2Block)), new(Stats3Block.BlockTypeId, typeof(Stats3Block)),
        new(Cta1Block.BlockTypeId, typeof(Cta1Block)), new(Cta2Block.BlockTypeId, typeof(Cta2Block)), new(Cta3Block.BlockTypeId, typeof(Cta3Block)), new(Cta4Block.BlockTypeId, typeof(Cta4Block)),
        new(Faq1Block.BlockTypeId, typeof(Faq1Block)), new(Faq2Block.BlockTypeId, typeof(Faq2Block)), new(Faq3Block.BlockTypeId, typeof(Faq3Block)),
        new(LogoClouds1Block.BlockTypeId, typeof(LogoClouds1Block)), new(LogoClouds2Block.BlockTypeId, typeof(LogoClouds2Block)),
        new(LogoClouds3Block.BlockTypeId, typeof(LogoClouds3Block)), new(LogoClouds4Block.BlockTypeId, typeof(LogoClouds4Block)),
        new(Pricing2Block.BlockTypeId, typeof(Pricing2Block)),
        new(Sections1Block.BlockTypeId, typeof(Sections1Block)), new(Sections2Block.BlockTypeId, typeof(Sections2Block)),
        new(Sections3Block.BlockTypeId, typeof(Sections3Block)), new(Sections4Block.BlockTypeId, typeof(Sections4Block)),
        new(Announcement1Block.BlockTypeId, typeof(Announcement1Block)), new(Announcement2Block.BlockTypeId, typeof(Announcement2Block)),
        new(Announcement3Block.BlockTypeId, typeof(Announcement3Block)), new(Announcement4Block.BlockTypeId, typeof(Announcement4Block)),
        new(Announcement5Block.BlockTypeId, typeof(Announcement5Block)), new(Announcement6Block.BlockTypeId, typeof(Announcement6Block)),
        new(BlogCard1Block.BlockTypeId, typeof(BlogCard1Block)), new(BlogCard2Block.BlockTypeId, typeof(BlogCard2Block)),
        new(BlogCard3Block.BlockTypeId, typeof(BlogCard3Block)), new(BlogCard4Block.BlockTypeId, typeof(BlogCard4Block)),
        new(BlogCard5Block.BlockTypeId, typeof(BlogCard5Block)), new(BlogCard6Block.BlockTypeId, typeof(BlogCard6Block)), new(BlogCard7Block.BlockTypeId, typeof(BlogCard7Block)),
        new(Card1Block.BlockTypeId, typeof(Card1Block)), new(Card2Block.BlockTypeId, typeof(Card2Block)),
        new(Card3Block.BlockTypeId, typeof(Card3Block)), new(Card4Block.BlockTypeId, typeof(Card4Block)),
        new(Card5Block.BlockTypeId, typeof(Card5Block)), new(Card6Block.BlockTypeId, typeof(Card6Block)),
        new(Card7Block.BlockTypeId, typeof(Card7Block)), new(Card8Block.BlockTypeId, typeof(Card8Block)), new(Card9Block.BlockTypeId, typeof(Card9Block)),
        new(ProductCard1Block.BlockTypeId, typeof(ProductCard1Block)), new(ProductCard2Block.BlockTypeId, typeof(ProductCard2Block)),
        new(ProductCard3Block.BlockTypeId, typeof(ProductCard3Block)), new(ProductCard4Block.BlockTypeId, typeof(ProductCard4Block)),
        new(ProductCard5Block.BlockTypeId, typeof(ProductCard5Block)), new(ProductCard6Block.BlockTypeId, typeof(ProductCard6Block)),
        new(ProductCard7Block.BlockTypeId, typeof(ProductCard7Block)), new(ProductCard8Block.BlockTypeId, typeof(ProductCard8Block)),
        // Wave 2
        new(TeamSection1Block.BlockTypeId, typeof(TeamSection1Block)), new(TeamSection2Block.BlockTypeId, typeof(TeamSection2Block)),
        new(TeamSection3Block.BlockTypeId, typeof(TeamSection3Block)),
        new(ProductCollection1Block.BlockTypeId, typeof(ProductCollection1Block)),
        new(ProductCollection2Block.BlockTypeId, typeof(ProductCollection2Block)),
        new(ProductCollection3Block.BlockTypeId, typeof(ProductCollection3Block)),
        new(ProductCollection4Block.BlockTypeId, typeof(ProductCollection4Block)),
        new(EmptyContent1Block.BlockTypeId, typeof(EmptyContent1Block)), new(EmptyContent2Block.BlockTypeId, typeof(EmptyContent2Block)),
        new(EmptyContent3Block.BlockTypeId, typeof(EmptyContent3Block)), new(EmptyContent4Block.BlockTypeId, typeof(EmptyContent4Block)),
        new(EmptyContent5Block.BlockTypeId, typeof(EmptyContent5Block)),
        new(Cart1Block.BlockTypeId, typeof(Cart1Block)), new(Cart2Block.BlockTypeId, typeof(Cart2Block)), new(Cart3Block.BlockTypeId, typeof(Cart3Block)),
        new(Poll1Block.BlockTypeId, typeof(Poll1Block)), new(Poll2Block.BlockTypeId, typeof(Poll2Block)), new(Poll3Block.BlockTypeId, typeof(Poll3Block)),
        new(Button1Block.BlockTypeId, typeof(Button1Block)), new(Button2Block.BlockTypeId, typeof(Button2Block)),
        new(Button3Block.BlockTypeId, typeof(Button3Block)), new(Button4Block.BlockTypeId, typeof(Button4Block)),
        new(Button5Block.BlockTypeId, typeof(Button5Block)), new(Button6Block.BlockTypeId, typeof(Button6Block)),
        new(Button7Block.BlockTypeId, typeof(Button7Block)), new(Button8Block.BlockTypeId, typeof(Button8Block)),
        new(Button9Block.BlockTypeId, typeof(Button9Block)), new(Button10Block.BlockTypeId, typeof(Button10Block)),
        new(Button11Block.BlockTypeId, typeof(Button11Block)), new(Button12Block.BlockTypeId, typeof(Button12Block)),
        new(NewsletterSignup1Block.BlockTypeId, typeof(NewsletterSignup1Block)),
        new(NewsletterSignup2Block.BlockTypeId, typeof(NewsletterSignup2Block)),
        new(ContactForm1Block.BlockTypeId, typeof(ContactForm1Block)), new(ContactForm2Block.BlockTypeId, typeof(ContactForm2Block)),
        new(ContactForm3Block.BlockTypeId, typeof(ContactForm3Block)), new(ContactForm4Block.BlockTypeId, typeof(ContactForm4Block)),
        new(ContactForm5Block.BlockTypeId, typeof(ContactForm5Block))
    ];

    public IReadOnlyCollection<IPageEditorBlockDefinition> GetDefinitions() => Definitions;
    public IReadOnlyCollection<CmsBlockModelRegistration> GetBlockModels() => BlockModels;
}
