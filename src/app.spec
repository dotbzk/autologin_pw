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

exe = EXE(
    pyz,
    a.scripts,
    [],
    exclude_binaries=True,
    name='GameLauncherBot',
    console=False,
    icon=str(source_dir / 'configs/ico/app.ico')
)

coll = COLLECT(
    exe,
    a.binaries,
    a.zipfiles,
    a.datas,
    name='client'
)
