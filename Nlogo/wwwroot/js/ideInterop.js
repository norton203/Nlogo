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
function highlight(source) {
    const rawLines = source.split('\n');
    const htmlLines = rawLines.map((line, i) => {
        const lineNum = i + 1;
        const cls = _errorLines.has(lineNum) ? ' class="line-error"' : '';
        return `<span${cls}>${tokeniseLine(line)}</span>`;
    });
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

// ── Public: mark error lines ──────────────────────────────────────
export function setErrorLines(lines) {
    _errorLines = new Set(lines);
    const textarea = document.getElementById('codeEditor');
    if (textarea) {
        updateHighlight(textarea.value);
        updateLineNumbers(textarea.value);
    }
}

// ── Public: clear error highlights ───────────────────────────────
export function clearErrorLines() {
    _errorLines = new Set();
    const textarea = document.getElementById('codeEditor');
    if (textarea) {
        updateHighlight(textarea.value);
        updateLineNumbers(textarea.value);
    }
}

// ════════════════════════════════════════════════════════════════
//  Canvas  —  two-layer approach
//    canvas / ctx   : drawing layer  (lines stay here)
//    overlay / octx : turtle sprite  (cleared on every move — no ghosts)
// ════════════════════════════════════════════════════════════════
let canvas, ctx;
let overlay, octx;

const TURTLE_SHAPES = ['arrow', 'turtle', 'dot', 'rocket', 'bug',
    'emoji:💩', 'emoji:👾', 'emoji:🦆', 'emoji:🐸',
    'emoji:👻', 'emoji:🤖', 'emoji:🦄', 'emoji:⭐',
    'emoji:🦖', 'emoji:🐙', 'emoji:🍕', 'emoji:🎃'];

let turtle = {
    x: 0, y: 0, angle: -90,
    penDown: true, visible: true,
    color: '#222222', width: 2,
    shape: 'arrow'
};

export function initCanvas(canvasId) {
    canvas = document.getElementById(canvasId);
    if (!canvas) { console.error('initCanvas: not found:', canvasId); return; }

    // Overlay canvas for the turtle sprite — sits on top of the drawing canvas.
    // Clearing it never disturbs drawn lines.
    const parent = canvas.parentElement;
    parent.style.position = 'relative';

    overlay = document.createElement('canvas');
    overlay.style.position = 'absolute';
    overlay.style.pointerEvents = 'none';
    overlay.style.zIndex = '1';
    parent.appendChild(overlay);

    const ro = new ResizeObserver(() => fitCanvasToWrapper());
    ro.observe(parent);
    fitCanvasToWrapper();
}

function syncOverlay() {
    if (!canvas || !overlay) return;
    overlay.width = canvas.width;
    overlay.height = canvas.height;
    const cr = canvas.getBoundingClientRect();
    const pr = canvas.parentElement.getBoundingClientRect();
    overlay.style.left = Math.round(cr.left - pr.left) + 'px';
    overlay.style.top = Math.round(cr.top - pr.top) + 'px';
    octx = overlay.getContext('2d');
}

function fitCanvasToWrapper() {
    if (!canvas) return;
    const wrapper = canvas.parentElement;
    const padding = 8;
    const w = wrapper.clientWidth - padding * 2;
    const h = wrapper.clientHeight - padding * 2;
    if (w <= 10 || h <= 10) return;

    canvas.width = Math.round(w);
    canvas.height = Math.round(h);
    ctx = canvas.getContext('2d');
    syncOverlay();
    resetTurtle();
    drawTurtleMarker();
}

function resetTurtle() {
    if (!canvas) return;
    const shape = turtle?.shape ?? 'arrow';
    turtle = {
        x: canvas.width / 2, y: canvas.height / 2,
        angle: -90, penDown: true, visible: true,
        color: '#222222', width: 2,
        shape
    };
}

// ── Turtle marker ─────────────────────────────────────────────────
// Always clears the overlay first — that's all "erasing" ever needs.
function drawTurtleMarker() {
    if (!octx || !overlay) return;
    octx.clearRect(0, 0, overlay.width, overlay.height);
    if (!turtle.visible) return;

    const rad = (turtle.angle * Math.PI) / 180;
    octx.save();
    octx.translate(turtle.x, turtle.y);
    octx.rotate(rad + Math.PI / 2);  // 0° = pointing up

    const isEmoji = turtle.shape.startsWith('emoji:');

    if (isEmoji) {
        // Emoji shapes: counter-rotate so they stay upright, then draw
        octx.rotate(-(rad + Math.PI / 2));
        drawShapeEmoji(octx, turtle.shape.slice(6), rad + Math.PI / 2);
    } else {
        switch (turtle.shape) {
            case 'turtle': drawShapeTurtle(octx); break;
            case 'dot': drawShapeDot(octx); break;
            case 'rocket': drawShapeRocket(octx); break;
            case 'bug': drawShapeBug(octx); break;
            default: drawShapeArrow(octx); break;
        }
    }

    octx.restore();
}

// ════════════════════════════════════════════════════════════════
//  Turtle shapes  (all drawn centred at origin, pointing up)
// ════════════════════════════════════════════════════════════════

function drawShapeEmoji(c, emoji, turtleRad) {
    const size = 26;

    // Small direction arrow underneath so heading is still readable
    c.save();
    c.rotate(turtleRad);  // re-apply the real heading for the arrow
    c.beginPath();
    c.moveTo(0, -18);
    c.lineTo(4, -10);
    c.lineTo(0, -13);
    c.lineTo(-4, -10);
    c.closePath();
    c.fillStyle = 'rgba(255,255,255,0.85)';
    c.strokeStyle = 'rgba(0,0,0,0.4)';
    c.lineWidth = 0.8;
    c.fill();
    c.stroke();
    c.restore();

    // Draw the emoji, centred
    c.font = `${size}px serif`;
    c.textAlign = 'center';
    c.textBaseline = 'middle';
    c.fillText(emoji, 0, 0);
}

function drawShapeArrow(c) {
    c.beginPath();
    c.moveTo(0, -10);
    c.lineTo(6, 8);
    c.lineTo(0, 4);
    c.lineTo(-6, 8);
    c.closePath();
    c.fillStyle = '#4CAF50';
    c.strokeStyle = '#2E7D32';
    c.lineWidth = 1.5;
    c.fill();
    c.stroke();
}

function drawShapeTurtle(c) {
    // Flippers: [x, y, radiusX, radiusY, rotation]
    const flippers = [
        [-7, -4, 5, 2.5, -0.5],
        [7, -4, 5, 2.5, 0.5],
        [-7, 5, 5, 2.5, 0.5],
        [7, 5, 5, 2.5, -0.5],
    ];
    c.fillStyle = '#66BB6A';
    c.strokeStyle = '#388E3C';
    c.lineWidth = 1;
    for (const [fx, fy, rx, ry, ra] of flippers) {
        c.save();
        c.translate(fx, fy);
        c.rotate(ra);
        c.beginPath();
        c.ellipse(0, 0, rx, ry, 0, 0, Math.PI * 2);
        c.fill(); c.stroke();
        c.restore();
    }
    // Head
    c.beginPath();
    c.arc(0, -11, 3, 0, Math.PI * 2);
    c.fillStyle = '#4CAF50'; c.strokeStyle = '#2E7D32';
    c.fill(); c.stroke();
    // Shell body
    c.beginPath();
    c.ellipse(0, 0, 6.5, 9, 0, 0, Math.PI * 2);
    c.fillStyle = '#388E3C'; c.strokeStyle = '#1B5E20'; c.lineWidth = 1;
    c.fill(); c.stroke();
    // Hex shell pattern
    c.strokeStyle = '#1B5E20'; c.lineWidth = 0.7;
    _hex(c, 0, 0, 3);
    _hex(c, 0, -5, 2.4);
    _hex(c, -4, 2.5, 2.4);
    _hex(c, 4, 2.5, 2.4);
}

function _hex(c, cx, cy, r) {
    c.beginPath();
    for (let i = 0; i < 6; i++) {
        const a = (i * Math.PI) / 3 - Math.PI / 6;
        i === 0
            ? c.moveTo(cx + r * Math.cos(a), cy + r * Math.sin(a))
            : c.lineTo(cx + r * Math.cos(a), cy + r * Math.sin(a));
    }
    c.closePath(); c.stroke();
}

function drawShapeDot(c) {
    c.beginPath();
    c.arc(0, 0, 9, 0, Math.PI * 2);
    c.fillStyle = '#4CAF50';
    c.strokeStyle = '#2E7D32';
    c.lineWidth = 1.5;
    c.fill(); c.stroke();
    // Direction nub
    c.fillStyle = '#1B5E20';
    c.beginPath();
    c.moveTo(0, -6); c.lineTo(3.5, 1); c.lineTo(0, -1); c.lineTo(-3.5, 1);
    c.closePath(); c.fill();
}

function drawShapeRocket(c) {
    // Exhaust flame
    c.beginPath();
    c.moveTo(-3.5, 9); c.quadraticCurveTo(0, 17, 3.5, 9);
    c.fillStyle = 'rgba(255,152,0,0.85)'; c.fill();
    c.beginPath();
    c.moveTo(-1.8, 9); c.quadraticCurveTo(0, 14, 1.8, 9);
    c.fillStyle = 'rgba(255,235,59,0.9)'; c.fill();
    // Wings
    c.lineWidth = 1;
    [[4.5, 5, 9, 10, 4.5, 9], [-4.5, 5, -9, 10, -4.5, 9]].forEach(([x1, y1, x2, y2, x3, y3]) => {
        c.beginPath(); c.moveTo(x1, y1); c.lineTo(x2, y2); c.lineTo(x3, y3); c.closePath();
        c.fillStyle = '#EF9A9A'; c.strokeStyle = '#C62828'; c.fill(); c.stroke();
    });
    // Body
    c.beginPath();
    c.moveTo(0, -12);
    c.bezierCurveTo(5.5, -5, 5.5, 4, 4.5, 9);
    c.lineTo(-4.5, 9);
    c.bezierCurveTo(-5.5, 4, -5.5, -5, 0, -12);
    c.fillStyle = '#E53935'; c.strokeStyle = '#B71C1C'; c.lineWidth = 1;
    c.fill(); c.stroke();
    // Porthole
    c.beginPath();
    c.arc(0, -2, 3, 0, Math.PI * 2);
    c.fillStyle = '#90CAF9'; c.strokeStyle = '#1565C0'; c.lineWidth = 1;
    c.fill(); c.stroke();
}

function drawShapeBug(c) {
    // Legs
    c.strokeStyle = '#757575'; c.lineWidth = 1;
    [[-4, -7, -1], [4, -7, 1], [-4, -3, -1], [4, -3, 1], [-4, 2, -1], [4, 2, 1]].forEach(([x, y, dir]) => {
        c.beginPath(); c.moveTo(x, y); c.lineTo(x + dir * 8, y - 2); c.stroke();
    });
    // Body segments
    c.fillStyle = '#212121'; c.strokeStyle = '#555'; c.lineWidth = 1;
    [[0, 5.5, 4, 5.5], [0, -2, 3.5, 4], [0, -9, 3.5, 3.5]].forEach(([cx, cy, rx, ry]) => {
        c.beginPath(); c.ellipse(cx, cy, rx, ry, 0, 0, Math.PI * 2); c.fill(); c.stroke();
    });
    // Eyes
    c.fillStyle = '#FF5252';
    [[-1.8, -10], [1.8, -10]].forEach(([ex, ey]) => {
        c.beginPath(); c.arc(ex, ey, 1, 0, Math.PI * 2); c.fill();
    });
    // Antennae + tips
    c.strokeStyle = '#9E9E9E'; c.lineWidth = 0.9;
    [[-1.5, -12, -5.5, -16.5], [1.5, -12, 5.5, -16.5]].forEach(([x1, y1, x2, y2]) => {
        c.beginPath(); c.moveTo(x1, y1); c.lineTo(x2, y2); c.stroke();
        c.fillStyle = '#BDBDBD';
        c.beginPath(); c.arc(x2, y2, 1.2, 0, Math.PI * 2); c.fill();
    });
}

// ── Public: change turtle shape ────────────────────────────────
export function setTurtleShape(shape) {
    if (TURTLE_SHAPES.includes(shape) || shape.startsWith('emoji:')) {
        turtle.shape = shape;
    } else {
        turtle.shape = 'arrow';
    }
    drawTurtleMarker();
}   

// ── Public: movement & pen ─────────────────────────────────────
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

export function right(degrees) { turtle.angle += degrees; drawTurtleMarker(); }
export function left(degrees) { turtle.angle -= degrees; drawTurtleMarker(); }
export function penUp() { turtle.penDown = false; }
export function penDown() { turtle.penDown = true; }
export function setColor(color) { turtle.color = color; }
export function setWidth(w) { turtle.width = w; }
export function backward(distance) { forward(-distance); }

export function home() {
    if (!canvas) return;
    turtle.x = canvas.width / 2; turtle.y = canvas.height / 2; turtle.angle = -90;
    drawTurtleMarker();
}

export function showTurtle() { turtle.visible = true; drawTurtleMarker(); }
export function hideTurtle() { turtle.visible = false; drawTurtleMarker(); }

export function goTo(x, y) {
    if (!canvas) return;
    turtle.x = canvas.width / 2 + x;
    turtle.y = canvas.height / 2 - y;
    drawTurtleMarker();
}

export function clearCanvas() {
    if (!ctx || !canvas) return;
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    if (octx) octx.clearRect(0, 0, overlay.width, overlay.height);
    resetTurtle();
    drawTurtleMarker();
}

export function saveCanvasImage(filename) {
    // Composite both layers into one temporary canvas before saving
    const tmp = document.createElement('canvas');
    tmp.width = canvas.width;
    tmp.height = canvas.height;
    const tctx = tmp.getContext('2d');
    tctx.drawImage(canvas, 0, 0);
    if (overlay) tctx.drawImage(overlay, 0, 0);
    const link = document.createElement('a');
    link.download = filename;
    link.href = tmp.toDataURL('image/png');
    link.click();
}
// ── Canvas stats for challenge validation ─────────────────────────
// Returns pixel count and bounding-box aspect ratio of drawn content.
// The canvas CSS background is white, but pixels are transparent until drawn,
// so any alpha > 0 means the turtle painted there.
export function getCanvasStats() {
    if (!canvas || !ctx) {
        return { hasDrawing: false, pixelCount: 0, aspectRatio: 1 };
    }

    const w = canvas.width;
    const h = canvas.height;
    const imageData = ctx.getImageData(0, 0, w, h);
    const data = imageData.data;

    let minX = w, minY = h, maxX = 0, maxY = 0, pixelCount = 0;

    for (let i = 0; i < data.length; i += 4) {
        const alpha = data[i + 3];
        if (alpha > 20) {           // painted pixel (ignore near-transparent anti-aliasing edges)
            pixelCount++;
            const idx = i / 4;
            const x = idx % w;
            const y = Math.floor(idx / w);
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }
    }

    if (pixelCount === 0) {
        return { hasDrawing: false, pixelCount: 0, aspectRatio: 1 };
    }

    const bw = maxX - minX + 1;
    const bh = maxY - minY + 1;
    return {
        hasDrawing: true,
        pixelCount: pixelCount,
        aspectRatio: bh > 0 ? bw / bh : 1,
    };
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
            _errorLines = new Set();
            updateHighlight(textarea.value);
            updateLineNumbers(textarea.value);
        });

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
                updateLineNumbers(textarea.value);
            }
        });

        initAutoComplete(textarea);
        console.log('initEditor: ready');
    };
    attempt(20);
}

export function getEditorValue() {
    const textarea = document.getElementById('codeEditor');
    return textarea ? textarea.value : '';
}

// ── Set editor content (used by challenge panel to load starter code) ──
export function setEditorValue(code) {
    const textarea = document.getElementById('codeEditor');
    if (!textarea) return;
    textarea.value = code;
    _errorLines = new Set();
    updateHighlight(code);
    updateLineNumbers(code);
    // Put cursor at end
    textarea.selectionStart = textarea.selectionEnd = code.length;
    textarea.focus();
}

function syncScroll() {
    const textarea = document.getElementById('codeEditor');
    const pre = document.getElementById('highlightLayer');
    const gutter = document.getElementById('lineNumbers');
    if (!textarea) return;
    if (pre) { pre.scrollTop = textarea.scrollTop; pre.scrollLeft = textarea.scrollLeft; }
    if (gutter) { gutter.scrollTop = textarea.scrollTop; }
}

// ════════════════════════════════════════════════════════════════
//  Auto-complete
// ════════════════════════════════════════════════════════════════

const AC_KEYWORDS = [
    // Movement
    'FORWARD', 'FD', 'BACKWARD', 'BK', 'RIGHT', 'RT', 'LEFT', 'LT',
    // Pen
    'PENUP', 'PU', 'PENDOWN', 'PD',
    'SETCOLOR', 'SETPENCOLOR', 'SETPC', 'SETWIDTH', 'SETPENSIZE', 'SETPOS',
    // Screen
    'HOME', 'CLEARSCREEN', 'CS', 'SHOWTURTLE', 'ST', 'HIDETURTLE', 'HT',
    // Output
    'PRINT', 'SHOW', 'TYPE', 'LABEL',
    // Math
    'SIN', 'COS', 'TAN', 'ARCTAN', 'SQRT', 'ABS', 'ROUND', 'INT',
    'POWER', 'LOG', 'EXP', 'RANDOM',
    // Control flow
    'REPEAT', 'FOREVER', 'IF', 'IFELSE', 'WHILE', 'STOP', 'OUTPUT', 'OP',
    // Procedures & variables
    'TO', 'END', 'MAKE', 'LOCAL', 'THING',
    // Logic & constants
    'TRUE', 'FALSE', 'AND', 'OR', 'NOT',
    // Timing
    'WAIT',
];

// Short hint shown to the right of each keyword in the dropdown
const AC_HINTS = {
    'FORWARD': 'fd steps', 'FD': 'fd steps',
    'BACKWARD': 'bk steps', 'BK': 'bk steps',
    'RIGHT': 'rt degrees', 'RT': 'rt degrees',
    'LEFT': 'lt degrees', 'LT': 'lt degrees',
    'PENUP': 'lift pen', 'PU': 'lift pen',
    'PENDOWN': 'lower pen', 'PD': 'lower pen',
    'SETCOLOR': 'setcolor "red', 'SETPENCOLOR': 'set color', 'SETPC': 'set color',
    'SETWIDTH': 'pen width', 'SETPENSIZE': 'pen width',
    'SETPOS': 'setpos [x y]',
    'HOME': 'go to centre',
    'CLEARSCREEN': 'clear + home', 'CS': 'clear + home',
    'SHOWTURTLE': 'show turtle', 'ST': 'show turtle',
    'HIDETURTLE': 'hide turtle', 'HT': 'hide turtle',
    'PRINT': 'print value', 'SHOW': 'print value', 'TYPE': 'print value',
    'LABEL': 'draw text at turtle',
    'SIN': 'sin degrees', 'COS': 'cos degrees', 'TAN': 'tan degrees',
    'ARCTAN': '→ degrees', 'SQRT': 'square root', 'ABS': 'absolute',
    'ROUND': 'round number', 'INT': 'truncate',
    'POWER': 'power base exp', 'LOG': 'natural log', 'EXP': 'e ^ value',
    'RANDOM': 'random max',
    'REPEAT': 'repeat n [ ]',
    'FOREVER': 'forever [ ]',
    'IF': 'if cond [ ]',
    'IFELSE': 'ifelse cond [ ] [ ]',
    'WHILE': 'while cond [ ]',
    'STOP': 'exit procedure',
    'OUTPUT': 'return value', 'OP': 'return value',
    'TO': 'define procedure', 'END': 'end procedure',
    'MAKE': 'make "var value', 'LOCAL': 'local "var', 'THING': 'thing "var',
    'TRUE': 'boolean', 'FALSE': 'boolean',
    'AND': 'logical and', 'OR': 'logical or', 'NOT': 'logical not',
    'WAIT': 'wait seconds',
};

let _acPopup = null;   // the floating <div>
let _acList = [];     // current filtered suggestions
let _acIdx = 0;      // highlighted row index

// ── Initialise (called once from initEditor) ──────────────────────
function initAutoComplete(textarea) {
    _acPopup = document.createElement('div');
    Object.assign(_acPopup.style, {
        position: 'fixed',
        zIndex: '99999',
        background: '#1e1e2e',
        border: '1px solid #444466',
        borderRadius: '6px',
        boxShadow: '0 6px 24px rgba(0,0,0,0.6)',
        overflow: 'hidden',
        display: 'none',
        minWidth: '210px',
        maxWidth: '340px',
        fontFamily: "'Cascadia Code','Fira Code','Consolas',monospace",
        fontSize: '13px',
        userSelect: 'none',
    });
    document.body.appendChild(_acPopup);

    // Show / update on every character typed
    textarea.addEventListener('input', () => acOnInput(textarea));

    // Navigation + accept keys — use capture so we intercept before
    // the Tab handler in the main keydown listener
    textarea.addEventListener('keydown', e => acOnKeydown(e, textarea), true);

    // Delay hide so a mousedown on a suggestion item fires first
    textarea.addEventListener('blur', () => setTimeout(acHide, 130));
}

// ── Triggered on each input event ────────────────────────────────
function acOnInput(textarea) {
    const word = acCurrentWord(textarea);

    if (!word) { acHide(); return; }

    const upper = word.toUpperCase();
    // Only suggest keywords that start with what the user typed,
    // and only when the match is incomplete (exclude exact matches)
    _acList = AC_KEYWORDS.filter(k => k.startsWith(upper) && k !== upper);

    if (_acList.length === 0) { acHide(); return; }

    _acIdx = 0;
    acRender(textarea);
    acPosition(textarea);
    _acPopup.style.display = 'block';
}

// ── Keyboard navigation inside the popup ─────────────────────────
function acOnKeydown(e, textarea) {
    if (!_acPopup || _acPopup.style.display === 'none') return;

    switch (e.key) {
        case 'ArrowDown':
            e.preventDefault(); e.stopPropagation();
            _acIdx = (_acIdx + 1) % _acList.length;
            acRender(textarea);
            break;

        case 'ArrowUp':
            e.preventDefault(); e.stopPropagation();
            _acIdx = (_acIdx - 1 + _acList.length) % _acList.length;
            acRender(textarea);
            break;

        case 'Tab':
        case 'Enter':
            if (_acList.length > 0) {
                e.preventDefault(); e.stopPropagation();
                acAccept(textarea, _acList[_acIdx]);
            }
            break;

        case 'Escape':
            e.preventDefault(); e.stopPropagation();
            acHide();
            break;
    }
}

// ── Extract the word currently being typed at the caret ──────────
function acCurrentWord(textarea) {
    const before = textarea.value.substring(0, textarea.selectionStart);
    const m = before.match(/[A-Za-z_][A-Za-z0-9_]*$/);
    return m ? m[0] : '';
}

// ── Replace the partial word with the accepted keyword ───────────
function acAccept(textarea, keyword) {
    const pos = textarea.selectionStart;
    const before = textarea.value.substring(0, pos);
    const after = textarea.value.substring(pos);
    const m = before.match(/[A-Za-z_][A-Za-z0-9_]*$/);
    if (!m) return;

    const start = pos - m[0].length;
    textarea.value = textarea.value.substring(0, start) + keyword + after;
    textarea.selectionStart = textarea.selectionEnd = start + keyword.length;

    updateHighlight(textarea.value);
    updateLineNumbers(textarea.value);
    acHide();
    textarea.focus();
}

// ── Hide and reset the popup ──────────────────────────────────────
function acHide() {
    if (_acPopup) _acPopup.style.display = 'none';
    _acList = [];
}

// ── Rebuild the popup's inner HTML ───────────────────────────────
function acRender(textarea) {
    if (!_acPopup) return;

    _acPopup.innerHTML = _acList.map((kw, i) => {
        const sel = i === _acIdx;
        const hint = AC_HINTS[kw] ?? '';
        return `<div data-i="${i}" style="
            display:         flex;
            align-items:     center;
            justify-content: space-between;
            gap:             14px;
            padding:         5px 10px;
            cursor:          pointer;
            background:      ${sel ? '#1a3a5c' : 'transparent'};
            border-left:     2px solid ${sel ? '#4ec9b0' : 'transparent'};
        ">
            <span style="color:${sel ? '#4ec9b0' : '#9cdcfe'};font-weight:${sel ? '700' : '400'}">
                ${kw}
            </span>
            ${hint
                ? `<span style="color:#44446a;font-size:11px;flex-shrink:0">${hint}</span>`
                : ''}
        </div>`;
    }).join('');

    // Mousedown (not click) so the event fires before the blur on the textarea
    _acPopup.querySelectorAll('[data-i]').forEach(el => {
        el.addEventListener('mousedown', e => {
            e.preventDefault();
            acAccept(textarea, _acList[parseInt(el.dataset.i, 10)]);
        });
    });
}

// ── Position the popup just below the current caret ──────────────
// Uses the known CSS constants from Home.razor.css so no DOM
// measurement is needed: font 14px, line-height 1.6, padding 12px/16px.
function acPosition(textarea) {
    const FONT = 14;
    const LINE_H = FONT * 1.6;      // 22.4 px
    const PAD_TOP = 12;
    const PAD_LEFT = 16;
    const CHAR_W = FONT * 0.601;    // Cascadia Code / Consolas at 14px

    const pos = textarea.selectionStart;
    const before = textarea.value.substring(0, pos);
    const lines = before.split('\n');
    const lineNo = lines.length - 1;            // 0-based
    const col = lines[lineNo].length;

    const rect = textarea.getBoundingClientRect();
    let left = rect.left + PAD_LEFT + col * CHAR_W;
    let top = rect.top + PAD_TOP + (lineNo + 1) * LINE_H - textarea.scrollTop;

    // Keep popup inside the viewport horizontally
    left = Math.min(left, window.innerWidth - 350);

    // Flip above the caret if it would overflow the bottom edge
    const approxH = _acList.length * 28 + 6;
    if (top + approxH > window.innerHeight - 8) {
        top = rect.top + PAD_TOP + lineNo * LINE_H - textarea.scrollTop - approxH;
    }

    _acPopup.style.left = `${Math.max(4, left)}px`;
    _acPopup.style.top = `${Math.max(4, top)}px`;
}

// ════════════════════════════════════════════════════════════════
//  Drag-to-resize
// ════════════════════════════════════════════════════════════════
export function initResizer(handleId, leftPaneId, rightPaneId) {
    const handle = document.getElementById(handleId);
    const leftPane = document.getElementById(leftPaneId);
    const rightPane = document.getElementById(rightPaneId);
    if (!handle || !leftPane || !rightPane) {
        console.error('initResizer: missing elements', { handleId, leftPaneId, rightPaneId });
        return;
    }

    const container = leftPane.parentElement;
    let dragging = false, startX = 0, startWidth = 0;

    const getClientX = e => e.touches ? e.touches[0].clientX : e.clientX;

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
        const pct = ((startWidth + getClientX(e) - startX) /
            container.getBoundingClientRect().width) * 100;
        if (pct > 15 && pct < 85) leftPane.style.width = `${pct}%`;
        e.preventDefault();
    }

    function onEnd() {
        if (!dragging) return;
        dragging = false;
        handle.classList.remove('dragging');
        document.body.style.cursor = '';
        document.body.style.userSelect = '';
        fitCanvasToWrapper();
    }

    handle.addEventListener('mousedown', onStart);
    document.addEventListener('mousemove', onMove);
    document.addEventListener('mouseup', onEnd);
    handle.addEventListener('touchstart', onStart, { passive: false });
    document.addEventListener('touchmove', onMove, { passive: false });
    document.addEventListener('touchend', onEnd);

    console.log('initResizer: ready');
}