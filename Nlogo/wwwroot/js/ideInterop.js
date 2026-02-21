import { highlight } from './highlighter.js';

// ── Canvas state ────────────────────────────────────────────────
let canvas, ctx;
let turtle = { x: 0, y: 0, angle: 0, penDown: true, color: '#000', width: 2 };

// ── Init ────────────────────────────────────────────────────────
export function initCanvas(canvasId) {
    canvas = document.getElementById(canvasId);
    resizeCanvasToWrapper();
    resetTurtle();
    drawTurtleMarker();

    // Keep canvas crisp when wrapper resizes
    window.addEventListener('resize', () => {
        resizeCanvasToWrapper();
        resetTurtle();
    });
}

function resizeCanvasToWrapper() {
    if (!canvas) return;
    const wrapper = canvas.parentElement;
    const size = Math.min(wrapper.clientWidth, wrapper.clientHeight) - 16;
    canvas.width = size;
    canvas.height = size;
    ctx = canvas.getContext('2d');
}

// ── Turtle helpers ──────────────────────────────────────────────
function resetTurtle() {
    if (!canvas) return;
    turtle = {
        x: canvas.width / 2,
        y: canvas.height / 2,
        angle: -90,   // 0° = up, matching Logo convention
        penDown: true,
        color: '#222222',
        width: 2
    };
}

function drawTurtleMarker() {
    if (!ctx) return;
    const r = 8;
    const rad = (turtle.angle * Math.PI) / 180;

    ctx.save();
    ctx.translate(turtle.x, turtle.y);
    ctx.rotate(rad + Math.PI / 2);

    ctx.beginPath();
    ctx.moveTo(0, -r);
    ctx.lineTo(r * 0.6, r * 0.8);
    ctx.lineTo(0, r * 0.4);
    ctx.lineTo(-r * 0.6, r * 0.8);
    ctx.closePath();

    ctx.fillStyle = '#4CAF50';
    ctx.strokeStyle = '#2E7D32';
    ctx.lineWidth = 1.5;
    ctx.fill();
    ctx.stroke();
    ctx.restore();
}

// ── Draw commands (called from C# compiler output) ─────────────
export function forward(distance) {
    const rad = (turtle.angle * Math.PI) / 180;
    const newX = turtle.x + distance * Math.cos(rad);
    const newY = turtle.y + distance * Math.sin(rad);

    if (turtle.penDown) {
        ctx.beginPath();
        ctx.strokeStyle = turtle.color;
        ctx.lineWidth = turtle.width;
        ctx.lineCap = 'round';
        ctx.moveTo(turtle.x, turtle.y);
        ctx.lineTo(newX, newY);
        ctx.stroke();
    }

    turtle.x = newX;
    turtle.y = newY;
    drawTurtleMarker();
}

export function right(degrees) { turtle.angle += degrees; }
export function left(degrees) { turtle.angle -= degrees; }
export function penUp() { turtle.penDown = false; }
export function penDown() { turtle.penDown = true; }

export function setColor(color) { turtle.color = color; }
export function setWidth(w) { turtle.width = w; }
export function backward(distance) {
    forward(-distance); // reuse forward with negative distance
}

export function home() {
    turtle.x = canvas.width / 2;
    turtle.y = canvas.height / 2;
    turtle.angle = -90;
    drawTurtleMarker();
}

export function showTurtle() { /* hook for later if you want to toggle visibility */ }
export function hideTurtle() { /* hook for later */ }
export function goTo(x, y) {
    turtle.x = canvas.width / 2 + x;
    turtle.y = canvas.height / 2 - y;
}

// ── Canvas utilities ────────────────────────────────────────────
export function clearCanvas() {
    if (!ctx || !canvas) return;
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    resetTurtle();
    drawTurtleMarker();
}

export function saveCanvasImage(filename) {
    const link = document.createElement('a');
    link.download = filename;
    link.href = canvas.toDataURL('image/png');
    link.click();
}

export function copyToClipboard(text) {
    navigator.clipboard.writeText(text);
}

// ── Drag-to-resize ──────────────────────────────────────────────
export function initResizer(handleId, leftPaneId, rightPaneId) {
    const handle = document.getElementById(handleId);
    const leftPane = document.getElementById(leftPaneId);
    const container = leftPane.parentElement;
    let dragging = false;

    handle.addEventListener('mousedown', e => {
        dragging = true;
        handle.classList.add('dragging');
        document.body.style.cursor = 'col-resize';
        document.body.style.userSelect = 'none';
        e.preventDefault();
    });

    document.addEventListener('mousemove', e => {
        if (!dragging) return;
        const rect = container.getBoundingClientRect();
        const newWidth = e.clientX - rect.left;
        const pct = (newWidth / rect.width) * 100;

        if (pct > 20 && pct < 80)
            leftPane.style.width = `${pct}%`;
    });

    document.addEventListener('mouseup', () => {
        if (!dragging) return;
        dragging = false;
        handle.classList.remove('dragging');
        document.body.style.cursor = '';
        document.body.style.userSelect = '';
        resizeCanvasToWrapper(); // re-fit canvas after resize
    });
}
export function updateHighlight(source) {
    const code = document.getElementById('highlightCode');
    if (code) code.innerHTML = highlight(source);
    syncScroll();
}

export function initEditor(editorId) {
    const textarea = document.getElementById(editorId);
    if (!textarea) return;

    // Sync scroll position so highlight layer tracks the textarea
    textarea.addEventListener('scroll', syncScroll);

    // Handle Tab key — insert spaces instead of moving focus
    textarea.addEventListener('keydown', e => {
        if (e.key === 'Tab') {
            e.preventDefault();
            const start = textarea.selectionStart;
            const end = textarea.selectionEnd;
            textarea.value =
                textarea.value.substring(0, start) + '  ' +
                textarea.value.substring(end);
            textarea.selectionStart = textarea.selectionEnd = start + 2;

            // Trigger highlight update after tab insert
            updateHighlight(textarea.value);
        }
    });
}

function syncScroll() {
    const textarea = document.getElementById('codeEditor');
    const pre = document.getElementById('highlightLayer');
    if (textarea && pre) {
        pre.scrollTop = textarea.scrollTop;
        pre.scrollLeft = textarea.scrollLeft;
    }
}