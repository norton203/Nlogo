// ── Token definitions ────────────────────────────────────────────
const TOKENS = [
    { type: 'comment', pattern: /;[^\n]*/ },
    { type: 'keyword', pattern: /\b(FORWARD|FD|BACKWARD|BK|RIGHT|RT|LEFT|LT|PENUP|PU|PENDOWN|PD|HOME|CLEARSCREEN|CS|SETCOLOR|SETPENCOLOR|SETPC|SETWIDTH|SETPENSIZE|SETPOS|SHOWTURTLE|ST|HIDETURTLE|HT|REPEAT|FOREVER|IF|IFELSE|WHILE|STOP|OUTPUT|OP|TO|END|MAKE|LOCAL|THING|AND|OR|NOT|PRINT|SHOW|TYPE|RANDOM|SIN|COS|TAN|ARCTAN|SQRT|ABS|ROUND|INT|POWER|LOG|EXP|TRUE|FALSE|WAIT|LABEL)\b/i },
    { type: 'string', pattern: /"[^\s\[\]()]*/ },
    { type: 'deref', pattern: /:[A-Za-z_][A-Za-z0-9_]*/ },
    { type: 'number', pattern: /-?\d+(\.\d+)?/ },
    { type: 'bracket', pattern: /[\[\]()]/ },
    { type: 'operator', pattern: /[+\-*\/^%=<>]+/ },
];

function escapeHtml(text) {
    return text
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;');
}

function highlight(source) {
    let output = '';
    let pos = 0;
    while (pos < source.length) {
        let matched = false;
        for (const { type, pattern } of TOKENS) {
            const anchored = new RegExp('^(?:' + pattern.source + ')', 'i');
            const match = anchored.exec(source.slice(pos));
            if (match) {
                output += `<span class="tok-${type}">${escapeHtml(match[0])}</span>`;
                pos += match[0].length;
                matched = true;
                break;
            }
        }
        if (!matched) {
            const ch = source[pos];
            output += ch === '\n' ? '\n' : escapeHtml(ch);
            pos++;
        }
    }
    return output + '\n';
}

// ── Canvas state ─────────────────────────────────────────────────
let canvas, ctx;
let turtle = { x: 0, y: 0, angle: -90, penDown: true, color: '#222222', width: 2 };

// ── Init canvas with ResizeObserver ──────────────────────────────
export function initCanvas(canvasId) {
    canvas = document.getElementById(canvasId);
    if (!canvas) {
        console.error('initCanvas: canvas element not found:', canvasId);
        return;
    }

    const wrapper = canvas.parentElement;

    // ResizeObserver fires whenever the wrapper changes size,
    // including the initial layout pass — no timing guesswork needed.
    const ro = new ResizeObserver(() => {
        fitCanvasToWrapper();
    });
    ro.observe(wrapper);

    // Attempt an immediate fit in case the observer fires late
    fitCanvasToWrapper();
}

function fitCanvasToWrapper() {
    if (!canvas) return;
    const wrapper = canvas.parentElement;
    const w = wrapper.clientWidth;
    const h = wrapper.clientHeight;
    if (w === 0 || h === 0) return; // layout not ready yet, observer will retry

    const size = Math.min(w, h) - 16;
    canvas.width = size;
    canvas.height = size;
    ctx = canvas.getContext('2d');

    resetTurtle();
    drawTurtleMarker();
}

// ── Turtle helpers ───────────────────────────────────────────────
function resetTurtle() {
    if (!canvas) return;
    turtle = {
        x: canvas.width / 2,
        y: canvas.height / 2,
        angle: -90,
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

// ── Draw commands ────────────────────────────────────────────────
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
export function backward(distance) { forward(-distance); }

export function home() {
    turtle.x = canvas.width / 2;
    turtle.y = canvas.height / 2;
    turtle.angle = -90;
    drawTurtleMarker();
}

export function showTurtle() { }
export function hideTurtle() { }

export function goTo(x, y) {
    turtle.x = canvas.width / 2 + x;
    turtle.y = canvas.height / 2 - y;
}

// ── Canvas utilities ─────────────────────────────────────────────
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

// ── Editor ───────────────────────────────────────────────────────
function updateHighlight(source) {
    const code = document.getElementById('highlightCode');
    if (code) code.innerHTML = highlight(source);
    syncScroll();
}

export function initEditor(editorId) {
    const attempt = (retries) => {
        const textarea = document.getElementById(editorId);
        if (!textarea) {
            if (retries > 0) {
                setTimeout(() => attempt(retries - 1), 100);
            } else {
                console.error('initEditor: element never found:', editorId);
            }
            return;
        }

        textarea.value = '';
        updateHighlight('');

        textarea.addEventListener('input', () => updateHighlight(textarea.value));
        textarea.addEventListener('scroll', syncScroll);

        textarea.addEventListener('keydown', e => {
            if (e.key === 'Tab') {
                e.preventDefault();
                const start = textarea.selectionStart;
                const end = textarea.selectionEnd;
                textarea.value =
                    textarea.value.substring(0, start) + '  ' +
                    textarea.value.substring(end);
                textarea.selectionStart = textarea.selectionEnd = start + 2;
                updateHighlight(textarea.value);
            }
        });

        console.log('initEditor: ready');
    };

    attempt(20);
}

export function getEditorValue() {
    const textarea = document.getElementById('codeEditor');
    return textarea ? textarea.value : '';
}

function syncScroll() {
    const textarea = document.getElementById('codeEditor');
    const pre = document.getElementById('highlightLayer');
    if (textarea && pre) {
        pre.scrollTop = textarea.scrollTop;
        pre.scrollLeft = textarea.scrollLeft;
    }
}

// ── Drag-to-resize ───────────────────────────────────────────────
export function initResizer(handleId, leftPaneId, rightPaneId) {
    const handle = document.getElementById(handleId);
    const leftPane = document.getElementById(leftPaneId);
    const rightPane = document.getElementById(rightPaneId);

    if (!handle || !leftPane || !rightPane) {
        console.error('initResizer: could not find elements', { handleId, leftPaneId, rightPaneId });
        return;
    }

    const container = leftPane.parentElement;
    let dragging = false;
    let startX = 0;
    let startWidth = 0;

    function getClientX(e) {
        return e.touches ? e.touches[0].clientX : e.clientX;
    }

    function onStart(e) {
        dragging = true;
        startX = getClientX(e);
        startWidth = leftPane.getBoundingClientRect().width;
        handle.classList.add('dragging');
        document.body.style.cursor = 'col-resize';
        document.body.style.userSelect = 'none';
        e.preventDefault();
    }

    function onMove(e) {
        if (!dragging) return;
        const dx = getClientX(e) - startX;
        const containerW = container.getBoundingClientRect().width;
        const newWidth = startWidth + dx;
        const pct = (newWidth / containerW) * 100;
        if (pct > 15 && pct < 85) {
            leftPane.style.width = `${pct}%`;
        }
        e.preventDefault();
    }

    function onEnd() {
        if (!dragging) return;
        dragging = false;
        handle.classList.remove('dragging');
        document.body.style.cursor = '';
        document.body.style.userSelect = '';
        // Let the ResizeObserver on the canvas wrapper handle the canvas resize
        fitCanvasToWrapper();
    }

    // Mouse events
    handle.addEventListener('mousedown', onStart);
    document.addEventListener('mousemove', onMove);
    document.addEventListener('mouseup', onEnd);

    // Touch events (MAUI WebView)
    handle.addEventListener('touchstart', onStart, { passive: false });
    document.addEventListener('touchmove', onMove, { passive: false });
    document.addEventListener('touchend', onEnd);

    console.log('initResizer: ready');
}