# Release Source Export

This folder is the clean GitHub/source export for the ModTheSpire2 clean restart-manager build.

It intentionally includes:

- current companion source
- current native launcher source
- helper/verifier scripts
- controlled test manifests
- manual test notes
- current clean Workshop upload content under `dist/WorkshopUpload/ModTheSpire2Content-Clean`
- uploader metadata JSON for reference

It intentionally excludes:

- `.git`
- `bin` / `obj`
- runtime `ModTheSpire2Data`
- logs
- snapshots
- temporary build output
- old launcher backup sources
- analysis TSV files

The Workshop upload content must remain exactly five files:

```text
ModTheSpire2.dll
ModTheSpire2.json
ModTheSpire2.pck
ModTheSpire2Launcher.exe
README.md
```
