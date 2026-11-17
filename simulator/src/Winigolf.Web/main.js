import { dotnet } from './_framework/dotnet.js';
const canvas = document.getElementById('canvas');
const viewport = canvas.parentElement;
const error = document.getElementById('error');
const performanceDisplay = document.getElementById('performance');
function fail(e) { console.error(e); error.textContent = `Erro: ${e}`; }
try {
    const { getAssemblyExports, getConfig, runMain } = await dotnet.withDiagnosticTracing(false).create();
    const exports = await getAssemblyExports(getConfig().mainAssemblyName);
    const host = exports.Winigolf.Game.Host;
    dotnet.instance.Module.canvas = canvas;
    await runMain();
    // raylib uses a fixed logical screen; some browsers resize the backing store
    // to the CSS dimensions, which otherwise crops its 960x600 viewport.
    function fitCanvas() {
        const width = viewport.clientWidth, height = viewport.clientHeight;
        if (!width || !height) return;
        const scale = Math.min(width / 960, height / 600);
        canvas.style.width = `${960 * scale}px`;
        canvas.style.height = `${600 * scale}px`;
    }
    function ensureBuffer() {
        if (canvas.width !== 960) canvas.width = 960;
        if (canvas.height !== 600) canvas.height = 600;
    }
    new ResizeObserver(fitCanvas).observe(viewport);
    fitCanvas();
    ensureBuffer();
    let active = null;
    function pointer(e, kind) {
        const rect = canvas.getBoundingClientRect();
        host.Pointer(kind, (e.clientX - rect.left) * canvas.width / rect.width,
            (e.clientY - rect.top) * canvas.height / rect.height);
    }
    canvas.addEventListener('pointerdown', e => {
        if (active !== null || (e.pointerType === 'mouse' && e.button !== 0)) return;
        e.preventDefault(); active = e.pointerId; canvas.setPointerCapture(active); pointer(e, 0);
    });
    canvas.addEventListener('pointermove', e => { if (e.pointerId === active) pointer(e, 1); });
    canvas.addEventListener('pointerup', e => { if (e.pointerId === active) { pointer(e, 2); active = null; } });
    canvas.addEventListener('pointercancel', e => { if (e.pointerId === active) { pointer(e, 3); active = null; } });
    canvas.addEventListener('contextmenu', e => e.preventDefault());
    document.getElementById('reset').addEventListener('click', () => host.Reset());
    let last, sampleStart, frames = 0, totalFrameMs = 0, maxFrameMs = 0;
    function frame(now) {
        try {
            ensureBuffer();
            const started = performance.now();
            host.Frame(last === undefined ? 0 : (now - last) / 1000);
            const frameMs = performance.now() - started;
            last = now;
            if (sampleStart === undefined) sampleStart = now;
            frames++; totalFrameMs += frameMs; maxFrameMs = Math.max(maxFrameMs, frameMs);
            const elapsed = now - sampleStart;
            if (elapsed >= 1000) {
                performanceDisplay.textContent = `Quadros: ${(frames * 1000 / elapsed).toFixed(0)} FPS | C# por quadro: ${(totalFrameMs / frames).toFixed(1)} ms (máx. ${maxFrameMs.toFixed(1)} ms)`;
                sampleStart = now; frames = 0; totalFrameMs = 0; maxFrameMs = 0;
            }
            requestAnimationFrame(frame);
        }
        catch (e) { fail(e); }
    }
    requestAnimationFrame(frame);
} catch (e) { fail(e); }
