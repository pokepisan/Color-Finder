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
        new("Yellow Ochre",            "Earth",        204, 153,  51),
        new("Gold Ochre",              "Earth",        196, 154,  60),
        new("Raw Sienna",              "Earth",        181, 133,  65),

        // Oranges
        new("Cadmium Orange",          "Orange",       236,  97,  28),
        new("Burnt Sienna",            "Earth",        150,  73,  38),
        new("Transparent Red Oxide",   "Earth",        165,  68,  46),

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

        // Greens
        new("Viridian",                "Green",         64, 130, 109),
        new("Phthalo Green",           "Green",         18, 107,  57),
        new("Cadmium Green",           "Green",          0, 107,  60),
        new("Sap Green",               "Green",         80, 125,  42),
        new("Chromium Oxide Green",    "Green",        100, 130,  90),
        new("Terre Verte",             "Green",        144, 168, 127),
        new("Cobalt Green",            "Green",         60, 144, 108),
        new("Hooker's Green",          "Green",         40, 100,  65),

        // Browns / Earth
        new("Burnt Umber",             "Earth",        138,  87,  53),
        new("Raw Umber",               "Earth",        115,  97,  72),
        new("Van Dyke Brown",          "Earth",         93,  67,  46),

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
