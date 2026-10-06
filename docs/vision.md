# Vision

The yardstick for what this repo takes on next. If a proposal doesn't serve this, it doesn't ship,
however good it is on its own.

## Who it's for

The people and agents who write, build and review terminal UIs for MentalDesk apps. Today those
apps are [TuiCode](https://github.com/mentaldesk/TuiCode) and
[a-team](https://github.com/mentaldesk/a-team)'s dashboard, and any new Terminal.Gui app the org
starts.

- **Whoever writes a requirement**, often an agent Lead, needs to describe a screen precisely
  enough that nobody has to guess.
- **Whoever builds it**, often an agent Dev, needs to start from code that already gets focus,
  hints, errors, scrolling and themes right, instead of working them out again.
- **Whoever reviews it**, usually the maintainer, needs to point at a rule rather than make the
  same comment a third time.

Behind all of them is the person using the app, who should find that every MentalDesk app looks
and behaves the same way.

## What it's trying to be

**The one place a MentalDesk terminal app's look and behaviour is decided, written down, and
built.**

- **A guide, a library and a reference app, kept in step.** The rule is in the README, the code is
  in `MentalDesk.Tui`, and Swatch shows it working. One PR changes all three, so they never
  disagree.
- **Adopted, not copied.** Apps take `MentalDesk.Tui` as a package and delete their own copies.
  A convention the apps don't actually use hasn't really been adopted. The job isn't done until
  TuiCode and a-team run on it.
- **Earned by review.** A rule is added when review has had to make the same point twice. Every
  rule is short, says what to do, and is about a decision someone will actually face.
- **Built on Terminal.Gui, not around it.** Built-in views come first. The library adds what the
  framework lacks, works around its bugs, and says which rules are framework mechanics.
- **Extensible by apps.** Apps add their own schemes, commands and settings on top of the shared
  ones without forking them (#10, #9).
- **Showable.** Swatch exercises every rule, so a reviewer can run one command and see what
  "right" looks like in every theme.

## What it deliberately isn't

- **Not a general-purpose TUI framework or a public design system.** It serves MentalDesk apps.
  Others may use it, but their needs don't set the priorities.
- **Not a replacement for Terminal.Gui's controls.** Custom views need a stated reason, here as
  in the apps.
- **Not exhaustive.** Something that never comes up in review doesn't need a rule.
- **Not Swatch as a product.** Swatch exists to exercise the guide. A theme-editing feature
  ships only when it shows a convention.
- **Not app-specific UI.** Editor carets and syntax colours belong in TuiCode. Pitch and run views
  belong in a-team. The library takes a piece only once a second app needs it.

## Products it learns from

- **Terminal.Gui**, which is the foundation. What it ships in each release changes what the
  library has to build or can drop.
- **Textual** (Python): design tokens and themes that apps extend, a command palette, and a
  built-in gallery app. It's the closest thing to what this repo is trying to be.
- **Charm** (Bubble Tea, Lip Gloss, Huh, Bubbles): small composable pieces with strong defaults,
  and forms that look the same in every app built on them.
- **VS Code**: the command palette, keybindings editor and theme model that TuiCode's users
  already know.
- **lazygit, k9s, btop**: keyboard-first TUIs that people praise for discoverability and status
  feedback.
- **Primer and the platform HIGs** (Apple, GNOME): a guide, a component library and a living
  reference that are maintained together.

## Next themes

Roughly in order. Each is a direction, not a commitment; pitches turn them into work.

1. **Adoption.** Get TuiCode onto Terminal.Gui 2.5 and the library (mentaldesk/TuiCode#442), and
   retire the copies each app still carries. Each one we remove is drift we no longer pay for.
2. **Extension points.** Apps can add their own schemes to each shared theme (#10), and rebind
   keys with one shared editor and one settings format (#9).
3. **Closing the guide's gaps.** Sections that still point at TuiCode or a-team code (icons,
   painted carets, custom scrolled views) get a library home or a stated reason not to.
4. **Rules that keep coming back.** New conventions as review produces them: settings screens,
   confirmation and destructive actions, empty and loading states, and narrow terminals.
