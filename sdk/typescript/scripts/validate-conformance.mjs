import { readFileSync, readdirSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import Ajv2020 from "ajv/dist/2020.js";
import addFormats from "ajv-formats";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const schemaDir = path.join(repoRoot, "schemas", "v1");
const sampleDir = path.join(repoRoot, "conformance", "samples");

const sampleSchemaMap = new Map([
  ["audit-event.sample.json", "audit-event.schema.json"],
  ["authorization-decision.sample.json", "authorization-decision.schema.json"],
  ["authorization-request.sample.json", "authorization-request.schema.json"],
  ["discovery-enrollment-status.sample.json", "discovery-enrollment-status.schema.json"],
  ["discovery-enrollment-transition-request.sample.json", "discovery-enrollment-transition-request.schema.json"],
  ["discovery-enrollment-transition-result.sample.json", "discovery-enrollment-transition-result.schema.json"],
  ["discovery-ingestion-access-request.sample.json", "discovery-ingestion-access-request.schema.json"],
  ["discovery-ingestion-access-decision.sample.json", "discovery-ingestion-access-decision.schema.json"],
  ["discovery-lifecycle-event.sample.json", "discovery-lifecycle-event.schema.json"],
  ["entitlement-bundle-request.sample.json", "entitlement-bundle-request.schema.json"],
  ["evaluate-entitlements-request.sample.json", "evaluate-entitlements-request.schema.json"],
  ["import-signed-entitlement-snapshot-request.sample.json", "import-signed-entitlement-snapshot-request.schema.json"],
  ["signed-entitlement-snapshot.sample.json", "signed-entitlement-snapshot.schema.json"],
  ["tenant-lifecycle-query.sample.json", "tenant-lifecycle-query.schema.json"],
  ["tenant-lifecycle-status.sample.json", "tenant-lifecycle-status.schema.json"],
  ["tenant-lifecycle-transition-request.sample.json", "tenant-lifecycle-transition-request.schema.json"],
  ["tenant-lifecycle-transition-result.sample.json", "tenant-lifecycle-transition-result.schema.json"],
  ["taxonomy-catalog.sample.json", "taxonomy-catalog.schema.json"]
]);

const ajv = new Ajv2020({ allErrors: true, strict: false });
addFormats(ajv);

for (const file of readdirSync(schemaDir)) {
  if (!file.endsWith(".json")) {
    continue;
  }

  const schema = JSON.parse(readFileSync(path.join(schemaDir, file), "utf8"));
  ajv.addSchema(schema, schema.$id ?? file);
}

let failures = 0;

for (const [sampleFile, schemaFile] of sampleSchemaMap) {
  const schema = ajv.getSchema(`https://schemas.vev.software/fabric/v1/${schemaFile}`) ?? ajv.getSchema(schemaFile);
  if (!schema) {
    console.error(`Missing schema registration for ${schemaFile}`);
    failures++;
    continue;
  }

  const sample = JSON.parse(readFileSync(path.join(sampleDir, sampleFile), "utf8"));
  const valid = schema(sample);

  if (!valid) {
    failures++;
    console.error(`Schema validation failed for ${sampleFile} against ${schemaFile}`);
    for (const error of schema.errors ?? []) {
      console.error(`  ${error.instancePath || "/"} ${error.message}`);
    }
  }
}

if (failures > 0) {
  process.exit(1);
}

console.log(`Validated ${sampleSchemaMap.size} conformance samples against published schemas.`);
