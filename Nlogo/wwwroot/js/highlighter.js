// ── Token definitions ────────────────────────────────────────────
const TOKENS = [
    {
        type: 'comment',
        pattern: /;[^\n]*/
    },
    {
        type: 'keyword',
        pattern: /\b(FORWARD|FD|BACKWARD|BK|RIGHT|RT|LEFT|LT|PENUP|PU|PENDOWN|PD|HOME|CLEARSCREEN|CS|SETCOLOR|SETPENCOLOR|SETPC|SETWIDTH|SETPENSIZE|SETPOS|SHOWTURTLE|ST|HIDETURTLE|HT|REPEAT|FOREVER|IF|IFELSE|WHILE|STOP|OUTPUT|OP|TO|END|MAKE|LOCAL|THING|AND|OR|NOT|PRINT|SHOW|TYPE|RANDOM|SIN|COS|TAN|ARCTAN|SQRT|ABS|ROUND|INT|POWER|LOG|EXP|TRUE|FALSE|WAIT|LABEL)\b/i
    },
    {
        type: 'string',
        pattern: /"[^\s\[\]()]*/
    },
    {
        type: 'deref',
        pattern: /:[A-Za-z_][A-Za-z0-9_]*/
    },
    {
        type: 'number',
        pattern: /-?\d+(\.\d+)?/
    },
    {
        type: 'bracket',
        pattern: /[\[\]()]/
    },
    {
        type: 'operator',
        pattern: /[+\-*\/^%=<>]+/
    },
];

// ── Escape HTML so injected text can't break the DOM ─────────────
function escapeHtml(text) {
    return text
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;');
}

// ── Main highlight function ───────────────────────────────────────
export function highlight(source) {
    let output = '';
    let pos = 0;

    while (pos < source.length) {
        // Try each token pattern at the current position
        let matched = false;

        for (const { type, pattern } of TOKENS) {
            // Anchor pattern to current position
            const anchored = new RegExp('^(?:' + pattern.source + ')', 'i');
            const slice = source.slice(pos);
            const match = anchored.exec(slice);

            if (match) {
                output += `<span class="tok-${type}">${escapeHtml(match[0])}</span>`;
                pos += match[0].length;
                matched = true;
                break;
            }
        }

        // No token matched — emit the character as-is (whitespace, newlines etc.)
        if (!matched) {
            const ch = source[pos];
            output += ch === '\n' ? '\n' : escapeHtml(ch);
            pos++;
        }
    }

    // The mirror <pre> needs a trailing newline to keep scroll in sync
    return output + '\n';
}