# 🐢 NLogo

> A modern Logo programming language IDE built with .NET 10 MAUI Blazor Hybrid — designed to teach students the fundamentals of programming through turtle graphics.

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-Latest-239120?style=flat-square&logo=csharp)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![MAUI](https://img.shields.io/badge/MAUI-Blazor%20Hybrid-512BD4?style=flat-square&logo=dotnet)](https://docs.microsoft.com/en-us/dotnet/maui/)
[![MudBlazor](https://img.shields.io/badge/UI-MudBlazor-594AE2?style=flat-square)](https://mudblazor.com/)
[![License](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](LICENSE)

---

## 📖 Overview

**NLogo** is an educational development environment that brings the classic [Logo programming language](https://en.wikipedia.org/wiki/Logo_(programming_language)) into a modern cross-platform desktop application. Students can write Logo code, see a turtle draw their commands in real time, and learn core programming concepts — all within a polished, beginner-friendly interface.

Logo's intuitive turtle graphics model makes abstract concepts like loops, variables, and procedures immediately visual and tangible, making it an ideal first programming language.

---

## ✨ Features

### 🖊️ Code Editor
- **Syntax highlighting** — keywords, values, strings, and comments are colour-coded in real time
- **Live feedback** — errors are surfaced immediately in the console output panel
- Transparent textarea overlay with a mirrored `<pre>` element for smooth highlighting without cursor disruption

### 🐢 Turtle Graphics Canvas
- HTML5 `<canvas>`-powered rendering via JavaScript interop
- Real-time drawing as code executes
- Resizable split-pane layout with drag-to-resize support
- Canvas resets cleanly between runs

### ⚙️ Compiler Pipeline
| Stage | Description |
|---|---|
| **Lexer** | Tokenises raw Logo source code into a structured token stream |
| **Parser** | Recursive descent parser that builds a full Abstract Syntax Tree (AST) |
| **Interpreter** | Tree-walking interpreter with variable scoping and procedure support |
| **JS Interop** | Bridges the .NET interpreter to the HTML5 canvas for drawing commands |

### 🖥️ IDE Layout
- **Left panel** — code editor (top) and console output (bottom) at equal heights
- **Right panel** — full-height turtle graphics canvas
- Drag-resize divider between panels
- `ResizeObserver`-driven responsive layout

---

## 🧠 Supported Logo Commands

### Movement
| Command | Description |
|---|---|
| `FORWARD n` / `FD n` | Move turtle forward *n* steps |
| `BACKWARD n` / `BK n` | Move turtle backward *n* steps |
| `RIGHT n` / `RT n` | Turn right *n* degrees |
| `LEFT n` / `LT n` | Turn left *n* degrees |
| `SETPOS [x y]` | Move turtle to absolute position |
| `HOME` | Return turtle to origin |

### Pen Control
| Command | Description |
|---|---|
| `PENUP` / `PU` | Lift pen (move without drawing) |
| `PENDOWN` / `PD` | Lower pen (draw while moving) |
| `SETPENCOLOR n` | Set pen colour by index |
| `SETPENSIZE n` | Set pen stroke width |

### Drawing
| Command | Description |
|---|---|
| `CLEARSCREEN` / `CS` | Clear the canvas and reset turtle |
| `CLEAN` | Clear drawing, keep turtle position |
| `HIDETURTLE` / `HT` | Hide the turtle cursor |
| `SHOWTURTLE` / `ST` | Show the turtle cursor |

### Control Flow
| Command | Description |
|---|---|
| `REPEAT n [ ... ]` | Repeat a block *n* times |
| `IF condition [ ... ]` | Conditional execution |
| `IFELSE cond [ ... ] [ ... ]` | If/else branching |
| `WHILE condition [ ... ]` | While loop |

### Variables & Procedures
| Command | Description |
|---|---|
| `MAKE "name value` | Assign a variable |
| `THING "name` / `:name` | Read a variable |
| `TO name ... END` | Define a procedure |
| `OUTPUT value` | Return a value from a procedure |

### Output
| Command | Description |
|---|---|
| `PRINT value` | Print to console output |
| `SHOW value` | Show value in console |

---

## 🚀 Getting Started

### Prerequisites

| Requirement | Version |
|---|---|
| Visual Studio | 2026 (or 2022 17.x+) |
| .NET SDK | 10.0 |
| MAUI Workload | Latest |
| WebView2 Runtime | Latest (Windows) |

### Install the MAUI Workload

```bash
dotnet workload install maui
```

### Clone & Run

```bash
git clone https://github.com/your-username/nlogo.git
cd nlogo
dotnet restore
```

Open `NLogo.sln` in Visual Studio 2026, set the startup project, and press **F5**.

---

## 🗂️ Project Structure

```
NLogo/
├── Components/
│   ├── Pages/
│   │   └── Home.razor          # Main IDE layout
│   └── Layout/
├── Compiler/
│   ├── Lexer.cs                # Tokeniser
│   ├── Token.cs                # Token definitions
│   ├── Parser.cs               # Recursive descent parser
│   ├── AstNodes.cs             # AST node types
│   └── Interpreter.cs          # Tree-walking interpreter
├── wwwroot/
│   ├── js/
│   │   ├── turtle.js           # Canvas drawing & turtle state
│   │   └── editor.js           # Syntax highlighting logic
│   └── css/
│       └── app.css             # Global styles (incl. highlight colours)
├── MauiProgram.cs
└── NLogo.csproj
```

---

## 🛠️ Architecture Notes

### Why MAUI Blazor Hybrid?
MAUI Blazor Hybrid lets us use web technologies (HTML5 canvas, CSS, JavaScript) for the rendering layer while keeping a full .NET backend for the compiler pipeline. This means the NLogo compiler is pure C# with no browser limitations, while the visual output leverages the mature web rendering engine.

### CSS Isolation Caveat
Blazor's scoped CSS (`.razor.css` files) does **not** apply to dynamically injected DOM elements like the syntax highlighting `<span>` tags. All highlight styles live in the **global** `app.css` to avoid this scoping issue.

### JavaScript Ownership of the Editor
The syntax-highlighted editor uses a transparent `<textarea>` overlaid on a `<pre>` element. JavaScript fully owns textarea input handling to prevent Blazor re-renders from resetting the cursor position mid-type.

### WebView2 DevTools
During development, WebView2 DevTools can be enabled for debugging JavaScript and inspecting the DOM inside the MAUI WebView — invaluable for diagnosing rendering vs. logic issues.

---

## ⚠️ Known Limitations

### Language Coverage
- **No turtle graphics animation** — drawing is currently synchronous; there is no step-through playback or speed control
- **No built-in math functions** — advanced operations like `SIN`, `COS`, `SQRT` may not be implemented yet
- **Limited error recovery** — the parser stops at the first syntax error rather than continuing and reporting multiple errors
- **No file I/O** — Logo programs cannot read from or write to files

### Platform
- **Windows only (currently)** — MAUI targets multiple platforms, but the WebView2 dependency and canvas interop have only been tested on Windows
- **No mobile support** — the split-pane drag-resize layout is not optimised for touch/mobile screen sizes
- **WebView2 required** — Windows users need the WebView2 Runtime installed (ships with Windows 11; may need manual install on Windows 10)

### IDE Features
- **No step-through debugger** — code runs to completion; there is no breakpoint or step-over functionality
- **No built-in example snippets** — users start from a blank editor
- **No undo/redo history** — the textarea does not implement a custom undo stack beyond the browser default
- **No project save/open** — code is not persisted between sessions
- **No auto-complete / IntelliSense** — keyword suggestions are not implemented
- **Single file only** — the IDE does not support multi-file Logo projects or `LOAD` commands that reference external files

### Performance
- **Large programs may be slow** — the interpreter is a simple tree-walker; very deep recursion or extremely large `REPEAT` loops may feel sluggish
- **Canvas does not scale with DPI** — on high-DPI displays the canvas may appear slightly blurry

---

## 🗺️ Roadmap

- [ ] Step-through debugger with breakpoints
- [ ] Animated turtle movement with speed control
- [ ] Save / open `.logo` source files
- [ ] Built-in example programs
- [ ] Auto-complete for Logo keywords
- [ ] macOS and Linux support
- [ ] Export canvas as PNG/SVG
- [ ] Full math function library (`SIN`, `COS`, `SQRT`, `ABS`, etc.)
- [ ] Multi-error reporting across the entire file
- [ ] Dark / light theme toggle

---

## 🤝 Contributing

Contributions are welcome! Please open an issue first to discuss what you'd like to change.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

---

## 📚 Resources

- [Logo Foundation](https://el.media.mit.edu/logo-foundation/) — history and philosophy of Logo
- [UCBLogo Manual](https://people.eecs.berkeley.edu/~bh/logo.html) — reference for standard Logo commands
- [.NET MAUI Blazor Hybrid Docs](https://learn.microsoft.com/en-us/aspnet/core/blazor/hybrid/tutorials/maui)
- [MudBlazor Component Library](https://mudblazor.com/)

---

## 📄 License

This project is licensed under the **MIT License** — see the [LICENSE](LICENSE) file for details.

---

<div align="center">
  <sub>Built with ❤️ to make programming accessible to everyone.</sub>
</div>
