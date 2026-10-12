# -*- mode: python ; coding: utf-8 -*-

from pathlib import Path
from PyInstaller.utils.hooks import collect_all


source_dir = Path(SPECPATH)
project_root = source_dir.parent
rapidocr_datas, rapidocr_binaries, rapidocr_hiddenimports = collect_all('rapidocr')
qml_datas = [(str(source_dir / 'qml'), 'qml')]
hub_datas = [
    (str(project_root / 'game_tools_hub' / 'qml'), 'game_tools_hub/qml'),
    (str(project_root / 'game_tools_hub' / 'version.json'), 'game_tools_hub'),
]

a = Analysis(
    [str(source_dir / 'app.py')],
    pathex=[str(source_dir)],
    binaries=rapidocr_binaries,
    datas=rapidocr_datas + qml_datas,
    hiddenimports=rapidocr_hiddenimports + [
        'PySide6.QtQuick',
        'PySide6.QtQuickControls2',
    ],
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=[],
)

pyz = PYZ(a.pure)

hub_a = Analysis(
    [str(project_root / 'game_tools_hub' / 'entry.py')],
    pathex=[str(project_root), str(source_dir)],
    binaries=[],
    datas=hub_datas,
    hiddenimports=[
        'PySide6.QtQuick',
        'PySide6.QtQuickControls2',
    ],
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=[],
)

hub_pyz = PYZ(hub_a.pure)

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

hub_exe = EXE(
    hub_pyz,
    hub_a.scripts,
    [],
    exclude_binaries=True,
    name='GameToolsHub',
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
    hub_exe,
    memory_cleaner_exe,
    updater_exe,
    a.binaries,
    hub_a.binaries,
    memory_cleaner_a.binaries,
    a.zipfiles,
    hub_a.zipfiles,
    memory_cleaner_a.zipfiles,
    a.datas,
    hub_a.datas,
    memory_cleaner_a.datas,
    name='client'
)
