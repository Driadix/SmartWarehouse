import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import YAML from 'yaml';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(scriptDir, '..', '..');
const knowledgeMapDir = path.join(repoRoot, 'eng', 'knowledge-map');
const buildDir = path.join(knowledgeMapDir, 'build');
const outputFile = path.join(buildDir, 'knowledge-map.json');

function readUtf8(filePath) {
  return fs.readFileSync(filePath, 'utf8');
}

function parseYamlFile(filePath) {
  return YAML.parse(readUtf8(filePath));
}

function ensure(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

function toRepoPath(filePath) {
  return path.relative(repoRoot, filePath).replace(/\\/g, '/');
}

function normalizePath(filePath) {
  return filePath.replace(/\\/g, '/');
}

function slugify(value) {
  return value
    .normalize('NFKD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .replace(/-{2,}/g, '-');
}

function normalizeSearchText(value) {
  return value
    .normalize('NFKC')
    .toLowerCase()
    .replace(/[^\p{L}\p{N}-]+/gu, ' ')
    .replace(/\s+/g, ' ')
    .trim();
}

function dedupeStrings(values) {
  const seen = new Set();
  const result = [];

  for (const value of values ?? []) {
    if (typeof value !== 'string') {
      continue;
    }

    const trimmed = value.trim();
    if (!trimmed) {
      continue;
    }

    const key = trimmed.toLocaleLowerCase('ru-RU');
    if (seen.has(key)) {
      continue;
    }

    seen.add(key);
    result.push(trimmed);
  }

  return result;
}

function globToRegExp(pattern) {
  const escaped = pattern.replace(/[.+^${}()|[\]\\]/g, '\\$&');
  return new RegExp(`^${escaped.replace(/\*/g, '.*').replace(/\?/g, '.')}$`, 'i');
}

function walkFiles(rootDir) {
  const entries = fs.readdirSync(rootDir, { withFileTypes: true });
  const files = [];

  for (const entry of entries) {
    const absolutePath = path.join(rootDir, entry.name);

    if (entry.isDirectory()) {
      files.push(...walkFiles(absolutePath));
      continue;
    }

    if (entry.isFile()) {
      files.push(absolutePath);
    }
  }

  return files;
}

function listMatchingFiles(rootDir, pattern) {
  const matcher = globToRegExp(pattern);

  return walkFiles(rootDir)
    .filter((filePath) => matcher.test(path.basename(filePath)))
    .sort((left, right) => toRepoPath(left).localeCompare(toRepoPath(right)));
}

function extractFirstMarkdownHeading(text) {
  const match = text.match(/^#\s+(.+)$/m);
  return match ? match[1].trim() : null;
}

function extractXmlTagValue(xml, tagName) {
  const match = xml.match(new RegExp(`<${tagName}>([^<]+)</${tagName}>`, 'i'));
  return match ? match[1].trim() : null;
}

function extractXmlAttributeValues(xml, tagName, attributeName) {
  const matcher = new RegExp(`<${tagName}\\s+[^>]*${attributeName}="([^"]+)"[^>]*/?>`, 'gi');
  const values = [];
  let match;

  while ((match = matcher.exec(xml)) !== null) {
    values.push(match[1].trim());
  }

  return values;
}

function collectConstValues(value, propertyName, acc = new Set()) {
  if (Array.isArray(value)) {
    for (const item of value) {
      collectConstValues(item, propertyName, acc);
    }

    return [...acc];
  }

  if (!value || typeof value !== 'object') {
    return [...acc];
  }

  for (const [key, child] of Object.entries(value)) {
    if (
      key === propertyName &&
      child &&
      typeof child === 'object' &&
      typeof child.const === 'string'
    ) {
      acc.add(child.const);
    }

    collectConstValues(child, propertyName, acc);
  }

  return [...acc];
}

function fileStem(filePath) {
  const basename = path.basename(filePath);

  if (basename.endsWith('.schema.json')) {
    return basename.slice(0, -'.schema.json'.length);
  }

  return basename.slice(0, -path.extname(basename).length);
}

function buildDiscoveredId(rule, absolutePath) {
  const stem = fileStem(absolutePath);
  const slug = slugify(stem);
  const prefix = slugify(rule.idPrefix);

  return slug.startsWith(`${prefix}-`) || slug === prefix ? slug : `${prefix}-${slug}`;
}

function isIgnoredPath(repoPath) {
  return repoPath.startsWith('docs/Standards/');
}

function extractMetadata(absolutePath, kind) {
  const basename = path.basename(absolutePath);
  const extension = path.extname(absolutePath).toLowerCase();
  const text = readUtf8(absolutePath);
  let title = null;
  const aliases = [];
  const metadata = {};

  if (extension === '.md') {
    title = extractFirstMarkdownHeading(text);
  }

  if (extension === '.yaml' || extension === '.yml') {
    const doc = YAML.parse(text);

    if (doc?.info?.title) {
      title = String(doc.info.title).trim();
    }

    if (typeof doc?.topologyId === 'string') {
      title ??= doc.topologyId;
      metadata.topologyId = doc.topologyId;
      aliases.push(doc.topologyId);
    }

    if (Array.isArray(doc?.services)) {
      metadata.services = doc.services;
    }

    if (doc?.services && typeof doc.services === 'object' && !Array.isArray(doc.services)) {
      metadata.services = Object.keys(doc.services);
      aliases.push(...metadata.services);
    }

    if (doc?.tasks && typeof doc.tasks === 'object') {
      metadata.tasks = Object.keys(doc.tasks);
      aliases.push(...metadata.tasks);
    }

    if (typeof doc?.version === 'string' || typeof doc?.version === 'number') {
      metadata.version = String(doc.version);
    }
  }

  if (extension === '.json') {
    const doc = JSON.parse(text);

    if (typeof doc.title === 'string') {
      title = doc.title.trim();
    }

    if (typeof doc.$id === 'string') {
      metadata.schemaId = doc.$id;
      aliases.push(doc.$id);
      aliases.push(path.basename(doc.$id));
    }

    const messageTypes = collectConstValues(doc, 'messageType');
    if (messageTypes.length > 0) {
      metadata.messageTypes = messageTypes;
      aliases.push(...messageTypes);
    }
  }

  if (basename === 'global.json') {
    const doc = JSON.parse(text);
    if (typeof doc?.sdk?.version === 'string') {
      metadata.sdkVersion = doc.sdk.version;
      aliases.push(doc.sdk.version);
      title = `.NET SDK ${doc.sdk.version}`;
    }
  }

  if (basename === 'package.json') {
    const doc = JSON.parse(text);
    if (typeof doc?.name === 'string') {
      metadata.packageName = doc.name;
      aliases.push(doc.name);
      title ??= doc.name;
    }
  }

  if (basename === 'go.mod') {
    const moduleMatch = text.match(/^module\s+(.+)$/m);
    const versionMatch = text.match(/^go\s+(.+)$/m);

    if (moduleMatch) {
      metadata.module = moduleMatch[1].trim();
      aliases.push(metadata.module);
      title ??= metadata.module;
    }

    if (versionMatch) {
      metadata.goVersion = versionMatch[1].trim();
      aliases.push(`go ${metadata.goVersion}`);
    }
  }

  if (extension === '.csproj') {
    const targetFramework = extractXmlTagValue(text, 'TargetFramework');
    const targetFrameworks = extractXmlTagValue(text, 'TargetFrameworks');
    const projectReferences = extractXmlAttributeValues(text, 'ProjectReference', 'Include');

    metadata.projectName = fileStem(absolutePath);
    aliases.push(metadata.projectName);
    title ??= metadata.projectName;

    if (targetFramework) {
      metadata.targetFramework = targetFramework;
      aliases.push(targetFramework);
    }

    if (targetFrameworks) {
      metadata.targetFrameworks = targetFrameworks.split(';').map((item) => item.trim()).filter(Boolean);
      aliases.push(...metadata.targetFrameworks);
    }

    if (projectReferences.length > 0) {
      metadata.projectReferences = projectReferences.map((value) => normalizePath(value));
    }
  }

  if (basename === '.env.example') {
    metadata.variables = text
      .split(/\r?\n/)
      .map((line) => line.trim())
      .filter((line) => line && !line.startsWith('#') && line.includes('='))
      .map((line) => line.split('=')[0].trim());
    aliases.push(...metadata.variables);
  }

  title ??= fileStem(absolutePath);

  if (kind === 'adr') {
    const adrMatch = path.basename(absolutePath).match(/^(ADR-\d+)/i);
    if (adrMatch) {
      aliases.push(adrMatch[1].toUpperCase());
    }
  }

  return {
    title,
    aliases: dedupeStrings(aliases),
    metadata,
  };
}

function finalizeArtifact(baseArtifact, options = {}) {
  const absolutePath = path.join(repoRoot, baseArtifact.path);
  const repoPath = normalizePath(baseArtifact.path);

  ensure(fs.existsSync(absolutePath), `Artifact path does not exist: ${repoPath}`);
  ensure(!isIgnoredPath(repoPath), `Artifact path is outside scope: ${repoPath}`);

  const extracted = extractMetadata(absolutePath, baseArtifact.kind);
  const basename = path.basename(repoPath);
  const logicalStem = fileStem(repoPath);

  return {
    id: baseArtifact.id,
    kind: baseArtifact.kind,
    title: baseArtifact.title ?? extracted.title ?? logicalStem,
    path: repoPath,
    basename,
    authority: Number(baseArtifact.authority),
    aliases: dedupeStrings([
      ...(baseArtifact.aliases ?? []),
      basename,
      logicalStem,
      ...extracted.aliases,
    ]),
    tags: dedupeStrings(baseArtifact.tags ?? []),
    sourceOfTruthFor: [...(baseArtifact.sourceOfTruthFor ?? [])],
    generated: options.generated ?? false,
    discovery: options.discovery ?? null,
    metadata: extracted.metadata,
  };
}

function buildArtifacts() {
  const configPath = path.join(knowledgeMapDir, 'artifacts.yaml');
  const config = parseYamlFile(configPath);

  ensure(config?.schemaVersion === 'v0', 'Unsupported artifacts schemaVersion');
  ensure(Array.isArray(config.artifacts), 'artifacts.yaml must contain artifacts array');
  ensure(Array.isArray(config.discovery), 'artifacts.yaml must contain discovery array');

  const artifacts = [];
  const ids = new Set();

  for (const artifact of config.artifacts) {
    const finalized = finalizeArtifact(artifact);
    ensure(!ids.has(finalized.id), `Duplicate artifact id: ${finalized.id}`);
    ids.add(finalized.id);
    artifacts.push(finalized);
  }

  for (const rule of config.discovery) {
    const rootDir = path.join(repoRoot, rule.root);
    ensure(fs.existsSync(rootDir), `Discovery root does not exist: ${normalizePath(rule.root)}`);

    const files = listMatchingFiles(rootDir, rule.pattern);
    for (const filePath of files) {
      const repoPath = toRepoPath(filePath);
      ensure(!isIgnoredPath(repoPath), `Discovered path is outside scope: ${repoPath}`);

      const discovered = finalizeArtifact(
        {
          id: buildDiscoveredId(rule, filePath),
          kind: rule.kind,
          path: repoPath,
          authority: rule.authority,
          aliases: [],
          tags: rule.tags ?? [],
        },
        {
          generated: true,
          discovery: {
            root: normalizePath(rule.root),
            pattern: rule.pattern,
            kind: rule.kind,
          },
        },
      );

      ensure(!ids.has(discovered.id), `Duplicate artifact id: ${discovered.id}`);
      ids.add(discovered.id);
      artifacts.push(discovered);
    }
  }

  return artifacts.sort((left, right) => left.id.localeCompare(right.id));
}

function buildEntities(artifactsById) {
  const configPath = path.join(knowledgeMapDir, 'entities.yaml');
  const config = parseYamlFile(configPath);

  ensure(config?.schemaVersion === 'v0', 'Unsupported entities schemaVersion');
  ensure(Array.isArray(config.entities), 'entities.yaml must contain entities array');

  const ids = new Set();
  const entities = config.entities.map((entity) => {
    ensure(!ids.has(entity.id), `Duplicate entity id: ${entity.id}`);
    ids.add(entity.id);

    const authoritativeArtifacts = [...(entity.authoritativeArtifacts ?? [])];
    const relatedArtifacts = [...(entity.relatedArtifacts ?? [])];

    for (const artifactId of [...authoritativeArtifacts, ...relatedArtifacts]) {
      ensure(artifactsById.has(artifactId), `Unknown artifact reference in entity ${entity.id}: ${artifactId}`);
    }

    const authorityValues = authoritativeArtifacts
      .map((artifactId) => artifactsById.get(artifactId)?.authority ?? 0)
      .filter((value) => Number.isFinite(value));

    return {
      id: entity.id,
      kind: entity.kind,
      canonical: entity.canonical,
      aliases: dedupeStrings(entity.aliases ?? []),
      tags: dedupeStrings(entity.tags ?? []),
      authority: authorityValues.length > 0 ? Math.max(...authorityValues) : 50,
      authoritativeArtifacts,
      relatedArtifacts,
    };
  });

  return entities.sort((left, right) => left.id.localeCompare(right.id));
}

function buildLinks(knownIds) {
  const configPath = path.join(knowledgeMapDir, 'links.yaml');
  const config = parseYamlFile(configPath);

  ensure(config?.schemaVersion === 'v0', 'Unsupported links schemaVersion');
  ensure(Array.isArray(config.links), 'links.yaml must contain links array');

  return config.links.map((link) => {
    ensure(knownIds.has(link.from), `Unknown link source: ${link.from}`);
    ensure(knownIds.has(link.to), `Unknown link target: ${link.to}`);
    ensure(typeof link.relation === 'string' && link.relation.trim(), `Invalid link relation for ${link.from} -> ${link.to}`);

    return {
      from: link.from,
      to: link.to,
      relation: link.relation.trim(),
    };
  });
}

function buildArtifactSearchRecord(artifact) {
  const exactTerms = dedupeStrings([
    artifact.id,
    artifact.title,
    artifact.basename,
    fileStem(artifact.path),
    ...artifact.aliases,
  ]);

  const keywords = dedupeStrings([
    artifact.kind,
    artifact.path,
    ...artifact.tags,
    ...(artifact.sourceOfTruthFor ?? []),
    ...(artifact.metadata?.messageTypes ?? []),
    artifact.metadata?.topologyId,
    artifact.metadata?.projectName,
    artifact.metadata?.packageName,
    artifact.metadata?.sdkVersion,
    ...(artifact.metadata?.services ?? []),
    ...(artifact.metadata?.tasks ?? []),
  ]);

  return {
    id: artifact.id,
    recordType: 'artifact',
    kind: artifact.kind,
    displayName: artifact.title,
    authority: artifact.authority,
    path: artifact.path,
    exactTerms,
    keywords,
    searchText: dedupeStrings([...exactTerms, ...keywords]),
  };
}

function buildEntitySearchRecord(entity) {
  const exactTerms = dedupeStrings([entity.id, entity.canonical, ...entity.aliases]);
  const keywords = dedupeStrings([
    entity.kind,
    ...entity.tags,
    ...entity.authoritativeArtifacts,
    ...entity.relatedArtifacts,
  ]);

  return {
    id: entity.id,
    recordType: 'entity',
    kind: entity.kind,
    displayName: entity.canonical,
    authority: entity.authority,
    path: null,
    exactTerms,
    keywords,
    searchText: dedupeStrings([...exactTerms, ...keywords]),
  };
}

function main() {
  const artifacts = buildArtifacts();
  const artifactsById = new Map(artifacts.map((artifact) => [artifact.id, artifact]));
  const entities = buildEntities(artifactsById);
  const knownIds = new Set([
    ...artifacts.map((artifact) => artifact.id),
    ...entities.map((entity) => entity.id),
  ]);
  const links = buildLinks(knownIds);
  const searchRecords = [
    ...artifacts.map(buildArtifactSearchRecord),
    ...entities.map(buildEntitySearchRecord),
  ].sort((left, right) => {
    const authorityDiff = right.authority - left.authority;
    if (authorityDiff !== 0) {
      return authorityDiff;
    }

    return left.id.localeCompare(right.id);
  });

  for (const record of searchRecords) {
    record.normalizedExactTerms = record.exactTerms.map(normalizeSearchText);
    record.normalizedKeywords = record.keywords.map(normalizeSearchText);
    record.normalizedSearchText = record.searchText.map(normalizeSearchText).join(' ');
  }

  fs.mkdirSync(buildDir, { recursive: true });

  const output = {
    schemaVersion: 'v0',
    generatedAt: new Date().toISOString(),
    sourceFiles: {
      artifacts: 'eng/knowledge-map/artifacts.yaml',
      entities: 'eng/knowledge-map/entities.yaml',
      links: 'eng/knowledge-map/links.yaml',
    },
    artifacts,
    entities,
    links,
    searchRecords,
  };

  fs.writeFileSync(outputFile, `${JSON.stringify(output, null, 2)}\n`, 'utf8');

  console.log(`Knowledge map built: ${toRepoPath(outputFile)}`);
  console.log(`Artifacts: ${artifacts.length}`);
  console.log(`Entities: ${entities.length}`);
  console.log(`Links: ${links.length}`);
}

try {
  main();
} catch (error) {
  console.error(error instanceof Error ? error.message : String(error));
  process.exitCode = 1;
}
