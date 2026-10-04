"""从中转站摄影原片读取曝光参数，写出站点内容层 photoExif.ts。

取机身、镜头、焦距、光圈、快门、ISO，以及拍摄时间 takenAt。不读 GPS。
takenAt 只供排序，访客页不展示。网页 WebP 已去元数据，须读 sourceStageRel。
对象键按正式 src 写入。`--merge` 合并进已有键，不删该键上这次没读到的字段。
"""

from __future__ import annotations

import argparse
import json
import re
from datetime import datetime
from pathlib import Path, PurePosixPath

from PIL import Image, ExifTags


PHOTO_CHANNELS = {
    "landscape-photo",
    "humanist-photo",
    "portrait-photo",
    "real-world-photo",
    "game-photo",
    "ai-photo",
}
EXIF_IFD = 0x8769
FIELD_ORDER = ("camera", "lens", "focalLength", "aperture", "shutter", "iso", "takenAt")
KEY_LINE = re.compile(r'^\s*"((?:\\.|[^"\\])*)"\s*:\s*\{\s*$')
FIELD_LINE = re.compile(
    r'^\s*(camera|lens|focalLength|aperture|shutter|iso|takenAt)\s*:\s*"((?:\\.|[^"\\])*)"\s*,?\s*$'
)
TAKEN_AT = re.compile(
    r"(\d{4}):(\d{2}):(\d{2})[ T](\d{2}):(\d{2}):(\d{2})"
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="从中转站原片提取摄影 EXIF")
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument("--batch", help="首轮项目 JSON 清单")
    source.add_argument("--pairs", help="对象键与原片相对路径的 JSON 数组")
    source.add_argument("--ledger", help="media-ledger.json，补已发布摄影键")
    parser.add_argument("--stage-root", required=True, help="作品中转站根目录")
    parser.add_argument("--out", required=True, help="写出的 photoExif.ts")
    parser.add_argument("--merge", action="store_true", help="并入已有表，不整文件覆盖")
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--dry-run", action="store_true", help="只汇总，不写文件")
    mode.add_argument("--apply", action="store_true", help="写出 TypeScript 表")
    return parser.parse_args()


def to_stage_path(stage_root: Path, stage_rel: str) -> Path:
    rel = PurePosixPath(stage_rel)
    if rel.is_absolute() or ".." in rel.parts:
        raise ValueError(f"中转站相对路径无效：{stage_rel}")
    return stage_root.joinpath(*rel.parts)


def resolve_source(stage_root: Path, stage_rel: str) -> Path | None:
    """先按台账相对路径找原片。年份套夹后移入的，在项目夹前补四位年份再找一次。"""
    direct = to_stage_path(stage_root, stage_rel)
    if direct.is_file():
        return direct
    parts = list(PurePosixPath(stage_rel).parts)
    for index, part in enumerate(parts):
        if len(part) < 8 or not part[:8].isdigit():
            continue
        year = part[:4]
        if index > 0 and parts[index - 1] == year:
            return None
        candidate = stage_root.joinpath(*parts[:index], year, *parts[index:])
        return candidate if candidate.is_file() else None
    return None


def as_float(value: object) -> float | None:
    if value is None:
        return None
    if isinstance(value, (int, float)):
        return float(value)
    if hasattr(value, "numerator") and hasattr(value, "denominator"):
        denom = float(value.denominator)
        if denom == 0:
            return None
        return float(value.numerator) / denom
    if isinstance(value, tuple) and len(value) == 2:
        denom = float(value[1])
        if denom == 0:
            return None
        return float(value[0]) / denom
    try:
        return float(value)
    except (TypeError, ValueError):
        return None


def format_camera(make: object, model: object) -> str | None:
    make_text = str(make).strip() if make else ""
    model_text = str(model).strip() if model else ""
    if make_text and model_text:
        if model_text.lower().startswith(make_text.lower()):
            return model_text
        if make_text.isupper():
            make_text = make_text.title()
        return f"{make_text} {model_text}"
    return model_text or make_text or None


def format_focal(length: object, length_35: object) -> str | None:
    chosen = as_float(length_35) if length_35 not in (None, 0) else as_float(length)
    if chosen is None or chosen <= 0:
        return None
    if abs(chosen - round(chosen)) < 0.05:
        return f"{int(round(chosen))}mm"
    return f"{chosen:.1f}mm"


def format_aperture(number: object) -> str | None:
    value = as_float(number)
    if value is None or value <= 0:
        return None
    text = f"{value:.1f}".rstrip("0").rstrip(".")
    return f"f/{text}"


def format_shutter(seconds: object) -> str | None:
    value = as_float(seconds)
    if value is None or value <= 0:
        return None
    if value >= 1:
        if abs(value - round(value)) < 0.05:
            return f"{int(round(value))}s"
        return f"{value:.1f}s"
    reciprocal = round(1 / value)
    if reciprocal < 1:
        return f"{value:.3f}s"
    return f"1/{reciprocal}s"


def format_iso(value: object) -> str | None:
    if value is None:
        return None
    if isinstance(value, (list, tuple)) and value:
        value = value[0]
    number = as_float(value)
    if number is None or number <= 0:
        return None
    return str(int(round(number)))


def format_taken(value: object) -> str | None:
    match = TAKEN_AT.search(str(value or "").strip())
    if not match:
        return None
    year, month, day, hour, minute, second = (int(part) for part in match.groups())
    if hour > 23 or minute > 59 or second > 59:
        return None
    try:
        datetime(year, month, day)
    except ValueError:
        return None
    return f"{year:04d}-{month:02d}-{day:02d}T{hour:02d}:{minute:02d}:{second:02d}"


def read_exif(path: Path) -> dict[str, str]:
    with Image.open(path) as image:
        raw = image.getexif()
        merged: dict[str, object] = {}
        for key, value in raw.items():
            merged[str(ExifTags.TAGS.get(key, key))] = value
        try:
            extra = raw.get_ifd(EXIF_IFD)
        except Exception:
            extra = {}
        for key, value in extra.items():
            merged[str(ExifTags.TAGS.get(key, key))] = value

    result: dict[str, str] = {}
    camera = format_camera(merged.get("Make"), merged.get("Model"))
    lens = str(merged.get("LensModel") or "").strip() or None
    focal = format_focal(merged.get("FocalLength"), merged.get("FocalLengthIn35mmFilm"))
    aperture = format_aperture(merged.get("FNumber"))
    shutter = format_shutter(merged.get("ExposureTime"))
    iso = format_iso(merged.get("ISOSpeedRatings") or merged.get("PhotographicSensitivity"))
    taken = format_taken(merged.get("DateTimeOriginal") or merged.get("DateTimeDigitized") or merged.get("DateTime"))
    if camera:
        result["camera"] = camera
    if lens:
        result["lens"] = lens
    if focal:
        result["focalLength"] = focal
    if aperture:
        result["aperture"] = aperture
    if shutter:
        result["shutter"] = shutter
    if iso:
        result["iso"] = iso
    if taken:
        result["takenAt"] = taken
    return result


def ts_escape(value: str) -> str:
    return value.replace("\\", "\\\\").replace('"', '\\"')


def write_ts(path: Path, rows: list[tuple[str, dict[str, str]]]) -> None:
    lines = [
        "export type PhotoExif = {",
        "  camera?: string;",
        "  lens?: string;",
        "  focalLength?: string;",
        "  aperture?: string;",
        "  shutter?: string;",
        "  iso?: string;",
        "  /** 拍摄时刻，只用于排序，访客页不展示。 */",
        "  takenAt?: string;",
        "};",
        "",
        "export function readPhotoExif(objectKey: string | undefined): PhotoExif | undefined {",
        "  if (!objectKey) return undefined;",
        "  return photoExif[objectKey];",
        "}",
        "",
        "const TAKEN_AT = /^\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}$/;",
        "",
        "/**",
        " * 已收录的拍摄时刻。格式不符或没有记录时为空。",
        " */",
        "export function photoTakenAt(objectKey: string | undefined): string {",
        "  const value = readPhotoExif(objectKey)?.takenAt;",
        "  return value && TAKEN_AT.test(value) ? value : \"\";",
        "}",
        "",
        "/** 按正式对象键收录的摄影曝光参数；缺项不写。 */",
        "export const photoExif: Record<string, PhotoExif> = {",
    ]
    for object_key, fields in rows:
        lines.append(f'  "{object_key}": {{')
        for key in FIELD_ORDER:
            if key in fields:
                lines.append(f'    {key}: "{ts_escape(fields[key])}",')
        lines.append("  },")
    lines.append("};")
    lines.append("")
    path.write_text("\n".join(lines), encoding="utf-8")


def unescape_ts(value: str) -> str:
    return json.loads(f'"{value}"')


def is_photo_object(object_key: str) -> bool:
    parts = [part for part in object_key.split("/") if part]
    if len(parts) >= 2 and parts[0] == "photo" and parts[1] in PHOTO_CHANNELS:
        return True
    return bool(parts) and parts[0] in PHOTO_CHANNELS


def parse_existing(path: Path) -> list[tuple[str, dict[str, str]]]:
    if not path.is_file():
        return []
    rows: list[tuple[str, dict[str, str]]] = []
    current_key: str | None = None
    current: dict[str, str] = {}
    for line in path.read_text(encoding="utf-8").splitlines():
        key_match = KEY_LINE.match(line)
        if key_match:
            current_key = unescape_ts(key_match.group(1))
            current = {}
            continue
        if current_key is None:
            continue
        field_match = FIELD_LINE.match(line)
        if field_match:
            current[field_match.group(1)] = unescape_ts(field_match.group(2))
            continue
        if line.strip() == "},":
            rows.append((current_key, current))
            current_key = None
            current = {}
    return rows


def merge_rows(
    existing: list[tuple[str, dict[str, str]]],
    incoming: list[tuple[str, dict[str, str]]],
) -> list[tuple[str, dict[str, str]]]:
    index = {key: position for position, (key, _) in enumerate(existing)}
    for key, fields in incoming:
        if key in index:
            merged = dict(existing[index[key]][1])
            merged.update(fields)
            existing[index[key]] = (key, merged)
        else:
            existing.append((key, fields))
    return existing


def append_pair(
    stage_root: Path,
    object_key: str,
    source: str,
    rows: list[tuple[str, dict[str, str]]],
    missing: list[str],
    empty: list[str],
) -> None:
    if not object_key or not source:
        missing.append(str(object_key or source))
        return
    path = resolve_source(stage_root, source)
    if path is None:
        missing.append(source)
        return
    fields = read_exif(path)
    if not fields:
        empty.append(object_key)
        return
    rows.append((object_key, fields))


def rows_from_batch(stage_root: Path, batch_path: Path) -> tuple[list, list, list]:
    batch = json.loads(batch_path.read_text(encoding="utf-8"))
    rows: list[tuple[str, dict[str, str]]] = []
    missing: list[str] = []
    empty: list[str] = []
    for project in batch.get("projects", []):
        if project.get("channel") not in PHOTO_CHANNELS:
            continue
        for media in project.get("media", []):
            if media.get("action") == "skip":
                continue
            append_pair(
                stage_root,
                str(media.get("object") or ""),
                str(media.get("sourceStageRel") or ""),
                rows,
                missing,
                empty,
            )
    return rows, missing, empty


def rows_from_pairs(stage_root: Path, pairs_path: Path) -> tuple[list, list, list]:
    payload = json.loads(pairs_path.read_text(encoding="utf-8"))
    if not isinstance(payload, list):
        raise ValueError("pairs 须为 JSON 数组。")
    rows: list[tuple[str, dict[str, str]]] = []
    missing: list[str] = []
    empty: list[str] = []
    for item in payload:
        append_pair(
            stage_root,
            str(item.get("object") or ""),
            str(item.get("sourceStageRel") or ""),
            rows,
            missing,
            empty,
        )
    return rows, missing, empty


def rows_from_ledger(stage_root: Path, ledger_path: Path) -> tuple[list, list, list]:
    payload = json.loads(ledger_path.read_text(encoding="utf-8"))
    records = payload.get("records", payload if isinstance(payload, list) else [])
    rows: list[tuple[str, dict[str, str]]] = []
    missing: list[str] = []
    empty: list[str] = []
    for record in records:
        if record.get("status") != "published":
            continue
        object_key = str(record.get("object") or "")
        if not is_photo_object(object_key):
            continue
        source = str(record.get("sourceStageRel") or "")
        if not source:
            missing.append(object_key)
            continue
        append_pair(stage_root, object_key, source, rows, missing, empty)
    return rows, missing, empty


def main() -> int:
    args = parse_args()
    stage_root = Path(args.stage_root)
    if args.batch:
        rows, missing, empty = rows_from_batch(stage_root, Path(args.batch))
    elif args.pairs:
        rows, missing, empty = rows_from_pairs(stage_root, Path(args.pairs))
    else:
        rows, missing, empty = rows_from_ledger(stage_root, Path(args.ledger))
    print(f"已读 {len(rows)} 张，缺文件 {len(missing)}，无参数 {len(empty)}")
    if missing:
        print("缺文件：")
        for item in missing:
            print(f"  {item}")
    if empty:
        print("无参数：")
        for item in empty:
            print(f"  {item}")
    if args.apply:
        out_path = Path(args.out)
        written = merge_rows(parse_existing(out_path), rows) if args.merge else rows
        write_ts(out_path, written)
        print(f"已写出 {args.out}，共 {len(written)} 条")
    if args.pairs and missing:
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
