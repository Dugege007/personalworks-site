import assert from "node:assert/strict";
import test from "node:test";
import { isTransientFsLock, retryTransientFsLock } from "./fs-lock-retry.mjs";

test("占用类错误会重试，其它错误不会", async () => {
  assert.equal(isTransientFsLock({ code: "EPERM" }), true);
  assert.equal(isTransientFsLock({ code: "EACCES" }), true);
  assert.equal(isTransientFsLock({ code: "EBUSY" }), true);
  assert.equal(isTransientFsLock({ code: "ENOENT" }), false);

  let runs = 0;
  const value = await retryTransientFsLock(() => {
    runs += 1;
    if (runs < 3) {
      const error = new Error("busy");
      error.code = "EPERM";
      throw error;
    }
    return "ok";
  }, { sleep: async () => {} });
  assert.equal(value, "ok");
  assert.equal(runs, 3);

  await assert.rejects(
    () => retryTransientFsLock(() => {
      const error = new Error("missing");
      error.code = "ENOENT";
      throw error;
    }, { sleep: async () => {} }),
    (error) => error && error.code === "ENOENT",
  );
});
