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
| Multi-line free text | The library's [`MultiLineField`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Fields/MultiLineField.cs) |
| Progress of a known-length job | `ProgressBar` |

`MultiLineField` is Terminal.Gui.Editor, which replaces the obsolete `TextView`, set to this guide's
rules: it wraps, shows a dimmed `Placeholder` while empty, undoes and selects like an editor, and
copies through the library's clipboard. In Swatch, *Help › Writing a comment…* has one.

It moves the caret with the keys a Mac user already knows:

| To move to | Keys | On a Mac laptop | With [Mac keys](#mac-keys) on |
| --- | --- | --- | --- |
| Previous / next word | `Option+←/→` or `Ctrl+←/→` | — | `Option+←/→` |
| Start / end of the row you see | `Home` / `End` | `fn+←/→` | `Cmd+←/→` |
| Start / end of the text | `Ctrl+Home` / `Ctrl+End` | `fn+Ctrl+←/→` | `Cmd+↑/↓` |

Each selects as it moves with `Shift` held, and `Ctrl+A` selects all. `Option+←/→` works whether the
terminal sends it as `Alt+←/→` (iTerm2, Ghostty) or `Esc b`/`Esc f` (Terminal.app). With Mac keys on,
`Cmd+⌫` and `Option+⌫` delete to the start of the line and the previous word, and `Cmd+A`, `Cmd+C`,
`Cmd+X` and `Cmd+Z` select all, copy, cut and undo, in `MultiLineField` and `TextField` alike.

`Home` and `End` go by the row you see rather than the paragraph, as in TextEdit: in a box that wraps,
a comment is often one long paragraph, and `End` would otherwise jump to the end of all of it.

**Style a built-in before you replace it.** Most of what looks like "we need a custom control" is a
property: `NoDecorations`, `NoPadding`, `ShadowStyle`, `SchemeName`, `Orientation`, `TabBehavior`.

**A custom view needs a reason, stated in the PR** — name the built-in you rejected and what it
could not do. TuiCode's `LogView` is custom only because `TextView` cannot scroll without moving its
cursor; that sentence is the whole bar to clear.

## 2. One affordance per action

**Never give the same action two affordances in the same view.** A dialog with a `Submit` button
*and* a hint reading `Ctrl+Enter submit` is telling the user the same thing twice and asking them to
work out whether it is one thing or two.

**Each view shows its keys in one place: on its buttons.** Never on a button and in a hint too, and
never a mix: if one action shows its key, every action does, Cancel included. Wherever the key is
shown, **it comes first**: `▓ Ctrl+Enter Delete ▓`. The status bar's `Press F1 for help` is the one
exception (see *Status bars and hints*).

- **A dialog shows every action as a button**, keys on the labels, with no hint row:
  `▓ Ctrl+Enter Post ▓  ▓ Esc Cancel ▓`. That holds when the dialog is mostly a field, when it's a
  picker (`▓ Enter Run ▓  ▓ Esc Cancel ▓`), and when it has many actions. Its other keys are found
  with `F1` (see *Status bars and hints*).

### Buttons

- **A button is a filled block, not bracketed text.** No `[ ]`, one space of padding either side.
- **One Primary per view, at most.**
- **Danger is for destructive actions only**: delete, discard, overwrite. If the destructive action
  is the only one besides Cancel, it's still Danger, not Primary, and focus never starts on it.
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

### Status bars and hints

**The main screen's last row is the status bar, and it shows status, not keys**: the focus word
(section 5), errors outside a dialog (section 3), and whatever describes where the user is, such as
the selected item or the caret's row and column.

**Its one hint is `Press F1 for help`, always last at its right end** (section 5). Without it, no
other key can be found. It's the one hint that doesn't put the key first: it's a sentence for someone
who hasn't learned the keys yet. Anything else the status bar shows on the right, such as the theme
name, sits just before it: `Daylight  •  Press F1 for help`. The unread-error count `⚠ n` comes
before those, in the `Error` scheme: `⚠ 1  •  Daylight  •  Press F1 for help` (see
[Status messages](#status-messages)).

**A dialog never has a hint row.** Every action it has is a button at its foot, right-aligned, with
its key on the label (section 2).

**Every other key, in a dialog or on the main screen, is found with `F1` (under the area's name) or the command
palette, never on screen**: a picker's `Up/Down/PgUp/PgDn`, a control's editing keys such as undo,
select all and `Home`/`End`.

Write the status bar's hint and status text the same way:

- **Key first, then a lower-case verb phrase**: `Ctrl+E commands`. The key is what the user is
  scanning for. `Press F1 for help` is the one exception.
- **Separate items with `  •  `** (two spaces, U+2022, two spaces): `Daylight  •  Press F1 for help`.
  [`HintRow`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Chrome/HintRow.cs)
  lays them out.
- Name keys as the terminal reports them: `Ctrl+Enter`, `Esc`, `PgUp`.
- **Make the hint clickable.** A Terminal.Gui `Button` with its decorations turned off *is* a clickable hint:

```csharp
private static Button Hint(string text, Pos x) => new()
{
    Text = text,
    X = x,
    NoDecorations = true,
    NoPadding = true,
    ShadowStyle = ShadowStyles.None,
    HotKeySpecifier = (Rune)0xffff,   // the hint names its own key; don't also claim a hotkey
};
```

The status bar itself:

- **It spans the full width of the screen's last row**, never the window title or the foot of one pane.
- **Give it its own colour**, a `StatusBar` scheme whose background differs from every region above
  it, in every theme. In the content's colours it reads as one more line of content.
- Framework mechanic: TG's built-in `StatusBar` paints in the `Menu` scheme and draws a border
  between its items, so the status bar is a plain one-row `View` in the `StatusBar` scheme instead.

The library's [`AppStatusBar`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Chrome/AppStatusBar.cs),
which `AppShell` gives `Press F1 for help` and nothing else, and the `StatusBar` scheme in each of its
[themes](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Theming/themes.json) are the reference implementation.
[`AppDialog`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Dialogs/AppDialog.cs)
has no hint row, and lays out a dialog's buttons from its commands and keys. Every dialog in Swatch,
the command palette and *Help › Loading states…* included, has buttons and no hint row.

## 3. Errors and status

**An error must never be interleaved with the controls.** Growing a `Label` in place pushes it over
whatever sits below, and an error that lands between the buttons reads as part of them.

**Give the dialog a dedicated message block at its foot, below its buttons**, spanning the full width,
occupying zero rows while there is nothing to say. When a message arrives, **the dialog grows** and
the content above it shrinks — the message never steals the buttons' row.

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

### Status messages

**A message is either a reply or news.** A *reply* answers what the user just did: `Copied 3 lines`,
`Saved`, `Opened https://…`. *News* happened by itself: `Couldn't list files…`, `notes.md changed on disk`.

**A message stays for 5 seconds, except:**

- **A newer message replaces it.** There's no ladder: the latest wins.
- **A reply goes at your next key**, if that comes first, since you've seen it.
- **An `Error` doesn't time out.** It stays until your next key; error news, until your next key after
  its 5 seconds, so typing through it doesn't lose it.
- **News that stops being true comes off.** A load that works after a failed one takes the failure's
  message off the bar.

The keys of a chord don't count: `Ctrl+G…` shows over the message, which comes back after it.

**With nothing to say, the bar can rest on something useful**, like the active file's path, in its
normal colours. A message or a chord shows over it.

**No error is lost.** Every `Error` news counts in `⚠ n`, a button in the `Error` scheme just before
the theme name at the status bar's right end, until *Messages* is opened. A reply doesn't count,
because the user is looking at it. What the app logs rather than shows, like a folder it couldn't
watch, goes in *Messages* too, and counts in `⚠ n` if it's an `Error`.

**Messages** lists this session's messages, newest first, each with its time, and focus starts on the
newest. It's in the *Help* menu and the command palette, and clicking `⚠ n` opens it. Errors show in
the `Error` scheme, `Ctrl+C` copies the selected line, and `Esc` closes it. With nothing to show, it
says `No messages yet`, dimmed.

```
 Editor  •  Couldn't list files, so these are the files from the last load     ⚠ 1  •  Daylight  •  Press F1 for help
```

The library's [`AppStatusBar`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Chrome/AppStatusBar.cs)
is the reference implementation. `ShowMessage(…)` puts up a reply and `ShowNews(…)` news; both return
the message, which `Withdraw(…)` takes off again if it's still showing. `Record(…)` adds a message to
*Messages* without showing it, and `RestingText` is what the bar shows between messages. `AppShell` has
those three methods too, counts each key that isn't part of a chord, and opens
[`MessagesDialog`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Chrome/MessagesDialog.cs)
from *Messages*. The copy and link outcomes it reports are replies. An app without `AppShell` calls
`KeyPressed()` on its `AppStatusBar` before handling each key, and opens `MessagesDialog` from the
bar's `MessagesRequested`.

In Swatch's sample, type in the Editor while *View › Load files, failing* runs: the failure stays, and
`⚠ 1` appears. *View › Log a warning* adds to `⚠ n` without a message, the selected file rests on the
bar, *File › Save* shows a reply, and *Help › Messages…* lists them all.

### Loading and empty

**A region that waits for data says so in itself**, where its content goes: not on the status bar,
or in a header. A message away from the pane outlives the load.

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

An app without `AppShell` calls `TerminalClipboard.Install(app)` once after `Init`. Built-in controls then
copy through the terminal too, and its own copy command calls `Copy(text)` on the clipboard that returns.
It shows each `Copied` outcome on its status bar, with the message and severity the table below gives.

**Say what was copied, on the status bar:**

| Outcome | Says | Severity |
| --- | --- | --- |
| The clipboard took it | `Copied 3 lines  •  142 characters` | `Info` |
| Only the terminal took it | `Copied 3 lines  •  142 characters through the terminal` | `Info` |
| Nothing took it | `Copy failed: too large to send through the terminal (the limit is 750 KB)` | `Error` |

In a dialog, `MultiLineField` puts a failed copy in the dialog's message block instead (see
[Errors and status](#3-errors-and-status)), keeps the selection, and a failed cut leaves the text.

Deleting to the end or start of a line (`Ctrl+K` and friends) puts the text on the clipboard too,
but says what it did: `6 characters deleted`.

Through the terminal is the normal outcome over SSH, not a warning. For it to land, tmux needs
`set -g set-clipboard on`, and iTerm2 needs *Applications in terminal may access clipboard*.

In Swatch, `Ctrl+C` in *Roles*, or *Edit › Copy colours*, copies the selected role's colours.

### Opening a link

**A link opens locally and is copied over SSH.** Over SSH (`SSH_CONNECTION`, `SSH_CLIENT` or
`SSH_TTY` is set) there's no browser to open it in, so it's copied the same way as any other copy.
Only `http`, `https` and `mailto` links open; nothing the opener prints reaches the screen.

**Say which, on the status bar:**

| Outcome | Says | Severity |
| --- | --- | --- |
| Opened | `Opened https://github.com/mentaldesk/tui-style-guide` | `Info` |
| Copied over SSH | `Copied https://github.com/mentaldesk/tui-style-guide (no browser over SSH)` | `Info` |
| The copy failed | `Copy failed: …`, as for any copy | `Error` |
| No opener | `Couldn't open the link: xdg-open not found` | `Error` |
| Not a web or mail link | `Couldn't open the link: only web and mail links open` | `Error` |

Never call `Link.OpenUrl` directly. An app with `AppShell` calls `shell.OpenLink(url)`, which puts the
outcome on the status bar. One without builds a `LinkOpener` with its clipboard, calls
`opener.Open(url)` and shows the `LinkOutcome` it returns. A `Markdown` view's `LinkClicked` is one of
the places to call it: open the link there and set `e.Handled = true`.

In Swatch, *Help › Open the guide on GitHub* opens this page.

## 4. Icons and glyphs

**Use [Nerd Font](https://www.nerdfonts.com/cheat-sheet) glyphs where an icon does real work.** An
icon earns its place when it *classifies* (this row is a folder, this one a C# file) or
*disambiguates* at a glance. Decoration does not earn its place — a glyph on every label makes the
ones that mean something invisible.

- **One vocabulary per app.** Reuse a glyph already in use before picking a new one, and pick from
  one Nerd Font set (`nf-md-*`, say) rather than mixing.
- **Always have a fallback: plain Unicode, not emoji.** No terminal reports its font, so assume some
  users have no Nerd Font. Give every icon a plain Unicode glyph too (`▸`, `✓`, `⚠`). Emoji ignore the
  theme's colours, and their width depends on the terminal and the font.
- **One icon style for every MentalDesk app**: *Auto*, *Nerd Font* or *Plain*. It's a shared
  setting, never a per-app one, kept in `~/.config/mentaldesk/settings.json` (`$XDG_CONFIG_HOME` if
  set; `%APPDATA%\MentalDesk\settings.json` on Windows). Change it in any app and the others use it
  the next time they start.
- **Auto says why it chose what it did**, so a user who gets Plain can see why and fix it:
  "iTerm2 uses JetBrainsMono Nerd Font", "couldn't tell which font the terminal uses". Show it next to
  the setting and in Diagnostics.
- **Draw the icon; don't put it in the text.** Prepend the glyph's cells at draw time so the item's
  text stays the bare name, or filtering, sorting and type-to-jump all match against the glyph.
- **Budget the cells.** Every icon gets the same 2-cell field in either style, so the text beside it
  never moves when the style changes.
- **Colour comes from the theme**, and the icon keeps its row's background so selection still reads.

The library's [`IconSettings`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Icons/IconSettings.cs)
is the shared style, read once at start (`AppShell.Icons`), with
[`TerminalFontDetection`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Icons/TerminalFontDetection.cs)
behind *Auto*. Each app keeps its own icons, each an `Icon` with a Nerd Font and a Plain glyph.
[`IconField`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Icons/IconField.cs)
draws one in its 2-cell field, in the row's hot colour over the row's background, in front of a tree
row or anywhere else. In Swatch, *File › Settings…* picks the style on its *Appearance* page, with
Auto's reason beside it, and `F12` shows what Auto chose.

## 5. Keys and focus

- **`Esc` cancels. Always.** It never does anything else.
- **`Ctrl+Enter` confirms. Always.** In every dialog, whether it has a field, a multi-line one or none,
  so a user never has to stop and check which key this one takes. `Enter` never confirms a dialog: in
  a multi-line field it starts a new line, and anywhere else it does nothing, so a stray `Enter` can't
  delete or save anything. A picker is a list, not a dialog: `Enter` picks (see *Filtering a list*).
- Framework mechanic: the Windows console reports `Ctrl+Enter` as `Ctrl+LineFeed`; `AppShell` turns it
  back. Terminal.app and other terminals without the kitty keyboard protocol send it as plain `Enter`,
  so there the user `Tab`s to the button and presses `Space`. `F12` says which this terminal does.
- **Every action is reachable from the keyboard.** The mouse is a convenience, never the only way.
- **Tab moves focus, everywhere.** Terminal.Gui.Editor binds Tab to indent, so a multi-line field in
  a dialog is the library's `MultiLineField`, which leaves Tab, Esc and `Ctrl+Enter` to the dialog.
  `Tab` goes on from a pane's last control to the next pane. Where an app has a stated reason for `Tab`
  to do something else, such as an editor that indents, `F6` is still the way out (see *Panes and screens*).
- **Tabs and pages switch with `Ctrl+PgUp`/`Ctrl+PgDn`.** Wherever a view has tabs or pages, these go
  to the previous and next one, wrapping at either end, and `Tab` stays inside the current one.
- **Focus lands where work starts** when a view opens — the filter field, the summary, the first row.
- **In a set of options, the arrows choose.** `Left`/`Right` (or `Up`/`Down`) pick the next option,
  not just focus it, and focus enters on the chosen option. `Tab` leaves the set in one press and never
  changes the choice. `Space` has nothing left to do. Terminal.Gui's `OptionSelector` only moves focus,
  and `Tab` steps through its options, so call the library's
  [`SelectOnArrows()`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Fields/OptionSelectors.cs)
  on every one. Swatch's *Settings* uses it for the icon style.
- A container `View` that hosts focusable children needs `CanFocus = true`; `SetFocus()` silently
  returns false when any ancestor has it off.

### Panes and screens

A **pane** is a part of the current screen that takes its own keys: a sidebar view, an editor group,
a list beside a detail. Moving to one only moves focus. A **screen** replaces everything below the menu
bar, so moving to one redraws the window.

- **`F6` / `Shift+F6` move to the next / previous pane**, in reading order, wrapping. Focus lands where
  work starts in it. Every pane is reachable this way, whatever else the app binds.
- **The arrows never leave a pane.** At the top of a list or the end of a file, `Up` or `Down` does
  nothing. Terminal.Gui would move focus on an arrow the view doesn't use; `AppShell` stops it.
- **A pane the user moves to many times an hour may also have a chord of its own**, as TuiCode's
  Explorer has `Ctrl+Shift+E`. It's a second way in, never the only one. Not `Ctrl+Esc`, which Windows
  takes, and remember some terminals fold `Ctrl+Shift+letter` into `Ctrl+letter`.
- **`Ctrl+G` then a letter switches screen.** Every screen has one, listed in the *Go* menu, with the
  current screen checked, and in `F1`. Focus lands where work starts on the new screen.
- **`Ctrl+G` is *Go*: to a screen, or to a place** (a line, a definition, a scheme). It never just
  moves focus to a pane.

```
 Go
┌────────────────────────────────┐
│ Next pane            F6        │
│ Previous pane        Shift+F6  │   Menu with MenuItems
│ ────────────────────────────── │
│ ● Files              Ctrl+G F  │
│   Log                Ctrl+G L  │
└────────────────────────────────┘
```

`AppShell` is the reference implementation. The regions an app registers with `FocusTracker` are its
panes, in the order it registers them; `F6` and `Shift+F6` follow the framework's next- and
previous-pane keys if the user rebinds them. `AddScreen` gives a screen its `Ctrl+G` letter and a
*Go to* command, and throws if the letter clashes with a key already bound. `GoMenu` and `GoTo` build
the *Go* menu above, and `F1` lists it under *Moving around*. In Swatch, `F6` cycles Themes, Roles and
Preview, and *Go › Scheme…* is a place. In the sample app *Try this theme* opens, `Ctrl+G L` shows the
*Log* screen and `Ctrl+G F` goes back to *Files*.

### An assistant pane

**An app that lets the user ask an AI agent about it does so in one pane, *Assistant*.** One command
of that name, in the menu and the command palette, opens it down the right of the screen with focus in
its question box, and closes it again. It stays open while the user moves between screens.

- **A question carries what it's about**, so the user never has to name it. The line above the box says
  what the next question is about, the selection, and the question's heading repeats it:
  `You · Midnight › Base`.
- **Answers stream in, with a `▸` line for each thing the agent looks at or does**, so a long answer
  shows its progress.
- **`Enter` sends, `Esc` stops the answer and keeps what arrived, `Ctrl+L` starts a new conversation.**
  They're the pane's keys, listed under *Assistant* by `F1`.
- **The agent asks before it acts.** It reads without asking. A command, an edit or any other tool opens
  a dialog, *Claude Code wants to run a command*, showing the command, with `Ctrl+Enter Allow once`
  and `Esc Deny`. Deny has focus, and `Enter` does nothing there (see *Dialog buttons*). There's no
  *Always allow*.
- **When the agent can't answer, the pane says why in a line**: it isn't installed, it isn't signed in
  (with the command that signs in), or it exited.
- **The last conversation is there after a restart**, where the agent can reopen one.

The library's [`AssistantPane`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Assistant/AssistantPane.cs)
is the reference implementation. It talks to the agent, Claude Code by default, over the
[Agent Client Protocol](https://agentclientprotocol.com), takes its own keys so it works in an app
without `AppShell`, and asks with
[`AssistantPermission`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Assistant/AssistantPermission.cs).
In Swatch, *Help › Assistant* opens it beside the Roles pane, with a demo agent that streams a canned
answer about the selected scheme and asks to run a command.

### Finding keys

**`F1` always opens *Help*, and never means anything else.** *Everywhere* is on the left: the
global keys, grouped under the menu's headings. The focused area's keys are on the right, headed with
the area's name alone (`Themes`). When the two columns don't fit side by side, the area's keys go on
top and *Everywhere* below. Both are read from the keymap when it opens, so they can't drift from the
real bindings.

**`F1` works in dialogs too, with one column.** It's headed with the dialog's title (`Commands`) and
lists every key the dialog binds, its buttons' keys included. There's no *Everywhere*: the app's keys
don't work behind a dialog (see *Only what works here*). Closing it puts you back in the dialog, with
focus where it was.

- **The main screen's status bar always ends with `Press F1 for help`, and shows no other hint** (see
  *Status bars and hints*), so it's in the same place in every app. A dialog shows no `F1` hint: it
  works the same everywhere.
- **A command that only works in one region belongs to that region**, and so do its keys, so they
  show under its name (see *Only what works here*).
- It's a reference, not a runner: nothing in it takes focus, its one button is `▓ Esc Close ▓`, `F1`
  in it does nothing, and it leaves out commands that can't run here and commands with no key. The command palette has the
  second kind.

The library's [`KeysDialog`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Keys/KeysDialog.cs),
which `AppShell` binds to `F1` and `AppDialog` binds in every dialog, is the reference implementation. Swatch's `Ctrl+T` theme commands,
all but *Try it*, belong to the Themes pane.

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
  its button is dimmed.
- **An empty filter says `Type to filter`**, dimmed, in the field. It goes as soon as you type.
- **Typing never leaves the filter.** `Up/Down/PgUp/PgDn` move the selection while the cursor stays
  in the field, and the first row is selected after every keystroke, so `Enter` takes the best match.

The library's [`PickerDialog`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Dialogs/PickerDialog.cs),
matching with [`ListFilter`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Filtering/ListFilter.cs)
and TuiCode's [`CamelHumps`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Filtering/CamelHumps.cs),
is the reference implementation. The command palette is built on it, and so is Swatch's
*Go › Scheme…*.

Filter a list to pick one thing by a name you half-remember; find in what you read (below) to look
for words you've seen in text that stays as it is.

### Finding in what you read

**`Ctrl+F` opens a find bar on what you're reading**, docked at the top of that pane, never a dialog,
so the text stays where it was. A single-line selection starts the query.

- **Matching is plain text, and case never matters.** Not CamelHumps: you're looking for words you've
  seen, not a name you half-remember (*Filtering a list* is for that).
- **Every match is highlighted, and the current one is selected.** `Enter` goes to the next and
  `Shift+Enter` to the previous, wrapping at either end. The bar's right edge says `3 of 12`, or
  `No results`. Terminals without the kitty keyboard protocol send `Shift+Enter` as `Enter`, so there
  you step forward only.
- **A growing log keeps its place.** New lines are searched as they arrive and the count updates, but
  the view stops following the tail while the bar has a current match, so the line you found doesn't
  scroll away.
- **`Esc` closes the bar**, from the bar or the text, and leaves the view scrolled to the current
  match, with focus back in it.
- **`F1` in the bar lists its keys** in *Help*, in the area column, headed *Find bar*.
- *Find* sits in the *Edit* menu with `Ctrl+F`, and is dimmed wherever there's nothing to search (see
  *Only what works here*).

```
┌─Reading a log────────────────────────────────────────────────────┐
│ Find     retry▏                                          2 of 5  │   Label, TextField, Label
│──────────────────────────────────────────────────────────────────│
│ 11:02:31  restore failed: 503, ░retry░ in 5s                     │   every match highlighted
│ 11:02:36  ▓retry▓ 1 of 3                                         │   the current one selected
│ 11:02:37  restore ok                                             │
└──────────────────────────────────────────────────────────────────┘
```

The library's [`FindBar`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Finding/FindBar.cs)
is the reference implementation, moved from TuiCode with its replace row, which readers never show. It
searches an [`IFindTarget`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Finding/IFindTarget.cs):
`EditorFindTarget` covers a Terminal.Gui.Editor, read-only or not, and an app writes a small one for a
view of its own. `AppShell.FindIn` gives a region its target, and `AppShell` binds `Ctrl+F` to *Find*
there. An app without `AppShell` binds `Ctrl+F` itself, pushes `ScopeOver` its own keys when it opens the
bar, pops it on `Closed`, and shows the `KeysRequested` column on `F1`. Swatch's *Help › Reading a log…*
is a log that gains a line a second, to try it on.

### Menu letters

**Every item in an open menu has its own underlined letter, and pressing it runs the item**, without
`Alt`, as the menu bar's titles do with it.

- **No two items in a menu share a letter.** Each takes its first letter if that's free, else the first
  letter of a later word (*Save [a]s…*), else any letter in its label (*Se[t]tings…*, with *Export* holding `E`). First letters are
  handed out before later words, so *Open recent* doesn't take *Run*'s `R`. An item that marks its own
  letter keeps it.
- **A dimmed item keeps its letter**, underlined in the dim colour, so the menu doesn't change shape.
  Pressing it does nothing, and the menu stays open.

`AppMenu` picks the letters, keeping any a `MenuEntry` title marks with `_`. An app with menus of its own
picks them with `MenuLetters.Pick`, passing every title the menu can show, dimmed and swapped-out ones too. The sample app's *File*
menu has *Save*, *Save as…* and *Settings…*; Swatch's *Theme › Use for Swatch* dims with its `U`.

### Dialog buttons

A dialog of buttons, such as an "are you sure?", works the same way in every app:

- **`Esc` cancels.**
- **`Left`/`Right` (and `Tab`) move between the buttons, and `Space` presses the focused one.**
- **The main action's key is `Ctrl+Enter`**, Danger or not (`▓ Ctrl+Enter Delete ▓`).
- **Every action has one key, shown on its button**, Cancel's `Esc` too: the key written before the
  label (`▓ Ctrl+Enter Save ▓`), or for any other action the underlined letter (`D` don't save). The
  letter works without `Alt`, as in a menu. The dialog has no hint row (see *Status bars and hints*).
- **`Enter` does nothing**, whichever button has focus.

```
┌─ Delete theme ───────────────────────────────────┐
│ Delete "Solar"?                                  │
│ This can't be undone.                            │
│                                                  │
│       ▓ Ctrl+Enter Delete ▓   ▓ Esc Cancel ▓     │   Button (Danger, key Ctrl+Enter), Button (Secondary, key Esc)
└──────────────────────────────────────────────────┘
```

Which button starts focused, as long as it isn't a Danger one, the wording and the layout are up to
the app. Two pieces of advice:
**name the action on the button** (`Delete`, not `OK`), and **say in the text whether it can be
undone**.

- Framework mechanic: a focused `Button` presses on `Enter`. The dialog takes `Enter` before the
  button sees it, so `Enter` can't press Delete just because Delete has focus.

The library's [`ConfirmDialog`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Dialogs/ConfirmDialog.cs)
is the reference implementation: give it the message and the actions with their keys. It refuses
an action on `Enter`, a dialog without exactly one action on `Ctrl+Enter`, and focus starting on a
Danger button. Swatch's *Theme › Delete…* (can't be undone), *Remove…* and *Close…* (Save, Don't
save, Cancel) try every key. An app without `AppShell` uses it too: add it, push its `Scope` (keys fed
through `CtrlEnter.FromWindows`), and on `Closed` pop it, remove it, then run the chosen action.

### Settings screens

**Nothing in Settings is kept until `Ctrl+Enter`, and `Esc` undoes all of it.** A user never has to ask
which changes `Esc` will undo, because the answer is always "every one".

- **Settings holds only what the app owns.** Anything that acts on something else (GitHub, another
  app's config, another file) is a screen or a command of its own, reached from the menu and the
  palette, with its own Cancel. That's the test for whether something belongs in Settings.
- **Changes preview while Settings is open.** Pick a theme and the screen is in it. Leaving any way
  but `Ctrl+Enter` (`Esc`, *Cancel*, the close box) puts back every setting as it was saved, with no
  "are you sure?".
- **A dialog opened from a page stages its result.** Confirming it (`Ctrl+Enter`) changes the page;
  its `Esc` closes only that dialog. Nothing it did is kept until `Ctrl+Enter` on Settings, and `Esc`
  there undoes it with everything else.
- **Pages on the left, the page on the right.** The page list never takes focus: `Ctrl+PgUp`/`Ctrl+PgDn`
  change page, or click one, and `Tab`, `Shift+Tab` and the arrows move only among the page's
  controls. Focus starts on the page's first control.
- **`▓ Ctrl+Enter Keep ▓` and `▓ Esc Cancel ▓`**, at the foot, and no hint row. `Keep`, not `Save`:
  the change is already on screen, and you're keeping it. A page's own action is a button on that
  page, its key on the label, and `F1` lists every key under *Settings*.
- **A page with changes not yet kept shows `•` after its name** in the list, and loses it when they're
  put back.
- **A setting that differs from its default says so on its row**: `[ ] Show hex values  (default: on)`,
  `Daylight  (default: Midnight)`.
- **Keys are a page in Settings**, called *Keys*, and follow these rules too (see *Changing keys*).

```
┌─ Settings ──────────────────────────────────────────────────────────────────┐
│ Appearance •  │ Theme                                                       │   ListView (pages, no focus)  │ Line
│ Swatches      │   Midnight                                                  │
│               │   Daylight  (default: Midnight)                             │   ListView, focused, previews live
│               │   Turbo Pascal                                              │
│               │   Modern Borland                                            │
│               │                                                             │
│               │ Icons  (•) Auto  ( ) Nerd Font  ( ) Plain                   │   OptionSelector, previews live
│               │ Auto chose Nerd Font: kitty bundles the Nerd Font symbols   │   Label, dim
│               │                                                             │
│               │                  ▓ Ctrl+Enter Keep ▓   ▓ Esc Cancel ▓       │   Button (Primary), Button (Secondary)
└─────────────────────────────────────────────────────────────────────────────┘
```

The library's [`SettingsDialog`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Settings/SettingsDialog.cs)
is the reference implementation. An app adds its own pages, each a
[`SettingsPage`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Settings/SettingsPage.cs)
that says whether it has changes and how to keep or put them back. Swatch's *File › Settings…* has
an *Appearance* page (theme and icon style), a *Swatches* page, whose *Reset to defaults…* asks
through `ConfirmDialog` and stages the reset, a *Keys* page, and a *Mac keys* page.

### Changing keys

**Every app's keys are changed on a *Keys* page in its Settings, and saved the same way**, so a user
who has rebound a key in one app already knows how in the next. Nothing on it is kept until
`Ctrl+Enter`, and `Esc` undoes all of it, a reset included (see *Settings screens*).

- **One row per key, and one for a command with none.** Columns *Command*, *Keys* and *Where*: *Everywhere*,
  or the region's name as `F1` heads it. The filter above matches all three, as *Filtering a list* does.
- **`Enter` records keys for the selected row.** The row reads `Press keys…`, each key joins the chord as
  you press it (`Ctrl+T X`), `Enter` takes it and `Esc` stops recording without changing anything. It
  adds a key, in the row's place; the command's other keys stay. A dim line under the list says which
  keys do what.
- **`Del` removes the selected key.**
- **Keys the guide fixes can't be taken:** `Esc`, `Enter`, `Ctrl+Enter`, `Tab`, `Shift+Tab`, `F1`, and
  `F6`/`Shift+F6` for panes. The row says why, and recording carries on. A command's fixed key can't be
  removed either.
- **A clash says what it clashes with.** The same chord in the same place asks through `ConfirmDialog`
  (*`Ctrl+T D` already runs Delete this theme in Themes. Give it to Close this theme instead?*), and
  *Replace* moves it. A chord that starts or goes on from another one there is refused on the row, naming
  it; recording carries on. A region's key that hides one that works everywhere asks first, saying which
  wins where.
- **A changed command says its defaults** at the end of each key the user added, or of its first row
  when it only lost keys, where a narrow screen cuts it rather than the columns:
  `Ctrl+T X  Everywhere  (default: none)`. A key it has by default says nothing. *Reset to
  defaults…* (`Ctrl+R`) asks through `ConfirmDialog` and stages the reset.

```
│ Appearance    │ comment▏                                                    │   TextField (filter)
│ Swatches      │ Command          Keys                Where                  │   Label, dim
│ Keys •        │▓Write a comment  Press keys… Ctrl+T  Everywhere            ▓│   ListView, recording
│ Mac keys      │                                                             │
│               │ Enter takes the keys · Esc stops recording                  │   Label, dim
│               │                          ▓ Ctrl+R Reset to defaults… ▓      │   Button (Secondary), page action
│               │                  ▓ Ctrl+Enter Keep ▓   ▓ Esc Cancel ▓       │   Button (Primary), Button (Secondary)
```

**Only what differs from the defaults is saved**, by command, as key names a person can read and edit.
A command with one key in its usual place is a string, `""` is no key, and anything more is a list,
where a key in another place says where:

```json
"keys": {
  "swatch.writeComment": "Ctrl+T x",
  "app.quit": "",
  "swatch.tryTheme": ["F5", { "key": "Enter", "where": "Preview" }]
}
```

An entry that doesn't parse is skipped and that command keeps its defaults; the rest still load. A key
whose name doesn't read back as itself is written as `#` and its keycode. Terminal.Gui's own key-binding
config only holds its built-in commands, one key each, so the app's commands, chords and regions live here.

The library's [`KeysPage`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Keys/KeysPage.cs)
and [`KeyOverrides`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Keys/KeyOverrides.cs)
are the reference implementation. Once every default key is bound, an app creates `KeyOverrides`, keeping
it in its `MentalDeskSettings` section with `KeyOverrides.InSection` or handing it a section of its own
file, and calls `Load()`; then it adds `new KeysPage(shell, overrides)` to its Settings. A kept key works at
once and shows in the menu, the palette and `F1`. Swatch's *File › Settings… › Keys* is it.

### Mac keys

**Cmd and Option keys are each app's own, turned on from a *Mac keys* page in its Settings.** A Mac
terminal never passes Cmd to the app. So the library installs a small file in iTerm2, kitty or WezTerm,
under the app's name, that turns them into keys every `TextField` and `MultiLineField` already
handles, only while that app runs. The shell and every other app keep the terminal's own keys, and
installing or removing one app's file never touches another's.

| Keys | Does |
| --- | --- |
| `Cmd+←` / `Cmd+→` | Start / end of the line |
| `Cmd+↑` / `Cmd+↓` | Start / end of the text |
| `Option+←` / `Option+→` | Previous / next word |
| any of those with `Shift` | Select as it moves |
| `Cmd+⌫` / `Option+⌫` | Delete to the start of the line / the previous word |
| `Cmd+A` `Cmd+C` `Cmd+X` `Cmd+Z` | Select all, copy, cut, undo |

`Cmd+V` stays the terminal's paste, which already works.

The page follows the Settings rules: ticking the box changes nothing until `Ctrl+Enter`, which installs
(or updates an out-of-date copy), and clearing it then removes it. `Esc` leaves the terminal as it was.

```
│ Appearance    │ Terminal  kitty                                             │   Label
│ Swatches      │ State     Installed                                         │   Label
│ Mac keys      │                                                             │
│               │ [x] Cmd and Option keys in Swatch                           │   CheckBox
│               │                                                             │
│               │ Add this line to ~/.config/kitty/kitty.conf, then reopen    │   kitty and WezTerm only,
│               │ Swatch:                                                     │   once installed
│               │   include swatch.conf                          ▓ C Copy ▓   │   Button (Secondary)
│               │                                                             │
│               │   Cmd+← / Cmd+→         Start / end of the line             │   the table above
```

- *State* reads *Not installed*, *Installed*, *Out of date · saving updates it*, or, anywhere but iTerm2,
  kitty and WezTerm on a Mac, *Can't be installed here*. There the box is disabled and the page says
  which keys work without it.
- Until it's installed, the page says *Only Swatch is affected; your shell and other apps are unchanged.*
- kitty and WezTerm need one line in the user's own config, which the library never edits, so the page
  shows it with a *Copy* button.

The library's [`MacKeys`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Keys/MacKeys.cs)
has everything the page shows: `Here` (this terminal's integration, or null), `Terminal`, the
integration's `State`, `ConfigLine` and `ConfigFile`, `Install()` and `Remove()`, and `MacKeys.Rows`.
An app with `AppShell` calls `shell.UseMacKeys(new MacKeys("Swatch"))` before it creates any view, and
the shell marks it running while `Run` runs. One without calls `keys.Activate()` and disposes what it
returns on exit, and suspends inside `keys.WhileSuspended(...)`. Copy Swatch's
[`MacKeysPage`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/Swatch/MacKeysPage.cs) for
your own Settings: *File › Settings… › Mac keys*.

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
what has focus finds nothing there, so its menu item dims a moment after the menu opens, just as the
user reaches for it.

- **Read the target, and `isEnabled`, from state the content keeps**: the list's selected row, the
  pane last selected. Never from `HasFocus` or `Navigation.GetFocused()`.
- **Where selecting a thing is focusing it**, as in a grid of panes, remember the last one focus was
  in, and keep answering with it while focus is outside them.
- **Test it with the menu open.** Select something, open the menu, refresh it, and check the item
  isn't dimmed. Refreshing matters: an app that refreshes its menu on a timer dims the item on the
  first tick, not when the menu opens.

`FocusTracker` already does this for regions: while focus is in a view no region owns, such as the
open menu, it keeps the region it had. Swatch's *Theme › Use for Swatch* is the reference for a
selection. It acts on the theme selected in the tree, and is enabled only while that isn't the theme
already in use.

### Only what works here

**A command that can't run where you are seems not to exist.** Where you are is the region that had
focus when the menu or the palette opened. A command can run there if it's global or that region's,
and enabled. Its key, its menu item, the palette and `F1` all go by that one test, so they never
disagree, and none of them says why a command didn't run.

- **The palette and `F1` leave it out.**
- **The menu dims it in place**, so a menu reads the same wherever it opens. The arrows still stop on
  a dimmed item; `Enter` or a click on it does nothing.
- **A menu with nothing that can run leaves the bar**, and the titles close up behind it. Its
  `Alt+letter` does nothing until it's back.
- **Of two actions that undo each other, the menu offers only the one you can take**, in a place
  they share: *Collapse folders* while the folders are open, *Expand folders* while they're shut. No
  `•` beside the current state: the screen already shows it. A choice among more than two, like the
  sample's theme list, keeps its `●`.
- **A command that only works in one region belongs to that region, and so do its keys**, so a menu
  item can always show its key: the key works wherever the item does. A global command can have an
  extra key in one region, as Swatch's preview has `Enter`.
- Framework mechanic: Terminal.Gui's arrows skip a disabled `MenuItem`, and its `MenuBar` keeps a
  hidden title's place. So dim an item by drawing it in the `Disabled` colour and ignoring its
  activation, and take an empty menu off the bar.

The library's [`AppMenu`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Chrome/AppMenu.cs)
and [`CommandPalette`](https://github.com/mentaldesk/tui-style-guide/blob/main/src/MentalDesk.Tui/Palette/CommandPalette.cs)
share the test, `CommandRegistry.IsAvailable`, and `Keymap.Bind` won't give a region's command a key
anywhere else. A `MenuEntry`'s `Opposite` shares its place in the menu. In Swatch, open *Theme* from
the Roles pane: *Delete…*, *Remove…* and *Close…* are dimmed, with their `Ctrl+T` keys. In the sample
app that *Try it* opens, *Edit* leaves the bar while the Files pane has focus, and *View* offers
*Collapse folders* or *Expand folders*.

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
  `VisibilityMode = ScrollBarVisibilityMode.Auto`). `TreeView<T>`, `Markdown` and the library's
  `MultiLineField` turn theirs on.
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
│ │ Summary…                                           │ │   MultiLineField; focus starts here
│ └────────────────────────────────────────────────────┘ │
│                  ▓ Ctrl+Enter Submit ▓  ▓ Esc Cancel ▓ │   Primary, Secondary
└────────────────────────────────────────────────────────┘
```

Sketch conventions: `[x]` / `[ ]` checkbox, `(•)` / `( )` option, `[ 2 ▲▼]` numeric up/down,
`▓ Button ▓` button (say which: Primary, Danger or Secondary), `▸` collapsed tree node, `…` placeholder text, `▲` `█` `▼` down the right edge
a vertical scroll bar.

Then say, in prose:

- **Every key the view shows**, verbatim, on its buttons or in the status bar — it is part of the
  design, not a detail for the implementer.
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
