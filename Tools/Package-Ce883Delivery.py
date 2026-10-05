"""Bundle the CE883 model, integration source, evidence and instructions only."""
from pathlib import Path
import hashlib
import json
from zipfile import ZipFile, ZIP_DEFLATED

root = Path(__file__).resolve().parents[1]
frontend = root / "Frontend"
output = root / "Builds/Nextro_CE883_Delivery.zip"
items = [(root / "Builds/Nextro_CE883HP.unitypackage", "Nextro_CE883HP.unitypackage")]
items.extend([(root / 'Tools/local_b_data.py', 'Tools/local_b_data.py'),
              (root / 'Tests/test_local_b_data.py', 'Tests/test_local_b_data.py'),
              (frontend / 'vite.config.ts', 'React/vite.config.ts')])
for folder, prefix, pattern in [
    (root / "Docs", "Docs", "CE883*.md"),
    (root / "Evidence/ce883", "Previews", "*"),
    (root / "Tests/fixtures", "Tests/fixtures", "TEST_ONLY*.json"),
    (root / "Tools", "Tools", "*-Ce883Local.ps1"),
    (frontend / "src/ce883", "React/ce883", "*"),
    (frontend / "tests", "React/tests", "*ce883*"),
    (frontend / "public/ce883-data", "React/public/ce883-data", "TEST_ONLY*.json"),
]:
    items.extend((p, f"{prefix}/{p.name}") for p in sorted(folder.glob(pattern)) if p.is_file())
manifest = []
with ZipFile(output, "w", ZIP_DEFLATED) as archive:
    for path, name in items:
        data = path.read_bytes()
        archive.writestr(name, data)
        manifest.append({"path": name, "bytes": len(data), "sha256": hashlib.sha256(data).hexdigest()})
    archive.writestr("manifest.json", json.dumps(manifest, ensure_ascii=False, indent=2))
with ZipFile(output) as archive:
    assert archive.testzip() is None
print(f"{output}\n{len(items)} files, {output.stat().st_size:,} bytes; ZIP CRC verified")
