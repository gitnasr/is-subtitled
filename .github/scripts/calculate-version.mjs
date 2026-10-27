import fs from "node:fs";
import path from "node:path";
import { execSync } from "node:child_process";
import { changedFiles, decideBump, getRepoDir, readCommits } from "./release-lib.mjs";

const repoDir = getRepoDir();
const isTagPush = process.env.IS_TAG_PUSH === "true" || (process.env.GITHUB_REF || "").startsWith("refs/tags/v");
const dryRun = process.env.INPUT_DRY_RUN === "true";
const bumpType = (process.env.INPUT_BUMP_TYPE || "auto").toLowerCase();
const customInput = (process.env.INPUT_CUSTOM_VERSION || "").trim().replace(/^v/, "");

function runGit(cmd) {
  try {
    return execSync(`git -C "${repoDir}" ${cmd}`, {
      encoding: "utf8",
      stdio: ["pipe", "pipe", "ignore"],
    }).trim();
  } catch {
    return "";
  }
}

// 1. Determine base version from latest tag and IsSubtitled.csproj
let latestTag = runGit('describe --tags --abbrev=0 --match "v[0-9]*.[0-9]*.[0-9]*" --exclude "*-*"');
let latestTagVersion = latestTag.replace(/^v/, "");

let csprojVersion = "1.0.0";
const csprojPath = path.join(repoDir, "IsSubtitled.csproj");
try {
  const csprojContent = fs.readFileSync(csprojPath, "utf8");
  const match = /<Version>(.*?)<\/Version>/.exec(csprojContent);
  if (match) csprojVersion = match[1].trim();
} catch {}

function parseSemver(v) {
  const [semverPart] = v.split("-");
  let [major, minor, patch] = (semverPart || "1.0.0").split(".").map(Number);
  if (isNaN(major)) major = 1;
  if (isNaN(minor)) minor = 0;
  if (isNaN(patch)) patch = 0;
  return { major, minor, patch };
}

const tagSemver = parseSemver(latestTagVersion || csprojVersion);
const csprojSemver = parseSemver(csprojVersion);

let baseMajor = Math.max(tagSemver.major, csprojSemver.major);
let baseMinor =
  tagSemver.major > csprojSemver.major
    ? tagSemver.minor
    : csprojSemver.major > tagSemver.major
    ? csprojSemver.minor
    : Math.max(tagSemver.minor, csprojSemver.minor);
let basePatch =
  tagSemver.major === csprojSemver.major && tagSemver.minor === csprojSemver.minor
    ? Math.max(tagSemver.patch, csprojSemver.patch)
    : tagSemver.major > csprojSemver.major ||
      (tagSemver.major === csprojSemver.major && tagSemver.minor > csprojSemver.minor)
    ? tagSemver.patch
    : csprojSemver.patch;

let targetVersion = "";
let shouldRelease = true;

if (isTagPush) {
  const refTag = (process.env.GITHUB_REF || "").replace("refs/tags/", "");
  targetVersion = (refTag || latestTag).replace(/^v/, "");
  console.log(`Tag push detected: using tag version ${targetVersion}`);
} else if (bumpType === "custom" && customInput) {
  targetVersion = customInput;
  console.log(`Custom version specified: ${targetVersion}`);
} else {
  // If csproj is already bumped strictly ahead of latest tag, use csproj version!
  const isCsprojAhead =
    csprojSemver.major > tagSemver.major ||
    (csprojSemver.major === tagSemver.major && csprojSemver.minor > tagSemver.minor) ||
    (csprojSemver.major === tagSemver.major && csprojSemver.minor === tagSemver.minor && csprojSemver.patch > tagSemver.patch);

  if (isCsprojAhead && latestTagVersion) {
    targetVersion = `${csprojSemver.major}.${csprojSemver.minor}.${csprojSemver.patch}`;
    console.log(`csproj version (${targetVersion}) is already ahead of latest tag (${latestTagVersion}), using it.`);
  } else {
    // Determine bump automatically from commits or explicit bumpType
    let bump = bumpType;
    let reasons = [];
    if (bump === "auto") {
      const commits = readCommits(latestTag ? `${latestTag}..HEAD` : "HEAD", repoDir);
      const files = changedFiles(latestTag, "HEAD", repoDir);
      const decision = decideBump(commits, files);
      bump = decision.bump;
      reasons = decision.reasons;
      console.log(`Automatic bump decision: ${bump} (${reasons.join("; ")})`);
    }

    if (bump === "none") {
      shouldRelease = false;
      targetVersion = `${baseMajor}.${baseMinor}.${basePatch}`;
      console.log(`No release needed: ${reasons.join("; ")}`);
    } else if (bump === "major") {
      targetVersion = `${baseMajor + 1}.0.0`;
    } else if (bump === "minor") {
      targetVersion = `${baseMajor}.${baseMinor + 1}.0`;
    } else {
      // patch
      targetVersion = `${baseMajor}.${baseMinor}.${basePatch + 1}`;
    }
  }
}

const tag = `v${targetVersion}`;

console.log(`Target version: ${targetVersion} (${tag})`);
console.log(`Previous tag: ${latestTag || "none"}`);
console.log(`Should release: ${shouldRelease}`);

// Write outputs for GitHub Actions
const outputFile = process.env.GITHUB_OUTPUT;
if (outputFile) {
  fs.appendFileSync(outputFile, `version=${targetVersion}\n`);
  fs.appendFileSync(outputFile, `tag=${tag}\n`);
  fs.appendFileSync(outputFile, `prev_tag=${latestTag}\n`);
  fs.appendFileSync(outputFile, `should_release=${shouldRelease}\n`);
  fs.appendFileSync(outputFile, `is_tag_push=${isTagPush}\n`);
  fs.appendFileSync(outputFile, `dry_run=${dryRun}\n`);
}
