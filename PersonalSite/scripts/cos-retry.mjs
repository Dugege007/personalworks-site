/**
 * COS 瞬态网络错误判定与重试。
 * SDK 对流式 putObject（文件 ≤ SliceSize）不重试，应用层需自行补上。
 */

const TRANSIENT_COS_CODES = new Set([
  "ECONNRESET",
  "ETIMEDOUT",
  "EPIPE",
  "ECONNREFUSED",
  "ENOTFOUND",
  "EAI_AGAIN",
  "ESOCKETTIMEDOUT",
  "RequestTimeout",
  "NetworkingError",
  "SDK.NetworkingError",
]);

/**
 * @param {unknown} error
 */
export function isTransientCosError(error) {
  if (!error || typeof error !== "object") return false;
  const record = /** @type {Record<string, unknown>} */ (error);
  const nested = record.error;
  const nestedRecord =
    nested && typeof nested === "object"
      ? /** @type {Record<string, unknown>} */ (nested)
      : null;
  const code = String(record.code ?? record.name ?? "");
  const nestedCode = nestedRecord
    ? String(nestedRecord.code ?? nestedRecord.Code ?? "")
    : "";
  if (TRANSIENT_COS_CODES.has(code) || TRANSIENT_COS_CODES.has(nestedCode)) {
    return true;
  }
  const message = String(record.message ?? "");
  const nestedMessage =
    typeof nested === "string"
      ? nested
      : String(nestedRecord?.message ?? nestedRecord?.Message ?? "");
  return /ECONNRESET|ETIMEDOUT|EPIPE|socket hang up|NetworkingError/i.test(
    `${message} ${nestedMessage}`,
  );
}

/**
 * @template T
 * @param {(attempt: number) => Promise<T>} task
 * @param {{
 *   attempts?: number,
 *   delayMs?: number,
 *   sleep?: (ms: number) => Promise<void>,
 *   onRetry?: (error: unknown, attempt: number) => void,
 * }} [options]
 */
export async function withTransientRetry(task, options = {}) {
  const attempts = options.attempts ?? 4;
  const delayMs = options.delayMs ?? 800;
  const sleep =
    options.sleep ?? ((ms) => new Promise((resolve) => setTimeout(resolve, ms)));
  let lastError;
  for (let attempt = 1; attempt <= attempts; attempt++) {
    try {
      return await task(attempt);
    } catch (error) {
      lastError = error;
      if (!isTransientCosError(error) || attempt === attempts) {
        throw error;
      }
      options.onRetry?.(error, attempt);
      await sleep(delayMs * attempt);
    }
  }
  throw lastError;
}
