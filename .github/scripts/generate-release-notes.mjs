import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { buildNotes, changedFiles, getRepoDir, readCommits } from "./release-lib.mjs";

const repoDir = getRepoDir();
const args = process.argv.slice(2);
const flag = (name) => args.find((a) => a.startsWith(`--${name}=`))?.split("=").slice(1).join("=");
const hasFlag = (name) => args.includes(`--${name}`);

const [prevArg = "", tagArg = "HEAD"] = args.filter((a) => !a.startsWith("--"));
const prev = prevArg.trim();
const tag = tagArg.trim();
const out = flag("out") || "release-notes.md";
const repo = flag("repo") || process.env.GITHUB_REPOSITORY || "gitnasr/is-subtitled";
const updateChangelog = hasFlag("update-changelog") || flag("update-changelog") !== "false";

const version = tag.replace(/^v/, "");

function resolveGitRef(ref, fallback = "HEAD") {
  if (!ref) return fallback;
  try {
    execFileSync("git", ["-C", repoDir, "rev-parse", "--verify", ref], { stdio: "ignore" });
    return ref;
  } catch {
    return fallback;
  }
}

// When invoked before 'git tag' has been created, 'tag' does not exist as a git ref yet.
// In that case, fall back to 'HEAD' so all new commits are discovered.
const gitTarget = resolveGitRef(tag, "HEAD");
const gitPrev = prev ? resolveGitRef(prev, "") : "";
const range = gitPrev ? `${gitPrev}..${gitTarget}` : gitTarget;

const commits = readCommits(range, repoDir);
const files = gitPrev ? changedFiles(gitPrev, gitTarget, repoDir) : [];
const notes = buildNotes(commits, { repo, prev: gitPrev || prev, tag, files });

const outPath = path.isAbsolute(out) ? out : path.join(repoDir, out);
fs.writeFileSync(outPath, notes, "utf8");
console.log(`Wrote ${outPath} (${commits.length} commits, ${range})`);

// Update CHANGELOG.md if requested
const changelogPath = path.join(repoDir, "CHANGELOG.md");
if (updateChangelog && fs.existsSync(changelogPath)) {
  const changelogContent = fs.readFileSync(changelogPath, "utf8");
  const versionHeader = `## [${version}]`;

  if (!changelogContent.includes(versionHeader)) {
    const today = new Date().toISOString().split("T")[0];
    const compareLink = prev
      ? `https://github.com/${repo}/compare/${prev}...${tag}`
      : `https://github.com/${repo}/releases/tag/${tag}`;

    const newSection = [
      `## [${version}](${compareLink}) (${today})`,
      "",
      notes.replace(/\*\*Full Changelog\*\*:.*/g, "").trim(),
      "",
    ].join("\n");

    let updated = "";
    if (changelogContent.startsWith("# Changelog")) {
      updated = changelogContent.replace("# Changelog\n", `# Changelog\n\n${newSection}\n`);
    } else {
      updated = `# Changelog\n\n${newSection}\n\n${changelogContent}`;
    }

    fs.writeFileSync(changelogPath, updated, "utf8");
    console.log(`Updated ${changelogPath} with entry for [${version}]`);
  } else {
    console.log(`CHANGELOG.md already contains entry for [${version}], skipping update.`);
  }
}
