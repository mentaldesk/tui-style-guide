# MentalDesk TUI style guide

How terminal UIs in this org should look and behave, so that requirements, implementations and
reviews all start from the same assumptions.

Read this **before writing a requirement that describes UI**, and **before building one**. It is
deliberately small: it covers the things that keep coming back in review, not everything a UI can
do. It grows by pull request as new ones come back (see *Extending this guide*).

The framework is [Terminal.Gui](https://tui-cs.github.io/Terminal.Gui/docs/index.html) v2. Rules
that are really framework mechanics say so; the rest are design rules that would hold in any TUI.

**Using this guide.** The rules are here. Each one is also built, once, in
[`MentalDesk.Tui`](https://github.com/mentaldesk/tui-style-guide/tree/main/src/MentalDesk.Tui), the shared library, and shown working in
[Swatch](https://github.com/mentaldesk/tui-style-guide/tree/main/src/Swatch), a theme editor that exists to exercise them
(`dotnet run --project src/Swatch`). To try a theme for real, select it and use *Theme › Try it*
(`Ctrl+T T`, or `Enter` on the preview): a small sample app opens in that theme, with live menus,
dialogs and buttons, and `Esc` brings you back. **When you build something a rule covers, start from the
library type its section names**, not from TuiCode's or a-team's own version: those are the older
copies the library was taken from. Where a section still names TuiCode or a-team code, the library
doesn't have that piece yet. Apps reference the library as the
[`MentalDesk.Tui`](https://www.nuget.org/packages/MentalDesk.Tui) package rather than copying from it.

**Contents**

1. [Build from the framework's widgets](#1-build-from-the-frameworks-widgets)
2. [One affordance per action](#2-one-affordance-per-action)
3. [Errors and status](#3-errors-and-status)
4. [Icons and glyphs](#4-icons-and-glyphs)
5. [Keys and focus](#5-keys-and-focus)
6. [Scrolling](#6-scrolling)
7. [Writing requirements](#7-writing-requirements)

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

**Each view picks one place for its keys: on its buttons, or in its hint bar.** Never both, and
never a mix: if one action shows its key, every action does, Cancel included. Wherever the key is
shown, **it comes first**: `Del delete` in a hint, `▓ Del Delete ▓` on a button.

- **In a dialog of buttons, prefer the buttons**, with no hint bar: hints would only repeat each
  button's label.
- **Use the hint bar where there are no buttons**, or where a key on every button would crowd them:
  a picker, a dialog with many buttons. The main screen is different: see *The status bar*. Make
  the hints clickable. A Terminal.Gui `Button` with its decorations turned off *is* a clickable hint:

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


### Buttons

- **A button is a filled block, not bracketed text.** No `[ ]`, one space of padding either side.
- **One Primary per view, at most.** If `Enter` does anything, it's the Primary.
- **Danger is for destructive actions only**: delete, discard, overwrite. If the destructive action
  is the only one besides Cancel, it's still Danger, not Primary, and it's never the default.
- **Everything else is Secondary**, including Cancel.
- **Focus is shown by colour and in bold**, separately from emphasis, so a focused Secondary button
  is still obviously focused. **Hover** lightens the button without moving focus.

- Framework mechanic: with `NoDecorations` the stock `Button` drops its padding too, and padding the
  view instead leaves the padding in the unfocused colour. Pad the text.

The library's [`AppButton`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Dialogs/AppButton.cs)
(`AppButton.Primary("Save")`, `.Danger("Delete")`, `.Secondary("Cancel")`) and the `ButtonPrimary`,
`ButtonDanger` and `ButtonSecondary` schemes in each of its
[themes](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Theming/themes.json) are the reference implementation.
Swatch's *Theme › Delete…*, *Remove…* and *Close…* are live dialogs to tab through and hover over.

### The hint bar

Hints belong on the **last row of the view**, anchored with `Pos.AnchorEnd(1)`, separated by
`  •  ` (two spaces, U+2022, two spaces). [`HintRow`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Chrome/HintRow.cs)
lays them out.

Write each hint as **key first, then a lower-case verb phrase**: the key is what the user is
scanning for.

```
Type to filter  •  Up/Down/PgUp/PgDn  •  Enter compare  •  Esc cancel
Ctrl+Enter submit  •  Esc cancel
```

- Order by how often the hint is used, with **cancel last**.
- Name keys as the terminal reports them: `Ctrl+Enter`, `Esc`, `Up/Down`, `PgUp/PgDn`.
- Leave out keys that every view has (`Tab` to move focus). Name the ones specific to this view.
- Keep it to one row. If the hints don't fit, the view is doing too much.

### The status bar

**The main screen's last row is the status bar, and it shows status, not keys**: the focus word
(section 5), errors outside a dialog (section 3), and whatever describes where the user is, such as
the selected item or the caret's row and column.

**Its one hint is `F1 keys`** (section 5). Every other key is in the keys dialog, or in the command
palette, which the keys dialog shows how to open. Without that one hint, none of them can be found.

- **It spans the full width of the screen's last row**, never the window title or the foot of one pane.
- **Give it its own colour**, a `StatusBar` scheme whose background differs from every region above
  it, in every theme. In the content's colours it reads as one more line of content.
- Framework mechanic: TG's built-in `StatusBar` paints in the `Menu` scheme and draws a border
  between its items, so the status bar is a plain one-row `View` in the `StatusBar` scheme instead.

The library's [`AppStatusBar`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Chrome/AppStatusBar.cs),
which `AppShell` gives `F1 keys` and nothing else, and the `StatusBar` scheme in each of its
[themes](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Theming/themes.json) are the reference implementation.

## 3. Errors and status

**An error must never be interleaved with the controls.** Growing a `Label` in place pushes it over
whatever sits below, and an error that lands between the buttons reads as part of them.

**Give the dialog a dedicated message block at its foot, below the hints**, spanning the full width,
occupying zero rows while there is nothing to say. When a message arrives, **the dialog grows** and
the content above it shrinks — the message never steals the hint row.

The library's [`AlertView`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Dialogs/AlertView.cs) is the reference implementation: it
word-wraps to as many rows as the message needs, reports that count as `Lines` so the dialog can
re-lay itself out ([`AppDialog`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Dialogs/AppDialog.cs) grows by it), and picks its colours from
the theme by severity rather than hard-coding them.

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
  modal to report something the user did not just ask for. A pane that failed to load is the
  exception: see [Loading and empty](#loading-and-empty).

### Loading and empty

**A region that waits for data says so in itself**, where its content goes: not on the status bar,
in a header or in the hint row. A message away from the pane outlives the load.

**It's always one of four states**: loading, empty, failed, or its content. Never blank.

- **Loading:** a spinner and `Loading pull requests…`, naming what. Shown only once the load has
  taken longer than a moment, so a fast load doesn't flicker.
- **Empty:** say what isn't there, in the app's terms (`No open pull requests`), dimmed. If there's
  an obvious next step, add it as a hint under it (`N new branch`).
- **Failed:** one line in the `Error` scheme, same wording rules as above, with `R retry` under it.
  A failed load is a state of the pane, so it stays in the pane rather than going to the status bar.
- **Reloading keeps what's there.** The rows stay, and a spinner sits at the right of the region's
  top border until the new rows arrive. A reload that fails keeps the old rows and says so on the
  status bar.
- **The content keeps focus** in every state, so `Tab` order doesn't change as a pane loads.

```
┌┤Pull requests├────────────────────────────┐   ┌┤Pull requests├─────────────────────────⠙──┐
│                                           │   │ #27 Typing to narrow the command palette  │
│        ⠋ Loading pull requests…           │   │ #26 Esc, Space and each action's own key  │
│                                           │   └───────────────────────────────────────────┘
└───────────────────────────────────────────┘
```

The library's [`LoadStateView`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Loading/LoadStateView.cs)
is the reference implementation: it wraps any content view, and the app tells it `ShowLoading()`,
`ShowEmpty(…)`, `ShowFailed(…)` or `ShowContent()`. It centres each state over the content with
`SpinnerView` and the theme's `ReadOnly` role and `Error` scheme, and switches the terminal's own
progress indicator on while loading. In Swatch, *Try it* opens a sample whose Files pane loads, and
its *View* menu reloads it, empty or failing; *Help › Loading states…* shows the same states in a dialog.

### Copying

**Anything the user reads can be copied.** A view with a selection copies it with `Ctrl+C`. One
without offers a *Copy* command for what's under the cursor, on a key and in the menu.

**Copy through the library, so it reaches the laptop over SSH.** `AppShell` installs a clipboard that
sends every copy through the terminal (OSC 52) as well as to the machine's own clipboard, so built-in
controls like `TextField` take that path with no app code. An app's own copy command calls
`shell.Copy(text)`.

**Say what was copied, on the status bar:**

| Outcome | Says | Severity |
| --- | --- | --- |
| The clipboard took it | `Copied 3 lines  •  142 characters` | `Info` |
| Only the terminal took it | `Copied 3 lines  •  142 characters through the terminal` | `Info` |
| Nothing took it | `Copy failed: too large to send through the terminal (the limit is 750 KB)` | `Error` |

Through the terminal is the normal outcome over SSH, not a warning. For it to land, tmux needs
`set -g set-clipboard on`, and iTerm2 needs *Applications in terminal may access clipboard*.

In Swatch, `Ctrl+C` in *Roles*, or *Edit › Copy colours*, copies the selected role's colours.

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
  plain `Enter` has to insert a newline. A dialog's `Enter` is always harmless (see *Dialog buttons*).
- **Every action is reachable from the keyboard.** The mouse is a convenience, never the only way.
- **Tab moves focus, everywhere.** In a `TextView` inside a dialog, set `TabKeyAddsTab = false` so
  Tab leaves the field instead of typing into it.
- **Focus lands where work starts** when a view opens — the filter field, the summary, the first row.
- A container `View` that hosts focusable children needs `CanFocus = true`; `SetFocus()` silently
  returns false when any ancestor has it off.

### Finding keys

**`F1` always opens the keys for here and everywhere, and never means anything else.** *Here* lists
the keys bound to the focused region; *Everywhere* lists the global ones, grouped under the menu's
headings. Both are read from the keymap when it opens, so they can't drift from the real bindings.

- **The main screen's status bar always shows `F1 keys`, and no other hint** (see *The status bar*),
  so it's in the same place in every app.
- **Bind a key that only works in one region to that region's scope**, so it shows under *Here*.
  Its menu item still runs it from anywhere.
- It's a reference, not a runner: nothing in it takes focus, `Esc close` is its only hint, and it
  leaves out disabled commands and commands with no key. The command palette has those.

The library's [`KeysDialog`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Keys/KeysDialog.cs),
which `AppShell` binds to `F1`, is the reference implementation. Swatch binds its `Ctrl+T` theme keys
to the Themes pane.

### Filtering a list

**Every list that narrows as you type matches the same way**, so a user learns it once.

- **CamelHumps, then contains. Case never matters.** Each letter continues the last match or starts
  a new word or hump, as in JetBrains: `gtd`, `GTD` and `gotodef` all find `GoToDefinition`. A space
  makes the next letter start a hump. A row the letters don't match that way still matches if it
  contains them as one run: `iew` finds `GrammarPickerView.cs`. A query with `/` matches path
  segments in order, by CamelHumps only.
- **Best match first**: a prefix, then fewest jumps between humps, then rows that only contain the
  letters. Ties, and an empty filter, keep the list's own order.
- **Match only what's on screen.** A hidden id never matches, so every row shows why it's there.
- **Say when nothing matches**, in the list: a dimmed `(no matches)`. `Enter` then does nothing, and
  the hint bar drops it.
- **Typing never leaves the filter.** `Up/Down/PgUp/PgDn` move the selection while the cursor stays
  in the field, and the first row is selected after every keystroke, so `Enter` takes the best match.

The library's [`PickerDialog`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Dialogs/PickerDialog.cs),
matching with [`ListFilter`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Filtering/ListFilter.cs)
and TuiCode's [`CamelHumps`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Filtering/CamelHumps.cs),
is the reference implementation. The command palette is built on it, and so is Swatch's
*Go › Scheme…*.

### Dialog buttons

A dialog of buttons, such as an "are you sure?", works the same way in every app:

- **`Esc` cancels.**
- **`Left`/`Right` (and `Tab`) move between the buttons, and `Space` presses the focused one.**
- **Every action has one key, shown on its button**, Cancel's `Esc` too: the underlined letter
  (`S` save, `D` don't save), or the key written before the label (`▓ Del Delete ▓`). The letter
  works without `Alt`. The dialog has no hint bar (section 2).
- **`Enter` is optional.** Bind it only to a trivial, harmless action, never a destructive one, and
  then it is that action's one key (`▓ Enter Save ▓`). Where it isn't bound, it does nothing,
  whichever button has focus.

```
┌─ Delete theme ───────────────────────────────────┐
│ Delete "Solar"?                                  │
│ This can't be undone.                            │
│                                                  │
│              ▓ Del Delete ▓   ▓ Esc Cancel ▓     │   Button (Danger, key Del), Button (Secondary, key Esc)
└──────────────────────────────────────────────────┘
```

Which button starts focused, the wording and the layout are up to the app. Two pieces of advice:
**name the action on the button** (`Delete`, not `OK`), and **say in the text whether it can be
undone**.

- Framework mechanic: a focused `Button` presses on `Enter`. The dialog takes `Enter` before the
  button sees it, so an unbound `Enter` can't press Delete just because Delete has focus.

The library's [`ConfirmDialog`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Dialogs/ConfirmDialog.cs)
is the reference implementation: give it the message and the actions with their keys. It refuses
`Enter` on a Danger action, or on more than one. Swatch's *Theme › Delete…* (can't be
undone), *Remove…* (`Enter` removes) and *Close…* (Save, Don't save, Cancel) try every key.

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

The library's [`FocusTracker`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Focus/FocusTracker.cs) and
[`FocusBorder`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Focus/FocusBorder.cs), wired up in [`AppShell`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Shell/AppShell.cs), are the
reference implementation. `FocusTracker` is framework-free and unit-tested directly: the host
registers each region with a move and an ownership test and supplies the focused view.

### What a command acts on

**A command acts on the selection, and the selection stays put while the menu or the palette has
focus.** Opening either one takes focus from the content. A command that finds its target by asking
what has focus finds nothing there, so its menu item greys out a moment after the menu opens, just as
the user reaches for it.

- **Read the target, and `isEnabled`, from state the content keeps**: the list's selected row, the
  pane last selected. Never from `HasFocus` or `Navigation.GetFocused()`.
- **Where selecting a thing is focusing it**, as in a grid of panes, remember the last one focus was
  in, and keep answering with it while focus is outside them.
- **Test it with the menu open.** Select something, open the menu, refresh it, and check the item is
  still enabled. Refreshing matters: an app that refreshes its menu on a timer greys the item on the
  first tick, not when the menu opens.

`FocusTracker` already does this for regions: while focus is in a view no region owns, such as the
open menu, it keeps the region it had. Swatch's *Theme › Use for Swatch* is the reference for a
selection. It acts on the theme selected in the tree, and is enabled only while that isn't the theme
already in use.

### The caret

**The caret is the terminal's own cursor.** Colour it from the theme with `OSC 12` whenever the
theme is applied (the theme's `Cursor` scheme, which nothing draws with) and restore the terminal's
own with `OSC 112` on exit. Terminals that don't support it ignore the sequence.

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

The library's [`TerminalCursor`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Theming/TerminalCursor.cs) sets, restores and reads back the
cursor colour, and the Diagnostics dialog shows what was asked for beside what the terminal reports.
Painted carets aren't in the library: TuiCode's
[`EditorTextView.Carets.cs`](https://github.com/mentaldesk/TuiCode/blob/main/src/TuiCode.Editor/EditorTextView.Carets.cs)
and [`TerminalCursors`](https://github.com/mentaldesk/TuiCode/blob/main/src/TuiCode.Editor/TerminalCursors.cs)
are the reference implementation. A dialog's field gets this by reusing them, not by writing a
second caret — which is also how it stays one shape.

## 6. Scrolling

**Anything that scrolls shows a scroll bar while its content doesn't fit.** It is the one cue that
says, at a glance, where you are and how much is left. Without it, a pane that scrolls on
`PgUp`/`PgDn` looks like it ends at its last visible row.

- **Auto: never always, never off.** The bar appears when the content outgrows the view and goes
  when it fits again, so content that fits keeps every column. No setting to hide it.
- **Horizontal too, where lines don't wrap.** A line running past the right edge gets a bar along
  the bottom on the same terms. A view that wraps never needs one.
- **Focus doesn't matter.** A pane that never takes focus, scrolled by its parent's keys, still
  shows its bar.
- **One bar for panes that scroll together**, such as the two sides of a diff.

Framework mechanics:

- Every `View` has a `VerticalScrollBar` and a `HorizontalScrollBar`, hidden until you set
  `ViewportSettingsFlags.HasVerticalScrollBar` / `HasHorizontalScrollBar` (or
  `VisibilityMode = ScrollBarVisibilityMode.Auto`). `TreeView<T>` and `Markdown` turn theirs on;
  `TextView` ships with them off, so set `ScrollBars = true`.
- These bars track the view's content size and `Viewport`, and the mouse can drag them. A custom
  view that keeps its own scroll offset gets a bar that never moves: scroll it with
  `SetContentSize` and `Viewport` instead.

a-team's [`WorkView`](https://github.com/mentaldesk/a-team/blob/main/dashboard/WorkView.cs) is the
reference implementation of a custom view scrolled that way.

## 7. Writing requirements

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
│ Ctrl+Enter submit  •  Esc cancel                       │   clickable hints, `  •  ` separated
└────────────────────────────────────────────────────────┘
```

Sketch conventions: `[x]` / `[ ]` checkbox, `(•)` / `( )` option, `[ 2 ▲▼]` numeric up/down,
`▓ Button ▓` button (say which: Primary, Danger or Secondary), `▸` collapsed tree node, `…` placeholder text, `▲` `█` `▼` down the right edge
a vertical scroll bar.

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
guide: the guide says what to do, the PR says why. Change the library and Swatch in the same pull
request, so a rule and its reference implementation never disagree.

Repos that follow this guide link to it from their `AGENTS.md`:
[mentaldesk/TuiCode](https://github.com/mentaldesk/TuiCode),
[mentaldesk/a-team](https://github.com/mentaldesk/a-team).
