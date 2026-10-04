import assert from "node:assert/strict";
import test from "node:test";
import { annotateTerms, glossaryEntryFor, parseGlossary, tipParts } from "../src/content/glossary.ts";

const SOURCE = `
# 名词说明

## 名词

### FlexSim

别名：Flexsim
解释：产线仿真软件。详见 [FlexSim 官网](https://www.flexsim.com/)。
英文解释：Plant simulation software.

### 实时渲染

别名：渲染
解释：长词优先。

### Unity

解释：游戏引擎。

### A

解释：单字不收录。

### 重复

解释：第一条。

### 重复

别名：重复
解释：第二条。
`;

test("样例词在导语句中标出，两侧中文不挡英文词", () => {
  const book = parseGlossary(SOURCE);
  const pieces = annotateTerms(
    "尝试使用FlexSim搭建某工厂车间的装配线，并产生KPI报表。",
    book,
  );
  assert.deepEqual(
    pieces.map((piece) => (piece.kind === "term" ? piece.text : piece.text)),
    ["尝试使用", "FlexSim", "搭建某工厂车间的装配线，并产生KPI报表。"],
  );
  const term = pieces[1];
  assert.equal(term?.kind, "term");
  if (term?.kind === "term") {
    assert.equal(tipParts(term.entry, "zh-CN")[0]?.kind, "text");
    const link = tipParts(term.entry, "zh-CN").find((part) => part.kind === "link");
    assert.equal(link?.kind, "link");
    if (link?.kind === "link") {
      assert.equal(link.href, "https://www.flexsim.com/");
    }
    assert.equal(tipParts(term.entry, "en")[0]?.text, "Plant simulation software.");
  }
  const unity = book.keys.find((key) => key.text === "Unity");
  assert.ok(unity);
  assert.equal(tipParts(unity.entry, "en")[0]?.text, "游戏引擎。");
});

test("最长词优先，纯英文不命中单词内部，单字与重复词丢弃", () => {
  const book = parseGlossary(SOURCE);
  const rendered = annotateTerms("实时渲染与UnityEngine里的Unity。", book)
    .filter((piece) => piece.kind === "term")
    .map((piece) => piece.text);
  assert.deepEqual(rendered, ["实时渲染", "Unity"]);
  assert.equal(book.keys.some((key) => key.text === "A"), false);
  assert.equal(book.keys.filter((key) => key.text === "重复").length, 1);
  const first = book.keys.find((key) => key.text === "重复");
  assert.equal(first?.entry.zh[0]?.text, "第一条。");
});

test("别名按原文大小写命中，技能链接里的同形词不在本函数处理", () => {
  const book = parseGlossary(SOURCE);
  const pieces = annotateTerms("使用Flexsim和自研平台做产线仿真。", book);
  assert.equal(pieces.some((piece) => piece.kind === "term" && piece.text === "Flexsim"), true);
});

test("技能名按整词对名词表，对不上则没有条目", () => {
  const book = parseGlossary(SOURCE);
  assert.equal(glossaryEntryFor(book, ["FlexSim"])?.term, "FlexSim");
  assert.equal(glossaryEntryFor(book, ["PixelCake", "Flexsim"])?.term, "FlexSim");
  assert.equal(glossaryEntryFor(book, ["不存在", "A"]), undefined);
});

test("二级标题只分组，各组词条都收录，分组名不匹配", () => {
  const book = parseGlossary(`
## 引擎

分组说明不进页面。

### Unity

解释：引擎。

## 仿真

### FlexSim

解释：仿真。

## 引擎

### 重复

解释：后写的。
`);
  const terms = annotateTerms("Unity、FlexSim、引擎、仿真。", book)
    .filter((piece) => piece.kind === "term")
    .map((piece) => piece.text);
  assert.deepEqual(terms, ["Unity", "FlexSim"]);
  const again = parseGlossary(`
## 甲

### 重复

解释：先写的。

## 乙

### 重复

解释：后写的。
`);
  const first = again.keys.find((key) => key.text === "重复");
  assert.equal(first?.entry.zh[0]?.text, "先写的。");
});

test("没有收录词时原文成一段", () => {
  const book = parseGlossary(SOURCE);
  const pieces = annotateTerms("现场抓拍，不是摆拍。", book);
  assert.equal(pieces.length, 1);
  assert.equal(pieces[0]?.kind, "text");
  assert.equal(pieces[0]?.text, "现场抓拍，不是摆拍。");
});
