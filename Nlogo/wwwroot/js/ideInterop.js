// ── Token definitions ────────────────────────────────────────────
const TOKENS = [
    { type: 'comment',  pattern: /;[^\n]*/                          },
    { type: 'keyword',  pattern: /\b(FORWARD|FD|BACKWARD|BK|RIGHT|RT|LEFT|LT|PENUP|PU|PENDOWN|PD|HOME|CLEARSCREEN|CS|SETCOLOR|SETPENCOLOR|SETPC|SETWIDTH|SETPENSIZE|SETPOS|SHOWTURTLE|ST|HIDETURTLE|HT|REPEAT|FOREVER|IF|IFELSE|WHILE|STOP|OUTPUT|OP|TO|END|MAKE|LOCAL|THING|AND|OR|NOT|PRINT|SHOW|TYPE|RANDOM|SIN|COS|TAN|ARCTAN|SQRT|ABS|ROUND|INT|POWER|LOG|EXP|TRUE|FALSE|WAIT|LABEL)\b/i },
    { type: 'string',   pattern: /"[^\s\[\]()]*/                    },
    { type: 'deref',    pattern: /:[A-Za-z_][A-Za-z0-9_]*/         },
    { type: 'number',   pattern: /-?\d+(\.\d+)?/                   },
    { type: 'bracket',  pattern: /[\[\]()]/                         },
    { type: 'operator', pattern: /[+\-*\/^%=<>]+/                  },
];

// ── Error line tracking ───────────────────────────────────────────
let _errorLines = new Set();

// ── HTML escaping ────────────────────────────────────────────────
function escapeHtml(text) {
    return text
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;');
}

// ── Tokenise a single line into coloured spans ───────────────────
function tokeniseLine(source) {
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
            output += escapeHtml(source[pos]);
            pos++;
        }
    }
    return output;
}

// ── Build full highlighted HTML, wrapping each line in a span ────
//   Error lines get the 'line-error' class for the red gutter.
function highlight(source) {
    const rawLines = source.split('\n');
    const htmlLines = rawLines.map((line, i) => {
        const lineNum = i + 1;
        const cls = _errorLines.has(lineNum) ? ' class="line-error"' : '';
        return `<span${cls}>${tokeniseLine(line)}</span>`;
    });
    // trailing newline keeps the pre height in sync with the textarea
    return htmlLines.join('\n') + '\n';
}

// ── Update highlight layer ────────────────────────────────────────
function updateHighlight(source) {
    const code = document.getElementById('highlightCode');
    if (code) code.innerHTML = highlight(source);
    syncScroll();
}

// ── Update line-number gutter ─────────────────────────────────────
function updateLineNumbers(source) {
    const gutter = document.getElementById('lineNumbers');
    if (!gutter) return;

    const count = source.split('\n').length;
    let html = '';
    for (let i = 1; i <= count; i++) {
        const cls = _errorLines.has(i) ? ' class="ln-error"' : '';
        html += `<div${cls}>${i}</div>`;
    }
    gutter.innerHTML = html;
}

// ── Public: mark error lines (called from C#) ────────────────────
export function setErrorLines(lines) {
    _errorLines = new Set(lines);
    const textarea = document.getElementById('codeEditor');
    if (textarea) {
        updateHighlight(textarea.value);
        updateLineNumbers(textarea.value);
    }
}

// ── Public: clear all error highlights ───────────────────────────
export function clearErrorLines() {
    _errorLines = new Set();
    const textarea = document.getElementById('codeEditor');
    if (textarea) {
        updateHighlight(textarea.value);
        updateLineNumbers(textarea.value);
    }
}

// ════════════════════════════════════════════════════════════════
//  Canvas
// ════════════════════════════════════════════════════════════════
let canvas, ctx;
let turtle = { x: 0, y: 0, angle: -90, penDown: true, visible: true, color: '#222222', width: 2 };

export function initCanvas(canvasId) {
    canvas = document.getElementById(canvasId);
    if (!canvas) { console.error('initCanvas: not found:', canvasId); return; }

    const ro = new ResizeObserver(() => fitCanvasToWrapper());
    ro.observe(canvas.parentElement);
    fitCanvasToWrapper();
}

function fitCanvasToWrapper() {
    if (!canvas) return;
    const wrapper = canvas.parentElement;
    const padding = 8;
    const w = wrapper.clientWidth  - padding * 2;
    const h = wrapper.clientHeight - padding * 2;
    if (w <= 10 || h <= 10) return;

    canvas.width  = Math.round(w);
    canvas.height = Math.round(h);
    ctx = canvas.getContext('2d');
    resetTurtle();
    drawTurtleMarker();
}

function resetTurtle() {
    if (!canvas) return;
    turtle = { x: canvas.width / 2, y: canvas.height / 2,
               angle: -90, penDown: true, visible: true,
               color: '#222222', width: 2 };
}

function drawTurtleMarker() {
    if (!ctx || !turtle.visible) return;
    const r   = 8;
    const rad = (turtle.angle * Math.PI) / 180;
    ctx.save();
    ctx.translate(turtle.x, turtle.y);
    ctx.rotate(rad + Math.PI / 2);
    ctx.beginPath();
    ctx.moveTo(0, -r);
    ctx.lineTo(r * 0.6,  r * 0.8);
    ctx.lineTo(0,        r * 0.4);
    ctx.lineTo(-r * 0.6, r * 0.8);
    ctx.closePath();
    ctx.fillStyle   = '#4CAF50';
    ctx.strokeStyle = '#2E7D32';
    ctx.lineWidth   = 1.5;
    ctx.fill();
    ctx.stroke();
    ctx.restore();
}

export function forward(distance) {
    const rad  = (turtle.angle * Math.PI) / 180;
    const newX = turtle.x + distance * Math.cos(rad);
    const newY = turtle.y + distance * Math.sin(rad);
    if (turtle.penDown) {
        ctx.beginPath();
        ctx.strokeStyle = turtle.color;
        ctx.lineWidth   = turtle.width;
        ctx.lineCap     = 'round';
        ctx.moveTo(turtle.x, turtle.y);
        ctx.lineTo(newX, newY);
        ctx.stroke();
    }
    turtle.x = newX;
    turtle.y = newY;
    drawTurtleMarker();
}

export function right(degrees)     { turtle.angle += degrees; drawTurtleMarker(); }
export function left(degrees)      { turtle.angle -= degrees; drawTurtleMarker(); }
export function penUp()            { turtle.penDown = false; }
export function penDown()          { turtle.penDown = true;  }
export function setColor(color)    { turtle.color = color;   }
export function setWidth(w)        { turtle.width = w;       }
export function backward(distance) { forward(-distance);     }

export function home() {
    if (!canvas) return;
    turtle.x = canvas.width / 2; turtle.y = canvas.height / 2; turtle.angle = -90;
    drawTurtleMarker();
}

export function showTurtle() { turtle.visible = true;  drawTurtleMarker(); }
export function hideTurtle() { turtle.visible = false; }

export function goTo(x, y) {
    if (!canvas) return;
    turtle.x = canvas.width  / 2 + x;
    turtle.y = canvas.height / 2 - y;
    drawTurtleMarker();
}

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

// ════════════════════════════════════════════════════════════════
//  Editor
// ════════════════════════════════════════════════════════════════
export function initEditor(editorId) {
    const attempt = (retries) => {
        const textarea = document.getElementById(editorId);
        if (!textarea) {
            if (retries > 0) setTimeout(() => attempt(retries - 1), 100);
            else console.error('initEditor: element never found:', editorId);
            return;
        }

        textarea.value = '';
        updateHighlight('');
        updateLineNumbers('');

        textarea.addEventListener('input', () => {
            // clear error highlights as soon as the user edits
            _errorLines = new Set();
            updateHighlight(textarea.value);
            updateLineNumbers(textarea.value);
        });

        textarea.addEventListener('scroll', syncScroll);

        textarea.addEventListener('keydown', e => {
            if (e.key === 'Tab') {
                e.preventDefault();
                const start = textarea.selectionStart;
                const end   = textarea.selectionEnd;
                textarea.value =
                    textarea.value.substring(0, start) + '  ' +
                    textarea.value.substring(end);
                textarea.selectionStart = textarea.selectionEnd = start + 2;
                updateHighlight(textarea.value);
                updateLineNumbers(textarea.value);
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
    const pre      = document.getElementById('highlightLayer');
    const gutter   = document.getElementById('lineNumbers');
    if (!textarea) return;
    if (pre)    { pre.scrollTop  = textarea.scrollTop; pre.scrollLeft = textarea.scrollLeft; }
    if (gutter) { gutter.scrollTop = textarea.scrollTop; }
}

// ════════════════════════════════════════════════════════════════
//  Drag-to-resize
// ════════════════════════════════════════════════════════════════
export function initResizer(handleId, leftPaneId, rightPaneId) {
    const handle    = document.getElementById(handleId);
    const leftPane  = document.getElementById(leftPaneId);
    const rightPane = document.getElementById(rightPaneId);
    if (!handle || !leftPane || !rightPane) {
        console.error('initResizer: missing elements', { handleId, leftPaneId, rightPaneId });
        return;
    }

    const container = leftPane.parentElement;
    let dragging = false, startX = 0, startWidth = 0;

    const getClientX = e => e.touches ? e.touches[0].clientX : e.clientX;

    function onStart(e) {
        dragging   = true;
        startX     = getClientX(e);
        startWidth = leftPane.getBoundingClientRect().width;
        handle.classList.add('dragging');
        document.body.style.cursor     = 'col-resize';
        document.body.style.userSelect = 'none';
        e.preventDefault();
    }

    function onMove(e) {
        if (!dragging) return;
        const pct = ((startWidth + getClientX(e) - startX) /
                      container.getBoundingClientRect().width) * 100;
        if (pct > 15 && pct < 85) leftPane.style.width = `${pct}%`;
        e.preventDefault();
    }

    function onEnd() {
        if (!dragging) return;
        dragging = false;
        handle.classList.remove('dragging');
        document.body.style.cursor     = '';
        document.body.style.userSelect = '';
        fitCanvasToWrapper();
    }

    handle.addEventListener('mousedown', onStart);
    document.addEventListener('mousemove', onMove);
    document.addEventListener('mouseup',   onEnd);
    handle.addEventListener('touchstart',  onStart, { passive: false });
    document.addEventListener('touchmove', onMove,  { passive: false });
    document.addEventListener('touchend',  onEnd);

    console.log('initResizer: ready');
}