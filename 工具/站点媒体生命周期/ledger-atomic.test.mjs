import assert from "node:assert/strict";
import test from "node:test";
import {
  isTransientFsLock,
  ledgerRenameBackoffMs,
  replaceLedgerAtomically,
} from "./ledger-atomic.mjs";

test("占用类错误才重试，其它错误立刻抛出", () => {
  assert.equal(isTransientFsLock({ code: "EPERM" }), true);
  assert.equal(isTransientFsLock({ code: "EACCES" }), true);
  assert.equal(isTransientFsLock({ code: "EBUSY" }), true);
  assert.equal(isTransientFsLock({ code: "ENOENT" }), false);
});

test("改名被占用时重试成功，返回重试次数且不抛错", async () => {
  let calls = 0;
  const waits = [];
  const notified = [];
  const retries = await replaceLedgerAtomically("a.tmp", "a.json", {
    maxAttempts: 4,
    rename: async () => {
      calls += 1;
      if (calls < 3) {
        const error = new Error("occupied");
        error.code = "EPERM";
        throw error;
      }
    },
    sleep: async (ms) => {
      waits.push(ms);
    },
    onRetry: (count) => {
      notified.push(count);
    },
  });
  assert.equal(retries, 2);
  assert.equal(calls, 3);
  assert.deepEqual(waits, [ledgerRenameBackoffMs(1), ledgerRenameBackoffMs(2)]);
  assert.deepEqual(notified, [1, 2]);
});

test("重试用尽仍占用则抛出", async () => {
  await assert.rejects(
    () =>
      replaceLedgerAtomically("a.tmp", "a.json", {
        maxAttempts: 2,
        rename: async () => {
          const error = new Error("still locked");
          error.code = "EPERM";
          throw error;
        },
        sleep: async () => {},
      }),
    (error) => error && error.code === "EPERM",
  );
});
