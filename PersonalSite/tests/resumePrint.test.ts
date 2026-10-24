import assert from "node:assert/strict";
import test from "node:test";
import {
  compressJobSummary,
  compressMopaiSummary,
  compressStudySummary,
  digitalTwinClientNames,
  isOmittedPrintJob,
  printBlogHref,
  printContactLines,
  selectPrintSkillItems,
  shortClientTitle,
  toResumePrintHref,
} from "../src/lib/resumePrint.ts";

const MOPAI = `使用 Unity 开发数字孪生3D大屏；自研开发产线仿真平台。
负责阿克苏诺贝尔漆油、潍柴火炬等多个项目 [查看项目简介](/work-index/twin-sim/digital-twin)；ProdLineSim 产线仿真项目 [查看功能简介]。`;

const STUDY = `学习并做各类游戏 Demo，如地牢探险、跑酷、类幸存者和联机斗地主等多款游戏Demo。
参与游戏开发比赛，探索者号获 SikiGameJamS2 三等奖，反重力获 NewbiesJam 通关奖 [查看作品集](/game-dev)。`;

const LINE_SIM = "https://duhongbo.com/work-index/twin-sim/line-sim";

test("relative and http site links become absolute https", () => {
  assert.equal(
    toResumePrintHref("/work-index/twin-sim/digital-twin"),
    "https://duhongbo.com/work-index/twin-sim/digital-twin",
  );
  assert.equal(
    toResumePrintHref("http://duhongbo.com/profile-resume"),
    "https://duhongbo.com/profile-resume",
  );
  assert.equal(toResumePrintHref("https://blog.csdn.net/Dugege007"), "https://blog.csdn.net/Dugege007");
  assert.equal(toResumePrintHref("mailto:351080175@qq.com"), "mailto:351080175@qq.com");
  assert.equal(toResumePrintHref("//duhongbo.com/profile-resume"), "//duhongbo.com/profile-resume");
});

test("blog href falls back to the public CSDN address", () => {
  assert.equal(printBlogHref(undefined), "https://blog.csdn.net/Dugege007");
  assert.equal(printBlogHref("https://blog.csdn.net/Dugege007"), "https://blog.csdn.net/Dugege007");
});

test("contacts keep full email text and skip QR-only channels", () => {
  const lines = printContactLines([
    { id: "profileBase", value: "上海市 青浦区" },
    { id: "profileMail", value: "351080175@qq.com" },
    { id: "profilePhone", value: "" },
    { id: "profileWechat", qrSrc: "home-page/wechat-qr.webp" } as { id: string; value?: string },
    { id: "profileGithub", value: "github.com/Dugege007", href: "https://github.com/Dugege007" },
  ]);
  assert.deepEqual(
    lines.map((item) => item.text),
    ["上海市 青浦区", "351080175@qq.com", "github.com/Dugege007"],
  );
  assert.equal(lines.find((item) => item.id === "profileMail")?.href, "mailto:351080175@qq.com");
  assert.equal(lines.some((item) => item.id === "profilePhone"), false);
});

test("phone is printed in full only when the site contact value exists", () => {
  const lines = printContactLines([{ id: "profilePhone", value: "13800000000" }]);
  assert.equal(lines[0]?.text, "13800000000");
  assert.equal(lines[0]?.href, "tel:13800000000");
});

test("public twin clients use short titles and skip missing works", () => {
  assert.equal(shortClientTitle("阿克苏诺贝尔漆油·上海松江"), "阿克苏诺贝尔漆油");
  const names = digitalTwinClientNames([
    { id: "akzonobel-shanghai-songjiang", title: "阿克苏诺贝尔漆油·上海松江", channel: "digital-twin" },
    { id: "ningbo-siweier", title: "宁波四维尔", channel: "digital-twin" },
    { id: "weichai-spark-plug", title: "潍柴火炬", channel: "landscape-arch" },
    { id: "shanghai-mansheng-packaging", title: "上海曼盛包装", channel: "digital-twin" },
  ]);
  assert.deepEqual(names, ["阿克苏诺贝尔漆油", "宁波四维尔", "上海曼盛包装"]);
});

test("mopai print copy names public clients and links ProdLineSim to the site", () => {
  const text = compressMopaiSummary(MOPAI, LINE_SIM, ["阿克苏诺贝尔漆油", "潍柴火炬", "宁波四维尔", "上海曼盛包装"]);
  assert.match(text, /自研产线仿真平台 ProdLineSim/);
  assert.match(text, /详见网站\]\(https:\/\/duhongbo\.com\/work-index\/twin-sim\/line-sim\)/);
  assert.match(text, /宁波四维尔、上海曼盛包装等多个项目/);
  assert.match(text, /\/work-index\/twin-sim\/digital-twin/);
  assert.equal(text.includes("[查看功能简介]"), false);
  assert.equal(text.includes("github.com"), false);
});

test("study experience keeps the award line only", () => {
  const text = compressStudySummary(STUDY);
  assert.match(text, /探索者号/);
  assert.equal(text.includes("地牢探险"), false);
});

test("internship is omitted and landscape body stays", () => {
  assert.equal(isOmittedPrintJob({ role: "景观设计实习", org: "**洛阳市古建园林设计院**" }), true);
  const landscape = "负责施工图绘制，优化制图规范。\n负责武汉大华、苏州星河大区项目。";
  assert.equal(
    compressJobSummary(
      { id: "exp-3", role: "景观设计师", org: "**上海日清景观设计有限公司**", summary: landscape, span: "2020.09-2022.10" },
      LINE_SIM,
      [],
    ),
    landscape,
  );
});

test("print skills keep the landscape drafting line and drop secondary tools", () => {
  const unity = "能熟练使用 Unity 与 C# 完成工厂、车间等三维场景的搭建、交互与业务开发。";
  const drafting = "精通 AutoCAD、SketchUp、Lumion。能熟练绘制景观施工图。";
  const items = selectPrintSkillItems([
    unity,
    drafting,
    "能熟练使用 Photoshop 完成效果图后期处理。",
    "已使用 AI 开发多项实用工具，本[网站](http://duhongbo.com/profile-resume)也是通过 Vibe Coding 开发。",
  ]);
  assert.deepEqual(items, [unity, drafting]);
});
