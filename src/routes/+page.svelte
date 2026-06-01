<script lang="ts">
  import { onMount } from 'svelte';
  import { analyze, type ColorMatch } from '$lib/colorAnalyzer';
  import SplashScreen from '$lib/components/SplashScreen.svelte';

  let showSplash = true;

  // ── State ────────────────────────────────────────────────────────────────
  let image: HTMLImageElement | null = null;
  let imageData: ImageData | null = null;
  let numColors = 3;
  let matches: ColorMatch[] = [];
  let swatches: ColorMatch[][] = [];
  let status = 'Open an image to begin';
  let isAnalyzing = false;

  // Canvas refs
  let imageCanvas: HTMLCanvasElement;
  let magCanvas: HTMLCanvasElement;

  // Main selection in image-pixel coords
  let sel = { x: 0, y: 0, w: 0, h: 0 };
  let hasSelection = false;
  let dragging = false;
  let dragStart = { x: 0, y: 0 };

  // Magnifier sub-selection in image-pixel coords
  let magSel = { x: 0, y: 0, w: 0, h: 0 };
  let hasMagSelection = false;
  let magDragging = false;
  let magDragStart = { x: 0, y: 0 };
  let magIsDragging = false;
  const MAG_CLICK_PX = 20; // canvas pixels — keeps the indicator square regardless of aspect ratio

  // Zoom / pan
  let zoom = 1;
  let panX = 0;
  let panY = 0;
  let panning = false;
  let panStart = { x: 0, y: 0, px: 0, py: 0 };

  // Multi-touch tracking (pinch-zoom + two-finger pan)
  const activePointers = new Map<number, { x: number; y: number }>();
  let pinchDist = 0;
  let pinchMid  = { x: 0, y: 0 };

  // Click-to-pick
  let clickStartCanvas = { x: 0, y: 0 };
  let isDragging = false;
  const CLICK_THRESHOLD = 6;
  const CLICK_RADIUS = 6;

  // ── Resize state ─────────────────────────────────────────────────────────
  let rightColWidth = 310;
  let swatchesHeight = 160;

  let resizingRight = false;
  let resizeRightStartX = 0;
  let resizeRightStartWidth = 0;

  let resizingSwatches = false;
  let resizeSwatchesStartY = 0;
  let resizeSwatchesStartHeight = 0;

  function onRightHandleDown(e: PointerEvent) {
    resizingRight = true;
    resizeRightStartX = e.clientX;
    resizeRightStartWidth = rightColWidth;
    (e.currentTarget as HTMLElement).setPointerCapture(e.pointerId);
    e.preventDefault();
  }
  function onRightHandleMove(e: PointerEvent) {
    if (!resizingRight) return;
    rightColWidth = Math.max(220, Math.min(700, resizeRightStartWidth + (resizeRightStartX - e.clientX)));
  }
  function onRightHandleUp() { resizingRight = false; }

  function onSwatchHandleDown(e: PointerEvent) {
    resizingSwatches = true;
    resizeSwatchesStartY = e.clientY;
    resizeSwatchesStartHeight = swatchesHeight;
    (e.currentTarget as HTMLElement).setPointerCapture(e.pointerId);
    e.preventDefault();
  }
  function onSwatchHandleMove(e: PointerEvent) {
    if (!resizingSwatches) return;
    swatchesHeight = Math.max(60, Math.min(300, resizeSwatchesStartHeight + (resizeSwatchesStartY - e.clientY)));
  }
  function onSwatchHandleUp() { resizingSwatches = false; }

  $: if (typeof document !== 'undefined') {
    document.body.classList.toggle('resizing-col', resizingRight);
    document.body.classList.toggle('resizing-row', resizingSwatches);
  }

  // ── File open ────────────────────────────────────────────────────────────
  function openFile() {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = 'image/*';
    input.onchange = () => {
      const file = input.files?.[0];
      if (!file) return;
      const url = URL.createObjectURL(file);
      const img = new Image();
      img.onload = () => {
        image = img;
        zoom = 1; panX = 0; panY = 0;
        hasSelection = false;
        hasMagSelection = false;
        matches = [];
        status = 'Drag to select a region';
        extractImageData();
        drawImage();
        drawMag();
      };
      img.src = url;
    };
    input.click();
  }

  function extractImageData() {
    if (!image) return;
    const c = document.createElement('canvas');
    c.width = image.naturalWidth;
    c.height = image.naturalHeight;
    const ctx = c.getContext('2d')!;
    ctx.drawImage(image, 0, 0);
    imageData = ctx.getImageData(0, 0, c.width, c.height);
  }

  // ── Drawing helpers ──────────────────────────────────────────────────────

  function getDrawRect() {
    if (!image || !imageCanvas) return { x: 0, y: 0, w: 0, h: 0 };
    const iw = image.naturalWidth * zoom;
    const ih = image.naturalHeight * zoom;
    return {
      x: (imageCanvas.width - iw) / 2 + panX,
      y: (imageCanvas.height - ih) / 2 + panY,
      w: iw,
      h: ih,
    };
  }

  function canvasToImage(cx: number, cy: number) {
    const dr = getDrawRect();
    if (dr.w === 0) return { x: 0, y: 0 };
    return {
      x: Math.round(((cx - dr.x) / dr.w) * image!.naturalWidth),
      y: Math.round(((cy - dr.y) / dr.h) * image!.naturalHeight),
    };
  }

  function drawImage() {
    if (!imageCanvas) return;
    const ctx = imageCanvas.getContext('2d')!;
    ctx.clearRect(0, 0, imageCanvas.width, imageCanvas.height);

    if (!image) {
      ctx.fillStyle = '#030b14';
      ctx.fillRect(0, 0, imageCanvas.width, imageCanvas.height);
      ctx.fillStyle = '#0a2045';
      ctx.font = 'italic 14px "Segoe UI"';
      ctx.textAlign = 'center';
      ctx.fillText('Open an image to begin', imageCanvas.width / 2, imageCanvas.height / 2);
      return;
    }

    const dr = getDrawRect();
    ctx.drawImage(image, dr.x, dr.y, dr.w, dr.h);

    if (hasSelection) {
      const scaleX = dr.w / image.naturalWidth;
      const scaleY = dr.h / image.naturalHeight;
      const sx = dr.x + sel.x * scaleX;
      const sy = dr.y + sel.y * scaleY;
      const sw = sel.w * scaleX;
      const sh = sel.h * scaleY;

      ctx.strokeStyle = '#a78bfa';
      ctx.lineWidth = 2;
      ctx.setLineDash([6, 3]);
      ctx.strokeRect(sx, sy, sw, sh);
      ctx.setLineDash([]);
    }
  }

  function drawMag() {
    if (!magCanvas) return;
    const ctx = magCanvas.getContext('2d')!;
    ctx.clearRect(0, 0, magCanvas.width, magCanvas.height);

    if (!image || !hasSelection || sel.w < 2 || sel.h < 2) {
      ctx.fillStyle = '#030b14';
      ctx.fillRect(0, 0, magCanvas.width, magCanvas.height);
      ctx.fillStyle = '#0a2045';
      ctx.font = 'italic 13px "Segoe UI"';
      ctx.textAlign = 'center';
      ctx.fillText('Selection preview', magCanvas.width / 2, magCanvas.height / 2);
      return;
    }

    ctx.imageSmoothingEnabled = false;
    ctx.drawImage(image, sel.x, sel.y, sel.w, sel.h, 0, 0, magCanvas.width, magCanvas.height);

    if (hasMagSelection) {
      const scaleX = magCanvas.width / sel.w;
      const scaleY = magCanvas.height / sel.h;
      const mx = (magSel.x - sel.x) * scaleX;
      const my = (magSel.y - sel.y) * scaleY;
      const mw = magSel.w * scaleX;
      const mh = magSel.h * scaleY;

      ctx.strokeStyle = '#34d399';
      ctx.lineWidth = 2;
      ctx.setLineDash([6, 3]);
      ctx.strokeRect(mx, my, mw, mh);
      ctx.setLineDash([]);
    }
  }

  // ── Main canvas mouse events ──────────────────────────────────────────────

  function onImagePointerDown(e: PointerEvent) {
    if (!image) return;
    imageCanvas.setPointerCapture(e.pointerId);
    activePointers.set(e.pointerId, { x: e.clientX, y: e.clientY });

    if (activePointers.size >= 2) {
      // Second finger down → switch to pinch/pan mode
      dragging = false; isDragging = false; panning = false;
      const pts = [...activePointers.values()];
      pinchDist = Math.hypot(pts[1].x - pts[0].x, pts[1].y - pts[0].y);
      pinchMid  = { x: (pts[0].x + pts[1].x) / 2, y: (pts[0].y + pts[1].y) / 2 };
      return;
    }

    const rect = imageCanvas.getBoundingClientRect();
    const cx = (e.clientX - rect.left) * (imageCanvas.width / rect.width);
    const cy = (e.clientY - rect.top) * (imageCanvas.height / rect.height);

    // Middle-click or Alt+click → pan (desktop)
    if (e.button === 1 || (e.button === 0 && e.altKey)) {
      panning = true;
      panStart = { x: e.clientX, y: e.clientY, px: panX, py: panY };
      e.preventDefault();
      return;
    }

    dragging = true;
    isDragging = false;
    clickStartCanvas = { x: cx, y: cy };
    dragStart = canvasToImage(cx, cy);
  }

  function onImagePointerMove(e: PointerEvent) {
    if (!image) return;
    const prev = activePointers.get(e.pointerId);
    activePointers.set(e.pointerId, { x: e.clientX, y: e.clientY });

    if (activePointers.size >= 2 && prev) {
      // Pinch-zoom + two-finger pan
      const pts = [...activePointers.values()];
      const newDist = Math.hypot(pts[1].x - pts[0].x, pts[1].y - pts[0].y);
      const newMid  = { x: (pts[0].x + pts[1].x) / 2, y: (pts[0].y + pts[1].y) / 2 };
      if (pinchDist > 0) {
        zoom = Math.max(0.1, Math.min(20, zoom * (newDist / pinchDist)));
        panX += newMid.x - pinchMid.x;
        panY += newMid.y - pinchMid.y;
        drawImage();
      }
      pinchDist = newDist;
      pinchMid  = newMid;
      return;
    }

    const rect = imageCanvas.getBoundingClientRect();
    const cx = (e.clientX - rect.left) * (imageCanvas.width / rect.width);
    const cy = (e.clientY - rect.top) * (imageCanvas.height / rect.height);

    if (panning) {
      panX = panStart.px + (e.clientX - panStart.x);
      panY = panStart.py + (e.clientY - panStart.y);
      drawImage();
      return;
    }

    if (dragging) {
      const moved = Math.hypot(cx - clickStartCanvas.x, cy - clickStartCanvas.y);
      if (!isDragging && moved >= CLICK_THRESHOLD) {
        isDragging = true;
        hasSelection = false;
        hasMagSelection = false;
        matches = [];
      }
      if (isDragging) {
        const imgPt = canvasToImage(cx, cy);
        sel = {
          x: Math.max(0, Math.min(dragStart.x, imgPt.x)),
          y: Math.max(0, Math.min(dragStart.y, imgPt.y)),
          w: Math.min(Math.abs(imgPt.x - dragStart.x), image.naturalWidth),
          h: Math.min(Math.abs(imgPt.y - dragStart.y), image.naturalHeight),
        };
        hasSelection = sel.w > 4 && sel.h > 4;
        drawImage();
        if (hasSelection) drawMag();
      }
    }
  }

  function onImagePointerUp(e: PointerEvent) {
    activePointers.delete(e.pointerId);
    if (activePointers.size < 2) pinchDist = 0;

    const wasDragging = dragging;
    dragging = false;
    panning = false;

    if (!wasDragging) return;

    const rect = imageCanvas.getBoundingClientRect();
    const cx = (e.clientX - rect.left) * (imageCanvas.width / rect.width);
    const cy = (e.clientY - rect.top) * (imageCanvas.height / rect.height);

    if (!isDragging && image) {
      const imgPt = canvasToImage(cx, cy);
      sel = {
        x: Math.max(0, imgPt.x - CLICK_RADIUS),
        y: Math.max(0, imgPt.y - CLICK_RADIUS),
        w: Math.min(CLICK_RADIUS * 2, image.naturalWidth  - Math.max(0, imgPt.x - CLICK_RADIUS)),
        h: Math.min(CLICK_RADIUS * 2, image.naturalHeight - Math.max(0, imgPt.y - CLICK_RADIUS)),
      };
      hasSelection = true;
      hasMagSelection = false;
      matches = [];
      drawImage();
      drawMag();
      runAnalyze(sel.x, sel.y, sel.w, sel.h);
    } else if (hasSelection) {
      runAnalyze(sel.x, sel.y, sel.w, sel.h);
    }
  }

  function onImageWheel(e: WheelEvent) {
    if (!image) return;
    e.preventDefault();
    const delta = e.deltaY > 0 ? 0.9 : 1.1;
    zoom = Math.max(0.1, Math.min(20, zoom * delta));
    drawImage();
  }

  // ── Magnifier canvas mouse events ────────────────────────────────────────

  function magToImage(mx: number, my: number) {
    return {
      x: Math.round(sel.x + (mx / magCanvas.width) * sel.w),
      y: Math.round(sel.y + (my / magCanvas.height) * sel.h),
    };
  }

  function onMagPointerDown(e: PointerEvent) {
    if (!image || !hasSelection) return;
    magCanvas.setPointerCapture(e.pointerId);
    const rect = magCanvas.getBoundingClientRect();
    const mx = (e.clientX - rect.left) * (magCanvas.width / rect.width);
    const my = (e.clientY - rect.top) * (magCanvas.height / rect.height);
    magDragging = true;
    magIsDragging = false;
    magDragStart = { x: mx, y: my };
    hasMagSelection = false;
    drawMag();
  }

  function onMagPointerMove(e: PointerEvent) {
    if (!magDragging) return;
    const rect = magCanvas.getBoundingClientRect();
    const mx = (e.clientX - rect.left) * (magCanvas.width / rect.width);
    const my = (e.clientY - rect.top) * (magCanvas.height / rect.height);

    const moved = Math.hypot(mx - magDragStart.x, my - magDragStart.y);
    if (!magIsDragging && moved >= CLICK_THRESHOLD) magIsDragging = true;

    if (magIsDragging) {
      const s0 = magToImage(magDragStart.x, magDragStart.y);
      const s1 = magToImage(mx, my);
      magSel = {
        x: Math.min(s0.x, s1.x),
        y: Math.min(s0.y, s1.y),
        w: Math.abs(s1.x - s0.x),
        h: Math.abs(s1.y - s0.y),
      };
      hasMagSelection = magSel.w > 2 && magSel.h > 2;
      drawMag();
    }
  }

  function onMagPointerUp(e: PointerEvent) {
    magDragging = false;

    if (!magIsDragging && image) {
      const rect = magCanvas.getBoundingClientRect();
      const mx = (e.clientX - rect.left) * (magCanvas.width / rect.width);
      const my = (e.clientY - rect.top) * (magCanvas.height / rect.height);
      const s0 = magToImage(Math.max(0, mx - MAG_CLICK_PX), Math.max(0, my - MAG_CLICK_PX));
      const s1 = magToImage(Math.min(magCanvas.width, mx + MAG_CLICK_PX), Math.min(magCanvas.height, my + MAG_CLICK_PX));
      magSel = {
        x: Math.max(sel.x, s0.x),
        y: Math.max(sel.y, s0.y),
        w: s1.x - Math.max(sel.x, s0.x),
        h: s1.y - Math.max(sel.y, s0.y),
      };
      hasMagSelection = true;
      drawMag();
      runAnalyze(magSel.x, magSel.y, magSel.w, magSel.h);
    } else if (hasMagSelection) {
      runAnalyze(magSel.x, magSel.y, magSel.w, magSel.h);
    }
  }

  // ── Analyze ──────────────────────────────────────────────────────────────

  async function runAnalyze(x: number, y: number, w: number, h: number) {
    if (!imageData || w <= 2 || h <= 2) return;
    isAnalyzing = true;
    status = 'Analyzing…';
    await new Promise(r => setTimeout(r, 10));
    try {
      matches = analyze(imageData, x, y, w, h, numColors);
      status = `Found ${matches.length} dominant pigment${matches.length !== 1 ? 's' : ''}`;
    } catch (err) {
      status = `Analysis failed: ${err instanceof Error ? err.message : String(err)}`;
      console.error(err);
    }
    isAnalyzing = false;
  }

  let reAnalyzeTimer: ReturnType<typeof setTimeout> | null = null;
  $: triggerReAnalyze(numColors);
  function triggerReAnalyze(_n: number) {
    if (!imageData || (!hasSelection && !hasMagSelection)) return;
    if (reAnalyzeTimer) clearTimeout(reAnalyzeTimer);
    reAnalyzeTimer = setTimeout(() => {
      if (hasMagSelection) runAnalyze(magSel.x, magSel.y, magSel.w, magSel.h);
      else runAnalyze(sel.x, sel.y, sel.w, sel.h);
    }, 300);
  }

  function saveSwatch() {
    if (matches.length === 0) return;
    swatches = [[...matches], ...swatches].slice(0, 8);
    status = 'Swatch saved!';
  }

  // ── Resize canvases ───────────────────────────────────────────────────────

  function resizeCanvas(canvas: HTMLCanvasElement) {
    if (!canvas) return;
    const rect = canvas.getBoundingClientRect();
    if (canvas.width !== rect.width || canvas.height !== rect.height) {
      canvas.width = rect.width;
      canvas.height = rect.height;
    }
  }

  onMount(() => {
    const observer = new ResizeObserver(() => {
      resizeCanvas(imageCanvas);
      resizeCanvas(magCanvas);
      drawImage();
      drawMag();
    });
    observer.observe(imageCanvas);
    observer.observe(magCanvas);
    drawImage();
    drawMag();
    return () => observer.disconnect();
  });

  $: if (imageCanvas) { drawImage(); }
  $: if (magCanvas) { drawMag(); }
</script>

<svelte:window />

{#if showSplash}
  <SplashScreen onDone={() => (showSplash = false)} />
{/if}

<div class="app">
  <!-- Toolbar -->
  <header class="toolbar">
    <span class="title">Color Finder</span>
    <div class="accent-line"></div>

    <button class="btn btn-blue" on:click={openFile}>Open Image</button>

    <label class="colors-label">Colors:
      <input class="spinner" type="number" min="2" max="6" bind:value={numColors} />
    </label>

    <button
      class="btn btn-purple"
      disabled={matches.length === 0}
      on:click={saveSwatch}
    >
      Save Swatch
    </button>

    <span class="status">{status}</span>
  </header>

  <!-- Main layout -->
  <div class="main">
    <!-- Left: image panel -->
    <div class="image-pane">
      <canvas
        bind:this={imageCanvas}
        class="panel-canvas"
        style="cursor: crosshair; touch-action: none"
        on:pointerdown={onImagePointerDown}
        on:pointermove={onImagePointerMove}
        on:pointerup={onImagePointerUp}
        on:wheel={onImageWheel}
      ></canvas>
      {#if image}
        <div class="zoom-hint">Scroll to zoom · Alt+drag to pan</div>
      {/if}
    </div>

    <!-- Right column (resizable) -->
    <div class="right-col" style="width: {rightColWidth}px">
      <!-- Drag handle on left edge -->
      <div
        class="resize-handle-col"
        class:active={resizingRight}
        on:pointerdown={onRightHandleDown}
        on:pointermove={onRightHandleMove}
        on:pointerup={onRightHandleUp}
      ></div>

      <!-- Magnifier -->
      <div class="mag-pane">
        <div class="panel-header">
          <span class="panel-header-dot"></span>
          Selection
        </div>
        <div class="panel-canvas-wrap">
          <canvas
            bind:this={magCanvas}
            class="panel-canvas"
            style="{hasSelection ? 'cursor: crosshair' : 'cursor: default'}; touch-action: none"
            on:pointerdown={onMagPointerDown}
            on:pointermove={onMagPointerMove}
            on:pointerup={onMagPointerUp}
          ></canvas>
        </div>
        {#if hasSelection}
          <div class="zoom-hint">Click to pick · Drag to refine</div>
        {/if}
      </div>

      <!-- Results -->
      <div class="results-pane">
        <div class="panel-header">
          <span class="panel-header-dot accent"></span>
          Analysis
        </div>
        <div class="results-scroll">
          {#if isAnalyzing}
            <p class="hint">Analyzing…</p>
          {:else if matches.length === 0}
            <p class="hint">Select a region to analyze</p>
          {:else}
            <div class="results-list">
              {#each matches as m}
                <div class="match-row">
                  <div class="swatch-dot" style="background:{m.displayColor}"></div>
                  <div class="match-info">
                    <div class="match-header">
                      <span class="pigment-name">{m.pigment.name}</span>
                      <span class="pigment-cat">{m.pigment.category}</span>
                      <span class="pigment-pct">{m.percentage}%</span>
                    </div>
                    <div class="bar-track">
                      <div class="bar-fill" style="width:{m.percentage}%; background:{m.displayColor}"></div>
                    </div>
                  </div>
                  <div class="pigment-dot" style="background:rgb({m.pigment.r},{m.pigment.g},{m.pigment.b})"></div>
                </div>
              {/each}
            </div>
          {/if}
        </div>
      </div>
    </div>
  </div>

  <!-- Swatches (resizable) -->
  {#if swatches.length > 0}
    <div class="swatches-bar" style="height: {swatchesHeight}px">
      <!-- Drag handle on top edge -->
      <div
        class="resize-handle-row"
        class:active={resizingSwatches}
        on:pointerdown={onSwatchHandleDown}
        on:pointermove={onSwatchHandleMove}
        on:pointerup={onSwatchHandleUp}
      ></div>

      <div class="swatches-scroll">
        {#each swatches as sw}
          <div class="swatch-card">
            <div class="swatch-strips">
              {#each sw as m}
                <div
                  class="swatch-strip"
                  style="flex:{m.percentage}; background:{m.displayColor}"
                  title="{m.pigment.name} {m.percentage}%"
                ></div>
              {/each}
            </div>
            <div class="swatch-labels">
              {#each sw.slice(0, 3) as m}
                <span class="swatch-label">{m.pigment.name.split(' ').slice(-1)[0]} {m.percentage}%</span>
              {/each}
            </div>
          </div>
        {/each}
      </div>
    </div>
  {/if}
</div>

<style>
  :global(*) { box-sizing: border-box; margin: 0; padding: 0; }
  :global(body) {
    background: #040d1a;
    color: #dde1f5;
    font-family: 'Segoe UI Variable', 'Segoe UI', system-ui, sans-serif;
    overflow: hidden;
  }
  :global(::-webkit-scrollbar) { width: 4px; height: 4px; }
  :global(::-webkit-scrollbar-track) { background: transparent; }
  :global(::-webkit-scrollbar-thumb) { background: #0d2040; border-radius: 10px; }
  :global(::-webkit-scrollbar-thumb:hover) { background: #1a3a6a; }

  /* Cursor lock during resize */
  :global(body.resizing-col) { cursor: col-resize !important; user-select: none; }
  :global(body.resizing-col *) { cursor: col-resize !important; }
  :global(body.resizing-row) { cursor: row-resize !important; user-select: none; }
  :global(body.resizing-row *) { cursor: row-resize !important; }

  .app {
    display: flex;
    flex-direction: column;
    height: 100vh;
    background: #040d1a;
  }

  /* ── Toolbar ──────────────────────────────────────────────────────────── */
  .toolbar {
    position: relative;
    display: flex;
    align-items: center;
    gap: 10px;
    padding: 0 16px;
    height: 54px;
    background: #061528;
    border-bottom: 1px solid rgba(30, 120, 255, 0.30);
    flex-shrink: 0;
    z-index: 10;
  }

  .title {
    font-size: 14px;
    font-weight: 700;
    letter-spacing: 0.015em;
    background: linear-gradient(120deg, #a78bfa 0%, #60a5fa 100%);
    -webkit-background-clip: text;
    -webkit-text-fill-color: transparent;
    background-clip: text;
    white-space: nowrap;
    margin-right: 2px;
  }

  .accent-line {
    position: static;
    width: 1px;
    height: 22px;
    background: rgba(255, 255, 255, 0.08);
    flex-shrink: 0;
    margin: 0 2px;
  }

  .btn {
    padding: 6px 15px;
    border: none;
    border-radius: 8px;
    font-size: 12px;
    font-weight: 600;
    font-family: inherit;
    cursor: pointer;
    color: #fff;
    transition: transform 0.12s ease, box-shadow 0.12s ease, opacity 0.12s;
    white-space: nowrap;
    letter-spacing: 0.01em;
  }
  .btn:hover:not(:disabled) { transform: translateY(-1px); }
  .btn:active:not(:disabled) { transform: translateY(0); }
  .btn:disabled { opacity: 0.28; cursor: default; }

  .btn-blue {
    background: linear-gradient(135deg, #2563eb 0%, #1d4ed8 100%);
    box-shadow: 0 1px 6px rgba(37, 99, 235, 0.3);
  }
  .btn-blue:hover:not(:disabled) { box-shadow: 0 4px 16px rgba(37, 99, 235, 0.5); }

  .btn-purple {
    background: linear-gradient(135deg, #7c3aed 0%, #5b21b6 100%);
    box-shadow: 0 1px 6px rgba(124, 58, 237, 0.3);
  }
  .btn-purple:hover:not(:disabled) { box-shadow: 0 4px 16px rgba(124, 58, 237, 0.5); }

  .colors-label {
    display: flex;
    align-items: center;
    gap: 6px;
    font-size: 12px;
    color: #6888cc;
    white-space: nowrap;
  }
  .spinner {
    width: 50px;
    padding: 5px 7px;
    background: #06142890;
    border: 1px solid rgba(255, 255, 255, 0.1);
    border-radius: 8px;
    color: #d0d4ec;
    font-size: 12px;
    text-align: center;
    font-family: inherit;
    transition: border-color 0.15s, box-shadow 0.15s;
  }
  .spinner:focus {
    outline: none;
    border-color: rgba(124, 58, 237, 0.55);
    box-shadow: 0 0 0 2px rgba(124, 58, 237, 0.14);
  }

  .status {
    margin-left: auto;
    font-size: 11px;
    color: #5078c0;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  /* ── Main layout ─────────────────────────────────────────────────────── */
  .main {
    display: flex;
    flex: 1;
    overflow: hidden;
    padding: 10px;
    gap: 10px;
  }

  /* ── Image pane ──────────────────────────────────────────────────────── */
  .image-pane {
    flex: 1;
    position: relative;
    background: #061528;
    border-radius: 14px;
    overflow: hidden;
    border: 1px solid rgba(255, 255, 255, 0.06);
    min-width: 0;
  }

  .zoom-hint {
    position: absolute;
    bottom: 12px;
    left: 50%;
    transform: translateX(-50%);
    font-size: 11px;
    color: rgba(255, 255, 255, 0.55);
    pointer-events: none;
    background: rgba(3, 8, 22, 0.70);
    padding: 3px 12px;
    border-radius: 20px;
    white-space: nowrap;
    backdrop-filter: blur(6px);
    border: 1px solid rgba(255, 255, 255, 0.06);
  }

  /* ── Right column (resizable) ────────────────────────────────────────── */
  .right-col {
    flex: none;
    display: flex;
    flex-direction: column;
    min-width: 220px;
    max-width: 700px;
    gap: 10px;
    position: relative;
  }

  /* Vertical drag handle — left edge of right-col */
  .resize-handle-col {
    position: absolute;
    left: -7px;
    top: 0;
    bottom: 0;
    width: 14px;
    cursor: col-resize;
    z-index: 30;
    display: flex;
    align-items: center;
    justify-content: center;
  }
  .resize-handle-col::after {
    content: '';
    width: 3px;
    height: 36px;
    border-radius: 3px;
    background: rgba(255, 255, 255, 0.07);
    transition: background 0.15s, height 0.15s;
  }
  .resize-handle-col:hover::after,
  .resize-handle-col.active::after {
    background: rgba(124, 58, 237, 0.6);
    height: 52px;
  }

  /* ── Panel header ────────────────────────────────────────────────────── */
  .panel-header {
    display: flex;
    align-items: center;
    gap: 7px;
    padding: 8px 14px;
    font-size: 10px;
    font-weight: 700;
    letter-spacing: 0.1em;
    text-transform: uppercase;
    color: #4070c0;
    border-bottom: 1px solid rgba(255, 255, 255, 0.06);
    flex-shrink: 0;
  }
  .panel-header-dot {
    width: 6px;
    height: 6px;
    border-radius: 50%;
    background: #1e4880;
    flex-shrink: 0;
  }
  .panel-header-dot.accent {
    background: #5b21b6;
    box-shadow: 0 0 6px rgba(124, 58, 237, 0.7);
  }

  /* ── Mag pane ────────────────────────────────────────────────────────── */
  .mag-pane {
    flex: 1;
    background: #030b14;
    border-radius: 14px;
    overflow: hidden;
    position: relative;
    border: 1px solid rgba(255, 255, 255, 0.045);
    display: flex;
    flex-direction: column;
  }

  .panel-canvas-wrap {
    flex: 1;
    position: relative;
    overflow: hidden;
  }

  /* ── Results pane ────────────────────────────────────────────────────── */
  .results-pane {
    flex: 1;
    background: #061528;
    border-radius: 14px;
    overflow: hidden;
    border: 1px solid rgba(255, 255, 255, 0.06);
    display: flex;
    flex-direction: column;
  }

  .results-scroll {
    flex: 1;
    overflow-y: auto;
    padding: 10px;
  }

  /* ── Canvas ──────────────────────────────────────────────────────────── */
  .panel-canvas {
    position: absolute;
    inset: 0;
    width: 100%;
    height: 100%;
    display: block;
  }

  /* ── Hint ────────────────────────────────────────────────────────────── */
  .hint {
    font-size: 12px;
    font-style: italic;
    color: #2e5ca8;
    text-align: center;
    padding-top: 35%;
  }

  /* ── Results ─────────────────────────────────────────────────────────── */
  .results-list { display: flex; flex-direction: column; gap: 7px; }

  .match-row {
    display: flex;
    align-items: center;
    gap: 10px;
    background: rgba(255, 255, 255, 0.04);
    border: 1px solid rgba(255, 255, 255, 0.08);
    border-radius: 10px;
    padding: 9px 10px;
    transition: background 0.15s, border-color 0.15s;
  }
  .match-row:hover {
    background: rgba(255, 255, 255, 0.042);
    border-color: rgba(255, 255, 255, 0.09);
  }

  .swatch-dot {
    width: 26px;
    height: 26px;
    border-radius: 50%;
    flex-shrink: 0;
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.5), inset 0 1px 0 rgba(255, 255, 255, 0.14);
  }

  .pigment-dot {
    width: 18px;
    height: 18px;
    border-radius: 5px;
    flex-shrink: 0;
    box-shadow: 0 1px 4px rgba(0, 0, 0, 0.45);
  }

  .match-info { flex: 1; min-width: 0; }

  .match-header {
    display: flex;
    align-items: baseline;
    gap: 6px;
    margin-bottom: 5px;
  }
  .pigment-name {
    font-size: 12px;
    font-weight: 600;
    color: #d0d4ee;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }
  .pigment-cat { font-size: 10px; color: #4070b8; flex-shrink: 0; }
  .pigment-pct {
    font-size: 12px;
    font-weight: 700;
    color: #8b70f0;
    margin-left: auto;
    flex-shrink: 0;
  }

  .bar-track {
    height: 4px;
    background: rgba(255, 255, 255, 0.1);
    border-radius: 4px;
    overflow: hidden;
  }
  .bar-fill {
    height: 100%;
    border-radius: 4px;
    transition: width 0.4s cubic-bezier(0.25, 0.46, 0.45, 0.94);
    opacity: 0.82;
  }

  /* ── Swatches bar (resizable) ────────────────────────────────────────── */
  .swatches-bar {
    position: relative;
    background: #040d1a;
    border-top: 1px solid rgba(255, 255, 255, 0.07);
    flex-shrink: 0;
    display: flex;
    flex-direction: column;
    min-height: 100px;
    max-height: 300px;
  }

  /* Horizontal drag handle — top edge of swatches bar */
  .resize-handle-row {
    position: absolute;
    top: -7px;
    left: 0;
    right: 0;
    height: 14px;
    cursor: row-resize;
    z-index: 30;
    display: flex;
    align-items: center;
    justify-content: center;
  }
  .resize-handle-row::after {
    content: '';
    width: 40px;
    height: 3px;
    border-radius: 3px;
    background: rgba(255, 255, 255, 0.07);
    transition: background 0.15s, width 0.15s;
  }
  .resize-handle-row:hover::after,
  .resize-handle-row.active::after {
    background: rgba(124, 58, 237, 0.6);
    width: 58px;
  }

  .swatches-scroll {
    flex: 1;
    display: flex;
    gap: 8px;
    padding: 8px 10px;
    overflow-x: auto;
    overflow-y: hidden;
    align-items: stretch;
  }

  .swatch-card {
    flex-shrink: 0;
    width: 180px;
    border-radius: 10px;
    overflow: hidden;
    border: 1px solid rgba(255, 255, 255, 0.07);
    display: flex;
    flex-direction: column;
    background: #071428;
    transition: border-color 0.15s, box-shadow 0.15s;
  }
  .swatch-card:hover {
    border-color: rgba(255, 255, 255, 0.14);
    box-shadow: 0 4px 16px rgba(0, 0, 0, 0.35);
  }

  .swatch-strips {
    display: flex;
    flex: 1;
    min-height: 24px;
  }
  .swatch-strip { min-width: 2px; }

  .swatch-labels {
    display: flex;
    flex-direction: column;
    gap: 3px;
    padding: 6px 8px;
    flex: none;
    overflow: hidden;
  }
  .swatch-label {
    font-size: 11px;
    color: #4068c0;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }
</style>
