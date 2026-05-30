export interface OilPigment {
  name: string;
  category: string;
  r: number;
  g: number;
  b: number;
}

// Standard oil painting palette — core pigments used by most painters.
// Removed: Viridian, Permanent Rose, Indigo, Naples Yellow, Cadmium Orange, Cerulean Blue.
export const PIGMENTS: OilPigment[] = [
  // White
  { name: "Titanium White",       category: "White",  r: 255, g: 253, b: 247 },
  // Yellows & Ochres
  { name: "Cadmium Yellow Light", category: "Yellow", r: 255, g: 230, b:   0 },
  { name: "Yellow Ochre",         category: "Earth",  r: 192, g: 148, b:  56 },
  { name: "Raw Sienna",           category: "Earth",  r: 168, g: 122, b:  58 },
  // Reds
  { name: "Burnt Sienna",         category: "Earth",  r: 143, g:  68, b:  38 },
  { name: "Cadmium Red Light",    category: "Red",    r: 225, g:  38, b:  44 },
  { name: "Alizarin Crimson",     category: "Red",    r: 150, g:  30, b:  49 },
  // Violets
  { name: "Dioxazine Purple",     category: "Violet", r:  96, g:  30, b: 133 },
  // Blues
  { name: "Ultramarine Blue",     category: "Blue",   r:  18, g:  10, b: 143 },
  { name: "Cobalt Blue",          category: "Blue",   r:   0, g:  71, b: 171 },
  { name: "Prussian Blue",        category: "Blue",   r:   0, g:  49, b:  83 },
  // Greens
  { name: "Sap Green",            category: "Green",  r:  58, g:  90, b:  28 },
  { name: "Chromium Oxide Green", category: "Green",  r:  72, g: 108, b:  68 },
  // Browns & Earth
  { name: "Burnt Umber",          category: "Earth",  r: 110, g:  62, b:  36 },
  { name: "Raw Umber",            category: "Earth",  r:  92, g:  78, b:  58 },
  // Dark / Black
  { name: "Payne's Gray",         category: "Gray",   r:  83, g:  97, b: 107 },
  { name: "Ivory Black",          category: "Black",  r:  38, g:  36, b:  34 },
];

export const WHITE_IDX = PIGMENTS.findIndex(p => p.name === "Titanium White");
export const BLACK_IDX = PIGMENTS.findIndex(p => p.name === "Ivory Black");
