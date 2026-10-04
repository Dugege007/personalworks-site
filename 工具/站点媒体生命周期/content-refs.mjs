import { createRequire } from "node:module";
import path from "node:path";

const SCRIPT_KINDS = new Map([
  [".ts", "TS"],
  [".tsx", "TSX"],
  [".js", "JS"],
  [".jsx", "JSX"],
  [".json", "JSON"],
]);

/**
 * 从已安装的站点开发依赖加载 TypeScript 语法解析器。
 * 这里只构造语法树，不编译或执行内容代码。
 */
export function loadTypeScript(sitePackagePath) {
  const require = createRequire(sitePackagePath);
  try {
    return require("typescript");
  } catch {
    throw new Error("缺少 typescript，无法安全解析内容引用；请在 PersonalSite 执行 npm install");
  }
}

function lineOf(ts, sourceFile, node) {
  return sourceFile.getLineAndCharacterOfPosition(node.getStart(sourceFile)).line + 1;
}

function staticString(ts, node) {
  if (ts.isStringLiteral(node) || ts.isNoSubstitutionTemplateLiteral(node)) {
    return node.text;
  }
  return undefined;
}

function staticInteger(ts, node) {
  if (!ts.isNumericLiteral(node)) return undefined;
  const value = Number(node.text);
  return Number.isSafeInteger(value) ? value : undefined;
}

function propertyName(ts, node) {
  if (ts.isIdentifier(node) || ts.isStringLiteral(node)) return node.text;
  return "";
}

function addHit(result, seen, object, file, line, dynamic, helper) {
  const normalized = String(object ?? "").trim().replace(/\\/g, "/").replace(/^\/+/, "");
  if (!normalized || !normalized.includes("/")) return;
  const key = `${normalized}\0${file}\0${line}`;
  if (seen.has(key)) {
    if (dynamic) {
      const existing = result.find(
        (item) => item.object === normalized && item.file === file && item.line === line,
      );
      if (existing) {
        existing.dynamic = true;
        existing.helper = helper;
      }
    }
    return;
  }
  seen.add(key);
  result.push({ object: normalized, file, line, dynamic, helper });
}

/**
 * 静态展开内容辅助函数产生的对象键。
 * 仅接受字面量参数；未知表达式直接忽略，不进行 eval 或模块加载。
 */
export function extractExpandedReferences(ts, text, file) {
  const ext = path.extname(file).toLowerCase();
  const scriptKindName = SCRIPT_KINDS.get(ext);
  if (!scriptKindName) return [];
  const sourceFile = ts.createSourceFile(
    file,
    text,
    ts.ScriptTarget.Latest,
    true,
    ts.ScriptKind[scriptKindName],
  );
  const result = [];
  const seen = new Set();

  const visit = (node) => {
    if (ts.isPropertyAssignment(node) && propertyName(ts, node.name) === "src") {
      const object = staticString(ts, node.initializer);
      if (object !== undefined) {
        addHit(result, seen, object, file, lineOf(ts, sourceFile, node.initializer), false, "src");
      }
    }

    if (ts.isCallExpression(node) && ts.isIdentifier(node.expression)) {
      const helper = node.expression.text;
      if (helper === "album") {
        const channel = staticString(ts, node.arguments[0]);
        const id = staticString(ts, node.arguments[1]);
        const count = staticInteger(ts, node.arguments[2]);
        const extName =
          node.arguments.length >= 4 ? staticString(ts, node.arguments[3]) : "jpg";
        if (
          channel !== undefined &&
          id !== undefined &&
          count !== undefined &&
          count >= 0 &&
          extName !== undefined
        ) {
          const line = lineOf(ts, sourceFile, node);
          for (let index = 1; index <= count; index += 1) {
            const slot = String(index).padStart(2, "0");
            addHit(
              result,
              seen,
              `${channel}/${id}/${slot}.${extName}`,
              file,
              line,
              true,
              helper,
            );
          }
        }
      } else if (helper === "cover" || helper === "covered") {
        const channel = staticString(ts, node.arguments[0]);
        const id = staticString(ts, node.arguments[1]);
        if (channel !== undefined && id !== undefined) {
          addHit(
            result,
            seen,
            `${channel}/${id}/01.webp`,
            file,
            lineOf(ts, sourceFile, node),
            true,
            helper,
          );
        }
      } else if (helper === "listed") {
        const entries = node.arguments[0];
        if (entries && ts.isArrayLiteralExpression(entries)) {
          for (const entry of entries.elements) {
            if (!ts.isArrayLiteralExpression(entry) || entry.elements.length === 0) continue;
            const sourceNode = entry.elements[0];
            const object = staticString(ts, sourceNode);
            if (object !== undefined) {
              addHit(
                result,
                seen,
                object,
                file,
                lineOf(ts, sourceFile, sourceNode),
                false,
                helper,
              );
            }
          }
        }
      }
    }

    ts.forEachChild(node, visit);
  };
  visit(sourceFile);
  return result;
}

/**
 * 合并逐行字面量命中与静态展开命中，并按文件、行去重。
 */
export function hitsInContentCorpus(corpus, object) {
  const hits = [];
  const byLocation = new Map();
  const add = (hit) => {
    const key = `${hit.file}\0${hit.line}`;
    const existing = byLocation.get(key);
    if (existing) {
      if (hit.dynamic) {
        existing.dynamic = true;
        existing.helper = hit.helper;
      }
      return;
    }
    const next = {
      file: hit.file,
      line: hit.line,
      dynamic: Boolean(hit.dynamic),
      helper: hit.helper ?? "",
    };
    byLocation.set(key, next);
    hits.push(next);
  };

  for (const file of corpus.files) {
    file.lines.forEach((line, index) => {
      if (line.includes(object)) {
        add({ file: file.rel, line: index + 1, dynamic: false, helper: "literal" });
      }
    });
  }
  for (const hit of corpus.expandedRefs) {
    if (hit.object === object) add(hit);
  }
  hits.sort((left, right) => left.file.localeCompare(right.file) || left.line - right.line);
  return hits;
}
