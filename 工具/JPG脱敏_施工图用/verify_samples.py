"""合成施工图脱敏样例并回归主路径。"""

from __future__ import annotations

import shutil
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent
SAMPLES = ROOT / "samples"
FONT_CANDIDATES = (
    Path(r"C:\Windows\Fonts\msyh.ttc"),
    Path(r"C:\Windows\Fonts\simhei.ttf"),
    Path(r"C:\Windows\Fonts\simsun.ttc"),
)

PROJECT = "北京世茂一渡项目六期C/D地块"
CLIENT = "国达房地产开发有限公司"
DESIGN = "上海道田景观工程咨询有限公司"
RIQING = "上海日清景观设计有限公司"
KEEP_TITLE = "景观施工图设计图册"
DRAWING_NAME = "景观总平面图"
DRAWING_NO = "LP-1.01"
ROLE_SENTENCE = "本工程竖向采用建设单位提供的标高系统。"


def load_font(size: int) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    for path in FONT_CANDIDATES:
        if path.exists():
            try:
                return ImageFont.truetype(str(path), size=size, index=0)
            except OSError:
                continue
    return ImageFont.load_default()


def new_sheet(size: tuple[int, int]) -> tuple[Image.Image, ImageDraw.ImageDraw]:
    image = Image.new("RGB", size, (255, 255, 255))
    draw = ImageDraw.Draw(image)
    draw.rectangle((8, 8, size[0] - 9, size[1] - 9), outline=(0, 0, 0), width=2)
    return image, draw


def draw_sign_block(draw: ImageDraw.ImageDraw, x_label: int, x_sign: int, y0: int, font) -> None:
    """左侧标签、右侧姓名，模拟道田图签签字栏。"""
    rows = (
        (0, "审定", "张三"),
        (22, "审核", "李四"),
        (44, "校对", "王五"),
        (66, "工程负责人", "赵六"),
        (88, "专业负责人", "钱七"),
        (110, "设计", "孙八"),
    )
    for dy, label, name in rows:
        draw.text((x_label, y0 + dy), label, fill=(0, 0, 0), font=font)
        draw.text((x_sign, y0 + dy), name, fill=(0, 0, 0), font=font)


def make_cover(path: Path) -> None:
    image, draw = new_sheet((1200, 850))
    draw.line((40, 560, 1160, 560), fill=(0, 0, 0), width=4)
    draw.text((48, 500), PROJECT, fill=(0, 0, 0), font=load_font(36))
    draw.text((48, 590), "出图时间：2019-04", fill=(0, 0, 0), font=load_font(22))
    draw.text((420, 620), KEEP_TITLE, fill=(0, 0, 0), font=load_font(32))
    draw.text((740, 760), f"建设单位：{CLIENT}", fill=(0, 0, 0), font=load_font(20))
    draw.text((740, 794), f"设计单位：{DESIGN}", fill=(0, 0, 0), font=load_font(20))
    image.save(path, format="JPEG", quality=92)


def make_right_title(path: Path) -> None:
    image, draw = new_sheet((1400, 990))
    draw.rectangle((40, 40, 1180, 940), outline=(180, 180, 180), width=1)
    draw.text((80, 80), "图面中央保留文字", fill=(0, 0, 0), font=load_font(28))
    x0 = 1200
    draw.line((x0, 20, x0, 970), fill=(0, 0, 0), width=2)
    font = load_font(16)
    for y, text in (
        (30, CLIENT),
        (90, "DESIGN"),
        (112, "设计单位"),
        (170, DESIGN),
        (260, "PROJECT"),
        (282, "工程名称"),
        (320, PROJECT),
    ):
        draw.text((x0 + 8, y), text, fill=(0, 0, 0), font=font)
    draw_sign_block(draw, x0 + 8, x0 + 108, 400, font)
    for y, text in (
        (530, "绘图 周九"),
        (552, "日期 2019-04"),
        (600, "SEAL"),
        (622, "图签"),
        (720, "DRAWNING TITLE"),
        (742, "图名"),
        (780, DRAWING_NAME),
        (860, f"图号 {DRAWING_NO}"),
    ):
        draw.text((x0 + 8, y), text, fill=(0, 0, 0), font=font)
    image.save(path, format="JPEG", quality=92)


def make_bottom_title(path: Path) -> None:
    """纵向页：把右侧图签条顺时针转 90° 贴在底边，文字侧置。"""
    image, draw = new_sheet((800, 1400))
    draw.text((80, 200), "纵向图面保留文字", fill=(0, 0, 0), font=load_font(28))
    strip_w, strip_h = 200, 1100
    strip = Image.new("RGB", (strip_w, strip_h), (255, 255, 255))
    sdraw = ImageDraw.Draw(strip)
    sdraw.rectangle((0, 0, strip_w - 1, strip_h - 1), outline=(0, 0, 0), width=2)
    font = load_font(16)
    for y, text in (
        (20, CLIENT),
        (80, "DESIGN"),
        (100, "设计单位"),
        (160, DESIGN),
        (240, "PROJECT"),
        (260, "工程名称"),
        (300, PROJECT),
    ):
        sdraw.text((8, y), text, fill=(0, 0, 0), font=font)
    draw_sign_block(sdraw, 8, 100, 360, font)
    for y, text in (
        (500, "绘图 周九"),
        (520, "日期 2019-04"),
        (580, "SEAL"),
        (600, "图签"),
        (720, "DRAWNING TITLE"),
        (740, "图名"),
        (780, DRAWING_NAME),
        (860, f"图号 {DRAWING_NO}"),
    ):
        sdraw.text((8, y), text, fill=(0, 0, 0), font=font)
    rotated = strip.rotate(-90, expand=True)
    rotated = rotated.resize((780, 210), Image.Resampling.LANCZOS)
    image.paste(rotated, (10, 1175))
    image.save(path, format="JPEG", quality=92)


def make_riqing_title(path: Path) -> None:
    """日清右侧图签：项目负责人～审核；上方另放一套项目信息，应取靠底一组。"""
    image, draw = new_sheet((1400, 990))
    draw.rectangle((40, 40, 1180, 940), outline=(180, 180, 180), width=1)
    draw.text((80, 80), "图面中央保留文字", fill=(0, 0, 0), font=load_font(28))
    x0 = 1200
    draw.line((x0, 20, x0, 970), fill=(0, 0, 0), width=2)
    font = load_font(16)
    for y, text in (
        (30, "项目名称"),
        (52, "南京星河住宅"),
        (80, "项目负责人"),
        (102, "旧戳"),
        (200, "CLIENT"),
        (222, "建设单位"),
        (250, RIQING),
        (300, "PROJECT"),
        (322, "项目名称"),
        (350, PROJECT),
        (400, "DRAWING TITLE"),
        (422, "图纸名称"),
        (450, DRAWING_NAME),
        (500, "项目负责人"),
        (522, "张三"),
        (548, "设计"),
        (570, "李四"),
        (596, "校准"),
        (618, "王五"),
        (644, "审核"),
        (666, "赵六"),
        (700, "SCALE"),
        (722, "比例"),
        (750, "DATE"),
        (772, "日期"),
        (800, "DRAWING NO"),
        (822, DRAWING_NO),
    ):
        draw.text((x0 + 8, y), text, fill=(0, 0, 0), font=font)
    image.save(path, format="JPEG", quality=92)


def make_notes(path: Path) -> None:
    image, draw = new_sheet((1400, 990))
    font = load_font(20)
    draw.text((48, 48), "一、工程概况", fill=(0, 0, 0), font=load_font(26))
    draw.text((48, 100), f"项目名称：{PROJECT}", fill=(0, 0, 0), font=font)
    draw.text((48, 140), f"建设单位：{CLIENT}", fill=(0, 0, 0), font=font)
    draw.text((48, 200), ROLE_SENTENCE, fill=(0, 0, 0), font=font)
    x0 = 1200
    draw.line((x0, 20, x0, 970), fill=(0, 0, 0), width=2)
    small = load_font(16)
    for y, text in (
        (30, CLIENT),
        (90, "设计单位"),
        (160, DESIGN),
        (240, "工程名称"),
        (280, PROJECT),
        (500, "SEAL"),
        (700, "图名"),
        (740, "施工图设计说明（一）"),
        (860, "图号 LN-2.01"),
    ):
        draw.text((x0 + 8, y), text, fill=(0, 0, 0), font=small)
    image.save(path, format="JPEG", quality=92)


def make_catalog(path: Path) -> None:
    image, draw = new_sheet((1400, 990))
    draw.text((80, 40), "项目名称" + PROJECT, fill=(0, 0, 0), font=load_font(22))
    draw.text((80, 90), "图纸编号", fill=(0, 0, 0), font=load_font(20))
    draw.text((280, 90), "图名", fill=(0, 0, 0), font=load_font(20))
    draw.text((80, 130), "LN-1.01", fill=(0, 0, 0), font=load_font(18))
    draw.text((280, 130), "图纸目录一", fill=(0, 0, 0), font=load_font(18))
    image.save(path, format="JPEG", quality=92)


def write_samples() -> None:
    SAMPLES.mkdir(parents=True, exist_ok=True)
    # 只清合成样例与其脱敏件，保留 live/ 实测施工图
    for path in list(SAMPLES.iterdir()):
        if path.name in {"live", "_preview"}:
            continue
        if path.is_dir():
            shutil.rmtree(path)
        else:
            path.unlink()
    nested = SAMPLES / "nested"
    nested.mkdir(parents=True)
    make_cover(SAMPLES / "封面.jpg")
    make_right_title(SAMPLES / "right-title.jpg")
    make_riqing_title(SAMPLES / "riqing-title.jpg")
    make_bottom_title(SAMPLES / "bottom-title.jpg")
    make_notes(SAMPLES / "施工图设计说明.jpg")
    make_catalog(SAMPLES / "图纸目录.jpg")
    make_cover(nested / "封面.jpg")
    (SAMPLES / "broken.jpg").write_bytes(b"not-a-jpeg")


def ocr_texts(path: Path) -> list[str]:
    from jpgdesense4lcds import run_ocr

    with Image.open(path) as image:
        lines = run_ocr(image.convert("RGB"), 2400)
    return [line.text for line in lines]


def assert_has_gray_mask(path: Path) -> None:
    """应出现深灰填充。"""
    with Image.open(path) as image:
        rgb = image.convert("RGB")
        width, height = rgb.size
        hits = 0
        for x in range(4, width - 1, 6):
            for y in range(4, height - 1, 6):
                pixel = rgb.getpixel((x, y))
                if all(abs(pixel[i] - 64) <= 24 for i in range(3)):
                    hits += 1
        if hits < 8:
            raise AssertionError(f"{path.name} 深灰遮罩过少：{hits}")


def assert_gray_not_in_drawing(path: Path, max_x_ratio: float) -> None:
    """画心不得出现成块深灰（单个抗锯齿像素不计）。"""
    with Image.open(path) as image:
        rgb = image.convert("RGB")
        width, height = rgb.size
        limit = int(width * max_x_ratio)
        hits = 0
        for x in range(4, limit, 8):
            for y in range(4, height - 1, 10):
                pixel = rgb.getpixel((x, y))
                if all(abs(pixel[i] - 64) <= 24 for i in range(3)):
                    hits += 1
        if hits >= 40:
            raise AssertionError(f"{path.name} 画心深灰过多：{hits}")


def assert_has_watermark(path: Path) -> None:
    """画心应有浅灰半透明字，而不是空白或深灰遮罩。"""
    with Image.open(path) as image:
        rgb = image.convert("RGB")
        width, height = rgb.size
        x0, x1 = int(width * 0.22), int(width * 0.78)
        y0, y1 = int(height * 0.38), int(height * 0.62)
        faint = 0
        for x in range(x0, x1, 4):
            for y in range(y0, y1, 4):
                pixel = rgb.getpixel((x, y))
                if max(pixel) - min(pixel) > 12:
                    continue
                if all(214 <= channel <= 250 for channel in pixel):
                    faint += 1
        if faint < 20:
            raise AssertionError(f"{path.name} 中央水印过淡或缺失：{faint}")


def assert_desensed(path: Path, *, must_keep: tuple[str, ...]) -> None:
    texts = ocr_texts(path)
    blob = " ".join(texts)
    missing = [item for item in must_keep if item not in blob]
    if missing:
        raise AssertionError(f"{path.name} keep_miss={missing} ocr={texts}")


def assert_not_written(*paths: Path) -> None:
    extra = [str(path) for path in paths if path.exists()]
    if extra:
        raise AssertionError(f"不应写出：{extra}")


def _stash_live() -> Path | None:
    """回归只跑合成样例，实测件先挪开。"""
    live = SAMPLES / "live"
    stash = ROOT / ".live_stash"
    if not live.is_dir():
        return None
    if stash.exists():
        shutil.rmtree(stash)
    shutil.move(str(live), str(stash))
    return stash


def _restore_live(stash: Path | None) -> None:
    if stash is None or not stash.exists():
        return
    dest = SAMPLES / "live"
    if dest.exists():
        shutil.rmtree(dest)
    shutil.move(str(stash), str(dest))


def main() -> int:
    sys.path.insert(0, str(ROOT))
    from jpgdesense4lcds import main as desense_main

    stash = _stash_live()
    try:
        return _run_regression(desense_main)
    finally:
        _restore_live(stash)


def _run_regression(desense_main) -> int:
    write_samples()
    code = desense_main([str(SAMPLES)])
    if code not in {0, 1}:
        print(f"脱敏进程异常退出：{code}", file=sys.stderr)
        return code

    assert_not_written(
        SAMPLES / "broken.desense.jpg",
        SAMPLES / "封面.desense.jpg",
        SAMPLES / "施工图设计说明.desense.jpg",
        SAMPLES / "图纸目录.desense.jpg",
        SAMPLES / "nested" / "封面.desense.jpg",
    )

    keep = (DRAWING_NAME, DRAWING_NO, "图面中央保留文字", "审定", "设计单位", "绘图")
    assert_desensed(SAMPLES / "right-title.desense.jpg", must_keep=keep)
    assert_has_gray_mask(SAMPLES / "right-title.desense.jpg")
    assert_gray_not_in_drawing(SAMPLES / "right-title.desense.jpg", 0.82)
    assert_has_watermark(SAMPLES / "right-title.desense.jpg")
    riqing_keep = (DRAWING_NAME, DRAWING_NO, "图面中央保留文字", "建设单位", "图纸名称", "比例")
    assert_desensed(SAMPLES / "riqing-title.desense.jpg", must_keep=riqing_keep)
    assert_has_gray_mask(SAMPLES / "riqing-title.desense.jpg")
    assert_gray_not_in_drawing(SAMPLES / "riqing-title.desense.jpg", 0.82)
    assert_has_watermark(SAMPLES / "riqing-title.desense.jpg")
    assert_desensed(SAMPLES / "bottom-title.desense.jpg", must_keep=("纵向图面保留文字", DRAWING_NAME))
    assert_has_gray_mask(SAMPLES / "bottom-title.desense.jpg")
    assert_has_watermark(SAMPLES / "bottom-title.desense.jpg")

    before = list(SAMPLES.rglob("*.desense.jpg"))
    skip_code = desense_main([str(SAMPLES)])
    after = list(SAMPLES.rglob("*.desense.jpg"))
    if skip_code not in {0, 1} or len(after) != len(before):
        raise AssertionError("第二次运行应跳过且不新增文件")

    dry_dir = SAMPLES / "dry"
    dry_dir.mkdir()
    shutil.copy(SAMPLES / "right-title.jpg", dry_dir / "right-title.jpg")
    dry_code = desense_main([str(dry_dir), "--dry-run"])
    if dry_code not in {0, 1}:
        raise AssertionError("dry-run 退出码异常")
    if list(dry_dir.glob("*.desense.jpg")):
        raise AssertionError("dry-run 不应写盘")

    print("样例回归通过")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except AssertionError as exc:
        print(f"回归失败：{exc}", file=sys.stderr)
        raise SystemExit(1)
