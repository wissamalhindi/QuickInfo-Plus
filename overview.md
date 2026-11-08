Extension to add what the C# Quick Info tooltip leaves out of XML documentation comments: your `<example>` sections and the
descriptions of your `<exception>` tags — with clickable, summary-colored references and copyable code.

Works in **Visual Studio 2026 (18.x)** and **Visual Studio 2022 (17.x)**, C# only.

![QuickInfo Plus tooltip with Exceptions and Examples sections](src/QuickInfoPlus/Resources/tooltip.png)

## What it adds to the tooltip

Hover a C# method, constructor, property or type. Below VS's own tooltip, QuickInfo Plus adds two collapsible
sections:

- **Exceptions** — each `<exception cref>` with its description beside the type (VS's own list shows the types
  only). Left out when no exception has a description.
- **Examples** — the `<example>` text and `<code>` blocks.
- **Click a header** to expand or collapse a section. Each section remembers its state until Visual Studio
  closes and starts collapsed when VS starts.
- **References read like VS's summary**: `<see cref>` shows as `FileHelper.Exists(string)`, colored like the
  summary, with each part (`FileHelper`, `Exists`, `string`) its own link — click for Go To Definition, hover for
  a hint. `<typeparamref>` shows the actual type at the call (`int` at `Map<int>(1)`).
- **Web links** (`<see href="https://…">`) open in your browser. Other link types stay plain text.
- **Code blocks** keep their line breaks and relative indentation, use your editor font, and have an accent bar
  on the left. Select code with the mouse and press **Ctrl+C**, or click the **copy icon** at the right to copy the
  whole block. Right-click is ignored inside code.
- `<para>`, `<br/>` and `<list>` (bullets, numbers, term – description) are laid out like VS's summary.
- Works for your own code and for referenced assemblies / NuGet packages that ship an XML documentation file,
  including generic and extension-method calls and `using` aliases.

## Options

**Tools ▸ Options ▸ QuickInfo Plus ▸ General** (in VS 2026 the entry opens the classic options dialog):

- **Show Exceptions section** — on by default.
- **Show Examples section** — on by default.
- **Examples section ▸ Maximum height (px)** — 300 by default, 100 to 1000. Longer examples scroll.

Changes apply on the next hover; no restart.

## Install

Install from this page or from **Extensions ▸ Manage Extensions** in Visual Studio, then restart Visual Studio.

If the sections or the **QuickInfo Plus** Options page don't appear after installing, close Visual Studio, run
`devenv /updateconfiguration` once from a Developer Command Prompt, and start Visual Studio again.

## Known limits

- The sections appear **at the bottom** of the tooltip: an extension cannot insert text between VS's own
  signature, summary and exception list. Exception types therefore appear twice (VS's list and ours).
- The sections take the width of VS's part of the tooltip (at least 300 px). The Examples section is at most
  **300 px** high by default (adjustable in Options); longer examples scroll (mouse wheel anywhere, Shift + wheel
  sideways over code). The tooltip never grows past the top or bottom of the screen: if the screen is too small,
  Examples gets shorter first, and Exceptions scrolls only when even that is not enough. VS's own part cannot be
  shortened by an extension.
- Code blocks are not syntax-colored.
- `<inheritdoc/>` and `<include>` are not resolved.

## Source and issues

Source code, release notes and issue tracker:
[github.com/wissamalhindi/QuickInfo-Plus](https://github.com/wissamalhindi/QuickInfo-Plus) — report bugs and ask
questions in [Issues](https://github.com/wissamalhindi/QuickInfo-Plus/issues). MIT license.
