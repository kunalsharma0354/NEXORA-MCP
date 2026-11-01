# Frontend Development

Use this skill for component work, state changes, layout changes and rendering performance in an existing front-end.

## 1. Split the screen before writing it

Before editing, classify every piece of the screen:

| Kind | Where it lives | Test |
|---|---|---|
| Page/view shell | route file | does it own layout only? |
| Reusable component | components folder | used ≥ 2 times or clearly reusable? |
| State | nearest owner that needs it | does more than one sibling need it? |
| Derived data | computed at render | can it be computed instead of stored? |
| Side effect | hook / effect / service | does it survive a re-render? |

If a component has more than one responsibility, split it. If a state value can be computed, do not store it.

## 2. State rules

- Own state at the lowest common ancestor of the components that need it.
- Never duplicate server data into local state unless you need an editable draft.
- One source of truth per value; if two places disagree, the bug is the duplication.
- Every async flow needs three states: loading, error, success. Empty is a fourth — treat it as success with no rows.
- Clean up subscriptions, timers and listeners on unmount.
- Never mutate props or state objects in place.

## 3. Component rules

- Props are inputs; components do not fetch data their parent already has.
- No hidden global reads inside presentational components.
- Keyboard reachable: interactive elements are real buttons/links, not clickable divs.
- Focus is preserved across re-renders when a list updates.
- Rendering must stay stable: keys are stable ids, not array indexes, for reorderable lists.

## 4. Layout rules

- Reuse the existing layout primitives and spacing scale; do not invent new spacing values.
- Design mobile-first, then widen: base styles for narrow, media queries for larger.
- Text must not overflow its container: test long names, long words and 200% zoom.
- Grids use `minmax()` / `auto-fit` instead of fixed column counts when content width varies.
- Sticky or fixed elements must not cover content at small heights.

## 5. Performance rules

- Measure before optimising. A component is "slow" only with evidence (profiler, render counts).
- Memoise only when a value is expensive or identity stability matters for a child.
- Do not add `useMemo`/`useCallback` everywhere; it hides the real cost.
- Large lists need virtualisation, not more renders.
- Images: explicit dimensions to avoid layout shift; lazy-load below the fold.

## 6. Definition of done

- Component tree reviewed: no duplicated state, no unused props.
- All four states handled: loading, error, empty, success.
- Works at 320px, 768px and 1440px widths.
- Keyboard-only path works for every new control.
- No console errors or warnings introduced.
- Build and type-check pass.
