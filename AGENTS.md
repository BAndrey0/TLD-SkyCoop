# Local validation

Use `tools/check-fast.ps1` for ordinary mod changes. It builds incrementally with
already restored dependencies and runs the focused policy/packet checks. Keep
logs short: show the result and any new errors, rather than whole build logs.

Do not repeatedly launch the game or recreate full installations. For changes
that require Unity rendering/native gameplay, use one targeted smoke check with
isolated saves and a bounded purpose; reuse the running test process where
possible. Do not use a long game startup to validate pure packet/math logic.
State clearly when visual or two-player validation remains unverified.

For costume geometry, use `tools/render-models.ps1` after the fast build. It
renders idle/walk/run/crouch poses to `.model-qa` without loading a world, making
a server, or operating menus; it exits automatically, restores the installed
DLL and isolates all saves. Inspect the resulting image. It is not a replacement
for multiplayer validation. Do not repeatedly start full game sessions for this.

Package with `tools/package.ps1 -NoRestore` when dependencies are already
available. Repeat broader checks only after a relevant change or failure.
Never publish local logs, prepared DLC resources, or player saves.
