# JPG脱敏_施工图用

扫描指定目录（递归全部子目录）、单个或多个 JPG（可分属不同目录），将景观施工图脱敏为同目录 `<主名>.desense.jpg`。面向已由 `PDF转JPG_施工图用` 转出的网图。源 JPG 不覆盖、不删除。方案见 `Docs/工具开发/JPG脱敏_施工图用.md`。

遮罩范围：封面、说明、目录跳过。总图/详图/通图只盖图签「审定～设计」填写区（深灰一框），不盖左侧标签，不盖开发公司、设计单位、工程名称、绘图、日期、图签、图名、专业、阶段、版本、图号、比例、图幅。脱敏件画心正中叠灰色半透明「作品集展示用」（加粗、不透明度 10%）。规则见 `profiles/`（道田：审定～设计；日清：项目负责人～审核；洛阳古建仍待补）。

## 安装

可新建环境，或直接使用 `PDF转JPG_施工图用` 的 `.venv`（本工具依赖为其子集，不需要 PyMuPDF）。

```powershell
cd 工具\JPG脱敏_施工图用
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt
```

## 调用

也可在 Cursor 聊天输入 `/jpgdesense4lcds`，空格后跟目录或一个/多个 JPG。

也可直接调用 `jpgdesense4lcds.bat`（优先本目录 `.venv`，其次 PDF 转换工具 `.venv`）。

```powershell
python jpgdesense4lcds.py <路径>
python jpgdesense4lcds.py <路径1> <路径2>
python jpgdesense4lcds.py <路径> --dry-run
python jpgdesense4lcds.py <路径> --profile daotian
python jpgdesense4lcds.py --list-profiles
python jpgdesense4lcds.py <路径> --quality 92 --ocr-max-side 2400
```

| 参数 | 默认 | 说明 |
|------|------|------|
| `<路径>` | 必填，可重复 | 已存在的目录（递归）或 JPG；可分属不同目录，按传入顺序处理 |
| `--quality` | 92 | JPEG 质量，1～100 |
| `--ocr-max-side` | 2400 | OCR 预览长边，800～4000 |
| `--overwrite` | 关 | 覆盖已存在的脱敏件 |
| `--dry-run` | 关 | 只打印动作，不写盘 |
| `--profile` | 空 | 强制规则 id；默认按路径公司名或图面文字选择 |
| `--list-profiles` | 关 | 列出规则后退出 |

`图纸.jpg` → `图纸.desense.jpg`。已是 `*.desense.jpg` 的文件不再作为输入。同目录已有脱敏件时跳过，需要重跑时加 `--overwrite`。

本机不装 Tesseract。依赖见 `requirements.txt`（含 `rapidocr`、`onnxruntime`）。

回归：

```powershell
python verify_samples.py
```
