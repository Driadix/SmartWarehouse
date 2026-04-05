import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import SwaggerParser from '@apidevtools/swagger-parser';
import YAML from 'yaml';
import Ajv2020 from 'ajv/dist/2020.js';
import addFormats from 'ajv-formats';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const openApiPath = path.resolve(__dirname, '../../docs/api/northbound/openapi-v0.yaml');
const asyncApiPath = path.resolve(__dirname, '../../docs/api/southbound/asyncapi-v0.yaml');
const southboundSchemaDirectory = path.resolve(__dirname, '../../docs/api/schemas/southbound');

function isObject(value) {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}

function createAjv() {
  const ajv = new Ajv2020({
    strict: false,
    allErrors: true,
    validateFormats: true
  });

  addFormats(ajv);
  return ajv;
}

function formatErrors(errors = []) {
  return errors
    .map((error) => {
      const instancePath = error.instancePath || '/';
      return `${instancePath} ${error.message}`;
    })
    .join('; ');
}

function validateValue(ajv, schema, value, location, failures) {
  const validate = ajv.compile(schema);
  const valid = validate(value);

  if (!valid) {
    failures.push(`${location}: ${formatErrors(validate.errors)}`);
  }
}

function looksLikeSchema(node) {
  if (!isObject(node)) {
    return false;
  }

  return [
    '$schema',
    '$ref',
    'type',
    'properties',
    'required',
    'items',
    'allOf',
    'anyOf',
    'oneOf',
    'enum',
    'const',
    'format',
    'additionalProperties'
  ].some((key) => key in node);
}

function walkObjects(node, location, visitor) {
  if (Array.isArray(node)) {
    node.forEach((item, index) => walkObjects(item, `${location}/${index}`, visitor));
    return;
  }

  if (!isObject(node)) {
    return;
  }

  visitor(node, location);

  for (const [key, value] of Object.entries(node)) {
    if (key === 'example' || key === 'examples') {
      continue;
    }

    walkObjects(value, `${location}/${key}`, visitor);
  }
}

function collectSchemaExamples(node, location) {
  const examples = [];

  if ('example' in node) {
    examples.push({
      location: `${location}/example`,
      value: node.example
    });
  }

  if (Array.isArray(node.examples)) {
    node.examples.forEach((example, index) => {
      examples.push({
        location: `${location}/examples/${index}`,
        value: example
      });
    });
  }

  return examples;
}

function collectSchemaBoundExamples(node, location) {
  const examples = [];

  if ('example' in node) {
    examples.push({
      location: `${location}/example`,
      value: node.example
    });
  }

  if (!isObject(node.examples)) {
    return examples;
  }

  for (const [name, example] of Object.entries(node.examples)) {
    if (isObject(example) && 'externalValue' in example && !('value' in example)) {
      continue;
    }

    examples.push({
      location: `${location}/examples/${name}`,
      value: isObject(example) && 'value' in example ? example.value : example
    });
  }

  return examples;
}

async function loadSouthboundSchemas() {
  const entries = await fs.readdir(southboundSchemaDirectory, { withFileTypes: true });
  const schemaFiles = entries
    .filter((entry) => entry.isFile() && entry.name.endsWith('.json'))
    .map((entry) => path.join(southboundSchemaDirectory, entry.name))
    .sort();

  const schemas = [];

  for (const filePath of schemaFiles) {
    const schema = JSON.parse(await fs.readFile(filePath, 'utf8'));
    schemas.push({ filePath, schema });
  }

  return schemas;
}

async function validateOpenApiExamples() {
  const document = await SwaggerParser.dereference(openApiPath);
  const ajv = createAjv();
  const failures = [];
  let checkedExamples = 0;

  walkObjects(document, '#', (node, location) => {
    if (isObject(node.schema)) {
      for (const example of collectSchemaBoundExamples(node, location)) {
        checkedExamples += 1;
        validateValue(ajv, node.schema, example.value, example.location, failures);
      }
    }

    if (looksLikeSchema(node)) {
      for (const example of collectSchemaExamples(node, location)) {
        checkedExamples += 1;
        validateValue(ajv, node, example.value, example.location, failures);
      }
    }
  });

  if (failures.length > 0) {
    throw new Error(
      ['OpenAPI example validation failed:', ...failures.map((failure) => `- ${failure}`)].join('\n')
    );
  }

  console.log(`OpenAPI example validation passed: ${checkedExamples} example(s) checked.`);
}

async function validateSouthboundSchemaExamples() {
  const schemas = await loadSouthboundSchemas();
  const ajv = createAjv();
  const failures = [];
  let checkedExamples = 0;

  for (const { filePath, schema } of schemas) {
    ajv.addSchema(schema, schema.$id ?? filePath);
    ajv.addSchema(schema, filePath);
  }

  for (const { filePath, schema } of schemas) {
    walkObjects(schema, path.basename(filePath), (node, location) => {
      if (!looksLikeSchema(node)) {
        return;
      }

      for (const example of collectSchemaExamples(node, location)) {
        checkedExamples += 1;
        validateValue(ajv, node, example.value, example.location, failures);
      }
    });
  }

  if (failures.length > 0) {
    throw new Error(
      ['Southbound JSON Schema example validation failed:', ...failures.map((failure) => `- ${failure}`)].join('\n')
    );
  }

  console.log(`Southbound JSON Schema example validation passed: ${checkedExamples} example(s) checked.`);
}

async function validateAsyncApiExamples() {
  const asyncApiSource = await fs.readFile(asyncApiPath, 'utf8');
  const document = YAML.parseDocument(asyncApiSource);

  if (document.errors.length > 0) {
    throw document.errors[0];
  }

  const asyncApi = document.toJS();
  const schemas = await loadSouthboundSchemas();
  const ajv = createAjv();
  const schemasByPath = new Map();
  const failures = [];
  let checkedExamples = 0;
  const asyncApiDirectory = path.dirname(asyncApiPath);

  for (const { filePath, schema } of schemas) {
    ajv.addSchema(schema, schema.$id ?? filePath);
    ajv.addSchema(schema, filePath);
    schemasByPath.set(filePath, schema);
  }

  for (const [messageName, message] of Object.entries(asyncApi?.components?.messages ?? {})) {
    const payloadRef = message?.payload?.$ref;
    if (typeof payloadRef !== 'string') {
      continue;
    }

    const schemaPath = path.resolve(asyncApiDirectory, payloadRef);
    const schema = schemasByPath.get(schemaPath);
    if (!schema) {
      continue;
    }

    if ('example' in message) {
      checkedExamples += 1;
      validateValue(ajv, schema, message.example, `components/messages/${messageName}/example`, failures);
    }

    if (!Array.isArray(message.examples)) {
      continue;
    }

    message.examples.forEach((example, index) => {
      const exampleValue = isObject(example) && 'payload' in example ? example.payload : example;
      checkedExamples += 1;
      validateValue(
        ajv,
        schema,
        exampleValue,
        `components/messages/${messageName}/examples/${index}`,
        failures
      );
    });
  }

  if (failures.length > 0) {
    throw new Error(
      ['AsyncAPI example validation failed:', ...failures.map((failure) => `- ${failure}`)].join('\n')
    );
  }

  console.log(`AsyncAPI example validation passed: ${checkedExamples} example(s) checked.`);
}

try {
  await validateOpenApiExamples();
  await validateSouthboundSchemaExamples();
  await validateAsyncApiExamples();
} catch (error) {
  console.error(error.message);
  process.exit(1);
}
