/**
 * Windows 上文件被占用时的短暂重试。与台账改名使用同一类错误码。
 */

const MAX_ATTEMPTS = 6;
const MAX_TOTAL_WAIT_MS = 2000;

/**
 * 是否为文件被占用一类的瞬时错误。
 */
export function isTransientFsLock(error) {
  const code = error && error.code;
  return code === "EPERM" || code === "EACCES" || code === "EBUSY";
}

/**
 * 第 n 次失败后等待的毫秒。n 从 1 起。
 * 依次为 100、200、400、800、500，合计不超过 2 秒。
 */
export function lockBackoffMs(retryIndex) {
  if (retryIndex < 1) {
    return 0;
  }

  let spent = 0;
  for (let index = 1; index < retryIndex; index += 1) {
    spent += rawBackoffMs(index);
  }

  const remaining = MAX_TOTAL_WAIT_MS - spent;
  if (remaining <= 0) {
    return 0;
  }

  return Math.min(rawBackoffMs(retryIndex), remaining);
}

function rawBackoffMs(retryIndex) {
  return 100 * 2 ** (retryIndex - 1);
}

/**
 * 执行动作。遇到瞬时占用就等待后再试，其它错误立刻抛出。
 */
export async function retryTransientFsLock(action, options = {}) {
  const maxAttempts = options.maxAttempts ?? MAX_ATTEMPTS;
  const sleep = options.sleep ?? ((ms) => new Promise((resolve) => {
    setTimeout(resolve, ms);
  }));
  let lastError;
  for (let attempt = 1; attempt <= maxAttempts; attempt += 1) {
    try {
      return await action();
    } catch (error) {
      lastError = error;
      if (!isTransientFsLock(error) || attempt === maxAttempts) {
        throw error;
      }
      await sleep(lockBackoffMs(attempt));
    }
  }
  throw lastError;
}
