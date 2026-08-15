import { readdirSync, readFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const contractsRoot = path.join(repoRoot, "src", "Vev.Fabric.Contracts");
const projectFile = path.join(contractsRoot, "Vev.Fabric.Contracts.csproj");
const sourceFiles = enumerateFiles(contractsRoot, ".cs");

const forbiddenNamespacePatterns = [
  /\busing\s+Vev\.Atlas(\.|;)/,
  /\busing\s+Atlas\./,
  /\busing\s+Vev\.Portic(\.|;)/,
  /\busing\s+Portic\./,
  /\busing\s+Vev\.Orion(\.|;)/,
  /\busing\s+Orion\./,
  /\bglobal\s+using\s+Vev\.Atlas(\.|;)/,
  /\bglobal\s+using\s+Atlas\./,
  /\bglobal\s+using\s+Vev\.Portic(\.|;)/,
  /\bglobal\s+using\s+Portic\./,
  /\bglobal\s+using\s+Vev\.Orion(\.|;)/,
  /\bglobal\s+using\s+Orion\./,
  /\bVev\.Atlas\./,
  /\bVev\.Portic\./,
  /\bVev\.Orion\./
];

const forbiddenPackagePatterns = [
  /<PackageReference\s+Include="(?:Vev\.)?Atlas/i,
  /<PackageReference\s+Include="(?:Vev\.)?Portic/i,
  /<PackageReference\s+Include="(?:Vev\.)?Orion/i,
  /<ProjectReference\s+Include=".*(?:atlas|portic|orion)/i
];

const failures = [];
const projectText = readFileSync(projectFile, "utf8");

for (const pattern of forbiddenPackagePatterns) {
  if (pattern.test(projectText)) {
    failures.push(`${path.relative(repoRoot, projectFile)} matches forbidden reference pattern ${pattern}`);
  }
}

for (const file of sourceFiles) {
  const text = readFileSync(file, "utf8");
  for (const pattern of forbiddenNamespacePatterns) {
    if (pattern.test(text)) {
      failures.push(`${path.relative(repoRoot, file)} matches forbidden namespace pattern ${pattern}`);
    }
  }
}

if (failures.length > 0) {
  console.error("Fabric contract layer must not reference product namespaces or packages.");
  for (const failure of failures) {
    console.error(`- ${failure}`);
  }
  process.exit(1);
}

console.log("Dependency-direction fitness check passed.");

function enumerateFiles(root, extension) {
  const files = [];

  for (const entry of readdirSync(root, { withFileTypes: true })) {
    const fullPath = path.join(root, entry.name);
    if (entry.isDirectory()) {
      files.push(...enumerateFiles(fullPath, extension));
    } else if (entry.isFile() && fullPath.endsWith(extension)) {
      files.push(fullPath);
    }
  }

  return files;
}
