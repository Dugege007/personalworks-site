"""将目录内或单个施工图 JPG 脱敏为同主名 .desense.jpg。"""

from __future__ import annotations

import argparse
import json
import re
import sys
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

try:
    from PIL import Image, ImageDraw, ImageFile, ImageFont
except ImportError:
    print("错误：未安装 Pillow。请先执行：pip install -r requirements.txt", file=sys.stderr)
    sys.exit(1)

# A0+ 加长页超过 Pillow 默认解压上限
Image.MAX_IMAGE_PIXELS = 250_000_000
ImageFile.LOAD_TRUNCATED_IMAGES = True

DEFAULT_QUALITY = 92
DEFAULT_OCR_MAX_SIDE = 2400
DEFAULT_SUFFIX = ".desense"
MIN_OCR_MAX_SIDE = 800
MAX_OCR_MAX_SIDE = 4000
MIN_ENTITY_LEN = 4
MIN_TITLE_ANCHORS = 3
PROFILES_DIR = Path(__file__).resolve().parent / "profiles"
WATERMARK_TEXT = "作品集展示用"
WATERMARK_RGB = (128, 128, 128)
WATERMARK_OPACITY = 0.10
WATERMARK_FONT_RATIO = 0.11
WATERMARK_FONT_CANDIDATES = (
    Path(r"C:\Windows\Fonts\msyhbd.ttc"),
    Path(r"C:\Windows\Fonts\simhei.ttf"),
    Path(r"C:\Windows\Fonts\msyh.ttc"),
)

_OCR_ENGINE = None
_OCR_ENGINE_FAILED = False
_WARNED_PENDING: set[str] = set()

ANCHOR_KEYS = (
    "设计单位",
    "DESIGN",
    "工程名称",
    "PROJECT",
    "业主",
    "CLIENT",
    "SEAL",
    "图签",
    "图名",
    "图纸名称",
    "建设单位",
    "工程地点",
    "DRAWNING TITLE",
    "DRAWING TITLE",
    "项目名称",
    "PART",
)


@dataclass
class OcrLine:
    text: str
    score: float
    x0: float
    y0: float
    x1: float
    y1: float

    @property
    def cx(self) -> float:
        return (self.x0 + self.x1) / 2

    @property
    def cy(self) -> float:
        return (self.y0 + self.y1) / 2

    @property
    def width(self) -> float:
        return max(1e-6, self.x1 - self.x0)

    @property
    def height(self) -> float:
        return max(1e-6, self.y1 - self.y0)

    @property
    def aspect(self) -> float:
        return self.width / self.height


@dataclass
class TitleBlock:
    side: str
    x0: float
    y0: float
    x1: float
    y1: float
    split: float


@dataclass
class DesenseStats:
    scanned: int = 0
    desensed: int = 0
    skipped_exists: int = 0
    skipped_sheet: int = 0
    skipped_empty: int = 0
    failed: int = 0
    fail_paths: list[str] = field(default_factory=list)


def configure_stdio() -> None:
    """在 Windows 控制台输出 UTF-8，避免中文日志乱码。"""
    if sys.platform != "win32":
        return
    try:
        import ctypes

        ctypes.windll.kernel32.SetConsoleOutputCP(65001)
        ctypes.windll.kernel32.SetConsoleCP(65001)
    except Exception:
        pass
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding="utf-8")
        except Exception:
            pass


def load_profiles() -> dict[str, dict[str, Any]]:
    """读取 profiles/*.json，键为规则 id。"""
    loaded: dict[str, dict[str, Any]] = {}
    if not PROFILES_DIR.is_dir():
        raise RuntimeError(f"未找到规则目录：{PROFILES_DIR}")
    for path in sorted(PROFILES_DIR.glob("*.json")):
        data = json.loads(path.read_text(encoding="utf-8"))
        profile_id = str(data.get("id") or path.stem)
        data["id"] = profile_id
        loaded[profile_id] = data
    if "generic" not in loaded:
        raise RuntimeError("缺少 profiles/generic.json")
    return loaded


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        prog="jpgdesense4lcds",
        description="扫描目录（递归）、单个或多个 JPG（可分属不同目录），将景观施工图按公司规则脱敏为同目录 .desense.jpg（封面/说明/目录跳过，图签仅签字栏）。源文件保留。",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=(
            "示例：\n"
            "  python jpgdesense4lcds.py D:\\drawings\n"
            "  python jpgdesense4lcds.py D:\\drawings\\图纸.jpg\n"
            "  python jpgdesense4lcds.py D:\\a\\1.jpg D:\\b\\2.jpg\n"
            "  python jpgdesense4lcds.py D:\\drawings --profile daotian\n"
            "  python jpgdesense4lcds.py D:\\drawings --dry-run\n"
        ),
    )
    parser.add_argument(
        "paths",
        nargs="*",
        type=Path,
        metavar="路径",
        help="要扫描的目录或 JPG，可多个、可分属不同目录；按传入顺序处理",
    )
    parser.add_argument(
        "--quality",
        type=int,
        default=DEFAULT_QUALITY,
        help=f"JPEG 质量 1–100，默认 {DEFAULT_QUALITY}",
    )
    parser.add_argument(
        "--ocr-max-side",
        type=int,
        default=DEFAULT_OCR_MAX_SIDE,
        help=f"OCR 预览长边，默认 {DEFAULT_OCR_MAX_SIDE}",
    )
    parser.add_argument("--overwrite", action="store_true", help="覆盖已存在的脱敏件")
    parser.add_argument("--dry-run", action="store_true", help="只打印动作，不写盘")
    parser.add_argument(
        "--suffix",
        default=DEFAULT_SUFFIX,
        help=f"插入在主名与扩展名之间，默认 {DEFAULT_SUFFIX}",
    )
    parser.add_argument(
        "--profile",
        default="",
        help="强制使用的规则 id（如 daotian）。默认按路径或图面文字选择",
    )
    parser.add_argument(
        "--list-profiles",
        action="store_true",
        help="列出规则 id 后退出",
    )
    return parser.parse_args(argv)


def dest_path_for(src: Path, suffix: str) -> Path:
    """源 `图纸.jpg` → 同目录 `图纸.desense.jpg`。"""
    return src.with_name(f"{src.stem}{suffix}.jpg")


def is_desense_output(path: Path, suffix: str) -> bool:
    stem = path.stem.lower()
    marker = suffix.lower()
    return stem.endswith(marker) or stem.endswith(marker.lstrip("."))


def collect_jpg_paths(root: Path, suffix: str) -> list[Path]:
    """收集 JPG：单文件只收这一份，目录则递归。排除已是脱敏产出。"""
    if root.is_file():
        if root.suffix.lower() not in {".jpg", ".jpeg"}:
            return []
        if is_desense_output(root, suffix):
            return []
        return [root]
    found: list[Path] = []
    for path in root.rglob("*"):
        if not path.is_file():
            continue
        if path.suffix.lower() not in {".jpg", ".jpeg"}:
            continue
        if is_desense_output(path, suffix):
            continue
        if any(part == "_preview" for part in path.parts):
            continue
        found.append(path)
    found.sort()
    return found


def collect_jpg_paths_from(roots: list[Path], suffix: str) -> list[Path]:
    """按传入顺序展开各路径；同一文件只收一次。目录内仍排序。"""
    seen: set[Path] = set()
    found: list[Path] = []
    for root in roots:
        for path in collect_jpg_paths(root.resolve(), suffix):
            key = path.resolve()
            if key in seen:
                continue
            seen.add(key)
            found.append(path)
    return found


def _get_ocr_engine():
    """懒加载 RapidOCR；失败则本进程不再重试。"""
    global _OCR_ENGINE, _OCR_ENGINE_FAILED
    if _OCR_ENGINE_FAILED:
        return None
    if _OCR_ENGINE is not None:
        return _OCR_ENGINE
    try:
        from rapidocr import RapidOCR

        _OCR_ENGINE = RapidOCR(params={"Global.log_level": "ERROR"})
    except Exception as exc:
        _OCR_ENGINE_FAILED = True
        print(f"错误：RapidOCR 不可用（{exc}）", file=sys.stderr)
        return None
    return _OCR_ENGINE


def downscale_for_ocr(image: Image.Image, max_side: int) -> tuple[Image.Image, float]:
    """把长边限制在 max_side，返回预览与映回原图的倍率。"""
    width, height = image.size
    longest = max(width, height)
    if longest <= max_side:
        return image, 1.0
    scale = max_side / longest
    preview = image.resize(
        (max(1, int(width * scale)), max(1, int(height * scale))),
        Image.Resampling.LANCZOS,
    )
    return preview, 1.0 / scale


def run_ocr(image: Image.Image, max_side: int) -> list[OcrLine]:
    """在限长边预览上识别，坐标归一化到 0～1。"""
    engine = _get_ocr_engine()
    if engine is None:
        raise RuntimeError("RapidOCR 不可用")
    preview, _inv = downscale_for_ocr(image.convert("RGB"), max_side)
    pw, ph = preview.size
    import numpy as np

    result = engine(np.asarray(preview))
    boxes = result.boxes
    texts = result.txts
    scores = result.scores
    if texts is None or boxes is None:
        return []
    lines: list[OcrLine] = []
    for box, text, score in zip(boxes, texts, scores):
        raw = str(text).strip()
        if not raw:
            continue
        xs = [float(p[0]) for p in box]
        ys = [float(p[1]) for p in box]
        lines.append(
            OcrLine(
                text=raw,
                score=float(score),
                x0=min(xs) / pw,
                y0=min(ys) / ph,
                x1=max(xs) / pw,
                y1=max(ys) / ph,
            )
        )
    return lines


def _contains_key(text: str, key: str) -> bool:
    if key.isascii():
        return key.lower() in text.lower()
    return key in text


def _is_anchor(line: OcrLine) -> bool:
    return any(_contains_key(line.text, key) for key in ANCHOR_KEYS)


def _is_keep_title(line: OcrLine, keep_keys: list[str]) -> bool:
    return any(_contains_key(line.text, key) for key in keep_keys)


def _norm_label(text: str) -> str:
    return re.sub(r"[\s:：.]+", "", text).upper()


def infer_title_side(image: Image.Image, title: TitleBlock | None) -> str:
    """有图签用其实测朝向；否则竖图按底边、横图按右侧。"""
    if title is not None:
        return title.side
    width, height = image.size
    return "bottom" if height > width * 1.05 else "right"


def ocr_title_strip(
    image: Image.Image,
    side: str,
    title: TitleBlock | None,
    max_side: int,
) -> list[OcrLine]:
    """在图签条带上重跑 OCR，坐标映回整页。大图全页限长边会把签字栏缩得太小。"""
    width, height = image.size
    if side == "right":
        left_ratio = (title.x0 - 0.02) if title is not None else 0.86
        left_ratio = max(0.0, min(0.92, left_ratio))
        box = (int(left_ratio * width), 0, width, height)
    else:
        top_ratio = (title.y0 - 0.02) if title is not None else 0.86
        top_ratio = max(0.0, min(0.90, top_ratio))
        box = (0, int(top_ratio * height), width, height)
    crop = image.crop(box)
    if crop.width < 8 or crop.height < 8:
        return []
    local = run_ocr(crop, max_side)
    cx0, cy0, cx1, cy1 = box
    crop_w = max(1, cx1 - cx0)
    crop_h = max(1, cy1 - cy0)
    remapped: list[OcrLine] = []
    for line in local:
        remapped.append(
            OcrLine(
                text=line.text,
                score=line.score,
                x0=(cx0 + line.x0 * crop_w) / width,
                y0=(cy0 + line.y0 * crop_h) / height,
                x1=(cx0 + line.x1 * crop_w) / width,
                y1=(cy0 + line.y1 * crop_h) / height,
            )
        )
    return remapped


def refine_title_ocr(
    image: Image.Image,
    page_lines: list[OcrLine],
    keep_keys: list[str],
    max_side: int,
) -> tuple[list[OcrLine], TitleBlock | None]:
    """先全页粗定位朝向，再条带精识别；竖图无锚点时按画幅推断底边。"""
    title = detect_title_block(page_lines, keep_keys)
    side = infer_title_side(image, title)
    strip_lines = ocr_title_strip(image, side, title, max_side)
    work_lines = strip_lines if strip_lines else page_lines
    refined = detect_title_block(work_lines, keep_keys) or title
    return work_lines, refined


def _strip_outer_edge(lines: list[OcrLine], title: TitleBlock) -> float:
    """条带外缘取条带内全部文字的外沿，不用锚点簇截断填写格。"""
    if title.side == "right":
        strip = [line for line in lines if line.cx >= title.x0 - 0.03]
        return min(0.994, max((line.x1 for line in strip), default=title.x1) + 0.004)
    strip = [line for line in lines if line.cy >= title.y0 - 0.03]
    return min(0.994, max((line.y1 for line in strip), default=title.y1) + 0.004)


def _typical_role_labels(
    labels: list[OcrLine],
    start: OcrLine,
    *,
    origin: float,
    span: float,
    along: str,
) -> list[OcrLine]:
    """加长图条带窄，DISCIPLINE 等竖排英文会伸进填写格，不能用它们定起点。"""
    if not labels:
        return [start]
    limit = origin + max(0.02, span) * 0.52
    if along == "x":
        typical = [line for line in labels if line.x1 <= limit]
    else:
        typical = [line for line in labels if line.y1 <= limit]
    return typical or labels


def detect_title_block(lines: list[OcrLine], keep_keys: list[str]) -> TitleBlock | None:
    """判断图签在右侧还是底边，并给出紧贴锚点簇的条带范围。"""
    anchors = [line for line in lines if _is_anchor(line)]
    right_cluster = [line for line in anchors if line.cx >= 0.70]
    bottom_cluster = [line for line in anchors if line.cy >= 0.70]
    aspects = sorted(line.aspect for line in anchors) if anchors else [1.0]
    median_aspect = aspects[len(aspects) // 2]
    if len(right_cluster) >= MIN_TITLE_ANCHORS and not (
        median_aspect < 0.85 and len(bottom_cluster) >= len(right_cluster)
    ):
        side = "right"
        cluster = right_cluster
    elif len(bottom_cluster) >= 2 or median_aspect < 0.85:
        side = "bottom"
        cluster = bottom_cluster or anchors
    elif len(right_cluster) >= 2:
        side = "right"
        cluster = right_cluster
    else:
        return None

    keep_lines = [line for line in cluster if _is_keep_title(line, keep_keys)]
    if side == "right":
        x0 = min(line.x0 for line in cluster)
        x1 = max(line.x1 for line in cluster)
        split = min((line.y0 for line in keep_lines), default=0.70)
        return TitleBlock(
            side="right",
            x0=max(0.0, x0 - 0.006),
            y0=0.0,
            x1=min(1.0, x1 + 0.02),
            y1=1.0,
            split=split,
        )
    y0 = min(line.y0 for line in cluster)
    y1 = max(line.y1 for line in cluster)
    x0 = min(line.x0 for line in cluster)
    x1 = max(line.x1 for line in cluster)
    split = min((line.x0 for line in keep_lines), default=x0 + 0.08) if keep_lines else x0 + 0.08
    return TitleBlock(
        side="bottom",
        x0=max(0.0, x0 - 0.01),
        y0=max(0.0, y0 - 0.006),
        x1=min(1.0, x1 + 0.02),
        y1=min(1.0, y1 + 0.02),
        split=split,
    )


def resolve_profile(
    src: Path,
    lines: list[OcrLine],
    profiles: dict[str, dict[str, Any]],
    explicit: str,
) -> dict[str, Any]:
    """按强制开关、路径公司名、图面文字选择规则。"""
    if explicit:
        if explicit not in profiles:
            known = "、".join(sorted(profiles))
            raise RuntimeError(f"未知规则 {explicit}。可选：{known}")
        return profiles[explicit]
    path_text = str(src)
    ranked = sorted(
        profiles.values(),
        key=lambda item: max((len(n) for n in item.get("path_needles") or [""]), default=0),
        reverse=True,
    )
    for profile in ranked:
        for needle in profile.get("path_needles") or []:
            if needle and needle in path_text:
                return profile
    blob = " ".join(line.text for line in lines)
    for profile in ranked:
        for needle in profile.get("ocr_needles") or []:
            if needle and needle in blob:
                return profile
    return profiles["generic"]


def warn_pending(profile: dict[str, Any]) -> None:
    """每个进程对未实测规则只提示一次。"""
    if not profile.get("pending"):
        return
    profile_id = profile["id"]
    if profile_id in _WARNED_PENDING:
        return
    _WARNED_PENDING.add(profile_id)
    note = profile.get("notes") or "图签条目尚未按实测填写。"
    print(f"警告：规则 {profile_id}（{profile.get('zh', '')}）待补全，{note}", file=sys.stderr)


def _field_matches_line(keys: list[str], text: str) -> bool:
    upper = text.upper()
    compact = re.sub(r"\s+", "", text)
    for key in keys:
        if key.upper() == "PROJECT" and any(word in upper for word in ("NO", "DIRECTOR")):
            continue
        if key == "设计":
            if "单位" in text:
                continue
            if "DESIGNED" in upper:
                return True
            if compact.startswith("设计") and len(compact) <= 10:
                return True
            continue
        if key.upper() in {"DRAWN", "DRAWN BY"} and "TITLE" in upper:
            continue
        if key.upper() == "DATE" and "UPDATE" in upper:
            continue
        if _contains_key(text, key):
            return True
    return False


def _in_title_cluster(line: OcrLine, title: TitleBlock) -> bool:
    if title.side == "right":
        return line.cx >= title.x0 - 0.02
    return line.cy >= title.y0 - 0.02


def _in_keep_zone(line: OcrLine, title: TitleBlock | None) -> bool:
    if title is None:
        return False
    if not _in_title_cluster(line, title):
        return False
    if title.side == "right":
        return line.y0 >= title.split - 0.008
    return line.x1 <= title.split + 0.02


def _portion(
    x0: float,
    y0: float,
    x1: float,
    y1: float,
    frac: dict[str, list[float]],
) -> tuple[float, float, float, float]:
    """取矩形内中部/右侧的一部分，允许盖不全。"""
    fx = frac.get("x") or [0.30, 0.86]
    fy = frac.get("y") or [0.15, 0.85]
    w = max(1e-6, x1 - x0)
    h = max(1e-6, y1 - y0)
    return (
        x0 + w * fx[0],
        y0 + h * fy[0],
        x0 + w * fx[1],
        y0 + h * fy[1],
    )


def _clip_rect(rect: tuple[float, float, float, float]) -> tuple[float, float, float, float] | None:
    x0, y0, x1, y1 = rect
    x0 = max(0.0, min(1.0, x0))
    y0 = max(0.0, min(1.0, y0))
    x1 = max(0.0, min(1.0, x1))
    y1 = max(0.0, min(1.0, y1))
    if x1 - x0 < 0.004 or y1 - y0 < 0.003:
        return None
    return (x0, y0, x1, y1)


def _looks_like_entity(text: str, profile: dict[str, Any]) -> bool:
    keep = profile.get("cover_keep") or []
    if any(title in text for title in keep):
        return False
    keep_keys = profile.get("title_keep") or []
    if any(_contains_key(text, key) for key in keep_keys):
        return False
    if len(text) < MIN_ENTITY_LEN:
        return False
    return any(hint in text for hint in (profile.get("entity_hints") or []))


def extract_entities(lines: list[OcrLine], title: TitleBlock | None, profile: dict[str, Any]) -> list[str]:
    """收集公司名与项目名，供画心/说明二次命中。"""
    labels = tuple(profile.get("body_value_labels") or [])
    labeled_re = re.compile(rf"^({'|'.join(map(re.escape, labels))})[：:]\s*(.+)$") if labels else None
    entities: list[str] = []
    for line in lines:
        text = line.text.strip()
        if labeled_re:
            matched = labeled_re.match(text)
            if matched and len(matched.group(2).strip()) >= 2:
                entities.append(matched.group(2).strip())
                continue
        if _looks_like_entity(text, profile):
            entities.append(re.sub(r"\s+", "", text))
    unique: list[str] = []
    for item in entities:
        cleaned = re.sub(r"\s+", "", item)
        if len(cleaned) < MIN_ENTITY_LEN or cleaned in unique:
            continue
        unique.append(cleaned)
    unique.sort(key=len, reverse=True)
    return unique


def line_hits_entity(line: OcrLine, entities: list[str]) -> bool:
    text = re.sub(r"\s+", "", line.text)
    for entity in entities:
        if entity in text or (len(text) >= MIN_ENTITY_LEN and text in entity):
            return True
    return False


def _text_mid_rect(line: OcrLine, fills: dict[str, Any]) -> tuple[float, float, float, float] | None:
    return _clip_rect(_portion(line.x0, line.y0, line.x1, line.y1, fills.get("text_mid") or {}))


def skip_reason_for_path(src: Path, profile: dict[str, Any]) -> str | None:
    """封面、说明、目录不入库作品集，路径命中则不脱敏。"""
    path_text = str(src)
    for needle in profile.get("skip_path_needles") or []:
        if needle and needle in path_text:
            return f"非图签页（路径含「{needle}」）"
    return None


def skip_reason_for_ocr(lines: list[OcrLine], profile: dict[str, Any]) -> str | None:
    """路径未标出时，用封面题名再跳过。"""
    blob = " ".join(line.text for line in lines)
    for needle in profile.get("skip_ocr_needles") or []:
        if needle and needle in blob:
            return "非图签页（封面/说明）"
    return None


def collect_signature_cluster_rect(
    lines: list[OcrLine],
    title: TitleBlock,
    profile: dict[str, Any],
) -> list[tuple[float, float, float, float]]:
    """签字填写区合成一框。道田为审定～设计；日清为项目负责人～审核。不盖左侧标签。"""
    spec = profile.get("signature_cluster") or {}
    if not spec:
        return []
    start_keys = list(spec.get("start_keys") or [])
    end_keys = list(spec.get("end_keys") or [])
    stop_keys = list(spec.get("stop_before_keys") or [])
    label_keys = list(spec.get("label_keys") or start_keys)
    member_keys = list(spec.get("member_keys") or label_keys)
    cluster = [line for line in lines if _in_title_cluster(line, title)]
    members = [line for line in cluster if _field_matches_line(member_keys, line.text)]
    starts = [line for line in cluster if _field_matches_line(start_keys, line.text)]
    ends = [line for line in cluster if _field_matches_line(end_keys, line.text)]
    stops = [line for line in cluster if _field_matches_line(stop_keys, line.text)]
    if not starts and not members:
        return []

    prefer = str(spec.get("prefer") or "top")

    if title.side == "right":
        if prefer == "bottom":
            seed = max(starts or members, key=lambda line: line.y0)
            # 同一格常有中英两行，取该格顶，避免英文本行把遮罩下移
            near = [
                line
                for line in (starts or members)
                if seed.y0 - line.y0 <= 0.028
            ]
            start = min(near, key=lambda line: line.y0)
            members = [line for line in members if line.y0 >= start.y0 - 0.012]
            ends = [line for line in ends if line.y0 >= start.y0 - 0.012]
            stops = [line for line in stops if line.y0 > start.y1]
        else:
            start = min(starts or members, key=lambda line: line.y0)
        end = max(ends, key=lambda line: line.y1) if ends else None
        stop = min(stops, key=lambda line: line.y0) if stops else None
        y0 = start.y0 - 0.003
        if end is not None:
            y1 = end.y1 + 0.008
        elif stop is not None:
            y1 = stop.y0 - 0.004
        else:
            y1 = max(line.y1 for line in members) + 0.006
        if stop is not None:
            y1 = min(y1, stop.y0 - 0.003)
        role_labels = [
            line
            for line in cluster
            if _field_matches_line(label_keys + member_keys, line.text)
            and line.y1 >= y0
            and line.y0 <= y1
        ]
        if not role_labels:
            role_labels = [start]
        # 填写格从常规标签右缘起，伸到条带外缘；条带越窄（加长图）起点越靠左
        outer_x1 = _strip_outer_edge(lines, title)
        strip_w = max(0.02, outer_x1 - title.x0)
        typical = _typical_role_labels(
            role_labels, start, origin=title.x0, span=strip_w, along="x"
        )
        x0 = max(line.x1 for line in typical) + 0.003
        fill_from = 0.36 if strip_w < 0.08 else 0.42
        x0 = max(x0, title.x0 + strip_w * fill_from)
        x1 = outer_x1
        rect = _clip_rect((x0, y0, x1, y1))
        return [rect] if rect else []

    start = max(starts or members, key=lambda line: line.x1)
    end = min(ends, key=lambda line: line.x0) if ends else None
    stop = max(stops, key=lambda line: line.x1) if stops else None
    x1 = min(1.0, start.x1 + 0.008)
    if end is not None:
        x0 = end.x0 - 0.004
    else:
        x0 = min(line.x0 for line in members) - 0.004
    if stop is not None:
        x0 = max(x0, stop.x1 + 0.004)
    role_labels = [
        line
        for line in cluster
        if _field_matches_line(label_keys + member_keys, line.text)
        and line.x1 >= x0
        and line.x0 <= x1
    ]
    outer_y1 = _strip_outer_edge(lines, title)
    strip_h = max(0.02, outer_y1 - title.y0)
    typical = _typical_role_labels(
        role_labels, start, origin=title.y0, span=strip_h, along="y"
    )
    y0 = max((line.y1 for line in typical), default=title.y0) + 0.003
    fill_from = 0.22 if strip_h < 0.08 else 0.28
    y0 = max(y0, title.y0 + strip_h * fill_from)
    y1 = outer_y1
    rect = _clip_rect((x0, y0, x1, y1))
    return [rect] if rect else []


def collect_field_rects(
    lines: list[OcrLine],
    title: TitleBlock,
    profile: dict[str, Any],
) -> list[tuple[float, float, float, float]]:
    """按规则条目遮填写格中部或右侧，不整段覆盖图签。"""
    fills = profile.get("fill") or {}
    fields = profile.get("title_fields") or []
    if not fields:
        return []
    cluster = [line for line in lines if _in_title_cluster(line, title) and not _in_keep_zone(line, title)]
    used: set[int] = set()
    hits: list[tuple[int, OcrLine, dict[str, Any]]] = []
    for spec in fields:
        keys = spec.get("keys") or []
        found: OcrLine | None = None
        found_idx = -1
        for idx, line in enumerate(cluster):
            if idx in used:
                continue
            if _field_matches_line(keys, line.text):
                found = line
                found_idx = idx
                break
        if found is None:
            continue
        used.add(found_idx)
        hits.append((found_idx, found, spec))

    if title.side == "right":
        hits.sort(key=lambda item: item[1].y0)
    else:
        hits.sort(key=lambda item: item[1].x0, reverse=True)

    rects: list[tuple[float, float, float, float]] = []
    for i, (_idx, label, spec) in enumerate(hits):
        fill_name = spec.get("fill") or "row_right"
        frac = fills.get(fill_name) or fills.get("text_mid") or {}
        nxt = hits[i + 1][1] if i + 1 < len(hits) else None
        if title.side == "right":
            cell_x0, cell_x1 = title.x0, title.x1
            cell_y0 = label.y0 - 0.004
            cell_y1 = (nxt.y0 - 0.006) if nxt else max(label.y1 + 0.02, title.split - 0.01)
            if fill_name == "row_right":
                compact = re.sub(r"\s+", "", label.text)
                keys = spec.get("keys") or []
                longest = max((len(k) for k in keys), default=2)
                if len(compact) > longest + 1:
                    rect = _text_mid_rect(label, fills)
                    if rect:
                        rects.append(rect)
                    continue
                cell_x0 = min(title.x1, label.x1 + 0.004)
                cell_y0, cell_y1 = label.y0 - 0.004, label.y1 + 0.006
            elif fill_name == "seal_mid":
                cell_y1 = min(title.split - 0.01, label.y0 + 0.12)
            else:
                cap = label.y0 + 0.09
                cell_y1 = min(cell_y1, cap)
        else:
            cell_y0, cell_y1 = title.y0, title.y1
            cell_x1 = title.x1
            cell_x0 = nxt.x1 if nxt else title.split
            if fill_name == "row_right":
                cell_x0, cell_x1 = label.x0 - 0.004, label.x1 + 0.004
                cell_y0 = min(title.y1, label.y1 + 0.002)
                cell_y1 = title.y1
            elif fill_name == "seal_mid":
                cell_x0 = title.split
                cell_x1 = nxt.x0 if nxt else title.x1
        rect = _clip_rect(_portion(cell_x0, cell_y0, cell_x1, cell_y1, frac))
        if rect:
            rects.append(rect)
    return rects


def collect_mask_rects(
    lines: list[OcrLine],
    title: TitleBlock | None,
    entities: list[str],
    profile: dict[str, Any],
) -> list[tuple[float, float, float, float]]:
    """图签签字栏一框；仅当 mask_body 开启时才遮封面/说明实体。"""
    fills = profile.get("fill") or {}
    keep_titles = tuple(profile.get("cover_keep") or [])
    body_labels = tuple(profile.get("body_value_labels") or [])
    labeled_re = (
        re.compile(rf"^({'|'.join(map(re.escape, body_labels))})[：:]\s*(.+)$") if body_labels else None
    )
    time_label = profile.get("cover_time_label") or ""
    rects: list[tuple[float, float, float, float]] = []
    if title is not None:
        cluster_rects = collect_signature_cluster_rect(lines, title, profile)
        if cluster_rects:
            rects.extend(cluster_rects)
        else:
            rects.extend(collect_field_rects(lines, title, profile))
    if not profile.get("mask_body", False):
        return rects

    has_cover_title = any(any(k in line.text for k in keep_titles) for line in lines)
    for line in lines:
        text = line.text.strip()
        if any(keep in text for keep in keep_titles):
            continue
        if title is not None and _in_title_cluster(line, title):
            continue
        if labeled_re:
            matched = labeled_re.match(text)
            if matched and len(matched.group(2).strip()) >= 2:
                rect = _text_mid_rect(line, fills)
                if rect:
                    rects.append(rect)
                continue
        if time_label and time_label in text and (has_cover_title or title is None):
            rect = _text_mid_rect(line, fills)
            if rect:
                rects.append(rect)
            continue
        if line_hits_entity(line, entities) or (
            has_cover_title and title is None and _looks_like_entity(text, profile)
        ):
            rect = _text_mid_rect(line, fills)
            if rect:
                rects.append(rect)
    return rects


def classify_sheet(width: int, height: int) -> str | None:
    """按 200 DPI 成品像素分档图幅与横竖。合成小样不命中。"""
    landscape = width > height * 1.05
    short, long = min(width, height), max(width, height)
    if 2000 <= short <= 2800 and 2900 <= long <= 3800:
        return "a3_land" if landscape else "a3_port"
    if 2800 <= short <= 3900 and 4000 <= long <= 5400:
        return "a2_land" if landscape else "a2_port"
    if 4000 <= short <= 5400 and 5800 <= long <= 7800:
        return "a1_land" if landscape else "a1_port"
    if short >= 5600 or long >= 8500:
        return "a0_land" if landscape else "a0_port"
    return None


def apply_sheet_insets(
    rects: list[tuple[float, float, float, float]],
    width: int,
    height: int,
    profile: dict[str, Any],
) -> list[tuple[float, float, float, float]]:
    """按图幅把遮罩四边收进或放出若干像素。"""
    spec = (profile.get("sheet_insets") or {}).get(classify_sheet(width, height) or "")
    if not spec:
        return rects
    dx0 = int(spec.get("dx0") or 0)
    dy0 = int(spec.get("dy0") or 0)
    dx1 = int(spec.get("dx1") or 0)
    dy1 = int(spec.get("dy1") or 0)
    adjusted: list[tuple[float, float, float, float]] = []
    for x0, y0, x1, y1 in rects:
        px0 = x0 * width + dx0
        py0 = y0 * height + dy0
        px1 = x1 * width + dx1
        py1 = y1 * height + dy1
        rect = _clip_rect((px0 / width, py0 / height, px1 / width, py1 / height))
        if rect:
            adjusted.append(rect)
    return adjusted


def apply_masks(
    image: Image.Image,
    rects: list[tuple[float, float, float, float]],
    fill_rgb: tuple[int, int, int],
    profile: dict[str, Any] | None = None,
) -> Image.Image:
    """按归一化框在原图上铺深灰。"""
    rgb = image.convert("RGB")
    draw = ImageDraw.Draw(rgb)
    width, height = rgb.size
    if profile is not None:
        rects = apply_sheet_insets(rects, width, height, profile)
    for x0, y0, x1, y1 in rects:
        box = (
            int(x0 * width),
            int(y0 * height),
            int(x1 * width),
            int(y1 * height),
        )
        draw.rectangle(box, fill=fill_rgb)
    return rgb


def _load_watermark_font(size: int) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    """优先黑体加粗，供中央水印使用。"""
    for path in WATERMARK_FONT_CANDIDATES:
        if not path.exists():
            continue
        try:
            return ImageFont.truetype(str(path), size=size, index=0)
        except OSError:
            continue
    return ImageFont.load_default()


def apply_center_watermark(image: Image.Image) -> Image.Image:
    """在画心正中叠灰色「作品集展示用」，不透明度 10%。"""
    rgb = image.convert("RGB")
    width, height = rgb.size
    size = max(36, int(min(width, height) * WATERMARK_FONT_RATIO))
    overlay = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)
    alpha = max(1, min(255, int(round(255 * WATERMARK_OPACITY))))
    fill = (*WATERMARK_RGB, alpha)
    font = _load_watermark_font(size)
    bbox = draw.textbbox((0, 0), WATERMARK_TEXT, font=font)
    text_w = max(1, bbox[2] - bbox[0])
    text_h = max(1, bbox[3] - bbox[1])
    max_w = int(width * 0.72)
    while text_w > max_w and size > 28:
        size -= 8
        font = _load_watermark_font(size)
        bbox = draw.textbbox((0, 0), WATERMARK_TEXT, font=font)
        text_w = max(1, bbox[2] - bbox[0])
        text_h = max(1, bbox[3] - bbox[1])
    x = (width - text_w) // 2 - bbox[0]
    y = (height - text_h) // 2 - bbox[1]
    draw.text((x, y), WATERMARK_TEXT, font=font, fill=fill)
    return Image.alpha_composite(rgb.convert("RGBA"), overlay).convert("RGB")


def desense_image(
    image: Image.Image,
    ocr_max_side: int,
    src: Path,
    profiles: dict[str, dict[str, Any]],
    explicit_profile: str,
) -> tuple[Image.Image, int, str]:
    """识别并按公司规则分条目遮罩。"""
    lines = run_ocr(image, ocr_max_side)
    profile = resolve_profile(src, lines, profiles, explicit_profile)
    warn_pending(profile)
    keep_keys = list(profile.get("title_keep") or [])
    work_lines, title = refine_title_ocr(image, lines, keep_keys, ocr_max_side)
    entities = extract_entities(work_lines, title, profile)
    rects = collect_mask_rects(work_lines, title, entities, profile)
    fill = tuple(int(v) for v in (profile.get("fill_rgb") or [64, 64, 64]))
    if not rects:
        return image.convert("RGB"), 0, profile["id"]
    return apply_center_watermark(apply_masks(image, rects, fill, profile)), len(rects), profile["id"]


def desense_one(
    src: Path,
    *,
    suffix: str,
    quality: int,
    ocr_max_side: int,
    overwrite: bool,
    dry_run: bool,
    profiles: dict[str, dict[str, Any]],
    explicit_profile: str,
    stats: DesenseStats,
) -> None:
    """处理单个 JPG：已有脱敏件则跳过，否则识别后写盘。"""
    stats.scanned += 1
    display = str(src)
    dest = dest_path_for(src, suffix)
    if dest.exists() and dest.is_file() and not overwrite:
        stats.skipped_exists += 1
        print(f"[跳过] {display}  已有脱敏件：{dest.name}")
        return

    peek = resolve_profile(src, [], profiles, explicit_profile)
    skip_path = skip_reason_for_path(src, peek)
    if skip_path:
        stats.skipped_sheet += 1
        print(f"[跳过] {display}  {skip_path}")
        return

    if dry_run:
        stats.desensed += 1
        print(f"[预览] {display} -> {dest.name}")
        return

    try:
        with Image.open(src) as opened:
            image = opened.convert("RGB")
            lines = run_ocr(image, ocr_max_side)
            profile = resolve_profile(src, lines, profiles, explicit_profile)
            warn_pending(profile)
            skip_ocr = skip_reason_for_ocr(lines, profile)
            if skip_ocr:
                stats.skipped_sheet += 1
                print(f"[跳过] {display}  {skip_ocr}")
                return
            keep_keys = list(profile.get("title_keep") or [])
            work_lines, title = refine_title_ocr(image, lines, keep_keys, ocr_max_side)
            entities = extract_entities(work_lines, title, profile)
            rects = collect_mask_rects(work_lines, title, entities, profile)
            if not rects:
                stats.skipped_empty += 1
                print(f"[跳过] {display}  未定位到签字栏  规则 {profile['id']}")
                return
            fill = tuple(int(v) for v in (profile.get("fill_rgb") or [64, 64, 64]))
            result = apply_center_watermark(apply_masks(image, rects, fill, profile))
            result.save(str(dest), format="JPEG", quality=quality, optimize=True)
        stats.desensed += 1
        print(f"[脱敏] {display} -> {dest.name}  规则 {profile['id']}  遮罩 {len(rects)} 块")
    except Exception as exc:
        stats.failed += 1
        stats.fail_paths.append(display)
        print(f"[失败] {display}  无法处理：{exc}")


def print_summary(stats: DesenseStats, *, dry_run: bool) -> None:
    title = "预览汇总" if dry_run else "汇总"
    print()
    print(title)
    print(f"  扫描 {stats.scanned} 个 JPG")
    print(f"  {'将脱敏' if dry_run else '脱敏'} {stats.desensed}")
    print(f"  跳过（已存在） {stats.skipped_exists}")
    print(f"  跳过（封面/说明/目录） {stats.skipped_sheet}")
    print(f"  跳过（未定位签字栏） {stats.skipped_empty}")
    print(f"  失败 {stats.failed}")
    if stats.fail_paths:
        print("  失败文件：")
        for path in stats.fail_paths:
            print(f"    {path}")


def list_profiles(profiles: dict[str, dict[str, Any]]) -> None:
    print("规则")
    for profile_id, profile in sorted(profiles.items()):
        flag = "待补全" if profile.get("pending") else "可用"
        print(f"  {profile_id:16} {profile.get('zh', '')}  [{flag}]")


def validate_one_path(path: Path, suffix: str) -> str | None:
    """校验单个目录或 JPG。"""
    if not path.exists():
        return f"错误：路径不存在：{path}"
    if path.is_file():
        if path.suffix.lower() not in {".jpg", ".jpeg"}:
            return f"错误：不是 JPG：{path}"
        if is_desense_output(path, suffix):
            return f"错误：已是脱敏件，不能作为输入：{path}"
        return None
    if path.is_dir():
        return None
    return f"错误：路径不是目录或文件：{path}"


def validate_args(args: argparse.Namespace, profiles: dict[str, dict[str, Any]]) -> str | None:
    if args.list_profiles:
        return None
    if not args.paths:
        return "错误：必须提供目录或 JPG，或使用 --list-profiles"
    for path in args.paths:
        error = validate_one_path(path, args.suffix)
        if error:
            return error
    if not (1 <= args.quality <= 100):
        return f"错误：--quality 须在 1～100 之间"
    if not (MIN_OCR_MAX_SIDE <= args.ocr_max_side <= MAX_OCR_MAX_SIDE):
        return f"错误：--ocr-max-side 须在 {MIN_OCR_MAX_SIDE}～{MAX_OCR_MAX_SIDE} 之间"
    if not args.suffix.startswith("."):
        return "错误：--suffix 须以点开头，例如 .desense"
    if args.profile and args.profile not in profiles:
        known = "、".join(sorted(profiles))
        return f"错误：未知规则 {args.profile}。可选：{known}"
    return None


def main(argv: list[str] | None = None) -> int:
    configure_stdio()
    args = parse_args(argv)
    try:
        profiles = load_profiles()
    except Exception as exc:
        print(f"错误：{exc}", file=sys.stderr)
        return 2
    error = validate_args(args, profiles)
    if error:
        print(error, file=sys.stderr)
        return 2
    if args.list_profiles:
        list_profiles(profiles)
        return 0
    if _get_ocr_engine() is None:
        return 2

    jpg_paths = collect_jpg_paths_from(args.paths, args.suffix)
    if not jpg_paths:
        joined = "；".join(str(path.resolve()) for path in args.paths)
        print(f"未找到 JPG：{joined}")
        return 0

    stats = DesenseStats()
    try:
        for jpg_path in jpg_paths:
            desense_one(
                jpg_path,
                suffix=args.suffix,
                quality=args.quality,
                ocr_max_side=args.ocr_max_side,
                overwrite=args.overwrite,
                dry_run=args.dry_run,
                profiles=profiles,
                explicit_profile=args.profile,
                stats=stats,
            )
    except KeyboardInterrupt:
        print("\n已中断。已写入的脱敏件保留。", file=sys.stderr)
        print_summary(stats, dry_run=args.dry_run)
        return 130

    print_summary(stats, dry_run=args.dry_run)
    return 1 if stats.failed else 0


if __name__ == "__main__":
    sys.exit(main())
