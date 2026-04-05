import fs from 'node:fs/promises';
import SwaggerParser from '@apidevtools/swagger-parser';
import YAML from 'yaml';

const target = '../../docs/api/northbound/openapi-v0.yaml';
const supportedVersions = new Set([
  '3.1.0',
  '3.1.1',
  '3.1.2',
  '3.0.0',
  '3.0.1',
  '3.0.2',
  '3.0.3',
  '3.0.4'
]);

function assertShape(document) {
  if (!document || typeof document !== 'object') {
    throw new Error('Root YAML node must be a mapping/object.');
  }

  if (typeof document.openapi !== 'string' || document.openapi.length === 0) {
    throw new Error('Missing required root property "openapi".');
  }

  if (!document.info || typeof document.info !== 'object') {
    throw new Error('Missing required root property "info".');
  }

  if (!document.paths || typeof document.paths !== 'object') {
    throw new Error('Missing required root property "paths".');
  }

  if (!document.components || typeof document.components !== 'object') {
    throw new Error('Missing required root property "components".');
  }
}

try {
  const source = await fs.readFile(new URL(target, import.meta.url), 'utf8');
  const parsed = YAML.parseDocument(source);

  if (parsed.errors.length > 0) {
    throw parsed.errors[0];
  }

  const document = parsed.toJS();
  assertShape(document);

  if (supportedVersions.has(document.openapi)) {
    await SwaggerParser.validate(target);
    console.log(`OpenAPI validation passed: ${target}`);
  } else {
    console.log(
      `OpenAPI syntax and shape validation passed: ${target}. Semantic validation is skipped for unsupported version ${document.openapi}.`
    );
  }
} catch (error) {
  console.error(`OpenAPI validation failed: ${target}`);
  console.error(error.message);
  process.exit(1);
}
