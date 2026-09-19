import assert from "node:assert/strict";
import test from "node:test";
import {
  isTransientCosError,
  withTransientRetry,
} from "../scripts/cos-retry.mjs";

test("ECONNRESET 与 socket hang up 视为瞬态", () => {
  assert.equal(
    isTransientCosError({
      code: "ECONNRESET",
      name: "ECONNRESET",
      message: "write ECONNRESET",
      method: "PUT",
    }),
    true,
  );
  assert.equal(isTransientCosError({ message: "socket hang up" }), true);
  assert.equal(
    isTransientCosError({
      code: "SDK.NetworkingError",
      error: { Code: "RequestTimeout", Message: "timeout" },
    }),
    true,
  );
});

test("鉴权与业务错误不重试", () => {
  assert.equal(
    isTransientCosError({
      code: "AccessDenied",
      statusCode: 403,
      message: "Access Denied",
    }),
    false,
  );
  assert.equal(isTransientCosError({ code: "NoSuchBucket", statusCode: 404 }), false);
  assert.equal(isTransientCosError(null), false);
});

test("瞬态错误耗尽前重试并成功", async () => {
  const sleeps = [];
  let calls = 0;
  const result = await withTransientRetry(
    async () => {
      calls += 1;
      if (calls < 3) {
        const error = new Error("write ECONNRESET");
        error.code = "ECONNRESET";
        throw error;
      }
      return "ok";
    },
    {
      attempts: 4,
      delayMs: 10,
      sleep: async (ms) => {
        sleeps.push(ms);
      },
    },
  );
  assert.equal(result, "ok");
  assert.equal(calls, 3);
  assert.deepEqual(sleeps, [10, 20]);
});

test("非瞬态错误立即抛出", async () => {
  let calls = 0;
  await assert.rejects(
    () =>
      withTransientRetry(
        async () => {
          calls += 1;
          const error = new Error("Access Denied");
          error.code = "AccessDenied";
          error.statusCode = 403;
          throw error;
        },
        { attempts: 4, sleep: async () => {} },
      ),
    { code: "AccessDenied" },
  );
  assert.equal(calls, 1);
});

test("瞬态错误耗尽后抛出最后一次", async () => {
  let calls = 0;
  await assert.rejects(
    () =>
      withTransientRetry(
        async () => {
          calls += 1;
          const error = new Error("write ECONNRESET");
          error.code = "ECONNRESET";
          throw error;
        },
        { attempts: 3, delayMs: 1, sleep: async () => {} },
      ),
    { code: "ECONNRESET" },
  );
  assert.equal(calls, 3);
});
