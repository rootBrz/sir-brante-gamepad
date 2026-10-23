#!/usr/bin/env python3
"""Run against a remote Linux handheld (e.g. a Steam Deck) through SSH, without installing Python packages or changing Steam.

Examples (run from the workspace):
  python3 tools/deck_smoke.py screenshot /tmp/brante.png
  python3 tools/deck_smoke.py key Return
  python3 tools/deck_smoke.py gamepad a dpad_down a shot=/tmp/brante-pad.png
  python3 tools/deck_smoke.py gamepad lx=-1 lt=1 wait=1 b

Gamepad actions are pulses, not latched state. Keep one invocation alive with
wait=SECONDS when the controller must exist before launching the game separately.
Use --display :0 for Steam; gamescope's second Xwayland display is normally :1.
Screenshots remain on the remote host; retrieve them with scp.
"""

import argparse
import ctypes
import ctypes.util
import fcntl
import math
import os
from pathlib import Path
import shutil
import shlex
import struct
import subprocess
import sys
import time


BUTTONS = {
    "a": 304, "b": 305, "x": 307, "y": 308,
    "lb": 310, "rb": 311, "back": 314, "start": 315,
    "guide": 316, "ls": 317, "rs": 318,
}
# Linux ABS codes and raw ranges. Values passed on the CLI are normalized.
AXES = {
    "lx": (0, -32768, 32767), "ly": (1, -32768, 32767),
    "lt": (2, 0, 255), "rx": (3, -32768, 32767),
    "ry": (4, -32768, 32767), "rt": (5, 0, 255),
    "hat_x": (16, -1, 1), "hat_y": (17, -1, 1),
}
DPAD = {
    "dpad_left": ("hat_x", -1), "dpad_right": ("hat_x", 1),
    "dpad_up": ("hat_y", -1), "dpad_down": ("hat_y", 1),
}
EVENT = struct.Struct("@llHHi")
UI_DEV_CREATE = 0x5501
UI_DEV_DESTROY = 0x5502
UI_DEV_SETUP = 0x405C5503
UI_ABS_SETUP = 0x401C5504
UI_SET_EVBIT = 0x40045564
UI_SET_KEYBIT = 0x40045565
UI_SET_ABSBIT = 0x40045567


def nonnegative(text):
    value = float(text)
    if not math.isfinite(value) or value < 0:
        raise argparse.ArgumentTypeError("must be finite and nonnegative")
    return value


def parse_action(text):
    if text in BUTTONS:
        return "button", BUTTONS[text], 1
    if text in DPAD:
        name, value = DPAD[text]
    elif "=" in text:
        name, value = text.split("=", 1)
        if name == "shot":
            if not value or not Path(value).is_absolute():
                raise ValueError("shot requires an absolute remote path")
            return "shot", value, 0
        if name == "wait":
            return "wait", nonnegative(value), 0
        if name not in AXES:
            raise ValueError("unknown axis: " + name)
        value = float(value)
    else:
        raise ValueError("unknown action: " + text)
    code, minimum, maximum = AXES[name]
    low = -1 if minimum < 0 else 0
    if not math.isfinite(value) or not low <= value <= 1:
        raise ValueError(name + " must be between " + str(low) + " and 1")
    if name.startswith("hat_") and value not in (-1, 0, 1):
        raise ValueError("hat axes require -1, 0, or 1")
    raw = round(value * (maximum if value >= 0 else -minimum))
    return "axis", code, raw


class X11:
    def __init__(self, display):
        self.x = ctypes.CDLL(ctypes.util.find_library("X11") or "libX11.so.6")
        self.x.XOpenDisplay.argtypes = [ctypes.c_char_p]
        self.x.XOpenDisplay.restype = ctypes.c_void_p
        self.x.XCloseDisplay.argtypes = [ctypes.c_void_p]
        self.x.XGetInputFocus.argtypes = [ctypes.c_void_p, ctypes.POINTER(ctypes.c_ulong), ctypes.POINTER(ctypes.c_int)]
        self.x.XDefaultRootWindow.argtypes = [ctypes.c_void_p]
        self.x.XDefaultRootWindow.restype = ctypes.c_ulong
        self.x.XStringToKeysym.argtypes = [ctypes.c_char_p]
        self.x.XStringToKeysym.restype = ctypes.c_ulong
        self.x.XKeysymToKeycode.argtypes = [ctypes.c_void_p, ctypes.c_ulong]
        self.x.XKeysymToKeycode.restype = ctypes.c_ubyte
        self.x.XSync.argtypes = [ctypes.c_void_p, ctypes.c_int]
        self.display = self.x.XOpenDisplay(display.encode())
        if not self.display:
            raise RuntimeError("cannot open DISPLAY=" + display + "; check --display/--xauthority")

    def close(self):
        self.x.XCloseDisplay(self.display)

    def focused_window(self):
        window = ctypes.c_ulong()
        revert = ctypes.c_int()
        self.x.XGetInputFocus(self.display, ctypes.byref(window), ctypes.byref(revert))
        # None and PointerRoot are not usable screenshot window IDs.
        return window.value if window.value > 1 else self.x.XDefaultRootWindow(self.display)

    def tap(self, key, seconds):
        xtst = ctypes.CDLL(ctypes.util.find_library("Xtst") or "libXtst.so.6")
        xtst.XTestQueryExtension.argtypes = [ctypes.c_void_p] + [ctypes.POINTER(ctypes.c_int)] * 4
        version = [ctypes.c_int() for _ in range(4)]
        if not xtst.XTestQueryExtension(self.display, *(ctypes.byref(v) for v in version)):
            raise RuntimeError("display does not support XTEST")
        xtst.XTestFakeKeyEvent.argtypes = [ctypes.c_void_p, ctypes.c_uint, ctypes.c_int, ctypes.c_ulong]
        symbol = self.x.XStringToKeysym(key.encode())
        code = self.x.XKeysymToKeycode(self.display, symbol) if symbol else 0
        if not code:
            raise ValueError("unknown or unmapped X11 keysym: " + key)
        if not xtst.XTestFakeKeyEvent(self.display, code, 1, 0):
            raise RuntimeError("XTest key press failed")
        self.x.XSync(self.display, 0)
        try:
            time.sleep(seconds)
        finally:
            xtst.XTestFakeKeyEvent(self.display, code, 0, 0)
            self.x.XSync(self.display, 0)


def screenshot(args, path):
    if not Path(path).is_absolute():
        raise ValueError("screenshot requires an absolute remote path")
    if shutil.which("gamescopectl"):
        output = Path(path)
        previous = output.stat().st_mtime_ns if output.exists() else None
        subprocess.run(["gamescopectl", "screenshot", path], check=True)
        deadline = time.monotonic() + 5
        while time.monotonic() < deadline:
            if output.exists() and output.stat().st_mtime_ns != previous:
                time.sleep(0.2)
                print("screenshot " + path, flush=True)
                return
            time.sleep(0.1)
        raise RuntimeError("gamescope did not produce screenshot: " + path)
    window = args.window
    if window == "active":
        x = X11(args.display)
        try:
            window = hex(x.focused_window())
        finally:
            x.close()
    subprocess.run(["import", "-display", args.display, "-window", window, "png:" + path], check=True)
    print("screenshot " + path, flush=True)


def send_event(fd, kind, code, value):
    data = EVENT.pack(0, 0, kind, code, value) + EVENT.pack(0, 0, 0, 0, 0)
    if os.write(fd, data) != len(data):
        raise OSError("short input-event write")


def gamepad(args):
    # Validate the whole sequence before creating a controller or sending input.
    actions = [parse_action(action) for action in args.actions]
    fd = os.open(args.evdev or args.uinput, os.O_WRONLY | os.O_NONBLOCK)
    created = False
    try:
        if not args.evdev:
            for kind in (1, 3):  # EV_KEY, EV_ABS
                fcntl.ioctl(fd, UI_SET_EVBIT, kind)
            for code in BUTTONS.values():
                fcntl.ioctl(fd, UI_SET_KEYBIT, code)
            for code, minimum, maximum in AXES.values():
                fcntl.ioctl(fd, UI_SET_ABSBIT, code)
                info = struct.pack("=H2xiiiiii", code, 0, minimum, maximum, 0, 0, 0)
                fcntl.ioctl(fd, UI_ABS_SETUP, info)
            # Standard Xbox 360 USB identity, without depending on event/js numbering.
            setup = struct.pack("=HHHH80sI", 3, 0x045E, 0x028E, 0x0114, b"Sir Brante Smoke Gamepad", 0)
            fcntl.ioctl(fd, UI_DEV_SETUP, setup)
            fcntl.ioctl(fd, UI_DEV_CREATE)
            created = True
        print("gamepad connected; waiting " + str(args.settle) + " seconds", flush=True)
        time.sleep(args.settle)
        for kind, code, value in actions:
            if kind == "wait":
                time.sleep(code)
                continue
            if kind == "shot":
                screenshot(args, code)
                continue
            event_type = 1 if kind == "button" else 3
            send_event(fd, event_type, code, value)
            try:
                time.sleep(args.duration)
            finally:
                send_event(fd, event_type, code, 0)
            print(kind + " " + str(code) + "=" + str(value) + " released", flush=True)
            time.sleep(args.gap)
    finally:
        try:
            if created:
                fcntl.ioctl(fd, UI_DEV_DESTROY)
        finally:
            os.close(fd)


def self_check():
    assert struct.calcsize("=HHHH80sI") == 92
    assert struct.calcsize("=H2xiiiiii") == 28
    assert EVENT.size == ctypes.sizeof(ctypes.c_long) * 2 + 8
    assert parse_action("a") == ("button", 304, 1)
    assert parse_action("lx=-1") == ("axis", 0, -32768)
    assert parse_action("rx=1") == ("axis", 3, 32767)
    assert parse_action("lt=1") == ("axis", 2, 255)
    assert parse_action("dpad_down") == ("axis", 17, 1)
    for action in ("lx=nan", "rt=-1", "hat_x=0.5", "wait=-1", "shot=relative"):
        try:
            parse_action(action)
        except (ValueError, argparse.ArgumentTypeError):
            pass
        else:
            raise AssertionError("accepted invalid action: " + action)
    print("self-check passed; no input sent")


def main():
    remote = sys.argv[1:2] == ["--remote"]
    arguments = sys.argv[2:] if remote else sys.argv[1:]
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--host", default=os.environ.get("DECK_HOST"), help="user@host of the device; defaults to $DECK_HOST")
    parser.add_argument("--display", default=":1")
    parser.add_argument("--xauthority", help="remote Xauthority path; normally unnecessary outside Steam's container")
    parser.add_argument("--window", default="active", help="screenshot target: active, root, or numeric X11 window ID")
    commands = parser.add_subparsers(dest="command", required=True)
    shot = commands.add_parser("screenshot", help="save PNG to an absolute remote path")
    shot.add_argument("path")
    key = commands.add_parser("key", help="tap an X11 keysym, such as Return, Escape, or Down")
    key.add_argument("key")
    key.add_argument("--duration", type=nonnegative, default=0.12)
    pad = commands.add_parser("gamepad", help="create a real uinput controller for a sequence of pulses")
    pad.add_argument("actions", nargs="+", help="a/b/x/y/lb/rb/back/start/guide/ls/rs, dpad_up/down/left/right, lx/ly/rx/ry/lt/rt=VALUE, wait=SECONDS, shot=/tmp/file.png")
    pad.add_argument("--uinput", default="/dev/uinput")
    pad.add_argument("--evdev", help="inject through an existing writable controller event node instead of creating a controller")
    pad.add_argument("--duration", type=nonnegative, default=0.15)
    pad.add_argument("--gap", type=nonnegative, default=0.35)
    pad.add_argument("--settle", type=nonnegative, default=3)
    commands.add_parser("self-check", help="check event layout and action parsing without touching input")
    args = parser.parse_args(arguments)
    if args.command == "self-check":
        self_check()
        return 0
    if not remote:
        if not args.host:
            parser.error("set --host user@host or the DECK_HOST environment variable")
        # Send this source through stdin. No remote installation or temporary script.
        command = shlex.join(["python3", "-", "--remote"] + arguments)
        process = subprocess.Popen(["ssh", args.host, command], stdin=subprocess.PIPE)
        try:
            process.communicate(Path(__file__).read_bytes())
        except KeyboardInterrupt:
            # OpenSSH forwards SIGINT only with a PTY. Closing the connection
            # lets the remote process finish its short pulse and release input.
            process.terminate()
            process.wait()
            return 130
        return process.returncode
    if args.xauthority:
        os.environ["XAUTHORITY"] = args.xauthority
    if args.command == "screenshot":
        screenshot(args, args.path)
    elif args.command == "key":
        x = X11(args.display)
        try:
            x.tap(args.key, args.duration)
        finally:
            x.close()
    else:
        gamepad(args)
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, ValueError, RuntimeError, argparse.ArgumentTypeError, subprocess.CalledProcessError) as error:
        print("deck_smoke: " + str(error), file=sys.stderr)
        sys.exit(1)
