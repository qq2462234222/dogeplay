"""Run with python3 in the Baota site directory to retain three patch versions."""
from pathlib import Path
import re

folder = Path(__file__).resolve().parent / 'files'
pattern = re.compile(r'^(?:dogeplay|LumaKeys)-(\d+\.\d+\.\d+\.\d+)-patch\.zip$')
patches = []
if not folder.is_dir():
    raise SystemExit('files directory not found beside this script')
for path in folder.iterdir():
    match = pattern.fullmatch(path.name)
    if match and path.is_file() and not path.is_symlink():
        patches.append((tuple(map(int, match.group(1).split('.'))), path))
patches.sort(key=lambda item: item[0], reverse=True)
for version, path in patches[3:]:
    path.unlink()
    print('Removed old patch:', path.name)
for version, path in patches[:3]:
    print('Kept:', path.name)
