# ModTheSpire2 Tagged Future Content Test

Controlled manifest for ModTheSpire2 classifier tests.

This fixture intentionally does not use `hot_apply_scope`. It declares future-run
content through `content_type` and `content_tags`, so the scanner can prove it
recognizes a broader manifest vocabulary while still requiring a safe main menu
and an already-loaded mod before Hot-Apply is allowed.
