import { test } from "node:test";
import assert from "node:assert/strict";
import { execFileSync, spawnSync } from "node:child_process";
import { mkdtempSync, mkdirSync, writeFileSync, readFileSync } from "node:fs";
import { tmpdir } from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { checkStructural, checkDenyList, parseDenyList, extractNamespaceIds, readPullRequest, run } from "../../scripts/check-public-naming.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(here, "..", "..");
const opts = { productAllow: ["weekli", "product-a", "product-b"], namespaceAllow: ["weekli", "atlas", "portic", "fabric"] };
const f = (p, text) => [{ path: p, text }];
const rules = (files) => checkStructural(files, opts).map((x) => x.rule);

test("allow-listed productId passes, others fail with file:line", () => {
  assert.deepEqual(checkStructural(f("docs/a.md", '{ "productId": "product-a" }'), opts), []);
  assert.deepEqual(checkStructural(f("docs/a.md", 'productId = "Weekli"'), opts), []);
  const found = checkStructural(f("x.json", '{\n "productId": "secret-product-x"\n}'), opts);
  assert.equal(found.length, 1);
  assert.equal(found[0].line, 2);
});

test("unknown capability namespace fails in .cs, .ts and json", () => {
  assert.equal(extractNamespaceIds("a.cs", 'new CapabilityId("atlas.history.view")').length, 1);
  assert.deepEqual(checkStructural(f("a.cs", 'new LimitKey("atlas.entities")'), opts), []);
  assert.equal(checkStructural(f("a.cs", 'new CapabilityId("secretns.feature.on")'), opts).length, 1);
  assert.equal(checkStructural(f("sdk/typescript/src/index.ts", '  feat: "secretns.feature.on",'), opts).length, 1);
  assert.deepEqual(checkStructural(f("sdk/typescript/src/index.ts", '  serverCreated: "eu.vev.atlas.server.created.v1",'), opts), []);
  assert.equal(checkStructural(f("c.json", '{"capabilities":[{"id":"secretns.feature.on"}]}'), opts).length, 1);
  assert.deepEqual(checkStructural(f("c.json", '{"capabilities":[{"id":"weekli.ai.text"}]}'), opts), []);
});

test("commercial leakage is detected", () => {
  for (const text of ["The price is 99 DKK", "costs 9.99 USD per month", "only $5", "pris 49 kr", "12/month", "20% off", "discount code: X", "use this coupon", "€ 10"]) {
    assert.ok(rules(f("docs/a.md", text)).length > 0, text);
  }
});

test("existing neutral wording does not trip the guard", () => {
  for (const text of [
    "No default allowance, price, commercial plan mapping or automatic grant is introduced.",
    "prices, plans, discounts or payment concepts (Fabric owns entitlements, not billing)",
    "no price, plan or tier meaning",
    '"price": 10',
    "Dispatched text provider attempts per UTC calendar month",
    "Fabric is not a product. It is not sold, has no price"
  ]) {
    assert.deepEqual(rules(f("docs/a.md", text)), [], text);
  }
});

test("deliberately bad fixture is exempt from layer A but caught by layer B", () => {
  const text = readFileSync(path.join(here, "fixtures", "bad-sample.json"), "utf8");
  assert.deepEqual(checkStructural(f("tests/public-naming/fixtures/bad-sample.json", text), opts), []);
  // the same content elsewhere fails on all three structural rules
  const outside = rules(f("conformance/samples/bad.json", text));
  assert.ok(outside.some((r) => r.startsWith("productId")));
  assert.ok(outside.some((r) => r.startsWith("id namespace")));
  assert.ok(outside.some((r) => r.startsWith("commercial")));
  const found = checkDenyList(f("tests/public-naming/fixtures/bad-sample.json", text), parseDenyList("Secret-Product-X"), null);
  assert.ok(found.length >= 2);
});

test("deny-list parsing and redaction", () => {
  assert.deepEqual(parseDenyList(" A, ,b ,"), ["a", "b"]);
  assert.deepEqual(parseDenyList(""), []);
  assert.deepEqual(parseDenyList(undefined), []);
  const found = checkDenyList(f("a.md", "ok\nsee Secret-Product-X here"), ["secret-product-x"], { title: "t", body: "x\nSECRET-PRODUCT-X" });
  assert.deepEqual(found.map((x) => [x.file, x.line, x.col]), [["a.md", 2, 5], ["pull request body", 2, 1]]);
  assert.ok(found.every((x) => !JSON.stringify(x).toLowerCase().includes("secret-product-x")));
});

test("PR title and body are read from the event payload file", () => {
  const dir = mkdtempSync(path.join(tmpdir(), "naming-"));
  const p = path.join(dir, "event.json");
  writeFileSync(p, JSON.stringify({ pull_request: { title: "T", body: null } }));
  assert.deepEqual(readPullRequest({ GITHUB_EVENT_NAME: "pull_request", GITHUB_EVENT_PATH: p }), { title: "T", body: "" });
  assert.equal(readPullRequest({ GITHUB_EVENT_NAME: "push", GITHUB_EVENT_PATH: p }), null);
});

function tempRepo(files) {
  const dir = mkdtempSync(path.join(tmpdir(), "naming-repo-"));
  const git = (...a) => execFileSync("git", a, { cwd: dir, stdio: "pipe" });
  git("init", "-q");
  mkdirSync(path.join(dir, ".github"));
  writeFileSync(path.join(dir, ".github/public-naming-allowlist.txt"), "product-a\n");
  writeFileSync(path.join(dir, ".github/public-naming-namespaces.txt"), "atlas\n");
  for (const [name, text] of Object.entries(files)) writeFileSync(path.join(dir, name), text);
  git("add", "-A");
  return dir;
}

test("run: deny-list skipped when unset, enforced when set, PR text covered", () => {
  const dir = tempRepo({ "a.md": "mentions secret-product-x\n" });
  const out = [];
  assert.equal(run(dir, {}, (l) => out.push(l)), 0);
  assert.ok(out.includes("deny-list not configured, skipped"));

  const out2 = [];
  assert.equal(run(dir, { PUBLIC_NAMING_DENYLIST: "Secret-Product-X" }, (l) => out2.push(l)), 1);
  assert.ok(out2.some((l) => l.startsWith("a.md:1:10:")));
  assert.ok(!out2.join("\n").toLowerCase().includes("secret-product-x"));

  const clean = tempRepo({ "a.md": "nothing here\n" });
  const ev = path.join(clean, "..", "ev-" + path.basename(clean) + ".json");
  writeFileSync(ev, JSON.stringify({ pull_request: { title: "ok", body: "about secret-product-x" } }));
  const env = { PUBLIC_NAMING_DENYLIST: "secret-product-x", GITHUB_EVENT_NAME: "pull_request", GITHUB_EVENT_PATH: ev };
  const out3 = [];
  assert.equal(run(clean, env, (l) => out3.push(l)), 1);
  assert.ok(out3.some((l) => l.startsWith("pull request body:1:")));
});

test("guard passes on the current repository without a deny-list", () => {
  const r = spawnSync(process.execPath, [path.join(repoRoot, "scripts", "check-public-naming.mjs")], { cwd: repoRoot, env: { ...process.env, PUBLIC_NAMING_DENYLIST: "" }, encoding: "utf8" });
  assert.equal(r.status, 0, r.stdout + r.stderr);
  assert.match(r.stdout, /deny-list not configured, skipped/);
});
