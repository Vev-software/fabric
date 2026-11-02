#!/usr/bin/env node
// Public naming guard (see docs/public-naming-rule.md).
//
// Layer A - structural checks that need no secret list:
//   * every productId value must be in .github/public-naming-allowlist.txt
//   * capability / limit id namespaces must be in .github/public-naming-namespaces.txt
//   * no commercial leakage (currency amounts, per-month pricing, coupons, discount codes)
// Layer B - deny-list taken only from the PUBLIC_NAMING_DENYLIST environment variable
//   (comma-separated, case-insensitive). Empty => skipped. The list is never stored in the repo
//   and matched words are never printed.

import { execFileSync } from "node:child_process";
import { readFileSync, existsSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

// The guard's own tests and deliberately bad fixtures live here and are excluded from layer A.
export const EXEMPT_PREFIX = "tests/public-naming/";

// This file spells out the leakage patterns it looks for.
const SELF = "scripts/check-public-naming.mjs";

const BINARY_EXT = new Set([".png", ".jpg", ".jpeg", ".gif", ".ico", ".snk", ".nupkg", ".zip", ".gz", ".dll", ".pdf"]);

export function parseList(text) {
  return text
    .split(/\r?\n/)
    .map((l) => l.replace(/#.*/, "").trim().toLowerCase())
    .filter(Boolean);
}

// ---- layer A ---------------------------------------------------------------

const PRODUCT_ID_RE = /["']?productId["']?\s*[:=]\s*["']([^"']*)["']/gi;
const ID_RE = /^[a-z][a-z0-9-]*(\.[a-z0-9-]+)+$/;
const CS_ID_RE = /new\s+(?:CapabilityId|LimitKey)\(\s*"([^"]+)"\s*\)/g;
const TS_ID_RE = /^\s*[A-Za-z_$][\w$]*\s*:\s*"([a-z][a-z0-9-]*(?:\.[a-z0-9-]+)+)"/;
const JSON_ID_KEYS = new Set(["capabilityId", "limitKey"]);

const COMMERCIAL = [
  ["currency amount", /(?:[$€£]\s?\d|\b\d[\d.,]*\s?(?:DKK|EUR|USD|GBP|SEK|NOK|kr\b|[€$£]))/i],
  ["per-month pricing", /\b\d[\d.,]*\s*(?:\/|per\s+)(?:month|mo|måned|md)\b/i],
  ["coupon / voucher", /\b(?:coupon|voucher|promo(?:tion)?\s+code)\b/i],
  ["discount offer", /\b\d+\s*%\s*(?:off|discount|rabat)\b|\b(?:discount|rabat)\s*(?:of|code|:|=)\s*\d|\b(?:discount|rabat)\s+code\b/i]
];

function lineOf(text, index) {
  let n = 1;
  for (let i = 0; i < index; i++) if (text.charCodeAt(i) === 10) n++;
  return n;
}

function* walkJson(node, key, parentKey) {
  if (Array.isArray(node)) {
    for (const item of node) yield* walkJson(item, key, key);
  } else if (node && typeof node === "object") {
    for (const [k, v] of Object.entries(node)) yield* walkJson(v, k, key);
  } else if (typeof node === "string") {
    yield { key, parentKey, value: node };
  }
}

export function extractNamespaceIds(file, text) {
  const ids = [];
  const add = (id, line) => ids.push({ id, line });
  if (file.endsWith(".cs")) {
    for (const m of text.matchAll(CS_ID_RE)) add(m[1], lineOf(text, m.index));
  } else if (file.startsWith("sdk/typescript/src/") && file.endsWith(".ts")) {
    text.split("\n").forEach((l, i) => {
      const m = TS_ID_RE.exec(l);
      // reverse-DNS event types are a separate id space
      if (m && !m[1].startsWith("eu.vev.")) add(m[1], i + 1);
    });
  } else if (file.endsWith(".json")) {
    let doc;
    try { doc = JSON.parse(text); } catch { return ids; }
    for (const { key, parentKey, value } of walkJson(doc)) {
      if ((JSON_ID_KEYS.has(key) || (key === "id" && parentKey === "capabilities")) && ID_RE.test(value)) {
        const idx = text.indexOf(`"${value}"`);
        add(value, idx < 0 ? 1 : lineOf(text, idx));
      }
    }
  }
  return ids;
}

export function checkStructural(files, { productAllow, namespaceAllow }) {
  const findings = [];
  for (const { path: file, text } of files) {
    if (file.startsWith(EXEMPT_PREFIX) || file === SELF) continue;
    for (const m of text.matchAll(PRODUCT_ID_RE)) {
      if (!productAllow.includes(m[1].toLowerCase())) {
        findings.push({ file, line: lineOf(text, m.index), rule: "productId not in .github/public-naming-allowlist.txt", match: m[1] });
      }
    }
    for (const { id, line } of extractNamespaceIds(file, text)) {
      const ns = id.split(".")[0].toLowerCase();
      if (!namespaceAllow.includes(ns)) {
        findings.push({ file, line, rule: "id namespace not in .github/public-naming-namespaces.txt", match: id });
      }
    }
    if (/\.(md|json|cs|ts|mjs|yml|yaml|txt|csproj|props)$/i.test(file) && !file.endsWith("package-lock.json")) {
      text.split("\n").forEach((l, i) => {
        for (const [rule, re] of COMMERCIAL) {
          const m = re.exec(l);
          if (m) findings.push({ file, line: i + 1, rule: `commercial leakage: ${rule}`, match: m[0].trim() });
        }
      });
    }
  }
  return findings;
}

// ---- layer B ---------------------------------------------------------------

export function parseDenyList(raw) {
  return (raw ?? "").split(",").map((s) => s.trim().toLowerCase()).filter(Boolean);
}

function scanText(label, text, deny, findings) {
  text.split("\n").forEach((l, i) => {
    const lower = l.toLowerCase();
    for (const word of deny) {
      const col = lower.indexOf(word);
      if (col >= 0) findings.push({ file: label, line: i + 1, col: col + 1, length: word.length });
    }
  });
}

export function checkDenyList(files, deny, pr) {
  const findings = [];
  for (const { path: file, text } of files) {
    scanText(file, text, deny, findings);
    scanText(`${file} (file name)`, file, deny, findings);
  }
  if (pr) {
    scanText("pull request title", pr.title ?? "", deny, findings);
    scanText("pull request body", pr.body ?? "", deny, findings);
  }
  return findings;
}

// The PR title/body come from the event payload file, never from shell interpolation.
export function readPullRequest(env) {
  if (env.GITHUB_EVENT_NAME !== "pull_request" || !env.GITHUB_EVENT_PATH || !existsSync(env.GITHUB_EVENT_PATH)) return null;
  const pr = JSON.parse(readFileSync(env.GITHUB_EVENT_PATH, "utf8")).pull_request;
  return pr ? { title: pr.title ?? "", body: pr.body ?? "" } : null;
}

// ---- main ------------------------------------------------------------------

export function loadTrackedFiles(root) {
  const out = execFileSync("git", ["ls-files", "-z"], { cwd: root, maxBuffer: 1 << 28 }).toString("utf8");
  const files = [];
  for (const rel of out.split("\0").filter(Boolean)) {
    if (BINARY_EXT.has(path.extname(rel).toLowerCase())) continue;
    const abs = path.join(root, rel);
    if (!existsSync(abs)) continue;
    const buf = readFileSync(abs);
    if (buf.includes(0)) continue;
    files.push({ path: rel, text: buf.toString("utf8") });
  }
  return files;
}

export function run(root, env, log = console.log) {
  const files = loadTrackedFiles(root);
  const productAllow = parseList(readFileSync(path.join(root, ".github/public-naming-allowlist.txt"), "utf8"));
  const namespaceAllow = parseList(readFileSync(path.join(root, ".github/public-naming-namespaces.txt"), "utf8"));
  let failed = false;

  const structural = checkStructural(files, { productAllow, namespaceAllow });
  for (const f of structural) log(`${f.file}:${f.line}: ${f.rule}: ${f.match}`);
  log(structural.length ? `structural checks: ${structural.length} finding(s)` : "structural checks: ok");
  failed ||= structural.length > 0;

  const deny = parseDenyList(env.PUBLIC_NAMING_DENYLIST);
  if (deny.length === 0) {
    log("deny-list not configured, skipped");
  } else {
    const found = checkDenyList(files, deny, readPullRequest(env));
    for (const f of found) log(`${f.file}:${f.line}:${f.col}: deny-list match [redacted, ${f.length} chars]`);
    log(found.length ? `deny-list check: ${found.length} finding(s)` : "deny-list check: ok");
    failed ||= found.length > 0;
  }
  return failed ? 1 : 0;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
  process.exit(run(root, process.env));
}
