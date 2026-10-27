// Shared release logic for Is Subtitled: commit classification, bump decision, release notes.
// Used by calculate-version.mjs and generate-release-notes.mjs.
// Runs with Node builtins only (no npm install required).
import { execFileSync } from "node:child_process";
import fs from "node:fs";

export function getRepoDir() {
  const arg = process.argv.find((a) => a.startsWith("--cwd="));
  return arg ? arg.split("=")[1] : (process.env.REPO_DIR || process.cwd());
}

// --- file signals ---------------------------------------------------------

const NON_FUNCTIONAL = [
  /^(\.github|\.agents|\.claude|docs)\//,
  /\.mdx?$/i,
  /^(LICENSE|\.gitignore|\.gitattributes|\.editorconfig)$/,
];
export const isFunctional = (file) => !NON_FUNCTIONAL.some((re) => re.test(file));

// --- commit classification ------------------------------------------------

const CONVENTIONAL = /^([a-z]+)(?:\(([^)]*)\))?(!)?:\s*(.+)$/i;
const TYPE_CATEGORY = {
  feat: "feature",
  fix: "fix",
  perf: "perf",
  docs: "docs",
  refactor: "internal",
  chore: "internal",
  ci: "internal",
  build: "internal",
  test: "internal",
  style: "internal",
  revert: "internal",
};

const FEATURE_VERBS = ["add", "introduce", "implement", "support", "enable", "allow", "create"];
const FIX_VERBS = ["fix", "resolve", "prevent", "handle", "correct", "repair"];
const PERF_RE = /\b(faster|speed(s|ed)? up|optimi[sz]\w*|incremental(ly)?|performance)\b/i;
const FIX_WORD_RE = /\b(bug|crash(es)?|regression)\b/i;
const DOCS_VERBS = ["document"];

const PAST = {
  add: "Added", fix: "Fixed", remove: "Removed", make: "Made", open: "Opened",
  update: "Updated", improve: "Improved", persist: "Persisted", mark: "Marked",
  backfill: "Backfilled", show: "Showed", use: "Used", replace: "Replaced",
  prevent: "Prevented", handle: "Handled", resolve: "Resolved", implement: "Implemented",
  introduce: "Introduced", enable: "Enabled", allow: "Allowed", create: "Created",
  restore: "Restored", move: "Moved", rename: "Renamed", change: "Changed",
  support: "Supported", correct: "Corrected", repair: "Repaired", polish: "Polished",
  enhance: "Enhanced", refactor: "Refactored", prototype: "Prototyped", document: "Documented",
};

const isRelease = (subject) => /^chore\(release\)/i.test(subject);
const isDependabot = (c) =>
  /dependabot/i.test(c.author || "") || /^(build|chore)\(deps[^)]*\)/i.test(c.subject) || /^Bump /.test(c.subject);

export function releaseNote(body) {
  const m = /^Release-Note:[ \t]*(.+)$/im.exec(body || "");
  return m ? m[1].trim() : "";
}

function humanize(desc) {
  const text = desc.replace(/\.$/, "").trim();
  const [first, ...rest] = text.split(/\s+/);
  const past = PAST[first.toLowerCase()];
  const out = past ? [past, ...rest].join(" ") : text;
  return out.charAt(0).toUpperCase() + out.slice(1);
}

export function classify(commit) {
  const subject = (commit.subject || "").trim();
  const body = commit.body || "";
  const out = { hash: commit.hash, skip: false, category: "internal", breaking: false, functional: true, text: "", pr: "" };

  if (!subject || isRelease(subject) || /^Merge /.test(subject)) return { ...out, skip: true };

  const pr = /\s*\(#(\d+)\)\s*$/.exec(subject);
  out.pr = pr ? pr[1] : "";
  const clean = subject.replace(/\s*\(#\d+\)\s*$/, "");

  const conv = CONVENTIONAL.exec(clean);
  const isConv = conv && TYPE_CATEGORY[conv[1].toLowerCase()];
  const desc = isConv ? conv[4] : clean;
  const firstWord = desc.split(/\s+/)[0].toLowerCase();

  out.breaking =
    Boolean(isConv && conv[3]) || /BREAKING[ -]CHANGE/.test(subject) || /^BREAKING[ -]CHANGE:/m.test(body);

  if (isDependabot(commit)) out.category = "deps";
  else if (isConv) out.category = TYPE_CATEGORY[conv[1].toLowerCase()];
  else if (FEATURE_VERBS.includes(firstWord)) out.category = "feature";
  else if (FIX_VERBS.includes(firstWord) || FIX_WORD_RE.test(desc)) out.category = "fix";
  else if (PERF_RE.test(desc)) out.category = "perf";
  else if (DOCS_VERBS.includes(firstWord)) out.category = "docs";

  const files = commit.files;
  if (files && files.length) {
    out.functional = files.some(isFunctional);
    if (!out.functional && ["feature", "fix", "perf"].includes(out.category)) out.category = "internal";
    if (!out.functional && !isDependabot(commit) && files.every((f) => /\.mdx?$|^docs\//i.test(f))) out.category = "docs";
  }

  const note = releaseNote(body);
  if (/^(skip|none|n\/a)$/i.test(note)) return { ...out, skip: true };
  out.text = note || humanize(desc);
  out.hasNote = Boolean(note);
  return out;
}

export function decideBump(commits, files) {
  const live = commits.map(classify).filter((c) => !c.skip);
  if (!commits.length) return { bump: "none", reasons: ["no commits since last tag"] };
  if (files && files.length > 0 && !files.some(isFunctional)) {
    return { bump: "patch", reasons: ["docs or meta files updated"] };
  }
  const relevant = live.filter((c) => c.functional);
  const breaking = relevant.filter((c) => c.breaking);
  if (breaking.length) return { bump: "major", reasons: breaking.map((c) => `breaking: ${c.text}`) };
  const feats = relevant.filter((c) => c.category === "feature");
  if (feats.length) return { bump: "minor", reasons: feats.map((c) => `feature: ${c.text}`) };
  return { bump: "patch", reasons: [`${relevant.length || 1} change(s)`] };
}

// --- git log parsing ------------------------------------------------------

export function readCommits(range, repoDir = getRepoDir()) {
  try {
    const raw = execFileSync(
      "git",
      ["-C", repoDir, "log", "--format=%H%x1f%an%x1f%s%x1f%b%x1e", range],
      { encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] }
    );
    if (!raw.trim()) return [];
    return raw
      .split("\x1e")
      .map((entry) => entry.trim())
      .filter(Boolean)
      .map((entry) => {
        const [hash, author, subject, body] = entry.split("\x1f");
        return { hash, author, subject, body: body || "" };
      });
  } catch {
    return [];
  }
}

export function changedFiles(prev, current = "HEAD", repoDir = getRepoDir()) {
  try {
    const range = prev ? `${prev}..${current}` : current;
    const raw = execFileSync("git", ["-C", repoDir, "diff", "--name-only", range], {
      encoding: "utf8",
      stdio: ["ignore", "pipe", "ignore"],
    });
    return raw.split("\n").map((f) => f.trim()).filter(Boolean);
  } catch {
    return [];
  }
}

// --- release notes markdown -----------------------------------------------

const SECTIONS = [
  ["feature", "🚀 Highlights & Features"],
  ["fix", "🐛 Bug Fixes"],
  ["perf", "⚡ Performance Improvements"],
  ["docs", "📝 Documentation"],
  ["internal", "🔧 Maintenance & Internal"],
];

export function buildNotes(commits, { repo = "gitnasr/is-subtitled", prev = "", tag = "HEAD" } = {}) {
  const classified = commits.map(classify).filter((c) => !c.skip);
  const byCategory = new Map();
  for (const c of classified) {
    const list = byCategory.get(c.category) || [];
    list.push(c);
    byCategory.set(c.category, list);
  }

  const lines = [];
  for (const [key, title] of SECTIONS) {
    const items = byCategory.get(key) || [];
    if (!items.length) continue;
    lines.push(`### ${title}`);
    for (const item of items) {
      const shortSha = item.hash ? item.hash.slice(0, 7) : "";
      const commitLink = shortSha ? `([${shortSha}](https://github.com/${repo}/commit/${item.hash}))` : "";
      lines.push(`* ${item.text} ${commitLink}`.trim());
    }
    lines.push("");
  }

  if (lines.length === 0) {
    lines.push("### Changes");
    lines.push(`* Automated release for \`${tag}\`.`);
    lines.push("");
  }

  if (prev && tag) {
    lines.push(`**Full Changelog**: https://github.com/${repo}/compare/${prev}...${tag}`);
    lines.push("");
  }

  return lines.join("\n").trim();
}
