export type Lab = { l: number; a: number; b: number };

function linearize(v: number): number {
  return v > 0.04045 ? Math.pow((v + 0.055) / 1.055, 2.4) : v / 12.92;
}

function labF(t: number): number {
  return t > 0.008856 ? Math.cbrt(t) : 7.787 * t + 16 / 116;
}

// sRGB channel (0–255) → linear-light reflectance (0–1).
// Exposed for the subtractive pigment-mixing model, which must operate on
// linear reflectance, not gamma-encoded sRGB.
export function srgbToLinear(v: number): number {
  return linearize(v / 255);
}

// Linear-light RGB (0–1 each) → CIE L*a*b* (D65).
export function linearRgbToLab(rl: number, gl: number, bl: number): Lab {
  const x = labF((rl * 0.4124564 + gl * 0.3575761 + bl * 0.1804375) / 0.95047);
  const y = labF((rl * 0.2126729 + gl * 0.7151522 + bl * 0.0721750) / 1.00000);
  const z = labF((rl * 0.0193339 + gl * 0.1191920 + bl * 0.9503041) / 1.08883);
  return { l: 116 * y - 16, a: 500 * (x - y), b: 200 * (y - z) };
}

export function rgbToLab(r: number, g: number, b: number): Lab {
  return linearRgbToLab(srgbToLinear(r), srgbToLinear(g), srgbToLinear(b));
}

// CIE76 ΔE — plain Euclidean distance in Lab.
// Fast but not perceptually uniform; used only for k-means clustering, where
// centroids are computed as Lab means and speed across thousands of pixels matters.
export function deltaE(a: Lab, b: Lab): number {
  const dL = a.l - b.l;
  const dA = a.a - b.a;
  const dB = a.b - b.b;
  return Math.sqrt(dL * dL + dA * dA + dB * dB);
}

// CIEDE2000 ΔE — the current CIE standard for perceptual colour difference.
// Corrects CIE76's well-known errors in the blue region and for saturated
// colours by adding lightness/chroma/hue weighting and a chroma–hue rotation
// term. Used for final pigment identification, where perceptual accuracy of the
// match matters most. Reference: Sharma, Wu & Dalal (2005).
export function deltaE2000(s: Lab, t: Lab): number {
  const kL = 1, kC = 1, kH = 1;

  const C1 = Math.hypot(s.a, s.b);
  const C2 = Math.hypot(t.a, t.b);
  const Cbar = (C1 + C2) / 2;

  const Cbar7 = Math.pow(Cbar, 7);
  const G = 0.5 * (1 - Math.sqrt(Cbar7 / (Cbar7 + 6103515625))); // 25^7 = 6103515625

  const a1p = (1 + G) * s.a;
  const a2p = (1 + G) * t.a;
  const C1p = Math.hypot(a1p, s.b);
  const C2p = Math.hypot(a2p, t.b);

  const h1p = hueAngle(s.b, a1p);
  const h2p = hueAngle(t.b, a2p);

  const dLp = t.l - s.l;
  const dCp = C2p - C1p;

  let dhp = 0;
  if (C1p * C2p !== 0) {
    const diff = h2p - h1p;
    if (Math.abs(diff) <= 180) dhp = diff;
    else if (diff > 180) dhp = diff - 360;
    else dhp = diff + 360;
  }
  const dHp = 2 * Math.sqrt(C1p * C2p) * Math.sin((dhp * Math.PI) / 360);

  const Lbarp = (s.l + t.l) / 2;
  const Cbarp = (C1p + C2p) / 2;

  let hbarp = h1p + h2p;
  if (C1p * C2p !== 0) {
    if (Math.abs(h1p - h2p) > 180) hbarp = h1p + h2p < 360 ? hbarp + 360 : hbarp - 360;
    hbarp /= 2;
  }

  const T =
    1 -
    0.17 * Math.cos(deg2rad(hbarp - 30)) +
    0.24 * Math.cos(deg2rad(2 * hbarp)) +
    0.32 * Math.cos(deg2rad(3 * hbarp + 6)) -
    0.20 * Math.cos(deg2rad(4 * hbarp - 63));

  const dTheta = 30 * Math.exp(-(((hbarp - 275) / 25) ** 2));
  const Cbarp7 = Math.pow(Cbarp, 7);
  const Rc = 2 * Math.sqrt(Cbarp7 / (Cbarp7 + 6103515625));
  const Rt = -Rc * Math.sin(deg2rad(2 * dTheta));

  const Lbarp50 = (Lbarp - 50) ** 2;
  const Sl = 1 + (0.015 * Lbarp50) / Math.sqrt(20 + Lbarp50);
  const Sc = 1 + 0.045 * Cbarp;
  const Sh = 1 + 0.015 * Cbarp * T;

  const lTerm = dLp / (kL * Sl);
  const cTerm = dCp / (kC * Sc);
  const hTerm = dHp / (kH * Sh);

  return Math.sqrt(lTerm * lTerm + cTerm * cTerm + hTerm * hTerm + Rt * cTerm * hTerm);
}

function hueAngle(b: number, ap: number): number {
  if (ap === 0 && b === 0) return 0;
  const deg = (Math.atan2(b, ap) * 180) / Math.PI;
  return deg >= 0 ? deg : deg + 360;
}

function deg2rad(d: number): number {
  return (d * Math.PI) / 180;
}
