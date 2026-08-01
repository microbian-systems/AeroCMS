# Neo UI Blocks

- Every Block with the Exception of Hero01Block.cs and Hero01BlockMapper.cs need to got into a Folder called Aero. They are the original blocks custom to Aero. 

```csharp

@* -------------------- 


@* ----------------- Filterable Table ----------------- *@ 


<div class="@container mb-5 flex items-center justify-between">
    <div>
        <h2 class="text-xl font-semibold">Users</h2>
        <p class="text-sm text-muted-foreground">@_filtered.Count of @_all.Count users</p>
    </div>
    <Button Size="ButtonSize.Small" Class="gap-1.5">
        <LucideIcon Name="plus" Size="14" />
        Add User
    </Button>
</div>

<Card>
    <div class="px-4 py-3 border-b flex items-center gap-2 flex-wrap">
        <div class="relative flex-1 min-w-[160px]">
            <LucideIcon Name="search" Size="14" Class="absolute left-2.5 top-1/2 -translate-y-1/2 text-muted-foreground pointer-events-none" />
            <Input Placeholder="Search by name or email…"
                   @bind-Value="_search"
                   @bind-Value:after="ApplyFilters"
                   Class="pl-8 h-8 text-sm" />
        </div>
        <Select @bind-Value="_statusFilter" @bind-Value:after="ApplyFilters" TValue="string" Class="w-32 h-8">
            <SelectTrigger><SelectValue /></SelectTrigger>
            <SelectContent>
                <SelectItem Value="@("all")"      Text="All Status"  TValue="string" />
                <SelectItem Value="@("active")"   Text="Active"      TValue="string" />
                <SelectItem Value="@("inactive")" Text="Inactive"    TValue="string" />
            </SelectContent>
        </Select>
        <Select @bind-Value="_roleFilter" @bind-Value:after="ApplyFilters" TValue="string" Class="w-32 h-8">
            <SelectTrigger><SelectValue /></SelectTrigger>
            <SelectContent>
                <SelectItem Value="@("all")"       Text="All Roles"  TValue="string" />
                <SelectItem Value="@("Admin")"     Text="Admin"      TValue="string" />
                <SelectItem Value="@("Developer")" Text="Developer"  TValue="string" />
                <SelectItem Value="@("Designer")"  Text="Designer"   TValue="string" />
                <SelectItem Value="@("Manager")"   Text="Manager"    TValue="string" />
                <SelectItem Value="@("Viewer")"    Text="Viewer"     TValue="string" />
            </SelectContent>
        </Select>
    </div>
    <CardContent Class="p-0">
        <DataTable TData="UserRow" Data="_filtered" ShowToolbar="false" ShowPagination="true" InitialPageSize="5">
            <Columns>
                <DataTableColumn TData="UserRow" TValue="string" Property="@(u => u.Name)" Header="Name" Sortable>
                    <CellTemplate Context="user">
                        <div class="flex items-center gap-3">
                            <div class="size-8 rounded-full bg-muted flex items-center justify-center shrink-0 text-xs font-medium">
                                @(string.Concat(user.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => p[0])))
                            </div>
                            <div>
                                <p class="font-medium">@user.Name</p>
                                <p class="text-xs text-muted-foreground">@user.Email</p>
                            </div>
                        </div>
                    </CellTemplate>
                </DataTableColumn>
                <DataTableColumn TData="UserRow" TValue="string" Property="@(u => u.Role)" Header="Role" Sortable>
                    <CellTemplate Context="user">
                        <Badge Variant="BadgeVariant.Secondary">@user.Role</Badge>
                    </CellTemplate>
                </DataTableColumn>
                <DataTableColumn TData="UserRow" TValue="bool" Property="@(u => u.Active)" Header="Status">
                    <CellTemplate Context="user">
                        <Badge Variant="@(user.Active ? BadgeVariant.Default : BadgeVariant.Outline)">
                            @(user.Active ? "Active" : "Inactive")
                        </Badge>
                    </CellTemplate>
                </DataTableColumn>
                <DataTableColumn TData="UserRow" TValue="string" Property="@(u => u.Joined)" Header="Joined" Sortable />
            </Columns>
        </DataTable>
    </CardContent>
</Card>

@code {
    record UserRow(string Name, string Email, string Role, bool Active, string Joined);

    string _search = "";
    string _statusFilter = "all";
    string _roleFilter = "all";

    readonly List<UserRow> _all =
    [
        new("Alice Johnson", "alice@acme.com", "Admin",     true,  "Jan 12, 2023"),
        new("Bob Smith",     "bob@acme.com",   "Developer", true,  "Feb 3, 2023"),
        new("Carol White",   "carol@acme.com", "Designer",  true,  "Mar 17, 2023"),
        new("David Kim",     "david@acme.com", "Developer", false, "Apr 5, 2023"),
        new("Eva Martinez",  "eva@acme.com",   "Manager",   true,  "May 22, 2023"),
        new("Frank Lee",     "frank@acme.com", "Viewer",    false, "Jun 8, 2023"),
        new("Grace Taylor",  "grace@acme.com", "Developer", true,  "Jul 14, 2023"),
        new("Henry Brown",   "henry@acme.com", "Designer",  true,  "Aug 30, 2023"),
    ];

    List<UserRow> _filtered = [];

    protected override void OnInitialized() => ApplyFilters();

    void ApplyFilters()
    {
        _filtered = _all.Where(u =>
            (string.IsNullOrEmpty(_search) ||
             u.Name.Contains(_search, StringComparison.OrdinalIgnoreCase) ||
             u.Email.Contains(_search, StringComparison.OrdinalIgnoreCase)) &&
            (_statusFilter == "all" ||
             (_statusFilter == "active"   &&  u.Active) ||
             (_statusFilter == "inactive" && !u.Active)) &&
            (_roleFilter == "all" || u.Role == _roleFilter)
        ).ToList();
    }
}


@* ------------------- Simple Data Order ---------------- *@ 


<div class="@container mb-5">
    <h2 class="text-xl font-semibold">Users</h2>
    <p class="text-sm text-muted-foreground">A list of all users in your account.</p>
</div>

<DataTable TData="UserRow" Data="_users" ShowToolbar="false" ShowPagination="false">
    <Columns>
        <DataTableColumn TData="UserRow" TValue="string" Property="@(u => u.Name)" Header="Name">
            <CellTemplate Context="user">
                <div class="flex items-center gap-3">
                    <div class="size-8 rounded-full bg-muted flex items-center justify-center shrink-0 text-xs font-medium">
                        @(string.Concat(user.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => p[0])))
                    </div>
                    <div>
                        <p class="font-medium">@user.Name</p>
                        <p class="text-xs text-muted-foreground">@user.Email</p>
                    </div>
                </div>
            </CellTemplate>
        </DataTableColumn>
        <DataTableColumn TData="UserRow" TValue="string" Property="@(u => u.Role)" Header="Role">
            <CellTemplate Context="user">
                <Badge Variant="BadgeVariant.Secondary">@user.Role</Badge>
            </CellTemplate>
        </DataTableColumn>
        <DataTableColumn TData="UserRow" TValue="bool" Property="@(u => u.Active)" Header="Status">
            <CellTemplate Context="user">
                <Badge Variant="@(user.Active ? BadgeVariant.Default : BadgeVariant.Outline)">
                    @(user.Active ? "Active" : "Inactive")
                </Badge>
            </CellTemplate>
        </DataTableColumn>
        <DataTableColumn TData="UserRow" TValue="string" Property="@(u => u.Joined)" Header="Joined" Sortable />
    </Columns>
</DataTable>

@code {
    record UserRow(string Name, string Email, string Role, bool Active, string Joined);

    readonly List<UserRow> _users =
    [
        new("Alice Johnson", "alice@acme.com", "Admin",     true,  "Jan 12, 2023"),
        new("Bob Smith",     "bob@acme.com",   "Developer", true,  "Feb 3, 2023"),
        new("Carol White",   "carol@acme.com", "Designer",  true,  "Mar 17, 2023"),
        new("David Kim",     "david@acme.com", "Developer", false, "Apr 5, 2023"),
        new("Eva Martinez",  "eva@acme.com",   "Manager",   true,  "May 22, 2023"),
        new("Frank Lee",     "frank@acme.com", "Viewer",    false, "Jun 8, 2023"),
        new("Grace Taylor",  "grace@acme.com", "Developer", true,  "Jul 14, 2023"),
        new("Henry Brown",   "henry@acme.com", "Designer",  true,  "Aug 30, 2023"),
    ];
}




@* --------------------- Order Confirmation ---------------- *@ 

<Card Class="w-full max-w-lg">
    <CardContent Class="pt-8 space-y-6">
        <div class="flex flex-col items-center gap-3 text-center">
            <div class="size-16 rounded-full bg-green-100 dark:bg-green-900/30 flex items-center justify-center">
                <LucideIcon Name="check" Size="28" Class="text-green-600 dark:text-green-400" />
            </div>
            <div>
                <h2 class="text-xl font-bold">Order Confirmed!</h2>
                <p class="text-sm text-muted-foreground mt-1">Thank you for your purchase. We'll send a confirmation to your email.</p>
            </div>
            <Badge Variant="BadgeVariant.Secondary">Order #ORD-2024-8842</Badge>
        </div>

        <Separator />

        <div class="space-y-1">
            <p class="text-sm font-semibold mb-3">Order Summary</p>
            <div class="overflow-x-auto">
            <DataTable TData="OrderItem" Data="_orderItems" ShowToolbar="false" ShowPagination="false">
                <Columns>
                    <DataTableColumn TData="OrderItem" TValue="string" Property="@(i => i.Name)" Header="Item">
                        <CellTemplate Context="item">
                            <span class="text-muted-foreground">@item.Name <span class="text-xs">×@item.Qty</span></span>
                        </CellTemplate>
                    </DataTableColumn>
                    <DataTableColumn TData="OrderItem" TValue="int" Property="@(i => i.Price)" Header="Amount">
                        <CellTemplate Context="item">
                            <span class="font-medium">$@(item.Price * item.Qty).00</span>
                        </CellTemplate>
                    </DataTableColumn>
                </Columns>
            </DataTable>
            </div>
            <div class="flex justify-between text-sm pt-1">
                <span class="text-muted-foreground">Shipping</span>
                <span class="font-medium text-green-600">Free</span>
            </div>
            <div class="flex justify-between font-bold text-sm border-t pt-2 mt-1">
                <span>Total</span>
                <span>$@_orderItems.Sum(i => i.Price * i.Qty).00</span>
            </div>
        </div>

        <div class="flex gap-3">
            <Button Class="flex-1">Continue Shopping</Button>
            <Button Variant="ButtonVariant.Outline" Class="flex-1 gap-1.5">
                <LucideIcon Name="receipt" Size="14" />
                View order
            </Button>
        </div>
    </CardContent>
</Card>

@code {
    record OrderItem(string Name, int Qty, int Price);

    readonly List<OrderItem> _orderItems = new()
    {
        new("Wireless Headphones Pro", 1, 149),
        new("USB-C Hub 7-in-1", 2, 49),
        new("Laptop Stand", 1, 59),
    };
}


@* ------------------ Shopping Cart ---------------------- *@  

<div class="@container w-full max-w-md bg-card border rounded-xl flex flex-col shadow-sm">
    <div class="flex items-center justify-between px-5 py-4 border-b">
        <h2 class="font-semibold">Your Cart</h2>
        <Badge Variant="BadgeVariant.Secondary">@_items.Count items</Badge>
    </div>

    <ScrollArea Class="flex-1 max-h-[380px]" EnableScrollShadows="true">
        <div class="divide-y px-5">
            @foreach (var item in _items)
            {
                <div class="flex items-center gap-4 py-4">
                    <div class="size-16 rounded-lg bg-muted flex items-center justify-center shrink-0">
                        <LucideIcon Name="image" Size="20" Class="text-muted-foreground/40" />
                    </div>
                    <div class="flex-1 min-w-0">
                        <p class="font-medium text-sm truncate">@item.Name</p>
                        <p class="text-xs text-muted-foreground">@item.Variant</p>
                        <div class="flex items-center gap-2 mt-2">
                            <Button Variant="ButtonVariant.Outline" Size="ButtonSize.Icon" Class="size-6" @onclick="() => DecrQty(item)">
                                <LucideIcon Name="minus" Size="12" />
                            </Button>
                            <span class="text-sm w-5 text-center">@item.Qty</span>
                            <Button Variant="ButtonVariant.Outline" Size="ButtonSize.Icon" Class="size-6" @onclick="() => IncrQty(item)">
                                <LucideIcon Name="plus" Size="12" />
                            </Button>
                        </div>
                    </div>
                    <div class="flex flex-col items-end gap-2">
                        <span class="font-semibold text-sm">$@(item.UnitPrice * item.Qty).00</span>
                        <Button Variant="ButtonVariant.Ghost" Size="ButtonSize.Icon" Class="size-6 text-muted-foreground hover:text-destructive" @onclick="() => _items.Remove(item)">
                            <LucideIcon Name="x" Size="14" />
                        </Button>
                    </div>
                </div>
            }
        </div>
    </ScrollArea>

    <div class="border-t px-5 py-4 space-y-3">
        <div class="flex justify-between text-sm">
            <span class="text-muted-foreground">Subtotal</span>
            <span class="font-medium">$@_items.Sum(i => i.UnitPrice * i.Qty).00</span>
        </div>
        <div class="flex justify-between text-sm">
            <span class="text-muted-foreground">Shipping</span>
            <span class="font-medium text-green-600">Free</span>
        </div>
        <Separator />
        <div class="flex justify-between font-semibold">
            <span>Total</span>
            <span>$@_items.Sum(i => i.UnitPrice * i.Qty).00</span>
        </div>
        <Button Class="w-full gap-2">
            <LucideIcon Name="lock" Size="16" />
            Checkout
        </Button>
    </div>
</div>

@code {
    class CartItem
    {
        public string Name { get; set; } = "";
        public string Variant { get; set; } = "";
        public int Qty { get; set; }
        public int UnitPrice { get; set; }
    }

    List<CartItem> _items = new()
    {
        new() { Name = "Wireless Headphones Pro", Variant = "Black / M", Qty = 1, UnitPrice = 149 },
        new() { Name = "Minimalist Desk Lamp", Variant = "White", Qty = 2, UnitPrice = 89 },
        new() { Name = "USB-C Hub 7-in-1", Variant = "Silver", Qty = 1, UnitPrice = 49 },
    };

    void IncrQty(CartItem item) => item.Qty++;
    void DecrQty(CartItem item) { if (item.Qty > 1) item.Qty--; }
}


@* ------------------- Product Detail ---------------------- *@ 

<div class="grid grid-cols-1 @3xl:grid-cols-2 gap-8 items-start">
    <div class="bg-muted rounded-xl h-[200px] flex items-center justify-center">
        <LucideIcon Name="image" Size="64" Class="text-muted-foreground/30" />
    </div>

    <div class="space-y-5">
        <Breadcrumb>
            <BreadcrumbList>
                <BreadcrumbItem><BreadcrumbLink href="javascript:void(0)">Home</BreadcrumbLink></BreadcrumbItem>
                <BreadcrumbSeparator />
                <BreadcrumbItem><BreadcrumbLink href="javascript:void(0)">Peripherals</BreadcrumbLink></BreadcrumbItem>
                <BreadcrumbSeparator />
                <BreadcrumbItem><BreadcrumbPage>Wireless Headphones Pro</BreadcrumbPage></BreadcrumbItem>
            </BreadcrumbList>
        </Breadcrumb>

        <div>
            <h1 class="text-2xl font-bold mb-1">Wireless Headphones Pro</h1>
            <div class="flex items-center gap-2">
                <Rating Value="4.5" ReadOnly="true" AllowHalf="true" />
                <span class="text-sm text-muted-foreground">4.5 (128 reviews)</span>
            </div>
        </div>

        <div class="flex items-baseline gap-3">
            <span class="text-3xl font-bold">$149.00</span>
            <span class="text-lg text-muted-foreground line-through">$199.00</span>
            <Badge Variant="BadgeVariant.Destructive">Save 25%</Badge>
        </div>

        <p class="text-sm text-muted-foreground leading-relaxed">
            Premium wireless headphones with active noise cancellation, 30-hour battery life, and studio-quality sound. Perfect for work and travel.
        </p>

        <div>
            <p class="text-sm font-medium mb-2">Size</p>
            <div class="flex gap-2">
                @foreach (var size in new[] { "S", "M", "L", "XL" })
                {
                    <Button Variant="@(_selectedSize == size ? ButtonVariant.Default : ButtonVariant.Outline)"
                            Size="ButtonSize.Small"
                            @onclick="() => _selectedSize = size">
                        @size
                    </Button>
                }
            </div>
        </div>

        <div class="flex items-center gap-3">
            <Label Class="text-sm font-medium">Quantity</Label>
            <NumericInput @bind-Value="_qty" Min="1" Max="99" Class="w-24" />
        </div>

        <div class="flex gap-3">
            <Button Class="flex-1 gap-2">
                <LucideIcon Name="shopping-cart" Size="16" />
                Add to Cart
            </Button>
            <Button Variant="ButtonVariant.Outline" Size="ButtonSize.Icon">
                <LucideIcon Name="heart" Size="18" />
            </Button>
        </div>
    </div>
</div>

@code {
    string _selectedSize = "M";
    int _qty = 1;
}


@* ------------------- Commerce - Product Card ----------------  *@ 


<div class="@container flex items-center justify-between mb-6">
    <h2 class="text-xl font-semibold">Featured Products</h2>
    <a href="#" @onclick:preventDefault class="text-sm text-primary hover:underline underline-offset-2">View all</a>
</div>

<div class="grid grid-cols-1 @sm:grid-cols-2 @3xl:grid-cols-3 gap-5">
    @foreach (var product in _products)
    {
        <Card Class="overflow-hidden flex flex-col">
            <div class="relative bg-muted aspect-[4/3] flex items-center justify-center">
                @if (product.Sale)
                {
                    <Badge Variant="BadgeVariant.Destructive" Class="absolute top-3 left-3 z-10">Sale</Badge>
                }
                <LucideIcon Name="image" Size="40" Class="text-muted-foreground/30" />
            </div>
            <CardContent Class="flex-1 pt-4">
                <p class="text-xs text-muted-foreground mb-1">@product.Category</p>
                <h3 class="font-semibold text-sm mb-2">@product.Name</h3>
                <div class="flex items-center gap-1.5 mb-3">
                    <Rating Value="product.Rating" ReadOnly="true" AllowHalf="true" Size="RatingSize.Small" />
                    <span class="text-xs text-muted-foreground">(@product.ReviewCount)</span>
                </div>
                <div class="flex items-center justify-between">
                    <div class="flex items-center gap-2">
                        <span class="font-bold">@product.Price</span>
                        @if (product.OriginalPrice != null)
                        {
                            <span class="text-sm text-muted-foreground line-through">@product.OriginalPrice</span>
                        }
                    </div>
                </div>
            </CardContent>
            <CardFooter Class="pt-0">
                <Button Class="w-full gap-2" Variant="ButtonVariant.Outline">
                    <LucideIcon Name="shopping-cart" Size="14" />
                    Add to Cart
                </Button>
            </CardFooter>
        </Card>
    }
</div>

@code {
    record Product(string Name, string Category, string Price, string? OriginalPrice, bool Sale, double Rating = 4.5, int ReviewCount = 128);

    readonly List<Product> _products = new()
    {
        new("Wireless Headphones Pro", "Audio", "$149.00", "$199.00", true, 4.5, 128),
        new("Minimalist Desk Lamp", "Lighting", "$89.00", null, false, 4.2, 64),
        new("Mechanical Keyboard", "Peripherals", "$229.00", "$279.00", true, 4.8, 215),
        new("Laptop Stand Adjustable", "Accessories", "$59.00", null, false, 4.4, 92),
        new("USB-C Hub 7-in-1", "Peripherals", "$49.00", null, false, 4.1, 47),
        new("Ergonomic Mouse", "Peripherals", "$79.00", "$99.00", true, 4.6, 183),
    };
}


@* ------------------- Address Form ---------------------- *@ 

<Card Class="w-full max-w-lg">
    <CardHeader>
        <CardTitle>Shipping Address</CardTitle>
        <CardDescription>Enter the address where you'd like to receive your order.</CardDescription>
    </CardHeader>
    <CardContent Class="space-y-4">
        <Field>
            <FieldLabel For="addr-name">Full name</FieldLabel>
            <FieldContent>
                <Input Id="addr-name" Placeholder="Jane Doe" />
            </FieldContent>
        </Field>
        <Field>
            <FieldLabel For="addr-line1">Address line 1</FieldLabel>
            <FieldContent>
                <Input Id="addr-line1" Placeholder="123 Main Street" />
            </FieldContent>
        </Field>
        <Field>
            <FieldLabel For="addr-line2">Address line 2 <span class="text-muted-foreground font-normal">(optional)</span></FieldLabel>
            <FieldContent>
                <Input Id="addr-line2" Placeholder="Apt, suite, unit, building, floor, etc." />
            </FieldContent>
        </Field>

        <div class="grid grid-cols-2 gap-4">
            <Field>
                <FieldLabel For="addr-city">City</FieldLabel>
                <FieldContent>
                    <Input Id="addr-city" Placeholder="New York" />
                </FieldContent>
            </Field>
            <Field>
                <FieldLabel For="addr-zip">ZIP / Postal code</FieldLabel>
                <FieldContent>
                    <Input Id="addr-zip" Placeholder="10001" />
                </FieldContent>
            </Field>
        </div>

        <div class="grid grid-cols-2 gap-4">
            <Field>
                <FieldLabel For="addr-state">State</FieldLabel>
                <FieldContent>
                    <Select TValue="string" @bind-Value="_state">
                        <SelectTrigger><SelectValue /></SelectTrigger>
                        <SelectContent>
                            <SelectItem Value="@("ny")" Text="New York" />
                            <SelectItem Value="@("ca")" Text="California" />
                            <SelectItem Value="@("tx")" Text="Texas" />
                            <SelectItem Value="@("fl")" Text="Florida" />
                            <SelectItem Value="@("wa")" Text="Washington" />
                        </SelectContent>
                    </Select>
                </FieldContent>
            </Field>
            <Field>
                <FieldLabel For="addr-country">Country</FieldLabel>
                <FieldContent>
                    <Select TValue="string" @bind-Value="_country">
                        <SelectTrigger><SelectValue /></SelectTrigger>
                        <SelectContent>
                            <SelectItem Value="@("us")" Text="United States" />
                            <SelectItem Value="@("ca")" Text="Canada" />
                            <SelectItem Value="@("gb")" Text="United Kingdom" />
                            <SelectItem Value="@("au")" Text="Australia" />
                            <SelectItem Value="@("de")" Text="Germany" />
                        </SelectContent>
                    </Select>
                </FieldContent>
            </Field>
        </div>

        <Field>
            <FieldLabel For="addr-phone">Phone number</FieldLabel>
            <FieldContent>
                <Input Id="addr-phone" Placeholder="+1 (555) 000-0000" />
            </FieldContent>
        </Field>

        <div class="flex items-center gap-3 pt-2">
            <Button>Save address</Button>
            <Button Variant="ButtonVariant.Outline">Cancel</Button>
        </div>
    </CardContent>
</Card>

@code {
    string _state = "ny";
    string _country = "us";
}



@* -----------------  Feedback Rating Form -------------  *@ 

<Card Class="w-full max-w-sm">
    <CardHeader>
        <CardTitle Class="text-center">How was your experience?</CardTitle>
        <CardDescription Class="text-center">Your feedback helps us improve.</CardDescription>
    </CardHeader>
    <CardContent Class="space-y-5">
        <div class="flex justify-center">
            <Rating @bind-Value="_rating" AllowHalf="true" />
        </div>
        <Field>
            <FieldLabel For="fb-comment">Comments <span class="text-muted-foreground font-normal">(optional)</span></FieldLabel>
            <FieldContent>
                <Textarea Id="fb-comment" Placeholder="Tell us what you think..." Rows="3" />
            </FieldContent>
        </Field>
        <Button Class="w-full">Submit feedback</Button>
    </CardContent>
</Card>

@code {
    double _rating = 0;
}

@* -------------------- Responsive Marketing Nav ------------- *@ 

<div class="@container w-full bg-background border rounded-lg overflow-hidden">

    <ResponsiveNavProvider>

        <header class="flex items-center justify-between px-5 h-16 border-b bg-background">
            <div class="flex items-center gap-3">
                <a href="" @onclick:preventDefault class="flex items-center gap-2">
                    <div class="size-7 rounded-md bg-foreground flex items-center justify-center">
                        <LucideIcon Name="layers" Size="14" Class="text-background" />
                    </div>
                    <span class="font-semibold text-sm">Acme Inc.</span>
                </a>
                @* Hamburger — visible only on mobile (container < md) *@
                <div class="@md:hidden">
                    <ResponsiveNavTrigger />
                </div>
            </div>

            @* Desktop nav links — hidden on mobile *@
            <div class="hidden @md:flex">
                <NavigationMenu>
                    <NavigationMenuList>
                        <NavigationMenuItem>
                            <NavigationMenuLink Href="">Features</NavigationMenuLink>
                        </NavigationMenuItem>
                        <NavigationMenuItem>
                            <NavigationMenuLink Href="">Components</NavigationMenuLink>
                        </NavigationMenuItem>
                        <NavigationMenuItem>
                            <NavigationMenuLink Href="">Pricing</NavigationMenuLink>
                        </NavigationMenuItem>
                        <NavigationMenuItem>
                            <NavigationMenuLink Href="">Blog</NavigationMenuLink>
                        </NavigationMenuItem>
                    </NavigationMenuList>
                </NavigationMenu>
            </div>

            @* Desktop actions — hidden on mobile *@
            <div class="hidden @md:flex items-center gap-2">
                <Button Variant="ButtonVariant.Ghost" Size="ButtonSize.Icon" Class="size-8">
                    <LucideIcon Name="github" Size="16" />
                </Button>
                <Button Size="ButtonSize.Small" Class="gap-1.5">
                    Get Started
                    <LucideIcon Name="arrow-right" Size="14" />
                </Button>
            </div>
        </header>

        @* Mobile sheet — slide-out content *@
        <ResponsiveNavContent>
            <Header>
                <div class="flex items-center gap-2">
                    <div class="size-7 rounded-md bg-foreground flex items-center justify-center">
                        <LucideIcon Name="layers" Size="14" Class="text-background" />
                    </div>
                    <span class="font-semibold text-sm">Acme Inc.</span>
                </div>
            </Header>
            <ChildContent>
                <nav class="flex flex-col space-y-5">
                    <a href="" @onclick:preventDefault class="text-base font-medium hover:text-primary transition-colors">Features</a>
                    <a href="" @onclick:preventDefault class="text-base font-medium hover:text-primary transition-colors">Components</a>
                    <a href="" @onclick:preventDefault class="text-base font-medium hover:text-primary transition-colors">Pricing</a>
                    <a href="" @onclick:preventDefault class="text-base font-medium hover:text-primary transition-colors">Blog</a>
                </nav>
            </ChildContent>
            <Footer>
                <Button Class="w-full gap-1.5">
                    Get Started
                    <LucideIcon Name="arrow-right" Size="14" />
                </Button>
            </Footer>
        </ResponsiveNavContent>

    </ResponsiveNavProvider>

    @* Hero stub — gives the preview spatial context *@
    <div class="flex flex-col items-center justify-center gap-4 px-6 py-20 bg-muted/10 text-center">
        <Badge Variant="BadgeVariant.Secondary" Class="gap-1.5">
            <LucideIcon Name="sparkles" Size="12" Class="text-primary" />
            Now in v3.2
        </Badge>
        <h1 class="text-3xl font-bold tracking-tight max-w-md">
            Build beautiful Blazor apps faster
        </h1>
        <p class="text-muted-foreground text-sm max-w-sm">
            100+ production-ready components. Switch to mobile to see the nav collapse.
        </p>
        <div class="flex items-center gap-2 mt-2">
            <Button Class="gap-2">
                Get started
                <LucideIcon Name="arrow-right" Size="15" />
            </Button>
            <Button Variant="ButtonVariant.Outline">View components</Button>
        </div>
    </div>

</div>


@* -------------------- Status / Social Row ------------- *@ 


<div class="@container w-full min-h-[120px] bg-background p-8">
    <div class="flex flex-col @md:flex-row items-center justify-center gap-6 @md:gap-0 divide-y @md:divide-y-0 @md:divide-x divide-border w-full max-w-4xl mx-auto">
        @foreach (var stat in _stats)
        {
            <div class="flex-1 flex flex-col items-center text-center px-8 py-2 @md:py-0">
                <span class="text-3xl @md:text-4xl font-bold tracking-tight">@stat.Value</span>
                <span class="text-sm text-muted-foreground mt-1">@stat.Label</span>
            </div>
        }
    </div>
</div>

@code {
    record StatItem(string Value, string Label);

    readonly List<StatItem> _stats = new()
    {
        new("10,000+", "Happy Users"),
        new("$2M+", "ARR Generated"),
        new("99.9%", "Uptime SLA"),
        new("4.9/5", "Average Rating"),
    };
}



@* -------------------- Sign-Up Form --------------- *@ 

<Card Class="w-full max-w-sm" class="@container">
    <CardHeader Class="space-y-1 pb-4">
        <div class="flex items-center justify-center mb-2">
            <div class="size-10 rounded-xl bg-foreground flex items-center justify-center">
                <LucideIcon Name="layers" Size="18" Class="text-background" />
            </div>
        </div>
        <CardTitle Class="text-xl font-semibold text-center">Create an account</CardTitle>
        <CardDescription Class="text-center">Enter your details to get started</CardDescription>
    </CardHeader>
    <CardContent Class="space-y-4">
        <div class="grid grid-cols-1 @md:grid-cols-2 gap-3">
            <Field>
                <FieldLabel For="first-name">First name</FieldLabel>
                <FieldContent>
                    <Input Id="first-name" Placeholder="Jane" />
                </FieldContent>
            </Field>
            <Field>
                <FieldLabel For="last-name">Last name</FieldLabel>
                <FieldContent>
                    <Input Id="last-name" Placeholder="Doe" />
                </FieldContent>
            </Field>
        </div>
        <Field>
            <FieldLabel For="email">Email</FieldLabel>
            <FieldContent>
                <Input Id="email" Type="InputType.Email" Placeholder="you@example.com" />
            </FieldContent>
        </Field>
        <Field>
            <FieldLabel For="password">Password</FieldLabel>
            <FieldContent>
                <Input Id="password" Type="InputType.Password" Placeholder="Min. 8 characters" />
            </FieldContent>
        </Field>
        <div class="flex items-start gap-2">
            <Checkbox Id="terms" Class="mt-0.5" />
            <Label For="terms" Class="text-sm font-normal cursor-pointer leading-snug">
                I agree to the
                <a href="#" @onclick:preventDefault class="text-foreground font-medium hover:underline underline-offset-2">Terms of Service</a>
                and
                <a href="#" @onclick:preventDefault class="text-foreground font-medium hover:underline underline-offset-2">Privacy Policy</a>
            </Label>
        </div>
        <Button Class="w-full">Create account</Button>
        <p class="text-center text-xs text-muted-foreground">
            Already have an account?
            <a href="#" @onclick:preventDefault class="text-foreground font-medium hover:underline underline-offset-2">Sign in</a>
        </p>
    </CardContent>
</Card>



@* ------------------- Sign-In Form --------------- *@ 

<Card Class="w-full max-w-sm" class="@container">
    <CardHeader Class="space-y-1 pb-4">
        <div class="flex items-center justify-center mb-2">
            <div class="size-10 rounded-xl bg-foreground flex items-center justify-center">
                <LucideIcon Name="layers" Size="18" Class="text-background" />
            </div>
        </div>
        <CardTitle Class="text-xl font-semibold text-center">Welcome back</CardTitle>
        <CardDescription Class="text-center">Sign in to your account to continue</CardDescription>
    </CardHeader>
    <CardContent Class="space-y-4">
        <Field>
            <FieldLabel For="email">Email</FieldLabel>
            <FieldContent>
                <Input Id="email" Type="InputType.Email" Placeholder="you@example.com" />
            </FieldContent>
        </Field>
        <Field>
            <FieldLabel>
                <div class="flex items-center justify-between w-full">
                    <span>Password</span>
                    <a href="#" @onclick:preventDefault class="text-xs text-muted-foreground hover:text-foreground transition-colors">
                        Forgot password?
                    </a>
                </div>
            </FieldLabel>
            <FieldContent>
                <Input Id="password" Type="InputType.Password" />
            </FieldContent>
        </Field>
        <div class="flex items-center gap-2">
            <Checkbox Id="remember" />
            <Label For="remember" Class="text-sm font-normal cursor-pointer">Remember me for 30 days</Label>
        </div>
        <Button Class="w-full">Sign in</Button>
        <FieldSeparator>Or continue with</FieldSeparator>
        <div class="grid grid-cols-2 gap-2">
            <Button Variant="ButtonVariant.Outline" Class="gap-2">
                <svg class="size-4 shrink-0" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
                    <path d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z" fill="#4285F4"/>
                    <path d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z" fill="#34A853"/>
                    <path d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z" fill="#FBBC05"/>
                    <path d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z" fill="#EA4335"/>
                </svg>
                Google
            </Button>
            <Button Variant="ButtonVariant.Outline" Class="gap-2">
                <svg class="size-4 shrink-0" viewBox="0 0 24 24" fill="currentColor" xmlns="http://www.w3.org/2000/svg">
                    <path d="M17.05 20.28c-.98.95-2.05.8-3.08.35-1.09-.46-2.09-.48-3.24 0-1.44.62-2.2.44-3.06-.35C2.79 15.25 3.51 7.59 9.05 7.31c1.35.07 2.29.74 3.08.8 1.18-.24 2.31-.93 3.57-.84 1.51.12 2.65.72 3.4 1.8-3.12 1.87-2.38 5.98.48 7.13-.57 1.5-1.31 2.99-2.54 4.09l.01-.01zM12.03 7.25c-.15-2.23 1.66-4.07 3.74-4.25.29 2.58-2.34 4.5-3.74 4.25z" />
                </svg>
                Apple
            </Button>
        </div>
        <p class="text-center text-xs text-muted-foreground">
            Don't have an account?
            <a href="#" @onclick:preventDefault class="text-foreground font-medium hover:underline underline-offset-2">Sign up</a>
        </p>
    </CardContent>
</Card>


@* -------------------  Centered Hero ---------------- *@

<section class="@container flex flex-col items-center justify-center min-h-[460px] w-full bg-background px-6 py-16 text-center">

    <Badge Variant="BadgeVariant.Secondary" Class="mb-4 gap-1.5">
        <LucideIcon Name="sparkles" Size="12" Class="text-primary" />
        Introducing NeoUI v3
    </Badge>

    <h1 class="text-4xl @md:text-5xl font-bold tracking-tight text-foreground max-w-3xl leading-tight mb-4">
        Build beautiful Blazor apps<br class="hidden @md:block" />
        <span class="text-primary">faster than ever</span>
    </h1>

    <p class="text-lg text-muted-foreground max-w-xl leading-relaxed mb-8">
        100+ production-ready components for .NET Blazor. Accessible, customizable, and built for speed.
        Start shipping in minutes, not days.
    </p>

    <div class="flex flex-col @md:flex-row items-center gap-3">
        <Button Size="ButtonSize.Large" Class="gap-2 px-6">
            Get started for free
            <LucideIcon Name="arrow-right" Size="16" />
        </Button>
        <Button Variant="ButtonVariant.Outline" Size="ButtonSize.Large" Class="gap-2 px-6">
            <LucideIcon Name="github" Size="16" />
            View on GitHub
        </Button>
    </div>

    <div class="mt-12 flex items-center gap-6 text-sm text-muted-foreground flex-wrap justify-center">
        <div class="flex items-center gap-1.5">
            <LucideIcon Name="circle-check" Size="14" Class="text-primary" />
            Free &amp; open source
        </div>
        <div class="flex items-center gap-1.5">
            <LucideIcon Name="circle-check" Size="14" Class="text-primary" />
            .NET 8+ compatible
        </div>
        <div class="flex items-center gap-1.5">
            <LucideIcon Name="circle-check" Size="14" Class="text-primary" />
            Dark mode included
        </div>
        <div class="flex items-center gap-1.5">
            <LucideIcon Name="circle-check" Size="14" Class="text-primary" />
            100+ components
        </div>
    </div>

</section>


@* --------------------- Feature Grid ---------------  *@ 


<section class="@container w-full bg-background px-6 py-16">
    <div class="max-w-5xl mx-auto">

        <div class="text-center mb-12">
            <h2 class="text-3xl font-bold tracking-tight mb-3">Everything you need to ship faster</h2>
            <p class="text-muted-foreground text-lg max-w-xl mx-auto">
                A complete design system for Blazor with 100+ components, themes, and patterns.
            </p>
        </div>

        <div class="grid grid-cols-1 @md:grid-cols-2 @4xl:grid-cols-3 gap-4">
            @foreach (var feature in _features)
            {
                <Card Class="border bg-card">
                    <CardHeader Class="pb-3">
                        <div class="size-10 rounded-lg bg-primary/10 flex items-center justify-center mb-3">
                            <LucideIcon Name="@feature.Icon" Size="18" Class="text-primary" />
                        </div>
                        <CardTitle Class="text-base font-semibold">@feature.Title</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <p class="text-sm text-muted-foreground leading-relaxed">@feature.Description</p>
                    </CardContent>
                </Card>
            }
        </div>

    </div>
</section>

@code {
    private record Feature(string Icon, string Title, string Description);

    private static readonly Feature[] _features =
    [
        new("palette",      "Themeable",         "85 built-in color themes with light and dark mode. Customize every token via CSS variables."),
        new("zap",          "Blazing Fast",       "Built on .NET 10 and Blazor. Static SSR, InteractiveServer, WebAssembly — all supported."),
        new("puzzle",       "Composable",         "Headless primitives + styled components. Mix and match to build exactly what you need."),
        new("accessibility","Accessible",         "WCAG 2.1 compliant components with full keyboard navigation and ARIA roles out of the box."),
        new("code-xml",       "Copy & Paste Ready", "Every component ships with working code examples. Just copy, paste, and customize."),
        new("layers",       "Production-Ready",   "Used in real apps. Tested across browsers and devices. Maintained by the NeoUI team."),
    ];
}


@* --------------------- Forgot Password ----------------------- *@


<Card Class="w-full max-w-sm" class="@container">
    <CardHeader Class="space-y-1 pb-4">
        <div class="flex items-center justify-center mb-2">
            <div class="size-10 rounded-xl bg-foreground flex items-center justify-center">
                <LucideIcon Name="key-round" Size="18" Class="text-background" />
            </div>
        </div>
        <CardTitle Class="text-xl font-semibold text-center">Forgot password?</CardTitle>
        <CardDescription Class="text-center">Enter your email and we'll send you a reset link</CardDescription>
    </CardHeader>
    <CardContent Class="space-y-4">
        <Field>
            <FieldLabel For="email">Email address</FieldLabel>
            <FieldContent>
                <Input Id="email" Type="InputType.Email" Placeholder="you@example.com" />
            </FieldContent>
        </Field>
        <Button Class="w-full">Send reset link</Button>
        <p class="text-center text-sm text-muted-foreground">
            <a href="#" @onclick:preventDefault class="inline-flex items-center gap-1 text-foreground hover:underline underline-offset-2 font-medium">
                <LucideIcon Name="arrow-left" Size="14" />
                Back to sign in
            </a>
        </p>
    </CardContent>
</Card>

@* --------------------- Verify Code ----------------------- *@

<Card Class="w-full max-w-sm" class="@container">
    <CardHeader Class="space-y-1 pb-4">
        <div class="flex items-center justify-center mb-2">
            <div class="size-10 rounded-xl bg-foreground flex items-center justify-center">
                <LucideIcon Name="mail" Size="18" Class="text-background" />
            </div>
        </div>
        <CardTitle Class="text-xl font-semibold text-center">Check your email</CardTitle>
        <CardDescription Class="text-center">We sent a 6-digit verification code to <span class="text-foreground font-medium">you@example.com</span></CardDescription>
    </CardHeader>
    <CardContent Class="space-y-4">
        <div class="flex justify-center">
            <InputOtp Length="6" @bind-Value="_otp"
                      OnValueChange="HandleValueChange"
                      Disabled="@(_state == VerifyState.Verifying || _state == VerifyState.Success)"
                      AriaInvalid="@(_state == VerifyState.Error)">
                <InputOtpGroup>
                    <InputOtpSlot Index="0" />
                    <InputOtpSlot Index="1" />
                    <InputOtpSlot Index="2" />
                </InputOtpGroup>
                <InputOtpSeparator />
                <InputOtpGroup>
                    <InputOtpSlot Index="3" />
                    <InputOtpSlot Index="4" />
                    <InputOtpSlot Index="5" />
                </InputOtpGroup>
            </InputOtp>
        </div>

        <Button Class="w-full gap-2"
                Disabled="@(_otp.Length < 6 || _state == VerifyState.Verifying || _state == VerifyState.Success)"
                @onclick="HandleVerify">
            @if (_state == VerifyState.Verifying)
            {
                <LucideIcon Name="loader-circle" Size="14" Class="animate-spin" />
                <span>Verifying...</span>
            }
            else
            {
                <span>Verify code</span>
            }
        </Button>

        @if (_state == VerifyState.Success)
        {
            <Alert Variant="AlertVariant.Success">
                <Icon><LucideIcon Name="circle-check" Class="h-4 w-4" /></Icon>
                <ChildContent>
                    <AlertTitle>Code verified!</AlertTitle>
                    <AlertDescription>Your identity has been confirmed.</AlertDescription>
                </ChildContent>
            </Alert>
        }
        else if (_state == VerifyState.Error)
        {
            <Alert Variant="AlertVariant.Destructive">
                <Icon><LucideIcon Name="octagon-alert" Class="h-4 w-4" /></Icon>
                <ChildContent>
                    <AlertTitle>Invalid code</AlertTitle>
                    <AlertDescription>The code you entered is incorrect. Please try again using 123456.</AlertDescription>
                </ChildContent>
            </Alert>
        }

        <p class="text-center text-sm text-muted-foreground">
            Didn't receive a code?
            <a href="#" @onclick:preventDefault class="text-foreground font-medium hover:underline underline-offset-2">Resend</a>
        </p>
    </CardContent>
</Card>

@code {
    private enum VerifyState { Idle, Verifying, Success, Error }

    private string _otp = "";
    private VerifyState _state = VerifyState.Idle;

    private void HandleValueChange(string value)
    {
        if (_state is VerifyState.Error or VerifyState.Success)
            _state = VerifyState.Idle;
    }

    private async Task HandleVerify()
    {
        if (_otp.Length < 6) return;
        _state = VerifyState.Verifying;
        await Task.Delay(1200);
        _state = _otp == "123456" ? VerifyState.Success : VerifyState.Error;
    }
}

@* ---------------------- Hero Split Layout ------------------- *@

<div class="@container w-full min-h-[480px] bg-background p-0 overflow-hidden">
    <div class="grid grid-cols-1 @lg:grid-cols-2 min-h-[480px]">
        <div class="flex flex-col justify-center px-8 py-12 @lg:px-12">
            <Badge Variant="BadgeVariant.Secondary" Class="w-fit mb-4">New — v2.0 is here</Badge>
            <h1 class="text-3xl @lg:text-4xl font-bold tracking-tight mb-4">
                Build better products,<br />ship faster
            </h1>
            <p class="text-muted-foreground text-base mb-8 max-w-md">
                The all-in-one platform that helps your team design, develop, and deliver exceptional digital experiences without the complexity.
            </p>
            <div class="flex flex-wrap gap-3">
                <Button Size="ButtonSize.Large">Get started free</Button>
                <Button Variant="ButtonVariant.Outline" Size="ButtonSize.Large" Class="gap-2">
                    <LucideIcon Name="play" Size="16" />
                    Watch demo
                </Button>
            </div>
            <p class="text-xs text-muted-foreground mt-4">No credit card required · Free 14-day trial</p>
        </div>

        <div class="relative bg-muted/40 flex items-center justify-center min-h-[280px] @lg:min-h-full overflow-hidden">
            <div class="absolute inset-0 bg-gradient-to-br from-primary/10 via-transparent to-blue-500/10"></div>
            <div class="relative z-10 flex flex-col items-center gap-4 p-8">
                <div class="w-full max-w-xs bg-card border rounded-xl p-4 shadow-lg rotate-[-2deg]">
                    <div class="flex items-center gap-2 mb-3">
                        <div class="size-3 rounded-full bg-red-400"></div>
                        <div class="size-3 rounded-full bg-yellow-400"></div>
                        <div class="size-3 rounded-full bg-green-400"></div>
                    </div>
                    <div class="space-y-2">
                        <div class="h-2 rounded bg-muted w-3/4"></div>
                        <div class="h-2 rounded bg-primary/30 w-full"></div>
                        <div class="h-2 rounded bg-muted w-5/6"></div>
                        <div class="h-2 rounded bg-muted w-2/3"></div>
                    </div>
                </div>
                <div class="w-full max-w-xs bg-card border rounded-xl p-4 shadow-lg rotate-[1.5deg] -mt-4">
                    <div class="flex items-center gap-3 mb-3">
                        <div class="size-8 rounded-full bg-primary/20 flex items-center justify-center">
                            <LucideIcon Name="zap" Size="14" Class="text-primary" />
                        </div>
                        <div>
                            <div class="h-2 rounded bg-muted w-24"></div>
                            <div class="h-1.5 rounded bg-muted/60 w-16 mt-1"></div>
                        </div>
                    </div>
                    <div class="flex gap-1">
                        <div class="h-1.5 rounded-full bg-primary flex-1"></div>
                        <div class="h-1.5 rounded-full bg-primary/40 flex-1"></div>
                        <div class="h-1.5 rounded-full bg-muted flex-1"></div>
                    </div>
                </div>
            </div>
        </div>
    </div>
</div>

@* ---------------------- Feature Icon Row -------------------- *@

<div class="@container w-full min-h-[200px] bg-background p-8">
    <div class="flex flex-col @md:flex-row items-start @md:items-center gap-8 @md:gap-4">
        @foreach (var feature in _features)
        {
            <div class="flex-1 flex flex-col @md:items-center @md:text-center gap-3">
                <div class="size-10 rounded-xl bg-primary/10 flex items-center justify-center shrink-0">
                    <LucideIcon Name="@feature.Icon" Size="20" Class="text-primary" />
                </div>
                <div>
                    <h3 class="font-semibold text-sm mb-1">@feature.Title</h3>
                    <p class="text-sm text-muted-foreground leading-relaxed">@feature.Description</p>
                </div>
            </div>
            @if (feature != _features[^1])
            {
                <div class="hidden @md:block w-px h-16 bg-border self-center shrink-0"></div>
            }
        }
    </div>
</div>

@code {
    record FeatureItem(string Icon, string Title, string Description);

    readonly List<FeatureItem> _features = new()
    {
        new("zap", "Lightning Fast", "Optimized for performance with sub-second load times across the globe."),
        new("shield-check", "Secure by Default", "Enterprise-grade security baked in from the ground up."),
        new("sliders-horizontal", "Fully Customizable", "Adapt every aspect to match your brand and workflow."),
        new("headphones", "24/7 Support", "Our team is available around the clock to help you succeed."),
    };
}

@* ---------------------- Contact Us Basic --------------------- *@

<Card Class="w-full max-w-lg" class="@container">
    <CardHeader>
        <CardTitle>Contact Us</CardTitle>
        <CardDescription>Fill out the form below and we'll get back to you shortly.</CardDescription>
    </CardHeader>
    <CardContent Class="space-y-4">
        <div class="grid grid-cols-1 @sm:grid-cols-2 gap-4">
            <Field>
                <FieldLabel For="fname">First name</FieldLabel>
                <FieldContent>
                    <Input Id="fname" Placeholder="Jane" />
                </FieldContent>
            </Field>
            <Field>
                <FieldLabel For="lname">Last name</FieldLabel>
                <FieldContent>
                    <Input Id="lname" Placeholder="Doe" />
                </FieldContent>
            </Field>
        </div>
        <Field>
            <FieldLabel For="email">Email</FieldLabel>
            <FieldContent>
                <Input Id="email" Type="InputType.Email" Placeholder="you@example.com" />
            </FieldContent>
        </Field>
        <Field>
            <FieldLabel For="subject">Subject</FieldLabel>
            <FieldContent>
                <Input Id="subject" Placeholder="How can we help?" />
            </FieldContent>
        </Field>
        <Field>
            <FieldLabel For="message">Message</FieldLabel>
            <FieldContent>
                <Textarea Id="message" Placeholder="Tell us more about your inquiry..." Rows="4" />
            </FieldContent>
        </Field>
        <Button Class="w-full">Send message</Button>
    </CardContent>
</Card>


@* ---------------- Newsletter Signup ---------------- *@ 

<div class="@container w-full max-w-xl text-center space-y-4">
    <h2 class="text-2xl font-bold">Stay in the loop</h2>
    <p class="text-muted-foreground">Get the latest news, product updates, and tips delivered straight to your inbox.</p>
    <div class="flex flex-col @sm:flex-row gap-2 max-w-md mx-auto">
        <Input Type="InputType.Email" Placeholder="Enter your email" Class="flex-1" />
        <Button Size="ButtonSize.Small">Subscribe</Button>
    </div>
    <p class="text-xs text-muted-foreground">
        <LucideIcon Name="lock" Size="12" Class="inline mr-1" />
        We respect your privacy. Unsubscribe at any time.
    </p>
</div>


@* ------------------  Wizard ---------------------- *@

<Card Class="w-full max-w-md">
    <CardHeader>
        <div class="flex items-center justify-between mb-2">
            @for (int i = 1; i <= 3; i++)
            {
                var stepNum = i;
                var isActive = _step == stepNum;
                var isDone = _step > stepNum;
                <div class="flex items-center gap-2 flex-1 @(stepNum < 3 ? "relative" : "")">
                    <div class="size-8 rounded-full flex items-center justify-center text-sm font-semibold shrink-0 @(isDone ? "bg-primary text-primary-foreground" : isActive ? "bg-primary text-primary-foreground" : "bg-muted text-muted-foreground")">
                        @if (isDone)
                        {
                            <LucideIcon Name="check" Size="14" />
                        }
                        else
                        {
                            @stepNum
                        }
                    </div>
                    <span class="text-xs font-medium @(isActive ? "text-foreground" : "text-muted-foreground")">@_stepLabels[stepNum - 1]</span>
                    @if (stepNum < 3)
                    {
                        <div class="flex-1 h-px bg-border mx-2"></div>
                    }
                </div>
            }
        </div>
    </CardHeader>
    <CardContent Class="space-y-4">
        @if (_step == 1)
        {
            <h3 class="font-semibold text-base">Create your account</h3>
            <Field>
                <FieldLabel For="wiz-name">Full name</FieldLabel>
                <FieldContent>
                    <Input Id="wiz-name" Placeholder="Jane Doe" />
                </FieldContent>
            </Field>
            <Field>
                <FieldLabel For="wiz-email">Email</FieldLabel>
                <FieldContent>
                    <Input Id="wiz-email" Type="InputType.Email" Placeholder="you@example.com" />
                </FieldContent>
            </Field>
            <Field>
                <FieldLabel For="wiz-pass">Password</FieldLabel>
                <FieldContent>
                    <Input Id="wiz-pass" Type="InputType.Password" />
                </FieldContent>
            </Field>
        }
        else if (_step == 2)
        {
            <h3 class="font-semibold text-base">Your profile</h3>
            <Field>
                <FieldLabel For="wiz-role">Job title</FieldLabel>
                <FieldContent>
                    <Input Id="wiz-role" Placeholder="e.g. Software Engineer" />
                </FieldContent>
            </Field>
            <Field>
                <FieldLabel For="wiz-company">Company</FieldLabel>
                <FieldContent>
                    <Input Id="wiz-company" Placeholder="Acme Corp" />
                </FieldContent>
            </Field>
            <Field>
                <FieldLabel For="wiz-bio">Short bio</FieldLabel>
                <FieldContent>
                    <Textarea Id="wiz-bio" Placeholder="Tell us a little about yourself..." Rows="3" />
                </FieldContent>
            </Field>
        }
        else
        {
            <h3 class="font-semibold text-base">Confirm &amp; finish</h3>
            <div class="rounded-lg border bg-muted/30 p-4 space-y-2 text-sm">
                <div class="flex justify-between">
                    <span class="text-muted-foreground">Email</span>
                    <span class="font-medium">you@example.com</span>
                </div>
                <Separator />
                <div class="flex justify-between">
                    <span class="text-muted-foreground">Company</span>
                    <span class="font-medium">Acme Corp</span>
                </div>
                <Separator />
                <div class="flex justify-between">
                    <span class="text-muted-foreground">Plan</span>
                    <Badge Variant="BadgeVariant.Default">Pro</Badge>
                </div>
            </div>
            <div class="flex items-center gap-2">
                <Checkbox Id="terms" />
                <a href="#" @onclick:preventDefault class="underline">Terms of Service</a>
            </div>
        }

        <div class="flex justify-between pt-2">
            <Button Variant="ButtonVariant.Outline" Disabled="_step == 1" @onclick="() => _step--">Previous</Button>
            @if (_step < 3)
            {
                <Button @onclick="() => _step++">Next step</Button>
            }
            else
            {
                <Button>Create account</Button>
            }
        </div>
    </CardContent>
</Card>

@code {
    int _step = 1;
    readonly string[] _stepLabels = { "Account", "Profile", "Confirm" };
}




@* ------------  Top Navigation Bar ---------------- *@

<div class="@container w-full min-h-[200px] bg-background border-b">
    <div class="flex items-center justify-between h-16 px-4 @md:px-6 border-b">
        <div class="flex items-center gap-4">
            <a href="#" @onclick:preventDefault class="flex items-center gap-2 font-semibold shrink-0">
                <div class="size-7 rounded-lg bg-foreground flex items-center justify-center">
                    <LucideIcon Name="layers" Size="14" Class="text-background" />
                </div>
                <span class="text-sm">Acme</span>
            </a>
            <div class="hidden @md:block">
                <NavigationMenu>
                    <NavigationMenuList>
                        <NavigationMenuItem Value="products">
                            <NavigationMenuTrigger>Products</NavigationMenuTrigger>
                            <NavigationMenuContent>
                                <ul class="grid gap-1 p-3 min-w-[280px]">
                                    <li>
                                        <NavigationMenuLink Href="#" @onclick:preventDefault>
                                            <div class="text-sm font-medium">Analytics</div>
                                            <p class="text-xs text-muted-foreground">Real-time usage metrics and insights.</p>
                                        </NavigationMenuLink>
                                    </li>
                                    <li>
                                        <NavigationMenuLink Href="#" @onclick:preventDefault>
                                            <div class="text-sm font-medium">Automations</div>
                                            <p class="text-xs text-muted-foreground">Streamline repetitive tasks automatically.</p>
                                        </NavigationMenuLink>
                                    </li>
                                    <li>
                                        <NavigationMenuLink Href="#" @onclick:preventDefault>
                                            <div class="text-sm font-medium">Integrations</div>
                                            <p class="text-xs text-muted-foreground">Connect with your favorite tools.</p>
                                        </NavigationMenuLink>
                                    </li>
                                </ul>
                            </NavigationMenuContent>
                        </NavigationMenuItem>
                        <NavigationMenuItem Value="pricing">
                            <NavigationMenuLink Href="#" @onclick:preventDefault>Pricing</NavigationMenuLink>
                        </NavigationMenuItem>
                        <NavigationMenuItem Value="docs">
                            <NavigationMenuLink Href="#" @onclick:preventDefault>Docs</NavigationMenuLink>
                        </NavigationMenuItem>
                    </NavigationMenuList>
                </NavigationMenu>
            </div>
        </div>

        <div class="flex items-center gap-2">
            <div class="hidden @md:flex items-center gap-2">
                <Button Variant="ButtonVariant.Ghost" Size="ButtonSize.Small">Sign in</Button>
                <Button Size="ButtonSize.Small">Get started</Button>
            </div>
            <div class="@md:hidden">
                <Button Variant="ButtonVariant.Ghost" Size="ButtonSize.Icon">
                    <LucideIcon Name="menu" Size="20" />
                </Button>
            </div>
        </div>
    </div>

    <div class="flex items-center justify-center py-10 text-muted-foreground text-sm">
        Page content appears here
    </div>
</div>



@* ------------ - Breadcrumb + Page Header ------------------ *@ 

<div class="w-full min-h-[160px] bg-background p-6">
    <Breadcrumb>
        <BreadcrumbList>
            <BreadcrumbItem>
                <BreadcrumbLink href="javascript:void(0)">Home</BreadcrumbLink>
            </BreadcrumbItem>
            <BreadcrumbSeparator />
            <BreadcrumbItem>
                <BreadcrumbLink href="javascript:void(0)">Settings</BreadcrumbLink>
            </BreadcrumbItem>
            <BreadcrumbSeparator />
            <BreadcrumbItem>
                <BreadcrumbPage>Team Members</BreadcrumbPage>
            </BreadcrumbItem>
        </BreadcrumbList>
    </Breadcrumb>

    <div class="mt-4 flex items-start justify-between gap-4">
        <div>
            <h1 class="text-2xl font-bold tracking-tight">Team Members</h1>
            <p class="text-muted-foreground text-sm mt-1">Manage your team, roles, and permissions.</p>
        </div>
        <div class="flex items-center gap-2 shrink-0">
            <Button Variant="ButtonVariant.Outline" Size="ButtonSize.Small" Class="gap-1.5">
                <LucideIcon Name="download" Size="14" />
                Export
            </Button>
            <Button Size="ButtonSize.Small" Class="gap-1.5">
                <LucideIcon Name="plus" Size="14" />
                New Member
            </Button>
        </div>
    </div>
</div>

@* ------------------  User Management Table --------------- *@

<div class="@container flex items-center justify-between mb-4">
    <div>
        <h2 class="text-xl font-semibold">User Management</h2>
        <p class="text-sm text-muted-foreground">@_users.Count total users</p>
    </div>
    <Button Size="ButtonSize.Small" Class="gap-1.5">
        <LucideIcon Name="plus" Size="14" />
        Add User
    </Button>
</div>

<DataTable TData="UserRow" Data="_users" ShowPagination="false">
    <Columns>
        <DataTableColumn TData="UserRow" TValue="string" Property="@(u => u.Name)" Header="User" Sortable>
            <CellTemplate Context="user">
                <div class="flex items-center gap-3">
                    <div class="size-8 rounded-full bg-muted flex items-center justify-center shrink-0 text-xs font-medium">
                        @(user.Name.Length >= 2 ? user.Name[..2].ToUpper() : user.Name.ToUpper())
                    </div>
                    <div>
                        <p class="font-medium">@user.Name</p>
                        <p class="text-xs text-muted-foreground">@user.Email</p>
                    </div>
                </div>
            </CellTemplate>
        </DataTableColumn>
        <DataTableColumn TData="UserRow" TValue="string" Property="@(u => u.Role)" Header="Role" Sortable>
            <CellTemplate Context="user">
                <Badge Variant="BadgeVariant.Secondary">@user.Role</Badge>
            </CellTemplate>
        </DataTableColumn>
        <DataTableColumn TData="UserRow" TValue="bool" Property="@(u => u.Active)" Header="Status">
            <CellTemplate Context="user">
                <Badge Variant="@(user.Active ? BadgeVariant.Default : BadgeVariant.Outline)">
                    @(user.Active ? "Active" : "Inactive")
                </Badge>
            </CellTemplate>
        </DataTableColumn>
        <DataTableColumn TData="UserRow" TValue="string" Property="@(u => u.Name)" Header="Actions">
            <CellTemplate Context="user">
                <DropdownMenu>
                    <DropdownMenuTrigger AsChild>
                        <Button Variant="ButtonVariant.Ghost" Size="ButtonSize.Icon" Class="size-8">
                            <LucideIcon Name="ellipsis" Size="16" />
                        </Button>
                    </DropdownMenuTrigger>
                    <DropdownMenuContent>
                        <DropdownMenuLabel>Actions</DropdownMenuLabel>
                        <DropdownMenuSeparator />
                        <DropdownMenuItem>Edit</DropdownMenuItem>
                        <DropdownMenuItem>View profile</DropdownMenuItem>
                        <DropdownMenuSeparator />
                        <DropdownMenuItem Class="text-destructive">Remove</DropdownMenuItem>
                    </DropdownMenuContent>
                </DropdownMenu>
            </CellTemplate>
        </DataTableColumn>
    </Columns>
</DataTable>

@code {
    record UserRow(string Name, string Email, string Role, bool Active);

    readonly List<UserRow> _users =
    [
        new("Alice Johnson", "alice@acme.com", "Admin",     true),
        new("Bob Smith",     "bob@acme.com",   "Developer", true),
        new("Carol White",   "carol@acme.com", "Designer",  true),
        new("David Kim",     "david@acme.com", "Developer", false),
        new("Eva Martinez",  "eva@acme.com",   "Manager",   true),
        new("Frank Lee",     "frank@acme.com", "Viewer",    false),
    ];
}

@* ------------------- Metrics Board -------------- *@

<div class="@container mb-6">
    <h2 class="text-xl font-semibold">SaaS Metrics</h2>
    <p class="text-sm text-muted-foreground">Key performance indicators for this month</p>
</div>

<div class="grid grid-cols-1 @sm:grid-cols-2 @3xl:grid-cols-3 gap-4">
    @foreach (var metric in _metrics)
    {
        <Card>
            <CardContent Class="pt-6">
                <div class="flex items-center justify-between mb-1">
                    <span class="text-sm text-muted-foreground">@metric.Label</span>
                    <LucideIcon Name="@metric.Icon" Size="16" Class="text-muted-foreground" />
                </div>
                <div class="text-2xl font-bold tracking-tight mb-2">@metric.Value</div>
                <Badge Variant="@metric.TrendVariant" Class="text-xs">@metric.Trend</Badge>
            </CardContent>
        </Card>
    }
</div>

@code {
    record MetricCard(string Label, string Value, string Trend, string Icon, BadgeVariant TrendVariant);

    readonly List<MetricCard> _metrics = new()
    {
        new("MRR", "$24,500", "+8.1% vs last month", "trending-up", BadgeVariant.Default),
        new("ARR", "$294,000", "+8.1% annualized", "bar-chart-big", BadgeVariant.Default),
        new("Churn Rate", "2.4%", "-0.3% improvement", "user-minus", BadgeVariant.Default),
        new("NPS Score", "68", "+4 pts this quarter", "smile", BadgeVariant.Default),
        new("Active Subscriptions", "1,284", "+52 new this month", "credit-card", BadgeVariant.Secondary),
        new("Top Customer", "Globex Corp", "$3,200/mo", "star", BadgeVariant.Secondary),
    };
}


@* -------------------------------  Notifications Panel ------------------ -- *@

<div class="@container flex items-center justify-between mb-4">
    <h2 class="text-xl font-semibold">Notifications</h2>
    <a href="#" @onclick:preventDefault class="text-sm text-muted-foreground hover:text-foreground transition-colors">Mark all read</a>
</div>

<Card Class="max-w-lg">
    <ScrollArea Class="h-[420px]" EnableScrollShadows="true">
        <div class="p-4 space-y-4">
            <div>
                <p class="text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-2 px-1">Today</p>
                <div class="space-y-1">
                    @foreach (var n in _todayItems)
                    {
                        <div class="flex items-start gap-3 p-3 rounded-lg hover:bg-muted/50 transition-colors @(n.Unread ? "bg-primary/5" : "")">
                            <Avatar Class="size-9 shrink-0">
                                <AvatarFallback>@n.Initials</AvatarFallback>
                            </Avatar>
                            <div class="flex-1 min-w-0">
                                <p class="text-sm leading-snug">@((MarkupString)n.Text)</p>
                                <span class="text-xs text-muted-foreground">@n.Time</span>
                            </div>
                            @if (n.Unread)
                            {
                                <div class="size-2 rounded-full bg-primary mt-2 shrink-0"></div>
                            }
                        </div>
                    }
                </div>
            </div>

            <Separator />

            <div>
                <p class="text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-2 px-1">Earlier</p>
                <div class="space-y-1">
                    @foreach (var n in _earlierItems)
                    {
                        <div class="flex items-start gap-3 p-3 rounded-lg hover:bg-muted/50 transition-colors">
                            <Avatar Class="size-9 shrink-0">
                                <AvatarFallback>@n.Initials</AvatarFallback>
                            </Avatar>
                            <div class="flex-1 min-w-0">
                                <p class="text-sm leading-snug">@((MarkupString)n.Text)</p>
                                <span class="text-xs text-muted-foreground">@n.Time</span>
                            </div>
                        </div>
                    }
                </div>
            </div>
        </div>
    </ScrollArea>
</Card>

@code {
    record NotifItem(string Initials, string Text, string Time, bool Unread = false);

    readonly List<NotifItem> _todayItems = new()
    {
        new("JD", "<strong>Jane Doe</strong> deployed a new release to production.", "2 min ago", true),
        new("MS", "<strong>Mark S.</strong> mentioned you in pull request #142.", "18 min ago", true),
        new("AK", "<strong>Amy K.</strong> assigned you to issue #88.", "1 hr ago", true),
    };

    readonly List<NotifItem> _earlierItems = new()
    {
        new("RB", "<strong>Rob B.</strong> merged your pull request.", "Yesterday at 4:30 PM"),
        new("TL", "<strong>Tina L.</strong> commented on your dashboard design.", "2 days ago"),
        new("SY", "<strong>System</strong> Your scheduled backup completed successfully.", "3 days ago"),
    };
}



@* ------------------------------ sign-in split screen ----------------- *@ 

<div class="@container w-full min-h-[580px] bg-background overflow-hidden">
    <div class="grid grid-cols-1 @2xl:grid-cols-2 min-h-[580px]">
        <div class="hidden @2xl:flex flex-col justify-between bg-foreground text-background p-12">
            <div class="flex items-center gap-2">
                <div class="size-8 rounded-xl bg-background/20 flex items-center justify-center">
                    <LucideIcon Name="layers" Size="16" Class="text-background" />
                </div>
                <span class="font-semibold">Acme</span>
            </div>

            <div class="space-y-6">
                <blockquote class="text-xl font-medium leading-relaxed">
                    "This platform transformed how our team collaborates. We ship twice as fast with half the friction."
                </blockquote>
                <div>
                    <p class="font-semibold">Sarah Chen</p>
                    <p class="text-background/60 text-sm">CTO at Streamline Inc.</p>
                </div>
            </div>

            <div class="space-y-3">
                @foreach (var feat in _features)
                {
                    <div class="flex items-center gap-3">
                        <div class="size-5 rounded-full bg-background/20 flex items-center justify-center shrink-0">
                            <LucideIcon Name="check" Size="12" Class="text-background" />
                        </div>
                        <span class="text-sm text-background/80">@feat</span>
                    </div>
                }
            </div>
        </div>

        <div class="flex items-center justify-center p-8 @2xl:p-12 bg-muted/20">
            <div class="w-full max-w-sm space-y-6">
                <div>
                    <h1 class="text-2xl font-bold">Welcome back</h1>
                    <p class="text-muted-foreground text-sm mt-1">Sign in to your account to continue</p>
                </div>

                <div class="space-y-4">
                    <Field>
                        <FieldLabel For="ss-email">Email</FieldLabel>
                        <FieldContent>
                            <Input Id="ss-email" Type="InputType.Email" Placeholder="you@example.com" />
                        </FieldContent>
                    </Field>
                    <Field>
                        <FieldLabel>
                            <div class="flex items-center justify-between w-full">
                                <span>Password</span>
                                <a href="#" @onclick:preventDefault class="text-xs text-muted-foreground hover:text-foreground">Forgot password?</a>
                            </div>
                        </FieldLabel>
                        <FieldContent>
                            <Input Id="ss-pass" Type="InputType.Password" />
                        </FieldContent>
                    </Field>
                    <Button Class="w-full">Sign in</Button>
                </div>

                <p class="text-center text-sm text-muted-foreground">
                    Don't have an account?
                    <a href="#" @onclick:preventDefault class="text-foreground font-medium hover:underline underline-offset-2">Sign up</a>
                </p>
            </div>
        </div>
    </div>
</div>

@code {
    readonly string[] _features =
    {
        "Deploy in minutes, not days",
        "99.99% uptime SLA guaranteed",
        "SOC 2 Type II certified",
    };
}


@* --------------------------  account locoked ------------------------ *@
<Card Class="w-full max-w-sm" class="@container">
    <CardContent Class="pt-8 pb-8 space-y-5">
        <div class="flex flex-col items-center gap-4">
            <div class="size-16 rounded-full bg-destructive/10 flex items-center justify-center">
                <LucideIcon Name="lock" Size="28" Class="text-destructive" />
            </div>
            <div class="text-center space-y-1">
                <h2 class="text-xl font-semibold">Account Suspended</h2>
                <p class="text-sm text-muted-foreground">Your account has been temporarily locked due to multiple failed sign-in attempts.</p>
            </div>
        </div>

        <Alert Variant="AlertVariant.Warning">
            <AlertTitle>Action required</AlertTitle>
            <AlertDescription>Please verify your identity or contact support to restore access to your account.</AlertDescription>
        </Alert>

        <div class="space-y-2">
            <Button Class="w-full" Variant="ButtonVariant.Default">Try again</Button>
            <Button Class="w-full" Variant="ButtonVariant.Outline">
                <LucideIcon Name="headphones" Size="16" Class="mr-2" />
                Contact support
            </Button>
        </div>

        <p class="text-center text-xs text-muted-foreground">
            If you believe this is a mistake, please
            <a href="#" @onclick:preventDefault class="text-foreground underline underline-offset-2">let us know</a>
        </p>
    </CardContent>
</Card>



@* ****************************  Pricing - 3 Cards ******************************** *@

<div class="@container text-center mb-10">
    <h2 class="text-2xl font-bold mb-2">Simple, transparent pricing</h2>
    <p class="text-muted-foreground">Start for free, scale as you grow. No hidden fees.</p>
</div>

<div class="grid grid-cols-1 @md:grid-cols-3 gap-6 max-w-5xl mx-auto">
    @foreach (var plan in _plans)
    {
        var isSelected = plan.Name == _selectedPlan;
        <div class="cursor-pointer" tabindex="0" role="button"
             @onclick="() => _selectedPlan = plan.Name"
             @onkeydown="@(e => { if (e.Key is "Enter" or " ") _selectedPlan = plan.Name; })"
             aria-pressed="@isSelected.ToString().ToLowerInvariant()"
             aria-label="Select @plan.Name plan">
        <Card Class="@("flex flex-col h-full transition-all duration-200 " + (isSelected ? "border-primary ring-2 ring-primary" : "hover:border-primary/40"))">
            @if (plan.Highlighted && isSelected)
            {
                <div class="flex justify-center -mt-3">
                    <Badge Variant="BadgeVariant.Default" Class="shadow">Most Popular</Badge>
                </div>
            }
            <CardHeader>
                <div class="flex items-center justify-between">
                    <CardTitle Class="text-lg">@plan.Name</CardTitle>
                    @if (isSelected)
                    {
                        <div class="size-5 rounded-full bg-primary flex items-center justify-center shrink-0">
                            <LucideIcon Name="check" Size="12" Class="text-primary-foreground" />
                        </div>
                    }
                </div>
                <CardDescription>@plan.Description</CardDescription>
                <div class="pt-2">
                    <span class="text-3xl font-bold">@plan.Price</span>
                    @if (plan.Period != null)
                    {
                        <span class="text-muted-foreground text-sm">/@plan.Period</span>
                    }
                </div>
            </CardHeader>
            <CardContent Class="flex-1">
                <ul class="space-y-2">
                    @foreach (var feat in plan.Features)
                    {
                        <li class="flex items-center gap-2 text-sm">
                            <LucideIcon Name="check" Size="16" Class="@(isSelected ? "text-primary shrink-0" : "text-muted-foreground shrink-0")" />
                            @feat
                        </li>
                    }
                </ul>
            </CardContent>
            <CardFooter>
                <Button Class="w-full" Variant="@(isSelected ? ButtonVariant.Default : ButtonVariant.Outline)">@plan.Cta</Button>
            </CardFooter>
        </Card>
        </div>
    }
</div>

@code {
    record PricingPlan(string Name, string Description, string Price, string? Period, bool Highlighted, string Cta, string[] Features);

    string _selectedPlan = "Pro";

    readonly List<PricingPlan> _plans =
    [
        new("Free", "Perfect for side projects", "$0", null, false, "Get started",
        [
            "Up to 3 projects",
            "1 GB storage",
            "Community support",
            "Basic analytics",
        ]),
        new("Pro", "For growing teams", "$19", "mo", true, "Start free trial",
        [
            "Unlimited projects",
            "50 GB storage",
            "Priority support",
            "Advanced analytics",
            "Custom domains",
            "Team collaboration",
        ]),
        new("Enterprise", "For large organizations", "Custom", null, false, "Contact sales",
        [
            "Everything in Pro",
            "Unlimited storage",
            "Dedicated support",
            "SLA guarantee",
            "SSO / SAML",
            "Custom contracts",
        ]),
    ];
}


@* ****************************  Pricing - Feature Comparison ************************** *@

<div class="text-center mb-8">
    <h2 class="text-2xl font-bold mb-2">Compare plans</h2>
    <p class="text-muted-foreground">See exactly what's included in each plan.</p>
</div>

<div class="overflow-x-auto">
<DataTable TData="FeatureRow" Data="_rows" ShowToolbar="false" ShowPagination="false">
    <Columns>
        <DataTableColumn TData="FeatureRow" TValue="string" Property="@(r => r.Feature)" Header="Feature" />
        <DataTableColumn TData="FeatureRow" TValue="string" Property="@(r => r.Free)" Header="Free">
            <CellTemplate Context="row">@PlanCell(row.Free)</CellTemplate>
        </DataTableColumn>
        <DataTableColumn TData="FeatureRow" TValue="string" Property="@(r => r.Pro)" Header="Pro">
            <CellTemplate Context="row">@PlanCell(row.Pro)</CellTemplate>
        </DataTableColumn>
        <DataTableColumn TData="FeatureRow" TValue="string" Property="@(r => r.Enterprise)" Header="Enterprise">
            <CellTemplate Context="row">@PlanCell(row.Enterprise)</CellTemplate>
        </DataTableColumn>
    </Columns>
</DataTable>
</div>

<div class="flex justify-center gap-4 mt-8">
    <Button Variant="ButtonVariant.Outline">Get started free</Button>
    <Button>Start Pro trial</Button>
</div>

@code {
    record FeatureRow(string Feature, string Free, string Pro, string Enterprise);

    RenderFragment PlanCell(string val) => val == "✓"
        ? @<LucideIcon Name="check" Size="16" Class="text-primary" />
        : val == "–"
            ? @<span class="text-muted-foreground">–</span>
            : @<span>@val</span>;

    readonly List<FeatureRow> _rows = new()
    {
        new("Projects",         "3",       "Unlimited", "Unlimited"),
        new("Storage",          "1 GB",    "50 GB",     "Unlimited"),
        new("Team members",     "1",       "10",        "Unlimited"),
        new("Analytics",        "Basic",   "Advanced",  "Advanced"),
        new("Custom domains",   "–",       "✓",         "✓"),
        new("Priority support", "–",       "✓",         "✓"),
        new("SSO / SAML",       "–",       "–",         "✓"),
        new("SLA guarantee",    "–",       "–",         "✓"),
        new("Custom contracts", "–",       "–",         "✓"),
    };
}





@* ----------- anaalytics-dashboard ------- *@

<div class="@container w-full bg-background p-6">
    <div class="grid grid-cols-1 @sm:grid-cols-2 @3xl:grid-cols-4 gap-4">
        @foreach (var stat in _stats)
        {
            <Card>
                <CardHeader Class="flex flex-row items-center justify-between pb-2">
                    <CardTitle Class="text-sm font-medium text-muted-foreground">@stat.Label</CardTitle>
                    <div class="size-8 rounded-md bg-muted flex items-center justify-center shrink-0">
                        <LucideIcon Name="@stat.Icon" Size="15" Class="text-foreground/60" />
                    </div>
                </CardHeader>
                <CardContent>
                    <div class="text-2xl font-bold tracking-tight">@stat.Value</div>
                    <div class="flex items-center gap-1 mt-1">
                        <Badge Variant="@(stat.IsPositive ? BadgeVariant.Default : BadgeVariant.Destructive)"
                               Class="text-[10px] px-1.5 py-0 font-mono">
                            @(stat.IsPositive ? "+" : "")@stat.Delta
                        </Badge>
                        <span class="text-xs text-muted-foreground">from last month</span>
                    </div>
                </CardContent>
            </Card>
        }
    </div>
</div>

@code {
    private record Stat(string Label, string Icon, string Value, string Delta, bool IsPositive);

    private static readonly Stat[] _stats =
    [
        new("Total Revenue",    "dollar-sign",    "$45,231.89", "20.1%", true),
        new("Active Users",     "users",          "+2,350",     "180.1%", true),
        new("New Signups",      "user-plus",      "+12,234",    "19%",    true),
        new("Active Now",       "activity",       "+573",       "201",    true),
    ];
}


@* ---------------------- call to action banner ------------------------ *@
<div class="@container w-full min-h-[200px] bg-muted/40 flex items-center justify-center p-10">
    <div class="text-center space-y-5 max-w-xl">
        <h2 class="text-2xl @md:text-3xl font-bold tracking-tight">Start building for free today</h2>
        <p class="text-muted-foreground">Join thousands of teams already using Acme to ship faster and smarter. No credit card required.</p>
        <div class="flex flex-wrap justify-center gap-3">
            <Button Size="ButtonSize.Large">Get started free</Button>
            <Button Variant="ButtonVariant.Outline" Size="ButtonSize.Large">Schedule a demo</Button>
        </div>
    </div>
</div>

@* *************** Testimonials grid ************ *@

<div class="@container text-center mb-10">
    <h2 class="text-2xl font-bold mb-2">Loved by teams worldwide</h2>
    <p class="text-muted-foreground">Don't just take our word for it.</p>
</div>

<div class="grid grid-cols-1 @md:grid-cols-2 @3xl:grid-cols-3 gap-6">
    @foreach (var t in _testimonials)
    {
        <Card Class="flex flex-col">
            <CardContent Class="pt-6 flex-1">
                <div class="flex gap-0.5 mb-4">
                    @for (int i = 0; i < 5; i++)
                    {
                        <LucideIcon Name="star" Size="16" Class="text-yellow-400 fill-yellow-400" />
                    }
                </div>
                <p class="text-sm text-muted-foreground leading-relaxed mb-4">"@t.Quote"</p>
            </CardContent>
            <CardFooter Class="border-t pt-4">
                <div class="flex items-center gap-3">
                    <Avatar Class="size-9">
                        <AvatarFallback>@t.Initials</AvatarFallback>
                    </Avatar>
                    <div>
                        <p class="text-sm font-semibold">@t.Name</p>
                        <p class="text-xs text-muted-foreground">@t.Role</p>
                    </div>
                </div>
            </CardFooter>
        </Card>
    }
</div>

@code {
    record Testimonial(string Initials, string Name, string Role, string Quote);

    readonly List<Testimonial> _testimonials = new()
    {
        new("SC", "Sarah Chen", "CTO at Streamline Inc.", "This platform transformed how our team collaborates. We ship twice as fast with half the friction."),
        new("MR", "Marcus Reid", "Head of Product, Horizon", "The analytics alone paid for the subscription within the first week. Exceptional value."),
        new("AL", "Anya Lim", "Founder, Pixel Studio", "Switching from our old stack was painless. The onboarding experience is best-in-class."),
        new("JW", "James Walker", "Engineering Lead, Forge", "Rock solid reliability. We've been on the platform for 18 months and have never had a major outage."),
        new("PM", "Priya Mehta", "VP Engineering, CloudHQ", "The team collaboration features are exactly what we needed. Highly recommend for remote teams."),
        new("TN", "Tom Nguyen", "Co-founder, Launchpad", "Customer support is genuinely world class. They resolved our issue in under 10 minutes."),
    };
}
```

