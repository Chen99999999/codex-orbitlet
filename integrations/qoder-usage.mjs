import { createRequire } from 'node:module';
import { pathToFileURL } from 'node:url';

const target = process.argv[2];
const packages = {
  qoder: '@qoder-ai/qoder-agent-sdk',
  'qoder-cn': '@qodercn-ai/qodercn-agent-sdk',
};
if (!Object.hasOwn(packages, target)) process.exit(2);

const resolveFromAdapter = createRequire(new URL(`./${target}/package.json`, import.meta.url));
const controller = new AbortController();
const timeout = setTimeout(() => controller.abort(), 20_000);
let session;
let release;
async function* noPrompt() {
  await new Promise(resolve => { release = resolve; });
}
const number = value => Number.isFinite(value) ? value : undefined;
function bucket(value, totalKey) {
  if (!value) return undefined;
  return {
    [totalKey]: number(value[totalKey]),
    remaining: number(value.remaining),
    used: number(value.used),
    available: typeof value.available === 'boolean' ? value.available : undefined,
  };
}

try {
  const sdk = await import(pathToFileURL(resolveFromAdapter.resolve(packages[target])).href);
  const tokenName = target === 'qoder-cn' ? 'QODERCN_PERSONAL_ACCESS_TOKEN' : 'QODER_PERSONAL_ACCESS_TOKEN';
  const auth = process.env[tokenName]?.trim() ? sdk.accessTokenFromEnv(tokenName) : sdk.qodercliAuth();
  session = sdk.query({
    prompt: noPrompt(),
    options: {
      auth, cwd: process.cwd(), abortController: controller,
      settingSources: [], tools: [], skills: [], mcpServers: {}, strictMcpConfig: true,
      persistSession: false,
    },
  });
  await session.initializationResult();
  const usage = await session.getUsageInfo();
  if (!usage) throw new Error('No Qoder usage data');
  process.stdout.write('ORBITLET_USAGE ' + JSON.stringify({
    userQuota: bucket(usage.userQuota, 'total'),
    addOnQuota: bucket(usage.addOnQuota, 'total'),
    orgResourcePackage: bucket(usage.orgResourcePackage, 'cap'),
    expiresAt: number(usage.expiresAt),
  }) + '\n');
} catch {
  process.exitCode = 1;
} finally {
  clearTimeout(timeout);
  release?.();
  await session?.close();
}
