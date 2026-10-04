"""生成非破坏性的网页规格 WebP 副本。

批次模式：源图保留在原位置，派生图写入源图同目录下的
``.site-ready/<project-id>/<slot>.webp``。清单只在全部派生成功后原子更新。
单张模式：``--source`` / ``--output`` 只写指定成片，不改清单。
"""

from __future__ import annotations

import argparse
import io
import json
import os
import sys
import tempfile
from dataclasses import dataclass
from pathlib import Path, PurePosixPath
from typing import Any

from PIL import Image, ImageOps, ImageSequence


DEFAULT_MAX_SIDE = 2560
DEFAULT_MIN_SIDE = 1920
DEFAULT_QUALITY = 84
DEFAULT_MIN_QUALITY = 68
DEFAULT_TARGET_KB = 900


@dataclass(frozen=True)
class PreparedImage:
    contents: bytes
    width: int
    height: int
    quality: int
    source_bytes: int


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="生成网页规格 WebP")
    parser.add_argument("--batch", help="首轮项目 JSON 清单")
    parser.add_argument("--stage-root", help="作品中转站根目录")
    parser.add_argument("--source", help="单张源图路径")
    parser.add_argument("--output", help="单张 WebP 输出路径")
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--dry-run", action="store_true", help="只检查并汇总，不写文件")
    mode.add_argument("--apply", action="store_true", help="写入 WebP；批次模式同时更新清单")
    parser.add_argument(
        "--max-side",
        type=int,
        default=DEFAULT_MAX_SIDE,
        help="长边上限；0 表示不缩小，保持源图像素",
    )
    parser.add_argument("--min-side", type=int, default=DEFAULT_MIN_SIDE)
    parser.add_argument("--quality", type=int, default=DEFAULT_QUALITY)
    parser.add_argument("--min-quality", type=int, default=DEFAULT_MIN_QUALITY)
    parser.add_argument("--target-kb", type=int, default=DEFAULT_TARGET_KB)
    parser.add_argument(
        "--lossless",
        action="store_true",
        help="无损 WebP：不缩小、不按体积降质",
    )
    return parser.parse_args()


def validate_options(args: argparse.Namespace) -> None:
    if args.max_side < 0:
        raise ValueError("--max-side 不能为负数；0 表示不缩小")
    if args.max_side > 0 and args.max_side < 320:
        raise ValueError("--max-side 不能小于 320；不缩小请传 0")
    if args.max_side > 0 and not 320 <= args.min_side <= args.max_side:
        raise ValueError("--min-side 须在 320 与 --max-side 之间")
    if not args.lossless:
        if not 1 <= args.min_quality <= args.quality <= 100:
            raise ValueError("质量范围无效")
        if args.target_kb < 64:
            raise ValueError("--target-kb 不能小于 64")
    has_batch = bool(args.batch)
    has_stage_root = bool(args.stage_root)
    has_source = bool(args.source)
    has_output = bool(args.output)
    if has_batch != has_stage_root:
        raise ValueError("批次模式须同时指定 --batch 与 --stage-root")
    if has_source != has_output:
        raise ValueError("单张模式须同时指定 --source 与 --output")
    if has_batch == has_source:
        raise ValueError("须指定 --batch 与 --stage-root，或指定 --source 与 --output")


def to_stage_path(stage_root: Path, stage_rel: str) -> Path:
    rel = PurePosixPath(stage_rel)
    if rel.is_absolute() or ".." in rel.parts:
        raise ValueError(f"中转站相对路径无效：{stage_rel}")
    return stage_root.joinpath(*rel.parts)


def prepared_stage_rel(source_stage_rel: str, project_id: str, object_key: str) -> str:
    source = PurePosixPath(source_stage_rel)
    slot = PurePosixPath(object_key).stem
    return str(source.parent / ".site-ready" / project_id / f"{slot}.webp")


def fit_size(width: int, height: int, max_side: int) -> tuple[int, int]:
    current = max(width, height)
    if max_side <= 0 or current <= max_side:
        return width, height
    ratio = max_side / current
    return max(1, round(width * ratio)), max(1, round(height * ratio))


def normalized_frame(image: Image.Image, max_side: int) -> Image.Image:
    frame = ImageOps.exif_transpose(image)
    mode = "RGBA" if "A" in frame.getbands() else "RGB"
    if frame.mode != mode:
        frame = frame.convert(mode)
    size = fit_size(frame.width, frame.height, max_side)
    if size != frame.size:
        frame = frame.resize(size, Image.Resampling.LANCZOS)
    return frame


def encode_static(frame: Image.Image, quality: int) -> bytes:
    buffer = io.BytesIO()
    frame.save(
        buffer,
        format="WEBP",
        quality=quality,
        method=6,
        exact="A" in frame.getbands(),
    )
    return buffer.getvalue()


def encode_lossless(frame: Image.Image) -> bytes:
    buffer = io.BytesIO()
    frame.save(
        buffer,
        format="WEBP",
        lossless=True,
        method=6,
        exact="A" in frame.getbands(),
    )
    return buffer.getvalue()


def prepare_keep_pixels(image: Image.Image, source_bytes: int, quality: int) -> PreparedImage:
    emit_progress(0.06, "打开")
    frame = normalized_frame(image, 0)
    emit_progress(0.12, f"质量 {quality} {frame.width}x{frame.height}")
    contents = encode_static(frame, quality)
    emit_progress(0.88, "编码完成")
    return PreparedImage(
        contents=contents,
        width=frame.width,
        height=frame.height,
        quality=quality,
        source_bytes=source_bytes,
    )


def prepare_lossless(image: Image.Image, source_bytes: int) -> PreparedImage:
    emit_progress(0.06, "打开")
    frame = normalized_frame(image, 0)
    emit_progress(0.12, f"保持 {frame.width}x{frame.height}")
    contents = encode_lossless(frame)
    emit_progress(0.88, "编码完成")
    return PreparedImage(
        contents=contents,
        width=frame.width,
        height=frame.height,
        quality=100,
        source_bytes=source_bytes,
    )


def emit_progress(fraction: float, detail: str = "") -> None:
    pct = max(0, min(100, int(round(max(0.0, min(1.0, fraction)) * 100))))
    text = detail.strip()
    if text:
        print(f"进度 {pct}% {text}", flush=True)
    else:
        print(f"进度 {pct}%", flush=True)


def prepare_static(
    image: Image.Image,
    source_bytes: int,
    max_side: int,
    min_side: int,
    quality: int,
    min_quality: int,
    target_bytes: int,
) -> PreparedImage:
    emit_progress(0.06, "打开")
    quality_list = list(range(quality, min_quality - 1, -4))
    estimated = max(1, len(quality_list) * 8)
    attempt = 0
    current_max_side = max_side
    best: PreparedImage | None = None
    first_frame = True
    while True:
        frame = normalized_frame(image, current_max_side)
        if first_frame:
            emit_progress(0.12, f"缩放 {frame.width}x{frame.height}")
            first_frame = False
        for current_quality in quality_list:
            contents = encode_static(frame, current_quality)
            attempt += 1
            emit_progress(
                0.12 + 0.76 * min(attempt / estimated, 0.99),
                f"质量 {current_quality} {frame.width}x{frame.height}",
            )
            candidate = PreparedImage(
                contents=contents,
                width=frame.width,
                height=frame.height,
                quality=current_quality,
                source_bytes=source_bytes,
            )
            best = candidate
            if len(contents) <= target_bytes:
                emit_progress(0.88, "编码完成")
                return candidate
        if max(frame.size) <= min_side:
            if best is None:
                raise RuntimeError("未能生成网页规格图片")
            emit_progress(0.88, "编码完成")
            return best
        current_max_side = max(min_side, round(current_max_side * 0.9))


def prepare_animated(
    image: Image.Image,
    source_bytes: int,
    max_side: int,
    quality: int,
) -> PreparedImage:
    frames: list[Image.Image] = []
    durations: list[int] = []
    total = max(1, int(getattr(image, "n_frames", 0) or 0) or 1)
    for index, frame in enumerate(ImageSequence.Iterator(image), start=1):
        frames.append(normalized_frame(frame.copy(), max_side))
        durations.append(int(frame.info.get("duration", image.info.get("duration", 100))))
        emit_progress(0.08 + 0.42 * (index / total), f"帧 {index}/{total}")
    if not frames:
        raise RuntimeError("动画没有可用帧")
    emit_progress(0.52, "编码动画")
    buffer = io.BytesIO()
    frames[0].save(
        buffer,
        format="WEBP",
        save_all=True,
        append_images=frames[1:],
        duration=durations,
        loop=int(image.info.get("loop", 0)),
        quality=quality,
        method=6,
    )
    emit_progress(0.88, "编码完成")
    return PreparedImage(
        contents=buffer.getvalue(),
        width=frames[0].width,
        height=frames[0].height,
        quality=quality,
        source_bytes=source_bytes,
    )


def prepare_image(source: Path, args: argparse.Namespace) -> PreparedImage:
    source_bytes = source.stat().st_size
    with Image.open(source) as image:
        if bool(getattr(image, "is_animated", False)):
            return prepare_animated(image, source_bytes, args.max_side, args.quality)
        if args.lossless:
            return prepare_lossless(image, source_bytes)
        if args.max_side <= 0:
            return prepare_keep_pixels(image, source_bytes, args.quality)
        return prepare_static(
            image,
            source_bytes,
            args.max_side,
            args.min_side,
            args.quality,
            args.min_quality,
            args.target_kb * 1024,
        )


def write_atomic(path: Path, contents: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temp_name = tempfile.mkstemp(prefix=f".{path.name}.", suffix=".tmp", dir=path.parent)
    try:
        with os.fdopen(descriptor, "wb") as handle:
            handle.write(contents)
        os.replace(temp_name, path)
    except Exception:
        try:
            os.unlink(temp_name)
        except FileNotFoundError:
            pass
        raise


def write_json_atomic(path: Path, value: dict[str, Any]) -> None:
    contents = f"{json.dumps(value, ensure_ascii=False, indent=2)}\n".encode("utf-8")
    write_atomic(path, contents)


def source_stage_rel(media: dict[str, Any]) -> str:
    return str(media.get("sourceStageRel") or media.get("stageRel") or "")


def validate_batch(batch: dict[str, Any], stage_root: Path) -> list[tuple[dict[str, Any], dict[str, Any], Path]]:
    selected: list[tuple[dict[str, Any], dict[str, Any], Path]] = []
    object_keys: set[str] = set()
    for project in batch.get("projects", []):
        project_id = str(project.get("id") or "")
        if not project_id:
            raise ValueError("项目缺少 id")
        for media in project.get("media", []):
            if media.get("action") != "ingest":
                continue
            source_rel = source_stage_rel(media)
            object_key = str(media.get("object") or "")
            if not source_rel or not object_key:
                raise ValueError(f"项目 {project_id} 的待入库媒体缺少路径或对象键")
            source = to_stage_path(stage_root, source_rel)
            if not source.is_file():
                raise FileNotFoundError(f"中转站没有源图：{source}")
            next_object = str(PurePosixPath(object_key).with_suffix(".webp"))
            if next_object in object_keys:
                raise ValueError(f"对象键重复：{next_object}")
            object_keys.add(next_object)
            selected.append((project, media, source))
    return selected


def prepare_one(source: Path, output: Path, args: argparse.Namespace) -> PreparedImage:
    if not source.is_file():
        raise FileNotFoundError(f"没有源图：{source}")
    emit_progress(0.02, "开始")
    prepared = prepare_image(source, args)
    emit_progress(0.92, "写入")
    write_atomic(output, prepared.contents)
    emit_progress(0.96, "校验")
    with Image.open(output) as check:
        if check.format != "WEBP":
            raise RuntimeError(f"写出格式校验失败：{output}")
        if args.max_side > 0 and max(check.size) > args.max_side:
            raise RuntimeError(f"写出尺寸校验失败：{output}")
        if args.lossless or args.max_side <= 0:
            with Image.open(source) as source_image:
                expected = ImageOps.exif_transpose(source_image).size
            if check.size != expected:
                raise RuntimeError(f"写出尺寸与源图不一致：{output}")
    emit_progress(1.0, "完成")
    return prepared


def needs_shrink(image: Image.Image, args: argparse.Namespace) -> bool:
    if args.lossless or args.max_side <= 0:
        return False
    return max(ImageOps.exif_transpose(image).size) > args.max_side


def print_one_preview(source: Path, args: argparse.Namespace) -> None:
    source_total = source.stat().st_size
    with Image.open(source) as image:
        over_max_side = needs_shrink(image, args)
    print(
        f"预演：单张，源文件 {source_total / 1024 / 1024:.2f} MB，"
        f"{'需要缩小' if over_max_side else '无需缩小'}。"
    )
    print(spec_text(args))
    print("原图未修改。")


def spec_text(args: argparse.Namespace) -> str:
    if args.lossless:
        return "规格：WebP 无损，保持源图像素与比例。"
    if args.max_side <= 0:
        return f"规格：WebP 质量 {args.quality}，保持源图像素与比例。"
    return (
        f"规格：WebP，长边最多 {args.max_side}，必要时降到 {args.min_side}；"
        f"质量 {args.quality}-{args.min_quality}，目标单张不超过 {args.target_kb} KB。"
    )


def main() -> int:
    args = parse_args()
    validate_options(args)
    if args.source:
        source = Path(args.source).resolve()
        output = Path(args.output).resolve()
        if args.dry_run:
            if not source.is_file():
                raise FileNotFoundError(f"没有源图：{source}")
            print_one_preview(source, args)
            return 0
        prepared = prepare_one(source, output, args)
        ratio = len(prepared.contents) / prepared.source_bytes if prepared.source_bytes else 0
        print(
            f"完成：单张，{prepared.source_bytes / 1024 / 1024:.2f} MB → "
            f"{len(prepared.contents) / 1024 / 1024:.2f} MB（{ratio:.1%}）。原图保留。"
        )
        return 0

    batch_path = Path(args.batch).resolve()
    stage_root = Path(args.stage_root).resolve()
    batch = json.loads(batch_path.read_text(encoding="utf-8"))
    selected = validate_batch(batch, stage_root)

    if args.dry_run:
        source_total = sum(source.stat().st_size for _, _, source in selected)
        over_max_side = 0
        for _, _, source in selected:
            with Image.open(source) as image:
                if needs_shrink(image, args):
                    over_max_side += 1
        print(
            f"预演：{len(selected)} 张，源文件 {source_total / 1024 / 1024:.2f} MB，"
            f"{over_max_side} 张需要缩小。"
        )
        print(spec_text(args))
        print("原图与清单均未修改。")
        return 0

    output_total = 0
    source_total = 0
    for index, (project, media, source) in enumerate(selected, start=1):
        source_rel = source_stage_rel(media)
        next_object = str(PurePosixPath(str(media["object"])).with_suffix(".webp"))
        next_stage_rel = prepared_stage_rel(source_rel, str(project["id"]), next_object)
        output = to_stage_path(stage_root, next_stage_rel)
        prepared = prepare_image(source, args)
        write_atomic(output, prepared.contents)
        with Image.open(output) as check:
            if check.format != "WEBP":
                raise RuntimeError(f"写出格式校验失败：{output}")
            if args.max_side > 0 and max(check.size) > args.max_side:
                raise RuntimeError(f"写出尺寸校验失败：{output}")
        media["sourceStageRel"] = source_rel
        media["sourceObject"] = str(
            PurePosixPath(next_object).with_suffix(PurePosixPath(source_rel).suffix.lower())
        )
        media["stageRel"] = next_stage_rel
        media["object"] = next_object
        media["prepared"] = {
            "format": "webp",
            "width": prepared.width,
            "height": prepared.height,
            "quality": prepared.quality,
            "sourceBytes": prepared.source_bytes,
            "outputBytes": len(prepared.contents),
            "metadataStripped": True,
        }
        source_total += prepared.source_bytes
        output_total += len(prepared.contents)
        if index % 20 == 0 or index == len(selected):
            print(f"已处理 {index}/{len(selected)}")

    batch["webPreparation"] = {
        "format": "webp",
        "maxSide": args.max_side,
        "minSide": args.min_side,
        "quality": args.quality,
        "minQuality": args.min_quality,
        "targetKB": args.target_kb,
        "metadataStripped": True,
        "sourcePreserved": True,
    }
    write_json_atomic(batch_path, batch)
    ratio = output_total / source_total if source_total else 0
    print(
        f"完成：{len(selected)} 张，{source_total / 1024 / 1024:.2f} MB → "
        f"{output_total / 1024 / 1024:.2f} MB（{ratio:.1%}）。"
    )
    print("原图保留；清单已原子更新为 WebP 副本与 WebP 对象键。")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:
        print(f"失败：{error}", file=sys.stderr)
        raise SystemExit(1)
