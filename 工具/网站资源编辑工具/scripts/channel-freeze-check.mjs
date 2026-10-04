/**
 * 核对投放箱栏目四元组是否与冻结表一致，并检查指定 key 是否已写入 channels。
 * 不写盘。通名表仍只在正本 3.6，本脚本不复制。
 *
 *   node channel-freeze-check.mjs --lexicon <lexicon.ts> --profile <profile.json> [--expect-channel <key>]...
 */

import { readFile } from "node:fs/promises";
import path from "node:path";

function parseArgs(argv) {
  const flags = {
    lexicon: "",
    profile: "",
    expectChannelList: [],
    help: false,
  };
  const tokens = argv.slice(2);
  for (let i = 0; i < tokens.length; i += 1) {
    const token = tokens[i];
    if (token === "--help" || token === "-h") {
      flags.help = true;
    } else if (token === "--lexicon" || token === "--profile" || token === "--expect-channel") {
      const value = tokens[i + 1];
      if (!value || value.startsWith("--")) {
        throw new Error(`${token} 后需要值`);
      }
      if (token === "--lexicon") flags.lexicon = value;
      else if (token === "--profile") flags.profile = value;
      else flags.expectChannelList.push(value);
      i += 1;
    } else if (token.startsWith("--")) {
      throw new Error(`未知参数：${token}`);
    } else {
      throw new Error(`多余参数：${token}`);
    }
  }
  return flags;
}

function parseLexicon(text) {
  const dict = new Map();
  const entryRe =
    /zh:\s*"([^"]+)"[\s\S]*?en:\s*"([^"]+)"[\s\S]*?deco:\s*"([^"]+)"[\s\S]*?key:\s*"([^"]+)"/g;
  for (const match of text.matchAll(entryRe)) {
    dict.set(match[4], {
      zh: match[1],
      en: match[2],
      deco: match[3],
      key: match[4],
    });
  }
  return dict;
}

function printReport(report) {
  process.stdout.write(`${JSON.stringify(report, null, 2)}\n`);
}

async function main() {
  const flags = parseArgs(process.argv);
  if (flags.help || !flags.lexicon || !flags.profile) {
    console.log(
      "用法：node channel-freeze-check.mjs --lexicon <lexicon.ts> --profile <profile.json> [--expect-channel <key>]...",
    );
    process.exit(flags.help ? 0 : 2);
  }

  const lexiconText = await readFile(path.resolve(flags.lexicon), "utf8");
  const profileText = await readFile(path.resolve(flags.profile), "utf8");
  const lexiconDict = parseLexicon(lexiconText);
  const profile = JSON.parse(profileText);
  const channelList = Array.isArray(profile.channels) ? profile.channels : [];
  const channelKeySet = new Set(channelList.map((item) => item?.key).filter(Boolean));
  const errorList = [];

  for (const channel of channelList) {
    const key = channel?.key || "";
    if (!key) {
      errorList.push("channels 中存在空 key。");
      continue;
    }
    const frozen = lexiconDict.get(key);
    if (!frozen) {
      errorList.push(`栏目 ${key} 未写入冻结表。`);
      continue;
    }
    if (channel.zh && channel.zh !== frozen.zh) {
      errorList.push(`栏目 ${key} 的中文与冻结表不一致：${channel.zh} / ${frozen.zh}`);
    }
    if (channel.en && channel.en !== frozen.en) {
      errorList.push(`栏目 ${key} 的正式英文与冻结表不一致：${channel.en} / ${frozen.en}`);
    }
    if (channel.deco && channel.deco !== frozen.deco) {
      errorList.push(`栏目 ${key} 的装饰短写与冻结表不一致：${channel.deco} / ${frozen.deco}`);
    }
  }

  for (const key of flags.expectChannelList) {
    if (!channelKeySet.has(key)) {
      errorList.push(`漏写 channels：${key}`);
    }
  }

  const report = {
    ok: errorList.length === 0,
    errors: errorList,
    channelCount: channelList.length,
    lexiconCount: lexiconDict.size,
  };
  printReport(report);
  if (errorList.length > 0) {
    process.exitCode = 1;
  }
}

try {
  await main();
} catch (error) {
  const message = error instanceof Error ? error.message : String(error);
  printReport({ ok: false, errors: [message] });
  process.exitCode = 1;
}
