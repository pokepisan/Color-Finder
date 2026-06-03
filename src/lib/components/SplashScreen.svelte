<script lang="ts">
  import { onMount, onDestroy } from 'svelte';
  import { base } from '$app/paths';

  export let onDone: () => void;

  let visible    = true;
  let fadeOut    = false;
  let brushGroup: SVGGElement;
  let panelsGrp:  SVGGElement;
  let revealRect: SVGRectElement;
  let pickupRing: SVGCircleElement;
  let titleEl:    HTMLDivElement;
  let rafId:      number;

  // Smooth angle — bristles lead throughout.
  // ENTER departs toward (152,282) from (195,422): atan2(-140,-43) ≈ -107°
  // (bristles pointing upper-left as brush rises from below)
  let smoothAngle = -107;

  // ── Bezier helpers ────────────────────────────────────────────────────────
  function qb(a: number, b: number, c: number, t: number) {
    const m = 1 - t; return m * m * a + 2 * m * t * b + t * t * c;
  }
  function qbAngle(
    x0: number, y0: number, cx: number, cy: number,
    x1: number, y1: number, t: number
  ) {
    const m  = 1 - t;
    const dx = 2 * m * (cx - x0) + 2 * t * (x1 - cx);
    const dy = 2 * m * (cy - y0) + 2 * t * (y1 - cy);
    // No +180: bristles face the direction of travel (not handle-leads)
    return Math.atan2(dy, dx) * (180 / Math.PI);
  }
  function easeInOut(t: number) {
    return t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2;
  }
  function easeOut3(t: number) { return 1 - Math.pow(1 - t, 3); }

  // ── Phases ────────────────────────────────────────────────────────────────
  // 0 ENTER : brush rises from below via bezier arc to the blue area
  // 1 TAP   : dips straight down onto the blue
  // 2 ARC   : bezier arc lifts over the gap to the canvas
  // 3 PAINT : sweeps across with gentle wrist oscillation
  const ENTER = { x0:195, y0:422, cx:152, cy:282, x1:125, y1:162, dur:740 };
  const TAP   = { x0:125, y0:162, x1:125, y1:182, dur:185 };
  const ARC   = { x0:125, y0:182, cx:292, cy:90,  x1:456, y1:172, dur:640 };
  const PAINT = { x0:456, y0:172, x1:674, y1:172, dur:900 };

  onMount(() => {
    panelsGrp.style.opacity  = '0';
    revealRect.setAttribute('width', '0');
    pickupRing.style.opacity = '0';

    // Brush starts off-screen below, hidden, at ENTER departure angle
    brushGroup.style.transform = `translate(${(195/700*100).toFixed(3)}%,${(422/390*100).toFixed(3)}%) rotate(-107deg)`;
    brushGroup.style.opacity = '0';

    // Panels fade in immediately; brush fades in a beat later
    setTimeout(() => {
      panelsGrp.style.transition = 'opacity 0.4s ease';
      panelsGrp.style.opacity    = '1';
    }, 60);
    setTimeout(() => {
      brushGroup.style.transition = 'opacity 0.22s ease';
      brushGroup.style.opacity    = '1';
    }, 300);

    let phase      = 0;   // 0=enter, 1=tap, 2=arc, 3=paint
    let phaseStart = performance.now() + 460;
    let pickupDone = false;

    function tick(now: number) {
      if (phase > 3) { finish(); return; }

      let x = 0, y = 0, targetAngle = smoothAngle;

      if (phase === 0) {
        // ── ENTER: rise from below along bezier arc ────────────────────────
        const raw = Math.min((now - phaseStart) / ENTER.dur, 1);
        const e   = easeInOut(raw);
        x = qb(ENTER.x0, ENTER.cx, ENTER.x1, e);
        y = qb(ENTER.y0, ENTER.cy, ENTER.y1, e);
        targetAngle = qbAngle(ENTER.x0, ENTER.y0, ENTER.cx, ENTER.cy, ENTER.x1, ENTER.y1, e);
        if (raw >= 1) { phase = 1; phaseStart = now; }

      } else if (phase === 1) {
        // ── TAP: dip straight down onto the blue ──────────────────────────
        const raw = Math.min((now - phaseStart) / TAP.dur, 1);
        const e   = easeOut3(raw);
        x = TAP.x0;
        y = TAP.y0 + (TAP.y1 - TAP.y0) * e;
        targetAngle = smoothAngle; // hold angle while tapping

        if (!pickupDone && raw > 0.88) {
          pickupDone = true;
          pickupRing.style.transition = 'opacity 0.07s ease';
          pickupRing.style.opacity    = '1';
          setTimeout(() => {
            pickupRing.style.transition = 'opacity 0.42s ease';
            pickupRing.style.opacity    = '0';
          }, 115);
        }
        if (raw >= 1) { phase = 2; phaseStart = now; }

      } else if (phase === 2) {
        // ── ARC: bezier lift over gap to canvas ───────────────────────────
        const raw = Math.min((now - phaseStart) / ARC.dur, 1);
        const e   = easeInOut(raw);
        x = qb(ARC.x0, ARC.cx, ARC.x1, e);
        y = qb(ARC.y0, ARC.cy, ARC.y1, e);
        targetAngle = qbAngle(ARC.x0, ARC.y0, ARC.cx, ARC.cy, ARC.x1, ARC.y1, e);
        if (raw >= 1) { phase = 3; phaseStart = now; }

      } else {
        // ── PAINT: sweep across canvas with wrist oscillation ─────────────
        const raw = Math.min((now - phaseStart) / PAINT.dur, 1);
        const e   = easeInOut(raw);
        x = PAINT.x0 + (PAINT.x1 - PAINT.x0) * e;
        const osc  = Math.sin(e * Math.PI * 2.5) * 5;
        y = PAINT.y0 + osc;
        // Neutral paint angle: 0° = bristles right. Slight oscillation tip.
        const oscDeriv = Math.cos(e * Math.PI * 2.5) * 10;
        targetAngle = oscDeriv;
        revealRect.setAttribute('width', `${Math.max(0, x - 455)}`);
        if (raw >= 1) { phase = 4; phaseStart = now; }
      }

      // Smooth angle blending on every frame — organic, never snaps
      smoothAngle = smoothAngle * 0.78 + targetAngle * 0.22;
      brushGroup.style.transform =
        `translate(${(x/700*100).toFixed(3)}%,${(y/390*100).toFixed(3)}%) rotate(${smoothAngle.toFixed(2)}deg)`;

      rafId = requestAnimationFrame(tick);
    }

    function finish() {
      revealRect.setAttribute('width', '220');
      brushGroup.style.transition = 'opacity 0.3s ease';
      brushGroup.style.opacity    = '0';
      setTimeout(() => titleEl?.classList.add('show'), 360);
      setTimeout(() => {
        fadeOut = true;
        setTimeout(() => { visible = false; onDone(); }, 480);
      }, 2150);
    }

    rafId = requestAnimationFrame(tick);
  });

  onDestroy(() => { if (rafId) cancelAnimationFrame(rafId); });
</script>

{#if visible}
  <div class="splash" class:fade-out={fadeOut}>
    <div class="scene">
      <svg viewBox="0 0 700 390" xmlns="http://www.w3.org/2000/svg" style="overflow:visible">
        <defs>
          <clipPath id="cc">
            <rect bind:this={revealRect} x="455" y="60" width="0" height="240"/>
          </clipPath>
          <filter id="psf" x="-12%" y="-12%" width="130%" height="130%">
            <feDropShadow dx="0" dy="5" stdDeviation="10" flood-color="rgba(0,0,0,0.6)"/>
          </filter>
          <filter id="bsf" x="-20%" y="-50%" width="150%" height="200%">
            <feDropShadow dx="1" dy="2" stdDeviation="3" flood-color="rgba(0,0,0,0.45)"/>
          </filter>
          <filter id="pgf" x="-60%" y="-60%" width="220%" height="220%">
            <feGaussianBlur stdDeviation="5"/>
          </filter>
          <!-- brush handle wood gradient -->
          <linearGradient id="wood" x1="0%" y1="0%" x2="0%" y2="100%">
            <stop offset="0%"   stop-color="#E0B870"/>
            <stop offset="40%"  stop-color="#C89848"/>
            <stop offset="100%" stop-color="#B07830"/>
          </linearGradient>
        </defs>

        <!-- ══ PANELS ═══════════════════════════════════════════════════════ -->
        <g bind:this={panelsGrp}>

          <!-- ── LEFT: actual portrait image ──────────────────────────────── -->
          <g filter="url(#psf)">
            <image href="{base}/portrait-ref.jpg"
                   x="25" y="60" width="220" height="240"
                   preserveAspectRatio="xMidYMid slice"/>
            <rect x="25" y="60" width="220" height="240" fill="none"
                  stroke="rgba(255,255,255,0.15)" stroke-width="1.5" rx="3"/>
          </g>
          <text x="135" y="318" text-anchor="middle" fill="#3b5fa0"
                font-size="10" font-family="'Segoe UI',system-ui,sans-serif"
                letter-spacing="2.5">REFERENCE IMAGE</text>

          <!-- ── RIGHT: blank canvas → same portrait revealed by brush ──── -->
          <g filter="url(#psf)">
            <!-- blank canvas background (visible before brush arrives) -->
            <rect x="455" y="60" width="220" height="240" fill="#EDE8DF" rx="3"/>
            <!-- blue paint revealed left→right as brush sweeps -->
            <g clip-path="url(#cc)">
              <rect x="455" y="60" width="220" height="240" fill="#2060D8"/>
              <!-- subtle paint-stroke texture on top -->
              <rect x="455" y="60"  width="220" height="38"  fill="#2568DA" opacity="0.6"/>
              <rect x="455" y="108" width="220" height="32"  fill="#1A54C4" opacity="0.5"/>
              <rect x="455" y="152" width="220" height="36"  fill="#2568DA" opacity="0.55"/>
              <rect x="455" y="200" width="220" height="100" fill="#1A54C4" opacity="0.45"/>
            </g>
            <rect x="455" y="60" width="220" height="240" fill="none"
                  stroke="rgba(255,255,255,0.15)" stroke-width="1.5" rx="3"/>
          </g>
          <text x="565" y="318" text-anchor="middle" fill="#3b5fa0"
                font-size="10" font-family="'Segoe UI',system-ui,sans-serif"
                letter-spacing="2.5">CANVAS</text>

          <!-- gap arrow -->
          <line x1="252" y1="180" x2="448" y2="180"
                stroke="rgba(255,255,255,0.06)" stroke-width="1" stroke-dasharray="5,5"/>
          <text x="350" y="185" text-anchor="middle"
                fill="rgba(255,255,255,0.10)" font-size="20"
                font-family="'Segoe UI',system-ui,sans-serif">→</text>
        </g>

        <!-- pickup glow — blue, centred on the blue hair band of the portrait -->
        <circle bind:this={pickupRing}
                cx="125" cy="180" r="28"
                fill="rgba(32,96,216,0.28)" stroke="#2060D8"
                stroke-width="2.5" filter="url(#pgf)" opacity="0"/>

        <!-- ══ ROUND CARTOON BRUSH (matches brush.png reference) ══════════ -->
        <!--
          Round bristles at origin (tip), handle tapers to a point in -x.
          Cartoon style: thick black outlines, light wood handle, silver ferrule.
        -->
        <g bind:this={brushGroup} class="brush-gpu">

          <!-- bristles: cream/off-white rounded fan, tip at origin -->
          <path d="M 5,0
                   C  3,-2  -3,-7 -12,-11
                   C -17,-13 -24,-14 -24,-14
                   L -24,14
                   C -24,14 -17,13 -12,11
                   C  -3,7   3,2   5,0 Z"
                fill="#EEE8D8" stroke="#1A1A1A" stroke-width="2.5" stroke-linejoin="round"/>
          <!-- bristle hair lines -->
          <line x1="-22" y1="-13" x2="4"  y2="-0.8" stroke="#D5CDB8" stroke-width="1.2"/>
          <line x1="-22" y1=" -8" x2="4.5" y2="-0.4" stroke="#D5CDB8" stroke-width="1.0"/>
          <line x1="-22" y1=" -3" x2="5"  y2="0"     stroke="#D5CDB8" stroke-width="0.8"/>
          <line x1="-22" y1="  3" x2="5"  y2="0"     stroke="#D5CDB8" stroke-width="0.8"/>
          <line x1="-22" y1="  8" x2="4.5" y2="0.4"  stroke="#D5CDB8" stroke-width="1.0"/>
          <line x1="-22" y1=" 13" x2="4"  y2="0.8"   stroke="#D5CDB8" stroke-width="1.2"/>
          <!-- wet paint at tip — blue, picked up from the portrait -->
          <ellipse cx="3" cy="0" rx="3.5" ry="2" fill="rgba(32,96,216,0.7)"/>

          <!-- ferrule: silver-gray with black outline -->
          <rect x="-36" y="-13" width="14" height="26" rx="2"
                fill="#C0C8D0" stroke="#1A1A1A" stroke-width="2.5"/>
          <rect x="-35" y="-12" width="12" height="7" rx="0"
                fill="rgba(255,255,255,0.45)"/>
          <rect x="-35" y="5"   width="12" height="6" rx="0"
                fill="rgba(0,0,0,0.18)"/>

          <!-- handle: light wood, tapers to a sharp point at far end -->
          <path d="M -36,-11 L -112,-4 L -116,0 L -112,4 L -36,11 Z"
                fill="url(#wood)" stroke="#1A1A1A" stroke-width="2.5" stroke-linejoin="round"/>
          <!-- handle top highlight -->
          <path d="M -36,-10 L -111,-3.5 L -111,-1 L -36,-6 Z"
                fill="rgba(255,255,255,0.28)"/>
          <!-- handle bottom shadow -->
          <path d="M -36,6 L -111,3 L -111,3.8 L -36,10 Z"
                fill="rgba(0,0,0,0.18)"/>
        </g>
      </svg>

      <div class="title-area" bind:this={titleEl}>
        <span class="app-title">Color Finder</span>
        <span class="app-sub">color analysis tool</span>
      </div>
    </div>
  </div>
{/if}

<style>
  .splash {
    position: fixed;
    inset: 0;
    z-index: 9999;
    background: #040d1a;
    display: flex;
    align-items: center;
    justify-content: center;
    opacity: 1;
    transition: opacity 0.48s ease;
  }
  .splash.fade-out { opacity: 0; pointer-events: none; }

  .scene {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 4px;
    width: min(680px, 94vw);
  }
  .scene svg { width: 100%; height: auto; display: block; }

  .title-area {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 7px;
    opacity: 0;
    transform: translateY(14px);
    transition: opacity 0.55s ease, transform 0.55s ease;
  }
  :global(.title-area.show) { opacity: 1; transform: translateY(0); }

  .app-title {
    font-size: 28px;
    font-weight: 800;
    letter-spacing: 0.045em;
    background: linear-gradient(120deg, #a78bfa 0%, #60a5fa 100%);
    -webkit-background-clip: text;
    -webkit-text-fill-color: transparent;
    background-clip: text;
    filter: drop-shadow(0 0 18px rgba(167,139,250,0.4));
    font-family: 'Segoe UI Variable', 'Segoe UI', system-ui, sans-serif;
  }
  :global(.brush-gpu) {
    filter: drop-shadow(1px 2px 3px rgba(0,0,0,0.45));
    transform-box: view-box;
    transform-origin: 0% 0%;
    will-change: transform, opacity;
  }

  .app-sub {
    font-size: 10.5px;
    color: #3b5fa0;
    letter-spacing: 0.2em;
    text-transform: uppercase;
    font-family: 'Segoe UI Variable', 'Segoe UI', system-ui, sans-serif;
  }
</style>
