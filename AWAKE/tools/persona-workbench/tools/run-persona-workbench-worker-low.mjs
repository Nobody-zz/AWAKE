import crypto from "node:crypto";
import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
const projectRoot = path.resolve(scriptDirectory, "..");
const fixturePath = path.join(process.env.USERPROFILE || process.env.HOME || ".", ".codex", "skills", "persona-workbench-ollama-pretest", "references", "test-fixtures.md");
const localWorkerPath = path.join(process.env.USERPROFILE || process.env.HOME || ".", ".codex", "local-worker", "local-worker.mjs");
const modelName = "gpt-oss:20b";
const modelDigest = "17052f91a42e97930aa6e28a6c6c06a983e6a58dbb00434885a0cf5313e376f7";
const shortExpected = { bytes: 270, sha256: "3B82F664F26F11862C7DA899668AAA0635461F5CC3FDA711A8D7622CBA5EB968" };
const longExpected = { bytes: 3888, sha256: "8234670F470A8F663D5436111B39CF10D97AE9AA1D123E6FF7C090561F93B752" };
const expansionInstruction = "PersonaWorkbench expand-description diagnostic. Return only concise editable character prose in the same language as the input. Copy the first sentence from the input exactly through the first Chinese full stop inclusive. Preserve explicit identity, personality, public behavior, private behavior, contradictions, pressure reactions, relationships, and commitments. Compress repeated appearance details for long input. Do not invent facts. Do not output JSON, Markdown, DSL, tags, IDs, metadata, analysis, or commentary.";
const conversionInstruction = "PersonaWorkbench convert-to-dsl diagnostic. Return exactly one JSON object and nothing else. Extract only source-grounded sparse evidence for identityFacts, publicDescription, privateDescription, contradictionDescription, reaction, commitment, axes, and flags. Copy prose evidence word-for-word from the input in original order; never paraphrase or invent. Omit unsupported fields. Do not output DSL, metadata, IDs, names, tags, Markdown, or commentary.";

function parseArguments() {
  const values = new Map();
  for (let index = 2; index < process.argv.length; index += 1) {
    const argument = process.argv[index];
    if (!argument.startsWith("--")) throw new Error(`Unexpected argument: ${argument}`);
    const key = argument.slice(2);
    const value = process.argv[index + 1];
    if (!value || value.startsWith("--")) throw new Error(`Missing value for --${key}`);
    values.set(key, value);
    index += 1;
  }
  const report = values.get("report");
  if (!report) throw new Error("--report is required.");
  return { report: path.resolve(report) };
}

function hashBytes(bytes) {
  return crypto.createHash("sha256").update(bytes).digest("hex").toUpperCase();
}

function hashText(value) {
  return hashBytes(Buffer.from(value, "utf8"));
}

function parseFixtures(value) {
  const shortMatch = value.match(/## Short Fixture\s*\r?\n\r?\n```text\r?\n([\s\S]*?)\r?\n```/);
  const longMatch = value.match(/## Long Fixture\s*\r?\n\r?\n```text\r?\n([\s\S]*?)\r?\n```/);
  if (!shortMatch || !longMatch) throw new Error("FIXTURE_MISSING");
  const fixtures = {
    short: { text: shortMatch[1], bytes: Buffer.byteLength(shortMatch[1], "utf8"), sha256: hashText(shortMatch[1]) },
    long: { text: longMatch[1], bytes: Buffer.byteLength(longMatch[1], "utf8"), sha256: hashText(longMatch[1]) },
  };
  for (const [name, expected] of Object.entries({ short: shortExpected, long: longExpected })) {
    if (fixtures[name].bytes !== expected.bytes || fixtures[name].sha256 !== expected.sha256) throw new Error(`FIXTURE_DRIFT:${name}`);
  }
  return fixtures;
}

function publicError(caught) {
  return { code: caught?.code || "INTERNAL_ERROR", message: caught?.message || String(caught) };
}

function isInDoubt(error) {
  return ["OLLAMA_TIMEOUT", "OLLAMA_NETWORK_ERROR"].includes(error.code);
}

function summarizeResult(result, outputFormat) {
  const value = result.result;
  const serialized = typeof value === "string" ? value : JSON.stringify(value);
  return {
    accepted: true,
    outputFormat,
    outputBytes: Buffer.byteLength(serialized || "", "utf8"),
    outputSha256: hashText(serialized || ""),
    jsonValid: outputFormat === "json_object_v1" && value !== null && typeof value === "object" && !Array.isArray(value),
  };
}

async function main() {
  const { report } = parseArguments();
  const fixtures = parseFixtures(await fs.readFile(fixturePath, "utf8"));
  const worker = await import(pathToFileURL(localWorkerPath).href);
  const taskId = `pwb-pretest-${Date.now()}`;
  await worker.createLocalTask({
    task_id: taskId,
    title: "PersonaWorkbench fixed-fixture Worker-low diagnostic",
    decision_model: "gpt-5.6-luna",
    decision_provider: "custom",
    decision_endpoint: "",
  });
  const stages = [];
  const outputs = new Map();
  const run = async (stage, fixtureName, operation, inputText, outputFormat, instruction, numPredict, inputSource) => {
    const inputBytes = Buffer.byteLength(inputText, "utf8");
    const started = performance.now();
    const requestId = `pwb-${stage}-${Date.now()}`;
    try {
      const response = await worker.askLocalModel({
        task_id: taskId,
        request_id: requestId,
        batch_id: `pwb-${fixtureName}`,
        instruction,
        input: inputText,
        output_format: outputFormat,
        constraints: "Use only the supplied input. Keep output bounded. thinking_level is low.",
        num_predict: numPredict,
        thinking_level: "low",
      });
      const summary = summarizeResult(response, outputFormat);
      if (summary.accepted) outputs.set(stage, response.result);
      stages.push({
        stage,
        fixture: fixtureName,
        operation,
        inputSource,
        input: { bytes: inputBytes, sha256: hashText(inputText) },
        output: summary,
        durationMs: Math.round((performance.now() - started) * 10) / 10,
        error: null,
      });
      return response.result;
    } catch (caught) {
      const error = publicError(caught);
      stages.push({
        stage,
        fixture: fixtureName,
        operation,
        inputSource,
        input: { bytes: inputBytes, sha256: hashText(inputText) },
        output: { accepted: false, outputFormat, outputBytes: 0, outputSha256: null, jsonValid: false },
        durationMs: Math.round((performance.now() - started) * 10) / 10,
        error,
      });
      return null;
    }
  };

  for (const fixtureName of ["short", "long"]) {
    const fixture = fixtures[fixtureName];
    await run(`expand-${fixtureName}`, fixtureName, "expand", fixture.text, "text", expansionInstruction, 4096, `${fixtureName}-fixture`);
    const expansion = outputs.get(`expand-${fixtureName}`);
    const conversionInput = typeof expansion === "string" && expansion.trim() ? expansion : fixture.text;
    const conversionSource = typeof expansion === "string" && expansion.trim() ? `expand-${fixtureName}-output` : `${fixtureName}-fixture-fallback`;
    await run(`convert-${fixtureName}`, fixtureName, "convert", conversionInput, "json_object_v1", conversionInstruction, 4096, conversionSource);
  }

  const failed = stages.filter((stage) => !stage.output.accepted);
  const inDoubt = failed.some((stage) => stage.error && isInDoubt(stage.error));
  const reportValue = {
    schemaVersion: "persona-workbench.worker-low.v1",
    generatedAtUtc: new Date().toISOString(),
    taskId,
    model: { name: modelName, digest: modelDigest, thinkingLevel: "low" },
    fixtures: {
      short: { bytes: fixtures.short.bytes, sha256: fixtures.short.sha256 },
      long: { bytes: fixtures.long.bytes, sha256: fixtures.long.sha256 },
    },
    stages,
    verdict: failed.length === 0 ? "PASS" : inDoubt ? "IN_DOUBT" : "FAIL",
  };
  await fs.mkdir(path.dirname(report), { recursive: true });
  await fs.writeFile(report, `${JSON.stringify(reportValue, null, 2)}\n`, "utf8");
  process.stdout.write(`${JSON.stringify({ report, verdict: reportValue.verdict, taskId })}\n`);
  if (reportValue.verdict !== "PASS") process.exitCode = 1;
}

main().catch((caught) => {
  process.stderr.write(`${JSON.stringify(publicError(caught))}\n`);
  process.exitCode = 1;
});
