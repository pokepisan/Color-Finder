namespace Color_Finder;

public record OilPigment(string Name, string Category, byte R, byte G, byte B);

public static class PigmentDatabase
{
    public static readonly OilPigment[] Pigments =
    [
        // White
        new("Titanium White",       "White",   255, 253, 247),

        // Yellows & Ochres
        new("Cadmium Yellow Light", "Yellow",  255, 230,   0),
        new("Yellow Ochre",         "Earth",   192, 148,  56),
        new("Raw Sienna",           "Earth",   168, 122,  58),
        new("Naples Yellow",        "Yellow",  254, 223, 128),

        // Oranges
        new("Cadmium Orange",       "Orange",  236,  97,  28),
        new("Burnt Sienna",         "Earth",   143,  68,  38),

        // Reds
        new("Cadmium Red Light",    "Red",     225,  38,  44),
        new("Alizarin Crimson",     "Red",     150,  30,  49),
        new("Permanent Rose",       "Red",     230,  80, 120),

        // Violets
        new("Dioxazine Purple",     "Violet",   96,  30, 133),

        // Blues
        new("Ultramarine Blue",     "Blue",     18,  10, 143),
        new("Cobalt Blue",          "Blue",      0,  71, 171),
        new("Cerulean Blue",        "Blue",     42, 170, 238),
        new("Prussian Blue",        "Blue",      0,  49,  83),
        new("Indigo",               "Blue",     38,  28,  74),

        // Greens
        new("Viridian",             "Green",    35, 105,  83),
        new("Sap Green",            "Green",    58,  90,  28),
        new("Chromium Oxide Green", "Green",    72, 108,  68),

        // Browns & Earth
        new("Burnt Umber",          "Earth",   110,  62,  36),
        new("Raw Umber",            "Earth",    92,  78,  58),

        // Dark / Black
        new("Payne's Gray",         "Gray",     83,  97, 107),
        new("Ivory Black",          "Black",    38,  36,  34),
    ];
}
