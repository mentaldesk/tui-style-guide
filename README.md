# MentalDesk TUI style guide

How terminal UIs in this org should look and behave, so that requirements, implementations and
reviews all start from the same assumptions.

Read this **before writing a requirement that describes UI**, and **before building one**. It is
deliberately small: it covers the things that keep coming back in review, not everything a UI can
do. It grows by pull request as new ones come back (see *Extending this guide*).

The framework is [Terminal.Gui](https://tui-cs.github.io/Terminal.Gui/docs/index.html) v2. Rules
that are really framework mechanics say so; the rest are design rules that would hold in any TUI.

**Contents**

1. [Build from the framework's widgets](#1-build-from-the-frameworks-widgets)
2. [One affordance per action](#2-one-affordance-per-action)
3. [Errors and status](#3-errors-and-status)
4. [Icons and glyphs](#4-icons-and-glyphs)
5. [Keys and focus](#5-keys-and-focus)
6. [Writing requirements](#6-writing-requirements)

---

## 1. Build from the framework's widgets

**Reach for a [built-in view](https://tui-cs.github.io/Terminal.Gui/docs/views) before writing your
own.** A built-in already has the focus handling, mouse handling, theming and keyboard conventions
that a bespoke control has to reinvent and usually gets half right.

Pick the control that matches the *shape of the input*, not the one that is easiest to draw:

| The input is | Use |
| --- | --- |
| On or off | `CheckBox` |
| One of a few choices, all worth showing | `OptionSelector<T>` |
| Several independent on/off flags | `FlagSelector<T>` |
| A number in a range | `NumericUpDown<T>`, with a minimum and maximum |
| One of many choices | `DropDownList<T>` or `ListView` |
| One of many, with hierarchy | `TreeView` |
| A short free-text value | `TextField` |
| Multi-line free text | `TextView` |
| Progress of a known-length job | `ProgressBar` |

**Style a built-in before you replace it.** Most of what looks like "we need a custom control" is a
property: `NoDecorations`, `NoPadding`, `ShadowStyle`, `SchemeName`, `Orientation`, `TabBehavior`.

**A custom view needs a reason, stated in the PR** — name the built-in you rejected and what it
could not do. TuiCode's `LogView` is custom only because `TextView` cannot scroll without moving its
cursor; that sentence is the whole bar to clear.

## 2. One affordance per action

**Never give the same action two affordances in the same view.** A dialog with a `Submit` button
*and* a hint reading `Ctrl+Enter submit` is telling the user the same thing twice and asking them to
work out whether it is one thing or two.

**Prefer the hint, and make it clickable.** Hint text is more compact than a button, it teaches the
keyboard shortcut, and it can still be clicked. A Terminal.Gui `Button` with its decorations turned
off *is* a clickable hint:

```csharp
private static Button Hint(string text, Pos x) => new()
{
    Text = text,
    X = x,
    Y = Pos.AnchorEnd(1),
    NoDecorations = true,
    NoPadding = true,
    ShadowStyle = ShadowStyles.None,
    HotKeySpecifier = (Rune)0xffff,   // the hint names its own key; don't also claim a hotkey
};
```

Use a real, decorated `Button` only where there is no key to name — a toolbar action, or a choice
that has no sensible shortcut.

### The hint bar

Hints belong on the **last row of the view**, anchored with `Pos.AnchorEnd(1)`, separated by
` · ` (space, U+00B7, space).

Write each hint as **key first, then a lower-case verb phrase**: the key is what the user is
scanning for.

```
Type to filter · Up/Down/PgUp/PgDn · Enter compare · Esc cancel
Ctrl+Enter submit · Esc cancel
```

- Order by how often the hint is used, with **cancel last**.
- Name keys as the terminal reports them: `Ctrl+Enter`, `Esc`, `Up/Down`, `PgUp/PgDn`.
- Leave out keys that every view has (`Tab` to move focus). Name the ones specific to this view.
- Keep it to one row. If the hints don't fit, the view is doing too much.

> Known divergence to converge on: TuiCode's status bar currently separates its items with
> `  •  `. New work uses ` · `.

## 3. Errors and status

**An error must never be interleaved with the controls.** Growing a `Label` in place pushes it over
whatever sits below, and an error that lands between the buttons reads as part of them.

**Give the dialog a dedicated message block at its foot, below the hints**, spanning the full width,
occupying zero rows while there is nothing to say. When a message arrives, **the dialog grows** and
the content above it shrinks — the message never steals the hint row.

TuiCode's [`AlertView`](https://github.com/mentaldesk/TuiCode/blob/main/src/TuiCode.Workbench/Controls/AlertView.cs)
is the reference implementation: it word-wraps to as many rows as the message needs, reports that
count as `Lines` so the dialog can re-lay itself out, and picks its colours from the theme by
severity rather than hard-coding them.

| Severity | Scheme | For |
| --- | --- | --- |
| `Error` | `Error` | The action was refused or failed |
| `Info` | `Accent` | Progress, or a fact the user needs before acting |

Rules that matter more than the widget:

- **Keep what the user typed.** A refused action leaves the dialog open with its input intact. Never
  clear a form because the server said no.
- **Say what happened, in the app's terms.** One line. The first line of the tool's stderr beats an
  exception type; a stack trace belongs in the log, never on screen.
- **Put focus where the fix is.** A missing summary focuses the summary field.
- **Show busy, and refuse a second go.** `Submitting…` in the message block, with the confirm
  disabled until it resolves.
- **Outside a dialog, errors go to the status bar** — same wording rules, one line. Don't open a
  modal to report something the user did not just ask for.

## 4. Icons and glyphs

**Use [Nerd Font](https://www.nerdfonts.com/cheat-sheet) glyphs where an icon does real work.** An
icon earns its place when it *classifies* (this row is a folder, this one a C# file) or
*disambiguates* at a glance. Decoration does not earn its place — a glyph on every label makes the
ones that mean something invisible.

- **One vocabulary per app.** Reuse a glyph already in use before picking a new one, and pick from
  one Nerd Font set (`nf-md-*`, say) rather than mixing.
- **Always have a fallback.** No terminal reports its font, so assume some users have no Nerd Font:
  fall back to emoji, then to plain text. Make it a user-visible setting where icons are prominent.
- **Draw the icon; don't put it in the text.** Prepend the glyph's cells at draw time so the item's
  text stays the bare name — otherwise filtering, sorting and type-to-jump all match against the
  glyph. See TuiCode's
  [`FileIcons`](https://github.com/mentaldesk/TuiCode/tree/main/src/TuiCode.Icons).
- **Budget the cells.** Nerd Font glyphs are one cell; emoji are two. Lay out for the fallback you
  actually ship, or columns shift when the style changes.
- **Colour comes from the theme**, and the icon keeps its row's background so selection still reads.

## 5. Keys and focus

- **`Esc` cancels. Always.** It never does anything else.
- **`Enter` confirms a single-line dialog; `Ctrl+Enter` confirms one with a multi-line field**, where
  plain `Enter` has to insert a newline.
- **Every action is reachable from the keyboard.** The mouse is a convenience, never the only way.
- **Tab moves focus, everywhere.** In a `TextView` inside a dialog, set `TabKeyAddsTab = false` so
  Tab leaves the field instead of typing into it.
- **Focus lands where work starts** when a view opens — the filter field, the summary, the first row.
- A container `View` that hosts focusable children needs `CanFocus = true`; `SetFocus()` silently
  returns false when any ancestor has it off.

### Showing focus

**Focus has one owner, and that owner is not the framework's `HasFocus`.** Keep the focused
*region* in one place and re-read it from the view the framework reports as focused, on every
iteration and before every key — a mouse click moves focus between iterations. TG leaves `HasFocus`
set on a view that focus has moved on from, so reading it view by view gives you two views that
both claim the keyboard. `Navigation.GetFocused()` can also return an *ancestor* of the view
actually holding keys, so walk down to the innermost with `View.MostFocused`.

**Put it on screen twice: in colour and in a word.** The focused pane draws its border in the
theme's focus colour, and a word naming the region sits at the far left of the status bar, always
present. The colour is what you notice; the word is what still works when the theme is pale, the
terminal profile overrides, or the user can't tell the two colours apart. Without either, a focus
move that went somewhere unintended is invisible until the user types.

- Framework mechanic: TG draws border lines in `VisualRole.Normal` whatever has focus. Swap the
  role as the attribute is resolved — a `GettingAttributeForRole` hook mapping `Normal`→`Focus` and
  `HotNormal`→`HotFocus` — rather than overriding the pane's scheme, which has to be reapplied on
  every theme change.
- Don't read the framework's button painting as the answer either: TG draws the first `Button` in
  the `Focus` attribute whether or not it has focus, so the thing that looks focused is often the
  wrong one.

TuiCode's [`FocusService`](https://github.com/mentaldesk/TuiCode/blob/main/src/TuiCode.Workbench/Focus/FocusService.cs)
and [`FocusBorder`](https://github.com/mentaldesk/TuiCode/blob/main/src/TuiCode.Workbench/Focus/FocusBorder.cs)
are the reference implementation. `FocusService` is framework-free and unit-tested directly: the
host registers each region with a move and an ownership test and supplies the focused view.

### The caret

**The caret is the terminal's own cursor.** Colour it from the theme with `OSC 12` on startup
(`editorCursor.foreground`; no TG scheme covers it) and restore the terminal's own with `OSC 112`
on exit. Terminals that don't support it ignore the sequence.

**Paint a caret only where the terminal cannot draw one** — a second caret, say, in a terminal
without [kitty's multiple cursors protocol](https://sw.kovidgoyal.net/kitty/multiple-cursors-protocol/).
Then three rules, all of them things that have come back in review:

- **Paint every caret and hide the terminal cursor.** One painted caret beside one real one is two
  different-looking things on screen claiming to be the same thing.
- **A caret is a bar before the insertion point, everywhere.** The terminal cursor is a bar, so a
  painted one is a bar too — the shape a user sees must not depend on which terminal they have, on
  how many carets are on screen, or on whether they are in the editor or a dialog. An underline
  vanishes under an underscore; a reversed cell reads as a selection.
- **Invalidate the view whenever a caret moves.** A painted caret only moves when the view redraws.
  A caret that snaps into place only once the user types is a missing `SetNeedsDraw()`, not a
  drawing bug — the terminal cursor hid this, because the framework moves that one without a
  repaint.

TuiCode's [`EditorTextView.Carets.cs`](https://github.com/mentaldesk/TuiCode/blob/main/src/TuiCode.Editor/EditorTextView.Carets.cs)
and [`TerminalCursors`](https://github.com/mentaldesk/TuiCode/blob/main/src/TuiCode.Editor/TerminalCursors.cs)
are the reference implementation. A dialog's field gets this by reusing them, not by writing a
second caret — which is also how it stays one shape.

## 6. Writing requirements

A requirement that describes UI is not done until a reader can build it without guessing.

**Name the control for every element**, using the names in section 1, and **sketch it**. The sketch
carries the layout; the control names carry the behaviour.

```
┌─ Submit review on #187 ────────────────────────────────┐
│ (•) Comment  ( ) Approve  ( ) Request changes          │   OptionSelector<T>, horizontal
│                                                        │
│ ┌────────────────────────────────────────────────────┐ │
│ │ Summary…                                           │ │   TextView, word wrap; focus starts here
│ └────────────────────────────────────────────────────┘ │
│ Ctrl+Enter submit · Esc cancel                         │   clickable hints, ` · ` separated
└────────────────────────────────────────────────────────┘
```

Sketch conventions: `[x]` / `[ ]` checkbox, `(•)` / `( )` option, `[ 2 ▲▼]` numeric up/down,
`[ Button ]` button, `▸` collapsed tree node, `…` placeholder text.

Then say, in prose:

- **The hint bar**, verbatim — it is part of the design, not a detail for the implementer.
- **Where focus starts**, and what `Enter` and `Esc` do.
- **What happens when it fails.** Which message, shown where, and what survives. A requirement that
  only describes the happy path gets an error path invented in review.
- **Which state is remembered** across opens, if any.

---

## Extending this guide

Add a rule when a review comment has had to make the same point twice. A rule here should be
**short, imperative, and about a decision someone will actually face** — if it cannot be violated,
it does not need writing down.

Open a pull request. Include the review comment that prompted it in the PR description, not in the
guide: the guide says what to do, the PR says why.

Repos that follow this guide link to it from their `AGENTS.md`:
[mentaldesk/TuiCode](https://github.com/mentaldesk/TuiCode),
[mentaldesk/a-team](https://github.com/mentaldesk/a-team).
