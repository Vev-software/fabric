// Copies the authoritative JSON Schemas into the package before it is packed,
// so the published npm tarball ships `schemas/` (declared in package.json
// "files") the same way the NuGet package bundles them. npm runs this on
// `npm pack` and `npm publish`. The copied directory is git-ignored; it is a
// build artifact, and `schemas/v1` under the repo root stays the source of truth.
import { cpSync, rmSync, existsSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));
const source = resolve(here, "../../../schemas");
const dest = resolve(here, "../schemas");

if (!existsSync(source)) {
  console.error(`prepack: schemas source not found at ${source}`);
  process.exit(1);
}

rmSync(dest, { recursive: true, force: true });
cpSync(source, dest, { recursive: true });
// Log to stderr so `npm pack --dry-run --json` stdout stays valid JSON.
console.error(`prepack: copied ${source} -> ${dest}`);
