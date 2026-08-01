# Aero Blocks Port — Old Blocks from feature/subpages

## Source Branch
`feature/subpages`

## Public Renderer Files (have [CmsBlockRenderer] in RendererMarkers.cs)

These .razor files exist in `src/Aero.Cms.Shared/Blocks/Rendering/`:

- `CarouselRenderer.razor`
- `ColumnsRenderer.razor`
- `ImageBlockRenderer.razor`
- `BoringHeroRenderer.razor`
- `EmbedBlockRenderer.razor`
- `HeroRenderer.razor`
- `RawHtmlRenderer.razor` + `RawHtmlRenderer.razor.cs`

## Editor RenderFragments (inside PageEditor.razor)

All block editor UI lives inline in `src/Aero.Cms.Shared/Pages/Manager/PageEditor/PageEditor.razor`.

### Block dispatch (RenderBlock switch)
```csharp
RenderFragment RenderBlock(EditorBlock block, bool isSelected) => block.Type switch
{
    "boring_hero" => RenderBoringHeroBlock(block, isSelected),
    "hero" => RenderHeroBlock(block, isSelected),
    "aero_hero"         => RenderAeroHeroBlock(block, isSelected),
    "aero_features"     => RenderAeroFeaturesBlock(block, isSelected),
    "aero_cta"          => RenderAeroCtaBlock(block, isSelected),
    "aero_blog"         => RenderAeroBlogBlock(block, isSelected),
    "aero_pricing"      => RenderAeroPricingBlock(block, isSelected),
    "aero_teams"        => RenderAeroTeamsBlock(block, isSelected),
    "aero_testimonials" => RenderAeroTestimonialsBlock(block, isSelected),
    "aero_faq"          => RenderAeroFaqBlock(block, isSelected),
    "aero_portfolio"    => RenderAeroPortfolioBlock(block, isSelected),
    "aero_contact"      => RenderAeroContactBlock(block, isSelected),
    "aero_table"        => RenderAeroTableBlock(block, isSelected),
    "aero_auth"         => RenderAeroAuthBlock(block, isSelected),
    "text" => RenderTextBlock(block),
    "content" => RenderContentBlock(block),
    "markdown" => RenderMarkdownBlock(block),
    "quote" => RenderQuoteBlock(block),
    "separator" => RenderSeparatorBlock(),
    "columns" => RenderColumnsBlock(block, isSelected),
    "image" => RenderImageBlock(block),
    "video" => RenderVideoBlock(block),
    "gallery" => RenderGalleryBlock(block, isSelected),
    "carousel" => RenderCarouselBlock(block, isSelected),
    "audio" => RenderAudioBlock(block),
    "raw_html" => RenderHtmlBlock(block),
    "dynamic_template" => RenderDynamicTemplateBlock(block),
    _ => RenderReferenceBlock(block),
};
```

### Sidebar Block Items

**UI category:**
- Boring Hero (`"boring_hero"`)
- Hero (`"hero"`)
- Text (`"text"`)
- Columns (`"columns"`)
- Auth (`"aero_auth"`)
- Rich Text (`"content"`)
- Markdown (`"markdown"`)
- Raw HTML (`"raw_html"`)
- Scriban (`"dynamic_template"`)
- Quote (`"quote"`)
- Separator (`"separator"`)

**Aero UX category:**
- Hero (`"aero_hero"`)
- Features (`"aero_features"`)
- CTA (`"aero_cta"`)
- Blog (`"aero_blog"`)
- Pricing (`"aero_pricing"`)
- Teams (`"aero_teams"`)
- Testimonials (`"aero_testimonials"`)
- FAQ (`"aero_faq"`)
- Portfolio (`"aero_portfolio"`)
- Contact (`"aero_contact"`)
- Table (`"aero_table"`)
- Auth (`"aero_auth"`)

**Media category:**
- Image (`"image"`)
- Video (`"video"`)
- Gallery (`"gallery"`)
- Carousel (`"carousel"`)
- Audio (`"audio"`)

**References category:**
- Pages (`"pages"`)
- Posts (`"posts"`)
- Categories (`"categories"`)
- Tags (`"tags"`)
- Authors (`"authors"`)

### Individual Editor RenderFragment Methods

#### Boring Hero
```csharp
RenderFragment RenderBoringHeroBlock(EditorBlock b, bool sel) => __builder =>
{
    var model = new BoringHeroBlock
    {
        FullWidth = false,
        Title = b.MainText,
        Summary = b.SubText,
        BackgroundImageUrl = b.BackgroundImage
    };

    <div class="pe-hero-block-wrapper">
        <BoringHeroRenderer Block="model" />
        @if (sel && !PreviewMode)
        {
            <div class="pe-hero-bg-controls">
                <input class="pe-hero-title" type="text" placeholder="Page title"
                       value="@b.MainText" @oninput="e => b.MainText = e.Value?.ToString() ?? string.Empty"
                       @onfocus="() => SelectBlock(b.EditorId)" />
                <input class="pe-hero-subtitle" type="text" placeholder="Summary"
                       value="@b.SubText" @oninput="e => b.SubText = e.Value?.ToString() ?? string.Empty"
                       @onfocus="() => SelectBlock(b.EditorId)" />
                @if (string.IsNullOrEmpty(b.BackgroundImage))
                {
                    <button class="pe-btn pe-btn-secondary pe-btn-sm"
                            @onclick='() => OpenMediaSelector(b, false, "background")'>
                        Add Background Image
                    </button>
                }
                else
                {
                    <button class="pe-btn pe-btn-ghost pe-btn-sm"
                            @onclick="() => b.BackgroundImage = string.Empty">
                        Remove Background
                    </button>
                }
            </div>
        }
    </div>
};
```

#### Hero
```csharp
RenderFragment RenderHeroBlock(EditorBlock b, bool sel) => __builder =>
{
    var heroHeightStyle = b.FullScreen
        ? "min-height: 100vh;"
        : $"min-height: {Math.Max(240, b.Height)}px;";

    <div class="pe-hero-block"
         style="@($"{heroHeightStyle}{(string.IsNullOrEmpty(b.BackgroundImage) ? "" : $"background-image:url({b.BackgroundImage})")}")">
        @if (!string.IsNullOrEmpty(b.BackgroundImage))
        {
            <div class="pe-hero-overlay"></div>
        }
        <div class="pe-hero-content">
            <input class="pe-hero-title" type="text" placeholder="Main Headline"
                   value="@b.MainText" @oninput="e => b.MainText = e.Value?.ToString() ?? string.Empty"
                   @onfocus="() => SelectBlock(b.EditorId)"/>
            <input class="pe-hero-subtitle" type="text" placeholder="Sub headline or description"
                   value="@b.SubText" @oninput="e => b.SubText = e.Value?.ToString() ?? string.Empty"
                   @onfocus="() => SelectBlock(b.EditorId)"/>
            <div class="pe-hero-cta-row">
                <input class="pe-hero-cta-text" type="text" placeholder="Button text"
                       value="@b.CtaText" @oninput="e => b.CtaText = e.Value?.ToString() ?? string.Empty"
                       @onfocus="() => SelectBlock(b.EditorId)"/>
                <input class="pe-hero-cta-url" type="text" placeholder="Button URL"
                       value="@b.CtaUrl" @oninput="e => b.CtaUrl = e.Value?.ToString() ?? string.Empty"
                       @onfocus="() => SelectBlock(b.EditorId)"/>
            </div>
            <div class="pe-hero-bg-controls">
                <label class="pe-field-label">
                    <input type="checkbox" @bind="b.FullScreen" />
                    Full screen
                </label>
                @if (!b.FullScreen)
                {
                    <input class="pe-hero-cta-url" type="number" min="240" max="1600" step="20"
                           value="@b.Height"
                           @oninput="e => b.Height = int.TryParse(e.Value?.ToString(), out var height) ? height : 512"
                           @onfocus="() => SelectBlock(b.EditorId)" />
                }
                @if (string.IsNullOrEmpty(b.BackgroundImage))
                {
                    <button class="pe-btn pe-btn-secondary pe-btn-sm"
                            @onclick='() => OpenMediaSelector(b, false, "background")'>
                        Add Background Image
                    </button>
                }
                else
                {
                    <button class="pe-btn pe-btn-ghost pe-btn-sm"
                            @onclick="() => b.BackgroundImage = string.Empty">
                        Remove Background
                    </button>
                }
            </div>
        </div>
    </div>
};
```

#### Separator
```csharp
RenderFragment RenderSeparatorBlock() => __builder => { <hr class="pe-separator-block"/> };
```

#### Image
```csharp
RenderFragment RenderImageBlock(EditorBlock b) => __builder =>
{
    <div class="pe-image-block">
        @if (string.IsNullOrEmpty(b.Src))
        {
            <button class="pe-add-media-btn" @onclick="() => OpenMediaSelector(b)">
                <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <rect x="3" y="3" width="18" height="18" rx="2" ry="2"/>
                    <circle cx="8.5" cy="8.5" r="1.5"/>
                    <polyline points="21 15 16 10 5 21"/>
                </svg>
                <span>Add Image</span>
            </button>
        }
        else
        {
            <div class="pe-image-wrapper">
                <img src="@b.Src" alt="@b.Alt"/>
                <div class="pe-image-overlay">
                    <button class="pe-overlay-btn" @onclick="() => OpenMediaSelector(b)">Change</button>
                    <button class="pe-overlay-btn delete" @onclick="() => RemoveImage(b)">Remove</button>
                </div>
                <input class="pe-image-caption" type="text" placeholder="Add caption..."
                       @bind="b.Caption"/>
            </div>
        }
    </div>
};
```

#### Video
```csharp
RenderFragment RenderVideoBlock(EditorBlock b) => __builder =>
{
    <div class="pe-video-block">
        @if (string.IsNullOrEmpty(b.Src))
        {
            <div class="pe-video-input">
                <div class="pe-input-with-btn">
                    <input type="text" placeholder="Enter video URL (YouTube, Vimeo, or direct link)"
                           @bind="b.Url"/>
                    <button class="pe-btn pe-btn-sm" title="Browse media"
                            @onclick='() => OpenMediaSelector(b, false, "video")'>
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <rect x="3" y="3" width="18" height="18" rx="2" ry="2"/>
                            <circle cx="8.5" cy="8.5" r="1.5"/>
                            <polyline points="21 15 16 10 5 21"/>
                        </svg>
                    </button>
                </div>
                <button class="pe-btn pe-btn-primary" @onclick="() => LoadVideo(b)">Add Video</button>
            </div>
        }
        else
        {
            var videoSrc = b.Src;
            if (b.AutoPlay)
            {
                var sep = videoSrc.Contains('?') ? '&' : '?';
                videoSrc += $"{sep}autoplay=1";
            }

            <div class="pe-video-wrapper">
                @if (b.Src.Contains("youtube.com/embed/") || b.Src.Contains("player.vimeo.com"))
                {
                    <iframe src="@videoSrc" frameborder="0" allowfullscreen></iframe>
                }
                else
                {
                    <video controls src="@b.Src" autoplay="@(b.AutoPlay ? true : null)" muted="@(b.AutoPlay ? true : null)" style="max-width:100%;border-radius:8px;"></video>
                }
                <button class="pe-remove-video" @onclick="() => RemoveVideo(b)">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <line x1="18" y1="6" x2="6" y2="18"/>
                        <line x1="6" y1="6" x2="18" y2="18"/>
                    </svg>
                </button>
                <label class="pe-auto-play-bar">
                    <input type="checkbox" @bind="b.AutoPlay" />
                    Auto-play
                </label>
            </div>
        }
    </div>
};
```

#### Gallery
```csharp
RenderFragment RenderGalleryBlock(EditorBlock b, bool sel) => __builder =>
{
    <div class="pe-gallery-block" @onclick:stopPropagation>
        <div class="pe-gallery-grid">
            @for (var gi = 0; gi < b.GalleryImages.Count; gi++)
            {
                var img = b.GalleryImages[gi];
                var imgIndex = gi;
                <div class="pe-gallery-item">
                    <img src="@img.Src" alt="@img.Alt"/>
                    <button class="pe-remove-item" @onclick="() => b.GalleryImages.RemoveAt(imgIndex)">
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <line x1="18" y1="6" x2="6" y2="18"/>
                            <line x1="6" y1="6" x2="18" y2="18"/>
                        </svg>
                    </button>
                </div>
            }
            <button class="pe-add-gallery-item" @onclick="() => OpenMediaSelector(b, true)">
                <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <line x1="12" y1="5" x2="12" y2="19"/>
                    <line x1="5" y1="12" x2="19" y2="12"/>
                </svg>
            </button>
        </div>

        @* Carousel/Gallery controls *@
        @if (sel && !PreviewMode)
        {
            <div class="pe-carousel-controls" style="margin-top: 12px; display: flex; flex-wrap: wrap; gap: 12px; align-items: center;">
                <label class="pe-field-label" style="display: flex; align-items: center; gap: 6px;">
                    <input type="checkbox" @bind="b.AutoPlay" />
                    Autoscroll
                </label>
                @if (b.AutoPlay)
                {
                    <label style="display: flex; align-items: center; gap: 6px; font-size: 13px; color: var(--pe-text-secondary);">
                        Interval (ms):
                        <input type="number" min="1000" max="30000" step="500"
                               value="@b.CarouselInterval"
                               @oninput="e => b.CarouselInterval = int.TryParse(e.Value?.ToString(), out var v) ? Math.Clamp(v, 1000, 30000) : 5000"
                               style="width: 80px; background: var(--pe-bg-primary); color: var(--pe-text-primary); border: 1px solid var(--pe-border); border-radius: 4px; padding: 4px 8px;" />
                    </label>
                }
                <label class="pe-field-label" style="display: flex; align-items: center; gap: 6px;">
                    <input type="checkbox" @bind="b.ShowArrows" />
                    Show Arrows
                </label>
                <label style="display: flex; align-items: center; gap: 6px; font-size: 13px; color: var(--pe-text-secondary);">
                    Dots:
                    <select @bind="b.ControlLocation"
                            style="background: var(--pe-bg-primary); color: var(--pe-text-primary); border: 1px solid var(--pe-border); border-radius: 4px; padding: 4px 8px;">
                        <option value="bottom">Below image</option>
                        <option value="overlay">Over image</option>
                        <option value="hidden">Hidden</option>
                    </select>
                </label>
            </div>
        }
    </div>
};
```

#### Carousel (identical to Gallery structurally, separate block type entry)
```csharp
RenderFragment RenderCarouselBlock(EditorBlock b, bool sel) => __builder =>
{
    bool showBottomControls = b.ControlLocation != "hidden";
    <div class="pe-gallery-block" @onclick:stopPropagation>
        @* Image grid *@
        @if (showBottomControls || b.ControlLocation == "overlay")
        {
            <div class="pe-gallery-grid">
                @for (var gi = 0; gi < b.GalleryImages.Count; gi++)
                {
                    var img = b.GalleryImages[gi];
                    var imgIndex = gi;
                    <div class="pe-gallery-item">
                        <img src="@img.Src" alt="@img.Alt"/>
                        <button class="pe-remove-item" @onclick="() => b.GalleryImages.RemoveAt(imgIndex)">
                            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                <line x1="18" y1="6" x2="6" y2="18"/>
                                <line x1="6" y1="6" x2="18" y2="18"/>
                            </svg>
                        </button>
                    </div>
                }
                <button class="pe-add-gallery-item" @onclick="() => OpenMediaSelector(b, true)">
                    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <line x1="12" y1="5" x2="12" y2="19"/>
                        <line x1="5" y1="12" x2="19" y2="12"/>
                    </svg>
                </button>
            </div>
        }

        @* Carousel-specific controls *@
        @if (sel && !PreviewMode)
        {
            <div class="pe-carousel-controls" style="margin-top: 12px; display: flex; flex-wrap: wrap; gap: 12px; align-items: center;">
                <label class="pe-field-label" style="display: flex; align-items: center; gap: 6px;">
                    <input type="checkbox" @bind="b.AutoPlay" />
                    Autoscroll
                </label>
                @if (b.AutoPlay)
                {
                    <label style="display: flex; align-items: center; gap: 6px; font-size: 13px; color: var(--pe-text-secondary);">
                        Interval (ms):
                        <input type="number" min="1000" max="30000" step="500"
                               value="@b.CarouselInterval"
                               @oninput="e => b.CarouselInterval = int.TryParse(e.Value?.ToString(), out var v) ? Math.Clamp(v, 1000, 30000) : 5000"
                               style="width: 80px; background: var(--pe-bg-primary); color: var(--pe-text-primary); border: 1px solid var(--pe-border); border-radius: 4px; padding: 4px 8px;" />
                    </label>
                }
                <label class="pe-field-label" style="display: flex; align-items: center; gap: 6px;">
                    <input type="checkbox" @bind="b.ShowArrows" />
                    Show Arrows
                </label>
                <label style="display: flex; align-items: center; gap: 6px; font-size: 13px; color: var(--pe-text-secondary);">
                    Dots:
                    <select @bind="b.ControlLocation"
                            style="background: var(--pe-bg-primary); color: var(--pe-text-primary); border: 1px solid var(--pe-border); border-radius: 4px; padding: 4px 8px;">
                        <option value="bottom">Below image</option>
                        <option value="overlay">Over image</option>
                        <option value="hidden">Hidden</option>
                    </select>
                </label>
            </div>
        }
    </div>
};
```

#### Audio
```csharp
RenderFragment RenderAudioBlock(EditorBlock b) => __builder =>
{
    <div class="pe-audio-block">
        @if (string.IsNullOrEmpty(b.Src))
        {
            <button class="pe-add-media-btn" @onclick="() => OpenAudioSelector(b)">
                <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <path d="M9 18V5l12-2v13"/>
                    <circle cx="6" cy="18" r="3"/>
                    <circle cx="18" cy="16" r="3"/>
                </svg>
                <span>Add Audio</span>
            </button>
        }
        else
        {
            <div class="pe-audio-wrapper">
                <audio controls src="@b.Src" style="flex:1"></audio>
                <button class="pe-remove-audio" @onclick="() => b.Src = string.Empty">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <line x1="18" y1="6" x2="6" y2="18"/>
                        <line x1="6" y1="6" x2="18" y2="18"/>
                    </svg>
                </button>
            </div>
        }
    </div>
};
```

#### Columns
```csharp
RenderFragment RenderColumnsBlock(EditorBlock b, bool sel) => __builder =>
{
    <div class="pe-columns-block-wrapper">
        @if (sel && !PreviewMode)
        {
            <div class="pe-columns-controls">
                <div class="pe-col-ctrl">
                    <label>Columns: @b.ColumnCount</label>
                    <input type="range" min="1" max="12"
                           value="@b.ColumnCount"
                           @oninput='e => UpdateColumnCount(b, int.Parse(e.Value?.ToString() ?? "2"))'/>
                </div>
                <div class="pe-col-ctrl">
                    <label>Gap: @(b.Gap)px</label>
                    <input type="range" min="0" max="64"
                           value="@b.Gap"
                           @oninput='e => b.Gap = int.Parse(e.Value?.ToString() ?? "16")'/>
                </div>
            </div>
        }
        <div class="pe-columns-block" style="gap:@(b.Gap)px">
            @for (var ci = 0; ci < b.EditorColumns.Count; ci++)
            {
                var col = b.EditorColumns[ci];
                var colIndex = ci;
                <div class="pe-column"
                     style="flex: 0 0 calc(@(100.0 / b.ColumnCount)% - @(b.Gap * (b.ColumnCount - 1.0) / b.ColumnCount)px)"
                     @ondragover:preventDefault
                     @ondrop="e => DropOnColumn(e, b, colIndex)">
                    @if (sel && !PreviewMode)
                    {
                        <div class="pe-column-header">
                            <span class="pe-column-label">Col @(colIndex + 1)</span>
                            <button class="pe-column-add-btn" title="Add text"
                                    @onclick='() => AddBlockToColumn(b, colIndex, "text")'>
                                <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                    <line x1="12" y1="5" x2="12" y2="19"/>
                                    <line x1="5" y1="12" x2="19" y2="12"/>
                                </svg>
                            </button>
                        </div>
                    }
                    <div class="pe-column-content">
                        @for (var ni = 0; ni < col.Blocks.Count; ni++)
                        {
                            var nb = col.Blocks[ni];
                            var nestedIndex = ni;
                            <div class="pe-nested-block">
                                @RenderNestedBlock(nb, b, colIndex)
                                @if (sel && !PreviewMode)
                                {
                                    <button class="pe-remove-nested-btn"
                                            @onclick="() => RemoveNestedBlock(b, colIndex, nestedIndex)">
                                        <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                            <line x1="18" y1="6" x2="6" y2="18"/>
                                            <line x1="6" y1="6" x2="18" y2="18"/>
                                        </svg>
                                    </button>
                                }
                            </div>
                        }
                        @if (col.Blocks.Count == 0 && sel && !PreviewMode)
                        {
                            <div class="pe-column-drop-hint">
                                <span>Drag blocks here or click + to add</span>
                            </div>
                        }
                    </div>
                </div>
            }
        </div>
    </div>
};

RenderFragment RenderNestedBlock(NestedBlock nb, EditorBlock parent, int colIndex) => __builder =>
{
    switch (nb.Type)
    {
        case "text":
            <textarea @bind="nb.Content" placeholder="Text..." rows="3"></textarea>
            break;
        case "image":
            <div class="pe-nested-image">
                @if (string.IsNullOrEmpty(nb.Src))
                {
                    <button class="pe-add-nested-btn"
                            @onclick="() => OpenMediaSelectorForNested(parent, colIndex, nb)">
                        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <rect x="3" y="3" width="18" height="18" rx="2"/>
                            <circle cx="8.5" cy="8.5" r="1.5"/>
                            <polyline points="21 15 16 10 5 21"/>
                        </svg>
                    </button>
                }
                else
                {
                    <img src="@nb.Src" alt="@nb.Alt"/>
                }
            </div>
            break;
        case "video":
            <div class="pe-nested-video">
                @if (string.IsNullOrEmpty(nb.Src))
                {
                    <input type="text" placeholder="Video URL" @bind="nb.Url"
                           @onkeydown='e => { if (e.Key == "Enter") LoadNestedVideo(nb); }'/>
                }
                else
                {
                    <iframe src="@nb.Src" frameborder="0" allowfullscreen></iframe>
                }
            </div>
            break;
        case "button":
            <div class="pe-nested-button-editor">
                <input type="text" placeholder="Button text" @bind="nb.Text"/>
                <input type="text" placeholder="URL" @bind="nb.Url"/>
                <select @bind="nb.Style">
                    <option value="primary">Primary</option>
                    <option value="secondary">Secondary</option>
                    <option value="outline">Outline</option>
                </select>
            </div>
            break;
    }
};
```

#### Text
```csharp
RenderFragment RenderTextBlock(EditorBlock b) => __builder =>
{
    <div class="pe-text-block">
        <textarea placeholder="Enter your text here..."
                  @bind="b.Content"
                  @onfocus="() => SelectBlock(b.EditorId)"></textarea>
    </div>
};
```

#### Rich Text
```csharp
RenderFragment RenderContentBlock(EditorBlock b) => __builder =>
{
    <div class="pe-content-block">
        <RadzenHtmlEditor @bind-Value="b.Content" Style="height:300px"/>
    </div>
};
```

#### Markdown
```csharp
RenderFragment RenderMarkdownBlock(EditorBlock b) => __builder =>
{
    <div class="pe-markdown-block">
        <div class="pe-markdown-tabs">
            <button class='pe-tab-btn @(b.MarkdownView == "edit" ? "active" : "")'
                    @onclick='() => b.MarkdownView = "edit"'>
                Edit
            </button>
            <button class='pe-tab-btn @(b.MarkdownView == "preview" ? "active" : "")'
                    @onclick='() => b.MarkdownView = "preview"'>
                Preview
            </button>
        </div>
        @if (b.MarkdownView == "edit")
        {
            <textarea placeholder="# Markdown content..."
                      @bind="b.Content"
                      @onfocus="() => SelectBlock(b.EditorId)"></textarea>
        }
        else
        {
            <div class="pe-markdown-preview">
                <RadzenMarkdown Text="@b.Content" AllowHtml="false" />
            </div>
        }
    </div>
};
```

#### HTML / Raw HTML
```csharp
RenderFragment RenderHtmlBlock(EditorBlock b) => __builder =>
{
    <div class="pe-markdown-block">
        <div class="pe-markdown-tabs">
            <button class='pe-tab-btn @(b.MarkdownView == "edit" ? "active" : "")'
                    @onclick='() => b.MarkdownView = "edit"'>
                Edit
            </button>
            <button class='pe-tab-btn @(b.MarkdownView == "preview" ? "active" : "")'
                    @onclick='() => b.MarkdownView = "preview"'>
                Preview
            </button>
        </div>
        @if (b.MarkdownView == "edit")
        {
            <RadzenHtmlEditor @bind-Value="b.Content"
                              UploadUrl="/api/v1/admin/media/html-editor-image"
                              Paste="SanitizeHtmlPaste"
                              Style="height:300px"
                              @onfocus="() => SelectBlock(b.EditorId)" />
        }
        else
        {
            <div class="pe-markdown-preview">
                <RawHtmlRenderer Block="@(new RawHtmlBlock { Content = b.Content })" />
            </div>
        }
    </div>
};
```

#### Quote
```csharp
RenderFragment RenderQuoteBlock(EditorBlock b) => __builder =>
{
    <div class="pe-quote-block">
        <blockquote>
            <textarea placeholder="Enter quote text..."
                      @bind="b.Content"
                      @onfocus="() => SelectBlock(b.EditorId)"></textarea>
        </blockquote>
        <input class="pe-quote-author" type="text" placeholder="Author name"
               @bind="b.Author"
               @onfocus="() => SelectBlock(b.EditorId)"/>
    </div>
};
```

#### Scriban Dynamic Template
```csharp
RenderFragment RenderDynamicTemplateBlock(EditorBlock b) => __builder =>
{
    <div class="pe-markdown-block pe-dynamic-template-block">
        <div class="pe-markdown-tabs">
            <button class='pe-tab-btn @(b.ScribanView == "code" ? "active" : "")'
                    @onclick='() => b.ScribanView = "code"'>
                Code
            </button>
            <button class='pe-tab-btn @(b.ScribanView == "preview" ? "active" : "")'
                    @onclick='async () =>
                    {
                        b.ScribanView = "preview";
                        await RefreshDynamicTemplatePreviewAsync(b);
                    }'>
                Preview
            </button>
        </div>

        @if (b.ScribanView == "preview")
        {
            <div class="pe-dynamic-template-actions">
                <button class="pe-btn pe-btn-secondary pe-btn-sm"
                        @onclick="() => RefreshDynamicTemplatePreviewAsync(b)">
                    Refresh Preview
                </button>
            </div>
            <div class="pe-markdown-preview">
                @if (DynamicTemplatePreviewHtml.TryGetValue(b.EditorId, out var previewHtml))
                {
                    @((MarkupString)previewHtml)
                }
                else
                {
                    <p>Click Refresh Preview to render this Scriban template on the server.</p>
                }
            </div>
        }
        else
        {
            <label class="pe-field-label">Scriban template</label>
            <textarea class="pe-code-textarea"
                      placeholder="{{ block.title }}"
                      @bind="b.ScribanTemplate"
                      @onfocus="() => SelectBlock(b.EditorId)"></textarea>

            <label class="pe-field-label">Preview data JSON</label>
            <textarea class="pe-code-textarea pe-code-textarea-sm"
                      placeholder="{ &quot;title&quot;: &quot;Hello&quot; }"
                      @bind="b.ScribanDataJson"
                      @onfocus="() => SelectBlock(b.EditorId)"></textarea>
        }
    </div>
};
```

## Status
- Public renderer .razor files: All restored from git, have [CmsBlockRenderer] attributes, rendering through new source-generated system
- Editor RenderFragment methods: All present in current PageEditor.razor (restored from git)
- Migration code: Deleted (8 files + PagesModule.cs lines)
