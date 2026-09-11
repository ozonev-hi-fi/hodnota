#!/usr/bin/env node
// Enforces CLAUDE.md's "don't restate an ADR in a code comment" rule for the
// literal-citation case: a comment that names "decisions/NNNN" should not exist
// outside docs/ — link the ADR from the PR/commit instead.
const fs = require("fs");
const path = require("path");

const PROJECT_ROOT = process.cwd();

let input = "";
process.stdin.on("data", (chunk) => {
  input += chunk;
});
process.stdin.on("end", () => {
  let payload;
  try {
    payload = JSON.parse(input);
  } catch {
    process.exit(0);
  }

  const filePath = payload && payload.tool_input && payload.tool_input.file_path;
  if (!filePath) {
    process.exit(0);
  }

  const relativeToRoot = path.relative(PROJECT_ROOT, path.resolve(filePath));
  const isOutsideProject = relativeToRoot === ".." || relativeToRoot.startsWith(`..${path.sep}`) || path.isAbsolute(relativeToRoot);
  if (isOutsideProject) {
    process.exit(0);
  }

  const normalized = filePath.replace(/\\/g, "/");
  const baseName = normalized.split("/").pop();

  if (/(^|\/)docs\//.test(normalized) || baseName === "CLAUDE.md" || baseName === "CLAUDE.local.md") {
    process.exit(0);
  }

  let content;
  try {
    content = fs.readFileSync(filePath, "utf8");
  } catch {
    process.exit(0);
  }

  const pattern = /decisions\/[0-9]{4}/;
  const hits = [];
  content.split(/\r?\n/).forEach((line, i) => {
    if (pattern.test(line)) {
      hits.push(`  line ${i + 1}: ${line.trim()}`);
    }
  });

  if (hits.length > 0) {
    const reason = [
      "CLAUDE.md forbids restating an ADR's decision/rationale in a code comment",
      "(link from the PR/commit instead, trust the reader to open the ADR).",
      `Found a literal ADR citation in ${filePath}:`,
      ...hits,
      "Remove the comment entirely (not just shortened to a bare link) before continuing.",
    ].join("\n");
    console.log(JSON.stringify({ decision: "block", reason }));
  }

  process.exit(0);
});
