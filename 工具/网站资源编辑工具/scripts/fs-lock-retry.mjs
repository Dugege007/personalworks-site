/**
 * Windows 上文件被占用时的短暂重试。与台账改名使用同一类错误码。
 */

const MAX_ATTEMPTS = 8;

/**
 * 是否为文件被占用一类的瞬时错误。
 */
export function isTransientFsLock(error) {
  const code = error && error.code;
  return code === "EPERM" || code === "EACCES" || code === "EBUSY";
}

/**
 * 第 n 次失败后等待的毫秒。n 从 1 起。
 */
export function lockBackoffMs(retryIndex) {
  return Math.min(2000, 100 * 2 ** (retryIndex - 1));
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
