namespace Color_Finder;

public record OilPigment(string Name, string Category, byte R, byte G, byte B);

public static class PigmentDatabase
{
    public static readonly OilPigment[] Pigments =
    [
        // Whites
        new("Titanium White",          "White",        255, 253, 247),
        new("Zinc White",              "White",        250, 252, 255),
        new("Flake White",             "White",        255, 248, 234),

        // Yellows
        new("Lemon Yellow",            "Yellow",       255, 244,  50),
        new("Cadmium Yellow Light",    "Yellow",       255, 230,   0),
        new("Cadmium Yellow",          "Yellow",       255, 200,   0),
        new("Cadmium Yellow Deep",     "Yellow",       255, 155,   0),
        new("Hansa Yellow",            "Yellow",       238, 218,  47),
        new("Indian Yellow",           "Yellow",       228, 186,  52),
        new("Naples Yellow",           "Yellow",       254, 223, 128),
        new("Yellow Ochre",            "Earth",        192, 148,  56),
        new("Gold Ochre",              "Earth",        184, 148,  62),
        new("Raw Sienna",              "Earth",        168, 122,  58),

        // Oranges
        new("Cadmium Orange",          "Orange",       236,  97,  28),
        new("Burnt Sienna",            "Earth",        143,  68,  38),
        new("Transparent Red Oxide",   "Earth",        158,  60,  40),

        // Reds
        new("Vermilion",               "Red",          227,  66,  52),
        new("Cadmium Red Light",       "Red",          225,  38,  44),
        new("Cadmium Red Deep",        "Red",          194,  28,  38),
        new("Alizarin Crimson",        "Red",          150,  30,  49),
        new("Quinacridone Red",        "Red",          196,  53,  71),
        new("Rose Madder",             "Red",          196,  43,  80),
        new("Permanent Rose",          "Red",          230,  80, 120),
        new("Quinacridone Magenta",    "Red",          220,  40, 100),
        new("Indian Red",              "Earth",        185,  92,  82),
        new("Venetian Red",            "Earth",        200,  78,  51),

        // Violets
        new("Dioxazine Purple",        "Violet",        96,  30, 133),
        new("Cobalt Violet",           "Violet",       168, 101, 168),
        new("Manganese Violet",        "Violet",       148,  76, 152),
        new("Quinacridone Violet",     "Violet",       155,  60, 125),

        // Blues
        new("Ultramarine Blue",        "Blue",          18,  10, 143),
        new("French Ultramarine",      "Blue",          72,  50, 168),
        new("Cobalt Blue",             "Blue",           0,  71, 171),
        new("Prussian Blue",           "Blue",           0,  49,  83),
        new("Phthalo Blue",            "Blue",           0,  32,  96),
        new("Cerulean Blue",           "Blue",          42, 170, 238),
        new("Manganese Blue",          "Blue",          55, 162, 194),
        new("Cobalt Teal",             "Blue/Green",     0, 167, 181),
        new("Indanthrone Blue",        "Blue",          30,  50, 100),

        // Greens — values based on spectrophotometer-measured mass tones
        new("Viridian",                "Green",         35, 105,  83),
        new("Phthalo Green",           "Green",         12,  75,  44),
        new("Cadmium Green",           "Green",          0,  98,  55),
        new("Sap Green",               "Green",         58,  90,  28),
        new("Olive Green",             "Green",         80,  92,  46),
        new("Chromium Oxide Green",    "Green",         72, 108,  68),
        new("Terre Verte",             "Green",        102, 122,  88),
        new("Cobalt Green",            "Green",         48, 128,  95),
        new("Hooker's Green",          "Green",         38,  74,  45),

        // Browns / Earth — updated to measured mass tones
        new("Burnt Umber",             "Earth",        110,  62,  36),
        new("Raw Umber",               "Earth",         92,  78,  58),
        new("Van Dyke Brown",          "Earth",         80,  55,  36),

        // Blacks / Grays
        new("Neutral Gray Light",      "Gray",         200, 200, 198),
        new("Neutral Gray",            "Gray",         140, 140, 138),
        new("Davy's Gray",             "Gray",          85,  93,  80),
        new("Payne's Gray",            "Gray",          83,  97, 107),
        new("Ivory Black",             "Black",         38,  36,  34),
        new("Lamp Black",              "Black",         28,  28,  26),
        new("Mars Black",              "Black",         42,  42,  44),
    ];
}
