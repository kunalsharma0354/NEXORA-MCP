# UI/UX Design

Use this skill when designing a new screen or reviewing one for consistency with the product.

## 1. Extract the existing system first

Read the real tokens before designing anything:

- Colour variables (backgrounds, borders, text tiers, accent, semantic states).
- Spacing scale (usually 4px or 8px based — never invent 7px gaps).
- Type scale (display, heading, body, caption, mono) with weights and letter-spacing.
- Radius scale, border weights, shadow levels.
- Control sizes: button heights, input heights, icon sizes.
- The empty/loading/error patterns already in use.

Everything you design must be expressible with these tokens. If it is not, the design is wrong, not the tokens.

## 2. Hierarchy

A screen must be readable with the colour removed:

1. One primary action per screen.
2. One clear page title; section titles are visibly smaller.
3. Body text is the default size; metadata is smaller and muted.
4. Numbers that matter are larger than their labels.
5. Grouping is expressed with spacing before borders; borders are the last resort.

Test: squint at the screen — the eye should land on the title, then the primary action, then the data.

## 3. Layout

- Content column has a maximum width; wide screens add margin, not line length.
- Related items share a container; unrelated items are separated by a bigger gap, not a divider.
- Alignment: pick one edge per block. Mixed left/centre alignment reads as an error.
- Vertical rhythm: same gap between peer items everywhere.
- Cards: consistent padding, consistent header pattern, consistent action placement.

## 4. Typography

- Maximum three sizes per card.
- Line length 45–75 characters for prose.
- No all-caps for body text; all-caps only for short labels with letter-spacing.
- Numbers in tables use tabular figures or a mono font.
- Truncation is explicit (ellipsis + full value on hover/focus), never a silent cut.

## 5. Interaction

- Hover, focus, active and disabled states for every interactive element.
- Focus is always visible and never hidden by `outline: none` without a replacement.
- Destructive actions are visually distinct and not adjacent to the primary action.
- Loading state replaces the content area, not the whole page, and never shifts layout.
- Disabled controls explain why they are disabled when the reason is not obvious.

## 6. Review checklist

- [ ] Every colour, radius, spacing and font size traces to an existing token.
- [ ] One primary action, clearly dominant.
- [ ] All interactive states defined.
- [ ] Empty, loading and error states designed, not just the happy path.
- [ ] Works with long text, missing values and 200% zoom.
- [ ] No emoji, stock illustration or unrelated icon set introduced.
- [ ] Screen looks like the same product on a side-by-side comparison.

## 7. Definition of done

A short spec exists listing tokens used, component structure, states, and any deliberate deviation with a reason. The implemented screen matches the spec.
