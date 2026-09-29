# -*- mode: python ; coding: utf-8 -*-

from pathlib import Path
from PyInstaller.utils.hooks import collect_all


source_dir = Path(SPECPATH)
rapidocr_datas, rapidocr_binaries, rapidocr_hiddenimports = collect_all('rapidocr')

a = Analysis(
    [str(source_dir / 'app.py')],
    pathex=[str(source_dir)],
    binaries=rapidocr_binaries,
    datas=rapidocr_datas,
    hiddenimports=rapidocr_hiddenimports,
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=[],
)

pyz = PYZ(a.pure)

memory_cleaner_a = Analysis(
    [str(source_dir / 'memory_cleanup.py')],
    pathex=[str(source_dir)],
    binaries=[],
    datas=[],
    hiddenimports=[],
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=[],
)

memory_cleaner_pyz = PYZ(memory_cleaner_a.pure)

updater_a = Analysis(
    [str(source_dir / 'updater.py')],
    pathex=[str(source_dir)],
    binaries=[],
    datas=[],
    hiddenimports=[],
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=[],
)

updater_pyz = PYZ(updater_a.pure)

exe = EXE(
    pyz,
    a.scripts,
    [],
    exclude_binaries=True,
    name='GameLauncherBot',
    console=False,
    icon=str(source_dir / 'configs/ico/app.ico')
)

memory_cleaner_exe = EXE(
    memory_cleaner_pyz,
    memory_cleaner_a.scripts,
    [],
    exclude_binaries=True,
    name='MemoryCleaner',
    console=True,
)

updater_exe = EXE(
    updater_pyz,
    updater_a.scripts,
    updater_a.binaries,
    updater_a.zipfiles,
    updater_a.datas,
    [],
    name='Updater',
    console=False,
    icon=str(source_dir / 'configs/ico/app.ico'),
)

coll = COLLECT(
    exe,
    memory_cleaner_exe,
    updater_exe,
    a.binaries,
    memory_cleaner_a.binaries,
    a.zipfiles,
    memory_cleaner_a.zipfiles,
    a.datas,
    memory_cleaner_a.datas,
    name='client'
)
