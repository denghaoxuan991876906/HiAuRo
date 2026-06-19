# HiAuRo GCD Guard Design

## Scope

This change only applies to `E:\DalamudPlugins\HiAuRo` runtime code.
It does not include any job-specific resolver wiring from `MyACR`.

## Goal

Add an execution-time GCD guard window for selected ability spells.
When such an ability succeeds, HiAuRo should block starting the next GCD
until the configured guard window expires.

## Non-Goals

- Do not change the existing global oGCD throttle behavior.
- Do not change `WaitServerAcq` semantics.
- Do not extend the server-effect wait beyond the current fixed `500ms`.

## Design

- Add `Spell.GcdGuardMs` as an execution option.
- Add `Spell.WithExecutionOptions(...)` so slot building can override
  execution-only flags without mutating shared spell instances.
- Extend `Slot.Add(...)` to accept optional execution overrides.
- Add `BattleData.GcdGuardUntil` as the single runtime state for the active
  GCD guard window.
- In `SlotExecutor`, when an ability succeeds and `GcdGuardMs > 0`, set
  `GcdGuardUntil` to `now + GcdGuardMs`.
- In `AIRunner.CalSlot`, if GCD is otherwise available but `now` is still
  before `GcdGuardUntil`, skip GCD resolver execution for that frame.
- When a real GCD succeeds, clear `GcdGuardUntil` back to `0`.
- When `BattleData.Reset()` runs, also clear `GcdGuardUntil`.

## Tests

Use file-based tests only:

- verify execution options are copied onto a slot-added spell without
  mutating the original spell instance
- verify `BattleData.Reset()` clears the guard window state

The runtime wiring will be validated by existing build plus targeted
regression review because `AIRunner` / `SlotExecutor` execution depends on
game-bound services and is not suitable for direct unit tests here.
