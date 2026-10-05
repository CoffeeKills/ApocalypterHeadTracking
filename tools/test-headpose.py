# test-headpose.py — send a SIMULATED head pose to Apocalypter Head Tracking so the
# mod can be tested without real headtracking hardware.
#
#   python test-headpose.py              # FreeTrack MMF + OpenTrack UDP (default)
#   python test-headpose.py freetrack    # FreeTrack shared memory only
#   python test-headpose.py udp          # OpenTrack UDP only (mod Input = OpenTrack UDP)
#
# Keys:  arrows = yaw/pitch, Q/E = roll, +/- = step size, C = center (zero),
#        A = auto-sway demo (default ON), Esc = quit.
#
# The written field order matches the mod's readers (FreeTrack: yaw,pitch,roll,x,y,z;
# UDP: x,y,z,yaw,pitch,roll) — this tool tests the mod's pipeline, not OpenTrack's
# exact byte order (that still needs a real OpenTrack once available).
import ctypes
import math
import msvcrt
import socket
import struct
import sys
import time
from ctypes import wintypes

UDP_HOST = "127.0.0.1"
UDP_PORT = 4242
MMF_NAME = "FT_SharedMem"
MMF_SIZE = 36  # int32 dataID + camW + camH, then 6 floats
RATE = 60.0


def make_mmf_writer():
    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    # Without explicit restype, ctypes truncates the 64-bit handle to a 32-bit int.
    kernel32.CreateFileMappingW.restype = wintypes.HANDLE
    kernel32.CreateFileMappingW.argtypes = [wintypes.HANDLE, ctypes.c_void_p,
                                            wintypes.DWORD, wintypes.DWORD,
                                            wintypes.DWORD, wintypes.LPCWSTR]
    kernel32.MapViewOfFile.restype = ctypes.c_void_p
    kernel32.MapViewOfFile.argtypes = [wintypes.HANDLE, wintypes.DWORD,
                                       wintypes.DWORD, wintypes.DWORD, ctypes.c_size_t]
    kernel32.UnmapViewOfFile.argtypes = [ctypes.c_void_p]
    kernel32.UnmapViewOfFile.restype = wintypes.BOOL
    kernel32.CloseHandle.argtypes = [wintypes.HANDLE]
    kernel32.CloseHandle.restype = wintypes.BOOL

    PAGE_READWRITE = 0x04
    FILE_MAP_ALL_ACCESS = 0x000F001F
    h = kernel32.CreateFileMappingW(
        wintypes.HANDLE(-1), None, PAGE_READWRITE, 0, MMF_SIZE, MMF_NAME)
    if not h:
        raise ctypes.WinError(ctypes.get_last_error())
    view = kernel32.MapViewOfFile(h, FILE_MAP_ALL_ACCESS, 0, 0, MMF_SIZE)
    if not view:
        raise ctypes.WinError(ctypes.get_last_error())
    buf = (ctypes.c_byte * MMF_SIZE).from_address(view)
    struct.pack_into("<iii", buf, 0, 0, 640, 480)  # dataID, camW, camH

    def write(yaw, pitch, roll):
        struct.pack_into("<ffffff", buf, 12, yaw, pitch, roll, 0.0, 0.0, 0.0)

    return write


def main():
    modes = set(sys.argv[1:]) or {"freetrack", "udp"}
    mmf = udp = None
    if "freetrack" in modes:
        try:
            mmf = make_mmf_writer()
        except OSError as e:
            print("FreeTrack mapping failed:", e)
    if "udp" in modes:
        udp = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

    print("Simulated headtracking: FreeTrack={} UDP={}  ({} Hz)".format(
        bool(mmf), bool(udp), RATE))
    print("Arrows = yaw/pitch  Q/E = roll  +/- = step  C = center  A = auto-sway demo  Esc = quit")
    print("Starts still — hold the arrows to move the camera; press A for automatic sway.")
    print("If the in-game HUD shows 'tracking' and the camera moves, the mod works.")
    print()

    yaw = pitch = roll = 0.0
    step = 2.0
    auto = False
    t0 = time.time()
    frame = 0
    try:
        while True:
            # --- keys ---
            while msvcrt.kbhit():
                ch = msvcrt.getwch()
                if ch == "\x1b":
                    return
                if ch in ("\x00", "\xe0"):
                    code = msvcrt.getwch()
                    if code == "H":
                        pitch += step
                    elif code == "P":
                        pitch -= step
                    elif code == "K":
                        yaw -= step
                    elif code == "M":
                        yaw += step
                elif ch in ("a", "A"):
                    auto = not auto
                    print("auto-sway:", "on" if auto else "off")
                elif ch in ("c", "C"):
                    yaw = pitch = roll = 0.0
                    print("centered")
                elif ch == "+":
                    step = min(step + 1.0, 20.0)
                elif ch == "-":
                    step = max(step - 1.0, 0.5)
                elif ch in ("q", "Q"):
                    roll += step
                elif ch in ("e", "E"):
                    roll -= step

            t = time.time() - t0
            if auto:
                yaw = 15.0 * math.sin(t * 0.5)
                pitch = 8.0 * math.sin(t * 0.7 + 1.0)

            if mmf:
                mmf(yaw, pitch, roll)
            if udp:
                packet = struct.pack("<6d", 0.0, 0.0, 0.0, yaw, pitch, roll)
                udp.sendto(packet, (UDP_HOST, UDP_PORT))

            frame += 1
            if frame % 10 == 0:
                sys.stdout.write("\ryaw {:6.1f}  pitch {:6.1f}  roll {:6.1f}  step {:.1f}   "
                                 .format(yaw, pitch, roll, step))
                sys.stdout.flush()
            time.sleep(1.0 / RATE)
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    main()
