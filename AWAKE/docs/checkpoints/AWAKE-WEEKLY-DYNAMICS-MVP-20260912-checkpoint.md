# AWAKE 本周动态 MVP 检查点

- task_id: `AWAKE-WEEKLY-DYNAMICS-MVP-20260912`
- status: `implemented_e2_pending_game`
- BuildId: `awake-20260912-weekly-dynamics-mvp-002`
- DLL SHA-256: `2A42473E67F1FBD2BD0E213E2E4080BC8CC9490F9E7FC1948C7B747225D75C95`
- files_changed: report persistence separation, hourly report scheduler, terminal/Overlay/VM display state, report labels, localization, focused smoke.

## Verification

- Main Release build: passed.
- Focused world-report smoke: passed.
- Bilingual XML key parity: passed (299 keys).
- SDK smoke executable: unavailable in this checkout; not claimed.

## Known limitations

- No game sync or game run occurred; evidence is E2 only.
- Existing bounded fact retention and older background restore permission behavior remain outside this MVP.
- The shared working tree includes pre-existing Persona and Worldbook changes. This BuildId identifies the combined local candidate; no files from those tracks were reverted or edited by this batch except the shared BuildId line.

## Next action

Obtain explicit authorization to synchronize the candidate, then user-run the four checks in `docs/evidence/AWAKE-WEEKLY-DYNAMICS-MVP-E2-20260912.md`.
