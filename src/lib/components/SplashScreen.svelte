<script lang="ts">
  import { onMount, onDestroy } from 'svelte';

  export let onDone: () => void;

  let visible = true;
  let fadeOut = false;
  let svgEl: SVGSVGElement;
  let strokePath: SVGPathElement;
  let shadowPath: SVGPathElement;
  let highlightPath: SVGPathElement;
  let brushGroup: SVGGElement;
  let titleEl: HTMLDivElement;
  let rafId: number;

  const DRAW_MS = 1900;

  function easeInOutCubic(t: number) {
    return t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2;
  }

  onMount(() => {
    const len = strokePath.getTotalLength();

    [strokePath, shadowPath, highlightPath].forEach(p => {
      p.style.strokeDasharray = `${len}`;
      p.style.strokeDashoffset = `${len}`;
    });
    brushGroup.style.opacity = '0';

    const t0 = performance.now();

    function tick(now: number) {
      const raw = Math.min((now - t0) / DRAW_MS, 1);
      const e   = easeInOutCubic(raw);
      const off = len * (1 - e);

      strokePath.style.strokeDashoffset    = `${off}`;
      shadowPath.style.strokeDashoffset    = `${off}`;
      highlightPath.style.strokeDashoffset = `${off}`;

      if (e > 0.008) {
        const d      = e * len;
        const pt     = strokePath.getPointAtLength(d);
        const behind = strokePath.getPointAtLength(Math.max(0, d - 5));
        const ahead  = strokePath.getPointAtLength(Math.min(len, d + 5));
        const angle  = Math.atan2(ahead.y - behind.y, ahead.x - behind.x) * (180 / Math.PI);
        brushGroup.setAttribute('transform', `translate(${pt.x},${pt.y}) rotate(${angle + 180})`);
        brushGroup.style.opacity = '1';
      }

      if (raw < 1) {
        rafId = requestAnimationFrame(tick);
      } else {
        brushGroup.style.transition = 'opacity 0.25s ease';
        brushGroup.style.opacity = '0';

        const sps    = Array.from(svgEl.querySelectorAll<SVGElement>('.sp'));
        const delays = [0, 110, 200, 90, 280, 170];
        sps.forEach((el, i) => setTimeout(() => el.classList.add('pop'), delays[i]));

        setTimeout(() => titleEl?.classList.add('show'), 400);

        setTimeout(() => {
          fadeOut = true;
          setTimeout(() => { visible = false; onDone(); }, 480);
        }, 2150);
      }
    }

    rafId = requestAnimationFrame(tick);
  });

  onDestroy(() => { if (rafId) cancelAnimationFrame(rafId); });
</script>

{#if visible}
  <div class="splash" class:fade-out={fadeOut}>
    <div class="scene">
      <svg bind:this={svgEl} viewBox="0 0 660 370" xmlns="http://www.w3.org/2000/svg">
        <defs>
          <!-- ── STROKE GRADIENTS (Procreate reference) ───────────────────── -->
          <!-- Main: violet → blue → cyan → teal → lime → yellow → orange → pink -->
          <linearGradient id="sg" x1="3%" y1="97%" x2="97%" y2="3%">
            <stop offset="0%"    stop-color="#5B21B6"/>
            <stop offset="14%"   stop-color="#2563EB"/>
            <stop offset="28%"   stop-color="#0EA5E9"/>
            <stop offset="42%"   stop-color="#14B8A6"/>
            <stop offset="57%"   stop-color="#84CC16"/>
            <stop offset="71%"   stop-color="#EAB308"/>
            <stop offset="85%"   stop-color="#F97316"/>
            <stop offset="100%"  stop-color="#EC4899"/>
          </linearGradient>

          <!-- Shadow (darkened spectrum) -->
          <linearGradient id="shg" x1="3%" y1="97%" x2="97%" y2="3%">
            <stop offset="0%"    stop-color="#2E1065"/>
            <stop offset="33%"   stop-color="#0C4A6E"/>
            <stop offset="66%"   stop-color="#365314"/>
            <stop offset="100%"  stop-color="#831843"/>
          </linearGradient>

          <!-- Specular highlight (pastel spectrum) -->
          <linearGradient id="hg" x1="3%" y1="97%" x2="97%" y2="3%">
            <stop offset="0%"    stop-color="rgba(196,181,253,0.6)"/>
            <stop offset="25%"   stop-color="rgba(147,197,253,0.45)"/>
            <stop offset="50%"   stop-color="rgba(167,243,208,0.38)"/>
            <stop offset="75%"   stop-color="rgba(253,224,71,0.32)"/>
            <stop offset="100%"  stop-color="rgba(249,168,212,0.28)"/>
          </linearGradient>

          <!-- ── BRUSH GRADIENTS ─────────────────────────────────────────── -->
          <!-- Ferrule: copper / rose-gold, top→bottom -->
          <linearGradient id="b-copper" x1="0%" y1="0%" x2="0%" y2="100%">
            <stop offset="0%"    stop-color="#E8A878"/>
            <stop offset="18%"   stop-color="#C8784A"/>
            <stop offset="50%"   stop-color="#A85E30"/>
            <stop offset="80%"   stop-color="#8A4A20"/>
            <stop offset="100%"  stop-color="#5C2E0E"/>
          </linearGradient>

          <!-- Handle: dark forest green plastic, top→bottom -->
          <linearGradient id="b-green" x1="0%" y1="0%" x2="0%" y2="100%">
            <stop offset="0%"    stop-color="#2E7D32"/>
            <stop offset="22%"   stop-color="#1B5E20"/>
            <stop offset="55%"   stop-color="#145214"/>
            <stop offset="100%"  stop-color="#0A3010"/>
          </linearGradient>

          <!-- ── FILTERS ─────────────────────────────────────────────────── -->
          <!-- Outer glow on stroke -->
          <filter id="glow" x="-30%" y="-30%" width="160%" height="160%">
            <feGaussianBlur in="SourceGraphic" stdDeviation="9" result="b"/>
            <feMerge>
              <feMergeNode in="b"/>
              <feMergeNode in="SourceGraphic"/>
            </feMerge>
          </filter>

          <!-- Deep blurred shadow -->
          <filter id="shf" x="-30%" y="-30%" width="160%" height="160%">
            <feGaussianBlur in="SourceGraphic" stdDeviation="16"/>
          </filter>

          <!-- Soft blur on highlight -->
          <filter id="hf" x="-10%" y="-10%" width="120%" height="120%">
            <feGaussianBlur in="SourceGraphic" stdDeviation="2.5"/>
          </filter>

          <!-- Drop shadow on brush -->
          <filter id="brush-shadow" x="-20%" y="-40%" width="140%" height="180%">
            <feDropShadow dx="2" dy="3" stdDeviation="3" flood-color="rgba(0,0,0,0.5)"/>
          </filter>
        </defs>

        <!-- ════ DEPTH SHADOW (offset + blurred) ════════════════════════════ -->
        <path
          bind:this={shadowPath}
          d="M 118,328 C 96,155 298,38 492,108 C 568,138 598,93 622,68"
          fill="none" stroke="url(#shg)" stroke-width="44"
          stroke-linecap="round" opacity="0.5"
          filter="url(#shf)" transform="translate(8,13)"
        />

        <!-- ════ MAIN RAINBOW STROKE ═════════════════════════════════════════ -->
        <path
          bind:this={strokePath}
          d="M 118,328 C 96,155 298,38 492,108 C 568,138 598,93 622,68"
          fill="none" stroke="url(#sg)" stroke-width="28"
          stroke-linecap="round" filter="url(#glow)"
        />

        <!-- ════ SPECULAR HIGHLIGHT ══════════════════════════════════════════ -->
        <path
          bind:this={highlightPath}
          d="M 118,328 C 96,155 298,38 492,108 C 568,138 598,93 622,68"
          fill="none" stroke="url(#hg)" stroke-width="8"
          stroke-linecap="round" opacity="0.65"
          filter="url(#hf)"
        />

        <!-- ════ PAINT SPLATTER at stroke tip ════════════════════════════════ -->
        <circle class="sp" cx="636" cy="58"  r="17" fill="#F472B6"/>
        <circle class="sp" cx="618" cy="42"  r="11" fill="#EC4899"/>
        <circle class="sp" cx="652" cy="44"  r="8"  fill="#FBBF24"/>
        <circle class="sp" cx="640" cy="28"  r="6"  fill="#FDA4AF"/>
        <circle class="sp" cx="657" cy="72"  r="8"  fill="#F97316"/>
        <circle class="sp" cx="624" cy="24"  r="4"  fill="#FDE68A"/>

        <!-- ════ PAINTBRUSH (flat house-painter style) ══════════════════════ -->
        <!--
          Origin (0,0) = ferrule / bristle junction.
          +x = bristles (trail behind stroke after angle+180).
          -x = handle   (leads forward in direction of travel).
        -->
        <g bind:this={brushGroup} style="opacity:0" filter="url(#brush-shadow)">

          <!-- ── BRISTLES ─────────────────────────────────────────────────── -->
          <!-- Wide flat bristle mass — slightly wider at tips than at ferrule -->
          <path d="M 0,-20 C 10,-21 22,-22 32,-21 L 32,21 C 22,22 10,21 0,20 Z"
                fill="#1C1A18"/>
          <!-- Bristle body fill (dark charcoal) -->
          <rect x="0" y="-20" width="30" height="40" fill="#18161400"/>
          <!-- Top-face sheen (light catches the flat bristle face) -->
          <path d="M 0,-20 L 32,-21 L 32,-14 L 0,-13 Z"
                fill="rgba(255,255,255,0.07)"/>
          <!-- Individual bristle hair lines (run from ferrule→tip) -->
          <line x1="1" y1="-19" x2="31" y2="-20" stroke="#2E2B27" stroke-width="1.1"/>
          <line x1="1" y1="-16" x2="31" y2="-17" stroke="#2E2B27" stroke-width="0.9"/>
          <line x1="1" y1="-13" x2="31" y2="-14" stroke="#2E2B27" stroke-width="0.8"/>
          <line x1="1" y1="-10" x2="31" y2="-10" stroke="#2E2B27" stroke-width="0.8"/>
          <line x1="1" y1=" -7" x2="31" y2=" -7" stroke="#2E2B27" stroke-width="0.7"/>
          <line x1="1" y1=" -4" x2="31" y2=" -4" stroke="#262422" stroke-width="0.6"/>
          <line x1="1" y1="  0" x2="31" y2="  0" stroke="#262422" stroke-width="0.6"/>
          <line x1="1" y1="  4" x2="31" y2="  4" stroke="#2E2B27" stroke-width="0.7"/>
          <line x1="1" y1="  7" x2="31" y2="  7" stroke="#2E2B27" stroke-width="0.8"/>
          <line x1="1" y1=" 10" x2="31" y2=" 10" stroke="#2E2B27" stroke-width="0.8"/>
          <line x1="1" y1=" 13" x2="31" y2=" 14" stroke="#2E2B27" stroke-width="0.8"/>
          <line x1="1" y1=" 16" x2="31" y2=" 17" stroke="#2E2B27" stroke-width="0.9"/>
          <line x1="1" y1=" 19" x2="31" y2=" 20" stroke="#2E2B27" stroke-width="1.1"/>
          <!-- Paint wet-gloss at bristle tips (pink = stroke end colour) -->
          <rect x="29" y="-21" width="4" height="42" rx="1"
                fill="rgba(236,72,153,0.28)"/>
          <rect x="30" y="-19" width="2" height="38" rx="1"
                fill="rgba(255,200,230,0.18)"/>

          <!-- ── FERRULE (copper / rose-gold) ────────────────────────────── -->
          <!-- Main copper body -->
          <rect x="-15" y="-21" width="17" height="42" rx="1.5"
                fill="url(#b-copper)"/>
          <!-- Top copper highlight band -->
          <rect x="-14" y="-20" width="15" height="6" rx="0"
                fill="rgba(255,220,170,0.45)"/>
          <!-- Lower shadow band -->
          <rect x="-14" y=" 14" width="15" height="6" rx="0"
                fill="rgba(0,0,0,0.32)"/>
          <!-- Horizontal mid-seam -->
          <rect x="-14" y=" -1" width="15" height="2" rx="0"
                fill="rgba(0,0,0,0.20)"/>
          <!-- Rivets (two, matching reference) -->
          <circle cx="-6" cy="-11" r="2.3" fill="#C07840"
                  stroke="rgba(0,0,0,0.35)" stroke-width="0.6"/>
          <circle cx="-6" cy=" 11" r="2.3" fill="#C07840"
                  stroke="rgba(0,0,0,0.35)" stroke-width="0.6"/>
          <!-- Rivet highlight dots -->
          <circle cx="-7" cy="-12" r="0.8" fill="rgba(255,220,160,0.7)"/>
          <circle cx="-7" cy=" 10" r="0.8" fill="rgba(255,220,160,0.7)"/>
          <!-- Left depth edge -->
          <rect x="-15" y="-21" width="4" height="42" rx="1"
                fill="rgba(0,0,0,0.22)"/>

          <!-- ── HANDLE (dark green plastic) ─────────────────────────────── -->
          <!-- Main body — tapers from ±9 at ferrule to ±5 at end -->
          <path d="M -15,-9  C -45,-9.5 -85,-7.5 -112,-5.5
                   L -118,-5 L -118,5
                   L -112,5.5 C -85,7.5 -45,9.5 -15,9 Z"
                fill="url(#b-green)"/>
          <!-- Top plastic sheen (cylinder highlight) -->
          <path d="M -15,-8  C -45,-8.5 -85,-6.5 -112,-4.5
                   L -112,-1.5 C -85,-4   -45,-6   -15,-5.5 Z"
                fill="rgba(255,255,255,0.13)"/>
          <!-- Bottom shadow -->
          <path d="M -15,5.5 C -45,8   -85,6.5  -112,4.5
                   L -112,5.5 L -118,5 L -118,5
                   L -112,5.5 C -85,7.5 -45,9.5 -15,9 Z"
                fill="rgba(0,0,0,0.22)"/>
          <!-- Subtle plastic surface lines -->
          <line x1="-20" y1="-8"  x2="-110" y2="-4.5" stroke="rgba(0,0,0,0.06)" stroke-width="1.2"/>
          <line x1="-20" y1=" 5"  x2="-110" y2=" 3"   stroke="rgba(0,0,0,0.05)" stroke-width="0.9"/>
          <!-- Hang hole at handle end -->
          <ellipse cx="-114" cy="0" rx="4"   ry="5"   fill="#041008"/>
          <ellipse cx="-113" cy="-1" rx="1.8" ry="2"  fill="rgba(255,255,255,0.10)"/>
        </g>
      </svg>

      <!-- Title fades in after brush finishes -->
      <div class="title-area" bind:this={titleEl}>
        <span class="app-title">Color Finder</span>
        <span class="app-sub">pigment analysis tool</span>
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
  .splash.fade-out {
    opacity: 0;
    pointer-events: none;
  }

  .scene {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 4px;
    width: min(620px, 94vw);
  }
  .scene svg {
    width: 100%;
    height: auto;
    display: block;
    overflow: visible;
  }

  /* Splatter pop-in */
  :global(.sp) {
    transform-box: fill-box;
    transform-origin: center;
    transform: scale(0);
    opacity: 0;
    transition:
      transform 0.3s cubic-bezier(0.34, 1.56, 0.64, 1),
      opacity   0.2s ease;
  }
  :global(.sp.pop) {
    transform: scale(1);
    opacity: 1;
  }

  /* Title slide-up */
  .title-area {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 7px;
    opacity: 0;
    transform: translateY(14px);
    transition: opacity 0.55s ease, transform 0.55s ease;
  }
  :global(.title-area.show) {
    opacity: 1;
    transform: translateY(0);
  }

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
  .app-sub {
    font-size: 10.5px;
    color: #3b5fa0;
    letter-spacing: 0.2em;
    text-transform: uppercase;
    font-family: 'Segoe UI Variable', 'Segoe UI', system-ui, sans-serif;
  }
</style>
