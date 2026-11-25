# Wizards — Windows board game conversion

A standalone Windows 10/11 implementation of the 1982 Avalon Hill board game, intended for personal use. This is a new project, independent of StarForce.

## Rules authority

1. Designers' 2003 rule corrections, additions and revisions (14 entries) override the original rules where applicable.
2. Original rulebook, sections 1–14 and appendices A–D.
3. VASSAL 1.0 module is an asset and layout reference, not a rules authority. Review OCR and card text against original images when needed.

## Planned modes

- Native solitaire (Appendix A): first complete playable milestone; no AI opponent required.
- Local hot-seat: 2–6 human players, with private hand/knowledge views.
- Play by email: 2–6 humans, saved turn handoff, private information protection.
- Partnership game (Appendix B): 4 or 6 players, as a multiplayer option.
- Computer opponents: optional later enhancement, distinct from native solitaire.

## Development milestones

1. Windows launcher, automated build, board renderer and component inventory.
2. Hex-grid geometry, terrain and walls, tile placement/rotation, legal setup, decks and random spaces.
3. Movement, encounters, task/event resolution, orders, spells, records and Evil Attack.
4. Complete solitaire, including victory/defeat, save/load and rules regression tests.
5. Hot-seat multiplayer, partnership and asynchronous handoff.

Keep game rules/state separate from the UI; deterministic testable randomness, versioned saves, explicit player decisions, and rule-section references for enforcement. Do not claim a mode playable until all necessary rules and card effects are implemented and tested.

## Asset inventory

The supplied VASSAL module contains 413 image entries, including a 4276 × 2917 board, 18 Territory fronts, one tile-back image, 98 Task cards, 49 Event cards, 14 Wizard cards, 7 Gem cards, dashboard, charts and tokens. Artwork and scanned rules are not committed to this public repository; obtain appropriate rights before distributing them. The final application must not depend on VASSAL at runtime.

## Status

Project initialized. No playable build yet.
