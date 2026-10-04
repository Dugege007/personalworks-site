# PDF转JPG_施工图用

扫描指定目录（递归全部子目录）、单个或多个 PDF（可分属不同目录），将**单页** PDF 转为同目录、同主名的 JPG。面向景观施工图（`landscape-cds` / L. CDS）。多页 PDF 默认跳过。默认按文字方向或图签位置把画面转到正向。方案见 `Docs/工具开发/PDF转JPG_施工图用.md`。

## 安装

```powershell
cd 工具\PDF转JPG_施工图用
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt
```

## 调用

也可在 Cursor 聊天输入 `/pdf2jpg4lcds`，空格后跟目录或一个/多个 PDF。

也可直接调用 `pdf2jpg4lcds.bat`（优先使用本目录 `.venv`）。

```powershell
python pdf2jpg4lcds.py <路径>
python pdf2jpg4lcds.py <路径1> <路径2>
python pdf2jpg4lcds.py <路径> --dry-run
python pdf2jpg4lcds.py <路径> --dpi 150 --quality 92 --overwrite
python pdf2jpg4lcds.py <路径> --no-auto-rotate
```

| 参数 | 默认 | 说明 |
|------|------|------|
| `<路径>` | 必填，可重复 | 已存在的目录（递归）或 PDF；可分属不同目录，按传入顺序处理 |
| `--dpi` | 200 | 渲染分辨率，72～600 |
| `--quality` | 92 | JPEG 质量，1～100 |
| `--overwrite` | 关 | 覆盖已存在的同名 JPG |
| `--dry-run` | 关 | 只打印动作，不写盘 |
| `--no-auto-rotate` | 关 | 关闭自动旋转，按 PDF 页面原方向输出 |
| `--include-multipage` | 关 | 多页转换尚未实现，传入会报错退出 |

`samples/` 内含正向单页、嵌套多页，以及文字整体侧置的 `text-ccw90.pdf`。

单页：`图纸.pdf` → `图纸.jpg`。不删除源 PDF。同目录已有同名 JPG 时直接跳过，不打开该 PDF。需要重转时加 `--overwrite`。

先按图面长条文字行方向校正（RapidOCR 只取检测框，排除图签、边框，以及近似方形的尺寸数字与装饰单字）：竖向过半则转，横向过半则不转。图面字不足时改用图签条文字角度。格栅密度只在文字证据仍不足时辅助找图签。不以画幅长短决定横竖。本机不装 Tesseract。依赖见 `requirements.txt`（含 `rapidocr`、`onnxruntime`）。

`--include-multipage` 为后续可选项，接通后多页将输出 `图纸_01.jpg` 起的编号文件；未传该开关时行为不变。
