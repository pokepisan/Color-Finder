export type Lab = { l: number; a: number; b: number };

function linearize(v: number): number {
  return v > 0.04045 ? Math.pow((v + 0.055) / 1.055, 2.4) : v / 12.92;
}

function labF(t: number): number {
  return t > 0.008856 ? Math.cbrt(t) : 7.787 * t + 16 / 116;
}

export function rgbToLab(r: number, g: number, b: number): Lab {
  const rl = linearize(r / 255);
  const gl = linearize(g / 255);
  const bl = linearize(b / 255);

  const x = labF((rl * 0.4124564 + gl * 0.3575761 + bl * 0.1804375) / 0.95047);
  const y = labF((rl * 0.2126729 + gl * 0.7151522 + bl * 0.0721750) / 1.00000);
  const z = labF((rl * 0.0193339 + gl * 0.1191920 + bl * 0.9503041) / 1.08883);

  return { l: 116 * y - 16, a: 500 * (x - y), b: 200 * (y - z) };
}

export function deltaE(a: Lab, b: Lab): number {
  const dL = a.l - b.l;
  const dA = a.a - b.a;
  const dB = a.b - b.b;
  return Math.sqrt(dL * dL + dA * dA + dB * dB);
}
