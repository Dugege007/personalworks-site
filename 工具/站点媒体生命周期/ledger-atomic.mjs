/**
 * 台账原子落盘：先写 .tmp 再改名覆盖。Windows 上目标被占用时重试，不立刻当失败。
 */

import { mkdir, rename, writeFile } from "node:fs/promises";
import path from "node:path";

export const LEDGER_RENAME_RETRY_PREFIX = "台账改名重试：";
export const LEDGER_RENAME_RETRYING_PREFIX = "台账改名重试中：";
const MAX_ATTEMPTS = 8;

/**
 * 是否为 Windows 上文件被占用一类的瞬时错误。
 */
export function isTransientFsLock(error) {
  const code = error && error.code;
  return code === "EPERM" || code === "EACCES" || code === "EBUSY";
}

/**
 * 第 n 次重试前等待的毫秒（n 从 1 起）。
 */
export function ledgerRenameBackoffMs(retryIndex) {
  return Math.min(2000, 100 * 2 ** (retryIndex - 1));
}

/**
 * 把已写好的草稿改名盖住正式台账；返回成功前重试了几次。
 */
export async function replaceLedgerAtomically(tmp, dest, options = {}) {
  const renameFn = options.rename ?? rename;
  const sleepFn = options.sleep ?? sleep;
  const maxAttempts = options.maxAttempts ?? MAX_ATTEMPTS;
  let lastError;
  let retries = 0;
  for (let attempt = 1; attempt <= maxAttempts; attempt++) {
    try {
      await renameFn(tmp, dest);
      return retries;
    } catch (error) {
      lastError = error;
      if (!isTransientFsLock(error) || attempt === maxAttempts) {
        throw error;
      }
      retries += 1;
      if (typeof options.onRetry === "function") {
        options.onRetry(retries);
      }
      await sleepFn(ledgerRenameBackoffMs(retries));
    }
  }
  throw lastError;
}

/**
 * 写台账并按需重试改名；返回重试次数。
 */
export async function saveLedgerFile(ledgerPath, ledger, options = {}) {
  const text = `${JSON.stringify(ledger, null, 2)}\n`;
  const tmp = `${ledgerPath}.tmp`;
  await mkdir(path.dirname(ledgerPath), { recursive: true });
  await writeFile(tmp, text, "utf8");
  return replaceLedgerAtomically(tmp, ledgerPath, options);
}

function sleep(ms) {
  return new Promise((resolve) => {
    setTimeout(resolve, ms);
  });
}
