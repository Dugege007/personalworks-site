"""将目录内或单个单页 PDF 转为同名 JPG。"""

from __future__ import annotations

import argparse
import math
import sys
from collections import Counter
from dataclasses import dataclass, field
from pathlib import Path

try:
    import pymupdf
except ImportError:
    print("错误：未安装 pymupdf。请先执行：pip install -r requirements.txt", file=sys.stderr)
    sys.exit(1)

try:
    from PIL import Image
except ImportError:
    print("错误：未安装 Pillow。请先执行：pip install -r requirements.txt", file=sys.stderr)
    sys.exit(1)

DEFAULT_DPI = 200
DEFAULT_QUALITY = 92
MIN_DPI = 72
MAX_DPI = 600
PREVIEW_DPI = 36
PREVIEW_MAX_SIDE = 1000
MIN_TEXT_CHARS = 12
MIN_TEXT_MAJORITY = 0.55
MIN_GRID_ABS = 25.0
MIN_GRID_RATIO = 1.25
# 图签条带取约 1/16，避免把目录表或总图填充算进图签
FIG_BAND_DIV = 16
# 图签贴右：将网格度最高的边转到右侧所需的顺时针角度
EDGE_TO_CW = {"right": 0, "top": 90, "left": 180, "bottom": 270}
# 视觉文字检测：只统计长条文字行的方向，排除边框与图签条
OCR_DPI = 72
OCR_MAX_SIDE = 2000
MIN_OCR_BOXES = 3
MIN_OCR_MAJORITY = 0.5
# 近似方形的尺寸数字、装饰单字不参与横竖投票
MIN_TEXT_ASPECT = 2.0
OCR_BORDER_FRAC = 0.04
OCR_TB_STRIP_FRAC = 0.12
PORTRAIT_RATIO = 1.15

_OCR_ENGINE = None
_OCR_ENGINE_FAILED = False
# TODO: 打开 --include-multipage 后按方案 3.5 为多页写编号 JPG


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


@dataclass
class ConvertStats:
    scanned: int = 0
    converted: int = 0
    rotated: int = 0
    skipped_multipage: int = 0
    skipped_exists: int = 0
    failed: int = 0
    fail_paths: list[str] = field(default_factory=list)


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        prog="pdf2jpg4lcds",
        description="扫描目录（递归）、单个或多个 PDF（可分属不同目录），将单页 PDF 转为同目录、同主名的 JPG（景观施工图用）。多页默认跳过。默认按图面长条文字行方向校正朝向，格栅图签仅作不足时的辅助。",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=(
            "示例：\n"
            "  python pdf2jpg4lcds.py D:\\drawings\n"
            "  python pdf2jpg4lcds.py D:\\drawings\\图纸.pdf\n"
            "  python pdf2jpg4lcds.py D:\\a\\1.pdf D:\\b\\2.pdf\n"
            "  python pdf2jpg4lcds.py D:\\drawings --dry-run\n"
            "  python pdf2jpg4lcds.py D:\\drawings --dpi 150 --overwrite\n"
            "  python pdf2jpg4lcds.py D:\\drawings --no-auto-rotate\n"
        ),
    )
    parser.add_argument(
        "paths",
        nargs="+",
        type=Path,
        metavar="路径",
        help="要扫描的目录或 PDF，可多个、可分属不同目录；按传入顺序处理",
    )
    parser.add_argument("--dpi", type=int, default=DEFAULT_DPI, help=f"渲染分辨率，默认 {DEFAULT_DPI}")
    parser.add_argument("--quality", type=int, default=DEFAULT_QUALITY, help=f"JPEG 质量 1–100，默认 {DEFAULT_QUALITY}")
    parser.add_argument("--overwrite", action="store_true", help="覆盖已存在的同名 JPG")
    parser.add_argument("--dry-run", action="store_true", help="只打印动作，不写盘")
    parser.add_argument(
        "--no-auto-rotate",
        action="store_true",
        help="关闭自动旋转，按 PDF 页面原方向输出",
    )
    parser.add_argument(
        "--include-multipage",
        action="store_true",
        help="同时转换多页 PDF（尚未实现）",
    )
    return parser.parse_args(argv)


def collect_pdf_paths(root: Path) -> list[Path]:
    """收集 PDF：单文件只收这一份，目录则递归。按路径排序。"""
    if root.is_file():
        if root.suffix.lower() != ".pdf":
            return []
        return [root]
    found: list[Path] = []
    for path in root.rglob("*"):
        if path.is_file() and path.suffix.lower() == ".pdf":
            found.append(path)
    found.sort()
    return found


def collect_pdf_paths_from(roots: list[Path]) -> list[Path]:
    """按传入顺序展开各路径；同一文件只收一次。目录内仍排序。"""
    seen: set[Path] = set()
    found: list[Path] = []
    for root in roots:
        for path in collect_pdf_paths(root.resolve()):
            key = path.resolve()
            if key in seen:
                continue
            seen.add(key)
            found.append(path)
    return found


def jpg_path_for(pdf_path: Path) -> Path:
    """单页输出：与 PDF 同目录、同主名、扩展名为 .jpg。"""
    return pdf_path.with_suffix(".jpg")


def pixmap_to_image(pixmap: pymupdf.Pixmap) -> Image.Image:
    """将 RGB Pixmap 转为 Pillow 图像。"""
    return Image.frombytes("RGB", (pixmap.width, pixmap.height), pixmap.samples)


def rotate_image_cw(image: Image.Image, degrees: int) -> Image.Image:
    """顺时针旋转 0/90/180/270。Pillow 的 ROTATE_* 为逆时针，需对换 90 与 270。"""
    degrees = degrees % 360
    if degrees == 90:
        return image.transpose(Image.Transpose.ROTATE_270)
    if degrees == 180:
        return image.transpose(Image.Transpose.ROTATE_180)
    if degrees == 270:
        return image.transpose(Image.Transpose.ROTATE_90)
    return image


def quantize_writing_dir(dx: float, dy: float) -> int:
    """将 PyMuPDF 行方向 (cos, -sin) 量化为 0/90/180/270（逆时针书写角）。"""
    angle = math.degrees(math.atan2(-dy, dx)) % 360
    return int(round(angle / 90.0) * 90) % 360


def _get_ocr_engine():
    """懒加载 RapidOCR 检测引擎；失败则本进程不再重试。"""
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
        print(f"警告：RapidOCR 不可用，图面文字方向检测已跳过（{exc}）", file=sys.stderr)
        return None
    return _OCR_ENGINE


def _prepare_ocr_image(page: pymupdf.Page) -> Image.Image:
    """朝向检测固定 72 DPI 渲染，过长则缩到 OCR_MAX_SIDE。不用成品 DPI 预览，以免多检出短标注。"""
    pixmap = page.get_pixmap(dpi=OCR_DPI, alpha=False)
    image = pixmap_to_image(pixmap)
    width, height = image.size
    longest = max(width, height)
    if longest > OCR_MAX_SIDE:
        scale = OCR_MAX_SIDE / longest
        image = image.resize(
            (max(1, int(width * scale)), max(1, int(height * scale))),
            Image.Resampling.BILINEAR,
        )
    return image


def _box_geometry(box) -> tuple[float, float, bool, float, float]:
    """检测框中心、长边是否更接近竖向、长边长度、短边长度。"""
    p0, p1, p2 = box[0], box[1], box[2]
    edge0 = math.hypot(float(p1[0] - p0[0]), float(p1[1] - p0[1]))
    edge1 = math.hypot(float(p2[0] - p1[0]), float(p2[1] - p1[1]))
    if edge0 >= edge1:
        dx, dy = float(p1[0] - p0[0]), float(p1[1] - p0[1])
        long_edge, short_edge = edge0, edge1
    else:
        dx, dy = float(p2[0] - p1[0]), float(p2[1] - p1[1])
        long_edge, short_edge = edge1, edge0
    angle = abs(math.degrees(math.atan2(dy, dx))) % 180
    cx = (float(box[0][0]) + float(box[1][0]) + float(box[2][0]) + float(box[3][0])) / 4.0
    cy = (float(box[0][1]) + float(box[1][1]) + float(box[2][1]) + float(box[3][1])) / 4.0
    return cx, cy, 45.0 <= angle <= 135.0, long_edge, max(short_edge, 1.0)


def _title_block_edge(centers: list[tuple[float, float]], width: float, height: float) -> str | None:
    """四边条带内文字框最多的一边视为图签（或图签侧内容），用于从投票中排除。"""
    if not centers:
        return None
    counts = {"left": 0, "right": 0, "top": 0, "bottom": 0}
    strip_x = OCR_TB_STRIP_FRAC * width
    strip_y = OCR_TB_STRIP_FRAC * height
    for cx, cy in centers:
        if cx < strip_x:
            counts["left"] += 1
        elif cx > width - strip_x:
            counts["right"] += 1
        if cy < strip_y:
            counts["top"] += 1
        elif cy > height - strip_y:
            counts["bottom"] += 1
    if max(counts.values()) <= 0:
        return None
    return max(counts, key=lambda name: counts[name])


def _in_drawing_area(
    cx: float,
    cy: float,
    width: float,
    height: float,
    title_edge: str | None,
) -> bool:
    """排除边框与图签条带，只保留图面区域。"""
    border_x = OCR_BORDER_FRAC * width
    border_y = OCR_BORDER_FRAC * height
    if cx < border_x or cx > width - border_x or cy < border_y or cy > height - border_y:
        return False
    strip_x = OCR_TB_STRIP_FRAC * width
    strip_y = OCR_TB_STRIP_FRAC * height
    if title_edge == "left" and cx < strip_x:
        return False
    if title_edge == "right" and cx > width - strip_x:
        return False
    if title_edge == "top" and cy < strip_y:
        return False
    if title_edge == "bottom" and cy > height - strip_y:
        return False
    return True


def _rotation_from_hv_votes(vertical: int, horizontal: int, width: int, height: int) -> int | None:
    """长条文字竖向过半则转到横向可读；纵向页默认 270°（底边图签转到右侧）。"""
    total = vertical + horizontal
    if total < MIN_OCR_BOXES:
        return None
    if max(vertical, horizontal) / total < MIN_OCR_MAJORITY:
        return None
    if horizontal >= vertical:
        return 0
    if height > width * PORTRAIT_RATIO:
        return 270
    if width > height * PORTRAIT_RATIO:
        return 90
    return None


def detect_rotation_from_ocr(page: pymupdf.Page) -> int | None:
    """用 RapidOCR 长条文字行方向判断朝向；图面不足时改用图签条文字角度。证据不足则返回 None。"""
    engine = _get_ocr_engine()
    if engine is None:
        return None
    try:
        import numpy as np
    except ImportError:
        return None
    image = _prepare_ocr_image(page)
    width, height = image.size
    try:
        result = engine(np.asarray(image), use_det=True, use_cls=False, use_rec=False)
    except Exception:
        return None
    boxes = getattr(result, "boxes", None)
    if boxes is None or len(boxes) == 0:
        return None
    parsed = [_box_geometry(box) for box in boxes]
    title_edge = _title_block_edge([(cx, cy) for cx, cy, _, _, _ in parsed], float(width), float(height))
    draw_v = draw_h = 0
    strip_v = strip_h = 0
    for cx, cy, is_vertical, long_edge, short_edge in parsed:
        if long_edge / short_edge < MIN_TEXT_ASPECT:
            continue
        if _in_drawing_area(cx, cy, float(width), float(height), title_edge):
            if is_vertical:
                draw_v += 1
            else:
                draw_h += 1
        elif is_vertical:
            strip_v += 1
        else:
            strip_h += 1
    extra = _rotation_from_hv_votes(draw_v, draw_h, width, height)
    if extra is not None:
        return extra
    # 图面长条字不足（加长总图缩略后常只剩图签）：用图签条文字行方向，使图签文字转到正向。
    return _rotation_from_hv_votes(strip_v, strip_h, width, height)


def detect_rotation_from_text(page: pymupdf.Page) -> int | None:
    """按可提取文字的书写方向投票，返回使横排正向所需的顺时针角；证据不足则返回 None。"""
    votes: Counter[int] = Counter()
    try:
        data = page.get_text("dict")
    except Exception:
        return None
    for block in data.get("blocks", []):
        if block.get("type") != 0:
            continue
        for line in block.get("lines", []):
            dx, dy = line.get("dir", (1.0, 0.0))
            text = "".join(span.get("text", "") for span in line.get("spans", [])).strip()
            if not text:
                continue
            votes[quantize_writing_dir(dx, dy)] += len(text)
    total = sum(votes.values())
    if total < MIN_TEXT_CHARS:
        return None
    angle, count = votes.most_common(1)[0]
    if count / total < MIN_TEXT_MAJORITY:
        return None
    return angle


def _edge_grid_scores(image: Image.Image) -> dict[str, float]:
    """四边带宽约 1/16 的网格度：横竖边缘能量同时高时更像图签表格。"""
    gray = image.convert("L")
    pixels = gray.load()
    width, height = gray.size
    band_w = max(8, width // FIG_BAND_DIV)
    band_h = max(8, height // FIG_BAND_DIV)
    regions = {
        "left": (0, 0, band_w, height),
        "right": (width - band_w, 0, width, height),
        "top": (0, 0, width, band_h),
        "bottom": (0, height - band_h, width, height),
    }
    scores: dict[str, float] = {}
    for name, (x0, y0, x1, y1) in regions.items():
        h_acc = 0.0
        v_acc = 0.0
        count = 0
        for y in range(y0, y1):
            for x in range(x0, x1):
                val = pixels[x, y]
                count += 1
                if x + 1 < x1:
                    v_acc += abs(val - pixels[x + 1, y])
                if y + 1 < y1:
                    h_acc += abs(val - pixels[x, y + 1])
        if count == 0:
            scores[name] = 0.0
            continue
        h_e = h_acc / count
        v_e = v_acc / count
        scores[name] = min(h_e, v_e) * (h_e + v_e)
    return scores


def detect_rotation_from_image(image: Image.Image) -> int:
    """文字证据不足时的辅助：纵向页按短边细带图签转到右侧；横向页把网格度最高边转到右侧。"""
    orig_w, orig_h = image.size
    if max(orig_w, orig_h) > PREVIEW_MAX_SIDE:
        scale = PREVIEW_MAX_SIDE / max(orig_w, orig_h)
        image = image.resize(
            (max(1, int(orig_w * scale)), max(1, int(orig_h * scale))),
            Image.Resampling.BILINEAR,
        )
    scores = _edge_grid_scores(image)
    # 施工图横向图框常被存成纵向页：图签在短边。一律把较高网格的短边转到右侧。
    if orig_h > orig_w * 1.15:
        top, bottom = scores["top"], scores["bottom"]
        if max(top, bottom) < MIN_GRID_ABS:
            return 0
        return 90 if top >= bottom else 270

    ranked = sorted(scores.items(), key=lambda item: item[1], reverse=True)
    winner, best = ranked[0]
    second = ranked[1][1]
    if best < MIN_GRID_ABS:
        return 0
    if second > 0 and best / second < MIN_GRID_RATIO:
        return 0
    return EDGE_TO_CW[winner]


def detect_content_rotation(page: pymupdf.Page, preview: Image.Image | None = None) -> int:
    """校正朝向。先图面长条文字行方向，不足则图签条文字，再 PDF 文字层，最后细边图签。返回顺时针 0/90/180/270。"""
    extra = detect_rotation_from_ocr(page)
    if extra is not None:
        return extra
    extra = detect_rotation_from_text(page)
    if extra is not None:
        return extra
    if preview is None:
        pixmap = page.get_pixmap(dpi=PREVIEW_DPI, alpha=False)
        preview = pixmap_to_image(pixmap)
    return detect_rotation_from_image(preview)


def open_pdf(pdf_path: Path) -> pymupdf.Document:
    document = pymupdf.open(pdf_path)
    if document.needs_pass:
        document.close()
        raise RuntimeError("文件已加密，无法读取页数")
    if document.page_count < 1:
        document.close()
        raise RuntimeError("页数为 0")
    return document


def format_rotate_note(extra_cw: int) -> str:
    if extra_cw:
        return f"  顺时针旋转 {extra_cw}°"
    return ""


def convert_one(
    pdf_path: Path,
    *,
    dpi: int,
    quality: int,
    overwrite: bool,
    dry_run: bool,
    auto_rotate: bool,
    stats: ConvertStats,
) -> None:
    """处理单个 PDF：非单页则跳过，单页则写出 JPG。"""
    stats.scanned += 1
    display = str(pdf_path)
    dest = jpg_path_for(pdf_path)
    # 同目录已有同名 JPG 时不打开 PDF，避免大文件重跑时重复解析。
    if dest.exists() and dest.is_file() and not overwrite:
        stats.skipped_exists += 1
        print(f"[跳过] {display}  已有 JPG：{dest.name}")
        return

    try:
        document = open_pdf(pdf_path)
    except Exception as exc:
        stats.failed += 1
        stats.fail_paths.append(display)
        print(f"[失败] {display}  无法打开：{exc}")
        return

    try:
        page_count = document.page_count
        if page_count != 1:
            stats.skipped_multipage += 1
            print(f"[跳过] {display}  多页（{page_count} 页），默认仅处理单页")
            return

        page = document[0]
        extra_cw = 0

        if dry_run:
            extra_cw = detect_content_rotation(page) if auto_rotate else 0
            stats.converted += 1
            if extra_cw:
                stats.rotated += 1
            print(f"[预览] {display} -> {dest.name}{format_rotate_note(extra_cw)}")
            return

        zoom = dpi / 72.0
        pixmap = page.get_pixmap(matrix=pymupdf.Matrix(zoom, zoom), alpha=False)
        image = pixmap_to_image(pixmap)
        extra_cw = detect_content_rotation(page, preview=image) if auto_rotate else 0
        if extra_cw:
            image = rotate_image_cw(image, extra_cw)
            stats.rotated += 1
        image.save(str(dest), format="JPEG", quality=quality, optimize=True)
        stats.converted += 1
        print(f"[转换] {display} -> {dest.name}{format_rotate_note(extra_cw)}")
    except Exception as exc:
        stats.failed += 1
        stats.fail_paths.append(display)
        print(f"[失败] {display}  {exc}")
    finally:
        document.close()


def print_summary(stats: ConvertStats, *, dry_run: bool) -> None:
    title = "预览汇总" if dry_run else "汇总"
    print()
    print(title)
    print(f"  扫描 {stats.scanned} 个 PDF")
    print(f"  {'将转换' if dry_run else '转换'} {stats.converted}")
    print(f"  其中旋转 {stats.rotated}")
    print(f"  跳过（多页） {stats.skipped_multipage}")
    print(f"  跳过（已存在） {stats.skipped_exists}")
    print(f"  失败 {stats.failed}")
    if stats.fail_paths:
        print("  失败文件：")
        for path in stats.fail_paths:
            print(f"    {path}")


def validate_one_path(path: Path) -> str | None:
    """校验单个目录或 PDF。"""
    if not path.exists():
        return f"错误：路径不存在：{path}"
    if path.is_file():
        if path.suffix.lower() != ".pdf":
            return f"错误：不是 PDF：{path}"
        return None
    if path.is_dir():
        return None
    return f"错误：路径不是目录或文件：{path}"


def validate_args(args: argparse.Namespace) -> str | None:
    if args.include_multipage:
        return "错误：--include-multipage 尚未实现。多页转换见 Docs/工具开发/PDF转JPG_施工图用.md 阶段4。"
    for path in args.paths:
        error = validate_one_path(path)
        if error:
            return error
    if not (MIN_DPI <= args.dpi <= MAX_DPI):
        return f"错误：--dpi 须在 {MIN_DPI}～{MAX_DPI} 之间"
    if not (1 <= args.quality <= 100):
        return f"错误：--quality 须在 1～100 之间"
    return None


def main(argv: list[str] | None = None) -> int:
    configure_stdio()
    args = parse_args(argv)
    error = validate_args(args)
    if error:
        print(error, file=sys.stderr)
        return 2

    pdf_paths = collect_pdf_paths_from(args.paths)
    if not pdf_paths:
        joined = "；".join(str(path.resolve()) for path in args.paths)
        print(f"未找到 PDF：{joined}")
        return 0

    stats = ConvertStats()
    try:
        for pdf_path in pdf_paths:
            convert_one(
                pdf_path,
                dpi=args.dpi,
                quality=args.quality,
                overwrite=args.overwrite,
                dry_run=args.dry_run,
                auto_rotate=not args.no_auto_rotate,
                stats=stats,
            )
    except KeyboardInterrupt:
        print("\n已中断。已写入的 JPG 保留。", file=sys.stderr)
        print_summary(stats, dry_run=args.dry_run)
        return 130

    print_summary(stats, dry_run=args.dry_run)
    return 1 if stats.failed else 0


if __name__ == "__main__":
    sys.exit(main())
