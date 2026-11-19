import { execFileSync } from 'node:child_process';
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

// A conservative publication check, not a replacement for human review or secret rotation.
// Reports paths/rules/line numbers only; never prints matching values.
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const staged = process.argv.includes('--staged');
const args = staged
  ? ['ls-files', '--cached', '-z']
  : ['ls-files', '--cached', '--others', '--exclude-standard', '-z'];
const files = [...new Set(execFileSync('git', args, { cwd: root, encoding: 'utf8' }).split('\0').filter(Boolean))].sort();
const findings = [];
const inventory = [];
const rules = [
  ['GitHub token', /\b(?:gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{30,})\b/g],
  ['JWT-shaped token', /\beyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\b/g],
  ['Private key', /-----BEGIN (?:[A-Z]+ )?PRIVATE KEY-----/g],
  ['AWS access key', /\b(?:AKIA|ASIA)[A-Z0-9]{16}\b/g],
  ['Credential-bearing URL', /https?:\/\/[^/\s"'<>]+:[^/\s"'<>]+@/g],
  ['Connection-string password', /(?:Password|Pwd)\s*=\s*[^;\s"'$<>]+/gi],
  ['Literal credential assignment', /(?:client[_:-]?secret|access[_:-]?token|refresh[_:-]?token|api[_:-]?key|password|writekey)["']?\s*[:=]\s*["']([^"'\r\n]{8,})["']/gi],
];

for (const file of files) {
  const absolute = path.join(root, file);
  const bytes = staged
    ? execFileSync('git', ['show', `:${file}`], { cwd: root, maxBuffer: 20 * 1024 * 1024 })
    : readFileSync(absolute);
  inventory.push({ path: file, bytes: bytes.length, sha256: createHash('sha256').update(bytes).digest('hex') });
  const isExample = /(?:^|\/)\.env\.example$/.test(file) || /\.example\.json$/.test(file);
  if ((/(?:^|\/)(?:bin|obj|node_modules|dist|build|artifacts|test-results|storage|\.git|\.angular|\.vscode)(?:\/|$)/i.test(file)) ||
      /\.(?:db(?:-\w+)?|sqlite\w*|log|zip|7z|rar|pfx|p12|pem|key|crt|cer|jks|keystore)$/i.test(file) ||
      /(?:^|\/)\.env(?:\..*)?$/.test(file) && !isExample ||
      /(?:^|\/)appsettings\..*\.json$/i.test(file) && !isExample)
    findings.push({ file, rule: 'Forbidden runtime/private artifact' });
  if (bytes.length > 5 * 1024 * 1024) findings.push({ file, rule: 'Unexpected file larger than 5 MiB' });
  if (/\.(?:png|jpg|jpeg|gif)$/i.test(file)) continue; // Real screenshots are reviewed visually.
  if (bytes.includes(0)) { findings.push({ file, rule: 'Unexpected binary file' }); continue; }
  const source = bytes.toString('utf8');
  for (const [rule, expression] of rules) {
    expression.lastIndex = 0;
    for (const match of source.matchAll(expression)) {
      const fixture = [
        'backend/OpsPulse.Tests/ProbeSafetyTests.cs',
        'frontend/src/app/core/api-config.spec.ts',
        'scripts/review-publication.mjs',
      ].includes(file) && rule === 'Credential-bearing URL' && match[0] === 'https://user:secret@';
      if (!fixture) findings.push({ file, rule, line: source.slice(0, match.index).split('\n').length });
    }
  }
}

mkdirSync(path.join(root, 'artifacts'), { recursive: true });
writeFileSync(path.join(root, 'artifacts', staged ? 'staged-review.json' : 'publication-review.json'),
  JSON.stringify({ mode: staged ? 'staged' : 'working tree', files: inventory, findings }, null, 2) + '\n');
if (findings.length) {
  console.error(JSON.stringify({ reviewedFiles: files.length, findings }, null, 2));
  process.exitCode = 1;
} else {
  console.log(`PASS: reviewed ${files.length} publication files; no configured secret patterns or forbidden artifacts found.`);
}
