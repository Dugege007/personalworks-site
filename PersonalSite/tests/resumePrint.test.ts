import assert from "node:assert/strict";
import test from "node:test";
import {
  PRINT_FIT_LEVELS,
  PRINT_HOME_HREF,
  PRINT_PHONE_TEXT,
  isPrintInternship,
  isPrintSecondaryJob,
  printContacts,
  printIntroLines,
  printJobSummary,
  printSheetLimitMm,
  resumePdfFilename,
  resumePdfSaveTitle,
  toResumePrintHref,
  type PrintJobInput,
} from "../src/lib/resumePrint.ts";

const MOPAI = `使用 Unity 开发数字孪生3D大屏；自研开发产线仿真平台。
负责阿克苏诺贝尔漆油、潍柴火炬等多个项目 [查看项目简介](/work-index/twin-sim/digital-twin)；ProdLineSim 产线仿真项目 [查看项目简介](/work-index/twin-sim/line-sim/prodlinesim-rte)。`;

const STUDY = `学习并做各类游戏 Demo，如地牢探险、跑酷、类幸存者和联机斗地主等多款游戏Demo。
参与游戏开发比赛，探索者号获 SikiGameJamS2 三等奖，反重力获 NewbiesJam 通关奖 [查看作品集](/game-dev)。`;

const INTERN = `协助绘制市政景观 CAD 施工图。
参与伊水游园规划、洛阳园林局前院改造、新安县绿化提升等项目。`;

const LANDSCAPE = "负责施工图绘制，优化制图规范。\n负责武汉大华、苏州星河大区项目。";

function job(partial: Partial<PrintJobInput> & Pick<PrintJobInput, "role" | "summary">): PrintJobInput {
  return {
    id: partial.id ?? "exp",
    role: partial.role,
    org: partial.org,
    summary: partial.summary,
    span: partial.span ?? "",
  };
}

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

test("pdf filename follows the name on screen and the local date", () => {
  assert.equal(resumePdfFilename("杜红勃", new Date(2026, 9, 31)), "杜红勃简历-20261031.pdf");
  assert.equal(resumePdfFilename("杜宏博", new Date(2026, 0, 5)), "杜宏博简历-20260105.pdf");
  assert.equal(resumePdfSaveTitle("杜红勃", new Date(2026, 9, 31)), "杜红勃简历-20261031");
});

test("print sheet limit keeps page margins only in print media", () => {
  assert.equal(printSheetLimitMm(false), 297);
  assert.equal(printSheetLimitMm(true), 273);
});

test("contacts are the four text items and drop github before the homepage", () => {
  const source = {
    mail: "351080175@qq.com",
    githubText: "github.com/Dugege007",
    githubHref: "https://github.com/Dugege007",
  };
  const full = printContacts(source);
  assert.deepEqual(
    full.map((item) => `${item.label}${item.text}`),
    [
      `手机号(同微信)${PRINT_PHONE_TEXT}`,
      "邮箱351080175@qq.com",
      "现居地上海 青浦区",
      "个人主页www.duhongbo.com",
      "Githubgithub.com/Dugege007",
    ],
  );
  assert.equal(full.find((item) => item.id === "phone")?.href, undefined);
  assert.equal(full.find((item) => item.id === "mail")?.href, undefined);
  assert.equal(full.find((item) => item.id === "homepage")?.href, PRINT_HOME_HREF);
  assert.equal(full.find((item) => item.id === "github")?.href, "https://github.com/Dugege007");

  const noGithub = printContacts(source, { github: true }).map((item) => item.id);
  assert.deepEqual(noGithub, ["phone", "mail", "base", "homepage"]);
  const noSite = printContacts(source, { github: true, homepage: true }).map((item) => item.id);
  assert.deepEqual(noSite, ["phone", "mail", "base"]);
});

test("job prose stays as written until secondary compression is requested", () => {
  const mopai = job({ role: "数字孪生与产线仿真工程师", org: "**上海默派机电科技有限公司**", summary: MOPAI });
  assert.equal(printJobSummary(mopai, false), MOPAI.trim());
  assert.equal(printJobSummary(mopai, true), MOPAI.trim());
  assert.equal(isPrintSecondaryJob(mopai), false);
});

test("intro keeps the first two lines and internships are marked for omission", () => {
  const intro = "现在做数字孪生与产线仿真开发。\n此前做过四年景观设计，有丰富的深化设计经验。\n热衷于探索前沿技术，较强的学习能力。\n爱好徒步、摄影、游戏。";
  assert.equal(
    printIntroLines(intro),
    "现在做数字孪生与产线仿真开发。\n此前做过四年景观设计，有丰富的深化设计经验。",
  );
  assert.equal(isPrintInternship({ role: "景观设计实习", org: "**洛阳市古建园林设计院**" }), true);
  assert.equal(isPrintInternship({ role: "Unity 开发", org: "自学编程&独立开发" }), false);
});

test("internship and self-study compress only when asked", () => {
  const intern = job({ role: "景观设计实习", org: "**洛阳市古建园林设计院**", summary: INTERN });
  const study = job({ role: "Unity 开发", org: "自学编程&独立开发", summary: STUDY });
  const landscape = job({ role: "景观设计师", org: "**上海日清景观设计有限公司**", summary: LANDSCAPE });

  assert.equal(printJobSummary(intern, false), INTERN.trim());
  assert.equal(printJobSummary(study, false).includes("地牢探险"), true);
  assert.equal(printJobSummary(intern, true), "协助绘制市政景观 CAD 施工图。");
  assert.match(printJobSummary(study, true), /探索者号/);
  assert.equal(printJobSummary(study, true).includes("地牢探险"), false);
  assert.equal(printJobSummary(landscape, true), LANDSCAPE);
});

test("fit levels tighten spacing before dropping contacts", () => {
  assert.equal(PRINT_FIT_LEVELS[0]?.compressSecondary, false);
  assert.equal(PRINT_FIT_LEVELS[0]?.dropGithub, false);
  const firstCompress = PRINT_FIT_LEVELS.findIndex((level) => level.compressSecondary);
  const firstGithub = PRINT_FIT_LEVELS.findIndex((level) => level.dropGithub);
  const firstHome = PRINT_FIT_LEVELS.findIndex((level) => level.dropHomepage);
  assert.ok(firstCompress > 0);
  assert.ok(firstGithub > firstCompress);
  assert.ok(firstHome > firstGithub);
  assert.equal(PRINT_FIT_LEVELS[firstHome]?.dropGithub, true);
});
