from dataclasses import dataclass, field
import argparse
import ctypes
from ctypes import wintypes
import os
import sys


PROCESS_QUERY_LIMITED_INFORMATION = 0x1000
PROCESS_SET_QUOTA = 0x0100
TOKEN_ADJUST_PRIVILEGES = 0x0020
TOKEN_QUERY = 0x0008
SE_PRIVILEGE_ENABLED = 0x00000002
SystemMemoryListInformation = 80
MemoryPurgeStandbyList = 4


@dataclass
class MemorySnapshot:
    total_physical_mb: int
    available_physical_mb: int
    memory_load_percent: int


@dataclass
class MemoryCleanupResult:
    supported: bool
    trimmed_processes: int = 0
    failed_processes: int = 0
    file_cache_trimmed: bool = False
    standby_list_purged: bool = False
    before: MemorySnapshot | None = None
    after: MemorySnapshot | None = None
    errors: list[str] = field(default_factory=list)

    @property
    def success(self):
        return self.supported and not self.errors

    def summary(self):
        parts = [
            f"trimmed_processes={self.trimmed_processes}",
            f"failed_processes={self.failed_processes}",
            f"file_cache_trimmed={self.file_cache_trimmed}",
            f"standby_list_purged={self.standby_list_purged}",
        ]
        if self.before and self.after:
            delta = self.after.available_physical_mb - self.before.available_physical_mb
            parts.append(
                "available_physical_mb="
                f"{self.before.available_physical_mb}->{self.after.available_physical_mb}"
                f" ({delta:+d})"
            )
        if self.errors:
            parts.append("errors=" + "; ".join(self.errors))
        return ", ".join(parts)


class MEMORYSTATUSEX(ctypes.Structure):
    _fields_ = [
        ("dwLength", wintypes.DWORD),
        ("dwMemoryLoad", wintypes.DWORD),
        ("ullTotalPhys", ctypes.c_ulonglong),
        ("ullAvailPhys", ctypes.c_ulonglong),
        ("ullTotalPageFile", ctypes.c_ulonglong),
        ("ullAvailPageFile", ctypes.c_ulonglong),
        ("ullTotalVirtual", ctypes.c_ulonglong),
        ("ullAvailVirtual", ctypes.c_ulonglong),
        ("ullAvailExtendedVirtual", ctypes.c_ulonglong),
    ]


class LUID(ctypes.Structure):
    _fields_ = [
        ("LowPart", wintypes.DWORD),
        ("HighPart", wintypes.LONG),
    ]


class TOKEN_PRIVILEGES(ctypes.Structure):
    _fields_ = [
        ("PrivilegeCount", wintypes.DWORD),
        ("Luid", LUID),
        ("Attributes", wintypes.DWORD),
    ]


def _format_last_error(prefix):
    return f"{prefix}: {ctypes.WinError(ctypes.get_last_error())}"


def _load_windows_dlls():
    if sys.platform != "win32":
        return None

    dlls = {
        "kernel32": ctypes.WinDLL("kernel32", use_last_error=True),
        "advapi32": ctypes.WinDLL("advapi32", use_last_error=True),
        "psapi": ctypes.WinDLL("psapi", use_last_error=True),
        "ntdll": ctypes.WinDLL("ntdll"),
    }

    dlls["kernel32"].GlobalMemoryStatusEx.argtypes = [ctypes.POINTER(MEMORYSTATUSEX)]
    dlls["kernel32"].GlobalMemoryStatusEx.restype = wintypes.BOOL
    dlls["kernel32"].GetCurrentProcess.argtypes = []
    dlls["kernel32"].GetCurrentProcess.restype = wintypes.HANDLE
    dlls["kernel32"].OpenProcess.argtypes = [
        wintypes.DWORD,
        wintypes.BOOL,
        wintypes.DWORD,
    ]
    dlls["kernel32"].OpenProcess.restype = wintypes.HANDLE
    dlls["kernel32"].CloseHandle.argtypes = [wintypes.HANDLE]
    dlls["kernel32"].CloseHandle.restype = wintypes.BOOL
    dlls["kernel32"].SetSystemFileCacheSize.argtypes = [
        ctypes.c_size_t,
        ctypes.c_size_t,
        wintypes.DWORD,
    ]
    dlls["kernel32"].SetSystemFileCacheSize.restype = wintypes.BOOL

    dlls["advapi32"].OpenProcessToken.argtypes = [
        wintypes.HANDLE,
        wintypes.DWORD,
        ctypes.POINTER(wintypes.HANDLE),
    ]
    dlls["advapi32"].OpenProcessToken.restype = wintypes.BOOL
    dlls["advapi32"].LookupPrivilegeValueW.argtypes = [
        wintypes.LPCWSTR,
        wintypes.LPCWSTR,
        ctypes.POINTER(LUID),
    ]
    dlls["advapi32"].LookupPrivilegeValueW.restype = wintypes.BOOL
    dlls["advapi32"].AdjustTokenPrivileges.argtypes = [
        wintypes.HANDLE,
        wintypes.BOOL,
        ctypes.POINTER(TOKEN_PRIVILEGES),
        wintypes.DWORD,
        ctypes.c_void_p,
        ctypes.c_void_p,
    ]
    dlls["advapi32"].AdjustTokenPrivileges.restype = wintypes.BOOL

    dlls["psapi"].EnumProcesses.argtypes = [
        ctypes.POINTER(wintypes.DWORD),
        wintypes.DWORD,
        ctypes.POINTER(wintypes.DWORD),
    ]
    dlls["psapi"].EnumProcesses.restype = wintypes.BOOL
    dlls["psapi"].EmptyWorkingSet.argtypes = [wintypes.HANDLE]
    dlls["psapi"].EmptyWorkingSet.restype = wintypes.BOOL

    dlls["ntdll"].NtSetSystemInformation.argtypes = [
        ctypes.c_int,
        ctypes.c_void_p,
        ctypes.c_ulong,
    ]
    dlls["ntdll"].NtSetSystemInformation.restype = ctypes.c_long

    return dlls


def get_memory_snapshot():
    dlls = _load_windows_dlls()
    if not dlls:
        return None

    status = MEMORYSTATUSEX()
    status.dwLength = ctypes.sizeof(status)
    if not dlls["kernel32"].GlobalMemoryStatusEx(ctypes.byref(status)):
        return None

    return MemorySnapshot(
        total_physical_mb=int(status.ullTotalPhys // 1024 // 1024),
        available_physical_mb=int(status.ullAvailPhys // 1024 // 1024),
        memory_load_percent=int(status.dwMemoryLoad),
    )


def _enable_privilege(dlls, privilege_name):
    token = wintypes.HANDLE()
    current_process = dlls["kernel32"].GetCurrentProcess()
    if not dlls["advapi32"].OpenProcessToken(
        current_process,
        TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY,
        ctypes.byref(token),
    ):
        return False, _format_last_error(f"OpenProcessToken({privilege_name})")

    try:
        luid = LUID()
        if not dlls["advapi32"].LookupPrivilegeValueW(
            None,
            privilege_name,
            ctypes.byref(luid),
        ):
            return False, _format_last_error(f"LookupPrivilegeValueW({privilege_name})")

        privileges = TOKEN_PRIVILEGES(1, luid, SE_PRIVILEGE_ENABLED)
        if not dlls["advapi32"].AdjustTokenPrivileges(
            token,
            False,
            ctypes.byref(privileges),
            ctypes.sizeof(privileges),
            None,
            None,
        ):
            return False, _format_last_error(f"AdjustTokenPrivileges({privilege_name})")

        last_error = ctypes.get_last_error()
        if last_error:
            return False, f"AdjustTokenPrivileges({privilege_name}): {ctypes.WinError(last_error)}"

        return True, None
    finally:
        dlls["kernel32"].CloseHandle(token)


def _trim_process_working_sets(dlls, include_current=False):
    process_ids = (wintypes.DWORD * 65536)()
    bytes_returned = wintypes.DWORD()
    if not dlls["psapi"].EnumProcesses(
        process_ids,
        ctypes.sizeof(process_ids),
        ctypes.byref(bytes_returned),
    ):
        return 0, 0, [_format_last_error("EnumProcesses")]

    current_pid = os.getpid()
    process_count = bytes_returned.value // ctypes.sizeof(wintypes.DWORD)
    trimmed = 0
    failed = 0

    for index in range(process_count):
        pid = int(process_ids[index])
        if not pid or (pid == current_pid and not include_current):
            continue

        handle = dlls["kernel32"].OpenProcess(
            PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_SET_QUOTA,
            False,
            pid,
        )
        if not handle:
            failed += 1
            continue

        try:
            if dlls["psapi"].EmptyWorkingSet(handle):
                trimmed += 1
            else:
                failed += 1
        finally:
            dlls["kernel32"].CloseHandle(handle)

    return trimmed, failed, []


def _trim_file_cache(dlls):
    enabled, error = _enable_privilege(dlls, "SeIncreaseQuotaPrivilege")
    if not enabled:
        return False, error

    if dlls["kernel32"].SetSystemFileCacheSize(
        ctypes.c_size_t(-1),
        ctypes.c_size_t(-1),
        0,
    ):
        return True, None

    return False, _format_last_error("SetSystemFileCacheSize")


def _purge_standby_list(dlls):
    enabled, error = _enable_privilege(dlls, "SeProfileSingleProcessPrivilege")
    if not enabled:
        return False, error

    command = ctypes.c_int(MemoryPurgeStandbyList)
    status = dlls["ntdll"].NtSetSystemInformation(
        SystemMemoryListInformation,
        ctypes.byref(command),
        ctypes.sizeof(command),
    )
    if status == 0:
        return True, None

    return False, f"NtSetSystemInformation(MemoryPurgeStandbyList): status=0x{status & 0xFFFFFFFF:08x}"


def cleanup_memory(
    *,
    trim_processes=True,
    trim_file_cache=True,
    purge_standby_list=True,
    include_current_process=False,
):
    dlls = _load_windows_dlls()
    if not dlls:
        return MemoryCleanupResult(
            supported=False,
            errors=["Memory cleanup is supported only on Windows"],
        )

    result = MemoryCleanupResult(supported=True)
    result.before = get_memory_snapshot()

    if trim_processes:
        trimmed, failed, errors = _trim_process_working_sets(
            dlls,
            include_current=include_current_process,
        )
        result.trimmed_processes = trimmed
        result.failed_processes = failed
        result.errors.extend(errors)

    if trim_file_cache:
        result.file_cache_trimmed, error = _trim_file_cache(dlls)
        if error:
            result.errors.append(error)

    if purge_standby_list:
        result.standby_list_purged, error = _purge_standby_list(dlls)
        if error:
            result.errors.append(error)

    result.after = get_memory_snapshot()
    return result


def main(argv=None):
    parser = argparse.ArgumentParser(description="Clean Windows memory")
    parser.add_argument("--background", action="store_true")
    parser.add_argument("--install-dir")
    parser.add_argument("--parent-pid", type=int)
    parser.add_argument(
        "--skip-processes",
        action="store_true",
        help="Do not trim process working sets",
    )
    parser.add_argument(
        "--skip-file-cache",
        action="store_true",
        help="Do not trim the system file cache",
    )
    parser.add_argument(
        "--skip-standby-list",
        action="store_true",
        help="Do not purge the standby list",
    )
    parser.add_argument(
        "--include-current-process",
        action="store_true",
        help="Also trim this cleaner process",
    )
    args = parser.parse_args(argv)

    if args.background:
        if not args.install_dir or not args.parent_pid:
            parser.error("--background requires --install-dir and --parent-pid")
        try:
            from .background_memory import run_background_cleaner
        except ImportError:
            from background_memory import run_background_cleaner
        return run_background_cleaner(args.install_dir, args.parent_pid, cleanup_memory)

    result = cleanup_memory(
        trim_processes=not args.skip_processes,
        trim_file_cache=not args.skip_file_cache,
        purge_standby_list=not args.skip_standby_list,
        include_current_process=args.include_current_process,
    )
    print(result.summary())
    return 0 if result.supported else 1


if __name__ == "__main__":
    raise SystemExit(main())
