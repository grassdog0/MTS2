# ModTheSpire2 Future Run Content Test

This controlled manifest verifies state-aware Hot-Apply classification for future-run content.

It intentionally has no DLL or PCK. The important field is:

```json
"hot_apply_scope": "main_menu_no_run"
```

Expected classifier behavior:

- Default state: Restart Required / blocked with `main menu only; safe menu state not confirmed`.
- Safe main menu but not loaded: blocked with `main menu only; mod is not loaded, start through launcher to enable`.
- Safe main menu and simulated loaded state: Hot-Apply candidate with `future-run content toggle; main menu safe`.

This is test metadata only. It is not included in the Workshop upload package.
