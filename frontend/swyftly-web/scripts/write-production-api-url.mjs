import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';

const apiBaseUrl = (
  process.env.SWYFTLY_API_BASE_URL
  ?? process.env.SWYFTLY_WEB_API_BASE_URL
  ?? process.env.API_BASE_URL
  ?? ''
).trim().replace(/\/+$/, '');

if (!apiBaseUrl) {
  throw new Error('Set SWYFTLY_API_BASE_URL to the production API origin before building for Cloudflare Pages.');
}

const url = new URL(apiBaseUrl);
if (url.protocol !== 'https:' || url.hostname === 'localhost' || url.hostname.endsWith('.example')) {
  throw new Error('SWYFTLY_API_BASE_URL must be an external HTTPS production API origin.');
}

const target = resolve('src/environments/environment.cloudflare.ts');
mkdirSync(dirname(target), { recursive: true });
writeFileSync(
  target,
  `export const environment = {\n  production: true,\n  apiBaseUrl: ${JSON.stringify(apiBaseUrl)}\n};\n`,
  'utf8');

console.log(`Configured Angular production API base URL: ${apiBaseUrl}`);
