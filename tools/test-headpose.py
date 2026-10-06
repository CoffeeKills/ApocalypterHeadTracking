# test-headpose.py — send a SIMULATED head pose to Apocalypter Head Tracking so the
# mod can be tested without real headtracking hardware.
#
#   python test-headpose.py              # FreeTrack MMF + OpenTrack UDP (default)
#   python test-headpose.py freetrack    # FreeTrack shared memory only
#   python test-headpose.py udp          # OpenTrack UDP only (mod Input = OpenTrack UDP)
#
# Keys:  arrows = yaw/pitch, Q/E = roll, +/- = step size, C = center (zero),
#        A = auto-sway demo (default OFF), H = hold still (pose frozen, tracker alive),
#        F = freeze tracker (stop writing entirely), N = send one NaN frame, Esc = quit.
#
# 0.1.1: this tool now ENCODES EXACTLY LIKE OPENTRACK (source-verified, see the mod's
# FreeTrackPipe/OpenTrackUdp headers), instead of mirroring the mod's own readers:
#   OpenTrack internal pose: yaw +right, pitch +down, roll +left, degrees, cm.
#   FreeTrack 2.0 (proto-ft): Yaw = -yaw rad, Pitch = -pitch rad, Roll = +roll rad,
#     X/Y/Z = cm*10, DataID += 1 per frame. (0.1.0 of this tool wrote DEGREES with no
#     sign flips — which matched the 0.1.0 reader's bug, so the bug was invisible.)
#   UDP (proto-udp): 6 LE doubles in Axis order TX,TY,TZ,Yaw,Pitch,Roll, unchanged.
# Expected in game: Right arrow turns the view right, Up arrow looks up, Q rolls left,
# X moves the camera right, R up, T forward (with InvertX/Y/Z all false on a fresh
# 0.1.5 config; a migrated 0.1.2-0.1.4 config has InvertX/InvertZ flipped on purpose),
# identically for FreeTrack and UDP. Look down with the mouse and press T: the camera
# must move forward level, not sink. A 5 cm step at sensitivity 1 = 5 cm in game. H must keep the view where it is (0.1.0 snapped
# back to center after 0.5 s); F must make the HUD say "waiting" and ease back.
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
MMF_SIZE = 108  # sizeof(FTHeap): FTData (92 bytes) + GameID + table[8] + GameID2
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
    struct.pack_into("<iii", buf, 0, 1, 640, 480)  # dataID, camW, camH

    frame = [1]

    def write(yaw, pitch, roll, tx=0.0, ty=0.0, tz=0.0):
        # Arguments: OpenTrack internal pose (deg, cm). Encode like proto-ft pose():
        # radians with yaw/pitch negated, translations *10 (cm -> mm).
        d2r = math.pi / 180.0
        frame[0] = (frame[0] + 1) & 0xFFFFFFFF
        struct.pack_into("<ffffff", buf, 12, -yaw * d2r, -pitch * d2r, roll * d2r,
                         tx * 10.0, ty * 10.0, tz * 10.0)
        struct.pack_into("<I", buf, 0, frame[0])  # DataID ticks every pose() call

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
    print("CLICK THIS WINDOW FIRST — keys only reach this console while it is focused.")
    print("Arrows = yaw/pitch  Q/E = roll  Z/X = pan  R/V = height  T/G = depth  +/- = step")
    print("C = center  A = auto-sway demo  H = hold still  F = freeze tracker  N = one NaN frame  Esc = quit")
    print("Starts still — hold the arrows to move the camera; press A for automatic sway.")
    print("If the in-game HUD shows 'tracking' and the camera moves, the mod works.")
    print()

    # Operator intent: yaw +right, pitch +UP, roll +left, x +right, y +up, z +forward
    # (cm). Converted to OpenTrack's internal convention below.
    yaw = pitch = roll = 0.0
    tx = ty = tz = 0.0
    hold = freeze = nan_once = False
    step = 2.0
    step_cm = 5.0
    auto = False
    t0 = time.time()
    frame = 0
    try:
        while True:
            # --- keys ---
            pressed = False
            while msvcrt.kbhit():
                ch = msvcrt.getwch()
                if ch == "\x1b":
                    return
                if ch in ("\x00", "\xe0"):
                    code = msvcrt.getwch()
                    pressed = True
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
                    print("auto-sway:", "on" if auto else "off (relaxing to center)")
                elif ch in ("h", "H"):
                    hold = not hold
                    print("hold still:", "on (pose constant, tracker alive)" if hold else "off")
                elif ch in ("f", "F"):
                    freeze = not freeze
                    print("tracker", "FROZEN (no writes)" if freeze else "running")
                elif ch in ("n", "N"):
                    nan_once = True
                elif ch in ("c", "C"):
                    yaw = pitch = roll = 0.0
                    tx = ty = tz = 0.0
                    print("centered")
                elif ch == "+":
                    step = min(step + 1.0, 20.0)
                elif ch == "-":
                    step = max(step - 1.0, 0.5)
                elif ch in ("q", "Q"):
                    roll += step
                    pressed = True
                elif ch in ("e", "E"):
                    roll -= step
                    pressed = True
                elif ch in ("z", "Z"):
                    tx -= step_cm
                    pressed = True
                elif ch in ("x", "X"):
                    tx += step_cm
                    pressed = True
                elif ch in ("r", "R"):
                    ty += step_cm
                    pressed = True
                elif ch in ("v", "V"):
                    ty -= step_cm
                    pressed = True
                elif ch in ("t", "T"):
                    tz += step_cm
                    pressed = True
                elif ch in ("g", "G"):
                    tz -= step_cm
                    pressed = True

            t = time.time() - t0
            if auto:
                # Subtle, realistic head sway: real head movement is small.
                yaw = 8.0 * math.sin(t * 0.5)
                pitch = 4.0 * math.sin(t * 0.7 + 1.0)
            elif not pressed and not hold:
                # Demo off and no keys held: relax the pose back to center so the
                # in-game camera returns to vanilla (also helps when this window
                # loses focus and stops hearing keys).
                decay = min(1.0, 8.0 / RATE)
                yaw *= 1.0 - decay
                pitch *= 1.0 - decay
                roll *= 1.0 - decay
                tx *= 1.0 - decay
                ty *= 1.0 - decay
                tz *= 1.0 - decay

            # Intent -> OpenTrack internal convention: pitch +down; translations
            # +X LEFT, +Y up, +Z BACK, cm (0.1.5: source-checked against OpenTrack's
            # proto-simconnect, which negates TX/TZ to reach MSFS's +right/+forward;
            # the 0.1.4 rig sent intent unflipped, mirroring the mod's sign bug).
            ot_yaw, ot_pitch, ot_roll = yaw, -pitch, roll
            ot_tx, ot_ty, ot_tz = -tx, ty, -tz
            if nan_once:
                ot_roll = float("nan")
                nan_once = False
                print("sent one NaN frame (the mod must ignore it)")
            if not freeze:
                if mmf:
                    mmf(ot_yaw, ot_pitch, ot_roll, ot_tx, ot_ty, ot_tz)
                if udp:
                    packet = struct.pack("<6d", ot_tx, ot_ty, ot_tz, ot_yaw, ot_pitch, ot_roll)
                    udp.sendto(packet, (UDP_HOST, UDP_PORT))

            frame += 1
            if frame % 10 == 0:
                sys.stdout.write("\ryaw {:6.1f}  pitch {:6.1f}  roll {:6.1f}  x {:5.1f}  y {:5.1f}  z {:5.1f}  step {:.1f}   "
                                 .format(yaw, pitch, roll, tx, ty, tz, step))
                sys.stdout.flush()
            time.sleep(1.0 / RATE)
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    main()
