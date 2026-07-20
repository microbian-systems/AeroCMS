namespace Aero.Cms.Html;

/// <summary>Controls the main-axis direction of a flex container.</summary>
public enum CssFlexDirection
{
    /// <summary>Places items along the inline axis.</summary>
    Row,
    /// <summary>Places items along the reversed inline axis.</summary>
    RowReverse,
    /// <summary>Places items along the block axis.</summary>
    Column,
    /// <summary>Places items along the reversed block axis.</summary>
    ColumnReverse
}

/// <summary>Controls cross-axis alignment for flex and grid children.</summary>
public enum CssAlignment
{
    /// <summary>Aligns children at the cross-axis start.</summary>
    Start,
    /// <summary>Centers children on the cross axis.</summary>
    Center,
    /// <summary>Aligns children at the cross-axis end.</summary>
    End,
    /// <summary>Stretches eligible children across the cross axis.</summary>
    Stretch,
    /// <summary>Aligns children on their text baselines.</summary>
    Baseline
}

/// <summary>Controls distribution of flex or grid children on the main axis.</summary>
public enum CssJustification
{
    /// <summary>Packs children at the main-axis start.</summary>
    Start,
    /// <summary>Centers children on the main axis.</summary>
    Center,
    /// <summary>Packs children at the main-axis end.</summary>
    End,
    /// <summary>Distributes remaining space between children.</summary>
    SpaceBetween,
    /// <summary>Distributes space around each child.</summary>
    SpaceAround,
    /// <summary>Distributes equal space between and around children.</summary>
    SpaceEvenly
}
