import { rgbToLab, linearRgbToLab, srgbToLinear, deltaE, deltaE2000, type Lab } from './colorUtils';
import { PIGMENTS, WHITE_IDX, BLACK_IDX, type OilPigment } from './pigments';

export interface ColorMatch {
  pigment: OilPigment;
  percentage: number;
  displayColor: string;
}

const PIGMENT_LAB: Lab[] = PIGMENTS.map(p => rgbToLab(p.r, p.g, p.b));

// Log of each pigment's linear-light reflectance, per channel. Working in
// log-reflectance lets us model paint mixing as a *subtractive* process: a
// blend's reflectance is the weighted geometric mean of its components'
// reflectances, i.e. a linear interpolation of absorbance (−ln reflectance).
// This is the single-constant Kubelka–Munk approximation usable when only RGB
// is known, and unlike linear-Lab averaging it reproduces real pigment
// behaviour (e.g. blue + yellow → green, not grey).
const REFL_EPS = 1e-4;
const PIGMENT_LN_REFL = PIGMENTS.map(p => ({
  r: Math.log(Math.max(srgbToLinear(p.r), REFL_EPS)),
  g: Math.log(Math.max(srgbToLinear(p.g), REFL_EPS)),
  b: Math.log(Math.max(srgbToLinear(p.b), REFL_EPS)),
}));

// Below this chroma the color is treated as neutral (White ↔ Black by L only)
const NEUTRAL_CHROMA = 8;
// Don't emit a secondary pigment if its share would be below this fraction
const MIN_MIX_FRAC = 0.06;
// Resolution of the mixing-ratio search along each pigment pair (≈6 %/step).
const MIX_STEPS = 16;

// Lab colour of mixing pigments i and j with weight f on j (0 = all i, 1 = all j),
// under the subtractive model described above.
function subtractiveMixLab(i: number, j: number, f: number): Lab {
  const a = PIGMENT_LN_REFL[i];
  const b = PIGMENT_LN_REFL[j];
  const rl = Math.exp(a.r + f * (b.r - a.r));
  const gl = Math.exp(a.g + f * (b.g - a.g));
  const bl = Math.exp(a.b + f * (b.b - a.b));
  return linearRgbToLab(rl, gl, bl);
}

// Single-pixel precise colour pick — skips k-means, analyses one exact RGB value.
export function analyzeColor(r: number, g: number, b: number): ColorMatch[] {
  const lab = rgbToLab(r, g, b);
  const hex = rgbToHex(r, g, b);
  const raw: ColorMatch[] = [];
  pigmentDecomp(raw, lab, 100, hex);

  const merged = new Map<string, ColorMatch>();
  for (const m of raw) {
    if (merged.has(m.pigment.name)) {
      const ex = merged.get(m.pigment.name)!;
      merged.set(m.pigment.name, { ...ex, percentage: ex.percentage + m.percentage });
    } else {
      merged.set(m.pigment.name, { ...m });
    }
  }

  const result = [...merged.values()].sort((a, b) => b.percentage - a.percentage);
  const sum = result.reduce((s, m) => s + m.percentage, 0);
  if (sum !== 0 && sum !== 100) result[0].percentage += 100 - sum;
  return result;
}

export function analyze(
  imageData: ImageData,
  sx: number, sy: number, sw: number, sh: number,
  numColors: number
): ColorMatch[] {
  const pixels = samplePixels(imageData, sx, sy, sw, sh, 6000);
  if (pixels.length === 0) return [];

  const labs = pixels.map(([r, g, b]) => rgbToLab(r, g, b));
  const { centroids, assignments } = kmeans(labs, numColors);

  const clusterR = new Float64Array(numColors);
  const clusterG = new Float64Array(numColors);
  const clusterB = new Float64Array(numColors);
  const counts   = new Int32Array(numColors);

  for (let i = 0; i < assignments.length; i++) {
    const c = assignments[i];
    counts[c]++;
    clusterR[c] += pixels[i][0];
    clusterG[c] += pixels[i][1];
    clusterB[c] += pixels[i][2];
  }

  const total = pixels.length;
  const raw: ColorMatch[] = [];

  for (let k = 0; k < numColors; k++) {
    if (counts[k] === 0) continue;
    const percentage = Math.round((counts[k] / total) * 100);
    const dr = Math.round(clusterR[k] / counts[k]);
    const dg = Math.round(clusterG[k] / counts[k]);
    const db = Math.round(clusterB[k] / counts[k]);
    pigmentDecomp(raw, centroids[k], percentage, rgbToHex(dr, dg, db));
  }

  // Merge identical pigments across clusters
  const merged = new Map<string, ColorMatch>();
  for (const m of raw) {
    if (merged.has(m.pigment.name)) {
      const ex = merged.get(m.pigment.name)!;
      merged.set(m.pigment.name, { ...ex, percentage: ex.percentage + m.percentage });
    } else {
      merged.set(m.pigment.name, { ...m });
    }
  }

  const result = [...merged.values()].sort((a, b) => b.percentage - a.percentage);
  const sum = result.reduce((s, m) => s + m.percentage, 0);
  if (sum !== 0 && sum !== 100) result[0].percentage += 100 - sum;

  return result;
}

// ── Pigment decomposition ─────────────────────────────────────────────────────
//
// For each cluster centroid we search every pigment singly and every pigment
// PAIR, sampling the mixing ratio along the pair under the subtractive
// (Kubelka–Munk-style) mixing model, and keep the combination whose mixed colour
// is closest to the target in CIEDE2000. ΔE2000 is the perceptually-correct
// metric (Stockman / CIE cone fundamentals: matches happen at the cone level),
// and the subtractive model means a "mix" follows the real pigment path through
// colour space rather than a straight Lab line — so lightness no longer needs an
// ad-hoc down-weight, because darkening/lightening is achieved by mixing toward
// black/white as genuine palette members.

function pigmentDecomp(
  raw: ColorMatch[], centroid: Lab, percentage: number, displayColor: string
) {
  const chroma = Math.sqrt(centroid.a ** 2 + centroid.b ** 2);

  // Near-neutral: white ↔ black by lightness
  if (chroma < NEUTRAL_CHROMA) {
    const lW   = PIGMENT_LAB[WHITE_IDX].l;
    const lB   = PIGMENT_LAB[BLACK_IDX].l;
    const wFrac = clamp((centroid.l - lB) / (lW - lB), 0, 1);
    const wPct  = Math.round(percentage * wFrac);
    const bPct  = percentage - wPct;
    if (wPct > 0) raw.push({ pigment: PIGMENTS[WHITE_IDX], percentage: wPct, displayColor: pigmentHex(WHITE_IDX) });
    if (bPct > 0) raw.push({ pigment: PIGMENTS[BLACK_IDX], percentage: bPct, displayColor: pigmentHex(BLACK_IDX) });
    return;
  }

  const n = PIGMENTS.length;
  let bestDE = Infinity;
  let bestI  = 0;
  let bestJ  = -1;
  let bestF  = 0;

  // Single-pigment candidates
  for (let i = 0; i < n; i++) {
    const dE = deltaE2000(PIGMENT_LAB[i], centroid);
    if (dE < bestDE) { bestDE = dE; bestI = i; bestJ = -1; bestF = 0; }
  }

  // Two-pigment subtractive blends: sample the mixing ratio and keep the best.
  // The mix path is non-linear in Lab, so we scan it rather than projecting.
  for (let i = 0; i < n; i++) {
    for (let j = i + 1; j < n; j++) {
      for (let s = 1; s < MIX_STEPS; s++) {
        const f = s / MIX_STEPS;
        const dE = deltaE2000(subtractiveMixLab(i, j, f), centroid);
        if (dE < bestDE) { bestDE = dE; bestI = i; bestJ = j; bestF = f; }
      }
    }
  }

  // Emit result
  if (bestJ === -1 || bestF < MIN_MIX_FRAC) {
    raw.push({ pigment: PIGMENTS[bestI], percentage, displayColor });
    return;
  }
  if (1 - bestF < MIN_MIX_FRAC) {
    raw.push({ pigment: PIGMENTS[bestJ], percentage, displayColor });
    return;
  }

  const iPct = Math.round(percentage * (1 - bestF));
  const jPct = percentage - iPct;
  // Primary component gets the actual cluster colour so the user can compare
  if (iPct > 0) raw.push({ pigment: PIGMENTS[bestI], percentage: iPct, displayColor });
  if (jPct > 0) raw.push({ pigment: PIGMENTS[bestJ], percentage: jPct, displayColor: pigmentHex(bestJ) });
}

// ── K-means in Lab ────────────────────────────────────────────────────────────

function samplePixels(
  imageData: ImageData,
  sx: number, sy: number, sw: number, sh: number,
  maxSamples: number
): [number, number, number][] {
  const { data, width } = imageData;
  const step = Math.max(1, Math.round(Math.sqrt((sw * sh) / maxSamples)));
  const pixels: [number, number, number][] = [];
  for (let y = sy; y < sy + sh; y += step) {
    for (let x = sx; x < sx + sw; x += step) {
      const idx = (y * width + x) * 4;
      pixels.push([data[idx], data[idx + 1], data[idx + 2]]);
    }
  }
  return pixels;
}

function kmeans(points: Lab[], k: number): { centroids: Lab[]; assignments: number[] } {
  const centroids   = kMeansPlusPlus(points, k);
  const assignments = new Array<number>(points.length).fill(0);

  for (let iter = 0; iter < 60; iter++) {
    let changed = false;
    for (let i = 0; i < points.length; i++) {
      let best = 0, bestDist = Infinity;
      for (let c = 0; c < k; c++) {
        const d = deltaE(points[i], centroids[c]);
        if (d < bestDist) { bestDist = d; best = c; }
      }
      if (assignments[i] !== best) { assignments[i] = best; changed = true; }
    }
    if (!changed) break;

    const sumL = new Float64Array(k), sumA = new Float64Array(k), sumB = new Float64Array(k);
    const cnts = new Int32Array(k);
    for (let i = 0; i < points.length; i++) {
      const c = assignments[i];
      sumL[c] += points[i].l; sumA[c] += points[i].a; sumB[c] += points[i].b; cnts[c]++;
    }
    for (let c = 0; c < k; c++) {
      if (cnts[c] > 0) centroids[c] = { l: sumL[c] / cnts[c], a: sumA[c] / cnts[c], b: sumB[c] / cnts[c] };
    }
  }
  return { centroids, assignments };
}

function kMeansPlusPlus(points: Lab[], k: number): Lab[] {
  let seed = 42;
  const rand = () => { seed = (seed * 1664525 + 1013904223) & 0xffffffff; return (seed >>> 0) / 0xffffffff; };
  const centroids: Lab[] = [points[Math.floor(rand() * points.length)]];
  for (let c = 1; c < k; c++) {
    const dists = points.map(p => {
      let m = Infinity;
      for (let j = 0; j < c; j++) m = Math.min(m, deltaE(p, centroids[j]));
      return m * m;
    });
    const total = dists.reduce((s, d) => s + d, 0);
    let r = rand() * total, cumul = 0, idx = 0;
    for (let i = 0; i < dists.length; i++) { cumul += dists[i]; if (cumul >= r) { idx = i; break; } }
    centroids.push(points[idx]);
  }
  return centroids;
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function pigmentHex(idx: number): string {
  const p = PIGMENTS[idx];
  return rgbToHex(p.r, p.g, p.b);
}

function rgbToHex(r: number, g: number, b: number): string {
  return '#' + [r, g, b].map(v => v.toString(16).padStart(2, '0')).join('');
}

function clamp(v: number, min: number, max: number): number {
  return Math.max(min, Math.min(max, v));
}
