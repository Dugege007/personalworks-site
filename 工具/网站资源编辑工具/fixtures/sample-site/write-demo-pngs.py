"""生成模拟站色块 PNG。自有色块，不取素材站。"""

from __future__ import annotations

import struct
import zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parent


def write_png(path: Path, width: int, height: int, rgb: tuple[int, int, int]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    raw = bytearray()
    row = bytes((0, *rgb * width))
    for _ in range(height):
        raw.extend(row)

    def chunk(tag: bytes, data: bytes) -> bytes:
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    ihdr = struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)
    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr) + chunk(b"IDAT", zlib.compress(bytes(raw), 9)) + chunk(b"IEND", b"")
    path.write_bytes(png)


def main() -> None:
    stage = ROOT / "stage"
    place = ROOT / "public" / "placeholders"
    specs: list[tuple[str, tuple[int, int, int]]] = [
        ("demo-render/demo-park/01.png", (47, 93, 74)),
        ("demo-render/demo-park/02.png", (42, 86, 68)),
        ("demo-render/demo-park/03.png", (196, 92, 62)),
        ("demo-render/demo-park/04.png", (176, 78, 52)),
        ("demo-render/demo-park/old.png", (138, 134, 128)),
        ("demo-render/stock/keep.png", (42, 48, 56)),
        ("demo-render/leftover/unused.png", (196, 165, 116)),
        ("demo-render/new-folder/01.png", (86, 110, 154)),
        ("demo-render/studio-a/nested-new/01.png", (110, 92, 154)),
        ("demo-render/demo-yard/01.png", (47, 93, 74)),
        ("demo-photo/demo-walk/01.png", (26, 45, 66)),
    ]
    for rel, rgb in specs:
        write_png(stage / rel, 480, 270, rgb)

    for rel in (
        "demo-render/demo-park/01.png",
        "demo-render/demo-park/02.png",
        "demo-render/demo-yard/01.png",
        "demo-photo/demo-walk/01.png",
    ):
        dest = place / rel
        dest.parent.mkdir(parents=True, exist_ok=True)
        dest.write_bytes((stage / rel).read_bytes())

    write_demo_clip(stage / "demo-render/demo-park/clip.mp4")
    print("已写入模拟站色块图与短视频夹具。")


def write_demo_clip(path: Path) -> None:
    """写入约 2 秒的短 MP4；无 ffmpeg 时写最小可识别文件。"""
    path.parent.mkdir(parents=True, exist_ok=True)
    import shutil
    import subprocess

    ffmpeg = shutil.which("ffmpeg")
    if ffmpeg:
        subprocess.run(
            [
                ffmpeg,
                "-hide_banner",
                "-loglevel",
                "error",
                "-y",
                "-f",
                "lavfi",
                "-i",
                "color=c=0x2f5d4a:s=320x180:d=2",
                "-c:v",
                "libx264",
                "-pix_fmt",
                "yuv420p",
                str(path),
            ],
            check=True,
        )
        return

    # ftyp + free 占位，编目只认扩展名；抽帧失败则灰底文件名。
    path.write_bytes(
        b"\x00\x00\x00\x18ftypmp42\x00\x00\x00\x00mp42isom"
        b"\x00\x00\x00\x08free"
    )


if __name__ == "__main__":
    main()
