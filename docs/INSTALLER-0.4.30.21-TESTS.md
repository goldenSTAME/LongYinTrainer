# Installer 0.4.30.21 verification

- Plugin release build: 0 warnings, 0 errors.
- Installer tests: 25 passed, including missing interop dependency, corrupted coreclr, repair, existing config preservation, path traversal prevention and official loader hash.
- Final EXE embedded loader SHA256 equals pinned official archive; embedded plugin ZIP SHA256 equals final distributable ZIP.
- Uses stable 0.4.30.20 source + version bump; experimental native editor changes remain on separate worktree.
- Not yet tested on the reporting player's machine. Their symptom alone does not establish a root cause.
- Complete installer GUI / fresh game startup still requires field verification; core fixture tests are not a gameplay test.
