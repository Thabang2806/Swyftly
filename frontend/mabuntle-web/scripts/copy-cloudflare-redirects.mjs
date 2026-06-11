import { copyFileSync, existsSync, mkdirSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';

const [, , appName, outputDirectory] = process.argv;

if (!appName || !outputDirectory) {
  throw new Error('Usage: node scripts/copy-cloudflare-redirects.mjs <client|seller|admin> <dist-output-browser-dir>');
}

const redirectsFile = resolve('public', 'redirects', appName, '_redirects');
const outputFile = resolve(outputDirectory, '_redirects');

if (!existsSync(redirectsFile)) {
  throw new Error(`Redirects file not found: ${redirectsFile}`);
}

mkdirSync(dirname(outputFile), { recursive: true });
copyFileSync(redirectsFile, outputFile);
console.log(`Copied ${redirectsFile} -> ${outputFile}`);

